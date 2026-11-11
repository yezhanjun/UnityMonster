using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MTPSKIT.Gameplay;

[InitializeOnLoad]
static class MeleeScratchSetup
{
    const string Root = "Assets/CharacterSelection/DemonHunterNewU6/CombatScratch/";
    static MeleeScratchSetup() { EditorApplication.update += WaitForEditor; }
    static void WaitForEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!AssetDatabase.LoadAssetAtPath<MeleeFeedbackProfile>("Assets/CharacterSelection/MeleeFeedback/LongBladeFeedback.asset")) return;
        EditorApplication.update -= WaitForEditor;
        Prepare();
    }

    [MenuItem("TPSBattle/近战/安装刀剑命中印花")]
    static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var profile = AssetDatabase.LoadAssetAtPath<MeleeFeedbackProfile>("Assets/CharacterSelection/MeleeFeedback/LongBladeFeedback.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/NeroScratch.mat");
        if (!profile || !material) return;
        if (profile.swordStamp == null || !profile.swordStamp.material)
        {
            profile.swordStamp = new CombatScratchConfig { enabled = true, material = material, stampKind = 0 };
            EditorUtility.SetDirty(profile);
        }

        var meshes = new HashSet<string>();
        foreach (string path in new[] {
            "Assets/desNetware/Multiplayer TPS KIT/Levels/Prefabs/Gameplay/Character.prefab",
            "Assets/Resources/RangeRobot.prefab",
            "Assets/Battlefield/Generated/BattlefieldPlayer.prefab",
            "Assets/Battlefield/Generated/BattlefieldMinion.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;
            foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.sharedMesh) meshes.Add(AssetDatabase.GetAssetPath(skin.sharedMesh));
        }
        foreach (string path in meshes)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer || importer.isReadable) continue;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }
}
