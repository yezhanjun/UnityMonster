using UnityEditor;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class SkillTreePreview
    {
        [MenuItem("TPS Battle/Jotunnslayer/Open Skill Tree Preview")]
        private static void Open()
        {
            var panel = Object.FindFirstObjectByType<SkillTreePanel>();
            if (!panel)
            {
                var root = new GameObject("Jotunnslayer Skill Tree Preview");
                panel = root.AddComponent<SkillTreePanel>();
            }

            var catalog = SkillCatalog.Load();
            Font font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18);
            var progress = new SkillProgress(catalog, "jotunnslayer.preview.v1", 10, 15);
            panel.Open(catalog, progress, font);
            var local = NetworkClient.localPlayer;
            var player = local ? local.GetComponent<PlayerInstance>() : null;
            if (!player || !player.MyCharacter || !player.MyCharacter.isServer)
                return;

            var preview = panel.GetComponent<SkillBattlePreview>();
            if (!preview)
                preview = panel.gameObject.AddComponent<SkillBattlePreview>();

            preview.Bind(player.MyCharacter, progress, panel);
        }

        [MenuItem("TPS Battle/Jotunnslayer/Open Skill Tree Preview", true)]
        private static bool CanOpen() => EditorApplication.isPlaying;
    }
}


