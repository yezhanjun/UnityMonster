#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    // Explicit repair of stale UsePass shader dependencies. Never runs on ordinary startup.
    [InitializeOnLoad]
    public static class LilToonKeywordRepair
    {
        static readonly string Request = Path.Combine(Path.GetTempPath(), "TPS-LilToonKeywordRepair.request");
        static readonly string Report = Path.Combine(Path.GetTempPath(), "TPS-LilToonKeywordRepair.result.txt");
        static LilToonKeywordRepair() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (Application.isBatchMode || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Repair();
        }
        [MenuItem("Tools/TPS/Repair lilToon Keyword Cache")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { File.WriteAllText(Request, "deferred until edit mode"); Debug.Log("LIL_KEYWORD_REPAIR_DEFERRED until Edit mode"); return; }
            const string folder = "Packages/jp.lilxyzw.liltoon-2.3.2/Shader";
            try
            {
                var paths = Directory.GetFiles(folder, "*.shader");
                Array.Sort(paths, StringComparer.Ordinal);
                int count = 0;
                // Import internal pass providers first, then wrappers referencing them via UsePass.
                for (int pass = 0; pass < 2; pass++)
                    foreach (var raw in paths)
                    {
                        bool provider = Path.GetFileName(raw).StartsWith("ltspass_", StringComparison.Ordinal);
                        if (provider != (pass == 0)) continue;
                        AssetDatabase.ImportAsset(raw.Replace('\\', '/'), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        count++;
                    }
                SceneView.RepaintAll();
                File.WriteAllText(Report, "IMPORT_COMPLETE shaders=" + count + " utc=" + DateTime.UtcNow.ToString("O") + "\nRendering recheck still required.");
                Debug.Log("LIL_KEYWORD_REPAIR_IMPORTED shaders=" + count);
            }
            catch (Exception ex)
            { File.WriteAllText(Report, "FAILED " + ex); Debug.LogException(ex); }
        }
    }
}
#endif
