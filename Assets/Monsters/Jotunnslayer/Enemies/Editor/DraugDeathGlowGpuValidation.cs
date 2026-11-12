using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class DraugDeathGlowGpuValidation
{
    public static void Run()
    {
        var bundles=new List<AssetBundle>();
        Material original=null,ported=null;Mesh mesh=null;
        Texture2D texture=null,readback=null,depth=null;Texture2DArray sourceDepth=null;RenderTexture rt=null;ComputeBuffer globals=null;
        var previous=RenderTexture.active;
        var pipelineProperty=typeof(Shader).GetProperty("globalRenderPipeline",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if(pipelineProperty==null || !pipelineProperty.CanWrite)throw new Exception("Shader pipeline selection API unavailable");
        string previousPipeline=(string)pipelineProperty.GetValue(null);
        int cases=0;float maximum=0;
        try
        {
            const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
            foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
                foreach(var file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
            var bundle=AssetBundle.LoadFromFile("Logs/draug-death-recovered.bundle");bundles.Add(bundle);
            var root=bundle.LoadAsset<GameObject>("assets/recovered/draugdeath_visual.prefab");
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    if(!original && material && material.name=="M_Glow_Alpha")original=new Material(material);
            if(!original)throw new Exception("Original death material missing");
            ported=new Material(Shader.Find("Jotunnslayer/SourceDeathAnimatedAlpha"));
            pipelineProperty.SetValue(null,"HDRenderPipeline");
            int pass=-1;
            for(int i=0;i<original.passCount;i++)
            {
                Debug.Log($"DRAUG_DEATH_GPU_PASS_NAME {i}={original.GetPassName(i)} shader={original.shader.name}");
                if(string.Equals(original.GetPassName(i),"ForwardOnly",StringComparison.OrdinalIgnoreCase))pass=i;
            }
            if(pass<0)throw new Exception("Source ForwardOnly absent");
            Debug.Log("DRAUG_DEATH_GPU_SOURCE keywords="+string.Join(",",original.shaderKeywords)+" pass="+pass);
            original.SetFloat("_CullModeForward",0);original.SetFloat("_CullMode",0);
            original.SetFloat("_ZTestTransparent",8);original.SetFloat("_ZWrite",0);ported.SetFloat("_Cull",0);
            texture=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)texture.SetPixel(x,y,new Color((x+.5f)/16,(y+.5f)/16,((x*7+y*3)%16+.5f)/16,.2f+.7f*((x+y)%7)/6));
            texture.Apply();
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,.5f),new Vector3(1,-1,.5f),new Vector3(1,1,.5f),new Vector3(-1,1,.5f)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};
            mesh.tangents=new[]{new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1)};
            var vertex=new Color(.6f,.2f,.8f,.25f);mesh.colors=new[]{vertex,vertex,vertex,vertex};
            depth=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);depth.SetPixel(0,0,Color.white);depth.Apply();
            sourceDepth=new Texture2DArray(16,16,1,TextureFormat.RGBAFloat,false,true);
            var depthPixels=new Color[256];for(int n=0;n<256;n++)depthPixels[n]=Color.white;
            sourceDepth.SetPixels(depthPixels,0);sourceDepth.Apply();
            foreach(var mat in new[]{original,ported})
            {
                mat.SetTexture("_MainTexA",texture);mat.SetTexture("_MainTexB",Texture2D.whiteTexture);mat.SetTexture("_MainTexC",Texture2D.whiteTexture);mat.SetTexture("_MaskTexture",Texture2D.whiteTexture);
                mat.SetColor("_Tint",new Color(.4f,.7f,.2f,.5f));mat.SetFloat("_Intensity",2);
                mat.SetFloat("_LocalDistortionStrength",0);mat.SetFloat("_BlendMode",0);
            }
            rt=new RenderTexture(16,16,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
            readback=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);
            globals=new ComputeBuffer(231,16,ComputeBufferType.Constant);
            var values=new Vector4[231];
            values[20]=new Vector4(1,0,0,0);values[21]=new Vector4(0,1,0,0);values[22]=new Vector4(0,0,1,0);values[23]=new Vector4(0,0,0,1);
            // _OffScreenRendering is uint: upload integer bits, not IEEE float 1.
            values[227]=new Vector4(1,BitConverter.ToSingle(BitConverter.GetBytes(1),0),0,0);
            values[230]=new Vector4(0,0,0,1); // exposure=1
            values[0]=new Vector4(1,0,0,0);values[1]=new Vector4(0,1,0,0);values[2]=new Vector4(0,0,1,0);values[3]=new Vector4(0,0,-2,1);
            values[50]=new Vector4(16,16,1f/16,1f/16);values[61]=new Vector4(16,16,1f/16,1f/16);
            values[58]=new Vector4(1,.1f,1,1);
            Color Render(Material mat,int materialPass,float sceneDepth)

            {
                pipelineProperty.SetValue(null,mat==original?"HDRenderPipeline":previousPipeline);
                var command=new CommandBuffer();
                try
                {
                    command.SetRenderTarget(rt);command.ClearRenderTarget(false,true,Color.clear);
                    var projection=Matrix4x4.identity;projection.m22=-.1f;command.SetViewProjectionMatrices(Matrix4x4.Translate(new Vector3(0,0,-2)),projection);
                    command.SetGlobalVector("_Time",Vector4.zero);
                    command.SetGlobalVector("_ZBufferParams",new Vector4(0,1/sceneDepth,0,0));
                    command.SetGlobalVector("_ProjectionParams",new Vector4(1,.1f,1,1));
                    command.SetGlobalTexture("_CameraDepthTexture",mat==original?(Texture)sourceDepth:depth);
                    command.SetGlobalTexture("_ExposureTexture",Texture2D.whiteTexture);
                    command.SetGlobalConstantBuffer(globals,Shader.PropertyToID("ShaderVariablesGlobal"),0,231*16);
                    command.DrawMesh(mesh,Matrix4x4.identity,mat,0,materialPass);
                    Graphics.ExecuteCommandBuffer(command);
                    RenderTexture.active=rt;readback.ReadPixels(new Rect(0,0,16,16),0,0);readback.Apply();
                    return readback.GetPixel(8,8);
                }
                finally {command.Release();}
            }
            foreach(float fade in new[]{0f,.1f,1f,5f})
            foreach(float sceneDepth in new[]{.5f,1.5f,1.75f,3f})
            foreach(var uv in new[]{new Vector2(.1f,.2f),new Vector2(.5f,.5f),new Vector2(.9f,.8f)})
            {
                mesh.uv=new[]{uv,uv,uv,uv};values[57]=new Vector4(0,1/sceneDepth,0,0);globals.SetData(values);
                foreach(var mat in new[]{original,ported})mat.SetFloat("_Fade",fade);
                Color expected=Render(original,pass,sceneDepth),actual=Render(ported,0,sceneDepth);
                float error=Mathf.Max(Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),Mathf.Abs(expected.b-actual.b),Mathf.Abs(expected.a-actual.a));
                Debug.Log($"DRAUG_DEATH_GLOW_GPU_CASE fade={fade} scene={sceneDepth} uv={uv} source={expected:F7} port={actual:F7} error={error}");
                if(float.IsNaN(error) || float.IsInfinity(error) || error>.0002f)throw new Exception("Source/ported glow GPU mismatch");
                maximum=Mathf.Max(maximum,error);cases++;
            }
            File.WriteAllText("Docs/Validation/DraugDeathGlowGpuVerification.json",$"{{\"status\":\"PASS_CONTROLLED_SOURCE_GPU\",\"cases\":{cases},\"maxError\":{maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"limits\":\"Controlled depth and texture cases; no real particle/camera exposure or final death frame verification.\"}}");
            Debug.Log($"DRAUG_DEATH_GLOW_GPU_PASS cases={cases} maxError={maximum}");
        }
        finally
        {
            pipelineProperty.SetValue(null,previousPipeline);
            RenderTexture.active=previous;globals?.Release();
            foreach(var obj in new Object[]{original,ported,mesh,texture,readback,rt,depth,sourceDepth})if(obj)Object.DestroyImmediate(obj);
            foreach(var bundle in bundles)if(bundle)bundle.Unload(true);
        }
        EditorApplication.Exit(0);
    }
}
