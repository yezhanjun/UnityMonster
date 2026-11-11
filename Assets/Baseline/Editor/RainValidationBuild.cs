#if UNITY_EDITOR_WIN
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace MTPSKIT.BuildTools
{
    public static class RainValidationBuild
    {
        public static void Build()
        {
            AssetDatabase.Refresh();
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Resources/RainActors"}))
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                var serialized=new SerializedObject(mat);var textures=serialized.FindProperty("m_SavedProperties.m_TexEnvs");Texture texture=null;
                for(int i=0;i<textures.arraySize;i++){var entry=textures.GetArrayElementAtIndex(i);if(entry.FindPropertyRelative("first").stringValue=="_MainTex")texture=entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;}
                mat.shader=shader;if(texture)mat.mainTexture=texture;EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            NewFrontEndSetup.Setup();
            BuildPlayer("Builds/RainValidation");
        }

        [MenuItem("TPS Battle/Build Rain Validation Player")]
        public static void BuildCurrentProject() => BuildPlayer("Builds/RainMonsterValidation");

        [MenuItem("TPS Battle/Build Rain Items Validation Player")]
        public static void BuildItemsCurrentProject() => BuildPlayer("Builds/RainItemsFormal", true);

        // Keep the previous pickup-callbacks Player intact while validating Collector changes.
        public static void BuildCollectorCurrentProject() => BuildPlayer("Builds/RainItemsCollector", true);

        static void BuildPlayer(string outputDirectory, bool cleanBuild = false)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Rain validation requires an enabled build scene");
            // lilToon's build scan restores the active scene; an unsaved batch scene has no path to restore.
            if (Application.isBatchMode && string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes[0]);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=scenes,
                locationPathName=outputDirectory+"/TPSShoot.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development | (cleanBuild ? BuildOptions.CleanBuildCache : BuildOptions.None)});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Rain validation build failed");
            System.IO.File.WriteAllText(outputDirectory+"/steam_appid.txt","4069010\n");
            Debug.Log("RAIN_VALIDATION_BUILD_SUCCEEDED");
        }
    }
}
#endif
