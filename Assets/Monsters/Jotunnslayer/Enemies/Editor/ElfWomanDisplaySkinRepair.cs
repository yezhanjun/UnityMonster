using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class ElfWomanDisplaySkinRepair {
  public static void Run() {
   var model=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/ElfWoman/elf_woman_sur"));
   var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(); var mesh=Object.Instantiate(skin.sharedMesh);
   var original=mesh.bindposes; var bones=skin.bones; var poses=new Matrix4x4[bones.Length]; float error=0;
   for(int i=0;i<bones.Length;i++) {
    poses[i]=bones[i].worldToLocalMatrix*skin.transform.localToWorldMatrix;
    for(int j=0;j<16;j++) error=Mathf.Max(error,Mathf.Abs(original[i][j]-poses[i][j]));
   }
   mesh.bindposes=poses; mesh.name="ElfWoman Display Skin";
   string path="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElfWoman/DisplaySkin.asset";
   var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);} else AssetDatabase.CreateAsset(mesh,path);
   Debug.Log($"ELF_DISPLAY_SKIN_REPAIR bones={bones.Length} maxBindDifference={error}");
   Object.DestroyImmediate(model); AssetDatabase.SaveAssets(); Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}