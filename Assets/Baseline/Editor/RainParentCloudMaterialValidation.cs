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
    public static class RainParentCloudMaterialValidation
    {
        const string Evidence = "Docs/玩法参考设计/雨中冒险2/原版依据/";
        const string Folder = "Assets/RainSource/ParentEffects/";
        static readonly string[] Supported = { "USE_CLOUDS", "CLOUDOFFSET", "VERTEXCOLOR", "FADECLOSE" };

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var shader = Shader.Find("RainActors/ParentCloudRemap");
                Require(shader && !ShaderUtil.ShaderHasError(shader), "Cloud shader failed to compile.");
                ValidateGpu(shader);
                ImportMaterials(shader);
                BindPreparedCloudSlots();
                Debug.Log("RAIN_PARENT_CLOUD_MATERIALS_PASSED other_shader_families_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_CLOUD_MATERIALS_FAILED");
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
                if ((string)record["shaderName"] != "Hopoo Games/FX/Cloud Remap") continue;
                var data = record["data"];
                string name = (string)data["m_Name"];
                string path = Folder + name + "-Cloud.mat";
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
                Debug.Log("RAIN_PARENT_CLOUD_MATERIAL_CHECKED " + name);
            }
            Require(count == 7, "Expected seven original Cloud Remap materials in this dependency set.");
            AssetDatabase.SaveAssets();
        }

        static void BindPreparedCloudSlots()
        {
            var native = JObject.Parse(File.ReadAllText(Evidence + "Parent原生粒子校验源.json"));
            var materials = JObject.Parse(File.ReadAllText(Evidence + "Parent完整特效材质.json"));
            int bound = 0, pending = 0;
            foreach (var effect in native["effects"])
            {
                string name = (string)effect["name"];
                var root = PrefabUtility.LoadPrefabContents(Folder + name + "-Lifecycle.prefab");
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
                            if ((string)record["shaderName"] != "Hopoo Games/FX/Cloud Remap") { pending++; continue; }
                            int bracket = property.LastIndexOf('[');
                            int slot = int.Parse(property.Substring(bracket + 1, property.Length - bracket - 2));
                            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + (string)record["data"]["m_Name"] + "-Cloud.mat");
                            Require(material && material.shader.name == "RainActors/ParentCloudRemap", "Missing prepared cloud material.");
                            slots[slot] = material;
                            bound++;
                        }
                        renderer.sharedMaterials = slots;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + name + "-CloudPartial.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Require(bound > 0 && pending > 0, "Expected partial cloud binding with other shader families still pending.");
            Debug.Log($"RAIN_PARENT_CLOUD_SLOTS_BOUND count={bound} remainingOtherShaderSlots={pending} production_wiring_pending=True");
        }

        static void ValidateGpu(Shader shader)
        {
            const int size = 32;
            var main = Pattern(0); var one = Pattern(1); var two = Pattern(2); var ramp = Pattern(3);
            var depth = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            depth.SetPixel(0, 0, Color.white); depth.Apply();
            var result = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            var material = new Material(shader);
            var mesh = new Mesh();
            var vertexColor = new Color(.1f, .2f, .15f, .7f);
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.colors = Enumerable.Repeat(vertexColor, 4).ToArray(); mesh.triangles = new[] { 0,1,2,0,2,3 };
            var previous = RenderTexture.active;
            bool previousSoft = Shader.IsKeywordEnabled("SOFTPARTICLES_ON");
            var previousDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
            Vector4 previousZ = Shader.GetGlobalVector("_ZBufferParams");
            try
            {
                material.SetTexture("_MainTex", main); material.SetTexture("_Cloud1Tex", one);
                material.SetTexture("_Cloud2Tex", two); material.SetTexture("_RemapTex", ramp);
                material.SetColor("_TintColor", new Color(.8f, .6f, .4f, .9f));
                material.SetFloat("_AlphaBoost", 1.4f); material.SetFloat("_AlphaBias", .03f);
                material.SetFloat("_ExternalAlpha", .8f); material.SetFloat("_Fade", .9f);
                material.SetFloat("_Boost", 2f); material.SetFloat("_DistortionStrength", .17f);
                material.SetVector("_CutoffScroll", new Vector4(.07f,-.09f,.12f,.03f));
                material.SetFloat("_ValidationTime", .37f);
                material.SetFloat("_SrcBlend", 1); material.SetFloat("_DstBlend", 0); material.SetFloat("_ZTest", 8);
                material.SetFloat("_InvFade", .2f); material.SetFloat("_FadeCloseDistance", .7f);
                int checks = 0;
                // Enumerate selected original branch combinations, including near/depth fade.
                foreach (string flags in new[] { "", "USE_CLOUDS", "VERTEXCOLOR", "USE_CLOUDS VERTEXCOLOR",
                    "USE_CLOUDS CLOUDOFFSET", "USE_CLOUDS CLOUDOFFSET FADECLOSE",
                    "USE_CLOUDS SOFTPARTICLES_ON", "USE_CLOUDS CLOUDOFFSET SOFTPARTICLES_ON",
                    "USE_CLOUDS CLOUDOFFSET FADECLOSE SOFTPARTICLES_ON" })
                {
                    material.shaderKeywords = flags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool soft = flags.Contains("SOFTPARTICLES_ON");
                    if (soft) Shader.EnableKeyword("SOFTPARTICLES_ON"); else Shader.DisableKeyword("SOFTPARTICLES_ON");
                    var commands = new CommandBuffer();
                    try
                    {
                        commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.clear);
                        commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        commands.SetGlobalVector("_ZBufferParams", new Vector4(0,0,0,.2f));
                        commands.SetGlobalTexture("_CameraDepthTexture", depth);
                        commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    finally { commands.Release(); }
                    RenderTexture.active = target; result.ReadPixels(new Rect(0,0,size,size),0,0); result.Apply();
                    for (int y = 5; y < size; y += 9)
                    for (int x = 5; x < size; x += 9)
                    {
                        float u = (x + .5f) / size, v = (y + .5f) / size;
                        float a = Sample(main,u,v).a * .9f * vertexColor.a;
                        if (flags.Contains("USE_CLOUDS"))
                        {
                            float time = .37f + (flags.Contains("VERTEXCOLOR") ? 0 : vertexColor.r + vertexColor.g + vertexColor.b);
                            float c1 = Sample(one,u + time * .07f, v - time * .09f).a;
                            float c2 = Sample(two,u + time * .12f, v + time * .03f).a;
                            if (flags.Contains("CLOUDOFFSET"))
                                a *= Sample(main,u,v + .17f * (4*c1*c2-1)).a * .9f * vertexColor.a;
                            else a *= 4*c1*c2;
                        }
                        float coordinate = (a*1.4f+.03f)*.8f*.9f;
                        if (soft)
                        {
                            coordinate *= .8f; // scene eye depth 5, particle eye depth 1, InvFade .2.
                            if (flags.Contains("FADECLOSE")) coordinate *= .35f;
                        }
                        Color expected = Sample(ramp,coordinate,.5f) * new Color(.8f,.6f,.4f,.9f);
                        if (flags.Contains("VERTEXCOLOR"))
                        { expected.r *= vertexColor.r; expected.g *= vertexColor.g; expected.b *= vertexColor.b; }
                        expected.r *= 2; expected.g *= 2; expected.b *= 2;
                        Color pixel = result.GetPixel(x,y);
                        float error = Mathf.Max(Mathf.Abs(expected.r-pixel.r),Mathf.Abs(expected.g-pixel.g),
                            Mathf.Abs(expected.b-pixel.b),Mathf.Abs(expected.a-pixel.a));
                        Require(error < .003f, $"GPU instruction-reference mismatch flags={flags} x={x} y={y} expected={expected} actual={pixel} error={error}");
                        checks++;
                    }
                }
                Require(!ShaderUtil.ShaderHasError(shader), "Cloud shader variant compilation failed.");
                Debug.Log("RAIN_PARENT_CLOUD_GPU_CHECKED variants=9 samples=" + checks);
            }
            finally
            {
                RenderTexture.active = previous;
                if (previousSoft) Shader.EnableKeyword("SOFTPARTICLES_ON"); else Shader.DisableKeyword("SOFTPARTICLES_ON");
                Shader.SetGlobalTexture("_CameraDepthTexture", previousDepth); Shader.SetGlobalVector("_ZBufferParams", previousZ);
                target.Release();
                foreach (var obj in new UnityEngine.Object[] { main,one,two,ramp,depth,result,target,material,mesh })
                    UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        // GetPixelBilinear uses CPU texel coordinates; align its footprint with normalized GPU sampling.
        static Color Sample(Texture2D texture, float u, float v) =>
            texture.GetPixelBilinear(u - .5f / texture.width, v - .5f / texture.height);

        static Texture2D Pattern(int index)
        {
            var texture = new Texture2D(32,32,TextureFormat.RGBAFloat,false,true)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float u=(x+.5f)/32, v=(y+.5f)/32;
                texture.SetPixel(x,y,index==3 ? new Color(u,u*.5f+.1f,1-u,.2f+.6f*u)
                    : new Color(0,0,0,index==0 ? .1f+.6f*u+.2f*v : index==1 ? .2f+.4f*u : .15f+.5f*v));
            }
            texture.Apply(); return texture;
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
