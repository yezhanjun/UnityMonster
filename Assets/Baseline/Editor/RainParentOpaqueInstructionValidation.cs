using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MTPSKIT.BuildTools
{
    public static class RainParentOpaqueInstructionValidation
    {
        const string RampPath="Assets/Resources/RainActors/RoR2/Base/Characters/Parent/Effects/texRampCompositeLight.png";

        public static void Run()
        {
            if(!Application.isBatchMode) throw new InvalidOperationException("Use the isolated project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var shader=Shader.Find("Hidden/RainActors/ParentOpaqueInstructionValidation");
                Require(shader && !ShaderUtil.ShaderHasError(shader),"Opaque validation shader did not compile.");
                var importer=(TextureImporter)AssetImporter.GetAtPath(RampPath);
                Require(importer.sRGBTexture && !importer.mipmapEnabled && importer.filterMode==FilterMode.Point &&
                    importer.wrapModeU==TextureWrapMode.Clamp && importer.wrapModeV==TextureWrapMode.Clamp,"Original global ramp sampler changed.");
                var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(RampPath);
                Require(imported && imported.width==128 && imported.height==32,"Original global ramp import missing.");
                ValidateGpu(shader,imported);
                Debug.Log("RAIN_PARENT_OPAQUE_INSTRUCTIONS_PASSED production_shader_and_attenuation_pending=True");
                EditorApplication.Exit(0);
            }
            catch(Exception error)
            {
                Debug.LogException(error); Debug.LogError("RAIN_PARENT_OPAQUE_INSTRUCTIONS_FAILED");
                EditorApplication.Exit(1);
            }
        }

        static void ValidateGpu(Shader shader,Texture2D importedRamp)
        {
            const int size=32;
            var inputs=Enumerable.Range(0,5).Select(Pattern).ToArray();
            var targets=Enumerable.Range(0,4).Select(_=>new RenderTexture(size,size,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)).ToArray();
            var readbacks=Enumerable.Range(0,4).Select(_=>new Texture2D(size,size,TextureFormat.RGBAFloat,false,true)).ToArray();
            foreach(var rt in targets) rt.Create();
            var material=new Material(shader); var mesh=new Mesh();
            mesh.vertices=new[] {new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};
            mesh.normals=Enumerable.Repeat(Vector3.forward,4).ToArray();
            mesh.uv=Enumerable.Repeat(Vector2.zero,4).ToArray();
            mesh.uv2=new[] {Vector2.zero,Vector2.up,Vector2.one,Vector2.right}; mesh.triangles=new[] {0,1,2,0,2,3};
            var vertex=new Color(.1f,.2f,.15f,.7f); mesh.colors=Enumerable.Repeat(vertex,4).ToArray();
            var sourceRamp=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            ImageConversion.LoadImage(sourceRamp,File.ReadAllBytes(RampPath));
            var previous=RenderTexture.active;
            var clear=new Color(.031f,.041f,.051f,.061f);
            try
            {
                string[] names={"_MainTex","_Cloud1Tex","_Cloud2Tex","_RemapTex","_NormalTex"};
                for(int i=0;i<5;i++) material.SetTexture(names[i],inputs[i]);
                material.SetTextureScale("_MainTex",new Vector2(.8f,.7f)); material.SetTextureOffset("_MainTex",new Vector2(.03f,.09f));
                material.SetTextureScale("_Cloud1Tex",new Vector2(.6f,.9f)); material.SetTextureOffset("_Cloud1Tex",new Vector2(.1f,.02f));
                material.SetTextureScale("_Cloud2Tex",new Vector2(.9f,.8f)); material.SetTextureOffset("_Cloud2Tex",new Vector2(.04f,.07f));
                material.SetColor("_TintColor",new Color(.8f,.6f,.4f,.9f)); material.SetColor("_EmissionColor",new Color(.2f,.4f,.1f,1));
                material.SetVector("_CutoffScroll",new Vector4(.07f,-.09f,.12f,.03f));
                material.SetFloat("_ValidationTime",.37f); material.SetFloat("_AlphaBoost",1.4f); material.SetFloat("_ExternalAlpha",.8f);
                material.SetFloat("_SpecularStrength",.2f); material.SetFloat("_SpecularExponent",1.7f);
                int payloadSamples=0,drawn=0,discarded=0;
                for(int test=0;test<8;test++)
                {
                    float influence=(test%3)*.5f,normalStrength=.5f+test*.3f,cutoff=test<4 ? 0 : .11f;
                    int ramp=test%2==0 ? 0 : 4; float tangentSign=(test&4)==0 ? 1 : -1; bool hdr=(test&1)!=0;
                    mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,tangentSign),4).ToArray();
                    material.SetFloat("_VertexColorInfluence",influence); material.SetFloat("_NormalStrength",normalStrength);
                    material.SetFloat("_Cutoff",cutoff); material.SetInt("_RampInfo",ramp); material.SetFloat("_ValidationHDR",hdr ? 1 : 0);
                    Draw(mesh,material,targets,0,clear);
                    for(int i=0;i<4;i++) Read(targets[i],readbacks[i]);
                    for(int y=5;y<size;y+=9) for(int x=5;x<size;x+=9)
                    {
                        var expected=Surface(inputs,vertex,(x+.5f)/size,(y+.5f)/size,influence,normalStrength,tangentSign,ramp,hdr,out float mask);
                        if(mask<cutoff) discarded++; else drawn++;
                        for(int i=0;i<4;i++)
                        {
                            Color wanted=mask<cutoff ? clear : expected[i]; Color actual=readbacks[i].GetPixel(x,y);
                            Require(Error(wanted,actual)<.003f,$"Opaque payload mismatch case={test} target={i} x={x} y={y} expected={wanted} actual={actual}");
                            payloadSamples++;
                        }
                    }
                }
                Require(drawn>0 && discarded>0,"Alpha clip coverage is missing.");
                Debug.Log($"RAIN_PARENT_OPAQUE_PAYLOAD_GPU_CHECKED cases=8 target_samples={payloadSamples} drawn={drawn} discarded={discarded}");
                material.SetFloat("_VertexColorInfluence",1); material.SetFloat("_NormalStrength",1);
                material.SetFloat("_Cutoff",0); material.SetTexture("_WarpRamp",importedRamp);
                mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,1),4).ToArray();
                int lightSamples=0;
                for(int test=0;test<8;test++)
                {
                    bool local=(test&1)!=0; int ramp=(test&2)==0 ? 0 : 4;
                    var light=new Vector3((test&4)==0 ? .3f : -.8f,.2f,(test&4)==0 ? .9f : -.4f).normalized;
                    var view=new Vector3(.1f,.15f,1).normalized;
                    var lightColor=new Color(.7f,.8f,.6f,1);
                    material.SetFloat("_ValidationLocalLight",local ? 1 : 0); material.SetInt("_RampInfo",ramp);
                    material.SetVector("_ValidationLightDirection",light); material.SetVector("_ValidationViewDirection",view);
                    material.SetColor("_ValidationLightColor",lightColor);
                    Draw(mesh,material,new[] {targets[0]},1,clear); Read(targets[0],readbacks[0]);
                    for(int y=5;y<size;y+=9) for(int x=5;x<size;x+=9)
                    {
                        var surface=Surface(inputs,vertex,(x+.5f)/size,(y+.5f)/size,1,1,1,ramp,true,out _);
                        var n=new Vector3(surface[2].r*2-1,surface[2].g*2-1,surface[2].b*2-1).normalized;
                        float noL=Vector3.Dot(light,n),spec=Mathf.Pow(Mathf.Clamp01(Vector3.Dot(Vector3.Reflect(-light,n),view)),1.7f);
                        Color diffuse=Warp(sourceRamp,local ? noL*.5f+.5f : noL,1-ramp/16f);
                        Color reflected=Warp(sourceRamp,spec,1-ramp/16f);
                        Color wanted=surface[0]*lightColor*diffuse*2+lightColor*(.4f*reflected.r); wanted.a=1;
                        Color actual=readbacks[0].GetPixel(x,y);
                        Require(Error(wanted,actual)<.004f,$"Original light mismatch case={test} x={x} y={y} expected={wanted} actual={actual}");
                        lightSamples++;
                    }
                }
                Require(!ShaderUtil.ShaderHasError(shader),"Opaque validation shader variant failed.");
                Debug.Log($"RAIN_PARENT_OPAQUE_WARP_GPU_CHECKED cases=8 samples={lightSamples} original_ramp=True directional_and_local_coordinates=True attenuation_not_in_scope=True");
            }
            finally
            {
                RenderTexture.active=previous;
                foreach(var rt in targets) rt.Release();
                foreach(var o in inputs.Cast<UnityEngine.Object>().Concat(targets).Concat(readbacks).Concat(new UnityEngine.Object[] {material,mesh,sourceRamp})) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        static Color[] Surface(Texture2D[] tex,Color vertex,float u,float v,float influence,float strength,float sign,int ramp,bool hdr,out float mask)
        {
            var color=Color.white+(vertex-Color.white)*influence;
            float mu=u*.8f+.03f,mv=v*.7f+.09f;
            mask=Sample(tex[0],mu,mv).a*color.a;
            mask*=Sample(tex[1],u*.6f+.1f+.37f*.07f,v*.9f+.02f-.37f*.09f).a;
            mask*=Sample(tex[2],u*.9f+.04f+.37f*.12f,v*.8f+.07f+.37f*.03f).a;
            mask*=1.4f*color.a*.9f*4*.8f;
            var albedo=Sample(tex[3],mask,.5f)*new Color(.8f,.6f,.4f,.9f)*color; albedo.a=1;
            var packed=Sample(tex[4],mu,mv); float nx=packed.r*packed.a*2-1,ny=packed.g*2-1;
            float nz=Mathf.Sqrt(1-Mathf.Min(nx*nx+ny*ny,1));
            var n=new Vector3(nx*strength,ny*strength*sign,1+(nz-1)*strength).normalized;
            var emission=hdr ? new Color(.2f,.4f,.1f,1) : new Color(Mathf.Pow(2,-.2f),Mathf.Pow(2,-.4f),Mathf.Pow(2,-.1f),1);
            return new[] {albedo,new Color(.2f,1.7f*.05f,ramp/16f,0),new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,0),emission};
        }

        static Color Warp(Texture2D texture,float u,float v) =>
            texture.GetPixel(Mathf.Clamp(Mathf.FloorToInt(u*texture.width),0,texture.width-1),Mathf.Clamp(Mathf.FloorToInt(v*texture.height),0,texture.height-1)).linear;

        static void Draw(Mesh mesh,Material material,RenderTexture[] targets,int pass,Color clear)
        {
            var cmd=new CommandBuffer();
            try
            {
                cmd.SetRenderTarget(targets.Select(t=>new RenderTargetIdentifier(t)).ToArray(),new RenderTargetIdentifier(BuiltinRenderTextureType.None));
                cmd.ClearRenderTarget(false,true,clear); cmd.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                cmd.DrawMesh(mesh,Matrix4x4.identity,material,0,pass); Graphics.ExecuteCommandBuffer(cmd);
            }
            finally {cmd.Release();}
        }
        static void Read(RenderTexture source,Texture2D target) {RenderTexture.active=source; target.ReadPixels(new Rect(0,0,source.width,source.height),0,0); target.Apply();}
        static float Error(Color a,Color b) => Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a));
        static Color Sample(Texture2D texture,float u,float v) => texture.GetPixelBilinear(u-.5f/texture.width,v-.5f/texture.height);
        static Texture2D Pattern(int index)
        {
            var t=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float u=(x+.5f)/32,v=(y+.5f)/32;
                t.SetPixel(x,y,index==3 ? new Color(u,u*.5f+.1f,1-u,.2f+.6f*u) : index==4 ? new Color(.75f+.1f*u,.55f+.1f*v,0,.9f)
                    : new Color(0,0,0,index==0 ? .1f+.6f*u+.2f*v : index==1 ? .2f+.4f*u : .15f+.5f*v));
            }
            t.Apply(); return t;
        }
        static void Require(bool value,string message) {if(!value) throw new InvalidOperationException(message);}
    }
}
