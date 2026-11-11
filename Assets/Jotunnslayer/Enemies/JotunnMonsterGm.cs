using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // The in-match GM owns only the actors it starts through this adapter.
    [DisallowMultipleComponent]
    public sealed class JotunnMonsterGm : MonoBehaviour
    {
        readonly List<(string id, CharacterInstance actor)> spawned = new List<(string, CharacterInstance)>();
        bool fighting;

        static readonly (string id, Skeleton1HSpawn.Kind kind)[] Entries =
        {
            ("skeleton_1h_sur", Skeleton1HSpawn.Kind.Melee),
            ("skeleton_bow_VarRegular", Skeleton1HSpawn.Kind.Bow),
            ("spider_sur_VarRegular", Skeleton1HSpawn.Kind.Spider),
            ("spider_sur_VarInsane", Skeleton1HSpawn.Kind.SpiderInsane),
            ("elf_spider_02_sur", Skeleton1HSpawn.Kind.AlfheimDriderVariant),
            ("elf_spider_sur", Skeleton1HSpawn.Kind.AlfheimDrider),
            ("jotunn_stone_insane_sur", Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane),
            ("jotunn_stone_sur", Skeleton1HSpawn.Kind.AlfheimStoneJotunn),
            ("elf_ranged_sur", Skeleton1HSpawn.Kind.AlfheimElfRanged),
            ("elf_melee_insane_sur", Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane),
            ("elf_melee_sur", Skeleton1HSpawn.Kind.AlfheimElfMelee),
            ("sentinel_alf_sur", Skeleton1HSpawn.Kind.AlfheimSentinel),
            ("sentinel_veteran_alf_sur", Skeleton1HSpawn.Kind.AlfheimSentinelVeteran),
            ("spider_alf_sur", Skeleton1HSpawn.Kind.AlfheimSpider),
            ("spider_alf_veteran_sur", Skeleton1HSpawn.Kind.AlfheimSpiderVeteran),
            ("skeleton_guardian_sur", Skeleton1HSpawn.Kind.Guardian),
            ("skeleton_goat_sur_boss_VarEasy", Skeleton1HSpawn.Kind.NiflheimBossEasy),
            ("skeleton_goat_sur_boss_VarNormal", Skeleton1HSpawn.Kind.NiflheimBossNormal),
            ("skeleton_goat_endless", Skeleton1HSpawn.Kind.NiflheimBossEndless),
            ("skeleton_goat_hel_form_sur_boss", Skeleton1HSpawn.Kind.NiflheimBossHelForm),
            ("ghost_sur", Skeleton1HSpawn.Kind.Ghost),
            ("ghost_red_sur", Skeleton1HSpawn.Kind.GhostRed),
            ("ghost_red_sur_bf", Skeleton1HSpawn.Kind.GhostRedBattlefield),
            ("ghost_red_following_sur", Skeleton1HSpawn.Kind.GhostRedFollowing),
            ("ghost_svart_sur", Skeleton1HSpawn.Kind.GhostSvart),
            ("ghost_skull_sur", Skeleton1HSpawn.Kind.GhostSkull),
            ("jotunn_alfheim_boss_sur", Skeleton1HSpawn.Kind.AlfheimPhase1),
            ("jotunn_alfheim_boss_phase2_sur", Skeleton1HSpawn.Kind.AlfheimPhase2),
            ("hyb_sub_boss_sur_VarEasy", Skeleton1HSpawn.Kind.HybSubBossEasy),
            ("hyb_sub_boss_sur_VarNormal", Skeleton1HSpawn.Kind.HybSubBossNormal),
            ("hyb_boss_sur_VarEasy", Skeleton1HSpawn.Kind.HybBossEasy),
            ("hyb_boss_sur_VarNormal", Skeleton1HSpawn.Kind.HybBossNormal),
            ("hyb_boss_sur_VarHard", Skeleton1HSpawn.Kind.HybBossHard),
            ("ettin_boss", Skeleton1HSpawn.Kind.EttinBoss),
            ("ettin_endless", Skeleton1HSpawn.Kind.EttinEndless),
            ("ettin_hel_form_boss", Skeleton1HSpawn.Kind.EttinHelForm),
            ("jotunn_warrior_sur", Skeleton1HSpawn.Kind.JotunnWarrior),
            ("jotunn_warrior_v2_sur", Skeleton1HSpawn.Kind.JotunnWarriorV2),
            ("ghost_king_sur", Skeleton1HSpawn.Kind.GhostKing),
            ("elf_woman_sur", Skeleton1HSpawn.Kind.ElfWoman),
            ("elf_woman_02_sur", Skeleton1HSpawn.Kind.ElfWomanV2),
            ("ele_mb_melee_champion_sur", Skeleton1HSpawn.Kind.ElementalMeleeChampion),
            ("ele_mb_melee_sur", Skeleton1HSpawn.Kind.ElementalMelee),
            ("ele_mb_plague_sur", Skeleton1HSpawn.Kind.ElementalPlague),
            ("ele_mb_venom_sur", Skeleton1HSpawn.Kind.ElementalVenom),
            ("ele_mb_mage_sur", Skeleton1HSpawn.Kind.ElementalMage),
            ("ele_mb_mage_v2_sur", Skeleton1HSpawn.Kind.ElementalMageV2),
            ("bird_noobj_sur", Skeleton1HSpawn.Kind.BirdNoObjective),
            ("bird_sur", Skeleton1HSpawn.Kind.BirdObjective),
            ("jotunn_construct_sur", Skeleton1HSpawn.Kind.ConstructBoss),
            ("jotunn_muspel_sur", Skeleton1HSpawn.Kind.MuspelBoss),
            ("jotunn_muspel_endless", Skeleton1HSpawn.Kind.MuspelEndless),
            ("jotunn_muspel_hel_form_sur", Skeleton1HSpawn.Kind.MuspelHelForm),
            ("skeleton_1h_sphinx_sur", Skeleton1HSpawn.Kind.SphinxSkeleton),
            ("skeleton_bow_sphinx", Skeleton1HSpawn.Kind.SphinxBow),
            ("skeleton_guardian_sphinx_sur", Skeleton1HSpawn.Kind.SphinxGuardian),
            ("skeleton_mb_sphinx_sur", Skeleton1HSpawn.Kind.SphinxMiniBoss),
            ("milipede_sur", Skeleton1HSpawn.Kind.Milipede),
            ("milipede_insane_sur", Skeleton1HSpawn.Kind.MilipedeInsane),
            ("horror_expl_jotun_sur", Skeleton1HSpawn.Kind.HorrorJotun),
            ("horror_expl_alf_sur", Skeleton1HSpawn.Kind.HorrorAlf),
            ("bat_loot_sur", Skeleton1HSpawn.Kind.BatLoot),
        };

        internal static IEnumerable<(string id, Skeleton1HSpawn.Kind kind)> Catalog => Entries;

        internal static bool TryGetKind(string configurationId, out Skeleton1HSpawn.Kind kind)
        {
            foreach (var entry in Entries)
            {
                if (entry.id != configurationId) continue;
                kind = entry.kind;
                return true;
            }

            kind = default;
            return false;
        }

        internal static string GetConfigurationId(Skeleton1HSpawn.Kind kind)
        {
            foreach (var entry in Entries)
                if (entry.kind == kind) return entry.id;
            return null;
        }

        public static bool Supports(string configurationId) => TryGetKind(configurationId, out _);

        public static CharacterInstance TrySpawn(CharacterInstance owner, string configurationId)
        {
            if (!TryGetKind(configurationId, out var kind)) return null;

            var actor = Skeleton1HSpawn.TrySpawnInFrontOf(owner, kind);
            if (!actor)
            {
                Debug.LogWarning("JOTUNN_GM_SPAWN_FAILED " + configurationId + ": " + Skeleton1HSpawn.LastSpawnError);
                return null;
            }

            if (!owner.TryGetComponent<JotunnMonsterGm>(out var session))
                session = owner.gameObject.AddComponent<JotunnMonsterGm>();

            session.spawned.RemoveAll(existing => !existing.actor);
            session.spawned.Add((configurationId, actor));
            SetCombat(owner, false);
            Debug.Log("JOTUNN_GM_SPAWN_OK " + configurationId + " netId=" + actor.netId + " position=" + actor.transform.position);
            return actor;
        }

        public static bool IsFighting(CharacterInstance owner) =>
            owner && owner.TryGetComponent<JotunnMonsterGm>(out var session) && session.fighting;

        public static void SetCombat(CharacterInstance owner, bool active)
        {
            if (!owner || !owner.isServer || !owner.TryGetComponent<JotunnMonsterGm>(out var session)) return;
            session.fighting = active;
            foreach (var entry in session.spawned)
            {
                if (!entry.actor) continue;
                if (entry.actor.TryGetComponent<Skeleton1HCombat>(out var combat))
                    combat.SetGmCombat(active);
                else if (entry.actor.TryGetComponent<HorrorJotunCombat>(out var horror))
                    horror.SetGmCombat(active);
                else if ((entry.id is "ghost_sur" or "ghost_red_sur" or "ghost_red_sur_bf" or "ghost_red_following_sur" or
                    "ghost_svart_sur" or "ghost_skull_sur") &&
                    entry.actor.TryGetComponent<MonsterPassThrough>(out var ghost))
                    ghost.SetGmCombat(active);
            }
        }

        public static bool Remove(CharacterInstance owner, string configurationId)
        {
            if (!NetworkServer.active || !owner || !owner.isServer ||
                !owner.TryGetComponent<JotunnMonsterGm>(out var session)) return false;

            bool removed = false;
            for (int i = session.spawned.Count - 1; i >= 0; i--)
            {
                var entry = session.spawned[i];
                if (entry.id != configurationId) continue;
                session.spawned.RemoveAt(i);
                if (!entry.actor) continue;
                NetworkServer.Destroy(entry.actor.gameObject);
                removed = true;
            }
            return removed;
        }

        public static void Clear(CharacterInstance owner)
        {
            if (owner && owner.isServer && owner.TryGetComponent<JotunnMonsterGm>(out var session))
                session.ClearOwnedActors();
        }

        void ClearOwnedActors()
        {
            if (NetworkServer.active)
                foreach (var entry in spawned)
                    if (entry.actor) NetworkServer.Destroy(entry.actor.gameObject);
            spawned.Clear();
        }

        void OnDestroy() => ClearOwnedActors();
    }
}
