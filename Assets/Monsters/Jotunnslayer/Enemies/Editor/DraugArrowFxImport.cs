using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class DraugArrowFxImport
{
 const string Folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/BaseArrowFx";
 static readonly Dictionary<Object,Object> copies=new Dictionary<Object,Object>();
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
 static Texture TextureCopy(Texture value)
 {
  if(!value)return null;
  if(copies.TryGetValue(value,out var found))return (Texture)found;
  var rt=RenderTexture.GetTemporary(value.width,value.height,0,RenderTextureFormat.ARGB32);
  var previous=RenderTexture.active;Texture2D tex=null;
  try
  {
   Graphics.Blit(value,rt);RenderTexture.active=rt;
   tex=new Texture2D(value.width,value.height,TextureFormat.RGBA32,false);
   tex.ReadPixels(new Rect(0,0,value.width,value.height),0,0);tex.Apply();
   if(value.name=="T_VFX_Dust_01" || value.name=="T_ring_indicator01")
   {
    var pixels=tex.GetPixels();
    for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(1,1,1,Mathf.Max(pixels[i].r,Mathf.Max(pixels[i].g,pixels[i].b)));
    tex.SetPixels(pixels);tex.Apply();
   }
   string path=Folder+"/texture_"+copies.Count+".png";
   File.WriteAllBytes(path,tex.EncodeToPNG());AssetDatabase.ImportAsset(path);
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.wrapMode=value.wrapMode;importer.SaveAndReimport();
   var result=AssetDatabase.LoadAssetAtPath<Texture2D>(path);copies[value]=result;return result;
  }
  finally {if(tex)Object.DestroyImmediate(tex);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
 }
 static Material MaterialCopy(Material original)
 {
  if(!original)return null;
  if(copies.TryGetValue(original,out var found))return (Material)found;
  bool sourcePacked=original.shader && (original.shader.name=="VFX_Klaus/fx_explosion_add" || original.shader.name=="VFX_Klaus/fx_explosion_apb");
  var shader=Shader.Find(sourcePacked?"Jotunnslayer/SourcePackedExplosion":"Universal Render Pipeline/Particles/Unlit");
  if(!shader || !shader.isSupported)throw new Exception("Missing or unsupported particle shader");
  var mat=new Material(shader);mat.name=original.name+(sourcePacked?"_SourcePacked":"_URPApproximation");
  bool additive=original.shader && original.shader.name=="VFX_Klaus/fx_explosion_add" || original.HasProperty("_DstBlend") && original.GetFloat("_DstBlend")==1;
  if(!sourcePacked)mat.SetFloat("_Surface",1);if(!sourcePacked)mat.SetFloat("_Blend",additive?2:0);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",additive?1:10);if(!sourcePacked){mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);}
  mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
  foreach(var key in new[]{"_Main_Tex","_mainTexture","Main_tex","_MainTex","_BaseMap","_MaskTexture","Texture2D_F593E37E","_MainMask"})
   if(original.HasProperty(key) && original.GetTexture(key)){mat.SetTexture("_BaseMap",TextureCopy(original.GetTexture(key)));mat.SetTextureScale("_BaseMap",original.GetTextureScale(key));mat.SetTextureOffset("_BaseMap",original.GetTextureOffset(key));break;}
  var color=Color.white;
  foreach(var key in new[]{"_FinalColor","_color","_Color","_BaseColor"})if(original.HasProperty(key)){color=original.GetColor(key);if(color.a>0)break;}
  // Custom dissolve alpha semantics are not URP opacity; preserve visible tint pending visual review.
  if(color.a<=0f)color.a=1;if(!sourcePacked)mat.SetColor("_BaseColor",color);
  if(sourcePacked)mat.SetFloat("_Emission_Power",additive && original.HasProperty("_Emission_Power")?original.GetFloat("_Emission_Power"):1f);
  var result=Save(mat,Folder+"/material_"+copies.Count+".mat");copies[original]=result;return result;
 }
 public static void Run()=>Import(false);
 public static void RunAndPreview()=>Import(true);
 static void Import(bool preview)
 {
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();copies.Clear();
  var bundles=new List<AssetBundle>();GameObject copy=null;
  try
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})foreach(var file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
   var main=AssetBundle.LoadFromFile("Logs/draug-arrow-recovered.bundle");bundles.Add(main);
   foreach(var kind in new[]{"muzzle","projectile","hit","end"})
   {
    var original=main.LoadAsset<GameObject>("assets/recovered/draugarrow_"+kind+".prefab");if(!original)throw new Exception("Missing FX "+kind);
    copy=Object.Instantiate(original);copy.name="DraugArrow_"+kind;
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
    if(!PrefabUtility.SaveAsPrefabAsset(copy,Folder+"/DraugArrow_"+kind+".prefab"))throw new Exception("Save failed "+kind);
    Debug.Log($"DRAUG_ARROW_FX_IMPORTED {kind} particles={copy.GetComponentsInChildren<ParticleSystem>(true).Length} shaderApproximation=True");
    Object.DestroyImmediate(copy);copy=null;
   }
   AssetDatabase.SaveAssets();Debug.Log("DRAUG_ARROW_FX_IMPORT_PASS");
  }
  finally {if(copy)Object.DestroyImmediate(copy);foreach(var b in bundles)if(b)b.Unload(true);copies.Clear();}
  if(preview)DraugArrowFxPreview.Run();else EditorApplication.Exit(0);
 }
}


