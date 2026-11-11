using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public static class Skeleton1HSpawn
    {
        const string ResourcePath = "Jotunnslayer/Skeleton1H/";

        public enum Kind { Melee, Bow, Spider, Guardian, NiflheimBossEasy, Ghost, AlfheimPhase1, AlfheimPhase2, HybSubBossEasy, SphinxSkeleton, AlfheimSpider, AlfheimSpiderVeteran, AlfheimSentinel, AlfheimSentinelVeteran, AlfheimElfMelee, AlfheimElfRanged, AlfheimStoneJotunn, AlfheimDrider, AlfheimDriderVariant, AlfheimElfMeleeInsane, AlfheimStoneJotunnInsane, SphinxBow, SphinxGuardian, HybBossEasy, SphinxMiniBoss, Milipede, HorrorJotun, BatLoot, MilipedeInsane, SpiderInsane, GhostRed, GhostRedBattlefield, GhostRedFollowing, GhostSvart, GhostSkull, HorrorAlf, HybSubBossNormal, HybBossNormal, HybBossHard, NiflheimBossNormal, NiflheimBossEndless, NiflheimBossHelForm, EttinBoss, EttinEndless, EttinHelForm, MuspelBoss, MuspelEndless, MuspelHelForm, ConstructBoss, BirdNoObjective, BirdObjective, ElementalMage, ElementalMageV2, ElementalMelee, ElementalMeleeChampion, ElementalPlague, ElementalVenom, ElfWoman, ElfWomanV2, GhostKing, JotunnWarrior, JotunnWarriorV2, Beetle, BeetleBig, BeetleDesert, BeetleDesertBig, BeetlePortal, BeetleExplosive, BeetleBigExplosive, BatPrototype, BatCombat, BatLarge, CultistMelee, CultistBow }

        const string SphinxResources = "Jotunnslayer/SphinxSkeleton/";
        const string MilipedeResources = "Jotunnslayer/Milipede/";
        const string HorrorJotunResources = "Jotunnslayer/HorrorJotun/";
        const string HorrorAlfResources = "Jotunnslayer/HorrorAlf/";
        const string BatLootResources = "Jotunnslayer/BatLoot/";
        const string HybBossResources = "Jotunnslayer/HybBossEasy/";
        const string HybResources = "Jotunnslayer/HybSubBossEasy/";
        const string DriderResources = "Jotunnslayer/AlfheimDrider/";
        const string StoneJotunnResources = "Jotunnslayer/AlfheimStoneJotunn/";
        const string ElfRangedResources = "Jotunnslayer/AlfheimElfRanged/";
        const string ElfMeleeResources = "Jotunnslayer/AlfheimElfMelee/";
        const string SentinelResources = "Jotunnslayer/AlfheimSentinels/";
        const string AlfheimSpiderResources = "Jotunnslayer/AlfheimSpiders/";
        const string AlfheimResources = "Jotunnslayer/AlfheimBoss/";
        const string GhostResources = "Jotunnslayer/Ghost/";
        const string GhostSkullResources = "Jotunnslayer/GhostSkull/";
        internal static bool IsRedGhost(Kind kind) => kind is Kind.GhostRed or Kind.GhostRedBattlefield or Kind.GhostRedFollowing;
        const string BossResources = "Jotunnslayer/NiflheimBossEasy/";
        const string GuardianResources = "Jotunnslayer/SkeletonGuardian/";
        const string SpiderResources = "Jotunnslayer/SpiderRegular/";
        const string BowResources = "Jotunnslayer/SkeletonBowRegular/";
        public static string LastSpawnError { get; private set; }
        static readonly float[] GmSpawnAngles = { 0f, -30f, 30f, -60f, 60f };

        static CharacterInstance SpawnFailure(string reason)
        {
            LastSpawnError = reason;
            return null;
        }

        public static CharacterInstance TrySpawnInFrontOf(CharacterInstance target, bool archer = false) =>
            TrySpawnInFrontOf(target, archer ? Kind.Bow : Kind.Melee);

        public static CharacterInstance TrySpawnInFrontOf(CharacterInstance target, Kind kind)
        {
            LastSpawnError = null;
            if (!target || target.CurrentHealth <= 0) return SpawnFailure("需要存活的房主角色。");

            Vector3 forward = Quaternion.Euler(0, target.lookInput.y, 0) * Vector3.forward;
            var camera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (camera)
            {
                var viewForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
                if (viewForward.sqrMagnitude > .01f) forward = viewForward.normalized;
            }
            const int mask = GameManager.environmentLayer;
            // Search near the viewed direction; a single obstructed six-metre point must not block GM.
            for (int distanceIndex = 0; distanceIndex < 3; distanceIndex++)
            {
                float distance = distanceIndex == 0 ? 4f : distanceIndex == 1 ? 6f : 2.5f;
                foreach (float angle in GmSpawnAngles)
                {
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
                    Vector3 position = target.transform.position + direction * distance;
                    if (Physics.Raycast(target.transform.position + Vector3.up, direction, distance, mask,
                            QueryTriggerInteraction.Ignore) ||
                        !Physics.Raycast(position + Vector3.up * 4f, Vector3.down, out var ground, 12f, mask,
                            QueryTriggerInteraction.Ignore) || ground.normal.y < .65f) continue;
                    position = ground.point + Vector3.up * .15f;
                    if (Physics.CheckCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.5f, .45f,
                            mask, QueryTriggerInteraction.Ignore)) continue;
                    return TrySpawn(target, position, Quaternion.LookRotation(-direction), kind);
                }
            }
            return SpawnFailure("视野前方 2.5～6 米内没有可用落点，请面向靶场空地。");
        }

        internal static bool TrySpawnAtScreenEdge(CharacterInstance target, Kind kind, ref Camera gameplayCamera,
            ref int placementAttempt, out CharacterInstance enemy)
        {
            const float MinimumSpawnDistance = 6f;
            const float MaximumSpawnDistance = 36f;
            const float ViewportMargin = .05f;
            enemy = null;
            if (!gameplayCamera)
                gameplayCamera = GameplayCamera._instance
                    ? GameplayCamera._instance.GetComponent<Camera>()
                    : Camera.main;
            if (!gameplayCamera) return false;

            // Adapt the top-down ScreenEdge rule to the current TPS viewport.
            int attempt = placementAttempt++;
            float side = (attempt & 1) == 0 ? -ViewportMargin : 1f + ViewportMargin;
            float height = Mathf.Lerp(.12f, .38f, Mathf.Repeat(attempt * .61803399f, 1f));
            Ray ray = gameplayCamera.ViewportPointToRay(new Vector3(side, height, 0f));
            const int mask = GameManager.environmentLayer;
            if (!Physics.Raycast(ray, out var hit, MaximumSpawnDistance, mask, QueryTriggerInteraction.Ignore) ||
                hit.normal.y < .65f) return false;

            Vector3 offset = hit.point - target.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < MinimumSpawnDistance * MinimumSpawnDistance ||
                offset.sqrMagnitude > MaximumSpawnDistance * MaximumSpawnDistance) return false;

            Vector3 position = hit.point + Vector3.up * .15f;
            if (Physics.CheckCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.5f,
                    .45f, mask, QueryTriggerInteraction.Ignore)) return false;

            enemy = Skeleton1HSpawn.TrySpawn(target, position, Quaternion.LookRotation(-offset), kind);
            return enemy;
        }

        internal static bool TrySpawnOutsideViewCircle(CharacterInstance target, Kind kind,
            ref Camera gameplayCamera, out CharacterInstance enemy)
        {
            enemy = null;
            if (!target) return false;
            if (!gameplayCamera)
                gameplayCamera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (!gameplayCamera) return false;
            // TPS adaptation of GetRandomCircularPointOutsideView. Radius requires original-camera calibration.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(18f, 30f);
                Vector3 candidate = target.transform.position + offset;
                const int mask = GameManager.environmentLayer;
                if (!Physics.Raycast(candidate + Vector3.up * 8f, Vector3.down, out var hit, 20f,
                    mask, QueryTriggerInteraction.Ignore) || hit.normal.y < .65f) continue;
                Vector3 position = hit.point + Vector3.up * .15f;
                Vector3 viewport = gameplayCamera.WorldToViewportPoint(position + Vector3.up);
                if (viewport.z > 0f && viewport.x >= -.05f && viewport.x <= 1.05f &&
                    viewport.y >= -.05f && viewport.y <= 1.05f) continue;
                if (Physics.CheckCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.5f,
                    .45f, mask, QueryTriggerInteraction.Ignore)) continue;
                enemy = TrySpawn(target, position, Quaternion.LookRotation(-offset), kind);
                return enemy;
            }
            return false;
        }

        // Local-host validation until monster identity and animation cues are replicated.
        internal static CharacterInstance TrySpawn(CharacterInstance target, Vector3 position, Quaternion rotation,
            Kind kind = Kind.Melee, bool passThrough = false)
        {
            if (!NetworkServer.activeHost) return SpawnFailure("巨人杀手怪物目前需要房主本机运行。");
            if (NetworkServer.connections.Count != 1) return SpawnFailure("当前怪物接入仅支持单人房主测试，请使用单人训练场。");
            if (!target || target.CurrentHealth <= 0) return SpawnFailure("召唤角色无效或已死亡。");
            if (GameManager.Gamemode == null) return SpawnFailure("当前游戏模式尚未就绪。");

            var characterPrefab = GameManager.Gamemode.GetPlayerCharacterPrefab();
            var (folder, modelName, spawn, idle, walk, attack, hit) = kind switch
            {
                Kind.BeetleDesertBig => ("Jotunnslayer/BeetleDesertBig/", "beetle_desert_big_sur", "beetle_veteran_idle", "beetle_veteran_idle", "beetle_veteran_flying", "beetle_veteran_attack", "beetle_veteran_idle"),
                Kind.BeetlePortal => ("Jotunnslayer/BeetlePortal/", "beetle_portal_sur", "beetle_minion_summon", "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun"),
                Kind.BeetleExplosive => ("Jotunnslayer/BeetleExplosive/", "beetle_explo_sur", "beetle_minion_summon", "beetle_minion_idle", "beetle_minion_fly", "beetle_minion_suicide", "beetle_minion_stun"),
                Kind.BeetleBigExplosive => ("Jotunnslayer/BeetleBigExplosive/", "beetle_big_explo_sur", "beetle_veteran_idle", "beetle_veteran_idle", "beetle_veteran_flying", "beetle_veteran_attack", "beetle_veteran_idle"),
                Kind.BatPrototype => ("Jotunnslayer/BatPrototype/", "bat", "bat_simple_init", "bat_simple_stand", "bat_simple_run", "bat_simple_attack_03", "bat_simple_hit_forward"),
                Kind.BatCombat => ("Jotunnslayer/BatCombat/", "bat_sur", "bat_simple_init", "bat_simple_stand", "bat_simple_run", "bat_simple_attack_03", "bat_simple_hit_forward"),
                Kind.BatLarge => ("Jotunnslayer/BatLarge/", "bat_large_sur", "bat_large_init", "bat_large_stand", "bat_large_run", "bat_large_attack_03", "bat_large_stun"),
                Kind.CultistMelee => ("Jotunnslayer/CultistMelee/", "cultist_1h_sur", "dualwield_ec_appear_summon", "1h_stand_combat", "1h_walk_combat", "1h_attack_04", "1h_hit"),
                Kind.CultistBow => ("Jotunnslayer/CultistBow/", "cultist_bow_sur", "bow_init", "bow_stand_attack", "bow_walk", "bow_attack_01", "bow_hit"),
                Kind.BeetleBig => ("Jotunnslayer/BeetleBig/", "beetle_big_sur", "beetle_veteran_idle", "beetle_veteran_idle", "beetle_veteran_flying", "beetle_veteran_attack", "beetle_veteran_idle"),
                Kind.BeetleDesert => ("Jotunnslayer/BeetleDesert/", "beetle_desert_sur", "beetle_minion_summon", "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun"),
                Kind.Beetle => ("Jotunnslayer/Beetle/", "beetle_sur", "beetle_minion_summon", "beetle_minion_idle", "beetle_minion_fly", "beetle_veteran_attack", "beetle_minion_stun"),
                Kind.AlfheimPhase1 => (AlfheimResources, "jotunn_alfheim_boss_sur", "jotunn_spider_init_01",
                    "jotunn_spider_idle_01", "jotunn_spider_walk_01", "jotunn_spider_idle_attack", "jotunn_spider_idle_01"),
                Kind.AlfheimPhase2 => (AlfheimResources, "jotunn_alfheim_boss_phase2_sur", "jotunn_spider_init_02",
                    "jotunn_spider_idle_02", "jotunn_spider_walk_02", "jotunn_spider_idle_attack", "jotunn_spider_idle_02"),
                Kind.SphinxSkeleton => (ResourcePath, "skeleton_1h_sphinx_sur", "skeleton_summon_ground",
                    "1h_stand_combat", "1h_walk", "1h_attack_04_nofire", "1h_hit"),
                Kind.HybSubBossEasy or Kind.HybSubBossNormal => (HybResources, "hyb_sub_boss_sur_VarEasy", "staff_init",
                    "staff_stand_attack", "staff_walk", "staff_attack_01_skill", "staff_hit"),
                Kind.JotunnWarrior or Kind.JotunnWarriorV2 => ("Jotunnslayer/JotunnWarrior/", "jotunn_warrior_sur",
                    "jotunn_muspel_init", "jotunn_muspel_stand_v02", "jotunn_muspel_walk_v02", "jotunn_warrior_stomp", "jotunn_muspel_stand_v02"),
                Kind.GhostKing => ("Jotunnslayer/GhostKing/", "ghost_king_sur",
                    "ghost_v2_summon", "ghost_v2_stand", "ghost_v2_stand", "ghost_v2_attack_03_long", "ghost_v2_stun"),
                Kind.ElfWoman or Kind.ElfWomanV2 => ("Jotunnslayer/ElfWoman/", "elf_woman_sur",
                    "woman_spider_spawn_01", "woman_spider_stand", "woman_spider_walk", "woman_spider_cast_02", "woman_spider_stun"),
                Kind.ElementalMelee or Kind.ElementalMeleeChampion => ("Jotunnslayer/ElementalMelee/", "ele_mb_melee_champion_sur",
                    "elem_mb_melee_summon_plc", "elem_mb_melee_stand", "elem_mb_melee_walk", "elem_mb_melee_stomp", "elem_mb_melee_stand"),
                Kind.ElementalMage or Kind.ElementalMageV2 or Kind.ElementalPlague or Kind.ElementalVenom => ("Jotunnslayer/ElementalMage/", "ele_mb_mage_sur",
                    "elem_mage_summon_ground", "elem_mb_mage_stand", "elem_mb_mage_walk", "elem_mage_skill_jumpattack", "elem_mb_mage_stand"),
                Kind.BirdNoObjective or Kind.BirdObjective => ("Jotunnslayer/BirdBoss/", "bird_noobj_sur", "bird_summon_flydown",
                    "bird_stand_01", "bird_walk_02", "bird_skill_dive", "bird_death"),
                Kind.ConstructBoss => ("Jotunnslayer/ConstructBoss/", "jotunn_construct_sur", "jotunn_construct_init",
                    "jotunn_construct_idle", "jotunn_construct_idle", "jotunn_construct_saw_v5", "jotunn_construct_hit"),
                Kind.MuspelBoss or Kind.MuspelEndless or Kind.MuspelHelForm => ("Jotunnslayer/MuspelBoss/", "jotunn_muspel_sur", "jotunn_muspel_init",
                    "jotunn_muspel_stand_v02", "jotunn_muspel_walk_v02", "jotunn_muspel_line_attack", "jotunn_muspel_stand_v02"),
                Kind.EttinBoss or Kind.EttinEndless or Kind.EttinHelForm => ("Jotunnslayer/EttinBoss/", "ettin_boss", "ettin_init",
                    "etin_stand", "etin_walk", "ettin_attack_noevent", "etin_stand"),
                Kind.HybBossEasy or Kind.HybBossNormal or Kind.HybBossHard => (HybResources, "hyb_boss_sur_VarEasy", "staff_init",
                    "staff_stand_attack", "staff_walk", "staff_attack_01_skill", "staff_hit"),
                Kind.Bow => (BowResources, "skeleton_bow_VarRegular", "dualwield_ec_appear_summon",
                    "bow_stand_attack", "bow_run", "bow_attack_01", "bow_hit"),
                Kind.SphinxBow => (BowResources, "skeleton_bow_sphinx", "bow_init",
                    "bow_stand_attack", "bow_run", "bow_attack_01", "bow_hit"),
                Kind.SphinxGuardian => (SphinxResources, "skeleton_guardian_sphinx_sur", "dual_init",
                    "dual_stand_attack", "dual_walk", "dual_attack_04_double", "dual_hit"),
                Kind.SphinxMiniBoss => (GuardianResources, "skeleton_mb_sphinx_sur", "2handed_stand_attack_summon_from_statue",
                    "2handed_stand_attack", "2handed_walk", "2handed_skill_04", "2handed_hit"),
                Kind.Milipede or Kind.MilipedeInsane => (MilipedeResources, "milipede_sur", "mili_gr_appear_summon",
                    "mili_gr_stand_attack", "mili_gr_run", "mili_gr_attack_melee_01", "mili_gr_hit"),
                Kind.HorrorAlf => (HorrorAlfResources, "horror_expl_alf_sur", "summon",
                    "attack_stand_01", "run_01", "horror_suicide", "stun_01"),
                Kind.HorrorJotun or Kind.GhostSvart => (HorrorJotunResources, "horror_expl_jotun_sur", "jotunheim_exploder_init",
                    // No hit motion is used by HorrorJotunCombat. Never bind the incompatible death clip as Hit.
                    "jotunheim_exploder_idle", "jotunheim_exploder_move", "jotunheim_exploder_attack1_skill", "jotunheim_exploder_idle"),
                Kind.BatLoot => (BatLootResources, "bat_loot_sur", "bat_n_init",
                    "bat_n_stand_40fps", "bat_n_run", "bat_n_stand_40fps", "bat_n_hit_forward"),
                Kind.GhostSkull => (GhostSkullResources, "ghost_skull_sur", "move", "move", "move", "move", "move"),
                Kind.Ghost or Kind.GhostRed or Kind.GhostRedBattlefield or Kind.GhostRedFollowing => (GhostResources, "ghost_sur", "spectrals_summon",
                    "spectrals_move", "spectrals_move", "spectrals_move", "spectrals_move"),
                Kind.NiflheimBossEasy or Kind.NiflheimBossNormal or Kind.NiflheimBossEndless or Kind.NiflheimBossHelForm => (BossResources, "skeleton_goat_sur_boss_VarEasy", "goatlord_init",
                    "goatlord_attack_stand_a", "goatlord_walk_short", "goatlord_attack_01", "goatlord_hit_forward"),
                Kind.Guardian => (GuardianResources, "skeleton_guardian_sur", "2handed_init",
                    "2handed_stand_attack", "2handed_walk", "2handed_attack_01", "2handed_hit"),
                Kind.AlfheimDrider or Kind.AlfheimDriderVariant => (DriderResources,
                    kind == Kind.AlfheimDrider ? "elf_spider_sur" : "elf_spider_02_sur", "elf_spider_init", "elf_spider_idle",
                    "elf_spider_walk", "elf_spider_attack_noevent", "elf_spider_stun"),
                Kind.AlfheimStoneJotunn or Kind.AlfheimStoneJotunnInsane => (StoneJotunnResources,
                    kind == Kind.AlfheimStoneJotunn ? "jotunn_stone_sur" : "jotunn_stone_insane_sur",
                    "stone_sentinel_init", "stone_sentinel_idle", "stone_sentinel_walk", "stone_sentinel_skill",
                    "stone_sentinel_stun"),
                Kind.AlfheimElfRanged => (ElfRangedResources, "elf_ranged_sur",
                    "elf_ranged_init", "elf_ranged_idle", "elf_ranged_walk", "elf_ranged_attack_skill", "elf_ranged_stun"),
                Kind.AlfheimElfMelee or Kind.AlfheimElfMeleeInsane => (ElfMeleeResources,
                    kind == Kind.AlfheimElfMelee ? "elf_melee_sur" : "elf_melee_insane_sur",
                    "elf_melee_init", "elf_melee_idle", "elf_melee_walk", "elf_melee_attack_noevent", "elf_melee_stun"),
                Kind.AlfheimSentinel or Kind.AlfheimSentinelVeteran => (SentinelResources,
                    kind == Kind.AlfheimSentinel ? "sentinel_alf_sur" : "sentinel_veteran_alf_sur",
                    "talon_spawn", "talon_stand", "talon_move", "talon_attack_02", "talon_hit"),
                Kind.AlfheimSpider or Kind.AlfheimSpiderVeteran => (AlfheimSpiderResources,
                    kind == Kind.AlfheimSpider ? "spider_alf_sur" : "spider_alf_veteran_sur",
                    "spider_aflh_init", "spider_aflh_idle", "spider_alfh_walk",
                    "spider_aflh_attack_noevent", "spiderling_hit_01"),
                Kind.Spider or Kind.SpiderInsane => (SpiderResources, "spider_sur_VarRegular", "spiderling_summon_ground",
                    "spiderling_stand", "spiderling_walk", "spiderling_attack_03_nofire", "spiderling_hit_01"),
                _ => (ResourcePath, "skeleton_1h_sur", "skeleton_summon_ground",
                    "1h_stand_combat", "1h_walk", "1h_attack_04_nofire", "1h_hit")
            };
            bool hybCaster = kind is Kind.HybSubBossEasy or Kind.HybSubBossNormal;
            bool hybBoss = kind is Kind.HybBossEasy or Kind.HybBossNormal or Kind.HybBossHard;
            bool sphinxMiniBoss = kind == Kind.SphinxMiniBoss;
            bool boss = kind is Kind.NiflheimBossEasy or Kind.NiflheimBossNormal or Kind.NiflheimBossEndless or Kind.NiflheimBossHelForm;
            bool ghost = kind is Kind.Ghost or Kind.GhostSvart or Kind.GhostSkull || IsRedGhost(kind);
            bool alfheim = kind == Kind.AlfheimPhase1 || kind == Kind.AlfheimPhase2;
            bool alfheimSpider = kind is Kind.AlfheimSpider or Kind.AlfheimSpiderVeteran;
            bool sentinel = kind is Kind.AlfheimSentinel or Kind.AlfheimSentinelVeteran;
            bool elfMelee = kind is Kind.AlfheimElfMelee or Kind.AlfheimElfMeleeInsane;
            bool elfRanged = kind == Kind.AlfheimElfRanged;
            bool stoneJotunn = kind is Kind.AlfheimStoneJotunn or Kind.AlfheimStoneJotunnInsane;
            bool drider = kind is Kind.AlfheimDrider or Kind.AlfheimDriderVariant;
            bool milipede = kind is Kind.Milipede or Kind.MilipedeInsane;
            bool ettin = kind is Kind.EttinBoss or Kind.EttinEndless or Kind.EttinHelForm;
            bool muspelHel = kind is Kind.MuspelEndless or Kind.MuspelHelForm;
            bool muspel = kind == Kind.MuspelBoss || muspelHel;
            bool embeddedMaterials = (kind is Kind.Beetle or Kind.BeetleBig or Kind.BeetleDesert or Kind.BeetleDesertBig or Kind.BeetlePortal or Kind.BeetleExplosive or Kind.BeetleBigExplosive or Kind.BatPrototype or Kind.BatCombat or Kind.BatLarge or Kind.CultistMelee or Kind.CultistBow) || ettin || muspel || kind is Kind.ConstructBoss or Kind.BirdNoObjective or Kind.BirdObjective or Kind.ElementalMage or Kind.ElementalMageV2 or Kind.ElementalMelee or Kind.ElementalMeleeChampion or Kind.ElementalPlague or Kind.ElementalVenom or Kind.ElfWoman or Kind.ElfWomanV2 or Kind.GhostKing or Kind.JotunnWarrior or Kind.JotunnWarriorV2;
            bool generic = embeddedMaterials || milipede || kind is Kind.Spider or Kind.SpiderInsane or Kind.HorrorJotun or Kind.HorrorAlf or Kind.BatLoot || boss || ghost || alfheim || alfheimSpider || sentinel || elfMelee || elfRanged || stoneJotunn || drider;
            string modelFolder = hybBoss ? HybBossResources
                : kind is Kind.SphinxSkeleton or Kind.SphinxBow or Kind.SphinxMiniBoss ? SphinxResources : folder;
            var modelPrefab = Resources.Load<GameObject>(modelFolder + modelName);
            if (alfheim) folder += kind == Kind.AlfheimPhase1 ? "Phase1/" : "Phase2/";
            string texture = kind switch
            {
                Kind.SphinxSkeleton or Kind.SphinxBow => SphinxResources + "sphinx",
                Kind.SphinxGuardian => SphinxResources + "sphinx_elite",
                Kind.SphinxMiniBoss => SphinxResources + "sphinx_miniboss",
                Kind.Milipede => MilipedeResources + "necropede_negro",
                Kind.MilipedeInsane => MilipedeResources + "necropede_sick",
                Kind.HorrorJotun or Kind.GhostSvart => HorrorJotunResources + "horror_jotunheim",
                Kind.HorrorAlf => HorrorAlfResources + "horror_expl_alf",
                Kind.BatLoot => BatLootResources + "bat_loot",
                Kind.AlfheimElfMelee => ElfMeleeResources + "elven_wraith",
                Kind.AlfheimElfMeleeInsane => ElfMeleeResources + "elven_wraith_insane",
                Kind.AlfheimStoneJotunn => StoneJotunnResources + "jotun_sentinel",
                Kind.AlfheimStoneJotunnInsane => StoneJotunnResources + "jotunn_stone_insane",
                Kind.AlfheimDrider => DriderResources + "elf_drider",
                Kind.AlfheimDriderVariant => DriderResources + "elf_drider_insane",
                Kind.AlfheimElfRanged => ElfRangedResources + "elven_wraith_ranged",
                Kind.AlfheimSentinel or Kind.AlfheimSentinelVeteran => SentinelResources + "gargoyle",
                Kind.Spider => SpiderResources + "spiderling",
                Kind.SpiderInsane => SpiderResources + "spiderling_elite",
                Kind.AlfheimSpider => AlfheimSpiderResources + "spider_alf",
                Kind.AlfheimSpiderVeteran => AlfheimSpiderResources + "spider_alf_veteran",
                Kind.Ghost => GhostResources + "ghost",
                Kind.GhostSkull => GhostSkullResources + "ghost_skull2",
                Kind.GhostRed or Kind.GhostRedBattlefield or Kind.GhostRedFollowing => GhostResources + "ghost_red",
                Kind.AlfheimPhase1 or Kind.AlfheimPhase2 => AlfheimResources + "jotunn_spider",
                Kind.Guardian => GuardianResources + "skeleton_part2",
                _ => ResourcePath + "skeleton_part1"
            };
            var albedo = Resources.Load<Texture2D>(hybCaster || hybBoss ? HybResources + "sphinx_boss_dm" :
                boss ? BossResources + "skeleton_goat_albedo" : texture + "_dm");
            var normal = Resources.Load<Texture2D>(kind == Kind.SpiderInsane ? SpiderResources + "spiderling_nm"
                : kind == Kind.MilipedeInsane ? MilipedeResources + "necropede_nm"
                : IsRedGhost(kind) ? GhostResources + "ghost_nm"
                : kind == Kind.GhostSkull ? GhostSkullResources + "ghost_skull_nm"
                : hybBoss ? HybBossResources + "sphinx_boss_nm"
                : boss ? BossResources + "skeleton_goat_normal"
                : kind == Kind.SphinxGuardian ? SphinxResources + "sphinx_nm" : texture + "_nm");
            var alternate = boss ? Resources.Load<AnimationClip>(folder + "goatlord_attack_02")
                : kind == Kind.Guardian ? Resources.Load<AnimationClip>(folder + "2handed_attack_02")
                : kind == Kind.SphinxGuardian ? Resources.Load<AnimationClip>(folder + "dual_attck_03_double")
                : kind == Kind.AlfheimElfMeleeInsane ? Resources.Load<AnimationClip>(folder + "elf_melee_attack_skill")
                : drider ? Resources.Load<AnimationClip>(folder + "elf_spider_attack_03_skill")
                : milipede ? Resources.Load<AnimationClip>(folder + "mili_gr_spit")
                : kind == Kind.SpiderInsane ? Resources.Load<AnimationClip>(folder + "spiderling_attack_01_skill")
                : sentinel ? Resources.Load<AnimationClip>(folder + "talon_attack_01")
                : kind == Kind.AlfheimPhase2 ? Resources.Load<AnimationClip>(folder + "jotunn_spider_roundslash")
                : kind is Kind.JotunnWarrior or Kind.JotunnWarriorV2 ? Resources.Load<AnimationClip>(folder + "jotunn_warrior_line_skill")
                : null;
            var change = boss ? Resources.Load<AnimationClip>(folder + "goatlord_change")
                : kind == Kind.AlfheimPhase1 ? Resources.Load<AnimationClip>(folder + "jotunn_spider_mid_cutscene") : null;
            var horns = boss ? Resources.Load<AnimationClip>(folder + "goatlord_skill_horns_a")
                : drider ? Resources.Load<AnimationClip>(folder + "elf_spider_skill_web") : null;
            if (drider && (!alternate || alternate.humanMotion || !horns || horns.humanMotion)) return null;
            if (milipede && (!alternate || alternate.humanMotion)) return SpawnFailure("蜈蚣吐弹动画缺失或导入类型错误。");
            if (kind == Kind.SpiderInsane && (!alternate || alternate.humanMotion))
                return SpawnFailure("疯狂蜘蛛曲射动画缺失或导入类型错误。");
            var summon = boss ? Resources.Load<AnimationClip>(folder + "goatlord_skill_buff_a")
                : kind == Kind.AlfheimDriderVariant ? Resources.Load<AnimationClip>(folder + "elf_spider_skill_summon") : null;
            if (kind == Kind.AlfheimDriderVariant && (!summon || summon.humanMotion)) return null;
            var jump = boss ? Resources.Load<AnimationClip>(folder + "goatlord_skill_jump") : null;
            if (boss && (!alternate || alternate.humanMotion || !change || change.humanMotion ||
                !horns || horns.humanMotion || !summon || summon.humanMotion || !jump || jump.humanMotion)) return null;
            if (kind == Kind.Guardian && (!alternate || !alternate.humanMotion)) return null;
            if ((sentinel || kind == Kind.AlfheimElfMeleeInsane) && (!alternate || alternate.humanMotion)) return null;
            var wave = kind == Kind.ElementalMeleeChampion ? Resources.Load<AnimationClip>(folder + "elem_mb_melee_skill_plc")
                : kind == Kind.ElementalMageV2 ? Resources.Load<AnimationClip>(folder + "elem_mage_skill_wave")
                : kind is Kind.ElementalPlague or Kind.ElementalVenom ? Resources.Load<AnimationClip>(folder + "elem_mage_skill_cast")
                : muspel ? Resources.Load<AnimationClip>(folder + "jotunn_muspel_line_skill")
                : kind == Kind.AlfheimPhase2
                ? Resources.Load<AnimationClip>(folder + "jotunn_spider_tailattack") : null;
            var spikes = kind == Kind.AlfheimPhase2
                ? Resources.Load<AnimationClip>(folder + "jotunn_spider_spikes") : null;
            var burrow = kind == Kind.AlfheimPhase2
                ? Resources.Load<AnimationClip>(folder + "jotunn_spider_burrow") : null;
            if (kind == Kind.AlfheimPhase2 && (!alternate || alternate.humanMotion || !wave ||
                wave.humanMotion || !spikes || spikes.humanMotion || !burrow || burrow.humanMotion))
                return null;
            var splash = kind == Kind.ElementalPlague ? Resources.Load<AnimationClip>(folder + "elem_mage_skill_wave")
                : kind == Kind.AlfheimPhase1
                ? Resources.Load<AnimationClip>(folder + "jotunn_spider_splash") : null;
            var spiderSummon = kind is Kind.ElfWoman or Kind.ElfWomanV2
                ? Resources.Load<AnimationClip>(folder + "woman_spider_cast_01")
                : kind == Kind.AlfheimPhase1
                ? Resources.Load<AnimationClip>(folder + "jotunn_spider_summon") : null;
            if (kind == Kind.AlfheimPhase1 && (!splash || splash.humanMotion || !spiderSummon ||
                spiderSummon.humanMotion || !change || change.humanMotion))
                return null;
            var circles = ettin ? Resources.Load<AnimationClip>(folder + (kind is Kind.EttinEndless or Kind.EttinHelForm ? "etin_nova" : "etin_lightning_attack"))
                : kind == Kind.NiflheimBossNormal
                ? Resources.Load<AnimationClip>(BossResources + "goatlord_skill_castlavafields")
                : hybCaster || hybBoss ? Resources.Load<AnimationClip>(HybResources + "staff_skill_grenade_short") : null;
            if (ettin && (!circles || circles.humanMotion)) return SpawnFailure("Ettin lightning clip missing.");
            if (kind == Kind.NiflheimBossNormal && (!circles || circles.humanMotion))
                return SpawnFailure("山羊领主冰冻施法动作未正确导入。");
            var hybVolley = hybCaster || hybBoss ? Resources.Load<AnimationClip>(HybResources + "staff_skill_summonclap") : null;
            if ((hybCaster || hybBoss) && (!circles || !circles.humanMotion || !hybVolley || !hybVolley.humanMotion)) return null;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!characterPrefab) return SpawnFailure("当前模式没有角色载体预制体。");
            if (!modelPrefab) return SpawnFailure("怪物模型未加载：" + modelFolder + modelName);
            if (!embeddedMaterials && !albedo) return SpawnFailure("怪物颜色贴图未加载：" + texture + "_dm");
            if (!embeddedMaterials && !hybCaster && !normal) return SpawnFailure("怪物法线贴图未加载：" + texture + "_nm");
            if (!shader) return SpawnFailure("当前渲染管线缺少 URP Lit 材质。");

            var instance = Object.Instantiate(characterPrefab, position, rotation);
            if (!instance.TryGetComponent<CharacterInstance>(out var body) || !body.CharacterItemManager)
            {
                Object.Destroy(instance);
                return SpawnFailure("怪物载体缺少角色或武器槽组件。");
            }

            // These training monsters own their presentation. Do not create a delayed
            // player display in Start, or a soldier corpse when their shared Health dies.
            if (kind == Kind.Melee || milipede)
            {
                var playerVisual = instance.GetComponent<RangeRobotVisual>();
                if (playerVisual) { playerVisual.enabled = false; Object.Destroy(playerVisual); }
                var playerDeath = instance.GetComponent<HumanoidDeath>();
                if (playerDeath) { playerDeath.enabled = false; Object.Destroy(playerDeath); }
            }

            var originalRenderers = instance.GetComponentsInChildren<Renderer>();
            var model = Object.Instantiate(modelPrefab, instance.transform);
            // Exported FBX roots can retain the original scene position (Milipede does).
            // The carrier owns placement and facing; the imported visual is local to it.
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (kind is Kind.HybSubBossEasy or Kind.HybSubBossNormal or Kind.ConstructBoss or Kind.NiflheimBossEasy or Kind.NiflheimBossNormal or Kind.NiflheimBossEndless or Kind.NiflheimBossHelForm or Kind.EttinBoss or Kind.JotunnWarrior or Kind.JotunnWarriorV2 or Kind.HybBossEasy or Kind.HybBossNormal or Kind.HybBossHard)
            {
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length > 0)
                {
                    var bounds = skins[0].bounds;
                    for (int i = 1; i < skins.Length; i++) bounds.Encapsulate(skins[i].bounds);
                    float displaySize = kind == Kind.ConstructBoss ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : bounds.size.y;
                    if (displaySize > .01f) model.transform.localScale *= (hybCaster ? 3f : 5f) / displaySize;
                }
            }
            // FBX unit fixes do not update the source Humanoid avatar's human scale.
            if (kind is Kind.Bow or Kind.Guardian or Kind.SphinxSkeleton && model.TryGetComponent<Animator>(out var displayAnimator))
            {
                var displayAvatar = Resources.Load<Avatar>(modelFolder + "DisplayAvatar");
                if (displayAvatar) displayAnimator.avatar = displayAvatar;
            }
            if (!model.TryGetComponent<Animator>(out var animator) || !animator.avatar ||
                !animator.avatar.isValid || animator.avatar.isHuman == generic)
            {
                Object.Destroy(instance);
                return SpawnFailure("怪物模型的 Animator／Avatar 不可用或动画类型不匹配：" + modelName);
            }

            Material material = null;
            Material bagMaterial = null;
            if (!embeddedMaterials)
            {
                material = new Material(shader);
                material.SetTexture("_BaseMap", albedo);
                if (kind == Kind.BatLoot)
                    material.SetTexture("_OcclusionMap", Resources.Load<Texture2D>(BatLootResources + "bat_loot_rm"));
                if (kind == Kind.HorrorAlf)
                    material.SetTexture("_OcclusionMap", Resources.Load<Texture2D>(HorrorAlfResources + "horror_expl_alf_ao"));
                if (!hybCaster)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    material.SetTexture("_EmissionMap", Resources.Load<Texture2D>(HybResources + "sphinx_boss_clone_em"));
                    material.SetColor("_EmissionColor", Color.white);
                    material.EnableKeyword("_EMISSION");
                }
                if (ghost || alfheim || alfheimSpider || elfMelee || elfRanged || stoneJotunn || drider ||
                    kind is Kind.SphinxGuardian or Kind.HorrorJotun or Kind.HorrorAlf or Kind.SpiderInsane || milipede || hybBoss || sphinxMiniBoss)
                {
                    material.SetTexture("_EmissionMap", Resources.Load<Texture2D>(hybBoss
                        ? HybBossResources + "sphinx_boss_em" : texture + "_em"));
                    material.SetColor("_EmissionColor", kind == Kind.SpiderInsane ? new Color(0f, 6.0686f, 1.7908f) : Color.white);
                    material.EnableKeyword("_EMISSION");
                }

                if (kind == Kind.AlfheimPhase1)
                {
                    string bag = AlfheimResources + "jotunn_spider_bag";
                    bagMaterial = new Material(material);
                    bagMaterial.SetTexture("_BaseMap", Resources.Load<Texture2D>(bag + "_dm"));
                    bagMaterial.SetTexture("_BumpMap", Resources.Load<Texture2D>(bag + "_nm"));
                    bagMaterial.SetTexture("_EmissionMap", Resources.Load<Texture2D>(bag + "_em"));
                }

                Renderer[] renderers = sphinxMiniBoss
                    ? model.GetComponentsInChildren<Renderer>()
                    : model.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var renderer in renderers)
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = bagMaterial && slots[i] && slots[i].name.Contains("jotunn_spider_bag")
                            ? bagMaterial : material;
                    renderer.sharedMaterials = slots;
                }

            }

            if (kind is Kind.EttinEndless or Kind.EttinHelForm)
            {
                var armor = Resources.Load<Material>(folder + "etin_dead_armor");
                var bodyMaterial = Resources.Load<Material>(folder + "etin_dead_body");
                if (!armor || !bodyMaterial)
                {
                    Object.Destroy(instance);
                    return SpawnFailure("Ettin Endless materials missing.");
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterial = skin.name == "etin_armor" ? armor : bodyMaterial;
            }

            if (kind == Kind.JotunnWarriorV2)
            {
                var variant = Resources.Load<Material>(folder + "V2WarriorMaterial");
                if (!variant)
                {
                    Object.Destroy(instance);
                    return SpawnFailure("Jotunn Warrior V2 material missing.");
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterial = variant;
            }
            if (kind == Kind.ElfWomanV2)
            {
                var variant = Resources.Load<Material>(folder + "V2ElfWomanMaterial");
                if (!variant)
                {
                    Object.Destroy(instance);
                    return SpawnFailure("Elf Woman V2 material missing.");
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterial = variant;
            }
            if (kind is Kind.ElementalMageV2 or Kind.ElementalPlague or Kind.ElementalVenom)
            {
                string materialName = kind == Kind.ElementalPlague ? "PlagueMageMaterial" :
                    kind == Kind.ElementalVenom ? "VenomMageMaterial" : "V2MageMaterial";
                var variant = Resources.Load<Material>(folder + materialName);
                if (!variant)
                {
                    Object.Destroy(instance);
                    return SpawnFailure("Elemental Mage material missing: " + materialName);
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterial = variant;
            }
            if (muspelHel)
            {
                var helMaterial = Resources.Load<Material>(folder + "MuspelHelMaterial");
                if (!helMaterial)
                {
                    Object.Destroy(instance);
                    return SpawnFailure("Muspel Hel 材质未加载。");
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterial = helMaterial;
            }
            model.SetActive(true);
            if (kind == Kind.HorrorAlf) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (kind == Kind.GhostSkull) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                // Decoded rigid attachments contain invalid vertex data; keep the valid animated body.
                renderer.enabled = kind != Kind.ConstructBoss || renderer is not MeshRenderer;
                renderer.forceRenderingOff = false;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
            var visual = model.AddComponent<Skeleton1HVisual>();
            if (kind is Kind.EttinEndless or Kind.EttinHelForm)
                hybVolley = Resources.Load<AnimationClip>(folder + "etin_poison_attack");
            if (kind == Kind.ConstructBoss)
                visual.BindConstructSkills(
                    Resources.Load<AnimationClip>(folder + "jotunn_construct_hammer_medium"),
                    Resources.Load<AnimationClip>(folder + "jotunn_construct_hammer_long"),
                    Resources.Load<AnimationClip>(folder + "jotunn_construct_flame_left"),
                    Resources.Load<AnimationClip>(folder + "jotunn_construct_flame_right"),
                    Resources.Load<AnimationClip>(folder + "jotunn_construct_crystal"));
            visual.Bind(
                Resources.Load<AnimationClip>((sphinxMiniBoss ? SphinxResources : folder) + spawn),
                Resources.Load<AnimationClip>(folder + idle),
                Resources.Load<AnimationClip>((hybBoss || hybCaster ? HybBossResources : folder) + walk),
                Resources.Load<AnimationClip>((sphinxMiniBoss ? SphinxResources : folder) + attack),
                Resources.Load<AnimationClip>(folder + hit),
                material, alternate, change, horns, summon, jump, bagMaterial, circles, hybVolley, wave, spikes, burrow, splash, spiderSummon);
            if (!visual.IsConfigured)
            {
                Object.Destroy(instance);
                return SpawnFailure("怪物的出生／待机／移动／攻击／受击动画未正确加载或类型不匹配：" + modelName);
            }

            foreach (var slot in body.CharacterItemManager.Slots)
                slot.ItemOnSpawn = null;
            GameManager.Gamemode.ConfigurePlayerCharacter(body);
            body.Team = target.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(instance);
            body.SetAsBOT(true);
            bool started = kind is Kind.BeetleExplosive or Kind.BeetleBigExplosive
                ? instance.AddComponent<BeetleExplosionCombat>().Begin(target, visual)
                : kind is Kind.HorrorJotun or Kind.HorrorAlf
                ? instance.AddComponent<HorrorJotunCombat>().Begin(target, visual, kind == Kind.HorrorAlf)
                : kind == Kind.BatLoot
                ? instance.AddComponent<BatLootFleeing>().Begin(target, visual)
                : ghost || passThrough
                ? instance.AddComponent<MonsterPassThrough>().Begin(body, target, visual, rotation * Vector3.forward, kind)
                : instance.AddComponent<Skeleton1HCombat>().Begin(target, visual, kind);
            if (!started)
            {
                NetworkServer.Destroy(instance);
                return SpawnFailure("怪物生成后战斗初始化失败：" + modelName);
            }

            foreach (var renderer in originalRenderers)
                if (renderer) renderer.enabled = false;

            return body;
        }
    }
}
