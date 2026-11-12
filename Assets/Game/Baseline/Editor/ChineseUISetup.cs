using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
public static class ChineseUISetup
{
    public static void Build()
    {
        AssetDatabase.Refresh();
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Localization/LocalSimHei.ttf");
        if (!font) throw new System.InvalidOperationException("Chinese font missing");
        var tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Localization/ChineseSDF.asset");
        if (!tmp)
        {
            tmp = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            tmp.name = "ChineseSDF";
            AssetDatabase.CreateAsset(tmp, "Assets/Game/Localization/ChineseSDF.asset");
            AssetDatabase.AddObjectToAsset(tmp.material, tmp);
            foreach (var texture in tmp.atlasTextures) AssetDatabase.AddObjectToAsset(texture, tmp);
        }
        int count = 0;
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/AssetStore/desNetware/") && p.EndsWith(".prefab")).ToArray())
        {
            if (!File.ReadAllText(path).Contains("m_Text:") && !File.ReadAllText(path).Contains("m_text:")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try { count += Apply(root, font, tmp); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/AssetStore/desNetware/") && p.EndsWith(".unity")).ToArray())
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects()) count += Apply(root, font, tmp);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CHINESE_UI_FONT_COMPONENTS: " + count);
        BaselineBuild.Build();
    }
    static int Apply(GameObject root, Font font, TMP_FontAsset tmp)
    {
        int count = 0;
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            text.font = font;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(10, text.fontSize);
            text.resizeTextMaxSize = Mathf.Max(text.fontSize, 14);
            EditorUtility.SetDirty(text); count++;
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = tmp;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(10, text.fontSize);
            text.fontSizeMax = Mathf.Max(text.fontSize, 14);
            EditorUtility.SetDirty(text); count++;
        }
        return count;
    }
}
