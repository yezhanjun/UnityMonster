using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class GunSwordImportValidation
{
    const string Root="Assets/AssetStore/Gun and Sword Animation Pack";
    public static void Run()
    {
        try
        {
            int clips=0,models=0,animators=0,controllers=0,renderers=0;
            foreach(string file in Directory.GetFiles(Root,"*.anim",SearchOption.AllDirectories))
            {if(!AssetDatabase.LoadAssetAtPath<AnimationClip>(file.Replace('\\','/')))throw new Exception("Clip failed import: "+file);clips++;}
            foreach(string file in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))
            {if(!AssetDatabase.LoadMainAssetAtPath(file.Replace('\\','/')))throw new Exception("FBX failed import: "+file);models++;}
            foreach(string file in Directory.GetFiles(Root,"*.controller",SearchOption.AllDirectories))
            {
                var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(file.Replace('\\','/'));
                if(!controller)throw new Exception("Controller failed import: "+file);
                foreach(var clip in controller.animationClips)if(!clip)throw new Exception("Missing controller motion: "+file);
                controllers++;
            }
            var scene=EditorSceneManager.OpenScene(Root+"/Scenes/Demo.unity");
            foreach(var root in scene.GetRootGameObjects())
            {
                if(PrefabUtility.IsPrefabAssetMissing(root))throw new Exception("Missing scene prefab: "+root.name);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
                foreach(var a in root.GetComponentsInChildren<Animator>(true))
                {
                    // Original Demo is a static model preview; no controller is assigned.
                    animators++;
                }
                renderers+=root.GetComponentsInChildren<Renderer>(true).Length;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach(var mat in renderer.sharedMaterials)if(!mat||!mat.shader)throw new Exception("Missing material/shader: "+renderer.name);
            }
            if(clips!=618||models!=621||controllers!=622||renderers<1)throw new Exception("Unexpected import counts: "+clips+"/"+models+"/"+controllers+"/"+renderers);
            File.WriteAllText("validation.json","{\"status\":\"PASS_IMPORT_AND_SCENE_REFERENCES\",\"nativeClips\":"+clips+",\"fbxAssets\":"+models+",\"sceneAnimators\":"+animators+",\"environment\":\"isolated Unity 6000.2.10f1 project with URP 17.2 and UGUI 2.0\",\"limits\":\"No exhaustive animation or Play-mode/render verification; source missing normal-map reference remains documented.\"}");
            Debug.Log("GUN_SWORD_IMPORT_PASS "+clips+" clips / "+models+" FBX / "+controllers+" controllers / "+renderers+" scene renderers");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
