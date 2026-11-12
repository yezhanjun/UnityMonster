using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class RainClayAssetAudit {
  public static void Run(){
   var model=Object.Instantiate(Resources.Load<GameObject>("RainActors/PendingWhitebox/claybruiser/mdlClayBruiser"));
   foreach(var r in model.GetComponentsInChildren<Renderer>(true))Debug.Log($"CLAY_RENDERER {r.name} type={r.GetType().Name} bounds={r.bounds}");
   foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Resources/RainActors/PendingWhitebox/claybruiser"})){
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));int missing=0;
    foreach(var b in AnimationUtility.GetCurveBindings(clip))if(b.type==typeof(Transform)&&b.path.Length>0&&!model.transform.Find(b.path))missing++;
    Debug.Log($"CLAY_CLIP_AUDIT {clip.name} length={clip.length} human={clip.humanMotion} missing={missing}");
   }
   Object.DestroyImmediate(model);EditorApplication.Exit(0);
  }
 }
}