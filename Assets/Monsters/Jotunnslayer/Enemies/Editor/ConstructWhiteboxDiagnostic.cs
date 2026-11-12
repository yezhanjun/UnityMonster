using UnityEngine;
namespace Jotunnslayer {
 public static class ConstructWhiteboxDiagnostic {
  public static void Run() {
   var go=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/ConstructBoss/jotunn_construct_sur"));
   foreach(var r in go.GetComponentsInChildren<Renderer>(true)) {
    string path=r.name; for(var t=r.transform.parent;t&&t!=go.transform;t=t.parent) path=t.name+"/"+path;
    foreach(var m in r.sharedMaterials) Debug.Log($"CONSTRUCT_RENDERER path={path} type={r.GetType().Name} bounds={r.bounds} scale={r.transform.lossyScale} material={(m?m.name:"null")} shader={(m&&m.shader?m.shader.name:"null")}");
   }
      foreach(var filter in go.GetComponentsInChildren<MeshFilter>()) {
    var clone=Object.Instantiate(filter.sharedMesh); clone.RecalculateBounds();
    Debug.Log($"CONSTRUCT_RIGID_ACTUAL name={filter.name} imported={filter.sharedMesh.bounds} actual={clone.bounds}"); Object.DestroyImmediate(clone);
   }
   foreach(string name in new[]{"jotunn_construct_init","jotunn_construct_idle","jotunn_construct_saw_v5"}) {
    var clip=Resources.Load<AnimationClip>("Jotunnslayer/ConstructBoss/"+name);
    int missing=0; foreach(var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip)) if(binding.type==typeof(Transform)&&!go.transform.Find(binding.path)) missing++;
    Debug.Log($"CONSTRUCT_BINDINGS {name} missing={missing}");
    foreach(float time in new[]{0f,.5f,2f}) {
     clip.SampleAnimation(go,time);
     foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
      var mesh=new Mesh(); skin.BakeMesh(mesh); var verts=mesh.vertices; var weights=skin.sharedMesh.boneWeights;
      int worst=0; for(int vi=1;vi<verts.Length;vi++) if(verts[vi].sqrMagnitude>verts[worst].sqrMagnitude)worst=vi;
      mesh.RecalculateBounds(); var w=weights[worst]; var bones=skin.bones;
      Debug.Log($"CONSTRUCT_BAKED {name} time={time} bounds={mesh.bounds} worst={worst} pos={verts[worst]} bone0={w.boneIndex0}:{w.weight0} bone1={w.boneIndex1}:{w.weight1} bone2={w.boneIndex2}:{w.weight2} bone3={w.boneIndex3}:{w.weight3}");
      foreach(int bi in new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3}) if(bi<bones.Length) Debug.Log($"CONSTRUCT_WORST_BONE {bi} name={(bones[bi]?bones[bi].name:"NULL")} local={(bones[bi]?bones[bi].localPosition:Vector3.zero)} scale={(bones[bi]?bones[bi].lossyScale:Vector3.zero)}");
      Object.DestroyImmediate(mesh);
     }
     foreach(var r in go.GetComponentsInChildren<Renderer>(true)) if(r.bounds.size.magnitude>30f) Debug.Log($"CONSTRUCT_LARGE {name} time={time} renderer={r.name} bounds={r.bounds} scale={r.transform.lossyScale}");
    }
   }
   Object.DestroyImmediate(go); Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}