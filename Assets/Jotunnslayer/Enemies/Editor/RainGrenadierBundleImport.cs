using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Jotunnslayer {
 public static class RainGrenadierBundleImport {
  const string Source="E:/SteamLibrary/steamapps/common/Risk of Rain 2/Risk of Rain 2_Data/StreamingAssets/aa/StandaloneWindows64";
  static Dictionary<Object,Object> copies; static string folder; static int serial;
  static string Safe(string value)=>System.Text.RegularExpressions.Regex.Replace(value,@"[^A-Za-z0-9_.-]","_");
  static T Persist<T>(T source) where T:Object {
   if(!source)return null; if(copies.TryGetValue(source,out var saved))return (T)saved;
   if(source is Texture2D texture){
    var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
    var previous=RenderTexture.active;Graphics.Blit(texture,rt);RenderTexture.active=rt;
    var readable=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
    readable.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readable.Apply();
    string path=folder+"/"+Safe(texture.name)+"_"+(serial++)+".png";File.WriteAllBytes(path,readable.EncodeToPNG());
    RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(readable);AssetDatabase.ImportAsset(path);
    var result=AssetDatabase.LoadAssetAtPath<Texture2D>(path);copies[source]=result;return (T)(Object)result;
   }
   var clone=Object.Instantiate(source); clone.name=source.name;
   if (clone is AnimationClip clip) AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
   string extension=source is AnimationClip?".anim":".asset";
   AssetDatabase.CreateAsset(clone,folder+"/"+Safe(source.name)+"_"+(serial++)+extension); copies[source]=clone;return clone;
  }
  static Material MaterialCopy(Material original) {
   if(original&&copies.TryGetValue(original,out var saved))return (Material)saved;
   var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=original?original.name:"Imported material";
   if(original) {
    Texture texture=original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
    if(!texture&&original.HasProperty("_BaseMap"))texture=original.GetTexture("_BaseMap");
    if(texture is Texture2D tex)material.SetTexture("_BaseMap",Persist(tex));
    if(original.HasProperty("_Color"))material.SetColor("_BaseColor",original.GetColor("_Color"));
   }
   AssetDatabase.CreateAsset(material,folder+"/"+Safe(material.name)+"_"+(serial++)+".mat");if(original)copies[original]=material;return material;
  }
  public static void Run() {
   foreach(string key in new[]{"claygrenadier"}) {
    copies=new Dictionary<Object,Object>();serial=0;folder="Assets/Resources/RainActors/PendingWhitebox/"+key;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
    var bundles=new List<AssetBundle>();
    foreach(string pattern in new[]{"ror2-dlc1-"+key+"_static_assets_all_*.bundle","ror2-dlc1-"+key+"_assets_all_*.bundle"})
     foreach(string path in Directory.GetFiles(Source,pattern)){var bundle=AssetBundle.LoadFromFile(path);if(!bundle)throw new Exception("Cannot load "+path);bundles.Add(bundle);}
    GameObject prefab=null;
    foreach(var bundle in bundles)foreach(string asset in bundle.GetAllAssetNames())if(Path.GetFileName(asset).StartsWith("mdl",StringComparison.OrdinalIgnoreCase)) {
     Debug.Log("PENDING_BUNDLE_MODEL "+key+" "+asset);
     var candidate=bundle.LoadAsset<GameObject>(asset);if(candidate&&candidate.name.Equals("mdlClayGrenadier",StringComparison.OrdinalIgnoreCase))prefab=candidate;
    }
    if(!prefab){foreach(var bundle in bundles)foreach(string asset in bundle.GetAllAssetNames())Debug.Log("PENDING_BUNDLE_ASSET "+asset);throw new Exception("Model missing "+key);}
    var model=Object.Instantiate(prefab);model.name=prefab.name;
    foreach(var t in model.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)Object.DestroyImmediate(script);
    foreach(var c in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
    foreach(var c in model.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(c);
    foreach(var lod in model.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(lod);
    foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))filter.sharedMesh=Persist(filter.sharedMesh);
    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.sharedMesh=Persist(skin.sharedMesh);skin.updateWhenOffscreen=true;}
    foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)) {
     var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=MaterialCopy(materials[i]);renderer.sharedMaterials=materials;renderer.enabled=true;
    }
    foreach(var animator in model.GetComponentsInChildren<Animator>(true)){animator.runtimeAnimatorController=null;animator.avatar=Persist(animator.avatar);animator.fireEvents=false;}
    int clips=0;foreach(var bundle in bundles)foreach(var clip in bundle.LoadAllAssets<AnimationClip>()){Persist(clip);clips++;Debug.Log("PENDING_BUNDLE_CLIP "+key+" "+clip.name);}
    PrefabUtility.SaveAsPrefabAsset(model,folder+"/"+model.name+".prefab");
    Debug.Log($"PENDING_BUNDLE_IMPORTED {key} renderers={model.GetComponentsInChildren<Renderer>(true).Length} clips={clips} assets={copies.Count}");Object.DestroyImmediate(model);
    AssetDatabase.SaveAssets();foreach(var bundle in bundles)bundle.Unload(true);
   }
   RepairMaterials();
  }
  public static void RepairMaterials(){
   foreach(string key in new[]{"claygrenadier"}){
    copies=new Dictionary<Object,Object>();serial=1000;folder="Assets/Resources/RainActors/PendingWhitebox/"+key;
    var bundles=new List<AssetBundle>();Texture2D diffuse=null;
    foreach(string pattern in new[]{"ror2-dlc1-"+key+"_static_assets_all_*.bundle","ror2-dlc1-"+key+"_assets_all_*.bundle"})foreach(string path in Directory.GetFiles(Source,pattern))bundles.Add(AssetBundle.LoadFromFile(path));
    foreach(var bundle in bundles)foreach(var texture in bundle.LoadAllAssets<Texture2D>()){
     Debug.Log("PENDING_TEXTURE "+key+" "+texture.name);
     if(texture.name.Equals("texClayGrenadierDiffuse",StringComparison.OrdinalIgnoreCase)&&(texture.name.IndexOf("Diffuse",StringComparison.OrdinalIgnoreCase)>=0||texture.name.IndexOf("Albedo",StringComparison.OrdinalIgnoreCase)>=0))diffuse=texture;
    }
    if(!diffuse)throw new Exception("No diffuse for "+key);
    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=key+" source color";material.SetColor("_BaseColor",Color.white);material.SetColor("_Color",Color.white);material.SetTexture("_BaseMap",Persist(diffuse));AssetDatabase.CreateAsset(material,folder+"/SourceColor.mat");
    string prefabPath=folder+"/"+("mdlClayGrenadier")+".prefab";
    var model=PrefabUtility.LoadPrefabContents(prefabPath);foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)){var slots=renderer.sharedMaterials;for(int j=0;j<slots.Length;j++)slots[j]=material;renderer.sharedMaterials=slots;}
    PrefabUtility.SaveAsPrefabAsset(model,prefabPath);PrefabUtility.UnloadPrefabContents(model);AssetDatabase.SaveAssets();foreach(var bundle in bundles)bundle.Unload(true);
   }
   foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Resources/RainActors/PendingWhitebox/claygrenadier"})) {
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
    if(!clip.name.StartsWith("PendingGrenadier_"))clip.name="PendingGrenadier_"+clip.name;
    AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());EditorUtility.SetDirty(clip);
   }
   AssetDatabase.SaveAssets();Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}