using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class HumanoidDisplayAvatarRepair {
  public static void Run() {
   foreach(string relative in new[]{"SkeletonBowRegular/skeleton_bow_VarRegular","SkeletonGuardian/skeleton_guardian_sur","SphinxSkeleton/skeleton_1h_sphinx_sur"}) {
    string path="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/"+relative+".fbx";
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); var model=Object.Instantiate(prefab); model.name=prefab.name;
    var animator=model.GetComponent<Animator>(); float oldScale=animator.humanScale;
    var desc=((ModelImporter)AssetImporter.GetAtPath(path)).humanDescription;
    var transforms=model.GetComponentsInChildren<Transform>(); var skeleton=new SkeletonBone[transforms.Length];
    for(int i=0;i<skeleton.Length;i++) skeleton[i]=new SkeletonBone{name=transforms[i].name,position=transforms[i].localPosition,rotation=transforms[i].localRotation,scale=transforms[i].localScale};
    desc.skeleton=skeleton;
    var avatar=AvatarBuilder.BuildHumanAvatar(model,desc); avatar.name=prefab.name+" Display Avatar";
    if(!avatar.isValid||!avatar.isHuman) throw new System.InvalidOperationException("Invalid display avatar "+relative);
    animator.avatar=avatar;
    Debug.Log($"DISPLAY_AVATAR_REPAIR {relative} before={oldScale} after={animator.humanScale} valid={avatar.isValid}");
    string dest=path.Substring(0,path.LastIndexOf('/')+1)+"DisplayAvatar.asset";
    var existing=AssetDatabase.LoadAssetAtPath<Avatar>(dest); if(existing){EditorUtility.CopySerialized(avatar,existing);Object.DestroyImmediate(avatar);} else AssetDatabase.CreateAsset(avatar,dest);
    Object.DestroyImmediate(model);
   }
   AssetDatabase.SaveAssets(); Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}