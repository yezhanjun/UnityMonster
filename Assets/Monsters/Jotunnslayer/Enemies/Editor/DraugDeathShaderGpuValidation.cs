using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class DraugDeathShaderGpuValidation
{
    public static void Run()
    {
        var bundles=new List<AssetBundle>();
        Material original=null,ported=null;Mesh mesh=null;
        Texture2D texture=null,readback=null;RenderTexture rt=null;ComputeBuffer globals=null;
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
                    if(!original && material && material.name=="M_mecha_particles")original=new Material(material);
            if(!original)throw new Exception("Original death material missing");
            ported=new Material(Shader.Find("Jotunnslayer/SourceDeathShuriken"));
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
            foreach(var mat in new[]{original,ported}){mat.SetTexture("_mainTexture",texture);mat.SetColor("_color",new Color(.4f,.7f,.2f,.5f));mat.SetVector("_noiseSpeed",new Vector4(20.03f,5,0,0));}
            rt=new RenderTexture(16,16,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
            readback=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);
            globals=new ComputeBuffer(231,16,ComputeBufferType.Constant);
            var values=new Vector4[231];
            values[20]=new Vector4(1,0,0,0);values[21]=new Vector4(0,1,0,0);values[22]=new Vector4(0,0,1,0);values[23]=new Vector4(0,0,0,1);
            // _OffScreenRendering is uint: upload integer bits, not IEEE float 1.
            values[227]=new Vector4(1,BitConverter.ToSingle(BitConverter.GetBytes(1),0),0,0);
            values[230]=new Vector4(0,0,0,1); // exposure=1
            Color Render(Material mat,int materialPass,float time)
            {
                pipelineProperty.SetValue(null,mat==original?"HDRenderPipeline":previousPipeline);
                var command=new CommandBuffer();
                try
                {
                    command.SetRenderTarget(rt);command.ClearRenderTarget(false,true,Color.clear);
                    command.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                    command.SetGlobalVector("_Time",new Vector4(time/20,time,time*2,time*3));
                    command.SetGlobalConstantBuffer(globals,Shader.PropertyToID("ShaderVariablesGlobal"),0,231*16);
                    command.DrawMesh(mesh,Matrix4x4.identity,mat,0,materialPass);
                    Graphics.ExecuteCommandBuffer(command);
                    RenderTexture.active=rt;readback.ReadPixels(new Rect(0,0,16,16),0,0);readback.Apply();
                    return readback.GetPixel(8,8);
                }
                finally {command.Release();}
            }
            foreach(float distortion in new[]{0f,.01f,.3f})
            foreach(float time in new[]{0f,.37f,12.5f})
            foreach(var uv in new[]{new Vector2(.1f,.2f),new Vector2(.5f,.5f),new Vector2(.999f,.001f),new Vector2(-.13f,1.2f),new Vector2(2.03f,-1.7f)})
            {
                mesh.uv=new[]{uv,uv,uv,uv};values[80]=new Vector4(time,0,0,0);globals.SetData(values);
                foreach(var mat in new[]{original,ported}){mat.SetFloat("_distortionAmount",distortion);mat.SetFloat("_noiseScale",25);}
                Color expected=Render(original,pass,time),actual=Render(ported,0,time);
                float error=Mathf.Max(Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),Mathf.Abs(expected.b-actual.b),Mathf.Abs(expected.a-actual.a));
                Debug.Log($"DRAUG_DEATH_GPU_CASE distortion={distortion} time={time} uv={uv} source={expected:F7} port={actual:F7} error={error}");
                if(expected.maxColorComponent<.0001f || float.IsNaN(error) || error>.0002f)throw new Exception("Source/ported death GPU mismatch");
                maximum=Mathf.Max(maximum,error);cases++;
            }
            File.WriteAllText("Docs/Validation/DraugDeathShaderGpuVerification.json",$"{{\"status\":\"PASS_CONTROLLED_SOURCE_GPU\",\"cases\":{cases},\"maxError\":{maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"limits\":\"Fixed clip-space mesh; HDRP offscreen skips fog; exposure1/mipBias0; real particles and final frame not verified.\"}}");
            Debug.Log($"DRAUG_DEATH_SHADER_GPU_PASS cases={cases} maxError={maximum}");
        }
        finally
        {
            pipelineProperty.SetValue(null,previousPipeline);
            RenderTexture.active=previous;globals?.Release();
            foreach(var obj in new Object[]{original,ported,mesh,texture,readback,rt})if(obj)Object.DestroyImmediate(obj);
            foreach(var bundle in bundles)if(bundle)bundle.Unload(true);
        }
        EditorApplication.Exit(0);
    }
}
