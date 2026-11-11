#if UNITY_EDITOR_WIN
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MTPSKIT.BuildTools
{
    [InitializeOnLoad]
    static class TrainingWhiteboxPlayMode
    {
        const string OwnsStartSceneKey = "TPSShoot.TrainingOwnsPlayModeStartScene";

        static TrainingWhiteboxPlayMode()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("TPSShoot/Play Modular Training Range")]
        static void PlayModularTrainingRange()
        {
            if (EditorApplication.isPlaying)
            {
                var frontend = UnityEngine.Object.FindFirstObjectByType<NewFrontEnd>();
                if (frontend) frontend.LaunchModularTrainingFromEditor();
                return;
            }
            var startup = AssetDatabase.LoadAssetAtPath<SceneAsset>(DNNetworkManager.MainMenuScene);
            if (!startup || EditorSceneManager.playModeStartScene) return;

            SessionState.SetString(NewFrontEnd.EditorTrainingLaunchKey, RoomCreator.ModularTrainingScenePath);
            SessionState.SetBool(OwnsStartSceneKey, true);
            EditorSceneManager.playModeStartScene = startup;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("TPSShoot/Play Modular Training Range", true)]
        static bool CanPlayModularTrainingRange()
        {
            if (EditorApplication.isPlaying)
                return !EditorApplication.isPaused && UnityEngine.Object.FindFirstObjectByType<NewFrontEnd>() != null;
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorSceneManager.playModeStartScene;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            string activeScene = SceneManager.GetActiveScene().path;
            if (state == PlayModeStateChange.ExitingEditMode &&
                (activeScene == RoomCreator.TrainingScenePath || activeScene == RoomCreator.ModularTrainingScenePath) &&
                !EditorSceneManager.playModeStartScene)
            {
                var startup = AssetDatabase.LoadAssetAtPath<SceneAsset>(DNNetworkManager.MainMenuScene);
                if (!startup) return;

                SessionState.SetString(NewFrontEnd.EditorTrainingLaunchKey, activeScene);
                SessionState.SetBool(OwnsStartSceneKey, true);
                EditorSceneManager.playModeStartScene = startup;
            }
            else if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (SessionState.GetBool(OwnsStartSceneKey, false))
                {
                    if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == DNNetworkManager.MainMenuScene)
                        EditorSceneManager.playModeStartScene = null;
                    SessionState.EraseBool(OwnsStartSceneKey);
                }

                if (state == PlayModeStateChange.EnteredEditMode)
                    SessionState.EraseString(NewFrontEnd.EditorTrainingLaunchKey);
            }
        }
    }
}
#endif
