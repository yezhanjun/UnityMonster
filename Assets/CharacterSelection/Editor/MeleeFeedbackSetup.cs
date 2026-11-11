using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using MTPSKIT.Gameplay;
using SCPE;

[InitializeOnLoad]
static class MeleeFeedbackSetup
{
    const string ProfilePath = "Assets/CharacterSelection/MeleeFeedback/LongBladeFeedback.asset";
    static MeleeFeedbackSetup() { EditorApplication.update += WaitForEditor; }
    static void WaitForEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= WaitForEditor;
        Prepare();
    }

    [MenuItem("TPSBattle/近战/选中命中反馈参数")]
    static void SelectProfile()
    {
        Prepare();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<MeleeFeedbackProfile>(ProfilePath);
    }

    [MenuItem("TPSBattle/近战/安装命中反馈")]
    static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var profile = AssetDatabase.LoadAssetAtPath<MeleeFeedbackProfile>(ProfilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<MeleeFeedbackProfile>();
            profile.radialShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ThirdParty/SC Post Effects/Runtime/RadialBlur/RadialBlurShader.shader");
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Baseline/BaselineRenderer.asset");
        if (renderer && !renderer.rendererFeatures.OfType<RadialBlurRenderer>().Any())
        {
            var feature = ScriptableObject.CreateInstance<RadialBlurRenderer>();
            feature.name = "SCPE Melee Radial Blur";
            feature.settings.alwaysEnable = true;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            feature.Create();
            var serialized = new SerializedObject(renderer);
            serialized.Update();
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
        const string weaponPath = "Assets/CharacterSelection/SwordAnimationPack/item_longBlade.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
        if (asset && new SerializedObject(asset.GetComponent<MeleeWeapon>()).FindProperty("hitFeedback").objectReferenceValue != profile)
        {
            var weapon = PrefabUtility.LoadPrefabContents(weaponPath);
            try
            {
                var serialized = new SerializedObject(weapon.GetComponent<MeleeWeapon>());
                serialized.FindProperty("hitFeedback").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(weapon, weaponPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(weapon); }
        }
        AssetDatabase.SaveAssets();
    }
}
