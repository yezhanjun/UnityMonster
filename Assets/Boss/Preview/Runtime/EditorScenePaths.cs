namespace BossPreview
{
    public static class EditorScenePaths
    {
        public const string Unified = "Assets/Boss/BossSkillPreview.unity";
        public static bool IsPreview(string path) => path == Unified
            || path == "Assets/Boss/cyber-samurai/KatanaPreview/CyberSamurai_KatanaPreview.unity"
            || path == "Assets/Boss/darklord-mech/SwordShieldPreview/Darklord_SwordShieldPreview.unity"
            || path == "Assets/Paladin_Anim_Set/ScenePreview/Paladin_Anim_Set.unity";
    }
}
