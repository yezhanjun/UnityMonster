using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BaselineBuild
{
    public static void Build()
    {
                AssetDatabase.Refresh();
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Game/Baseline/BaselineURP.asset");
        if (!pipeline)
        {
            var renderer = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/Game/Baseline/BaselineRenderer.asset");
            pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, "Assets/Game/Baseline/BaselineURP.asset");
        }
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i); QualitySettings.renderPipeline = pipeline; }
        PlayerSettings.runInBackground = true;
        MTPSKIT.LayerSetupEditor.SetupLayers();
        AssetDatabase.SaveAssets();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        EditorSceneManager.OpenScene(scenes[0]);
        var report = BuildPipeline.BuildPlayer(scenes, "Builds/Windows/TPSShoot.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
        Debug.Log("BASELINE_BUILD: " + report.summary.result);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}



