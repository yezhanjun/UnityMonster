using MTPSKIT.UI.DamageNumbers;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class DamageNumbersSetup
{
    public const string SettingsPath = "Assets/AssetStore/DamageNumbers/Resources/DamageNumbersSettings.asset";

    [MenuItem("TPSBattle/UI/Select Damage Numbers Settings")]
    public static void SelectSettings()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<DamageNumbersSettings>(SettingsPath);
    }

    public static void SetupAndBuild()
    {
        AssetDatabase.Refresh();
        if (!AssetDatabase.LoadAssetAtPath<DamageNumbersSettings>(SettingsPath))
        {
            var settings = ScriptableObject.CreateInstance<DamageNumbersSettings>();
            settings.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/AssetStore/DamageNumbers/Resources/Fonts/Alibaba-PuHuiTi-H SDF.asset");
            if (!settings.Font) throw new System.InvalidOperationException("Source damage font missing.");
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/AssetStore/DamageNumbers/Resources/Fonts/Alibaba-PuHuiTi-H SDF.asset");
        // The template's old TMP shader reads the pre-Unity-6 UV layout.
        font.material.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AssetStore/DamageNumbers/Shaders/TMP_SDF.shader");
        font.isMultiAtlasTexturesEnabled = true;
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Localization/ChineseSDF.asset");
        if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(font);
        EditorUtility.SetDirty(font.material);
        AssetDatabase.SaveAssets();
        DesertBattlefieldSetup.BuildExisting();
    }
}
