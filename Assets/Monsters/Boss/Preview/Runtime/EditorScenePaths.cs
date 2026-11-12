namespace BossPreview
{
    public static class EditorScenePaths
    {
        public const string Unified = "Assets/Monsters/Boss/BossSkillPreview.unity";
        public static bool IsPreview(string path) => path == Unified
            || path == "Assets/AssetStore/Gun and Sword Animation Pack/Scenes/Demo.unity"
            || path == "Assets/Monsters/Boss/cyber-samurai/KatanaPreview/CyberSamurai_KatanaPreview.unity"
            || path == "Assets/Monsters/Boss/darklord-mech/SwordShieldPreview/Darklord_SwordShieldPreview.unity"
            || path == "Assets/AssetStore/Paladin_Anim_Set/ScenePreview/Paladin_Anim_Set.unity";
    }
}
