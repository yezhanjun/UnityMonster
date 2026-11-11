using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class RainChildAssetAudit {
  public static void Run(){
   var model=Object.Instantiate(Resources.Load<GameObject>("RainActors/PendingWhitebox/child/mdlChild"));
   foreach(var animator in model.GetComponentsInChildren<Animator>(true))Debug.Log($"CHILD_ANIMATOR root={AnimationUtility.CalculateTransformPath(animator.transform,model.transform)} avatar={(animator.avatar?animator.avatar.name:"null")} human={animator.isHuman}");
   foreach(var r in model.GetComponentsInChildren<Renderer>(true))Debug.Log($"CHILD_RENDERER {r.name} type={r.GetType().Name} bounds={r.bounds}");
   foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Resources/RainActors/PendingWhitebox/child"})){
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));int missing=0;
    foreach(var b in AnimationUtility.GetCurveBindings(clip))if(b.type==typeof(Transform)&&b.path.Length>0&&!model.transform.Find(b.path))missing++;
    Debug.Log($"CHILD_CLIP_AUDIT {clip.name} length={clip.length} human={clip.humanMotion} missing={missing}");
   }
   Object.DestroyImmediate(model);EditorApplication.Exit(0);
  }
 }
}