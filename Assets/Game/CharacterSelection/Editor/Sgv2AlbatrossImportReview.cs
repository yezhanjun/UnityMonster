using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class Sgv2AlbatrossImportReview
{
    static Sgv2AlbatrossImportReview() { EditorApplication.update += Poll; }
    static void Poll() { const string request="Library/Sgv2Albatross.request"; if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return; File.Delete(request); Run(); }
    [Serializable] public class Item { public string original, output, rootPosition, rootRotation, rootScale; public int renderers, removedAuthorComponents, missingScripts; public string[] controllers; }
    [Serializable] public class Report { public bool success; public string unity, error; public List<Item> prefabs = new List<Item>(); }
    public static void Run()
    {
        bool game = Directory.Exists("Assets/Game");
        string root = game ? "Assets/VRC/ImportedEquipment/SGV2Albatross_20261119" : "Assets/ImportedEquipment/SGV2Albatross_20261119";
        string evidence = game ? "Docs/Validation/SGV2Albatross-20261119" : "Doc/04-验证与证据/SGV2Albatross-20261119";
        string output = "Assets/VRC/Equipment/SGV2Albatross_20261119";
        var report = new Report { unity = Application.unityVersion };
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (game) { Directory.CreateDirectory(output); AssetDatabase.Refresh(); }
            foreach (var path in Directory.GetFiles(root, "*.prefab", SearchOption.AllDirectories))
            {
                string p = path.Replace('\\', '/');
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!source) throw new Exception("Cannot load " + p);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    var item = new Item { original = p, rootPosition = go.transform.localPosition.ToString("F6"), rootRotation = go.transform.localEulerAngles.ToString("F6"), rootScale = go.transform.localScale.ToString("F6") };
                    foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (game)
                        {
                            item.removedAuthorComponents += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
                            foreach (var c in tr.GetComponents<MonoBehaviour>())
                                if (c && (c.GetType().FullName.StartsWith("nadena.") || c.GetType().FullName.StartsWith("VRC."))) { UnityEngine.Object.DestroyImmediate(c); item.removedAuthorComponents++; }
                        }
                        item.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
                    }
                    if (item.missingScripts != 0) throw new Exception("Missing scripts: " + p);
                    var controllers = new List<string>();
                    foreach (var a in go.GetComponentsInChildren<Animator>(true)) if (a.runtimeAnimatorController) controllers.Add(AssetDatabase.GetAssetPath(a.runtimeAnimatorController));
                    item.controllers = controllers.ToArray();
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    item.renderers = renderers.Length;
                    if (renderers.Length == 0) throw new Exception("No renderers " + p);
                    foreach (var r in renderers)
                    {
                        if (r is SkinnedMeshRenderer sk && !sk.sharedMesh) throw new Exception("Missing mesh " + r.name);
                        var mf = r.GetComponent<MeshFilter>(); if (mf && !mf.sharedMesh) throw new Exception("Missing mesh " + r.name);
                        foreach (var m in r.sharedMaterials) if (!m || !m.shader || m.shader.name == "Hidden/InternalErrorShader") throw new Exception("Missing material/shader " + r.name);
                    }
                    if (game)
                    {
                        item.output = output + "/" + Path.GetFileName(p);
                        PrefabUtility.SaveAsPrefabAsset(go, item.output);
                    }
                    report.prefabs.Add(item);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            if (report.prefabs.Count != 5) throw new Exception("Expected five prefabs");
            AssetDatabase.SaveAssets();
            report.success = true;
        }
        catch (Exception e) { report.error = e.ToString(); Debug.LogException(e); }
        Directory.CreateDirectory(evidence);
        File.WriteAllText(evidence + "/unity-import-review.json", JsonUtility.ToJson(report, true));
        if (Application.isBatchMode) EditorApplication.Exit(report.success ? 0 : 1);
    }
}
