using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MTPSKIT.BuildTools
{
    public static class RainParentIntersectionMaterialValidation
    {
        const string Evidence = "Docs/玩法参考设计/雨中冒险2/原版依据/";
        const string Folder = "Assets/RainSource/ParentEffects/";
        static readonly string[] Supported = Array.Empty<string>();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var shader = Shader.Find("RainActors/ParentIntersectionRemap");
                Require(shader && !ShaderUtil.ShaderHasError(shader), "Intersection shader failed to compile.");
                ValidateGpu(shader);
                ImportMaterials(shader);
                BindPreparedIntersectionSlots();
                Debug.Log("RAIN_PARENT_INTERSECTION_MATERIALS_PASSED other_shader_families_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_INTERSECTION_MATERIALS_FAILED");
                EditorApplication.Exit(1);
            }
        }

        static void ImportMaterials(Shader shader)
        {
            var report = JObject.Parse(File.ReadAllText(Evidence + "Parent完整特效材质.json"));
            var textures = JObject.Parse(File.ReadAllText(Evidence + "Parent共享特效贴图校验.json"));
            int count = 0;
            foreach (var record in report["materials"])
            {
                if ((string)record["shaderName"] != "Hopoo Games/FX/Cloud Intersection Remap") continue;
                var data = record["data"];
                string name = (string)data["m_Name"];
                string path = Folder + name + "-Intersection.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                foreach (var keyword in data["m_ValidKeywords"])
                    Require(Supported.Contains((string)keyword), name + ": unimplemented original keyword " + keyword);
                material.shaderKeywords = data["m_ValidKeywords"].Values<string>().ToArray();
                foreach (var pair in data["m_SavedProperties"]["m_Floats"])
                    if (material.HasProperty((string)pair[0])) material.SetFloat((string)pair[0], (float)pair[1]);
                foreach (var pair in data["m_SavedProperties"]["m_Colors"])
                {
                    string property = (string)pair[0]; var color = pair[1];
                    if (material.HasProperty(property))
                        material.SetVector(property, new Vector4((float)color["r"], (float)color["g"], (float)color["b"], (float)color["a"]));
                }
                foreach (var reference in record["textureReferences"])
                {
                    string property = (string)reference["property"];
                    if (!material.HasProperty(property)) continue;
                    var texture = textures["textures"].First(t => (string)t["cab"] == (string)reference["cab"] &&
                        (long)t["pathId"] == (long)reference["pathId"]);
                    material.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>((string)texture["assetPath"]));
                }
                foreach (var pair in data["m_SavedProperties"]["m_TexEnvs"])
                {
                    string property = (string)pair[0]; var uv = pair[1];
                    if (!material.HasProperty(property)) continue;
                    material.SetTextureScale(property, new Vector2((float)uv["m_Scale"]["x"], (float)uv["m_Scale"]["y"]));
                    material.SetTextureOffset(property, new Vector2((float)uv["m_Offset"]["x"], (float)uv["m_Offset"]["y"]));
                }
                int queue = (int)data["m_CustomRenderQueue"];
                material.renderQueue = queue;
                EditorUtility.SetDirty(material);
                count++;
                Debug.Log("RAIN_PARENT_INTERSECTION_MATERIAL_CHECKED " + name);
            }
            Require(count == 1, "Expected one original Cloud Intersection Remap material in this dependency set.");
            AssetDatabase.SaveAssets();
        }

        static void BindPreparedIntersectionSlots()
        {
            var native = JObject.Parse(File.ReadAllText(Evidence + "Parent原生粒子校验源.json"));
            var materials = JObject.Parse(File.ReadAllText(Evidence + "Parent完整特效材质.json"));
            int bound = 0, pending = 0;
            foreach (var effect in native["effects"])
            {
                string name = (string)effect["name"];
                var root = PrefabUtility.LoadPrefabContents(Folder + name + "-CloudPartial.prefab");
                root.name = name;
                try
                {
                    foreach (var component in effect["nativeComponents"])
                    {
                        if ((string)component["type"] != "ParticleSystemRenderer") continue;
                        string path = (string)component["path"];
                        var renderer = root.transform.Find(path.Substring(path.IndexOf('/') + 1)).GetComponent<ParticleSystemRenderer>();
                        var slots = renderer.sharedMaterials;
                        foreach (var reference in native["unboundReferences"].Where(r =>
                            (string)r["effect"] == name && (long)r["objectPathId"] == (long)component["pathId"]))
                        {
                            string property = (string)reference["property"];
                            if (!property.Contains(".m_Materials[")) continue;
                            var record = materials["materials"].First(r => (string)r["cab"] == (string)reference["cab"] &&
                                (long)r["pathId"] == (long)reference["pathId"]);
                            if ((string)record["shaderName"] != "Hopoo Games/FX/Cloud Intersection Remap") { if ((string)record["shaderName"] != "Hopoo Games/FX/Cloud Remap") pending++; continue; }
                            int bracket = property.LastIndexOf('[');
                            int slot = int.Parse(property.Substring(bracket + 1, property.Length - bracket - 2));
                            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + (string)record["data"]["m_Name"] + "-Intersection.mat");
                            Require(material && material.shader.name == "RainActors/ParentIntersectionRemap", "Missing prepared intersection material.");
                            slots[slot] = material;
                            bound++;
                        }
                        renderer.sharedMaterials = slots;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + name + "-CloudIntersectionPartial.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Require(bound == 2 && pending == 14, "Expected partial intersection binding with other shader families still pending.");
            Debug.Log($"RAIN_PARENT_INTERSECTION_SLOTS_BOUND count={bound} remainingOtherShaderSlots={pending} production_wiring_pending=True");
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
                for (int test=0;test<16;test++)
                {
                    bool additive = (test & 1) != 0;
                    float inv = (test & 2) == 0 ? 1.2f : 0;
                    float sceneDepth = (test & 4) == 0 ? 1.3f : .8f;
                    if (test == 12) sceneDepth = 5;
                    float rimStrength = (test & 8) == 0 ? 0 : .8f;
                    float sign = (test & 1) == 0 ? 1 : -1;
                    mesh.normals = Enumerable.Repeat(Vector3.forward * sign,4).ToArray();
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
                        mask *= Sample(one,u*.6f+.1f+.37f*.07f,v*.9f+.02f-.37f*.09f).a;
                        mask *= Sample(two,u*.9f+.04f+.37f*.12f,v*.8f+.07f+.37f*.03f).a;
                        mask *= 4*1.4f*.8f;
                        float depthTerm = inv > 0 ? Mathf.Pow(Mathf.Clamp01((sceneDepth-1)*inv),.75f) : 1;
                        var view = new Vector3(1-2*u,1-2*v,3).normalized;
                        float rim = Mathf.Pow(1-Mathf.Clamp01(Mathf.Abs(view.z*sign)),2.69f);
                        float coordinate = Mathf.Clamp01((1+rim*rimStrength-depthTerm)*mask*.56f);
                        Color expected = Sample(ramp,coordinate,.5f)*new Color(.8f,.6f,.4f,.9f);
                        expected.r*=2; expected.g*=2; expected.b*=2;
                        if(additive) expected+=background;
                        Color actual=result.GetPixel(x,y);
                        float error=Mathf.Max(Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),
                            Mathf.Abs(expected.b-actual.b),Mathf.Abs(expected.a-actual.a));
                        Require(error<.003f,$"Intersection GPU mismatch case={test} x={x} y={y} expected={expected} actual={actual} error={error}");
                        checks++;
                    }
                }
                Require(!ShaderUtil.ShaderHasError(shader),"Intersection variant compilation failed.");
                Debug.Log("RAIN_PARENT_INTERSECTION_GPU_CHECKED cases=16 samples="+checks+" includes_original_additive_blend=True");
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

