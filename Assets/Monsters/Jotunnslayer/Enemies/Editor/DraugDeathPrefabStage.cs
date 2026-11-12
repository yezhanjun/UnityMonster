using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Portable native hierarchy only. Source game behaviours and body shader parity
// remain separate gates: this asset is deliberately not connected to production.
public static class DraugDeathPrefabStage
{
 const string Folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/DeathFx";
 const string Prefab=Folder+"/DraugDeathDecodedStage.prefab";
 [Serializable] class Report
 {
  public string status="STAGED_NATIVE_HIERARCHY_NOT_RUNTIME_READY";
  public int nodes,particles,bodies,colliders,meshes,removedMissingScripts,remappedReferences;
  public string limits="Existing local body material remains approximate (missing source normal/packed maps). Missing source scripts removed only from staged clone; source behaviour evidence retained separately. No WreckSpawner/audio/lifecycle implementation, particle simulation or final visual parity claim.";
 }
 static void Check(GameObject root)
 {
  if(root.GetComponentsInChildren<Transform>(true).Length!=26 || root.GetComponentsInChildren<ParticleSystem>(true).Length!=15 || root.GetComponentsInChildren<Rigidbody>(true).Length!=9 || root.GetComponentsInChildren<MeshCollider>(true).Length!=9)throw new Exception("Staged hierarchy counts changed");
  foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)throw new Exception("Staged missing script");
   foreach(var component in t.GetComponents<Component>())
   {
    var serialized=new SerializedObject(component);var p=serialized.GetIterator();
    while(p.Next(true))
    {
     if(p.propertyType!=SerializedPropertyType.ObjectReference)continue;
     var value=p.objectReferenceValue;if(!value)continue;
     if(value is GameObject go && (go.transform==root.transform || go.transform.IsChildOf(root.transform)))continue;
     if(value is Component c && (c.transform==root.transform || c.transform.IsChildOf(root.transform)))continue;
     string path=AssetDatabase.GetAssetPath(value);
     if(!path.StartsWith("Assets/",StringComparison.Ordinal) && !path.StartsWith("Packages/",StringComparison.Ordinal) && !path.StartsWith("Resources/",StringComparison.Ordinal))throw new Exception("Unresolved staged reference "+t.name+"/"+p.propertyPath+" "+value.name+" path="+path);
    }
   }
  }
 }
 public static void ValidateReload()
 {
  var root=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);if(!root)throw new Exception("Staged prefab missing");Check(root);DraugDeathDecodedGeometry.Validate();
  Debug.Log("DRAUG_DEATH_STAGE_RELOAD_PASS nodes=26 particles=15 bodies=9 colliders=9 runtimeReady=False");EditorApplication.Exit(0);
 }
 public static void Run()
 {
  var report=new Report();var bundles=new List<AssetBundle>();var meshes=new Dictionary<Mesh,Mesh>();GameObject clone=null;
  try
  {
   Directory.CreateDirectory(Folder+"/GeometryDecoded");AssetDatabase.Refresh();
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})foreach(var file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
   var bundle=AssetBundle.LoadFromFile("Logs/draug-death-recovered.bundle");bundles.Add(bundle);
   var root=bundle.LoadAsset<GameObject>("assets/recovered/draugdeath_visual.prefab");
   clone=Object.Instantiate(root);clone.name="DraugDeathDecodedStage";
   foreach(var t in clone.GetComponentsInChildren<Transform>(true))
   {
    report.removedMissingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    foreach(var component in t.GetComponents<Component>())
    {
     if(component is MonoBehaviour)throw new Exception("Unexpected executable source behaviour");
     var serialized=new SerializedObject(component);var p=serialized.GetIterator();
     while(p.Next(true))
     {
      if(p.propertyType!=SerializedPropertyType.ObjectReference)continue;
      var value=p.objectReferenceValue;
      if(value is Mesh mesh)
      {
       if(!meshes.TryGetValue(mesh,out var local))
       {
        local=DraugDeathDecodedGeometry.Import(mesh);
        if(!local)
        {
         if(mesh.name!="Cube")throw new Exception("Unmapped decoded mesh "+mesh.name);
         string path=Folder+"/GeometryDecoded/Cube.asset";
         var copy=Object.Instantiate(mesh);local=AssetDatabase.LoadAssetAtPath<Mesh>(path);
         if(local){EditorUtility.CopySerialized(copy,local);Object.DestroyImmediate(copy);}else{AssetDatabase.CreateAsset(copy,path);local=copy;}
        }
        meshes.Add(mesh,local);
       }
       p.objectReferenceValue=local;report.remappedReferences++;
      }
      else if(value is Material material)
      {
       string path=material.name=="draugh_ranged_mat"?"Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/draugh_ranged_mat.mat":Folder+"/"+material.name+".mat";
       var local=AssetDatabase.LoadAssetAtPath<Material>(path);if(!local)throw new Exception("Missing mapped material "+material.name);
       p.objectReferenceValue=local;report.remappedReferences++;
      }
     }
     serialized.ApplyModifiedPropertiesWithoutUndo();
    }
   }
   Check(clone);
   report.nodes=clone.GetComponentsInChildren<Transform>(true).Length;report.particles=clone.GetComponentsInChildren<ParticleSystem>(true).Length;
   report.bodies=clone.GetComponentsInChildren<Rigidbody>(true).Length;report.colliders=clone.GetComponentsInChildren<MeshCollider>(true).Length;report.meshes=meshes.Count;
   PrefabUtility.SaveAsPrefabAsset(clone,Prefab);AssetDatabase.SaveAssets();
  }
  finally
  {
   if(clone)Object.DestroyImmediate(clone);
   foreach(var bundle in bundles)if(bundle)bundle.Unload(true);
  }
  AssetDatabase.ImportAsset(Prefab,ImportAssetOptions.ForceUpdate);Check(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
  DraugDeathDecodedGeometry.Validate();
  File.WriteAllText("Docs/Validation/DraugDeathDecodedStageVerification.json",JsonUtility.ToJson(report,true));
  Debug.Log($"DRAUG_DEATH_STAGE_PASS nodes={report.nodes} particles={report.particles} meshes={report.meshes} missingScriptsRemoved={report.removedMissingScripts} runtimeReady=False");
  EditorApplication.Exit(0);
 }
}
