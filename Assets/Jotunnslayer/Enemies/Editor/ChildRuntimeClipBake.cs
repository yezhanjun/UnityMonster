using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Jotunnslayer {
 public static class ChildRuntimeClipBake {
  public static void Run(){
   const string source="E:/SteamLibrary/steamapps/common/Risk of Rain 2/Risk of Rain 2_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new System.Collections.Generic.List<AssetBundle>();
   foreach(string pattern in new[]{"ror2-dlc2-child_static_assets_all_*.bundle","ror2-dlc2-child_assets_all_*.bundle"})foreach(string file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
   string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
   int bakedCount=0;
   foreach(var bundle in bundles)foreach(var original in bundle.LoadAllAssets<AnimationClip>()){
    if(original.name!="Child_Idle_Single"&&original.name!="Child_Run_F"&&original.name!="Child_Spark_Launch")continue;
    var model=Object.Instantiate(Resources.Load<GameObject>("RainActors/PendingWhitebox/child/mdlChild"));
    var animator=model.GetComponentInChildren<Animator>();if(animator)animator.fireEvents=false;
    var bones=model.GetComponentsInChildren<Transform>();
    var curves=new AnimationCurve[bones.Length*10];for(int i=0;i<curves.Length;i++)curves[i]=new AnimationCurve();
    var initial=new Quaternion[bones.Length];float maxAngle=0;
    int frames=Mathf.Clamp(Mathf.CeilToInt(original.length*30),2,120);
    Debug.Log($"CHILD_RUNTIME_SOURCE {original.name} duration={original.length} sourceBindings={AnimationUtility.GetCurveBindings(original).Length}");
    for(int frame=0;frame<=frames;frame++){
     float time=original.length*frame/frames;original.SampleAnimation(model,time);
     for(int i=1;i<bones.Length;i++){
      var t=bones[i];var p=t.localPosition;var q=t.localRotation;var scale=t.localScale;
      if(frame==0)initial[i]=q;else maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(initial[i],q));
      float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w,scale.x,scale.y,scale.z};
      for(int axis=0;axis<10;axis++){if(float.IsNaN(values[axis])||float.IsInfinity(values[axis]))throw new Exception("Nonfinite source pose");curves[i*10+axis].AddKey(new Keyframe(time,values[axis],0,0));}
     }
    }
    Debug.Log($"CHILD_RUNTIME_POSE {original.name} maxAngle={maxAngle} bones={bones.Length} frames={frames}");
    if(original.name!="Child_Idle_Single"&&maxAngle<.1f)throw new Exception("Source sampling has no verified motion: "+original.name);
    var clip=new AnimationClip();clip.frameRate=30;
    for(int i=1;i<bones.Length;i++){string path=AnimationUtility.CalculateTransformPath(bones[i],model.transform);for(int axis=0;axis<10;axis++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[axis]),curves[i*10+axis]);}
    clip.EnsureQuaternionContinuity();
    string folder="Assets/Resources/RainActors/PendingWhitebox/child";var pathMatch=Array.Find(Directory.GetFiles(folder,"*.anim"),p=>Path.GetFileName(p).StartsWith(original.name+"_",StringComparison.Ordinal));
    if(pathMatch==null)throw new Exception("Destination missing "+original.name);
    var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(pathMatch);clip.name=existing.name;EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);
    Debug.Log($"CHILD_RUNTIME_BAKED {existing.name} bindings={AnimationUtility.GetCurveBindings(existing).Length}");
    Object.DestroyImmediate(clip);Object.DestroyImmediate(model);bakedCount++;
   }
   AssetDatabase.SaveAssets();foreach(var bundle in bundles)bundle.Unload(true);
   if(bakedCount!=3)throw new Exception("Expected 3 clips, got "+bakedCount);
   Skeleton1HHostSmoke.RunAlfheimBatch();
  }
  public static void VerifySavedAndRun(){
   int verified=0;
   foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Resources/RainActors/PendingWhitebox/child"})){
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
    if(!clip.name.StartsWith("Child_Idle_Single_")&&!clip.name.StartsWith("Child_Run_F_")&&!clip.name.StartsWith("Child_Spark_Launch_"))continue;
    int count=AnimationUtility.GetCurveBindings(clip).Length;
    if(count<400)throw new Exception("Saved clip missing baked curves: "+clip.name);
    var settings=AnimationUtility.GetAnimationClipSettings(clip);
    settings.loopTime=!clip.name.StartsWith("Child_Spark_Launch_");AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
    Debug.Log($"CHILD_SAVED_CLIP {clip.name} bindings={count} duration={clip.length} loop={settings.loopTime}");verified++;
   }
   if(verified!=3)throw new Exception("Expected three baked clips");
   AssetDatabase.SaveAssets();Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}