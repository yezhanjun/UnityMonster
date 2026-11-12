using System;
using System.Collections.Generic;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Jotunnslayer
{
    // A single, replaceable test effect. No PlayerPrefs or permanent skill-tree changes.
    [DisallowMultipleComponent]
    public sealed class GodSkillGm : MonoBehaviour
    {
        internal sealed class Entry
        {
            internal string id, name, description, stats;
            internal Texture2D icon;
        }

        SkillBattlePreview runtime;
        CharacterInstance owner;
        Canvas skillHud;
        internal string ActiveSkillId { get; private set; }
        internal int ActiveSkillLevel { get; private set; }
        internal Texture2D ActiveSkillIcon { get; private set; }
        internal bool HasSkillHud => skillHud && skillHud.gameObject.activeInHierarchy;

        internal static IEnumerable<Entry> Catalog
        {
            get
            {
                var catalog = SkillCatalog.Load();
                foreach (string id in SkillBattlePreview.GmGodSkillIds)
                {
                    var node = Find(catalog, id);
                    if (node == null) continue;
                    string icon = node.variants != null && node.variants.Length > 0 ? node.variants[0].icon : node.icon;
                    yield return new Entry
                    {
                        id = id,
                        name = GodName(node.group) + " · " + node.title,
                        description = "选择基础或进化效果，关闭 GM 后自动施放；附近需要有敌方目标。每次只测试一个技能，切换会清理上一个。\n\n进化说明：" + node.desc,
                        stats = "基础：1 阶 · 进化：4 阶\n当前技能表现与实伤测试\n" + GodName(node.group),
                        icon = Resources.Load<Texture2D>("Jotunnslayer/Icons/" + icon)
                            ?? Resources.Load<Texture2D>("Jotunnslayer/Icons/" + node.icon)
                    };
                }
            }
        }

        internal static bool CanTest(CharacterInstance character) =>
            NetworkServer.activeHost && character && character.isServer && character.CurrentHealth > 0 &&
            GameManager.Gamemode is MTPSKIT.Gameplay.Gamemodes.RangeGamemode;

        internal static string Activate(CharacterInstance character, string id, bool evolved, bool showHud = true)
        {
            if (!CanTest(character)) return "请在训练场或雨中冒险的房主角色上测试。";
            var catalog = SkillCatalog.Load();
            var node = Find(catalog, id);
            if (node == null || Array.IndexOf(SkillBattlePreview.GmGodSkillIds, id) < 0)
                return "该神祇技能尚未接入测试。";

            // An empty save key makes this loadout entirely temporary.
            var profile = new SkillProgress(catalog, null, 10000);
            if (!Learn(catalog, profile, node, 0)) return "技能测试配置缺少前置条件。";
            var evolution = Find(catalog, id.EndsWith("_evolve", StringComparison.Ordinal) ? id : id + "_evolve");
            if (evolved && evolution != null && !Learn(catalog, profile, evolution, 0))
                return "无法启用进化测试配置。";

            if (!character.TryGetComponent<GodSkillGm>(out var session))
                session = character.gameObject.AddComponent<GodSkillGm>();
            session.Clear();
            session.owner = character;
            var host = new GameObject("GM God Skill - " + node.title);
            host.transform.SetParent(character.transform, false);
            session.runtime = host.AddComponent<SkillBattlePreview>();
            if (!session.runtime.StartGmGodSkill(character, profile, id, evolved))
            {
                session.Clear();
                return "技能启用失败，未保留测试效果。";
            }
            if (showHud) session.ShowSkillHud(node, id, evolved);
            return "已启用" + GodName(node.group) + " · " + node.title + (evolved ? "（进化）" : "（基础）") + "。关闭 GM 后观察效果。";
        }

        internal static string Stop(CharacterInstance character)
        {
            if (!character || !character.isServer) return "需要房主角色。";
            if (!character.TryGetComponent<GodSkillGm>(out var session) || !session.runtime)
                return "当前没有神祇测试技能。";
            session.Clear();
            return "已停用当前神祇测试技能。";
        }

        static SkillCatalog.Node Find(SkillCatalog catalog, string id)
        {
            foreach (var node in catalog.skills)
                if (node.id == id) return node;
            return null;
        }

        static bool Learn(SkillCatalog catalog, SkillProgress profile, SkillCatalog.Node node, int depth)
        {
            if (profile.IsLearned(node.id)) return true;
            if (depth > catalog.skills.Length) return false;
            if (node.conditions != null)
                foreach (string condition in node.conditions)
                {
                    var parent = Find(catalog, condition);
                    if (parent == null || !Learn(catalog, profile, parent, depth + 1)) return false;
                }
            return profile.TryLearn(node.id);
        }

        static string GodName(string group) => group switch
        {
            "Thor [God]" => "索尔",
            "Freya [God]" => "芙蕾雅",
            "Loki [God]" => "洛基",
            "Skadi [God]" => "斯卡蒂",
            "Odin [God]" => "奥丁",
            "Njord [God]" => "尼奥尔德",
            "Nidhogg [God]" => "尼德霍格",
            _ => group
        };

        void Clear()
        {
            if (skillHud) { skillHud.gameObject.SetActive(false); Destroy(skillHud.gameObject); skillHud = null; }
            ActiveSkillId = null;
            ActiveSkillLevel = 0;
            ActiveSkillIcon = null;
            if (!runtime) return;
            runtime.StopGmGodSkill();
            Destroy(runtime.gameObject);
            runtime = null;
        }

        void ShowSkillHud(SkillCatalog.Node node, string id, bool evolved)
        {
            ActiveSkillId = id;
            ActiveSkillLevel = evolved ? 4 : 1;
            string key = node.variants != null && node.variants.Length > 0 ? node.variants[0].icon : node.icon;
            ActiveSkillIcon = Resources.Load<Texture2D>("Jotunnslayer/Icons/" + key)
                ?? Resources.Load<Texture2D>("Jotunnslayer/Icons/" + node.icon);
            skillHud = FrontEndUI.Canvas(null, 21);
            skillHud.name = "GM acquired god skill HUD";
            // Keep currency and the allied drone list at x=90 clear.
            var panel = FrontEndUI.Rect(skillHud.transform, "God skill", 380, 185, 320, 100);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.04f, .055f, .08f, .85f);
            background.raycastTarget = false;
            FrontEndUI.Text(panel, "神祇技能 · 临时测试", 12, 3, 296, 25, 18);
            var icon = FrontEndUI.Rect(panel, "Skill icon", 12, 34, 56, 56).gameObject.AddComponent<RawImage>();
            icon.texture = ActiveSkillIcon;
            icon.raycastTarget = false;
            var name = FrontEndUI.Text(panel, GodName(node.group) + " · " + node.title, 80, 32, 228, 30, 20);
            name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 20;
            FrontEndUI.Text(panel, "Lv." + ActiveSkillLevel + (evolved ? "  进化" : "  基础"), 80, 65, 228, 26, 20);
        }

        void Update()
        {
            if (runtime && (!owner || owner.CurrentHealth <= 0 || !owner.isServer)) Clear();
        }

        void OnDisable() => Clear();
        void OnDestroy() => Clear();
    }
}
