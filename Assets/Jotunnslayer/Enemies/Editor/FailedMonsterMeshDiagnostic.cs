using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class FailedMonsterMeshDiagnostic {
  public static void Run() {
   foreach(string path in new[]{"SkeletonBowRegular/skeleton_bow_VarRegular","SkeletonGuardian/skeleton_guardian_sur","SphinxSkeleton/skeleton_1h_sphinx_sur","ElfWoman/elf_woman_sur"}) {
    var model=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/"+path));
    var anim=model.GetComponent<Animator>();
    Debug.Log($"FAILED_AVATAR {path} human={anim.isHuman} humanScale={(anim.isHuman?anim.humanScale:0f)} rootScale={model.transform.lossyScale}");
    foreach(var t in model.GetComponentsInChildren<Transform>()) if(t.name=="Head"||t.name=="Neck"||t.name=="Hips") Debug.Log($"FAILED_REST_BONE {path} bone={t.name} local={t.localPosition} world={t.position} scale={t.lossyScale}");
    foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) {
     var clone=Object.Instantiate(filter.sharedMesh); clone.RecalculateBounds();
     Debug.Log($"FAILED_RIGID {path} {filter.name} stored={filter.sharedMesh.bounds} actual={clone.bounds}"); Object.DestroyImmediate(clone);
    }
    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) {
     var raw=Object.Instantiate(skin.sharedMesh); raw.RecalculateBounds();
     var baked=new Mesh();skin.BakeMesh(baked);baked.RecalculateBounds();
     Debug.Log($"FAILED_SKIN {path} {skin.name} bones={skin.bones.Length} scale={skin.transform.lossyScale} raw={raw.bounds} baked={baked.bounds}");
     Object.DestroyImmediate(raw); Object.DestroyImmediate(baked);
    }
    if(path.StartsWith("ElfWoman")) foreach(string clipName in new[]{"woman_spider_spawn_01","woman_spider_stand","woman_spider_walk","woman_spider_cast_02"}) {
     var clip=Resources.Load<AnimationClip>("Jotunnslayer/ElfWoman/"+clipName);
     foreach(float time in new[]{0f,.4f,1f}) {
      clip.SampleAnimation(model,time);
      foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) {
       var baked=new Mesh();skin.BakeMesh(baked);baked.RecalculateBounds();
       Debug.Log($"FAILED_POSE {clipName} t={time} {skin.name} bounds={baked.bounds}"); Object.DestroyImmediate(baked);
      }
     }
    }
    Object.DestroyImmediate(model);
   }
   Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}