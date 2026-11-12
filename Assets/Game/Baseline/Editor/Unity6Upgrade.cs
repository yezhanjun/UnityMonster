using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Unity6Upgrade
{
    static void ConfigureCameras(GameObject root)
    {
        foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            if (!camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>())
                camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
    }
    public static void ValidateAndBuild()
    {
        if (Application.unityVersion != "6000.2.10f1") throw new InvalidOperationException("Use Unity 6000.2.10f1");
        int missing = 0, materials = 0;
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/AssetStore/desNetware/") && p.EndsWith(".prefab")).ToArray())
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!root) continue;
            if (root.GetComponentsInChildren<Camera>(true).Any(c => !c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()))
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try { ConfigureCameras(contents); PrefabUtility.SaveAsPrefabAsset(contents, path); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        }
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path);
            foreach (var sceneRoot in scene.GetRootGameObjects()) ConfigureCameras(sceneRoot);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            EditorSceneManager.SaveScene(scene);
        }
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/AssetStore/desNetware/") && p.EndsWith(".mat")))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat || !mat.shader || mat.shader.name == "Hidden/InternalErrorShader") throw new InvalidOperationException("Invalid material shader: " + path);
            materials++;
        }
        Debug.Log($"UNITY6_AUDIT version={Application.unityVersion} missingScripts={missing} validMaterials={materials}");
        if (missing != 0) throw new InvalidOperationException("Missing scripts found");
        AssetDatabase.SaveAssets();
        BaselineBuild.Build();
    }
}


