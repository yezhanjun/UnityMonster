using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MTPSKIT.BuildTools
{
    public static class RainParentDistortionMaterialValidation
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
                var shader = Shader.Find("RainActors/ParentDistortion");
                Require(shader && !ShaderUtil.ShaderHasError(shader), "Distortion shader failed to compile.");
                ValidateGpu(shader);
                ValidateCameraCapture(shader);
                ImportMaterials(shader);
                BindPreparedDistortionSlots();
                Debug.Log("RAIN_PARENT_DISTORTION_MATERIALS_PASSED other_shader_families_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_DISTORTION_MATERIALS_FAILED");
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
                if ((string)record["shaderName"] != "Hopoo Games/FX/Distortion") continue;
                var data = record["data"];
                string name = (string)data["m_Name"];
                string path = Folder + name + "-Distortion.mat";
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
                Debug.Log("RAIN_PARENT_DISTORTION_MATERIAL_CHECKED " + name);
            }
            Require(count == 2, "Expected two original Distortion material in this dependency set.");
            AssetDatabase.SaveAssets();
        }

        static void BindPreparedDistortionSlots()
        {
            var native = JObject.Parse(File.ReadAllText(Evidence + "Parent原生粒子校验源.json"));
            var materials = JObject.Parse(File.ReadAllText(Evidence + "Parent完整特效材质.json"));
            int bound = 0, pending = 0;
            foreach (var effect in native["effects"])
            {
                string name = (string)effect["name"];
                var root = PrefabUtility.LoadPrefabContents(Folder + name + "-CloudIntersectionPartial.prefab");
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
                            if ((string)record["shaderName"] != "Hopoo Games/FX/Distortion") { if ((string)record["shaderName"] != "Hopoo Games/FX/Cloud Remap" && (string)record["shaderName"] != "Hopoo Games/FX/Cloud Intersection Remap") pending++; continue; }
                            int bracket = property.LastIndexOf('[');
                            int slot = int.Parse(property.Substring(bracket + 1, property.Length - bracket - 2));
                            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + (string)record["data"]["m_Name"] + "-Distortion.mat");
                            Require(material && material.shader.name == "RainActors/ParentDistortion", "Missing prepared distortion material.");
                            slots[slot] = material;
                            bound++;
                        }
                        renderer.sharedMaterials = slots;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + name + "-DistortionPartial.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Require(bound == 2 && pending == 12, "Expected partial intersection binding with other shader families still pending.");
            Debug.Log($"RAIN_PARENT_DISTORTION_SLOTS_BOUND count={bound} remainingOtherShaderSlots={pending} production_wiring_pending=True");
        }

        static void ValidateGpu(Shader shader)
        {
            Require(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"Fixture projection/readback mapping is D3D11-specific.");
            const int size=32;
            var normal=Pattern(0); var mask=Pattern(1); var captured=Pattern(2);
            var depth=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
            var result=new Texture2D(size,size,TextureFormat.RGBAFloat,false,true);
            var target=new RenderTexture(size,size,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            target.Create();
            var material=new Material(shader); var mesh=new Mesh();
            mesh.vertices=new[] {new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};
            mesh.uv=new[] {Vector2.zero,Vector2.up,Vector2.one,Vector2.right}; mesh.triangles=new[] {0,1,2,0,2,3};
            var previous=RenderTexture.active;
            var previousDepth=Shader.GetGlobalTexture("_CameraDepthTexture");
            var previousGrab=Shader.GetGlobalTexture("_GrabTexturePostProcess");
            var previousZ=Shader.GetGlobalVector("_ZBufferParams");
            var previousProjection=Shader.GetGlobalVector("_ProjectionParams");
            bool previousSoft=Shader.IsKeywordEnabled("SOFTPARTICLES_ON");
            try
            {
                material.SetTexture("_BumpMap",normal); material.SetTexture("_MaskTex",mask);
                material.SetTextureScale("_BumpMap",new Vector2(.6f,.7f));
                material.SetTextureOffset("_BumpMap",new Vector2(.13f,.09f));
                material.SetTextureScale("_MaskTex",new Vector2(.7f,.8f));
                material.SetTextureOffset("_MaskTex",new Vector2(.11f,.06f));
                material.SetFloat("_NearFadeZeroDistance",0); material.SetFloat("_NearFadeOneDistance",5);
                material.SetFloat("_FarFadeZeroDistance",250000); material.SetFloat("_FarFadeOneDistance",200000);
                int checks=0;
                for(int test=0;test<16;test++)
                {
                    bool soft=(test&1)!=0;
                    if(soft) Shader.EnableKeyword("SOFTPARTICLES_ON"); else Shader.DisableKeyword("SOFTPARTICLES_ON");
                    float alpha=test==6 ? .001f : .8f;
                    mesh.colors=Enumerable.Repeat(new Color(.1f,.8f,.3f,alpha),4).ToArray();
                    float inv=(test&2)!=0 ? 0 : .7f;
                    float sceneDepth=(test&4)!=0 ? .8f : 1.6f;
                    float magnitude=(test&8)!=0 ? .8f : .27f;
                    material.SetFloat("_InvFade",inv); material.SetFloat("_Magnitude",magnitude);
                    float nearZero=test==14 ? 2 : 0;
                    material.SetFloat("_NearFadeZeroDistance",nearZero);
                    bool neutral=test==15;
                    if(neutral) for(int y=0;y<normal.height;y++) for(int x=0;x<normal.width;x++) normal.SetPixel(x,y,new Color(.5f,.5f,0,1));
                    normal.Apply();
                    depth.SetPixel(0,0,new Color(1/sceneDepth,0,0,1)); depth.Apply();
                    var background=new Color(.15f,.27f,.36f,.42f);
                    var commands=new CommandBuffer();
                    try
                    {
                        commands.SetRenderTarget(target); commands.ClearRenderTarget(false,true,background);
                        commands.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                        commands.SetGlobalVector("_ProjectionParams",new Vector4(1,.1f,100,.01f));
                        commands.SetGlobalVector("_ZBufferParams",new Vector4(0,0,1,0));
                        commands.SetGlobalTexture("_CameraDepthTexture",depth);
                        commands.SetGlobalTexture("_GrabTexturePostProcess",captured);
                        commands.DrawMesh(mesh,Matrix4x4.identity,material,0,0);
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    finally { commands.Release(); }
                    RenderTexture.active=target; result.ReadPixels(new Rect(0,0,size,size),0,0); result.Apply();
                    for(int y=5;y<size;y+=9) for(int x=5;x<size;x+=9)
                    {
                        float u=(x+.5f)/size,v=(y+.5f)/size;
                        float strength=Mathf.Clamp01(Mathf.Min((1-nearZero)/(5-nearZero),(1-250000f)/(200000-250000f)));
                        strength*=alpha*Sample(mask,(u+.11f)*.7f,(v+.06f)*.8f).a;
                        if(soft && inv>0) strength*=Mathf.Clamp01((sceneDepth-1)*inv);
                        var packed=Sample(normal,(u+.13f)*.6f,(v+.09f)*.7f);
                        var n=new Vector2(packed.r*packed.a*2-1,packed.g*2-1);
                        bool discard=strength<.01f || Mathf.Sqrt(1-Mathf.Min(n.sqrMagnitude,1))>.99f;
                        // D3D render-to-texture projection flips clip Y. ReadPixels v already
                        // represents (clip.w-clip.y)/(2*clip.w), the original grab coordinate.
                        Color expected=discard ? background : Sample(captured,u+n.x*magnitude*strength,v+n.y*magnitude*strength);
                        Color actual=result.GetPixel(x,y);
                        float error=Error(expected,actual);
                        Require(error<.003f,$"Distortion GPU mismatch case={test} x={x} y={y} expected={expected} actual={actual} error={error}");
                        checks++;
                    }
                }
                Require(!ShaderUtil.ShaderHasError(shader),"Distortion variant compilation failed.");
                Debug.Log("RAIN_PARENT_DISTORTION_GPU_CHECKED cases=16 samples="+checks+" soft_depth_normal_and_alpha_discard=True");
            }
            finally
            {
                RenderTexture.active=previous;
                if(previousSoft) Shader.EnableKeyword("SOFTPARTICLES_ON"); else Shader.DisableKeyword("SOFTPARTICLES_ON");
                Shader.SetGlobalTexture("_CameraDepthTexture",previousDepth); Shader.SetGlobalTexture("_GrabTexturePostProcess",previousGrab);
                Shader.SetGlobalVector("_ZBufferParams",previousZ); Shader.SetGlobalVector("_ProjectionParams",previousProjection);
                target.Release();
                foreach(var o in new UnityEngine.Object[] {normal,mask,captured,depth,result,target,material,mesh}) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        static void ValidateCameraCapture(Shader shader)
        {
            var oldDefault=GraphicsSettings.defaultRenderPipeline;
            var oldQuality=QualitySettings.renderPipeline;
            var oldActive=RenderTexture.active;
            var oldGrab=Shader.GetGlobalTexture("_GrabTexturePostProcess");
            bool previousSoft=Shader.IsKeywordEnabled("SOFTPARTICLES_ON");
            var rendererData=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Baseline/BaselineRenderer.asset"));
            rendererData.rendererFeatures.Clear();
            var feature=ScriptableObject.CreateInstance<RainParentDistortionFeature>();
            rendererData.rendererFeatures.Add(feature); rendererData.SetDirty();
            var pipeline=UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.msaaSampleCount=1; pipeline.supportsHDR=false;
            var scene=EditorSceneManager.NewPreviewScene();
            var cameraObject=new GameObject("Parent distortion validation camera");
            SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>(); camera.scene=scene;
            camera.fieldOfView=90; camera.aspect=1; camera.nearClipPlane=.1f; camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.allowHDR=false; camera.allowMSAA=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear); target.Create();
            var baseline=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var result=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);
            var pattern=Pattern(2);
            var normal=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true); normal.SetPixel(0,0,new Color(.7f,.6f,0,1)); normal.Apply();
            var backgroundMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")); backgroundMaterial.SetTexture("_BaseMap",pattern);
            var distortionMaterial=new Material(shader); distortionMaterial.SetTexture("_BumpMap",normal);
            distortionMaterial.SetFloat("_Magnitude",.4f);
            var background=GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(background,scene);
            background.transform.position=new Vector3(0,0,3); background.transform.localScale=new Vector3(6.6f,6.6f,1);
            background.GetComponent<MeshRenderer>().sharedMaterial=backgroundMaterial;
            var distorted=GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(distorted,scene);
            distorted.transform.position=new Vector3(0,0,2); distorted.transform.localScale=new Vector3(4.4f,4.4f,1);
            var distortionRenderer=distorted.GetComponent<MeshRenderer>(); distortionRenderer.sharedMaterial=distortionMaterial;
            var overlayMaterial=new Material(backgroundMaterial);
            overlayMaterial.SetColor("_BaseColor",new Color(.8f,.2f,.4f,.6f));
            overlayMaterial.SetFloat("_Surface",1); overlayMaterial.SetFloat("_SrcBlend",5); overlayMaterial.SetFloat("_DstBlend",10);
            overlayMaterial.SetFloat("_SrcBlendAlpha",1); overlayMaterial.SetFloat("_DstBlendAlpha",10);
            overlayMaterial.SetFloat("_ZWrite",0); overlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            overlayMaterial.renderQueue=3000;
            var overlay=GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(overlay,scene);
            overlay.transform.position=new Vector3(.3f,.15f,2.5f); overlay.transform.localScale=new Vector3(3,3,1);
            var overlayRenderer=overlay.GetComponent<MeshRenderer>(); overlayRenderer.sharedMaterial=overlayMaterial;
            // Built-in quad color defaults to white, matching original particle alpha=1.
            try
            {
                GraphicsSettings.defaultRenderPipeline=pipeline; QualitySettings.renderPipeline=pipeline;
                Shader.DisableKeyword("SOFTPARTICLES_ON");
                distortionRenderer.enabled=false;
                overlayRenderer.enabled=false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target; result.ReadPixels(new Rect(0,0,128,128),0,0); result.Apply();
                overlayRenderer.enabled=true;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target; baseline.ReadPixels(new Rect(0,0,128,128),0,0); baseline.Apply();
                Require(baseline.GetPixel(64,64).maxColorComponent>.1f,"Camera baseline did not render.");
                Require(Error(baseline.GetPixel(64,64),result.GetPixel(64,64))>.03f,"Transparent reference did not affect the capture baseline.");
                distortionRenderer.enabled=true;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target; result.ReadPixels(new Rect(0,0,128,128),0,0); result.Apply();
                int checks=0; float maxDifference=0;
                for(int y=24;y<110;y+=17) for(int x=24;x<110;x+=17)
                {
                    float u=(x+.5f)/128,v=(y+.5f)/128;
                    // clip w=2, alpha=1, near factor=2/5, normal=(.4,.2).
                    var expected=Sample(baseline,u+.4f*.4f*.4f/2,v+.2f*.4f*.4f/2);
                    var actual=result.GetPixel(x,y);
                    Require(Error(expected,actual)<.012f,$"Capture mismatch x={x} y={y} expected={expected} actual={actual}");
                    maxDifference=Mathf.Max(maxDifference,Error(baseline.GetPixel(x,y),actual));
                    checks++;
                }
                Require(maxDifference>.008f,"Distortion feature produced no visible displacement.");
                Debug.Log("RAIN_PARENT_DISTORTION_CAMERA_CHECKED samples="+checks+" includes_prior_transparent=True maxDifference="+maxDifference);
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline=oldDefault; QualitySettings.renderPipeline=oldQuality;
                RenderTexture.active=oldActive;
                Shader.SetGlobalTexture("_GrabTexturePostProcess",oldGrab);
                if(previousSoft) Shader.EnableKeyword("SOFTPARTICLES_ON"); else Shader.DisableKeyword("SOFTPARTICLES_ON");
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                foreach(var o in new UnityEngine.Object[] {feature,pipeline,rendererData,target,baseline,result,pattern,normal,backgroundMaterial,distortionMaterial,overlayMaterial}) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        static float Error(Color a,Color b) => Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a));

        static Color Sample(Texture2D texture,float u,float v) => texture.GetPixelBilinear(u-.5f/texture.width,v-.5f/texture.height);

        static Texture2D Pattern(int index)
        {
            var texture=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float u=(x+.5f)/32,v=(y+.5f)/32;
                texture.SetPixel(x,y,index==0 ? new Color(.75f+.1f*u,.55f+.15f*v,0,.9f) : index==1 ? new Color(0,0,0,.2f+.6f*u+.1f*v) : new Color(.1f+.7f*u,.1f+.8f*v,.2f+.3f*u+.2f*v,.3f+.4f*u));
            }
            texture.Apply(); return texture;
        }

        static void Require(bool value,string message)
        { if(!value) throw new InvalidOperationException(message); }
    }
}

