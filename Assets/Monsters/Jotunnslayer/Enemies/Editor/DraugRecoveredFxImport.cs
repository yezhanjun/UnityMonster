using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class DraugRecoveredFxImport
{
 const string Folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/OriginalFx";
 static readonly Dictionary<Object,Object> copies=new Dictionary<Object,Object>();
 static readonly Dictionary<Object,Object> sourceTextures=new Dictionary<Object,Object>();
 static T Save<T>(T value,string path) where T:Object
 {
  var old=AssetDatabase.LoadAssetAtPath<T>(path);
  if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);return old;}
  AssetDatabase.CreateAsset(value,path);return value;
 }
 static Mesh MeshCopy(Mesh value)
 {
  if(!value)return null;
  if(copies.TryGetValue(value,out var found))return (Mesh)found;
  var result=Save(Object.Instantiate(value),Folder+"/mesh_"+copies.Count+".asset");copies[value]=result;return result;
 }
 static Texture CopySourceMipChain(Texture value)
 {
  bool srgb=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(value.graphicsFormat);
  var shader=Shader.Find("Hidden/Jotunnslayer/SourceTextureMipRead");
  if(!shader)throw new Exception("Missing explicit source mip sampler");
  var sampler=new Material(shader);
  var result=new Texture2D(value.width,value.height,TextureFormat.RGBA32,true,!srgb);
  result.name=value.name+"_SourceMipChain";
  var previous=RenderTexture.active;
  bool saved=false;
  try
  {
   if(result.mipmapCount!=value.mipmapCount)throw new Exception("Source partial mip chain needs explicit handling: "+value.name);
   for(int mip=0;mip<value.mipmapCount;mip++)
   {
    int width=Mathf.Max(1,value.width>>mip),height=Mathf.Max(1,value.height>>mip);
    var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,srgb?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
    var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false,!srgb);
    try
    {
     sampler.SetFloat("_SourceMip",mip);Graphics.Blit(value,rt,sampler);RenderTexture.active=rt;
     pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply(false);
     result.SetPixels32(pixels.GetPixels32(),mip);
    }
    finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(pixels);}
   }
   result.Apply(false); // Do not regenerate and overwrite the recovered source levels.
   result.filterMode=value.filterMode;result.anisoLevel=value.anisoLevel;result.mipMapBias=value.mipMapBias;
   result.wrapModeU=value.wrapModeU;result.wrapModeV=value.wrapModeV;result.wrapModeW=value.wrapModeW;
   var stored=Save(result,Folder+"/source_texture_"+sourceTextures.Count+".asset");saved=true;
   sourceTextures[value]=stored;Debug.Log($"DRAUG_SOURCE_MIPS_IMPORTED {value.name} levels={value.mipmapCount}");return stored;
  }
  finally {RenderTexture.active=previous;Object.DestroyImmediate(sampler);if(!saved && result)Object.DestroyImmediate(result);}
 }
 static Texture TextureCopy(Texture value,bool preserveChannels=false)
 {
  if(!value)return null;
  var cache=preserveChannels?sourceTextures:copies;
  if(cache.TryGetValue(value,out var found))return (Texture)found;
  if(preserveChannels && value.mipmapCount>1)return CopySourceMipChain(value);
  bool sourceSrgb=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(value.graphicsFormat);
  var readWrite=preserveChannels?(sourceSrgb?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear):RenderTextureReadWrite.Default;
  var rt=RenderTexture.GetTemporary(value.width,value.height,0,RenderTextureFormat.ARGB32,readWrite);
  var previous=RenderTexture.active;Texture2D tex=null;
  try
  {
   Graphics.Blit(value,rt);RenderTexture.active=rt;
   tex=new Texture2D(value.width,value.height,TextureFormat.RGBA32,false,preserveChannels && !sourceSrgb);
   tex.ReadPixels(new Rect(0,0,value.width,value.height),0,0);tex.Apply();
   if(!preserveChannels && (value.name=="T_VFX_Dust_01" || value.name=="T_ring_indicator01"))
   {
    var pixels=tex.GetPixels();
    for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(1,1,1,Mathf.Max(pixels[i].r,Mathf.Max(pixels[i].g,pixels[i].b)));
    tex.SetPixels(pixels);tex.Apply();
   }
   string path=Folder+(preserveChannels?"/source_texture_":"/texture_")+cache.Count+".png";
   File.WriteAllBytes(path,tex.EncodeToPNG());AssetDatabase.ImportAsset(path);
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=!preserveChannels;importer.wrapMode=value.wrapMode;
   if(preserveChannels)
   {
    // Packed channels drive dissolve and UVs; another lossy compression changes their meaning.
    importer.textureCompression=TextureImporterCompression.Uncompressed;
    importer.sRGBTexture=sourceSrgb;importer.mipmapEnabled=value.mipmapCount>1;
    importer.npotScale=TextureImporterNPOTScale.None;
    importer.maxTextureSize=Mathf.Max(32,Mathf.NextPowerOfTwo(Mathf.Max(value.width,value.height)));
    importer.filterMode=value.filterMode;importer.anisoLevel=value.anisoLevel;
    importer.wrapModeU=value.wrapModeU;importer.wrapModeV=value.wrapModeV;
   }
   importer.SaveAndReimport();
   var result=AssetDatabase.LoadAssetAtPath<Texture2D>(path);cache[value]=result;return result;
  }
  finally {if(tex)Object.DestroyImmediate(tex);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
 }
 static Material MaterialCopy(Material original)
 {
  if(!original)return null;
  if(copies.TryGetValue(original,out var found))return (Material)found;
  if(original.shader && original.shader.name=="Vefects/SH_Vefects_VFX_Particles_Erosion")
  {
   // Current source smoke has no camera offset; reject an unsupported future material configuration.
   if(original.GetFloat("_CameraOffset")!=0)throw new Exception("Source smoke camera offset requires a vertex port");
   var shader=Shader.Find("Jotunnslayer/SourceSmokeErosion");
   if(!shader)throw new Exception("Missing source smoke erosion shader");
   var specific=new Material(shader);specific.name=original.name+"_SourceSurfaceURP";
   for(int i=0;i<shader.GetPropertyCount();i++)
   {
    var key=shader.GetPropertyName(i);if(key=="_Cull")continue;
    if(!original.HasProperty(key))throw new Exception("Missing smoke source property "+key);
    var type=shader.GetPropertyType(i);
    if(type==UnityEngine.Rendering.ShaderPropertyType.Texture)
    {var texture=original.GetTexture(key);if(texture)specific.SetTexture(key,TextureCopy(texture,true));}
    else if(type==UnityEngine.Rendering.ShaderPropertyType.Color)specific.SetColor(key,original.GetColor(key));
    else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)specific.SetVector(key,original.GetVector(key));
    else specific.SetFloat(key,original.GetFloat(key));
   }
   specific.SetFloat("_Cull",original.GetFloat("_CullModeForward"));
   var smokeResult=Save(specific,Folder+"/material_"+copies.Count+".mat");copies[original]=smokeResult;
   Debug.Log("DRAUG_SMOKE_IMPORTED "+original.name);return smokeResult;
  }
  if(original.shader && original.shader.name=="Shader Graphs/Fx_Shoot&Hit_Particle_add")
  {
   var shader=Shader.Find("Jotunnslayer/SourceFlaskShootHit");
   if(!shader)throw new Exception("Missing source flask shoot-hit shader");
   const string textureKey="Texture2D_F593E37E";
   if(!original.HasProperty(textureKey) || !original.GetTexture(textureKey))throw new Exception("Missing source shoot-hit texture");
   var specific=new Material(shader);specific.name=original.name+"_SourceSurfaceURP";
   specific.SetTexture(textureKey,TextureCopy(original.GetTexture(textureKey),true));
   specific.SetFloat("Boolean_2FAC1FE4",original.GetFloat("Boolean_2FAC1FE4"));
   specific.SetFloat("_Cull",original.GetFloat("_CullModeForward"));
   var specificResult=Save(specific,Folder+"/material_"+copies.Count+".mat");copies[original]=specificResult;
   Debug.Log("DRAUG_FLASK_SHOOT_HIT_IMPORTED "+original.name);
   return specificResult;
  }
  if(original.shader && original.shader.name=="Kyeoms/Kyeoms_ParticleDissolve&Offset")
  {
   var shader=Shader.Find("Jotunnslayer/SourceFlaskDissolve");
   if(!shader)throw new Exception("Missing source flask dissolve shader");
   var specific=new Material(shader);specific.name=original.name+"_SourceSurfaceURP";
   foreach(var key in new[]{"Texture2D_F593E37E","_MainTex_UV"})
   {
    if(!original.HasProperty(key) || !original.GetTexture(key))throw new Exception("Missing source flask texture "+key);
    specific.SetTexture(key,TextureCopy(original.GetTexture(key),true));
   }
   specific.SetFloat("Boolean_2FAC1FE4",original.GetFloat("Boolean_2FAC1FE4"));
   specific.SetFloat("_Cull",original.GetFloat("_CullModeForward"));
   var specificResult=Save(specific,Folder+"/material_"+copies.Count+".mat");copies[original]=specificResult;
   Debug.Log("DRAUG_FLASK_DISSOLVE_IMPORTED "+original.name);
   return specificResult;
  }
  var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.name=original.name+"_URPApproximation";
  bool additive=original.HasProperty("_DstBlend") && original.GetFloat("_DstBlend")==1;
  mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",additive?2:0);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",additive?1:10);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);
  mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
  foreach(var key in new[]{"_mainTexture","Main_tex","_MainTex","_BaseMap","_MaskTexture","Texture2D_F593E37E","_MainMask"})
   if(original.HasProperty(key) && original.GetTexture(key)){mat.SetTexture("_BaseMap",TextureCopy(original.GetTexture(key)));mat.SetTextureScale("_BaseMap",original.GetTextureScale(key));mat.SetTextureOffset("_BaseMap",original.GetTextureOffset(key));break;}
  var color=Color.white;
  foreach(var key in new[]{"_FinalColor","_color","_Color","_BaseColor"})if(original.HasProperty(key)){color=original.GetColor(key);if(color.a>0)break;}
  // Custom dissolve alpha semantics are not URP opacity; preserve visible tint pending visual review.
  if(color.a<=0f)color.a=1;mat.SetColor("_BaseColor",color);
  var result=Save(mat,Folder+"/material_"+copies.Count+".mat");copies[original]=result;return result;
 }
 public static void Run()=>Import(false);
 public static void RunAndPreview()=>Import(true);
 public static void RunAndValidateSmoke()=>Import(false,true);
 public static void RunAndValidateTextures()=>Import(false,false,true);
 public static void RunAndValidateMips()=>Import(false,false,false,true);
 static void Import(bool preview,bool validateSmoke=false,bool validateTextures=false,bool validateMips=false)
 {
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();copies.Clear();sourceTextures.Clear();
  var bundles=new List<AssetBundle>();GameObject copy=null;
  try
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})foreach(var file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
   var main=AssetBundle.LoadFromFile("Logs/draug-recovered.bundle");bundles.Add(main);
   foreach(var kind in new[]{"muzzle","projectile","flask","warning","explosion"})
   {
    var original=main.LoadAsset<GameObject>("assets/recovered/draug_"+kind+".prefab");if(!original)throw new Exception("Missing FX "+kind);
    copy=Object.Instantiate(original);copy.name="Draug_"+kind;
    foreach(var c in copy.GetComponentsInChildren<MonoBehaviour>(true))if(c)Object.DestroyImmediate(c);
    foreach(var t in copy.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    foreach(var r in copy.GetComponentsInChildren<Renderer>(true))
    {
     var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=MaterialCopy(mats[i]);r.sharedMaterials=mats;
     if(r is ParticleSystemRenderer particle)
     {
      particle.trailMaterial=MaterialCopy(particle.trailMaterial);
      if(particle.meshCount>0){var meshes=new Mesh[particle.meshCount];particle.GetMeshes(meshes);for(int i=0;i<meshes.Length;i++)meshes[i]=MeshCopy(meshes[i]);particle.SetMeshes(meshes);}
     }
     if(r.TryGetComponent<MeshFilter>(out var filter))filter.sharedMesh=MeshCopy(filter.sharedMesh);
    } // Preserve disabled source debug renderers as disabled.
    foreach(var collider in copy.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
    foreach(var ps in copy.GetComponentsInChildren<ParticleSystem>(true))
    {
     ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var settings=ps.main;settings.playOnAwake=false;
     var shape=ps.shape;if(shape.mesh)shape.mesh=MeshCopy(shape.mesh);
    }
    copy.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
    if(!PrefabUtility.SaveAsPrefabAsset(copy,Folder+"/Draug_"+kind+".prefab"))throw new Exception("Save failed "+kind);
    Debug.Log($"DRAUG_FX_IMPORTED {kind} particles={copy.GetComponentsInChildren<ParticleSystem>(true).Length} shaderApproximation=True");
    Object.DestroyImmediate(copy);copy=null;
   }
   AssetDatabase.SaveAssets();Debug.Log("DRAUG_FX_IMPORT_PASS");
  }
  finally {if(copy)Object.DestroyImmediate(copy);foreach(var b in bundles)if(b)b.Unload(true);copies.Clear();sourceTextures.Clear();}
  if(validateMips)DraugTextureRoundTripValidation.RunMipChain();else if(validateTextures)DraugTextureRoundTripValidation.Run();else if(validateSmoke)DraugSmokeGpuValidation.Run();else if(preview)DraugFxPreview.Run();else EditorApplication.Exit(0);
 }
}


