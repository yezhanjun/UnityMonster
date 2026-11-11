using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MTPSKIT.BuildTools
{
    public static class RainParentDecalInstructionValidation
    {
        static readonly Color Clear = new Color(-2, -3, -4, -5);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var shader = Shader.Find("Hidden/RainActors/ParentDecalInstructionValidation");
                Require(shader && !ShaderUtil.ShaderHasError(shader), "Decal validation shader did not compile.");
                Validate(shader);
                Debug.Log("RAIN_PARENT_DECAL_INSTRUCTIONS_PASSED production_decal_and_camera_gbuffer_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error); Debug.LogError("RAIN_PARENT_DECAL_INSTRUCTIONS_FAILED");
                EditorApplication.Exit(1);
            }
        }

        static void Validate(Shader shader)
        {
            const int size = 32;
            var material = new Material(shader);
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
            mesh.uv = new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            var textures = new[] { Pattern(0), Pattern(1), Pattern(2) };
            string[] names = { "_Cloud1Tex", "_Cloud2Tex", "_RemapTex" };
            for (int i = 0; i < 3; i++) material.SetTexture(names[i], textures[i]);
            var targets = new RenderTexture[4]; var readbacks = new Texture2D[4];
            var ids = new RenderTargetIdentifier[4];
            for (int i = 0; i < 4; i++)
            {
                targets[i] = new RenderTexture(size,size,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                targets[i].Create(); ids[i] = targets[i];
                readbacks[i] = new Texture2D(size,size,TextureFormat.RGBAFloat,false,true);
            }
            var previous = RenderTexture.active;
            int drawn = 0, rejected = 0, checkedSamples = 0, negativeAngle = 0;
            try
            {
                for (int test = 0; test < 25; test++)
                {
                    bool hdr = (test & 1) == 0, fixedBasis = (test & 2) != 0, maskNormals = (test & 4) != 0;
                    var up = new Vector3(.1f,1,.12f).normalized;
                    var right = Vector3.Cross(up,Vector3.forward).normalized;
                    var receiver = new Vector3(.2f + test*.007f,1,.24f).normalized;
                    if (test == 20) receiver = -up;
                    if (test == 21) { receiver = right; fixedBasis = false; }
                    float angleLimit = test == 20 ? .1f : -.2f;
                    float mask = test == 16 ? .0002f : test == 19 ? .0005f : .37f;
                    float multiplier = test == 22 ? 3.1f : .85f;
                    if (test == 19) multiplier = 1;
                    float objectMask = .25f, limitTo = test == 12 ? .3501f : .25f;
                    if (test == 24) limitTo = float.NaN; // original renderer: unrestricted decal
                    float layer = 1f, encodedLayer = test == 17 ? .5f : layer / 3f;
                    var desired = new Vector3(.08f,-.06f,.11f);
                    if (test == 13) desired.x = .5002f;
                    if (test == 14) desired.y = -.5002f;
                    if (test == 15) desired.z = .5002f;
                    var ray = new Vector3(.12f,-.08f,1);
                    float far = 2, depth = .4f;
                    var zp = new Vector2(.7f,1.2f);
                    var ctw = Matrix4x4.TRS(new Vector3(1.3f,.2f,-.7f),Quaternion.Euler(13,24,-7),Vector3.one);
                    var wtd = Matrix4x4.TRS(new Vector3(-.2f,.4f,.6f),Quaternion.Euler(-11,8,3),new Vector3(.8f,1.2f,.9f)).inverse;
                    var projected = wtd.MultiplyPoint3x4(ctw.MultiplyPoint3x4(ray * (far / ray.z) / (zp.x * depth + zp.y)));
                    wtd.m03 += desired.x - projected.x; wtd.m13 += desired.y - projected.y; wtd.m23 += desired.z - projected.z;
                    if (test == 18)
                    {
                        ray = new Vector3(.5f,0,1); far = 1; depth = 0; zp = new Vector2(0,1);
                        ctw = Matrix4x4.identity; wtd = Matrix4x4.Translate(Vector3.back); // exact inclusive x boundary
                    }
                    var local = wtd.MultiplyPoint3x4(ctw.MultiplyPoint3x4(ray * (far / ray.z) / (zp.x * depth + zp.y)));
                    var st1 = new Vector4(.7f,.9f,.03f,.08f); var st2 = new Vector4(.9f,.6f,.11f,.04f);
                    var scroll = new Vector4(.13f,-.17f,-.3f,.1f);
                    float timeX = .6f + test*.03f, boost = 2.65f;
                    var tint = new Color(.8f,.6f,.4f,.7f); var ambient = new Vector3(.22f,.31f,.13f);
                    var packed = new Color(.83f,.61f,0,.9f); float strength = .45f + test*.03f;
                    var receiverSpec = new Color(.1f,.25f,.2f,.4f);
                    var spec = new Vector4(.18f,5,4,0);
                    material.SetVector("_Gate",new Vector4(objectMask,limitTo,mask,multiplier));
                    material.SetVector("_Receiver",new Vector4(receiver.x,receiver.y,receiver.z,encodedLayer));
                    material.SetVector("_Right",right); material.SetVector("_Up",up);
                    material.SetVector("_Settings",new Vector4(angleLimit,boost,timeX,layer));
                    material.SetVector("_NormalSettings",new Vector4(strength,fixedBasis ? 1 : 0,maskNormals ? 1 : 0,hdr ? 1 : 0));
                    material.SetVector("_SpecSettings",spec); material.SetVector("_PackedNormal",packed);
                    material.SetVector("_ReceiverSpec",receiverSpec);
                    material.SetVector("_RayDepth",new Vector4(ray.x,ray.y,ray.z,depth));
                    material.SetVector("_DepthParams",new Vector4(zp.x,zp.y,far,0));
                    material.SetMatrix("_CameraToWorldTest",ctw); material.SetMatrix("_WorldToDecalTest",wtd);
                    material.SetVector("_Cloud1Tex_ST",st1); material.SetVector("_Cloud2Tex_ST",st2);
                    material.SetVector("_Scroll",scroll); material.SetColor("_Tint",tint); material.SetVector("_Ambient",ambient);
                    var cmd = new CommandBuffer();
                    try { cmd.SetRenderTarget(ids,new RenderTargetIdentifier(BuiltinRenderTextureType.None)); cmd.ClearRenderTarget(false,true,Clear); cmd.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity); cmd.DrawMesh(mesh,Matrix4x4.identity,material); Graphics.ExecuteCommandBuffer(cmd); }
                    finally { cmd.Release(); }
                    for (int i = 0; i < 4; i++) { RenderTexture.active=targets[i]; readbacks[i].ReadPixels(new Rect(0,0,size,size),0,0); readbacks[i].Apply(); }
                    // Material properties enter the GPU as float32 values. Store the reference
                    // quotient too; Mono can otherwise retain a wider temporary in equality.
                    float referenceLayer = layer * (1f/3f);
                    bool accept = !(Mathf.Abs(objectMask-limitTo)>.1f) && !(Mathf.Abs(local.x)>.5f) && !(Mathf.Abs(local.y)>.5f) && !(Mathf.Abs(local.z)>.5f) && !(mask*multiplier-.0005f<0) && encodedLayer==referenceLayer;
                    if (accept) drawn++; else rejected++;
                    float angle = Vector3.Dot(receiver,up)-angleLimit;
                    if (accept && angle<0) negativeAngle++;
                    for (int y = 7; y < size; y += 16) for (int x = 7; x < size; x += 16)
                    {
                        var expected = new[] { Clear,Clear,Clear,Clear };
                        if (accept)
                        {
                            var uv = new Vector2(local.x+.5f+((x+.5f)/size-.5f)*.13f,local.z+.5f+((y+.5f)/size-.5f)*.13f);
                            float a = Sample(textures[0],uv.x*st1.x+st1.z+timeX*scroll.x,uv.y*st1.y+st1.w+timeX*scroll.y).a;
                            float b = Sample(textures[1],uv.x*st2.x+st2.z+timeX*scroll.z,uv.y*st2.y+st2.w+timeX*scroll.w).a;
                            var color = Sample(textures[2],angle*mask*multiplier*a*b*4*boost,.5f)*tint;
                            expected[0] = new Color(color.r*color.a,color.g*color.a,color.b*color.a,color.a);
                            expected[1] = new Color((hdr ? color.r*ambient.x : Mathf.Pow(2,-color.r*ambient.x))*color.a,(hdr ? color.g*ambient.y : Mathf.Pow(2,-color.g*ambient.y))*color.a,(hdr ? color.b*ambient.z : Mathf.Pow(2,-color.b*ambient.z))*color.a,color.a);
                            expected[2] = Color.LerpUnclamped(receiverSpec,new Color(spec.x,spec.y,spec.z/16f,spec.w),mask*multiplier);
                            var xy = new Vector2(packed.r*packed.a*2-1,packed.g*2-1);
                            Vector3 tangent,bitangent,n;
                            if (fixedBasis) { tangent=right; bitangent=Vector3.Cross(up,right); n=up; }
                            else
                            {
                                // Independent Gram projection equivalent to the original swizzled cross products.
                                tangent=right*receiver.sqrMagnitude-receiver*Vector3.Dot(receiver,right);
                                bitangent=Vector3.Cross(receiver,right); n=receiver;
                            }
                            var projectedNormal=tangent*(xy.x*strength)+bitangent*(xy.y*strength)+n*Mathf.Sqrt(1-Mathf.Min(xy.sqrMagnitude,1));
                            float weight=maskNormals ? mask*multiplier : multiplier;
                            var blended=(projectedNormal*weight+receiver*(1-weight)).normalized*.5f+Vector3.one*.5f;
                            expected[3]=new Color(blended.x,blended.y,blended.z,1);
                            if(angle<0) expected[2]=expected[3]=new Color(-7,-7,-7,-7);
                        }
                        for(int i=0;i<4;i++)
                        {
                            var actual=readbacks[i].GetPixel(x,y);
                            Require(Error(expected[i],actual)<.003f,$"Decal mismatch case={test} target={i} x={x} y={y} expected={expected[i]} actual={actual} accepted={accept} local={local.ToString("R")} encodedLayer={encodedLayer:R} referenceLayer={referenceLayer:R}");
                            checkedSamples++;
                        }
                    }
                }
                Require(drawn==19 && rejected==6 && negativeAngle>=1,"Decal boundary/angle coverage missing.");
                Debug.Log($"RAIN_PARENT_DECAL_GPU_CHECKED cases=25 target_samples={checkedSamples} accepted={drawn} rejected={rejected} negative_angle={negativeAngle} hdr_ldr=True two_normal_bases=True scaled_time_x=True unrestricted_nan=True");
            }
            finally
            {
                RenderTexture.active=previous;
                foreach(var t in targets) { t.Release(); UnityEngine.Object.DestroyImmediate(t); }
                foreach(var t in readbacks) UnityEngine.Object.DestroyImmediate(t);
                foreach(var t in textures) UnityEngine.Object.DestroyImmediate(t);
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        static Texture2D Pattern(int index)
        {
            var t=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true) { filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
            for(int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float u=(x+.5f)/32,v=(y+.5f)/32;
                t.SetPixel(x,y,index==2 ? new Color(.1f+.8f*u,.2f+.4f*u,1-u,.2f+.6f*u) : new Color(0,0,0,index==0 ? .2f+.5f*u : .3f+.4f*v));
            }
            t.Apply(); return t;
        }
        static Color Sample(Texture2D texture,float u,float v) => texture.GetPixelBilinear(u-.5f/texture.width,v-.5f/texture.height);
        static float Error(Color a,Color b) => Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a));
        static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    }
}
