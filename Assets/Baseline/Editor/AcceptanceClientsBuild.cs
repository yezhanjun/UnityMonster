#if UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class AcceptanceClientsBuild
    {
        [MenuItem("Tools/TPS/Build Independent Acceptance Clients")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building");
            Jotunnslayer.GmGodTabsValidation.RepairSkeletonAvatar();
            const string root = "Builds/AcceptanceClients";
            Directory.CreateDirectory(root);
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (Application.isBatchMode) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes[0]);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = root + "/TPSShoot.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.CleanBuildCache });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Acceptance build failed: " + report.summary.result);
            foreach (string lane in new[] { "jotunn", "rain" })
            {
                string title = lane == "jotunn" ? "Jotunn-Acceptance" : "Rain-Acceptance";
                File.WriteAllText(root + "/Start-" + title + ".cmd", "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0TPSShoot.exe\" --acceptance-lane " + lane +
                    " -screen-fullscreen 0 -screen-width 960 -screen-height 640 -logFile \"%~dp0" + title + ".log\"\r\n");
            }
            File.WriteAllText(root + "/Start-Both.cmd", "@echo off\r\ncall \"%~dp0Start-Jotunn-Acceptance.cmd\"\r\ncall \"%~dp0Start-Rain-Acceptance.cmd\"\r\n");
            if (File.Exists("acceptance-source.txt")) File.Copy("acceptance-source.txt", root + "/acceptance-source.txt", true);
            Debug.Log("ACCEPTANCE_CLIENTS_BUILD_SUCCEEDED " + Path.GetFullPath(root));
        }
    }
}
#endif
