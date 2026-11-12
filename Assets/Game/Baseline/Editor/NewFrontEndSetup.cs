#if UNITY_EDITOR_WIN
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class NewFrontEndSetup
    {
        public const string Startup = DNNetworkManager.MainMenuScene;
        const string LegacyHub = "Assets/AssetStore/desNetware/Multiplayer TPS KIT/Levels/hub.unity";
        const string LegacyServerList = "Assets/AssetStore/desNetware/Multiplayer TPS KIT/ServerListImplementation/hub_serverList.unity";
        [MenuItem("TPSShoot/Setup New Frontend")]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before setting up the frontend.");
            AssetDatabase.Refresh();
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/FrontEndReference"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                if(path.Contains("Panel")||path.Contains("Button")||path.Contains("Frame")||path.Contains("Outline"))
                    importer.spriteBorder=new Vector4(8,8,8,8);
                importer.SaveAndReimport();
            }
            if(!AssetDatabase.IsValidFolder("Assets/Game/Baseline/Scenes"))AssetDatabase.CreateFolder("Assets/Game/Baseline","Scenes");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())throw new OperationCanceledException("Scene setup cancelled; no build was started.");
            var training=TrainingRangeSetup.Prepare();
            var modularTraining=AssetDatabase.LoadAssetAtPath<MapRepresenter>("Assets/Resources/ModularTrainingMap.asset");
            var rain=TrainingRangeSetup.PrepareRain(training);
            var scene=EditorSceneManager.OpenScene(System.IO.File.Exists(Startup) ? Startup : LegacyHub);
            var creator=UnityEngine.Object.FindFirstObjectByType<RoomCreator>();
            if(!creator)throw new InvalidOperationException("Main menu RoomCreator is missing.");
            if(creator.GetComponent<Canvas>())
            {
                var services=new GameObject("Main Menu Services");
                var configuration=services.AddComponent<RoomCreator>();
                configuration.Maps=creator.Maps;
                configuration.TimeOptionsInMinutes=creator.TimeOptionsInMinutes;
                configuration.PlayerNumberOptions=creator.PlayerNumberOptions;
                services.AddComponent<UIUsernameSet>();
                UnityEngine.Object.DestroyImmediate(creator.gameObject);
                creator=configuration;
            }
            if(modularTraining && !creator.Maps.Contains(modularTraining))creator.Maps=creator.Maps.Concat(new[]{modularTraining}).ToArray();
            if(modularTraining && !EditorBuildSettings.scenes.Any(s=>s.path==RoomCreator.ModularTrainingScenePath))
                EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(RoomCreator.ModularTrainingScenePath,true)}).ToArray();
            if(!creator.Maps.Contains(training))creator.Maps=creator.Maps.Concat(new[]{training}).ToArray();
            if(!creator.Maps.Contains(rain))creator.Maps=creator.Maps.Concat(new[]{rain}).ToArray();
            var frontend=UnityEngine.Object.FindFirstObjectByType<NewFrontEnd>();
            if(!frontend)frontend=new GameObject("New Frontend").AddComponent<NewFrontEnd>();
            frontend.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Localization/ChineseSDF.asset");
            frontend.BuildEditorPreview();
            EditorSceneManager.SaveScene(scene,Startup);
            var old=EditorBuildSettings.scenes.Where(s=>s.path!=Startup&&s.path!=LegacyHub&&s.path!=LegacyServerList).ToList();
            old.Insert(0,new EditorBuildSettingsScene(Startup,true));
            EditorBuildSettings.scenes=old.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("NEW_FRONTEND_SETUP_SUCCEEDED: " + Startup);
        }
        [MenuItem("TPSShoot/Build New Frontend Test")]
        public static void Build()
        {
            Setup();
            if(!System.IO.File.Exists(Startup))throw new InvalidOperationException("Startup scene was not created.");
            SteamTestBuild.Build();
        }
    }
}
#endif
