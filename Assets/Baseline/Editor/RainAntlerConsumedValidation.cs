using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerConsumedValidation
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated batch project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var shader = Shader.Find("RainItems/AntlersConsumedCloudRemap");
                Require(shader && !ShaderUtil.ShaderHasError(shader), "Consumed Cloud Remap shader failed.");
                ValidateGpu(shader);
                RainAntlerConsumedImport.ImportAndValidate(shader);
                Debug.Log("RAIN_ANTLER_CONSUMED_ASSETS_PASSED gameplay_and_original_visual_comparison_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_ANTLER_CONSUMED_ASSETS_FAILED");
                EditorApplication.Exit(1);
            }
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
                foreach (string flags in new[] { "DISABLEREMAP", "DISABLEREMAP USE_CLOUDS",
                    "DISABLEREMAP SOFTPARTICLES_ON", "DISABLEREMAP USE_CLOUDS SOFTPARTICLES_ON",
                    "VERTEXCOLOR", "USE_CLOUDS CLOUDOFFSET" })
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
                        if (soft && !flags.Contains("DISABLEREMAP"))
                        {
                            coordinate *= .8f; // scene eye depth 5, particle eye depth 1, InvFade .2.
                            if (flags.Contains("FADECLOSE")) coordinate *= .35f;
                        }
                        Color expected;
                        if (flags.Contains("DISABLEREMAP"))
                        {
                            expected = Sample(main,u,v) * new Color(.8f,.6f,.4f,.9f) * vertexColor;
                            expected.a = a*1.4f+.03f;
                        }
                        else expected = Sample(ramp,coordinate,.5f) * new Color(.8f,.6f,.4f,.9f);
                        if (!flags.Contains("DISABLEREMAP") && flags.Contains("VERTEXCOLOR"))
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
                Debug.Log("RAIN_ANTLER_CONSUMED_CLOUD_GPU_CHECKED variants=6 samples=" + checks);
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
                    : new Color(u,.2f+.4f*v,1-u,index==0 ? .1f+.6f*u+.2f*v : index==1 ? .2f+.4f*u : .15f+.5f*v));
            }
            texture.Apply(); return texture;
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
