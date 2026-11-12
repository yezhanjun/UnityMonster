using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnityMonsterProjectValidation
{
    [Serializable] private sealed class Result
    {
        public string unityVersion;
        public string[] scenes;
        public int missingScripts;
        public string[] missingDependencies;
        public bool passed;
    }

    public static void Run()
    {
        var scenes = new[] { "Assets/Game/Baseline/Scenes/Startup.unity", "Assets/Game/Maps/TrainingWhitebox/TrainingWhitebox.unity" };
        var missing = new List<string>();
        int missingScripts = 0;
        foreach (string path in scenes)
        {
            if (!File.Exists(path)) { missing.Add(path); continue; }
            foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                if (dependency.StartsWith("Assets/") && !File.Exists(dependency)) missing.Add(dependency);
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        }
        var result = new Result { unityVersion = Application.unityVersion, scenes = scenes,
            missingScripts = missingScripts, missingDependencies = missing.ToArray(),
            passed = missing.Count == 0 && missingScripts == 0 };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/project-validation.json", JsonUtility.ToJson(result, true));
        Debug.Log("UNITYMONSTER_PROJECT_VALIDATION " + JsonUtility.ToJson(result));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
}
