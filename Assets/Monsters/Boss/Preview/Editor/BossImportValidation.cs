using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BossPreview.Editor
{
    [InitializeOnLoad]
    public static class BossImportValidation
    {
        static double start;
        static int stage;
        static string failure;
        static BossImportValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Route;
            Application.logMessageReceived += (m,t,k) => { if (SessionState.GetBool("BossImportTest",false) && (k==LogType.Error||k==LogType.Exception)) failure=m; };
        }
        static void Route(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("BossImportExit",false))
            {SessionState.EraseBool("BossImportExit");EditorApplication.delayCall += () => EditorApplication.Exit(0);}
            if(state==PlayModeStateChange.ExitingEditMode && EditorScenePaths.IsPreview(SceneManager.GetActiveScene().path))
            {
                SessionState.SetString("BossPreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool("BossOwnsStart",true);
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneManager.GetActiveScene().path);
            }
            if((state==PlayModeStateChange.EnteredPlayMode||state==PlayModeStateChange.EnteredEditMode)&&SessionState.GetBool("BossOwnsStart",false))
            {
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("BossPreviousStart",""));
                SessionState.EraseBool("BossOwnsStart");
            }
        }
        [MenuItem("Tools/Boss/Open Imported Skill Preview")]
        public static void Open()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(EditorScenePaths.Unified);
        }
        public static void OpenAndPlay(){Open();EditorApplication.isPlaying=true;}
        public static void Validate()
        {
            try
            {
                foreach(var path in new[]{EditorScenePaths.Unified,"Assets/Monsters/Boss/cyber-samurai/KatanaPreview/CyberSamurai_KatanaPreview.unity","Assets/Monsters/Boss/darklord-mech/SwordShieldPreview/Darklord_SwordShieldPreview.unity","Assets/AssetStore/Paladin_Anim_Set/ScenePreview/Paladin_Anim_Set.unity"})
                {
                    var scene=EditorSceneManager.OpenScene(path);
                    foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+path+" / "+t.name);
                    Debug.Log("BOSS_IMPORT_SCENE_PASS "+path);
                }
                EditorSceneManager.OpenScene(EditorScenePaths.Unified);
                SessionState.SetBool("BossImportTest",true);
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool("BossImportTest",false)||!EditorApplication.isPlaying)return;
            if(start==0)start=EditorApplication.timeSinceStartup;
            try
            {
                if(failure!=null)throw new Exception(failure);
                double age=EditorApplication.timeSinceStartup-start;
                var hub=UnityEngine.Object.FindFirstObjectByType<BossPreviewHub>();
                if(!hub){if(age>30)throw new Exception("Boss scene did not load");return;}
                if(hub.characters.Length!=2)throw new Exception("Expected two bosses");
                if(stage==0 && age>5){hub.SelectCharacter(0);Check(hub,0);stage=1;}
                if(stage==1 && age>9){Capture(hub.previewCamera,"darklord.png");hub.SelectCharacter(1);stage=2;}
                if(stage==2 && age>16){Check(hub,1);stage=3;}
                if(stage==3 && age>20)
                {
                    Capture(hub.previewCamera,"samurai.png");hub.SelectCharacter(0);Check(hub,0);
                    File.WriteAllText("Docs/Validation/BossImport/runtime.txt","PASS: four scenes load without missing scripts; two bosses activate, render and select an action; switching back succeeds. This is a smoke check, not verification of every animation or combat effect.\n");
                    Debug.Log("BOSS_IMPORT_RUNTIME_PASS");SessionState.SetBool("BossImportTest",false);SessionState.SetBool("BossImportExit",true);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){SessionState.SetBool("BossImportTest",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Check(BossPreviewHub hub,int i)
        {
            var c=hub.characters[i];if(!c.root.activeInHierarchy)throw new Exception("Inactive boss");
            if(c.root.GetComponentsInChildren<SkinnedMeshRenderer>().Length==0)throw new Exception("No visible mesh");
            if(c.swordShield){if(!c.swordShield.enabled||c.swordShield.clips.Length==0)throw new Exception("Missing sword clips");c.swordShield.Play(Math.Min(3,c.swordShield.clips.Length-1));Debug.Log("BOSS_SWORD_CLIPS "+c.swordShield.clips.Length);}
            if(c.katana){if(!c.katana.bootstrap.Ready||c.katana.states.Length==0)throw new Exception("Cloth or katana not ready");c.katana.PlayImmediate(Math.Min(3,c.katana.states.Length-1));Debug.Log("BOSS_KATANA_STATES "+c.katana.states.Length);}
        }
        static void Capture(Camera camera,string name)
        {
            Directory.CreateDirectory("Docs/Validation/BossImport");
            var rt=new RenderTexture(1200,900,24);var old=RenderTexture.active;var oldTarget=camera.targetTexture;
            var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,900),0,0);texture.Apply();File.WriteAllBytes("Docs/Validation/BossImport/"+name,texture.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
