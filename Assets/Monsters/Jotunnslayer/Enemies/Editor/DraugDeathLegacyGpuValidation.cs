using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class DraugDeathLegacyGpuValidation
{
 public static void Run()=>Process(false);
 public static void RunSoft()=>Process(true);
 static void Process(bool softMode)
 {
  var bundles=new List<AssetBundle>();Material source=null,port=null;Mesh mesh=null;Texture2D tex=null,read=null;RenderTexture rt=null;
  var previous=RenderTexture.active;
  var property=typeof(Shader).GetProperty("globalRenderPipeline",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
  string pipeline=(string)property.GetValue(null);bool soft=Shader.IsKeywordEnabled("SOFTPARTICLES_ON");
  int cases=0;float maximum=0;
  try
  {
   const string folder="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})foreach(var file in Directory.GetFiles(folder,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
   var bundle=AssetBundle.LoadFromFile("Logs/draug-death-recovered.bundle");bundles.Add(bundle);
   foreach(var r in bundle.LoadAsset<GameObject>("assets/recovered/draugdeath_visual.prefab").GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!source && m && m.name=="M_smoke_earthquake")source=new Material(m);
   if(!source)throw new Exception("Original smoke missing");
   port=new Material(Shader.Find("Jotunnslayer/SourceDeathLegacyAdd"));
   if(softMode){Shader.EnableKeyword("SOFTPARTICLES_ON");source.EnableKeyword("SOFTPARTICLES_ON");port.EnableKeyword("SOFTPARTICLES_ON");}
   else{Shader.DisableKeyword("SOFTPARTICLES_ON");source.DisableKeyword("SOFTPARTICLES_ON");port.DisableKeyword("SOFTPARTICLES_ON");}
   tex=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);tex.SetPixel(0,0,new Color(.8f,.3f,.1f,.6f));tex.Apply();
   mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,.5f),new Vector3(1,-1,.5f),new Vector3(1,1,.5f),new Vector3(-1,1,.5f)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
   rt=new RenderTexture(16,16,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();read=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);
   float sceneDepth=2;
   Color Render(Material m)
   {
    property.SetValue(null,m==source?"":pipeline);var cmd=new CommandBuffer();
    try
    {
     cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(false,true,Color.clear);
     var view=softMode?Matrix4x4.Translate(new Vector3(0,0,-2)):Matrix4x4.identity;
     var projection=Matrix4x4.identity;if(softMode)projection.m22=-.1f;
     cmd.SetViewProjectionMatrices(view,projection);
     if(softMode){cmd.SetGlobalTexture("_CameraDepthTexture",tex);cmd.SetGlobalVector("_ZBufferParams",new Vector4(0,0,0,1/sceneDepth));}
     cmd.DrawMesh(mesh,Matrix4x4.identity,m,0,0);Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,16,16),0,0);read.Apply();return read.GetPixel(8,8);
    }
    finally{cmd.Release();}
   }
   foreach(float controlA in new[]{0f,.25f,1f,2f})foreach(float controlB in new[]{0f,.2f,.7f,1f})
   {
    float tintAlpha=softMode?1:controlA,vertexAlpha=softMode?.7f:controlB;
    sceneDepth=1.5f+controlA;
    var vertex=new Color(.6f,.2f,.8f,vertexAlpha);mesh.colors=new[]{vertex,vertex,vertex,vertex};
    foreach(var m in new[]{source,port}){m.SetTexture("_MainTex",tex);m.SetColor("_TintColor",new Color(.4f,.7f,.2f,tintAlpha));m.SetFloat("_InvFade",controlB);}
    Color a=Render(source),b=Render(port);float alpha=Mathf.Clamp01(2*vertexAlpha*tintAlpha*.6f);
    if(softMode)alpha=Mathf.Clamp01(2*vertexAlpha*tintAlpha*.6f*Mathf.Clamp01(controlA*controlB));
    Color tint=new Color(.4f,.7f,.2f,tintAlpha);
    if(QualitySettings.activeColorSpace==ColorSpace.Linear)tint=tint.linear;
    Color expected=new Color(2*.6f*tint.r*.8f*alpha,2*.2f*tint.g*.3f*alpha,2*.8f*tint.b*.1f*alpha,0);
    float error=Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b),Mathf.Abs(a.r-expected.r),Mathf.Abs(a.g-expected.g),Mathf.Abs(a.b-expected.b));
    Debug.Log($"DRAUG_DEATH_LEGACY_CASE soft={softMode} gap={controlA} coefficient={controlB} tintAlpha={tintAlpha} vertexAlpha={vertexAlpha} source={a:F7} port={b:F7} error={error}");
    if(float.IsNaN(error)||error>.00002f)throw new Exception("Legacy death GPU mismatch");maximum=Mathf.Max(maximum,error);cases++;
   }
   File.WriteAllText("Docs/Validation/DraugDeathLegacy"+(softMode?"Soft":"")+"GpuVerification.json",$"{{\"status\":\"PASS_CONTROLLED_SOURCE_GPU\",\"cases\":{cases},\"maxError\":{maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"limits\":\"Constant texture and controlled depth globals; camera depth generation and actual scene pending.\"}}");
   Debug.Log($"DRAUG_DEATH_LEGACY_GPU_PASS cases={cases} maxError={maximum}");
  }
  finally{property.SetValue(null,pipeline);if(soft)Shader.EnableKeyword("SOFTPARTICLES_ON");else Shader.DisableKeyword("SOFTPARTICLES_ON");RenderTexture.active=previous;foreach(var o in new Object[]{source,port,mesh,tex,read,rt})if(o)Object.DestroyImmediate(o);foreach(var b in bundles)if(b)b.Unload(true);}
  EditorApplication.Exit(0);
 }
}
