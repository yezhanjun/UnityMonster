#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugControllerExportValidation
 {
  public static void Run()
  {
   const string folder="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   const string destination="Assets/Monsters/Jotunnslayer/Enemies/Editor/DraugPoseCandidates/SourceGraphCandidate.controller";
   var bundles=new List<AssetBundle>();RuntimeAnimatorController clone=null;int exit=1;
   try
   {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(folder,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(folder,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var animator=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    var overrides=(AnimatorOverrideController)animator.runtimeAnimatorController;
    var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(pairs);
    clone=Object.Instantiate(overrides.runtimeAnimatorController);clone.name="DraugSourceGraphCandidate";
    var serialized=new SerializedObject(clone);
    var iterator=serialized.GetIterator();while(iterator.NextVisible(false))Debug.Log("DRAUG_CONTROLLER_PROPERTY "+iterator.propertyPath+" "+iterator.propertyType);
    var clips=serialized.FindProperty("m_AnimationClips");
    if(clips==null || !clips.isArray)throw new Exception("Compiled controller clip table unavailable; cannot export safely");
    int replaced=0,unmapped=0;
    for(int i=0;i<clips.arraySize;i++)
    {
     var item=clips.GetArrayElementAtIndex(i);var original=item.objectReferenceValue as AnimationClip;
     var pair=pairs.FirstOrDefault(p=>p.Key==original);var effective=pair.Value?pair.Value:original;
     var baked=effective?Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/"+effective.name):null;
     Debug.Log($"DRAUG_CONTROLLER_CLIP slot={i} original={(original?original.name:"null")} effective={(effective?effective.name:"null")} baked={(baked?baked.name:"null")}");
     if(baked){item.objectReferenceValue=baked;replaced++;}else unmapped++;
    }
    serialized.ApplyModifiedPropertiesWithoutUndo();
    if(File.Exists(destination))throw new Exception("Preserving existing candidate; choose a new path before exporting");
    AssetDatabase.CreateAsset(clone,destination);AssetDatabase.SaveAssets();
    Debug.Log($"DRAUG_CONTROLLER_EXPORT candidate={destination} replaced={replaced} unmapped={unmapped} sourceClipSlots={clips.arraySize}; export only, reload not verified");exit=0;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{if(clone && !AssetDatabase.Contains(clone))Object.DestroyImmediate(clone);foreach(var bundle in bundles)if(bundle)bundle.Unload(true);}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
