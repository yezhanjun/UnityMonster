using UnityEditor;
using UnityEngine;
namespace Jotunnslayer {
 public static class RainPendingAssetsFinalize {
  public static void Run(){
   foreach(string family in new[]{"minimushroom","bell"}) {
    string folder="Assets/Resources/RainActors/PendingWhitebox/"+family;
    string prefix=family=="bell"?"PendingBell_":"PendingMini_";
    foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{folder})){
     var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
     if(!clip.name.StartsWith(prefix))clip.name=prefix+clip.name;
     AnimationUtility.SetAnimationEvents(clip,System.Array.Empty<AnimationEvent>());EditorUtility.SetDirty(clip);
    }
   }
   var keep=new System.Collections.Generic.HashSet<string>();
   foreach(string family in new[]{"minimushroom","bell"}){
    string folder="Assets/Resources/RainActors/PendingWhitebox/"+family;
    string prefab=folder+"/"+(family=="bell"?"mdlBell":"mdlMiniMushroom")+".prefab";
    foreach(string asset in AssetDatabase.GetDependencies(prefab,true))if(asset.StartsWith(folder))keep.Add(asset);
    foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{folder}))keep.Add(AssetDatabase.GUIDToAssetPath(guid));
    foreach(string file in System.IO.Directory.GetFiles(folder))if(!file.EndsWith(".meta")&&!keep.Contains(file.Replace('\\','/')))AssetDatabase.DeleteAsset(file.Replace('\\','/'));
   }
   System.IO.File.WriteAllLines("pending-whitebox-assets.txt",keep);
   AssetDatabase.SaveAssets();Skeleton1HHostSmoke.RunAlfheimBatch();
  }
 }
}