using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MonsterProjectExport
{
    public static void Collect()
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);
        string[] code = { ".cs", ".asmdef", ".asmref", ".dll", ".rsp", ".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc", ".compute", ".uxml", ".uss" };
        foreach (var p in AssetDatabase.GetAllAssetPaths())
        {
            if (!p.StartsWith("Assets/") || Directory.Exists(p)) continue;
            if (code.Contains(Path.GetExtension(p).ToLowerInvariant()) || p.Contains("/Resources/") ||
                p.StartsWith("Assets/Jotunnslayer/Enemies/") ||
                p == "Assets/Baseline/Scenes/Startup.unity" ||
                p == "Assets/Maps/TrainingWhitebox/TrainingWhitebox.unity" ||
                p == "Assets/Baseline/Scenes/map_training.unity" ||
                p.LastIndexOf('/') == 6) roots.Add(p);
        }
        foreach (var f in Directory.GetFiles("ProjectSettings"))
        foreach (Match m in Regex.Matches(File.ReadAllText(f), @"guid: ([a-f0-9]{32})"))
        {
            var p = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
            if (p.StartsWith("Assets/")) roots.Add(p);
        }
        var selected = AssetDatabase.GetDependencies(roots.ToArray(), true)
            .Where(p => p.StartsWith("Assets/") && File.Exists(p)).OrderBy(p => p).ToArray();
        File.WriteAllLines("monster-export-files.txt", selected);
        Debug.Log("MONSTER_EXPORT_OK files=" + selected.Length + " bytes=" + selected.Sum(p => new FileInfo(p).Length));
        EditorApplication.Exit(0);
    }
}
