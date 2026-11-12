using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MTPSKIT.BuildTools
{
    public static class SteamTestBuild
    {
        [MenuItem("TPSShoot/Build Windows Steam Test")]
        public static void Build()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/SteamTest/TPSShoot.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Steam test build failed: " + report.summary.result);
            string config = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt");
            // Local test configuration only; exclude this file from Steam depot uploads.
            File.WriteAllText(config, SteamRoomSession.DevelopmentAppId + "\n");
            UnityEngine.Debug.Log("STEAM_TEST_BUILD_SUCCEEDED: " + report.summary.outputPath);
        }
    }
}
