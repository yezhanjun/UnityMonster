using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class ElfWomanDisplayAvatarRepair {
  public static void Run() {
   var prefab=Resources.Load<GameObject>("Jotunnslayer/ElfWoman/elf_woman_sur");
   var model=Object.Instantiate(prefab); model.name=prefab.name;
   var avatar=AvatarBuilder.BuildGenericAvatar(model,""); avatar.name="ElfWoman Display Avatar";
   if(!avatar.isValid) throw new System.InvalidOperationException("Invalid rebuilt elf generic avatar");
   string path="Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/ElfWoman/DisplayAvatar.asset";
   var previous=AssetDatabase.LoadAssetAtPath<Avatar>(path); if(previous){EditorUtility.CopySerialized(avatar,previous);Object.DestroyImmediate(avatar);} else AssetDatabase.CreateAsset(avatar,path);
   Debug.Log("ELF_GENERIC_AVATAR_REPAIR valid="+avatar.isValid);
   Object.DestroyImmediate(model); AssetDatabase.SaveAssets(); Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}