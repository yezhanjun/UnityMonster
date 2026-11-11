#if UNITY_EDITOR_WIN
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Jotunnslayer
{
    public static class SkillValidationBuild
    {
        public static void Build() => BuildPlayer(false);

        // Same runtime and resources; only omit unused maps during arena fixture iterations.
        public static void BuildArena() => BuildPlayer(true);

        static void BuildPlayer(bool arenaOnly)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (arenaOnly)
            {
                scenes = scenes.Where(path => path == "Assets/Baseline/Scenes/Startup.unity" ||
                    System.IO.Path.GetFileNameWithoutExtension(path) == "map_arena").ToArray();
                if (scenes.Length != 2) throw new InvalidOperationException("Arena validation requires startup and arena scenes");
            }
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled validation scenes");
            if (string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes[0]);
            const string output = "Builds/JotunnSkillValidation";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = output + "/TPSShoot.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Jotunn skill validation build failed");
            System.IO.File.WriteAllText(output + "/steam_appid.txt", "4069010\n");
            Debug.Log("JOTUNN_SKILL_VALIDATION_BUILD_OK arenaOnly=" + arenaOnly);
        }
    }
}
#endif
