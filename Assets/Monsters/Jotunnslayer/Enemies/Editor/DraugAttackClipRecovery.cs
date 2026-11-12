#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugAttackClipRecovery
 {
  public static void Run()
  {
   const string folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower";
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new List<AssetBundle>();GameObject model=null;
   var type=typeof(BeetleBundleImport);const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
   var folderField=type.GetField("Folder",flags);var profileField=type.GetField("Profile",flags);
   object oldFolder=folderField.GetValue(null),oldProfile=profileField.GetValue(null);
   try
   {
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(source,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var animator=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    var clips=animator.runtimeAnimatorController.animationClips;
    model=Object.Instantiate(animator.gameObject);
    foreach(var a in model.GetComponentsInChildren<Animator>(true))a.fireEvents=false;
    folderField.SetValue(null,folder);profileField.SetValue(null,"draug_thrower_sur");
    foreach(string name in new[]{"attack_01","attack_02","attack_03"})
    {
     var original=clips.First(c=>c && c.name==name);string path=folder+"/"+name+".anim";
     if(!File.Exists(path))type.GetMethod("Bake",flags).Invoke(null,new object[]{model,original});
     var baked=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
     if(!baked || AnimationUtility.GetCurveBindings(baked).Length==0)throw new Exception("Missing recovered clip "+name);
     var bones=model.GetComponentsInChildren<Transform>(true);var positions=new Vector3[bones.Length];var rotations=new Quaternion[bones.Length];
     float maxPosition=0,maxRotation=0;
     // Exact export sample points verify binding and transform preservation, not between-frame interpolation.
     int frames=Mathf.Clamp(Mathf.CeilToInt(original.length*30),2,180);
     for(int frame=0;frame<=frames;frame++)
     {
      float time=original.length*frame/frames;original.SampleAnimation(model,time);
      for(int i=1;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
      baked.SampleAnimation(model,time);
      for(int i=1;i<bones.Length;i++){maxPosition=Mathf.Max(maxPosition,Vector3.Distance(positions[i],bones[i].localPosition));maxRotation=Mathf.Max(maxRotation,Quaternion.Angle(rotations[i],bones[i].localRotation));}
     }
     if(maxPosition>.001f || maxRotation>.1f)throw new Exception($"Pose mismatch {name} pos={maxPosition} angle={maxRotation}");
     foreach(var ev in AnimationUtility.GetAnimationEvents(original))Debug.Log($"DRAUG_BASE_EVENT {name} time={ev.time} event={ev.functionName}");
     Debug.Log($"DRAUG_BASE_CLIP_PASS {name} duration={baked.length} curves={AnimationUtility.GetCurveBindings(baked).Length} samples={frames+1} maxPosition={maxPosition} maxAngle={maxRotation}");
    }
    AssetDatabase.SaveAssets();Debug.Log("DRAUG_BASE_CLIPS_RECOVERED");EditorApplication.Exit(0);
   }
   catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
   finally
   {
    folderField.SetValue(null,oldFolder);profileField.SetValue(null,oldProfile);
    if(model)Object.DestroyImmediate(model);foreach(var bundle in bundles)if(bundle)bundle.Unload(true);
   }
  }
 }
}
#endif
