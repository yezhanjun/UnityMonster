using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class ElfWomanMissingPoseRepair {
  public static void Run() {
   const string folder="Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElfWoman";
   var model=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/ElfWoman/elf_woman_sur"));
   var properties=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
   foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{folder})) {
    string path=AssetDatabase.GUIDToAssetPath(guid); if(!path.EndsWith(".anim"))continue;
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path); var existing=new HashSet<string>();
    foreach(var binding in AnimationUtility.GetCurveBindings(clip)) if(binding.type==typeof(Transform)) existing.Add(binding.path+"|"+binding.propertyName);
    var bindings=new List<EditorCurveBinding>(); var curves=new List<AnimationCurve>();
    foreach(var bone in model.GetComponentsInChildren<Transform>()) {
     string bonePath=AnimationUtility.CalculateTransformPath(bone,model.transform);
     var p=bone.localPosition;var q=bone.localRotation;var s=bone.localScale;
     var values=new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
     for(int i=0;i<properties.Length;i++) if(!existing.Contains(bonePath+"|"+properties[i])) {
      bindings.Add(EditorCurveBinding.FloatCurve(bonePath,typeof(Transform),properties[i]));
      curves.Add(new AnimationCurve(new Keyframe(0,values[i],0,0),new Keyframe(clip.length,values[i],0,0)));
     }
    }
    AnimationUtility.SetEditorCurves(clip,bindings.ToArray(),curves.ToArray()); EditorUtility.SetDirty(clip);
    Debug.Log($"ELF_MISSING_POSE_REPAIR {clip.name} added={bindings.Count}");
   }
   Object.DestroyImmediate(model);AssetDatabase.SaveAssets(); Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}