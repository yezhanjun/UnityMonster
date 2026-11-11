using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerMaterialValidation
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var shader = Shader.Find("RainItems/AntlersIntersectionRemap");
                Require(shader && !ShaderUtil.ShaderHasError(shader), "Antler triplanar shader compilation failed.");
                ValidateGpu(shader);
                ImportAndBind(shader);
                Debug.Log("RAIN_ANTLER_TRIPLANAR_PASSED production_wiring_and_rendered_fidelity_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_ANTLER_TRIPLANAR_FAILED");
                EditorApplication.Exit(1);
            }
        }

        static void ImportAndBind(Shader intersection)
        {
            const string folder = "Assets/Resources/RainImported/Antlers/";
            var cloud = Shader.Find("RainActors/ParentCloudRemap");
            Require(cloud && !ShaderUtil.ShaderHasError(cloud), "Shared Cloud Remap shader unavailable.");
            var source = JObject.Parse(File.ReadAllText("Docs/FrontEnd/antlers-particle-material-sources.json"));
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var record in source["materials"])
            {
                var data = record["source"];
                bool isBase = (string)record["name"] == "matElusiveAntlersBuffBase";
                string[] keywords = data["m_ValidKeywords"].Values<string>().OrderBy(k => k).ToArray();
                Require(keywords.SequenceEqual(isBase ? new[] { "TRIPLANAR" } : new[] { "CLOUDOFFSET", "USE_CLOUDS" }), "Unsupported original keyword set.");
                string path = folder + (string)record["name"] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(isBase ? intersection : cloud); AssetDatabase.CreateAsset(material, path); }
                material.shader = isBase ? intersection : cloud;
                // The base shader implements its one TRIPLANAR program directly.
                material.shaderKeywords = isBase ? Array.Empty<string>() : keywords;
                foreach (var pair in data["m_SavedProperties"]["m_Floats"])
                    if (material.HasProperty((string)pair[0])) material.SetFloat((string)pair[0], (float)pair[1]);
                foreach (var pair in data["m_SavedProperties"]["m_Colors"])
                {
                    var c = pair[1];
                    if (material.HasProperty((string)pair[0]))
                        material.SetVector((string)pair[0], new Vector4((float)c["r"], (float)c["g"], (float)c["b"], (float)c["a"]));
                }
                foreach (var binding in record["textureBindings"])
                {
                    string property = (string)binding["property"];
                    if (!material.HasProperty(property)) continue;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>((string)binding["texture"]["file"]);
                    Require(texture, "Missing original texture " + binding["texture"]["file"]);
                    material.SetTexture(property, texture);
                }
                foreach (var pair in data["m_SavedProperties"]["m_TexEnvs"])
                {
                    string property = (string)pair[0];
                    if (!material.HasProperty(property)) continue;
                    var uv = pair[1];
                    if ((long)uv["m_Texture"]["m_PathID"] == 0) material.SetTexture(property, null);
                    material.SetTextureScale(property, new Vector2((float)uv["m_Scale"]["x"], (float)uv["m_Scale"]["y"]));
                    material.SetTextureOffset(property, new Vector2((float)uv["m_Offset"]["x"], (float)uv["m_Offset"]["y"]));
                }
                material.renderQueue = (int)data["m_CustomRenderQueue"];
                EditorUtility.SetDirty(material);
                materials.Add((string)record["pathID"], material);
                Debug.Log("RAIN_ANTLER_MATERIAL_IMPORTED " + record["name"]);
            }
            Require(materials.Count == 3, "Expected three original materials.");
            AssetDatabase.SaveAssets();
            var root = PrefabUtility.LoadPrefabContents(folder + "ElusiveAntlersWorldParticles-Parameters.prefab");
            try
            {
                var nodes = JObject.Parse(File.ReadAllText("Docs/FrontEnd/antlers-world-particles-source.json"))["nodes"];
                var transforms = new Transform[nodes.Count()];
                int bound = 0;
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i] = i == 0 ? root.transform : transforms[(int)nodes[i]["parent"]].Find((string)nodes[i]["name"]);
                    Require(transforms[i], "Native particle hierarchy missing.");
                    if (nodes[i]["sourceMaterials"] == null) continue;
                    var slots = nodes[i]["sourceMaterials"].Select(reference => materials[(string)reference["m_PathID"]]).ToArray();
                    var renderer = transforms[i].GetComponent<ParticleSystemRenderer>();
                    Require(renderer && slots.Length == 1, "Unexpected renderer slots.");
                    renderer.sharedMaterials = slots;
                    bound++;
                }
                Require(bound == 3, "Expected three bound renderer slots.");
                PrefabUtility.SaveAsPrefabAsset(root, folder + "ElusiveAntlersWorldParticles-Materials.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "ElusiveAntlersWorldParticles-Materials.prefab");
            Require(saved && saved.GetComponentsInChildren<ParticleSystemRenderer>().Length == 3, "Saved material prefab unavailable.");
            foreach (var renderer in saved.GetComponentsInChildren<ParticleSystemRenderer>())
                Require(renderer.sharedMaterial && !ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), "Saved renderer lost material.");
            Debug.Log("RAIN_ANTLER_MATERIAL_BINDINGS_PASSED materials=3 rendererSlots=3 production_wiring_pending=True");
        }

        static void ValidateGpu(Shader shader)
        {
            const int size = 32;
            var main = Pattern(0); var one = Pattern(1); var two = Pattern(2); var ramp = Pattern(3);
            var depth = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var result = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            var material = new Material(shader);
            var mesh = new Mesh();
            var vertexColor = new Color(.1f,.2f,.15f,.7f);
            mesh.vertices = new[] { new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0) };
            // TEXCOORD0 deliberately differs: the original instruction reads TEXCOORD1.
            mesh.uv = Enumerable.Repeat(Vector2.zero,4).ToArray();
            mesh.uv2 = new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
            var previous = RenderTexture.active;
            var previousDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
            var previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            var previousZ = Shader.GetGlobalVector("_ZBufferParams");
            var previousProjection = Shader.GetGlobalVector("_ProjectionParams");
            try
            {
                material.SetTexture("_MainTex",main); material.SetTexture("_Cloud1Tex",one);
                material.SetTexture("_Cloud2Tex",two); material.SetTexture("_RemapTex",ramp);
                material.SetTextureScale("_MainTex",new Vector2(.8f,.7f));
                material.SetTextureOffset("_MainTex",new Vector2(.03f,.09f));
                material.SetTextureScale("_Cloud1Tex",new Vector2(.6f,.9f));
                material.SetTextureOffset("_Cloud1Tex",new Vector2(.1f,.02f));
                material.SetTextureScale("_Cloud2Tex",new Vector2(.9f,.8f));
                material.SetTextureOffset("_Cloud2Tex",new Vector2(.04f,.07f));
                material.SetColor("_TintColor",new Color(.8f,.6f,.4f,.9f));
                material.SetFloat("_AlphaBoost",1.4f); material.SetFloat("_ExternalAlpha",.8f);
                material.SetFloat("_IntersectionStrength",.56f); material.SetFloat("_SoftPower",.75f);
                material.SetFloat("_RimPower",2.69f); material.SetFloat("_Boost",2);
                material.SetFloat("_ValidationTime",.37f);
                material.SetVector("_CutoffScroll",new Vector4(.07f,-.09f,.12f,.03f));
                material.SetFloat("_Cull",0); material.SetFloat("_SrcBlendFloat",1);
                int checks = 0;
                // Crossing the depth boundary, saturated far depth, disabled-depth branch,
                // both normal directions, rim on/off, and the original additive blend.
                for (int test=0;test<24;test++)
                {
                    bool additive = (test & 1) != 0;
                    float inv = (test & 2) == 0 ? 1.2f : 0;
                    float sceneDepth = (test & 4) == 0 ? 1.3f : .8f;
                    if (test == 12) sceneDepth = 5;
                    float rimStrength = (test & 8) == 0 ? 0 : .8f;
                    float sign = (test & 1) == 0 ? 1 : -1;
                    Vector3 normal = test < 8 ? Vector3.forward * sign : test < 16 ? new Vector3(.3f,-.8f,.5f).normalized * sign : new Vector3(-.7f,.2f,-.4f).normalized * sign;
                    mesh.normals = Enumerable.Repeat(normal,4).ToArray();
                    // RGB cannot influence phase or ramp in the original BASE program.
                    mesh.colors = Enumerable.Repeat(new Color(.8f-test*.02f,.3f,.9f,vertexColor.a),4).ToArray();
                    material.SetFloat("_InvFade",inv); material.SetFloat("_RimStrength",rimStrength);
                    material.SetFloat("_DstBlendFloat",additive ? 1 : 0);
                    depth.SetPixel(0,0,new Color(1/sceneDepth,0,0,1)); depth.Apply();
                    Color background = new Color(.13f,.21f,.07f,.4f);
                    var commands = new CommandBuffer();
                    try
                    {
                        commands.SetRenderTarget(target); commands.ClearRenderTarget(false,true,background);
                        commands.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                        commands.SetGlobalVector("_ProjectionParams",new Vector4(1,.1f,100,.01f));
                        commands.SetGlobalVector("_WorldSpaceCameraPos",new Vector4(0,0,3,1));
                        commands.SetGlobalVector("_ZBufferParams",new Vector4(0,0,1,0));
                        commands.SetGlobalTexture("_CameraDepthTexture",depth);
                        commands.DrawMesh(mesh,Matrix4x4.identity,material,0,0);
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    finally { commands.Release(); }
                    RenderTexture.active = target; result.ReadPixels(new Rect(0,0,size,size),0,0); result.Apply();
                    for(int y=5;y<size;y+=9) for(int x=5;x<size;x+=9)
                    {
                        float u=(x+.5f)/size,v=(y+.5f)/size;
                        float mask = Sample(main,u*.8f+.03f,v*.7f+.09f).a * .9f * vertexColor.a;
                        Vector3 world = new Vector3(2*u-1,2*v-1,0);
                        Vector3 w = new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                        w /= w.x+w.y+w.z;
                        float first = w.x*Sample(one,(world.z+.37f*.07f)*.6f,(world.y-.37f*.09f)*.9f).a
                            + w.y*Sample(one,(world.x+.37f*.07f)*.6f,(world.z-.37f*.09f)*.9f).a
                            + w.z*Sample(one,(world.x+.37f*.07f)*.6f,(world.y-.37f*.09f)*.9f).a;
                        float second = w.x*Sample(two,(world.z+.37f*.12f)*.9f,(world.y+.37f*.03f)*.8f).a
                            + w.y*Sample(two,(world.x+.37f*.12f)*.9f,(world.z+.37f*.03f)*.8f).a
                            + w.z*Sample(two,(world.x+.37f*.12f)*.9f,(world.y+.37f*.03f)*.8f).a;
                        mask *= first*second;
                        mask *= 4*1.4f*.8f;
                        float depthTerm = inv > 0 ? Mathf.Pow(Mathf.Clamp01((sceneDepth-1)*inv),.75f) : 1;
                        var view = new Vector3(1-2*u,1-2*v,3).normalized;
                        float rim = Mathf.Pow(1-Mathf.Clamp01(Mathf.Abs(Vector3.Dot(view,normal))),2.69f);
                        float coordinate = Mathf.Clamp01((1+rim*rimStrength-depthTerm)*mask*.56f);
                        Color expected = Sample(ramp,coordinate,.5f)*new Color(.8f,.6f,.4f,.9f);
                        expected.r*=2; expected.g*=2; expected.b*=2;
                        if(additive) expected+=background;
                        Color actual=result.GetPixel(x,y);
                        float error=Mathf.Max(Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),
                            Mathf.Abs(expected.b-actual.b),Mathf.Abs(expected.a-actual.a));
                        Require(error<.003f,$"Antler triplanar GPU mismatch case={test} x={x} y={y} expected={expected} actual={actual} error={error}");
                        checks++;
                    }
                }
                Require(!ShaderUtil.ShaderHasError(shader),"Intersection variant compilation failed.");
                Debug.Log("RAIN_ANTLER_TRIPLANAR_GPU_CHECKED cases=24 samples="+checks+" includes_original_additive_blend=True");
            }
            finally
            {
                RenderTexture.active=previous;
                Shader.SetGlobalTexture("_CameraDepthTexture",previousDepth);
                Shader.SetGlobalVector("_WorldSpaceCameraPos",previousCamera);
                Shader.SetGlobalVector("_ZBufferParams",previousZ);
                Shader.SetGlobalVector("_ProjectionParams",previousProjection);
                target.Release();
                foreach(var obj in new UnityEngine.Object[] { main,one,two,ramp,depth,result,target,material,mesh })
                    UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        static Color Sample(Texture2D texture,float u,float v) =>
            texture.GetPixelBilinear(u-.5f/texture.width,v-.5f/texture.height);

        static Texture2D Pattern(int index)
        {
            var texture=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true)
                { wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
            for(int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float u=(x+.5f)/32,v=(y+.5f)/32;
                texture.SetPixel(x,y,index==3 ? new Color(u,u*.5f+.1f,1-u,.2f+.6f*u)
                    : new Color(0,0,0,index==0 ? .1f+.6f*u+.2f*v : index==1 ? .2f+.4f*u : .15f+.5f*v));
            }
            texture.Apply(); return texture;
        }

        static void Require(bool value,string message)
        { if(!value) throw new InvalidOperationException(message); }
    }
}

