#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.RainImported;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Jotunnslayer
{
    public sealed class Skeleton1HHostSmoke : MonoBehaviour
    {
        const string MenuPath = "Tools/Jotunnslayer/Run Skeleton 1H Host Smoke";

        const string GuardianWaveMenu = "Tools/Jotunnslayer/Run Niflheim W8 Host Smoke";
        const string MixedWaveMenu = "Tools/Jotunnslayer/Run Niflheim W7 Host Smoke";
        const string WaveMenu = "Tools/Jotunnslayer/Run Niflheim W4 Host Smoke";
        const string BowMenu = "Tools/Jotunnslayer/Run Skeleton Bow Host Smoke";
        const string GmMenu = "Tools/Jotunnslayer/Run Monster GM UI Smoke";
        const int GmSmokeMode = -1;
        const int MonsterPlaytestMode = -2;
        const string BatchRunKey = "Jotunnslayer.AlfheimBatchSmoke";
        const string BatchResultKey = "Jotunnslayer.AlfheimBatchSmoke.Result";
        const string AlfheimRangedCapMenu = "Tools/Jotunnslayer/Run Alfheim W8 Ranged Cap Smoke";
        const string AlfheimStampedeMenu = "Tools/Jotunnslayer/Run Alfheim W2 Stampede Smoke";
        const string AlfheimCapMenu = "Tools/Jotunnslayer/Run Alfheim W4 Active Cap Smoke";
        const string AlfheimOpeningMenu = "Tools/Jotunnslayer/Run Alfheim W1 Host Smoke";
        const string AlfheimEncounterMenu = "Tools/Jotunnslayer/Run Alfheim Boss Encounter Smoke";
        const string BossEncounterMenu = "Tools/Jotunnslayer/Run Niflheim Boss Encounter Smoke";
        const string BossMenu = "Tools/Jotunnslayer/Run Niflheim Boss Easy Host Smoke";
        const string GuardianMenu = "Tools/Jotunnslayer/Run Skeleton Guardian Host Smoke";
        const string SpiderMenu = "Tools/Jotunnslayer/Run Spider Regular Host Smoke";
        const ushort SmokeTransportPort = 17878;
        const string Startup = "Assets/Baseline/Scenes/Startup.unity";

        [MenuItem(MenuPath, validate = true)]
        [MenuItem(WaveMenu, validate = true)]
        [MenuItem(MixedWaveMenu, validate = true)]
        [MenuItem(GuardianWaveMenu, validate = true)]
        [MenuItem(BowMenu, validate = true)]
        [MenuItem(SpiderMenu, validate = true)]
        [MenuItem(GuardianMenu, validate = true)]
        [MenuItem(BossMenu, validate = true)]
        [MenuItem(BossEncounterMenu, validate = true)]
        [MenuItem(AlfheimEncounterMenu, validate = true)]
        [MenuItem(AlfheimOpeningMenu, validate = true)]
        [MenuItem(AlfheimCapMenu, validate = true)]
        [MenuItem(AlfheimStampedeMenu, validate = true)]
        [MenuItem(AlfheimRangedCapMenu, validate = true)]
        [MenuItem(GmMenu, validate = true)]
        static bool CanRun() => !EditorApplication.isCompiling &&
            !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(MenuPath)]
        static void Run() => Run(wave: 0);

        [MenuItem(WaveMenu)]
        static void RunWave() => Run(wave: 4);

        [MenuItem(BowMenu)]
        static void RunBow() => Run(wave: 0, kind: Skeleton1HSpawn.Kind.Bow);

        [MenuItem(SpiderMenu)]
        static void RunSpider() => Run(wave: 0, kind: Skeleton1HSpawn.Kind.Spider);

        [MenuItem(MixedWaveMenu)]
        static void RunMixedWave() => Run(wave: 7);

        [MenuItem(GuardianWaveMenu)]
        static void RunGuardianWave() => Run(wave: 8);

        [MenuItem(GuardianMenu)]
        static void RunGuardian() => Run(wave: 0, kind: Skeleton1HSpawn.Kind.Guardian);

        [MenuItem(BossMenu)]
        static void RunBoss() => Run(wave: 0, kind: Skeleton1HSpawn.Kind.NiflheimBossEasy);

        [MenuItem(BossEncounterMenu)]
        static void RunBossEncounter() => Run(wave: 97791);

        [MenuItem(AlfheimRangedCapMenu)]
        static void RunAlfheimRangedCap() => Run(wave: 976808);

        [MenuItem(AlfheimStampedeMenu)]
        static void RunAlfheimStampede() => Run(wave: 976802);

        [MenuItem(AlfheimCapMenu)]
        static void RunAlfheimCap() => Run(wave: 976804);

        [MenuItem(AlfheimOpeningMenu)]
        static void RunAlfheimOpening() => Run(wave: 97680);

        [MenuItem(AlfheimEncounterMenu)]
        static void RunAlfheimEncounter() => Run(wave: 97679);

        [MenuItem(GmMenu)]
        static void RunGmUi() => Run(wave: GmSmokeMode);

        public static void RunAlfheimBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("RunAlfheimBatch requires Unity batch mode.");

            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-jotunnSmoke");
            string test = index < 0 ? "w1" : index + 1 < args.Length ? args[index + 1] : "";
            var (wave, kind) = test switch
            {
                "w1" => (97680, Skeleton1HSpawn.Kind.Melee),
                "w2" => (976802, Skeleton1HSpawn.Kind.Melee),
                "w4" => (976804, Skeleton1HSpawn.Kind.Melee),
                "w8" => (976808, Skeleton1HSpawn.Kind.Melee),
                "stone-waves" => (976862, Skeleton1HSpawn.Kind.Melee),
                "drider-waves" => (976826, Skeleton1HSpawn.Kind.Melee),
                "elf-insane-w40" => (976840, Skeleton1HSpawn.Kind.Melee),
                "drider-endless" => (97669, Skeleton1HSpawn.Kind.Melee),
                "boss" => (97679, Skeleton1HSpawn.Kind.Melee),
                "boss-phase1" => (0, Skeleton1HSpawn.Kind.AlfheimPhase1),
                "boss-phase2" => (0, Skeleton1HSpawn.Kind.AlfheimPhase2),
                "boss-tail-terrain" => (976792, Skeleton1HSpawn.Kind.Melee),
                "drider" => (0, Skeleton1HSpawn.Kind.AlfheimDrider),
                "drider-variant" => (0, Skeleton1HSpawn.Kind.AlfheimDriderVariant),
                "gm" => (GmSmokeMode, Skeleton1HSpawn.Kind.Melee),
                "monster-playtest" => (MonsterPlaytestMode, Skeleton1HSpawn.Kind.Melee),
                "spider" => (0, Skeleton1HSpawn.Kind.AlfheimSpider),
                "spider-veteran" => (0, Skeleton1HSpawn.Kind.AlfheimSpiderVeteran),
                "elf-melee" => (0, Skeleton1HSpawn.Kind.AlfheimElfMelee),
                "elf-insane" => (0, Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane),
                "sphinx-skeleton" => (0, Skeleton1HSpawn.Kind.SphinxSkeleton),
                "sphinx-bow" => (0, Skeleton1HSpawn.Kind.SphinxBow),
                "sphinx-guardian" => (0, Skeleton1HSpawn.Kind.SphinxGuardian),
                "sphinx-miniboss" => (0, Skeleton1HSpawn.Kind.SphinxMiniBoss),
                "milipede" => (0, Skeleton1HSpawn.Kind.Milipede),
                "milipede-insane" => (0, Skeleton1HSpawn.Kind.MilipedeInsane),
                "spider-insane" => (0, Skeleton1HSpawn.Kind.SpiderInsane),
                "horror-jotun" => (0, Skeleton1HSpawn.Kind.HorrorJotun),
                "bat-loot" => (0, Skeleton1HSpawn.Kind.BatLoot),
                "sphinx-miniboss-waves" => (9772915, Skeleton1HSpawn.Kind.Melee),
                "milipede-waves" => (9772902, Skeleton1HSpawn.Kind.Melee),
                "hyb-interior-endless-w1" => (9773401, Skeleton1HSpawn.Kind.Melee),
                "hyb-interior-endless-group" => (9773404, Skeleton1HSpawn.Kind.Melee),
                "sphinx-miniboss-endless" => (9773505, Skeleton1HSpawn.Kind.Melee),
                "hyb-bonus-supported" => (9773512, Skeleton1HSpawn.Kind.Melee),
                "hyb-bonus-lifecycle" => (9773513, Skeleton1HSpawn.Kind.Melee),
                "hyb-bonus-regression" => (9773514, Skeleton1HSpawn.Kind.Melee),
                "hyb-sub-boss" => (0, Skeleton1HSpawn.Kind.HybSubBossEasy),
                "hyb-sub-normal-circles" => (0, Skeleton1HSpawn.Kind.HybSubBossNormal),
                "goat-normal-rotator" => (0, Skeleton1HSpawn.Kind.NiflheimBossNormal),
                "goat-hel-form-horns" => (0, Skeleton1HSpawn.Kind.NiflheimBossHelForm),
                "goat-endless-horns" => (0, Skeleton1HSpawn.Kind.NiflheimBossEndless),
                "goat-hel-form-ring" => (0, Skeleton1HSpawn.Kind.NiflheimBossHelForm),
                "goat-endless-ring" => (0, Skeleton1HSpawn.Kind.NiflheimBossEndless),
                "goat-normal-ring" => (0, Skeleton1HSpawn.Kind.NiflheimBossNormal),
                "goat-normal-freeze" => (0, Skeleton1HSpawn.Kind.NiflheimBossNormal),
                "hyb-main-hard-circles" => (0, Skeleton1HSpawn.Kind.HybBossHard),
                "hyb-main-normal-circles" => (0, Skeleton1HSpawn.Kind.HybBossNormal),
                "hyb-boss-circles" => (0, Skeleton1HSpawn.Kind.HybBossEasy),
                "hyb-boss-teleport" => (977252, Skeleton1HSpawn.Kind.Melee),
                "hyb-sub-normal-summons" => (977254, Skeleton1HSpawn.Kind.Melee),
                "hyb-boss-summons" => (977253, Skeleton1HSpawn.Kind.Melee),
                "hyb-sub-boss-phases" => (977251, Skeleton1HSpawn.Kind.Melee),
                "hyb-interior-w1" => (977301, Skeleton1HSpawn.Kind.Melee),
                "hyb-interior-late" => (977302, Skeleton1HSpawn.Kind.Melee),
                "hyb-interior-insane-bursts" => (977469, Skeleton1HSpawn.Kind.Melee),
                "hyb-interior-group-cap" => (977300, Skeleton1HSpawn.Kind.Melee),
                "elf-ranged" => (0, Skeleton1HSpawn.Kind.AlfheimElfRanged),
                "stone" => (0, Skeleton1HSpawn.Kind.AlfheimStoneJotunn),
                "stone-insane" => (0, Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane),
                "sentinel" => (0, Skeleton1HSpawn.Kind.AlfheimSentinel),
                "sentinel-veteran" => (0, Skeleton1HSpawn.Kind.AlfheimSentinelVeteran),
                _ => throw new ArgumentException($"Unknown Alfheim smoke test '{test}'.")
            };

            SessionState.SetBool(BatchRunKey, true);
            SessionState.SetInt(BatchResultKey, 1);
            Run(wave, kind);
        }

        static void Run(int wave, Skeleton1HSpawn.Kind kind = Skeleton1HSpawn.Kind.Melee)
        {
            var startup = AssetDatabase.LoadAssetAtPath<SceneAsset>(Startup);
            if (!startup)
            {
                Debug.LogError("JOTUNN_SKELETON_HOST_SMOKE_FAILED startup scene missing.");
                if (SessionState.GetBool(BatchRunKey, false)) EditorApplication.Exit(1);
                return;
            }

            if (SessionState.GetBool(BatchRunKey, false)) EditorSceneManager.OpenScene(Startup);
            SessionState.SetBool(MenuPath, true);
            SessionState.SetInt(WaveMenu, wave);
            SessionState.SetInt(MenuPath + "/Kind", (int)kind);
            SessionState.SetString(MenuPath + "/Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = startup;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Listen() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(MenuPath, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var smoke = new GameObject("Skeleton 1H host smoke");
                DontDestroyOnLoad(smoke);
                var runner = smoke.AddComponent<Skeleton1HHostSmoke>();
                runner.waveIndex = SessionState.GetInt(WaveMenu, 0);
                runner.kind = (Skeleton1HSpawn.Kind)SessionState.GetInt(MenuPath + "/Kind", 0);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                string path = SessionState.GetString(MenuPath + "/Previous", "");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                SessionState.EraseBool(MenuPath);
                SessionState.EraseInt(WaveMenu);
                SessionState.EraseInt(MenuPath + "/Kind");
                SessionState.EraseString(MenuPath + "/Previous");
            }
        }

        bool startedHost;
        static int runtimeErrorCount;
        static string firstRuntimeError;
        static bool anySmokeFailed;

        void Awake()
        {
            runtimeErrorCount = 0;
            firstRuntimeError = null;
            anySmokeFailed = false;
            Application.logMessageReceived += OnRuntimeLog;
        }

        void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            runtimeErrorCount++;
            firstRuntimeError ??= message;
        }
        int waveIndex;
        Skeleton1HSpawn.Kind kind;
        bool verifyBow => kind is Skeleton1HSpawn.Kind.Bow or Skeleton1HSpawn.Kind.SphinxBow;
        bool verifyElfRanged => kind == Skeleton1HSpawn.Kind.AlfheimElfRanged;
        bool VerifyStomp => kind is Skeleton1HSpawn.Kind.AlfheimStoneJotunn or Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane;
        string ResultPrefix => kind switch
        {
            Skeleton1HSpawn.Kind.Bow => "JOTUNN_SKELETON_BOW_HOST_SMOKE",
            Skeleton1HSpawn.Kind.Spider => "JOTUNN_SPIDER_REGULAR_HOST_SMOKE",
            Skeleton1HSpawn.Kind.SpiderInsane => "JOTUNN_SPIDER_INSANE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimSpider => "JOTUNN_ALFHEIM_SPIDER_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimSpiderVeteran => "JOTUNN_ALFHEIM_SPIDER_VETERAN_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimElfMelee => "JOTUNN_ALFHEIM_ELF_MELEE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane => "JOTUNN_ALFHEIM_ELF_INSANE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.SphinxSkeleton => "JOTUNN_HYB_SPHINX_SKELETON_HOST_SMOKE",
            Skeleton1HSpawn.Kind.SphinxBow => "JOTUNN_HYB_SPHINX_BOW_HOST_SMOKE",
            Skeleton1HSpawn.Kind.SphinxGuardian => "JOTUNN_HYB_SPHINX_GUARDIAN_HOST_SMOKE",
            Skeleton1HSpawn.Kind.SphinxMiniBoss => "JOTUNN_HYB_SPHINX_MINIBOSS_HOST_SMOKE",
            Skeleton1HSpawn.Kind.Milipede => "JOTUNN_MILIPEDE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.MilipedeInsane => "JOTUNN_MILIPEDE_INSANE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.HorrorJotun => "JOTUNN_HORROR_JOTUN_HOST_SMOKE",
            Skeleton1HSpawn.Kind.BatLoot => "JOTUNN_BAT_LOOT_HOST_SMOKE",
            Skeleton1HSpawn.Kind.HybSubBossEasy => "JOTUNN_HYB_SUB_BOSS_HOST_SMOKE",
            Skeleton1HSpawn.Kind.HybBossEasy => "JOTUNN_HYB_BOSS_CIRCLES_HOST_SMOKE",
            Skeleton1HSpawn.Kind.HybBossNormal => "JOTUNN_HYB_MAIN_NORMAL_CIRCLES_HOST_SMOKE",
            Skeleton1HSpawn.Kind.HybBossHard => "JOTUNN_HYB_MAIN_HARD_CIRCLES_HOST_SMOKE",
            Skeleton1HSpawn.Kind.HybSubBossNormal => "JOTUNN_HYB_SUB_NORMAL_CIRCLES_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimElfRanged => "JOTUNN_ALFHEIM_ELF_RANGED_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimStoneJotunn => "JOTUNN_ALFHEIM_STONE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane => "JOTUNN_ALFHEIM_STONE_INSANE_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimSentinel => "JOTUNN_ALFHEIM_SENTINEL_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimSentinelVeteran => "JOTUNN_ALFHEIM_SENTINEL_VETERAN_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimDrider => "JOTUNN_ALFHEIM_DRIDER_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimDriderVariant => "JOTUNN_ALFHEIM_DRIDER_VARIANT_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimPhase1 => "JOTUNN_ALFHEIM_BOSS_PHASE1_HOST_SMOKE",
            Skeleton1HSpawn.Kind.AlfheimPhase2 => "JOTUNN_ALFHEIM_BOSS_PHASE2_HOST_SMOKE",
            Skeleton1HSpawn.Kind.Guardian => "JOTUNN_SKELETON_GUARDIAN_HOST_SMOKE",
            Skeleton1HSpawn.Kind.NiflheimBossEasy => "JOTUNN_NIFLHEIM_BOSS_EASY_HOST_SMOKE",
            _ => "JOTUNN_SKELETON_HOST_SMOKE"
        };

        IEnumerator Start()
        {
            startedHost = !NetworkServer.active && !NetworkClient.active;
            if (startedHost)
            {
                RoomCreator creator = null;
                float startupDeadline = Time.realtimeSinceStartup + 5f;
                while (!creator && Time.realtimeSinceStartup < startupDeadline)
                {
                    creator = FindFirstObjectByType<RoomCreator>();
                    if (!creator) yield return null;
                }

                if (!creator)
                {
                    Fail($"RoomCreator missing in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}.");
                    yield break;
                }

                bool whiteboxOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-monsterWhitebox") >= 0;
                int index = creator.Maps == null ? -1 : Array.FindIndex(creator.Maps,
                    map => map && (whiteboxOnly
                        ? string.Equals(map.Scene, RoomCreator.TrainingScenePath, StringComparison.Ordinal)
                        : RoomCreator.IsTrainingScene(map.Scene)));
                if (index < 0)
                {
                    Fail($"training map missing from RoomCreator map list ({creator.Maps?.Length ?? 0} slots).");
                    yield break;
                }
                var manager = DNNetworkManager.Instance;
                if (!manager)
                {
                    Fail("network manager missing.");
                    yield break;
                }
                manager.SetTransportPort(SmokeTransportPort);
                creator.LaunchFromFrontEnd(index, 0, false);
            }

            float deadline = Time.realtimeSinceStartup + 30f;
            CharacterInstance target = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var player = GameManager.myPlayerInstance;
                if (player && player.MyCharacter && player.MyCharacter.CurrentHealth > 0)
                {
                    target = player.MyCharacter;
                    break;
                }
                yield return null;
            }

            if (!target)
            {
                Fail("local character did not spawn.");
                yield break;
            }

            yield return new WaitForSeconds(2f);
            if (waveIndex == MonsterPlaytestMode)
            {
                yield return VerifyMonsterPlaytest(target);
                Destroy(gameObject);
                yield break;
            }
            if (waveIndex == GmSmokeMode)
            {
                yield return VerifyGmUi(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 97669)
            {
                yield return VerifyAlfheimEndlessDriderWave(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9773404)
            {
                yield return VerifyHyboreaEndlessGroup(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9773401)
            {
                yield return VerifyHyboreaEndlessOpeningWave(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9773505)
            {
                yield return VerifySphinxMiniBossEndless(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9773512)
            {
                yield return VerifyHyboreaBonusSupportedTracks(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9773513)
            {
                yield return VerifyHyboreaBonusTargetLifecycle(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9773514)
            {
                yield return VerifySphinxMiniBossEndless(target);
                if (!anySmokeFailed && runtimeErrorCount == 0)
                    yield return VerifyHyboreaBonusTargetLifecycle(target);
                if (!anySmokeFailed && runtimeErrorCount == 0)
                    yield return VerifyHyboreaEndlessGroup(target);
                ReportAlfheimSmoke("JOTUNN_HYBOREA_BONUS_REGRESSION_SMOKE", !anySmokeFailed,
                    "W5 recurrence, destroyed-target cleanup, formal sequence ownership and shared cap");
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 9772915 || waveIndex == 9772902)
            {
                yield return VerifySphinxMiniBossWaves(target, waveIndex == 9772902);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977254)
            {
                yield return VerifyHybSubNormalSummons(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977253)
            {
                yield return VerifyHyboreaBossSummons(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977252)
            {
                yield return VerifyHyboreaBossTeleport(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977251)
            {
                yield return VerifyHyboreaSubBossPhases(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977300)
            {
                yield return VerifyHyboreaInteriorGroupCap(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977469)
            {
                yield return VerifyHyboreaInteriorBursts(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977302)
            {
                yield return VerifyHyboreaInteriorLateWaves(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 977301)
            {
                yield return VerifyHyboreaInteriorOpeningWave(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 976840)
            {
                yield return VerifyAlfheimInsaneElfWave(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex is 976826 or 976862)
            {
                yield return VerifyAlfheimEliteWaves(target, drider: waveIndex == 976826);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 976808)
            {
                yield return VerifyAlfheimRangedCap(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 976802)
            {
                yield return VerifyAlfheimStampede(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 976804)
            {
                yield return VerifyAlfheimActiveCap(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 97680)
            {
                yield return VerifyAlfheimOpening(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 976792)
            {
                yield return VerifyAlfheimTailTerrain(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 97679)
            {
                yield return VerifyAlfheimEncounter(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex == 97791)
            {
                yield return VerifyBossEncounter(target);
                Destroy(gameObject);
                yield break;
            }

            if (waveIndex != 0)
            {
                yield return VerifyWave(target, waveIndex);
                Destroy(gameObject);
                yield break;
            }

            string configurationId = JotunnMonsterGm.GetConfigurationId(kind);
            var actor = JotunnMonsterGm.TrySpawn(target, configurationId);
            uint spawnedId = actor ? actor.netId : 0u;
            int expectedHealth = kind switch
            {
                Skeleton1HSpawn.Kind.HorrorJotun => HorrorJotunCombat.LocalBaseHealth,
                Skeleton1HSpawn.Kind.BatLoot => BatLootFleeing.LocalBaseHealth,
                Skeleton1HSpawn.Kind.Spider or Skeleton1HSpawn.Kind.AlfheimSpider => 14,
                Skeleton1HSpawn.Kind.Guardian or Skeleton1HSpawn.Kind.SphinxGuardian or Skeleton1HSpawn.Kind.AlfheimSpiderVeteran or
                    Skeleton1HSpawn.Kind.AlfheimStoneJotunn or Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane or
                    Skeleton1HSpawn.Kind.AlfheimSentinelVeteran or Skeleton1HSpawn.Kind.Milipede or
                    Skeleton1HSpawn.Kind.MilipedeInsane => 140,
                Skeleton1HSpawn.Kind.AlfheimDrider or Skeleton1HSpawn.Kind.AlfheimDriderVariant => 1100,
                Skeleton1HSpawn.Kind.HybSubBossEasy or Skeleton1HSpawn.Kind.HybSubBossNormal or Skeleton1HSpawn.Kind.SphinxMiniBoss or Skeleton1HSpawn.Kind.NiflheimBossEndless or Skeleton1HSpawn.Kind.NiflheimBossHelForm => 1100,
                Skeleton1HSpawn.Kind.NiflheimBossEasy or Skeleton1HSpawn.Kind.NiflheimBossNormal or Skeleton1HSpawn.Kind.AlfheimPhase1 or
                    Skeleton1HSpawn.Kind.AlfheimPhase2 or Skeleton1HSpawn.Kind.HybBossEasy or Skeleton1HSpawn.Kind.HybBossNormal or Skeleton1HSpawn.Kind.HybBossHard => 4000,
                _ => Skeleton1HCombat.BaseHealth
            };
            if (!actor || actor.MaxHealth != expectedHealth)
            {
                if (kind == Skeleton1HSpawn.Kind.SphinxGuardian)
                    LogSphinxGuardianAssets();
                if (kind == Skeleton1HSpawn.Kind.SphinxMiniBoss)
                    LogSphinxMiniBossAssets();
                JotunnMonsterGm.Clear(target);
                Fail($"GM spawn or base health check failed for {configurationId}. " +
                    $"Spawned={actor != null}, health={(actor ? actor.MaxHealth : 0)}, expected={expectedHealth}.");
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.SpiderInsane)
            {
                yield return VerifyInsaneSpider(actor, target);
                Destroy(gameObject);
                yield break;
            }
            JotunnMonsterGm.SetCombat(target, true);
            if (kind is Skeleton1HSpawn.Kind.NiflheimBossEndless or Skeleton1HSpawn.Kind.NiflheimBossHelForm)
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "goat-hel-form-horns") >= 0 ||
                    Array.IndexOf(Environment.GetCommandLineArgs(), "goat-endless-horns") >= 0)
                    yield return VerifyGoatHorns(actor, target);
                else yield return VerifyGoatRing(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.NiflheimBossNormal)
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "goat-normal-rotator") >= 0)
                    yield return VerifyGoatRotator(actor, target);
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "goat-normal-ring") >= 0)
                    yield return VerifyGoatRing(actor, target);
                else yield return VerifyGoatFreeze(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.NiflheimBossEasy)
            {
                yield return VerifyBoss(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind is Skeleton1HSpawn.Kind.AlfheimPhase1 or Skeleton1HSpawn.Kind.AlfheimPhase2)
            {
                yield return VerifyAlfheimBossCombat(actor, target, kind == Skeleton1HSpawn.Kind.AlfheimPhase1);
                Destroy(gameObject);
                yield break;
            }

            if (kind is Skeleton1HSpawn.Kind.HybBossEasy or Skeleton1HSpawn.Kind.HybBossNormal or Skeleton1HSpawn.Kind.HybBossHard or Skeleton1HSpawn.Kind.HybSubBossNormal)
            {
                bool advanced = kind is Skeleton1HSpawn.Kind.HybBossNormal or Skeleton1HSpawn.Kind.HybBossHard;
                if (advanced) actor.GetComponent<Skeleton1HCombat>().SetHyboreaBossSkillSet(3);
                int circles = kind == Skeleton1HSpawn.Kind.HybBossHard ? 6 : advanced ? 5 : 3;
                yield return VerifyHybBossCircles(actor, target, circles);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.HybSubBossEasy)
            {
                yield return VerifyHybSummon(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.SphinxMiniBoss)
            {
                yield return VerifySphinxMiniBoss(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.HorrorJotun)
            {
                yield return VerifyHorrorJotun(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.BatLoot)
            {
                yield return VerifyBatLoot(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind is Skeleton1HSpawn.Kind.Milipede or Skeleton1HSpawn.Kind.MilipedeInsane)
            {
                yield return VerifyMilipede(actor, target);
                Destroy(gameObject);
                yield break;
            }

            if (kind == Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane)
            {
                yield return VerifyAlfheimElfSword(actor, target);
                Destroy(gameObject);
                yield break;
            }

            Vector3 spawnPosition = actor.transform.position;
            bool verifySphinxGuardian = kind == Skeleton1HSpawn.Kind.SphinxGuardian;
            bool verifyGuardian = verifySphinxGuardian || kind == Skeleton1HSpawn.Kind.Guardian;
            var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
            bool hit = false;
            bool firstSwingHit = false;
            bool alternateSwingHit = false;
            float lastGuardianHitAt = float.NegativeInfinity;
            Skeleton1HVisual.Motion lastGuardianMotion = default;
            float arrowSeenAt = -1f;
            float arrowFlight = 0f;
            bool branchesObserved = false;
            bool stompWarningObserved = false;
            bool stompDamageObserved = false;
            bool verifyDriderVariant = kind == Skeleton1HSpawn.Kind.AlfheimDriderVariant;
            bool verifyDrider = verifyDriderVariant || kind == Skeleton1HSpawn.Kind.AlfheimDrider;
            bool driderWebObserved = false;
            bool driderOrbObserved = false;
            bool driderAreasObserved = false;
            bool DriderActionsObserved() =>
                driderWebObserved && driderOrbObserved && (!verifyDriderVariant || driderAreasObserved);
            void OnDamageDealt(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim != target || damage <= 0) return;
                hit = true;
                if (VerifyStomp) stompDamageObserved |= attack == AttackType.explosion && stompWarningObserved;
                if ((verifyBow || verifyElfRanged) && arrowSeenAt >= 0f) arrowFlight = Time.time - arrowSeenAt;
                if (!verifyGuardian || !visual) return;
                var motion = visual.CurrentMotion;
                bool qualifyingHit = !verifySphinxGuardian ||
                    (motion == lastGuardianMotion && Time.time - lastGuardianHitAt < .1f);
                firstSwingHit |= qualifyingHit && motion == Skeleton1HVisual.Motion.Attack;
                alternateSwingHit |= qualifyingHit && motion == Skeleton1HVisual.Motion.AttackAlternate;
                lastGuardianMotion = motion;
                lastGuardianHitAt = Time.time;
            }

            actor.Client_OnDamageDealt += OnDamageDealt;
            deadline = Time.realtimeSinceStartup + (verifyDrider ? 40f : 12f);
            while (actor && target && (!hit || (verifyGuardian && !alternateSwingHit) ||
                   (verifyElfRanged && !branchesObserved) || (verifyDrider && !DriderActionsObserved())) &&
                   Time.realtimeSinceStartup < deadline)
            {
                if (verifyBow && arrowSeenAt < 0f && FindFirstObjectByType<SkeletonBowArrow>())
                    arrowSeenAt = Time.time;
                if (VerifyStomp && !stompWarningObserved)
                {
                    var warning = actor.transform.Find("Enemy ground warning");
                    stompWarningObserved = warning && warning.TryGetComponent<LineRenderer>(out var ring) && ring.enabled;
                }
                if (verifyDrider)
                {
                    driderWebObserved |= visual && visual.CurrentMotion == Skeleton1HVisual.Motion.BossHorns;
                    if (!driderOrbObserved)
                    {
                        foreach (var projectile in FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None))
                            driderOrbObserved |= projectile.name == "Drider orb";
                    }
                    if (verifyDriderVariant && !driderAreasObserved)
                    {
                        int activeWarnings = 0;
                        foreach (var ring in actor.GetComponentsInChildren<LineRenderer>())
                            if (ring.enabled && ring.name == "Enemy ground warning") activeWarnings++;
                        driderAreasObserved = activeWarnings == 3;
                    }
                }
                if (verifyElfRanged)
                {
                    int branches = 0;
                    foreach (var projectile in FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None))
                    {
                        if (projectile.name == "Elf chain shot" && arrowSeenAt < 0f) arrowSeenAt = Time.time;
                        else if (projectile.name == "Elf chain branch") branches++;
                    }
                    branchesObserved |= branches == 2;
                }
                yield return null;
            }

            if (actor) actor.Client_OnDamageDealt -= OnDamageDealt;
            bool moved = actor && Vector3.Distance(actor.transform.position, spawnPosition) > 1f;
            bool actionObserved = verifyBow ? arrowFlight > .2f : moved;
            if (verifyElfRanged) actionObserved = arrowFlight > .2f && branchesObserved;
            if (VerifyStomp) actionObserved = stompDamageObserved;
            if (verifyDrider)
            {
                actionObserved = DriderActionsObserved();
                Debug.Log($"{ResultPrefix}: web={driderWebObserved}, orb={driderOrbObserved}, areas={driderAreasObserved}, damage={hit}");
            }
            if (verifyGuardian) actionObserved = moved && firstSwingHit && alternateSwingHit;
            JotunnMonsterGm.Clear(target);
            yield return null;
            bool cleanedUp = !actor && spawnedId != 0u && !NetworkServer.spawned.ContainsKey(spawnedId);
            if (!actionObserved || !hit || !cleanedUp)
                Fail($"actionObserved={actionObserved}, damage={hit}, cleanup={cleanedUp}, " +
                    $"arrowFlight={arrowFlight:F2}, branches={branchesObserved}, stompWarning={stompWarningObserved}");
            else
            {
                ReportAlfheimSmoke(ResultPrefix, true, "");
                Destroy(gameObject);
            }


        }

        static IEnumerator VerifyHyboreaEndlessGroup(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_ENDLESS_GROUP_SMOKE";
            var root = new GameObject("Hyborea endless group smoke");
            var sequence = root.AddComponent<HyboreaInteriorEncounterSequence>();
            HyboreaEndlessInteriorOpeningWave wave = null;
            var conflictRoot = new GameObject("Hyborea endless bonus ownership smoke");
            var occupiedBonus = conflictRoot.AddComponent<HyboreaInteriorMiniBossWaves>();
            var conflictSequence = conflictRoot.AddComponent<HyboreaInteriorEncounterSequence>();
            bool rollback = occupiedBonus.TryStartEndless(target, Time.time + 1000f) &&
                !conflictSequence.TryStartEndlessCore(target, Time.time) && occupiedBonus.enabled &&
                !occupiedBonus.TryStartEndless(target, Time.time);
            conflictSequence.StopCore();
            occupiedBonus.StopWaves();
            Destroy(conflictRoot);
            var sharedConflictRoot = new GameObject("Hyborea shared bonus ownership smoke");
            var occupiedSharedBonus = sharedConflictRoot.AddComponent<HyboreaEndlessBonusWaves>();
            var sharedConflictSequence = sharedConflictRoot.AddComponent<HyboreaInteriorEncounterSequence>();
            rollback &= occupiedSharedBonus.TryStart(target, Time.time + 1000f) &&
                !sharedConflictSequence.TryStartEndlessCore(target, Time.time) && occupiedSharedBonus.Running;
            sharedConflictSequence.StopCore();
            rollback &= occupiedSharedBonus.Running && !sharedConflictSequence.TryStartCore(target, Time.time);
            occupiedSharedBonus.StopWaves();
            Destroy(sharedConflictRoot);
            var floor = new GameObject("Hyborea circular test ground");
            Vector3 previousPosition = target.transform.position;
            Vector3 center = previousPosition + Vector3.up * 80f;
            floor.transform.position = center - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(100f, 1f, 100f);
            bool previousInvulnerable = target.RangeInvulnerable;
            float previousScale = Time.timeScale;
            bool passed = false;
            string detail = "";
            var ids = new HashSet<uint>();
            try
            {
                target.RangeInvulnerable = true;
                SetSmokePlayerPosition(target, center + Vector3.up * .15f);
                Physics.SyncTransforms();
                Time.timeScale = 5f;
                bool began = sequence.TryStartEndlessCore(target, Time.time - 180f);
                wave = root.GetComponent<HyboreaEndlessInteriorOpeningWave>();
                var bonus = root.GetComponent<HyboreaEndlessBonusWaves>();
                bool duplicate = !sequence.TryStartEndlessCore(target, Time.time) &&
                    !sequence.TryStartCore(target, Time.time);
                float deadline = Time.realtimeSinceStartup + 60f;
                while (wave.ActiveCount < 150 && Time.realtimeSinceStartup < deadline) yield return null;
                while (bonus && (bonus.SpawnedByTrack[1] < 3 || bonus.SpawnedByTrack[2] < 1 ||
                    bonus.SpawnedByTrack[5] < 1) && Time.realtimeSinceStartup < deadline) yield return null;
                var miniBoss = bonus ? bonus.FirstLivingActor(5) : null;
                var bat = bonus ? bonus.FirstLivingActor(2) : null;
                bool bonusSpawned = bonus && bonus.SpawnedByTrack[1] >= 3 &&
                    miniBoss && miniBoss.MaxHealth == 1100 && bat && bat.MaxHealth == 140 && bonus.EmptyWaveStarts >= 2;
                bool capped = wave.ActiveCount == 150 && wave.SpawnedByTrack[1] > 0 &&
                    wave.SpawnedByTrack[2] > 0 && wave.SpawnedByTrack[3] > 0;
                int spawned = wave.SpawnedCount;
                yield return new WaitForSeconds(2f);
                capped &= wave.ActiveCount == 150 && wave.SpawnedCount == spawned;
                var released = wave.FirstLivingActor;
                if (released) NetworkServer.Destroy(released.gameObject);
                deadline = Time.realtimeSinceStartup + 8f;
                while (wave.SpawnedCount == spawned && Time.realtimeSinceStartup < deadline) yield return null;
                passed = began && duplicate && rollback && bonusSpawned && capped && wave.SpawnedCount == spawned + 1 && wave.ActiveCount == 150;
                detail = $"began={began}, rollback={rollback}, bonus={bonusSpawned}, capped={capped}, tracks={string.Join(",", wave.SpawnedByTrack)}, active={wave.ActiveCount}, spawned={wave.SpawnedCount}";
                foreach (var combat in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
                    ids.Add(combat.GetComponent<CharacterInstance>().netId);
                if (bonus)
                    for (int i = 0; i < bonus.ActorCount; i++)
                    {
                        var actor = bonus.GetActor(i);
                        if (actor) ids.Add(actor.netId);
                    }
            }
            finally
            {
                sequence.StopCore();
                Destroy(root);
                Destroy(floor);
                Time.timeScale = previousScale;
                if (target)
                {
                    target.RangeInvulnerable = previousInvulnerable;
                    SetSmokePlayerPosition(target, previousPosition);
                }
            }
            yield return null;
            passed &= !ids.Overlaps(NetworkServer.spawned.Keys);
            ReportAlfheimSmoke(marker, passed, detail);
        }

        static IEnumerator VerifyHyboreaEndlessOpeningWave(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_ENDLESS_W1_SMOKE";
            var root = new GameObject("Hyborea endless W1 smoke");
            var wave = root.AddComponent<HyboreaEndlessInteriorOpeningWave>();
            bool gates = HyboreaEndlessInteriorOpeningWave.GetSlotWeight(1, 2, 0) == 0 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(1, 3, 0) == 10 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(1, 13, 0) == 0 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(2, 4, 0) == 0 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(2, 5, 0) == 30 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(3, 9, 0) == 0 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(3, 10, 9) == 4 &&
                HyboreaEndlessInteriorOpeningWave.GetSlotWeight(3, 10, 10) == 0;
            bool began = wave.TryStart(target, Time.time - 2f);
            bool duplicateRejected = !wave.TryStart(target, Time.time);
            yield return new WaitForSeconds(.4f);
            bool waited = wave.SpawnedCount == 0;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (began && wave.SpawnedCount < 8 && Time.realtimeSinceStartup < deadline)
                yield return null;
            var ids = new HashSet<uint>();
            foreach (var combat in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
            {
                var visual = combat.GetComponentInChildren<Skeleton1HVisual>();
                if (visual && visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal))
                    ids.Add(combat.GetComponent<CharacterInstance>().netId);
            }
            bool sampled = wave.SpawnedCount == 8 && ids.Count == 8;
            wave.StopWave();
            Destroy(root);
            yield return null;
            bool cleaned = !ids.Overlaps(NetworkServer.spawned.Keys);
            ReportAlfheimSmoke(marker, began && duplicateRejected && gates && waited && sampled && cleaned,
                $"gates={gates}, waited={waited}, sampled={sampled}, cleanup={cleaned}");
        }

        static IEnumerator VerifyHyboreaBonusTargetLifecycle(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_BONUS_TARGET_LIFECYCLE_SMOKE";
            var root = new GameObject("Hyborea bonus target lifecycle smoke");
            var waves = root.AddComponent<HyboreaEndlessBonusWaves>();
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            CharacterInstance proxy = null;
            var ids = new HashSet<uint>();
            bool passed = false;
            try
            {
                SetSmokePlayerPosition(target, center + Vector3.up * .15f);
                Physics.SyncTransforms();
                proxy = Skeleton1HSpawn.TrySpawn(target, center + Vector3.up * .15f,
                    Quaternion.identity, Skeleton1HSpawn.Kind.BatLoot);
                if (proxy)
                {
                    proxy.RangeInvulnerable = true;
                    proxy.GetComponent<BatLootFleeing>().enabled = false;
                    bool began = waves.TryStart(proxy, Time.time - 45f);
                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (waves.ActorCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                    for (int i = 0; i < waves.ActorCount; i++)
                    {
                        var actor = waves.GetActor(i);
                        if (actor) ids.Add(actor.netId);
                    }
                    bool spawned = ids.Count > 0;
                    NetworkServer.Destroy(proxy.gameObject);
                    proxy = null;
                    yield return null;
                    yield return null;
                    bool cleared = waves.ActorCount == 0 && waves.PendingCount == 0 &&
                        !ids.Overlaps(NetworkServer.spawned.Keys);
                    yield return new WaitForSeconds(5f);
                    bool stayedStopped = waves.ActorCount == 0 && waves.SpawnedByTrack[1] == 0;
                    bool restarted = waves.TryStart(target, Time.time);
                    passed = began && spawned && cleared && stayedStopped && restarted;
                }
            }
            finally
            {
                waves.StopWaves();
                if (proxy) NetworkServer.Destroy(proxy.gameObject);
                Destroy(root);
                Destroy(floor);
                if (target) SetSmokePlayerPosition(target, original);
            }
            yield return null;
            ReportAlfheimSmoke(marker, passed && !ids.Overlaps(NetworkServer.spawned.Keys),
                $"targetDestroyedCleanupAndRestart={passed}, trackedActors={ids.Count}");
        }

        static IEnumerator VerifyHyboreaBonusSupportedTracks(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_BONUS_SUPPORTED_TRACKS_SMOKE";
            var root = new GameObject("Hyborea bonus supported tracks smoke");
            var waves = root.AddComponent<HyboreaEndlessBonusWaves>();
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            float oldScale = Time.timeScale;
            bool oldInvulnerable = target.RangeInvulnerable;
            var ids = new HashSet<uint>();
            bool passed = false;
            string details = "";
            try
            {
                target.RangeInvulnerable = true;
                SetSmokePlayerPosition(target, center + Vector3.up * .15f);
                Physics.SyncTransforms();
                float startedAt = Time.time - 44f;
                bool began = waves.TryStart(target, startedAt) && !waves.TryStart(target, startedAt);
                yield return new WaitForSeconds(.25f);
                bool waited = waves.ActorCount == 0;
                Time.timeScale = 5f;
                bool initial = true;
                bool heldCap = false;
                bool refilled = false;
                bool batWaited = true;
                float deadline = Time.realtimeSinceStartup + 85f;
                while (Time.time - startedAt < 276f && Time.realtimeSinceStartup < deadline)
                {
                    float elapsed = Time.time - startedAt;
                    for (int i = 0; i < waves.ActorCount; i++)
                    {
                        var actor = waves.GetActor(i);
                        if (!actor || !ids.Add(actor.netId)) continue;
                        int track = waves.GetActorTrack(i);
                        int expectedHealth = track == 1 ? 54 : track == 2 ? 140 : 1100;
                        initial &= actor.MaxHealth == expectedHealth;
                        if (actor.TryGetComponent<HorrorJotunCombat>(out var horror)) horror.enabled = false;
                        if (actor.TryGetComponent<BatLootFleeing>(out var bat)) bat.enabled = false;
                        if (actor.TryGetComponent<Skeleton1HCombat>(out var combat)) combat.enabled = false;
                    }
                    if (elapsed < 53f) initial &= waves.SpawnedByTrack[1] <= (elapsed < 49f ? 1 : 2);
                    if (elapsed > 65f && elapsed < 80f) initial &= waves.SpawnedByTrack[1] == 3;
                    if (elapsed < 90f) initial &= waves.SpawnedByTrack[2] == 0;
                    if (elapsed > 110f && elapsed < 260f) batWaited &= waves.SpawnedByTrack[2] == 1;
                    if (elapsed > 240f && !heldCap)
                    {
                        heldCap = waves.SpawnedByTrack[1] == 6 && waves.PendingCount > 0;
                        if (heldCap)
                        {
                            for (int i = 0; i < waves.ActorCount; i++)
                            {
                                var actor = waves.GetActor(i);
                                if (actor && waves.GetActorTrack(i) == 1 && waves.GetActorRound(i) == 2)
                                {
                                    NetworkServer.Destroy(actor.gameObject);
                                    break;
                                }
                            }
                        }
                    }
                    if (heldCap && waves.SpawnedByTrack[1] == 7) refilled = true;
                    yield return null;
                }
                passed = began && waited && initial && heldCap && refilled && batWaited &&
                    waves.SpawnedByTrack[1] == 7 && waves.SpawnedByTrack[2] == 2 &&
                    waves.SpawnedByTrack[5] == 1 && waves.EmptyWaveStarts == 2;
                details = $"began={began}, waited={waited}, initial={initial}, cap={heldCap}, refill={refilled}, " +
                    $"batSharedClock={batWaited}, tracks={string.Join(",", waves.SpawnedByTrack)}, emptyWaveStarts={waves.EmptyWaveStarts}";
                waves.StopWaves();
                waves.StopWaves();
                bool reset = waves.ActorCount == 0 && waves.PendingCount == 0 &&
                    waves.EmptyWaveStarts == 0 && waves.SpawnedByTrack[1] == 0 &&
                    waves.TryStart(target, Time.time);
                passed &= reset;
                details += $", restart={reset}";
            }
            finally
            {
                waves.StopWaves();
                Destroy(root);
                Destroy(floor);
                Time.timeScale = oldScale;
                if (target)
                {
                    target.RangeInvulnerable = oldInvulnerable;
                    SetSmokePlayerPosition(target, original);
                }
            }
            yield return null;
            bool cleaned = ids.Count > 0 && !ids.Overlaps(NetworkServer.spawned.Keys);
            ReportAlfheimSmoke(marker, passed && cleaned, details + $", cleanup={cleaned}");
        }

        static IEnumerator VerifySphinxMiniBossEndless(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_SPHINX_MINIBOSS_ENDLESS_SMOKE";
            var root = new GameObject("Hyborea endless miniboss wave smoke");
            var waves = root.AddComponent<HyboreaInteriorMiniBossWaves>();
            float matchStartedAt = Time.time - 178f;
            float previousScale = Time.timeScale;
            bool priorInvulnerability = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            var ids = new HashSet<uint>();
            bool passed = waves.TryStartEndless(target, matchStartedAt) &&
                !waves.TryStartEndless(target, matchStartedAt);
            try
            {
                yield return new WaitForSeconds(.5f);
                passed &= waves.EndlessRound == 0 && waves.SpawnedCount == 0;
                Time.timeScale = 5f;
                int[] gates = { 180, 219, 258, 297 };
                for (int i = 0; passed && i < gates.Length; i++)
                {
                    int expectedTotal = i * 2 + 1;
                    float deadline = Time.realtimeSinceStartup + 20f;
                    while (waves.SpawnedCount < expectedTotal && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    for (int actorIndex = ids.Count; actorIndex < waves.SpawnedCount; actorIndex++)
                    {
                        var actor = waves.GetActor(actorIndex);
                        var visual = actor ? actor.GetComponentInChildren<Skeleton1HVisual>() : null;
                        if (visual && actor.MaxHealth == 1100 &&
                            visual.name.StartsWith("skeleton_mb_sphinx_sur", StringComparison.Ordinal))
                            ids.Add(actor.netId);
                    }
                    bool roundPassed = waves.EndlessRound == i + 1 && waves.SpawnedCount == expectedTotal &&
                        ids.Count == expectedTotal && Time.time >= matchStartedAt + gates[i];
                    passed &= roundPassed;
                    Debug.Log($"{marker}: round={i + 1}, expectedTotal={expectedTotal}, matched={ids.Count}, passed={roundPassed}");
                }
            }
            finally
            {
                waves.StopWaves();
                Destroy(root);
                if (target) target.RangeInvulnerable = priorInvulnerability;
                Time.timeScale = previousScale;
            }
            yield return null;
            bool cleaned = ids.Count == 7 && !ids.Overlaps(NetworkServer.spawned.Keys);
            ReportAlfheimSmoke(marker, passed && cleaned, $"rounds={passed}, cleanup={cleaned}");
        }

        static IEnumerator VerifySphinxMiniBossWaves(CharacterInstance target, bool includeMilipedes)
        {
            string marker = includeMilipedes ? "JOTUNN_HYBOREA_MILIPEDE_WAVES_SMOKE"
                : "JOTUNN_HYBOREA_SPHINX_MINIBOSS_WAVES_SMOKE";
            var root = new GameObject("Hyborea miniboss wave smoke");
            var waves = root.AddComponent<HyboreaInteriorMiniBossWaves>();
            bool priorInvulnerability = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            bool passed = true;
            foreach (var (difficulty, miniBossGate, miniBossCount, milipedeGate, milipedeCount) in new[]
            {
                (HyboreaInteriorDifficulty.Easy, 600f, 1, 870f, 7),
                (HyboreaInteriorDifficulty.Normal, 600f, 1, 870f, 13),
                (HyboreaInteriorDifficulty.Hard, 950f, 3, 980f, 16),
                (HyboreaInteriorDifficulty.Insane, 1123f, 5, 1140f, 20)
            })
            {
                float lastGate = includeMilipedes ? milipedeGate : miniBossGate;
                int expected = miniBossCount + (includeMilipedes ? milipedeCount : 0);
                float gate = Time.time + 1.5f;
                bool began = waves.TryStart(target, gate - lastGate, difficulty, includeMilipedes);
                bool duplicateRejected = !waves.TryStart(target, gate - lastGate, difficulty, includeMilipedes);
                yield return new WaitForSeconds(.5f);
                bool waited = waves.SpawnedCount < expected && Time.time < gate;
                float deadline = Time.realtimeSinceStartup + 18f;
                while (began && waves.SpawnedCount < expected && Time.realtimeSinceStartup < deadline)
                    yield return null;
                var ids = new HashSet<uint>();
                int miniBosses = 0;
                int milipedes = 0;
                for (int i = 0; i < waves.SpawnedCount; i++)
                {
                    var actor = waves.GetActor(i);
                    var visual = actor ? actor.GetComponentInChildren<Skeleton1HVisual>() : null;
                    if (visual && visual.name.StartsWith("skeleton_mb_sphinx_sur", StringComparison.Ordinal))
                    {
                        miniBosses++;
                        ids.Add(actor.netId);
                    }
                    if (visual && visual.name.StartsWith("milipede_sur", StringComparison.Ordinal))
                    {
                        milipedes++;
                        ids.Add(actor.netId);
                    }
                }
                if (waves.SpawnedCount > 0 && waves.GetActor(0))
                    NetworkServer.Destroy(waves.GetActor(0).gameObject);
                yield return new WaitForSeconds(.65f);
                bool countMatched = waves.SpawnedCount == expected && ids.Count == expected &&
                    miniBosses == miniBossCount && milipedes == (includeMilipedes ? milipedeCount : 0);
                waves.StopWaves();
                yield return null;
                bool cleaned = !ids.Overlaps(NetworkServer.spawned.Keys);
                Debug.Log($"{marker}: difficulty={difficulty}, waited={waited}, minibosses={miniBosses}, milipedes={milipedes}, " +
                    $"duplicateRejected={duplicateRejected}, cleanup={cleaned}");
                passed &= began && duplicateRejected && waited && countMatched && cleaned;
            }
            target.RangeInvulnerable = priorInvulnerability;
            Destroy(root);
            ReportAlfheimSmoke(marker, passed, "Check finite difficulty gates, actor counts and cleanup.");
        }

        IEnumerator VerifyBatLoot(CharacterInstance actor, CharacterInstance target)
        {
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            float priorScale = Time.timeScale;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            Physics.SyncTransforms();
            var behavior = actor.GetComponent<BatLootFleeing>();
            uint id = actor.netId;
            bool despawnReward = false;
            void OnExpiredKill(Health victim) { if (victim == actor) despawnReward = true; }
            target.Server_KilledCharacter += OnExpiredKill;
            CharacterInstance hitActor = null;
            bool idle = false, fled = false, lifetime = false, hitWoke = false, disabled = false, killed = false;
            int playerHealth = target.CurrentHealth;
            try
            {
                SetSmokePlayerPosition(target, center + Vector3.up * .15f);
                SetSmokePlayerPosition(actor, center + new Vector3(0f, .15f, 6f));
                Vector3 initial = actor.transform.position;
                Time.timeScale = 5f;
                yield return new WaitForSeconds(46f);
                idle = actor && !behavior.FleeingStarted &&
                    new Vector2(actor.transform.position.x - initial.x, actor.transform.position.z - initial.z).sqrMagnitude < .1f;
                if (idle)
                {
                    SetSmokePlayerPosition(target, actor.transform.position - Vector3.forward * 2f);
                    float deadline = Time.realtimeSinceStartup + 7f;
                    while (actor && Time.realtimeSinceStartup < deadline)
                    {
                        if (behavior.FleeingStarted && (actor.transform.position - target.transform.position).sqrMagnitude > 25f)
                        { fled = true; break; }
                        yield return null;
                    }
                    float expiry = behavior ? behavior.ExpiresAt : 0f;
                    bool early = false;
                    deadline = Time.realtimeSinceStartup + 12f;
                    while (actor && Time.realtimeSinceStartup < deadline)
                    {
                        early |= Time.time < expiry && !NetworkServer.spawned.ContainsKey(id);
                        yield return null;
                    }
                    lifetime = fled && !early && !actor && !NetworkServer.spawned.ContainsKey(id) &&
                        Time.time >= expiry && !despawnReward;
                }
                if (lifetime)
                {
                    Debug.Log("JOTUNN_BAT_LOOT_PROXIMITY_LIFETIME_CHECKED");
                    Time.timeScale = priorScale;
                    SetSmokePlayerPosition(target, center + Vector3.up * .15f);
                    hitActor = Skeleton1HSpawn.TrySpawn(target, center + new Vector3(0f, .15f, 6f),
                        Quaternion.identity, Skeleton1HSpawn.Kind.BatLoot);
                    if (hitActor)
                    {
                        var presentation = hitActor.GetComponentInChildren<Skeleton1HVisual>();
                        yield return new WaitForSeconds(presentation.SpawnDuration + .2f);
                        var hitBehavior = hitActor.GetComponent<BatLootFleeing>();
                        bool sleeping = !hitBehavior.FleeingStarted;
                        hitActor.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, target, 0);
                        float deadline = Time.realtimeSinceStartup + 3f;
                        while (hitActor && !hitBehavior.FleeingStarted && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        hitWoke = sleeping && hitActor && hitActor.CurrentHealth == BatLootFleeing.LocalBaseHealth - 1 &&
                            hitBehavior.FleeingStarted && hitBehavior.ExpiresAt - Time.time > 43f;
                        if (hitWoke)
                        {
                            Debug.Log("JOTUNN_BAT_LOOT_HIT_WAKE_CHECKED");
                            hitBehavior.enabled = false;
                            Vector3 stopped = hitActor.transform.position;
                            yield return new WaitForSeconds(1f);
                            disabled = hitActor && new Vector2(hitActor.transform.position.x - stopped.x,
                                hitActor.transform.position.z - stopped.z).sqrMagnitude < .1f;
                            void OnKilled(Health victim) { if (victim == hitActor) killed = true; }
                            target.Server_KilledCharacter += OnKilled;
                            try { hitActor.Server_ChangeHealthStateRaw(100000, CharacterPart.body, AttackType.hitscan, target, 0); }
                            finally { target.Server_KilledCharacter -= OnKilled; }
                        }
                    }
                }
            }
            finally
            {
                target.Server_KilledCharacter -= OnExpiredKill;
                Time.timeScale = priorScale;
                if (hitActor) NetworkServer.Destroy(hitActor.gameObject);
                JotunnMonsterGm.Clear(target);
                SetSmokePlayerPosition(target, original);
                Destroy(floor);
            }
            bool noAttack = target.CurrentHealth == playerHealth;
            ReportAlfheimSmoke(ResultPrefix, idle && fled && lifetime && hitWoke && disabled && killed && noAttack,
                $"idlePastBirth45={idle}, fled={fled}, lifetime={lifetime}, hitWake={hitWoke}, disabled={disabled}, kill={killed}, noAttack={noAttack}");
        }

        IEnumerator VerifyHorrorJotun(CharacterInstance actor, CharacterInstance target)
        {
            uint id = actor.netId;
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            SetSmokePlayerPosition(actor, center + new Vector3(0f, .15f, 6f));
            Vector3 initial = actor.transform.position;
            int before = target.CurrentHealth;
            bool hit = false;
            int blastHits = 0;
            bool awardedSelfDestructKill = false;
            float castObservedAt = -1f;
            float blastObservedAt = -1f;
            var attackVisual = actor.GetComponentInChildren<Skeleton1HVisual>();
            void OnKilled(Health victim) { if (victim == actor) awardedSelfDestructKill = true; }
            target.Server_KilledCharacter += OnKilled;
            void OnDamage(int health, int amount, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim != target || amount <= 0 || attack != AttackType.explosion) return;
                hit = true;
                blastHits++;
                blastObservedAt = Time.time;
            }
            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 15f;
            bool chased = false;
            while (actor && Time.realtimeSinceStartup < deadline)
            {
                chased |= (actor.transform.position - initial).sqrMagnitude > 1f;
                if (castObservedAt < 0f && attackVisual.CurrentMotion == Skeleton1HVisual.Motion.Attack)
                    castObservedAt = Time.time;
                yield return null;
            }
            target.Server_KilledCharacter -= OnKilled;
            if (actor) actor.Client_OnDamageDealt -= OnDamage;
            bool removed = !actor && !NetworkServer.spawned.ContainsKey(id);
            bool delayed = castObservedAt >= 0f && blastObservedAt - castObservedAt >= .65f;
            if (!chased || !hit || blastHits != 1 || !delayed || awardedSelfDestructKill || target.CurrentHealth >= before || !removed)
            {
                SetSmokePlayerPosition(target, original);
                Destroy(floor);
                JotunnMonsterGm.Clear(target);
                Fail($"Horror Jotun chase={chased}, hits={blastHits}, delayed={delayed}, selfKillReward={awardedSelfDestructKill}, health={target.CurrentHealth}/{before}, removed={removed}.");
                yield break;
            }
            Debug.Log("JOTUNN_HORROR_JOTUN_BLAST_CHECKED");
            for (int cancellation = 0; cancellation < 3; cancellation++)
            {
                var interrupted = Skeleton1HSpawn.TrySpawn(target, center + new Vector3(0f, .15f, 1.5f),
                    Quaternion.identity, Skeleton1HSpawn.Kind.HorrorJotun);
                if (!interrupted)
                {
                    SetSmokePlayerPosition(target, original);
                    Destroy(floor);
                    Fail("Horror Jotun cancellation actor could not spawn.");
                    yield break;
                }
                var presentation = interrupted.GetComponentInChildren<Skeleton1HVisual>();
                deadline = Time.realtimeSinceStartup + 8f;
                while (interrupted && presentation.CurrentMotion != Skeleton1HVisual.Motion.Attack &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                bool beganCast = interrupted && presentation.CurrentMotion == Skeleton1HVisual.Motion.Attack;
                int healthAtCancellation = target.CurrentHealth;
                if (beganCast)
                {
                    if (cancellation == 0)
                        interrupted.Server_ChangeHealthStateRaw(100000, CharacterPart.body, AttackType.hitscan, target, 0);
                    else if (cancellation == 1)
                        interrupted.GetComponent<HorrorJotunCombat>().enabled = false;
                    else
                        interrupted.BlockCharacter(true);
                }
                yield return new WaitForSeconds(1.2f);
                bool cancelled = beganCast && target.CurrentHealth == healthAtCancellation;
                if (cancellation == 2)
                {
                    bool survived = interrupted && interrupted.CurrentHealth > 0 &&
                        NetworkServer.spawned.ContainsKey(interrupted.netId);
                    cancelled &= survived && presentation.CurrentMotion != Skeleton1HVisual.Motion.Attack;
                    if (cancelled)
                    {
                        uint resumedId = interrupted.netId;
                        int resumedHits = 0;
                        float resumedAt = Time.time;
                        float resumedHitAt = -1f;
                        void OnResumedDamage(int health, int amount, CharacterPart part, AttackType attack, Health victim)
                        {
                            if (victim != target || amount <= 0 || attack != AttackType.explosion) return;
                            resumedHits++;
                            resumedHitAt = Time.time;
                        }
                        interrupted.Client_OnDamageDealt += OnResumedDamage;
                        interrupted.BlockCharacter(false);
                        deadline = Time.realtimeSinceStartup + 8f;
                        while (interrupted && Time.realtimeSinceStartup < deadline) yield return null;
                        if (interrupted) interrupted.Client_OnDamageDealt -= OnResumedDamage;
                        cancelled &= !interrupted && !NetworkServer.spawned.ContainsKey(resumedId) &&
                            resumedHits == 1 && resumedHitAt - resumedAt >= .65f &&
                            target.CurrentHealth < healthAtCancellation;
                        Debug.Log($"JOTUNN_HORROR_JOTUN_CONTROL_RESUME_CHECKED survived={survived} hits={resumedHits} delayed={resumedHitAt - resumedAt:F3} removed={!interrupted}");
                    }
                }
                if (interrupted) NetworkServer.Destroy(interrupted.gameObject);
                if (!cancelled)
                {
                    SetSmokePlayerPosition(target, original);
                    Destroy(floor);
                    Fail($"Horror Jotun cancellation failed path={cancellation}, cast={beganCast}.");
                    yield break;
                }
                Debug.Log($"JOTUNN_HORROR_JOTUN_CANCEL_CHECKED path={cancellation}");
            }
            SetSmokePlayerPosition(target, original);
            Destroy(floor);
            JotunnMonsterGm.Clear(target);
            ReportAlfheimSmoke(ResultPrefix, true,
                "normalBlastOnce=True, killedCancel=True, disabledCancel=True, blockedCancelAndFreshResume=True");
        }

        IEnumerator VerifyInsaneSpider(CharacterInstance actor, CharacterInstance target)
        {
            uint id = actor.netId;
            var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
            var mesh = visual ? visual.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            bool skin = mesh && mesh.sharedMaterial && mesh.sharedMaterial.mainTexture &&
                mesh.sharedMaterial.mainTexture.name == "spiderling_elite_dm";
            Vector3 start = actor.transform.position;
            yield return new WaitForSeconds(.6f);
            bool idle = HorizontalDistanceSquared(start, actor.transform.position) < .0225f;
            bool melee = false, splash = false;
            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim != target || damage <= 0) return;
                melee |= attack == AttackType.melee;
                splash |= attack == AttackType.explosion;
            }
            actor.Client_OnDamageDealt += OnDamage;
            JotunnMonsterGm.SetCombat(target, true);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (actor && target && !melee && Time.realtimeSinceStartup < deadline) yield return null;
            bool moved = actor && HorizontalDistanceSquared(start, actor.transform.position) >= .04f;
            if (!actor || !target || !melee)
            {
                ReportAlfheimSmoke(ResultPrefix, false, $"skin={skin}, idle={idle}, moved={moved}, melee={melee}");
                yield break;
            }

            JotunnMonsterGm.SetCombat(target, false);
            var original = target.transform.position;
            var center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            SetSmokePlayerPosition(actor, center + new Vector3(0f, .15f, 8f));
            actor.GetComponent<CharacterMotor>().enabled = false;
            splash = false;
            JotunnMonsterGm.SetCombat(target, true);
            float scale = Time.timeScale;
            Time.timeScale = 3f;
            bool arc = false, cue = false, cast = false;
            deadline = Time.realtimeSinceStartup + 20f;
            while (actor && target && !splash && Time.realtimeSinceStartup < deadline)
            {
                cast |= visual && visual.CurrentMotion == Skeleton1HVisual.Motion.AttackAlternate;
                cue |= actor.GetComponentInChildren<LineRenderer>() is { enabled: true };
                var projectile = GameObject.Find("Insane spider splash");
                arc |= projectile && projectile.transform.position.y > center.y + 2f;
                yield return null;
            }
            deadline = Time.realtimeSinceStartup + 20f;
            GameObject second = null;
            while (splash && !second && Time.realtimeSinceStartup < deadline)
            {
                second = GameObject.Find("Insane spider splash");
                if (!second) yield return null;
            }
            Time.timeScale = scale;
            bool hadSecondShot = second;
            JotunnMonsterGm.SetCombat(target, false);
            yield return null;
            bool stopped = hadSecondShot && !second && !(actor.GetComponentInChildren<LineRenderer>() is { enabled: true });
            actor.Client_OnDamageDealt -= OnDamage;
            JotunnMonsterGm.Clear(target);
            SetSmokePlayerPosition(target, original);
            Destroy(floor);
            yield return null;
            bool removed = !NetworkServer.spawned.ContainsKey(id);
            ReportAlfheimSmoke(ResultPrefix, skin && idle && moved && melee && cast && cue && arc && splash && stopped && removed,
                $"skin={skin}, idle={idle}, moved={moved}, melee={melee}, cast={cast}, cue={cue}, arc={arc}, splash={splash}, stopped={stopped}, removed={removed}");
        }

        IEnumerator VerifyMilipede(CharacterInstance actor, CharacterInstance target)
        {
            uint id = actor.netId;
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            SetSmokePlayerPosition(actor, center + new Vector3(0f, .15f, 8f));

            bool arced = false;
            bool spitHit = false;
            bool sparksReady = false;
            bool movedToMelee = false;
            bool meleeHit = false;
            bool spitAudioStarted = false;
            bool selectedSpitSound = false;
            var spitAudio = actor.GetComponent<Skeleton1HCombat>().AttackAudio;
            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                meleeHit |= victim == target && damage > 0 && attack == AttackType.melee;
                spitHit |= victim == target && damage > 0 && attack == AttackType.hitscan;
            }
            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 22f;
            while (actor && target && !meleeHit && Time.realtimeSinceStartup < deadline)
            {
                spitAudioStarted |= spitAudio && spitAudio.isPlaying;
                selectedSpitSound |= spitAudio && spitAudio.clip && spitAudio.clip.name == "OriginalSpit";
                if (!arced)
                {
                    foreach (var shot in FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None))
                    {
                        if (shot.name != "Milipede spit" || shot.transform.position.y < actor.CombatAimPoint.y + 3f)
                            continue;
                        arced = true;
                        var sparks = shot.GetComponentInChildren<ParticleSystemRenderer>();
                        sparksReady = sparks && sparks.sharedMaterial && sparks.sharedMaterial.shader.isSupported &&
                            sparks.sharedMaterial.mainTexture && sparks.sharedMaterial.mainTexture.name == "blast_light05new";
                        Vector3 landing = shot.MilipedeLanding;
                        landing.y = target.transform.position.y;
                        SetSmokePlayerPosition(target, landing);
                        break;
                    }
                }
                if (spitHit && !movedToMelee)
                {
                    SetSmokePlayerPosition(target, actor.transform.position - Vector3.forward * 1.1f);
                    movedToMelee = true;
                }
                yield return null;
            }
            if (actor) actor.Client_OnDamageDealt -= OnDamage;
            JotunnMonsterGm.Clear(target);
            SetSmokePlayerPosition(target, original);
            Destroy(floor);
            yield return null;
            bool cleaned = !NetworkServer.spawned.ContainsKey(id);
            ReportAlfheimSmoke(ResultPrefix, arced && spitHit && sparksReady && meleeHit && cleaned,
                $"arc={arced}, spitHit={spitHit}, sparks={sparksReady}, meleeHit={meleeHit}, audioSelected={selectedSpitSound}, " +
                $"audioStarted={spitAudioStarted}, cleanup={cleaned}");
        }

        IEnumerator VerifySphinxMiniBoss(CharacterInstance actor, CharacterInstance target)
        {
            uint id = actor.netId;
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            SetSmokePlayerPosition(actor, center + new Vector3(0f, .15f, 4f));
            float warningAt = -1f;
            float hitAfter = -1f;
            bool offsetVerified = false;
            bool interrupted = false;
            bool interruptionVerified = false;
            bool maskVerified = false;
            bool maskExpanded = false;
            bool audioPlayed = false;
            var attackAudio = actor.GetComponent<Skeleton1HCombat>().AttackAudio;
            GameObject warningMask = null;
            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim == target && damage > 0 && attack == AttackType.explosion && warningAt >= 0f)
                    hitAfter = Time.time - warningAt;
            }
            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (actor && target && hitAfter < 0f && Time.realtimeSinceStartup < deadline)
            {
                if (warningAt < 0f && actor.GetComponentInChildren<LineRenderer>() is { enabled: true } ring)
                {
                    warningAt = Time.time;
                    audioPlayed |= attackAudio && attackAudio.isPlaying && attackAudio.clip &&
                        attackAudio.clip.name == "OriginalMiniBossBlast";
                    warningMask = GameObject.Find("Sphinx blast warning mask");
                    if (warningMask && warningMask.TryGetComponent<MeshRenderer>(out var renderer))
                    {
                        var material = renderer.sharedMaterial;
                        maskVerified |= material && material.shader.isSupported && material.mainTexture &&
                            material.mainTexture.name == "ring_shield01";
                    }
                    if (!interrupted)
                    {
                        Vector3 forward = Quaternion.Euler(0f, actor.lookInput.y, 0f) * Vector3.forward;
                        Vector3 ringCenter = ring.GetPosition(0) - Vector3.right * 5f;
                        Vector3 expected = actor.transform.position + forward * 3.5f;
                        offsetVerified = Vector3.ProjectOnPlane(ringCenter - expected, Vector3.up).sqrMagnitude < .04f;
                        actor.BlockCharacter(true);
                        yield return null;
                        yield return new WaitForSeconds(2f);
                        interruptionVerified = hitAfter < 0f && !ring.enabled && warningMask && !warningMask.activeSelf &&
                            attackAudio && !attackAudio.isPlaying;
                        actor.BlockCharacter(false);
                        interrupted = true;
                        warningAt = -1f;
                    }
                }
                if (interrupted && warningMask && warningMask.activeSelf && warningMask.transform.localScale.x > 6f)
                    maskExpanded = true;
                yield return null;
            }
            if (actor) actor.Client_OnDamageDealt -= OnDamage;
            JotunnMonsterGm.Clear(target);
            SetSmokePlayerPosition(target, original);
            Destroy(floor);
            yield return null;
            bool cleaned = !NetworkServer.spawned.ContainsKey(id);
            ReportAlfheimSmoke(ResultPrefix, offsetVerified && interruptionVerified && maskVerified && maskExpanded && audioPlayed &&
                hitAfter >= 1.75f && hitAfter <= 2.15f && cleaned,
                $"offset={offsetVerified}, interrupted={interruptionVerified}, mask={maskVerified}, " +
                $"expanded={maskExpanded}, blastAfter={hitAfter:F2}, audio={audioPlayed}, cleanup={cleaned}");
        }

        IEnumerator VerifyHybBossCircles(CharacterInstance actor, CharacterInstance target, int expectedCircles)
        {
            uint id = actor.netId;
            Vector3 originalPosition = target.transform.position;
            bool invulnerable = target.RangeInvulnerable;
            int maxHealth = target.MaxHealth;
            int health = target.CurrentHealth;
            target.MaxHealth = target.CurrentHealth = 2000;
            target.RangeInvulnerable = false;
            Vector3 center = originalPosition + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(42f, 1f, 42f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(actor, center + Vector3.up * .15f);
            SetSmokePlayerPosition(target, center + Vector3.forward * 8f + Vector3.up * .15f);
            var seenWarnings = new HashSet<LineRenderer>();
            bool castAudioPlayed = false;
            var blastSources = new System.Collections.Generic.HashSet<AudioSource>();
            var audio = actor.GetComponent<Skeleton1HCombat>().AttackAudio;
            bool hit = false;
            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                hit |= victim == target && damage > 0 && attack == AttackType.explosion;
            }

            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 22f;
            while (actor && target && (seenWarnings.Count < expectedCircles || !hit || !castAudioPlayed || blastSources.Count < expectedCircles) && Time.realtimeSinceStartup < deadline)
            {
                foreach (var ring in actor.GetComponentsInChildren<LineRenderer>())
                    if (ring.enabled && ring.name == "Enemy ground warning") seenWarnings.Add(ring);
                castAudioPlayed |= audio && audio.isPlaying && audio.clip.name == "OriginalCircleCast";
                foreach (var source in actor.GetComponentsInChildren<AudioSource>())
                    if (source.isPlaying && source.clip && source.clip.name == "OriginalCircleBlast")
                        blastSources.Add(source);
                yield return null;
            }
            if (actor) actor.Client_OnDamageDealt -= OnDamage;
            JotunnMonsterGm.SetCombat(target, false);
            bool stopped = audio && !audio.isPlaying;
            foreach (var source in blastSources) stopped &= source && !source.isPlaying;
            if (target)
            {
                SetSmokePlayerPosition(target, originalPosition);
                target.RangeInvulnerable = invulnerable;
                target.MaxHealth = maxHealth;
                target.CurrentHealth = health;
            }
            Destroy(floor);
            JotunnMonsterGm.Clear(target);
            yield return null;
            bool cleaned = !NetworkServer.spawned.ContainsKey(id);
            ReportAlfheimSmoke(ResultPrefix, seenWarnings.Count == expectedCircles && hit && castAudioPlayed && blastSources.Count == expectedCircles && stopped && cleaned,
                $"warnings={seenWarnings.Count}/{expectedCircles}, hit={hit}, castAudioPlayed={castAudioPlayed}, blastSources={blastSources.Count}, stopped={stopped}, cleanup={cleaned}");
        }

        static void LogSphinxMiniBossAssets()
        {
            const string folder = "Jotunnslayer/SphinxSkeleton/";
            var model = Resources.Load<GameObject>(folder + "skeleton_mb_sphinx_sur");
            var animator = model ? model.GetComponent<Animator>() : null;
            var avatar = animator ? animator.avatar : null;
            var spawn = Resources.Load<AnimationClip>(folder + "2handed_stand_attack_summon_from_statue");
            var attack = Resources.Load<AnimationClip>(folder + "2handed_skill_04");
            Debug.Log($"JOTUNN_HYB_SPHINX_MINIBOSS_ASSETS model={(bool)model}, " +
                $"humanAvatar={avatar && avatar.isValid && avatar.isHuman}, " +
                $"albedo={(bool)Resources.Load<Texture2D>(folder + "sphinx_miniboss_dm")}, " +
                $"normal={(bool)Resources.Load<Texture2D>(folder + "sphinx_miniboss_nm")}, " +
                $"spawnHuman={spawn && spawn.humanMotion}, attackHuman={attack && attack.humanMotion}");
        }

        static void LogSphinxGuardianAssets()
        {
            const string folder = "Jotunnslayer/SphinxSkeleton/";
            var model = Resources.Load<GameObject>(folder + "skeleton_guardian_sphinx_sur");
            var animator = model ? model.GetComponent<Animator>() : null;
            var avatar = animator ? animator.avatar : null;
            var clips = new[] { "dual_init", "dual_stand_attack", "dual_walk", "dual_attack_04_double",
                "dual_attck_03_double", "dual_hit" };
            string states = string.Join(", ", Array.ConvertAll(clips, name =>
            {
                var clip = Resources.Load<AnimationClip>(folder + name);
                return $"{name}={(clip ? clip.humanMotion.ToString() : "missing")}";
            }));
            Debug.Log($"JOTUNN_HYB_SPHINX_GUARDIAN_ASSETS model={(bool)model}, " +
                $"animator={(bool)animator}, avatar={(bool)avatar}, valid={avatar && avatar.isValid}, " +
                $"human={avatar && avatar.isHuman}, " +
                $"albedo={(bool)Resources.Load<Texture2D>(folder + "sphinx_elite_dm")}, " +
                $"normal={(bool)Resources.Load<Texture2D>(folder + "sphinx_nm")}, {states}");
        }

        static IEnumerator VerifyHybSubNormalSummons(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYB_SUB_NORMAL_SUMMONS_SMOKE";
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            bool invulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);

            var caster = Skeleton1HSpawn.TrySpawn(target, center + new Vector3(0f, .15f, 6f),
                Quaternion.identity, Skeleton1HSpawn.Kind.HybSubBossNormal);
            var bows = new HashSet<uint>();
            var melee = new HashSet<uint>();
            bool resumedSummoning = false;
            if (caster)
            {
                caster.GetComponent<Skeleton1HCombat>().SetHyboreaSkillSet(2);
                float deadline = Time.realtimeSinceStartup + 75f;
                while (caster && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var identity in NetworkServer.spawned.Values)
                    {
                        var visual = identity.GetComponentInChildren<Skeleton1HVisual>();
                        if (!visual) continue;
                        if (visual.name.StartsWith("skeleton_bow_sphinx", StringComparison.Ordinal))
                            bows.Add(identity.netId);
                        if (visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal) &&
                            melee.Add(identity.netId) && bows.Count >= 4)
                            resumedSummoning = true;
                    }
                    if (bows.Count > 4 || (bows.Count == 4 && resumedSummoning)) break;
                    KillHybSmokeSummons(melee, target);
                    if (bows.Count >= 4) KillHybSmokeSummons(bows, target);
                    yield return null;
                }
            }

            if (caster) NetworkServer.Destroy(caster.gameObject);
            yield return null;
            yield return null;
            bool cleaned = !bows.Overlaps(NetworkServer.spawned.Keys) && !melee.Overlaps(NetworkServer.spawned.Keys);
            target.RangeInvulnerable = invulnerable;
            SetSmokePlayerPosition(target, original);
            Destroy(floor);
            ReportAlfheimSmoke(marker, bows.Count == 4 && resumedSummoning && cleaned,
                $"group=2, bows={bows.Count}, resumedSummoning={resumedSummoning}, cleanup={cleaned}");
        }

        static IEnumerator VerifyHyboreaBossSummons(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_BOSS_SUMMONS_SMOKE";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            bool invulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);

            bool passed = true;
            for (int group = 0; group < 3; group++)
            {
                var caster = Skeleton1HSpawn.TrySpawn(target, center + new Vector3(0f, .15f, 6f),
                    Quaternion.identity, Skeleton1HSpawn.Kind.HybBossEasy);
                var melee = new HashSet<uint>();
                var bows = new HashSet<uint>();
                var guardians = new HashSet<uint>();
                var missiles = new HashSet<int>();
                if (caster)
                    caster.GetComponent<Skeleton1HCombat>().SetHyboreaBossSkillSet(group);
                float deadline = Time.realtimeSinceStartup + 55f;
                while (caster && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var identity in NetworkServer.spawned.Values)
                    {
                        var visual = identity.GetComponentInChildren<Skeleton1HVisual>();
                        if (!visual) continue;
                        if (visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal))
                            melee.Add(identity.netId);
                        if (visual.name.StartsWith("skeleton_bow_sphinx", StringComparison.Ordinal))
                            bows.Add(identity.netId);
                        if (visual.name.StartsWith("skeleton_guardian_sphinx_sur", StringComparison.Ordinal))
                            guardians.Add(identity.netId);
                    }
                    foreach (var missile in FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None))
                        if (missile.IsHybBossMissileFrom(caster)) missiles.Add(missile.GetInstanceID());
                    if (missiles.Count >= 5 && melee.Count >= 7 &&
                        (group != 1 || (bows.Count >= 4 && guardians.Count >= 4))) break;
                    if (group == 1)
                    {
                        if (bows.Count == 4) KillHybSmokeSummons(bows, target);
                        if (guardians.Count == 4) KillHybSmokeSummons(guardians, target);
                        if (melee.Count >= 7) KillHybSmokeSummons(melee, target);
                    }
                    yield return null;
                }
                if (caster) NetworkServer.Destroy(caster.gameObject);
                yield return null;
                yield return null;
                bool cleaned = !melee.Overlaps(NetworkServer.spawned.Keys) &&
                    !bows.Overlaps(NetworkServer.spawned.Keys) && !guardians.Overlaps(NetworkServer.spawned.Keys);
                bool counts = group == 1 ? melee.Count >= 7 && bows.Count == 4 && guardians.Count == 4
                    : melee.Count == 7 && bows.Count == 0 && guardians.Count == 0;
                Debug.Log($"{marker} group={group}, melee={melee.Count}, bows={bows.Count}, guardians={guardians.Count}, " +
                    $"missiles={missiles.Count}, cleanup={cleaned}");
                passed &= counts && missiles.Count >= 5 && cleaned;
            }
            target.RangeInvulnerable = invulnerable;
            SetSmokePlayerPosition(target, original);
            Destroy(floor);
            ReportAlfheimSmoke(marker, passed, "Check skill set counts and cleanup.");
        }

        static void KillHybSmokeSummons(HashSet<uint> ids, CharacterInstance target)
        {
            foreach (uint id in ids)
                if (NetworkServer.spawned.TryGetValue(id, out var identity) &&
                    identity.TryGetComponent<CharacterInstance>(out var actor) && actor.CurrentHealth > 0)
                    actor.Server_ChangeHealthStateRaw(40000, CharacterPart.body, AttackType.melee, target, 0);
        }

        static IEnumerator VerifyHyboreaBossTeleport(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_BOSS_TELEPORT_SMOKE";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            var root = new GameObject("Hyborea main boss smoke anchors");
            var encounter = root.AddComponent<HyboreaEasyBossEncounter>();
            Transform Anchor(Vector3 offset)
            {
                var anchor = new GameObject("Smoke anchor").transform;
                anchor.SetParent(root.transform);
                anchor.position = center + offset + Vector3.up * .15f;
                return anchor;
            }
            var spawn = Anchor(Vector3.forward * 8f);
            var destination = Anchor(Vector3.right * 8f);
            var subBossPhase = root.AddComponent<HyboreaEasySubBossPhase>();
            subBossPhase.BindAnchors(new[] { Anchor(new Vector3(-4f, 0f, -4f)), Anchor(Vector3.back * 7f),
                Anchor(new Vector3(4f, 0f, -4f)), Anchor(new Vector3(-4f, 0f, 4f)),
                Anchor(Vector3.left * 7f), Anchor(new Vector3(4f, 0f, 4f)) });
            encounter.BindAnchors(spawn, new[] { destination, Anchor(Vector3.left),
                Anchor(Vector3.back), Anchor(Vector3.right) }, subBossPhase);
            bool began = encounter.TryBegin(target);
            bool refusedRepeat = began && !encounter.TryBegin(target);
            CharacterInstance actor = null;
            foreach (var combat in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
            {
                var candidate = combat.GetComponent<CharacterInstance>();
                if ((candidate.transform.position - spawn.position).sqrMagnitude > .01f) continue;
                actor = candidate;
                break;
            }
            uint id = actor ? actor.netId : 0;
            var renderer = actor ? actor.GetComponentInChildren<Skeleton1HVisual>()
                .GetComponentInChildren<SkinnedMeshRenderer>() : null;
            bool arrivedHidden = false;
            bool reappeared = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (renderer && Time.realtimeSinceStartup < deadline)
            {
                Vector3 offset = actor.transform.position - destination.position;
                arrivedHidden |= renderer.forceRenderingOff && offset.sqrMagnitude < .25f;
                reappeared = arrivedHidden && !renderer.forceRenderingOff;
                if (reappeared) break;
                yield return null;
            }
            bool cyclesPassed = reappeared;
            if (reappeared)
            {
                var combat = actor.GetComponent<Skeleton1HCombat>();
                var opaqueMaterial = renderer.sharedMaterial;
                for (int group = 1; group <= 2; group++)
                {
                    int health = actor.CurrentHealth;
                    bool split = encounter.TryBeginSplit();
                    bool suspended = split && !encounter.TryBeginSplit() && renderer.forceRenderingOff &&
                        actor.RangeInvulnerable;
                    actor.Server_ChangeHealthStateRaw(40000, CharacterPart.body, AttackType.melee, target, 0);
                    bool protectedHealth = actor.CurrentHealth == health;
                    int killed = 0;
                    bool fading = false;
                    deadline = Time.realtimeSinceStartup + 10f;
                    while (split && actor && encounter.CurrentPhase == group * 2 - 1 &&
                           Time.realtimeSinceStartup < deadline)
                    {
                        var currentMaterial = renderer.sharedMaterial;
                        if (currentMaterial && currentMaterial != opaqueMaterial && !renderer.forceRenderingOff &&
                            actor.RangeInvulnerable)
                        {
                            float alpha = currentMaterial.GetColor("_BaseColor").a;
                            fading |= alpha > .15f && alpha < .85f;
                        }
                        foreach (var minion in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
                        {
                            if (!minion.TryGetComponent<CharacterInstance>(out var subBoss) || subBoss.MaxHealth != 1100 ||
                                subBoss.CurrentHealth <= 0 || minion.HyboreaSkillSet != group - 1)
                                continue;
                            subBoss.Server_ChangeHealthStateRaw(40000, CharacterPart.body, AttackType.melee, target, 0);
                            killed++;
                        }
                        yield return null;
                    }
                    bool restored = actor && encounter.CurrentPhase == group * 2 && combat.HyboreaSkillSet == group &&
                        actor.netId == id && actor.CurrentHealth == health && !actor.RangeInvulnerable &&
                        !renderer.forceRenderingOff && renderer.sharedMaterial == opaqueMaterial &&
                        (actor.transform.position - spawn.position).sqrMagnitude < .25f;
                    cyclesPassed &= suspended && protectedHealth && killed == group + 1 && fading && restored;
                    Debug.Log($"{marker} cycle={group}, suspended={suspended}, protected={protectedHealth}, " +
                        $"killed={killed}, fading={fading}, restored={restored}");
                    if (!restored) break;
                }
                cyclesPassed &= !encounter.TryBeginSplit();
            }
            if (actor)
                actor.Server_ChangeHealthStateRaw(40000, CharacterPart.body, AttackType.melee, target, 0);
            bool cleared = encounter.Cleared;
            Destroy(root);
            Destroy(floor);
            SetSmokePlayerPosition(target, original);
            yield return null;
            yield return null;
            bool cleaned = id != 0 && !NetworkServer.spawned.ContainsKey(id);
            ReportAlfheimSmoke(marker, refusedRepeat && arrivedHidden && reappeared && cyclesPassed && cleared && cleaned,
                $"spawn={began}, repeatRejected={refusedRepeat}, arrivedHidden={arrivedHidden}, " +
                $"reappeared={reappeared}, cycles={cyclesPassed}, cleared={cleared}, cleanup={cleaned}");
        }

        static IEnumerator VerifyHyboreaSubBossPhases(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_SUB_BOSS_PHASES_SMOKE";
            var root = new GameObject("Hyborea sub-boss smoke anchors");
            var encounter = root.AddComponent<HyboreaEasySubBossPhase>();
            var anchors = new Transform[6];
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = new GameObject($"Hyborea sub-boss anchor {i}").transform;
                anchor.SetParent(root.transform);
                anchor.position = target.transform.position + new Vector3(i * 2f - 2f, .15f, 6f);
                anchors[i] = anchor;
            }
            encounter.BindAnchors(anchors);
            Vector3 splitOrigin = target.transform.position + new Vector3(0f, 2f, -4f);
            var bossAnchor = new GameObject("Hyborea return pin").transform;
            bossAnchor.SetParent(root.transform);
            bossAnchor.position = splitOrigin - Vector3.up * 2f;
            var ids = new List<uint>(5);
            bool passed = true;
            foreach (var (phase, count, skillSet) in new[] { (1, 2, 0), (3, 3, 1) })
            {
                bool began = encounter.TryBegin(target, phase, splitOrigin);
                bool refusedRepeat = began && !encounter.TryBegin(target, phase, splitOrigin) && !encounter.Cleared;
                bool prematureReturnRejected = !encounter.TryBeginReturn(bossAnchor);
                int projectiles = 0;
                bool visualReady = true;
                bool divideAudioReady = false;
                foreach (Transform child in root.transform)
                {
                    if (child.name == "Hyborea phase divide audio")
                        divideAudioReady = HasSplitAudio(child, "HybSplitDivide");
                    if (child.name != "Hyborea phase split projectile") continue;
                    projectiles++;
                    var sparks = child.GetComponent<ParticleSystemRenderer>();
                    var trail = child.GetComponent<TrailRenderer>();
                    visualReady &= sparks && sparks.sharedMaterial && sparks.sharedMaterial.shader.isSupported &&
                        sparks.sharedMaterial.mainTexture && sparks.sharedMaterial.mainTexture.name == "T_sparks01" &&
                        trail && trail.sharedMaterial && !child.GetComponent<Collider>() &&
                        HasSplitAudio(child, "HybSplitFlying");
                }
                bool delayed = true;
                foreach (var bot in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
                    if (bot.TryGetComponent<CharacterInstance>(out var actor) && actor.CurrentHealth > 0)
                        delayed = false;
                var landed = new bool[anchors.Length];
                int matching = 0;
                Vector3 lastKill = default;
                float deadline = Time.realtimeSinceStartup + 6f;
                while (began && matching < count && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var bot in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
                    {
                        if (!bot.TryGetComponent<CharacterInstance>(out var actor) || actor.CurrentHealth <= 0 ||
                            actor.MaxHealth != 1100 || bot.HyboreaSkillSet != skillSet)
                            continue;
                        for (int i = 0; i < anchors.Length; i++)
                        {
                            if (landed[i] || (actor.transform.position - anchors[i].position).sqrMagnitude > .25f)
                                continue;
                            bool arrivalReady = false;
                            foreach (Transform child in root.transform)
                            {
                                if (child.name != "Hyborea phase arrival dust" ||
                                    (child.position - anchors[i].position).sqrMagnitude > .01f)
                                    continue;
                                var renderer = child.GetComponent<ParticleSystemRenderer>();
                                var particles = child.GetComponent<ParticleSystem>();
                                arrivalReady = renderer && renderer.sharedMaterial && renderer.sharedMaterial.mainTexture &&
                                    renderer.sharedMaterial.mainTexture.name == "HybSplitArrivalAtlas" &&
                                    particles && particles.isPlaying && particles.emission.burstCount == 1 &&
                                    HasSplitAudio(child, "HybSplitArrive");
                            }
                            visualReady &= arrivalReady;
                            landed[i] = true;
                            matching++;
                            ids.Add(actor.netId);
                            lastKill = actor.CombatAimPoint;
                            actor.Server_ChangeHealthStateRaw(40000, CharacterPart.body, AttackType.melee, target, 0);
                            break;
                        }
                    }
                    yield return null;
                }
                bool cleared = encounter.Cleared;
                bool returnBegan = cleared && encounter.TryBeginReturn(bossAnchor);
                bool returnFromLastKill = false;
                foreach (Transform child in root.transform)
                    if (child.name == "Hyborea phase return projectile")
                        returnFromLastKill = (child.position - lastKill).sqrMagnitude < .01f &&
                            HasSplitAudio(child, "HybSplitFlying");
                bool repeatReturnRejected = !encounter.TryBeginReturn(bossAnchor);
                deadline = Time.realtimeSinceStartup + 5f;
                while (returnBegan && !encounter.ReturnArrived && Time.realtimeSinceStartup < deadline)
                    yield return null;
                bool returnEffect = false;
                foreach (Transform child in root.transform)
                    if (child.name == "Hyborea phase arrival dust" &&
                        (child.position - bossAnchor.position).sqrMagnitude < .01f)
                        returnEffect = HasSplitAudio(child, "HybSplitArrive");
                bool returned = returnBegan && returnFromLastKill && repeatReturnRejected &&
                    encounter.ReturnArrived && returnEffect;
                passed &= refusedRepeat && delayed && visualReady && divideAudioReady &&
                    projectiles == count && matching == count && cleared && prematureReturnRejected && returned;
                Debug.Log($"{marker} phase {phase}: projectiles={projectiles}, delayed={delayed}, matched={matching}, " +
                    $"visualAndAudioReady={visualReady && divideAudioReady}, repeatRejected={refusedRepeat}, " +
                    $"cleared={cleared}, returned={returned}");
                yield return null;
            }
            bool cancelBegan = encounter.TryBegin(target, 1, splitOrigin);
            encounter.StopPhase();
            yield return null;
            bool cancelled = cancelBegan && !encounter.Cleared && root.transform.childCount == anchors.Length + 1;
            Destroy(root);
            yield return null;
            yield return null;
            bool cleaned = ids.Count == 5 && ids.TrueForAll(id => !NetworkServer.spawned.ContainsKey(id));
            ReportAlfheimSmoke(marker, passed && cancelled && cleaned,
                $"phases={passed}, cancelled={cancelled}, cleanup={cleaned}");
        }

        static bool HasSplitAudio(Transform owner, string clipName) =>
            owner.TryGetComponent<AudioSource>(out var audio) && audio.clip &&
            audio.clip.name == clipName && audio.spatialBlend == 1f;

        IEnumerator VerifyHybSummon(CharacterInstance actor, CharacterInstance target)
        {
            uint casterId = actor.netId;
            Vector3 originalPosition = target.transform.position;
            bool wasInvulnerable = target.RangeInvulnerable;
            Vector3 center = originalPosition + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Hyborea summon smoke floor";
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            SetSmokePlayerPosition(actor, center + Vector3.forward * 6f + Vector3.up * .15f);
            target.RangeInvulnerable = true;

            int peakProjectiles = 0;
            var casterVisual = actor.GetComponentInChildren<Skeleton1HVisual>();
            float boltStartedAt = -1f;
            float boltReleaseDelay = -1f;
            var summons = new HashSet<uint>();
            float deadline = Time.realtimeSinceStartup + 50f;
            try
            {
                while (actor && target && (summons.Count < 7 || boltReleaseDelay < 0f) &&
                       Time.realtimeSinceStartup < deadline)
                {
                    if (boltReleaseDelay < 0f && casterVisual &&
                        casterVisual.CurrentMotion == Skeleton1HVisual.Motion.Attack)
                    {
                        if (boltStartedAt < 0f) boltStartedAt = Time.time;
                        foreach (var projectile in FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None))
                            if (projectile.name == "Hyb single missile") boltReleaseDelay = Time.time - boltStartedAt;
                    }
                    else
                    {
                        boltStartedAt = -1f;
                    }
                    int count = 0;
                    foreach (Transform child in actor.transform)
                        if (child.name == "Hyb summon projectile") count++;
                    peakProjectiles = Mathf.Max(peakProjectiles, count);

                    if (peakProjectiles > 0)
                    {
                        foreach (var bot in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
                        {
                            if (bot.gameObject == actor.gameObject ||
                                !bot.TryGetComponent<CharacterInstance>(out var skeleton) || skeleton.MaxHealth != 36)
                                continue;
                            var visual = bot.GetComponentInChildren<Skeleton1HVisual>();
                            if (visual && visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal))
                                summons.Add(skeleton.netId);
                        }
                    }
                    yield return null;
                }
            }
            finally
            {
                if (target)
                {
                    JotunnMonsterGm.Clear(target);
                    target.RangeInvulnerable = wasInvulnerable;
                    SetSmokePlayerPosition(target, originalPosition);
                }
                Destroy(floor);
            }

            yield return null;
            yield return null;
            bool cleaned = !NetworkServer.spawned.ContainsKey(casterId);
            foreach (uint id in summons)
                cleaned &= !NetworkServer.spawned.ContainsKey(id);
            bool boltTimed = boltReleaseDelay >= 1.55f && boltReleaseDelay <= 2f;
            ReportAlfheimSmoke(ResultPrefix, peakProjectiles == 7 && summons.Count == 7 && boltTimed && cleaned,
                $"projectiles={peakProjectiles}, summoned={summons.Count}, boltRelease={boltReleaseDelay:F2}, cleanup={cleaned}");
        }

        IEnumerator VerifyAlfheimElfSword(CharacterInstance actor, CharacterInstance target)
        {
            uint id = actor.netId;
            Vector3 spawnPosition = actor.transform.position;
            float launchedAt = -1f;
            float hitAfter = -1f;
            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim == target && damage > 0 && attack == AttackType.hitscan && launchedAt >= 0f)
                    hitAfter = Time.time - launchedAt;
            }

            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 24f;
            while (actor && target && hitAfter < 0f && Time.realtimeSinceStartup < deadline)
            {
                if ((actor.transform.position - target.transform.position).sqrMagnitude < 5.5f * 5.5f)
                    SetSmokePlayerPosition(actor, spawnPosition);
                if (launchedAt < 0f)
                {
                    foreach (var projectile in FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None))
                        if (projectile.name == "Elf sword projectile") launchedAt = Time.time;
                }
                yield return null;
            }

            if (actor) actor.Client_OnDamageDealt -= OnDamage;
            JotunnMonsterGm.Clear(target);
            yield return new WaitForFixedUpdate();
            yield return null;
            bool cleaned = !NetworkServer.spawned.ContainsKey(id) && !FindFirstObjectByType<SkeletonBowArrow>();
            ReportAlfheimSmoke(ResultPrefix, hitAfter > .2f && cleaned,
                $"swordFlightBeforeHit={hitAfter:F2}, cleanup={cleaned}");
        }

        static IEnumerator VerifyHyboreaInteriorGroupCap(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_INTERIOR_GROUP_CAP_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            float previousTimeScale = Time.timeScale;
            target.RangeInvulnerable = true;
            var anchor = new GameObject("Hyborea group cap smoke anchor");
            var sequence = anchor.AddComponent<HyboreaInteriorEncounterSequence>();
            float matchStartedAt = Time.time - 299f;
            bool began = sequence.TryStartCore(target, matchStartedAt);
            var waves = target.GetComponents<HyboreaInteriorWave>();
            var ids = new HashSet<uint>();
            bool saturated = false;
            bool overLimit = false;
            bool refilled = false;
            try
            {
                Time.timeScale = 4f;
                float deadline = Time.realtimeSinceStartup + 30f;
                int countAtCap = 0;
                while (began && Time.realtimeSinceStartup < deadline)
                {
                    int active = 0;
                    int activeWaves = 0;
                    CharacterInstance firstActor = null;
                    foreach (var wave in waves)
                    {
                        int waveActive = wave ? wave.CountActiveActors() : 0;
                        active += waveActive;
                        if (waveActive == 0) continue;
                        activeWaves++;
                        foreach (var actor in wave.Actors)
                        {
                            if (!actor) continue;
                            ids.Add(actor.netId);
                            if (!firstActor && actor.CurrentHealth > 0) firstActor = actor;
                        }
                    }

                    int limit = Mathf.RoundToInt(Mathf.Lerp(50f, 150f, (Time.time - matchStartedAt) / 900f));
                    overLimit |= active > limit;
                    if (!saturated && activeWaves >= 2 && active == limit && firstActor)
                    {
                        saturated = true;
                        countAtCap = active;
                        NetworkServer.Destroy(firstActor.gameObject);
                    }
                    else if (saturated && active >= countAtCap && ids.Count > countAtCap)
                    {
                        refilled = true;
                        break;
                    }
                    if (overLimit) break;
                    yield return null;
                }
            }
            finally
            {
                sequence.StopCore();
                Destroy(anchor);
                if (target) target.RangeInvulnerable = wasInvulnerable;
                Time.timeScale = previousTimeScale;
            }

            yield return null;
            bool cleaned = ids.Count > 0;
            foreach (uint id in ids)
                cleaned &= !NetworkServer.spawned.ContainsKey(id);
            ReportAlfheimSmoke(marker, began && saturated && !overLimit && refilled && cleaned,
                $"saturated={saturated}, overLimit={overLimit}, refilled={refilled}, cleanup={cleaned}");
        }

        static IEnumerator VerifyHyboreaInteriorBursts(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_INSANE_INTERIOR_BURSTS_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            bool passed = true;
            HyboreaInteriorWave wave = null;
            try
            {
                foreach (var (index, start, count) in new[] { (9, 1185f, 30), (10, 1188f, 35) })
                {
                    bool began = HyboreaInteriorWave.TryStart(target, Time.time - start + 1f,
                        new System.Random(index), out wave, index, HyboreaInteriorDifficulty.Insane);
                    yield return new WaitForSeconds(.5f);
                    bool waited = wave && wave.SpawnedCount == 0;
                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (wave && wave.SpawnedCount == 0 && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    bool batch = wave && wave.SpawnedCount > 1;
                    while (wave && wave.SpawnedCount < count && Time.realtimeSinceStartup < deadline)
                        yield return null;

                    var ids = new List<uint>(count);
                    bool correct = wave && wave.SpawnedCount == count && wave.BowSpawnCount == 0;
                    if (wave)
                    {
                        foreach (var actor in wave.Actors)
                        {
                            var visual = actor ? actor.GetComponentInChildren<Skeleton1HVisual>() : null;
                            correct &= visual && actor.MaxHealth == 36 &&
                                visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal);
                            if (actor) ids.Add(actor.netId);
                        }
                        Destroy(wave);
                    }
                    yield return null;
                    bool cleaned = ids.Count == count && ids.TrueForAll(id => !NetworkServer.spawned.ContainsKey(id));
                    bool wavePassed = began && waited && batch && correct && cleaned;
                    passed &= wavePassed;
                    Debug.Log($"{marker} W{index}: waited={waited}, batch={batch}, actors={correct}, cleanup={cleaned}");
                }
            }
            finally
            {
                if (wave) Destroy(wave);
                if (target) target.RangeInvulnerable = wasInvulnerable;
            }
            ReportAlfheimSmoke(marker, passed, "Check W9 and W10 observations.");
        }

        static IEnumerator VerifyHyboreaInteriorLateWaves(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_INTERIOR_LATE_SMOKE";
            const int sampleCount = 24;
            bool wasInvulnerable = target.RangeInvulnerable;
            float previousTimeScale = Time.timeScale;
            target.RangeInvulnerable = true;
            Time.timeScale = 4f;
            HyboreaInteriorWave wave = null;
            var anchor = new GameObject("Hyborea interior smoke anchor");
            var sequence = anchor.AddComponent<HyboreaInteriorEncounterSequence>();
            bool passed = true;
            try
            {
                float futureStart = Time.time + 60f;
                bool registered = sequence.TryStartCore(target, futureStart) &&
                    target.GetComponents<HyboreaInteriorWave>().Length == 5 &&
                    !sequence.TryStartCore(target, futureStart);
                sequence.StopCore();
                yield return null;
                bool stopped = target.GetComponents<HyboreaInteriorWave>().Length == 0;
                bool existingStarted = HyboreaInteriorWave.TryStart(target, futureStart,
                    new System.Random(1), out wave, index: 3);
                bool conflictRejected = existingStarted && !sequence.TryStartCore(target, futureStart);
                yield return null;
                var remaining = target.GetComponents<HyboreaInteriorWave>();
                bool rolledBack = conflictRejected && remaining.Length == 1 && remaining[0] == wave;
                if (wave) Destroy(wave);
                yield return null;
                passed = registered && stopped && rolledBack;
                Debug.Log($"{marker}: registered={registered}, stopped={stopped}, rolledBack={rolledBack}");

                foreach (var (index, start, swordCount, guardianCount) in new[]
                {
                    (2, 300f, 20, 3),
                    (3, 420f, 20, 3),
                    (4, 540f, 19, 4),
                    (5, 720f, 19, 4)
                })
                {
                    bool began = HyboreaInteriorWave.TryStart(target, Time.time - start + 1f,
                        new System.Random(1), out wave, index);
                    yield return new WaitForSeconds(.5f);
                    bool waited = wave && wave.SpawnedCount == 0;
                    float deadline = Time.realtimeSinceStartup + 15f;
                    while (wave && wave.SpawnedCount < sampleCount && Time.realtimeSinceStartup < deadline)
                        yield return null;

                    int swords = 0;
                    int bows = 0;
                    int guardians = 0;
                    var ids = new List<uint>(sampleCount);
                    if (wave)
                    {
                        foreach (var actor in wave.Actors)
                        {
                            if (!actor) continue;
                            ids.Add(actor.netId);
                            var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
                            if (!visual) continue;
                            if (actor.MaxHealth == 36 && visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal)) swords++;
                            if (actor.MaxHealth == 36 && visual.name.StartsWith("skeleton_bow_sphinx", StringComparison.Ordinal)) bows++;
                            if (actor.MaxHealth == 140 && visual.name.StartsWith("skeleton_guardian_sphinx_sur", StringComparison.Ordinal)) guardians++;
                        }
                        Destroy(wave);
                    }
                    yield return null;
                    bool cleanup = ids.Count == sampleCount && ids.TrueForAll(id => !NetworkServer.spawned.ContainsKey(id));
                    bool wavePassed = began && waited && swords == swordCount && bows == 1 &&
                        guardians == guardianCount && cleanup;
                    passed &= wavePassed;
                    Debug.Log($"{marker} W{index}: waited={waited}, swords={swords}, bows={bows}, " +
                        $"guardians={guardians}, cleanup={cleanup}, passed={wavePassed}");
                }
            }
            finally
            {
                if (wave) Destroy(wave);
                Destroy(anchor);
                if (target) target.RangeInvulnerable = wasInvulnerable;
                Time.timeScale = previousTimeScale;
            }
            ReportAlfheimSmoke(marker, passed, "Check W2–W5 observations.");
        }

        static IEnumerator VerifyHyboreaInteriorOpeningWave(CharacterInstance target)
        {
            const string marker = "JOTUNN_HYBOREA_INTERIOR_W1_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            float previousTimeScale = Time.timeScale;
            target.RangeInvulnerable = true;
            bool began = HyboreaInteriorWave.TryStart(target, Time.time - 2f,
                new System.Random(97730), out var wave);
            bool duplicateRejected = began && !HyboreaInteriorWave.TryStart(target, Time.time);
            var ids = new List<uint>(41);
            bool passed = false;
            try
            {
                yield return new WaitForSeconds(.5f);
                bool waited = wave && wave.SpawnedCount == 0;
                Time.timeScale = 4f;
                float deadline = Time.realtimeSinceStartup + 25f;
                while (wave && wave.SpawnedCount < 40 && Time.realtimeSinceStartup < deadline)
                    yield return null;

                yield return new WaitForSeconds(1.5f);
                int swords = 0;
                int bows = 0;
                if (wave)
                {
                    foreach (var actor in wave.Actors)
                    {
                        if (!actor) continue;
                        ids.Add(actor.netId);
                        var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
                        if (!visual || actor.MaxHealth != 36) continue;
                        if (visual.name.StartsWith("skeleton_1h_sphinx_sur", StringComparison.Ordinal)) swords++;
                        if (visual.name.StartsWith("skeleton_bow_sphinx", StringComparison.Ordinal)) bows++;
                    }
                }
                bool capped = wave && wave.SpawnedCount == 40 && swords == 39 && bows == 1 &&
                    wave.BowSpawnCount == 1;
                bool refilled = false;
                if (capped)
                {
                    NetworkServer.Destroy(wave.Actors[0].gameObject);
                    yield return new WaitForSeconds(2f);
                    refilled = wave && wave.SpawnedCount == 41 && wave.Actors.Count == 40;
                    if (refilled) ids.Add(wave.Actors[wave.Actors.Count - 1].netId);
                }
                passed = began && duplicateRejected && waited && capped && refilled;
                Debug.Log($"{marker}: waited={waited}, swords={swords}, bows={bows}, capped={capped}, refilled={refilled}");
            }
            finally
            {
                if (wave) Destroy(wave);
                if (target) target.RangeInvulnerable = wasInvulnerable;
                Time.timeScale = previousTimeScale;
            }

            yield return null;
            bool cleaned = ids.Count == 41 && ids.TrueForAll(id => !NetworkServer.spawned.ContainsKey(id));
            ReportAlfheimSmoke(marker, passed && cleaned, $"wave={passed}, cleanup={cleaned}");
        }

        static IEnumerator VerifyAlfheimInsaneElfWave(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_W40_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            bool started = AlfheimNormalOpeningWave.TryStart(target, Time.time - 420f,
                new System.Random(976840), out var wave, index: 40);
            yield return new WaitForSeconds(.5f);
            bool waited = wave && wave.SpawnedCount == 0;
            int maxInsane = 0;
            int maxRanged = 0;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (wave && wave.SpawnedCount < 40 && Time.realtimeSinceStartup < deadline)
            {
                maxInsane = Mathf.Max(maxInsane, wave.ActiveInsaneElfCount);
                maxRanged = Mathf.Max(maxRanged, wave.ActiveRangedCount);
                yield return null;
            }

            var ids = new List<uint>();
            int insaneModels = 0;
            if (wave)
            {
                foreach (var actor in wave.Actors)
                {
                    if (!actor) continue;
                    ids.Add(actor.netId);
                    var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
                    if (actor.MaxHealth == 36 && visual &&
                        visual.name.StartsWith("elf_melee_insane_sur", StringComparison.Ordinal))
                        insaneModels++;
                }
                Destroy(wave);
            }
            if (target) target.RangeInvulnerable = wasInvulnerable;
            yield return null;
            bool cleaned = ids.Count > 0 && ids.TrueForAll(id => !NetworkServer.spawned.ContainsKey(id));
            ReportAlfheimSmoke(marker, started && waited && maxInsane == 2 && maxRanged == 5 &&
                insaneModels == 2 && ids.Count >= 40 && cleaned,
                $"waited={waited}, samples={ids.Count}, insane={maxInsane}, ranged={maxRanged}, models={insaneModels}, cleanup={cleaned}");
        }

        static IEnumerator VerifyAlfheimTailTerrain(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_TAIL_TERRAIN_SMOKE";
            Vector3 playerPosition = target.transform.position;
            Vector3 origin = playerPosition + Vector3.up * 30f;
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Alfheim tail test ramp";
            ramp.transform.SetPositionAndRotation(origin + Vector3.forward * 6f, Quaternion.Euler(10f, 0f, 0f));
            ramp.transform.localScale = new Vector3(8f, 1f, 16f);
            Physics.SyncTransforms();
            if (!Physics.Raycast(origin + Vector3.up * 4f, Vector3.down, out var startGround, 8f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                Destroy(ramp);
                ReportAlfheimSmoke(marker, false, "Ramp ground was not found.");
                yield break;
            }

            SetSmokePlayerPosition(target, startGround.point);
            AlfheimSpiderWave.Launch(target, target, Quaternion.identity, 1);
            SetSmokePlayerPosition(target, playerPosition);
            var wave = FindFirstObjectByType<AlfheimSpiderWave>();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (wave && wave.transform.position.z - origin.z < 5f && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            bool followed = wave && startGround.point.y - wave.transform.position.y > .4f &&
                Physics.Raycast(wave.transform.position, Vector3.down, out var ground, 1f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore) &&
                ground.collider.gameObject == ramp && Mathf.Abs(ground.distance - .4f) < .05f;
            bool expanded = wave && Mathf.Abs(wave.transform.localScale.x -
                (4f + 4f * (wave.transform.position.z - origin.z) / 12f)) < .05f;
            while (wave && Time.realtimeSinceStartup < deadline) yield return null;
            bool expired = !wave;
            if (wave) Destroy(wave.gameObject);
            Destroy(ramp);
            ReportAlfheimSmoke(marker, followed && expanded && expired,
                $"followed={followed}, expanded={expanded}, expired={expired}");
        }

        IEnumerator VerifyAlfheimBossCombat(CharacterInstance actor, CharacterInstance target, bool firstPhase)
        {
            uint bossId = actor.netId;
            Vector3 startPosition = target.transform.position;
            int startHealth = target.CurrentHealth;
            bool explosiveHit = false;
            bool waveHit = false;
            bool splashObserved = false;
            bool waveObserved = false;
            var summonIds = new HashSet<uint>();

            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim != target || damage <= 0) return;
                explosiveHit |= attack == AttackType.explosion;
                waveHit |= attack == AttackType.hitscan;
            }

            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 50f;
            while (actor && target && target.CurrentHealth > 0 && Time.realtimeSinceStartup < deadline)
            {
                if (target.CurrentHealth < target.MaxHealth / 2) target.CurrentHealth = target.MaxHealth;
                if (firstPhase)
                {
                    foreach (var bot in FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None))
                        if (bot.gameObject != actor.gameObject && bot.TryGetComponent<NetworkIdentity>(out var identity))
                            summonIds.Add(identity.netId);

                    bool splashInFlight = actor.transform.Find("Alfheim splash projectile");
                    splashObserved |= splashInFlight;
                    if (splashInFlight && !explosiveHit)
                    {
                        foreach (var ring in actor.GetComponentsInChildren<LineRenderer>())
                        {
                            if (!ring.enabled || ring.name != "Enemy ground warning") continue;
                            Vector3 center = (ring.GetPosition(0) + ring.GetPosition(ring.positionCount / 2)) * .5f;
                            if (!Physics.Raycast(center + Vector3.up, Vector3.down, out var ground, 2f,
                                GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;
                            SetSmokePlayerPosition(target, ground.point);
                            break;
                        }
                    }
                    if (splashObserved && explosiveHit && summonIds.Count > 0) break;
                }
                else
                {
                    waveObserved |= FindFirstObjectByType<AlfheimSpiderWave>();
                    if (waveObserved && waveHit && explosiveHit) break;
                }
                yield return null;
            }

            if (actor) actor.Client_OnDamageDealt -= OnDamage;
            JotunnMonsterGm.Clear(target);
            if (target && target.CurrentHealth > 0)
            {
                SetSmokePlayerPosition(target, startPosition);
                target.CurrentHealth = startHealth;
            }
            yield return new WaitForFixedUpdate();
            yield return null;
            bool cleaned = !NetworkServer.spawned.ContainsKey(bossId) && !FindFirstObjectByType<AlfheimSpiderWave>();
            foreach (uint id in summonIds)
                cleaned &= !NetworkServer.spawned.ContainsKey(id);
            bool actions = firstPhase ? splashObserved && summonIds.Count > 0 : waveObserved && waveHit;
            Debug.Log($"{ResultPrefix}: splash={splashObserved}, summons={summonIds.Count}, wave={waveObserved}, " +
                $"waveHit={waveHit}, explosiveHit={explosiveHit}, cleanup={cleaned}");
            ReportAlfheimSmoke(ResultPrefix, actions && explosiveHit && cleaned, "Check phase combat observations.");
        }

        static IEnumerator VerifyMonsterPlaytest(CharacterInstance target)
        {
            const string marker = "MONSTER_PLAYTEST";
            var lab = FindFirstObjectByType<RangeResearchLab>();
            if (!lab)
            {
                ReportAlfheimSmoke(marker, false, "training GM missing");
                yield break;
            }

            string[] args = Environment.GetCommandLineArgs();
            bool whiteboxOnly = Array.IndexOf(args, "-monsterWhitebox") >= 0;
            if (whiteboxOnly && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != RoomCreator.TrainingScenePath)
            {
                ReportAlfheimSmoke(marker, false, "expected whitebox training scene");
                yield break;
            }
            int filterIndex = Array.IndexOf(args, "-monsterId") + 1;
            string[] filter = filterIndex > 0 && filterIndex < args.Length ? args[filterIndex].Split(',') : null;
            int captureIndex = Array.IndexOf(args, "-monsterFrames") + 1;
            string captureDirectory = whiteboxOnly && captureIndex > 0 && captureIndex < args.Length
                ? args[captureIndex] : null;
            string[] rainIds =
            {
                "CHILD_BODY", "CLAYGRENADIER_BODY", "CLAYBRUISER_BODY", "MINIMUSHROOM_BODY", "BELL_BODY", "LEMURIAN_BODY", "BEETLEGUARD_BODY", "BEETLE_BODY", "GOLEM_BODY",
                "TITAN_BODY", "LEMURIANBRUISER_BODY", "WISP_BODY", "GREATERWISP_BODY",
                "IMP_BODY", "JELLYFISH_BODY", "VERMIN_BODY", "FLYINGVERMIN_BODY",
                "HERMIT_CRAB_BODY", "BISON_BODY", "ACIDLARVA_BODY", "LUNAREXPLODER_BODY",
                "PARENT_BODY", "BEETLEQUEEN_BODY", "TITANGOLD_BODY", "GM_SNOW_GOLEM",
                "GM_NATURE_GOLEM", "GM_SANDY_GOLEM", "GUP_BODY", "GEEP_BODY", "GIP_BODY",
            };
            int failures = 0;
            int tested = 0;
            int originalMaxHealth = target.MaxHealth;
            int originalHealth = target.CurrentHealth;
            // Keep the same host character alive for subsequent cases while accepting real damage.
            target.MaxHealth = target.CurrentHealth = Mathf.Max(originalMaxHealth, 1000);
            foreach (var (id, jotunn) in MonsterPlaytestOrder(rainIds))
            {
                if (filter != null && Array.IndexOf(filter, id) < 0) continue;
                if (!target || target.CurrentHealth <= 0)
                {
                    ReportAlfheimSmoke(marker, false, "training player unavailable");
                    yield break;
                }
                tested++;
                Debug.Log($"{marker}_BEGIN {id}");
                int health = target.CurrentHealth;
                bool invulnerable = target.RangeInvulnerable;
                target.RangeInvulnerable = false;
                Vector2 look = target.lookInput;
                Vector3 originalPosition = target.transform.position;
                var entry = new BrowserEntry { id = id, category = 1 };
                string spawnResult = null;
                CharacterInstance actor = null;
                if (Array.IndexOf(args, "-monsterUiSpawn") >= 0)
                {
                    lab.SendMessage("Toggle");
                    yield return null;
                    var browser = lab.GetComponentInChildren<JournalBrowser>();
                    string label = id;
                    if (!jotunn)
                    {
                        var journal = JsonUtility.FromJson<ReferenceJournal>(Resources.Load<TextAsset>("RainImported/RainJournal").text);
                        var row = Array.Find(journal.entries, candidate => candidate.key == id);
                        if (row != null) label = row.name;
                    }
                    bool selected = browser && ClickGmButton(browser, jotunn ? "怪物（巨人杀手）" : "怪物");
                    bool clickedEntry = false;
                    for (int page = 0; selected && page < 16; page++)
                    {
                        clickedEntry = ClickGmButton(browser, label);
                        if (clickedEntry || !ClickGmButton(browser, ">")) break;
                        yield return null;
                    }
                    bool clickedSpawn = clickedEntry && ClickGmButton(browser, "召唤");
                    yield return null;
                    if (clickedSpawn)
                    {
                        var rainActor = FindFirstObjectByType<RangeResearchEnemy>();
                        if (rainActor && rainActor.IsLabSummon) actor = rainActor.GetComponent<CharacterInstance>();
                        if (jotunn)
                        {
                            var presentation = FindFirstObjectByType<Skeleton1HVisual>();
                            if (presentation) actor = presentation.GetComponentInParent<CharacterInstance>();
                        }
                    }
                    Debug.Log($"MONSTER_UI_SPAWN {id} category={selected} entry={clickedEntry} button={clickedSpawn} actor={(actor ? actor.netId : 0)}");
                    if (browser) browser.Close();
                }
                else if (jotunn)
                    actor = JotunnMonsterGm.TrySpawn(target, id);
                else
                {
                    for (int direction = 0; direction < 4 && !actor; direction++)
                    {
                        target.lookInput = new Vector2(look.x, look.y + direction * 90f);
                        spawnResult = lab.ExecuteCatalog(entry);
                        var rainActor = FindFirstObjectByType<RangeResearchEnemy>();
                        if (rainActor && rainActor.IsLabSummon)
                            actor = rainActor.GetComponent<CharacterInstance>();
                    }
                }
                target.lookInput = look;

                if (!actor)
                {
                    failures++;
                    target.RangeInvulnerable = invulnerable;
                    Debug.LogWarning($"{marker}_SKIP {id} spawnFailed {(jotunn ? JotunnMonsterGm.Supports(id) ? Skeleton1HSpawn.LastSpawnError : "unregistered Jotunn monster" : spawnResult)}");
                    continue;
                }

                uint identity = actor.netId;
                if (id == "TITAN_BODY" && Array.IndexOf(args, "-unknownMonsterGuard") >= 0)
                {
                    int spawnedBefore = NetworkServer.spawned.Count;
                    var unknown = new BrowserEntry { id = "BELL_BODY", category = 1 };
                    string rejectedSpawn = lab.ExecuteCatalog(unknown);
                    string rejectedRemove = lab.RemoveCatalog(unknown);
                    bool guarded = NetworkServer.spawned.Count == spawnedBefore && NetworkServer.spawned.ContainsKey(identity) &&
                        rejectedSpawn == "该怪物尚未接入召唤。" && rejectedRemove == rejectedSpawn;
                    if (!guarded) failures++;
                    Debug.Log($"MONSTER_UNKNOWN_ID_GUARD pass={guarded} existing={identity} spawn={rejectedSpawn} remove={rejectedRemove}");
                }
                yield return new WaitForSeconds(.35f);
                Vector3 idlePosition = actor ? actor.transform.position : Vector3.zero;
                yield return new WaitForSeconds(.5f);
                bool idle = actor && HorizontalDistanceSquared(actor.transform.position, idlePosition) < .0225f &&
                    target.CurrentHealth == health;
                var jotunnVisual = actor ? actor.GetComponentInChildren<Skeleton1HVisual>() : null;
                var rainVisual = actor ? actor.GetComponent<RainActorVisual>() : null;
                var rainEnemy = actor ? actor.GetComponent<RangeResearchEnemy>() : null;
                bool model = jotunnVisual ? jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>() != null
                    : rainVisual && actor.GetComponentInChildren<SkinnedMeshRenderer>();
                var combat = actor ? actor.GetComponent<Skeleton1HCombat>() : null;
                var beetleExplosion = actor.GetComponent<BeetleExplosionCombat>();
                bool verifyExplosiveBeetle = beetleExplosion != null;
                bool beetleCancelled = false, beetleBurstSeen = false;
                var ghostCharge = actor.GetComponent<MonsterPassThrough>();
                var attackAudio = beetleExplosion ? beetleExplosion.AttackAudio : combat ? combat.AttackAudio : ghostCharge ? ghostCharge.ChargeAudio :
                    rainVisual ? rainVisual.AttackAudio : null;
                if (captureDirectory != null)
                    CaptureMonsterFrame(captureDirectory, id, "idle", actor);
                bool attackFrameCaptured = false;
                bool releaseFrameCaptured = false;
                bool impactFrameCaptured = false;
                bool signatureFrameCaptured = false;
                bool greaterWispShotLogged = false;
                bool aimMeleeFrames = whiteboxOnly && (id is "CLAYBRUISER_BODY" or "elf_woman_sur" or "elf_woman_02_sur" or "skeleton_bow_VarRegular" or "skeleton_guardian_sur" or "skeleton_1h_sphinx_sur" or "hyb_sub_boss_sur_VarEasy" or "hyb_sub_boss_sur_VarNormal" or "jotunn_construct_sur" or "skeleton_goat_sur_boss_VarEasy" or "skeleton_goat_sur_boss_VarNormal" or
                    "jotunn_alfheim_boss_phase2_sur" or "ettin_boss" or "jotunn_warrior_sur" or "jotunn_warrior_v2_sur" or
                    "hyb_boss_sur_VarEasy" or "hyb_boss_sur_VarNormal" or "hyb_boss_sur_VarHard");
                float requiredAudioPeak = id is "ACIDLARVA_BODY" or "LUNAREXPLODER_BODY" ? .003f : .0001f;
                bool sampleAttackAudio = whiteboxOnly;
                var attackSamples = sampleAttackAudio ? new float[256] : null;
                float attackSourcePeak = 0f;
                float listenerPeak = 0f;
                bool verifyTeleport = id is "hyb_boss_sur_VarEasy" or "hyb_boss_sur_VarNormal" or "hyb_boss_sur_VarHard";
                bool verifyGoatNormal = id is "skeleton_goat_sur_boss_VarNormal" or "skeleton_goat_endless" or "skeleton_goat_hel_form_sur_boss";
                bool goatJumpWarning = false;
                bool goatJumpHit = false;
                bool goatJumpLaunchPlayed = false;
                int lastHealth = health;
                bool verifyChampionMissiles = id == "ele_mb_melee_champion_sur" &&
                    Array.IndexOf(Environment.GetCommandLineArgs(), "-championMissiles") >= 0;
                bool championAudioPlayed = false;
                bool verifyWarrior = id is "jotunn_warrior_sur" or "jotunn_warrior_v2_sur";
                bool verifyWarriorSpikes = verifyWarrior && Array.IndexOf(args, "-warriorSpikes") >= 0;
                int expectedWarriorSpikes = id == "jotunn_warrior_v2_sur" ? 18 : 6;
                int warriorWarnings = 0;
                bool warriorSpikeMotion = false;
                bool warriorSpikeLayout = false;
                float warriorSpikeCastAt = -1f;
                float warriorSpikeImpactAt = -1f;
                int warriorTurnStage = 0;
                bool warriorTracked = false;
                bool warriorLocked = false;
                float warriorFixtureYaw = 0f;
                Vector3 warriorFixtureOrigin = default;
                Vector3 warriorTrackedPoint = default;
                bool warriorSpikesCleared = false;
                GameObject warriorFloor = null;
                bool warriorLayeredAudio = false;
                int warriorStompArtLayers = 0;
                ParticleSystem[] warriorStompSystems = null;
                bool warriorStompArtCleared = false;
                int warriorImpactLayers = 0;
                ParticleSystem[] warriorImpactSystems = null;
                bool warriorImpactCleared = false;
                int warriorCraterPhases = 0;
                MaterialPropertyBlock warriorCraterSample = null;
                float warriorAudioAt = -1f;
                float warriorWarningAt = -1f;
                float warriorHitAt = -1f;
                if (verifyWarrior && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    model = actor.MaxHealth == 1100 && skin.bones.Length == 60 && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == (id == "jotunn_warrior_v2_sur" ? "V2WarriorColor" : "WarriorColor");
                }
                bool verifyGhostKing = id == "ghost_king_sur";
                bool ghostKingCastPlayed = false, ghostKingImpactPlayed = false;
                if (verifyGhostKing && model)
                    model = actor.MaxHealth == 1100 && jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length == 41;
                bool verifyElfWoman = id is "elf_woman_sur" or "elf_woman_02_sur";
                bool elfCastAudio = false, elfBlastAudio = false;
                bool verifyElfSummons = verifyElfWoman && Array.IndexOf(Environment.GetCommandLineArgs(), "-elfSummons") >= 0;
                bool verifyElfExpiry = verifyElfSummons && Array.IndexOf(Environment.GetCommandLineArgs(), "-elfExpiry") >= 0;
                var elfExpiryIds = verifyElfExpiry ? new Dictionary<uint, float>(6) : null;
                var observedElfExpiries = verifyElfExpiry ? new HashSet<uint>() : null;
                int elfExpiredCount = 0;
                bool elfExpiryTiming = true;
                if (verifyElfSummons) target.RangeInvulnerable = true;
                if (verifyElfWoman && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    model = actor.MaxHealth == 1100 && skin.bones.Length == 35 && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == (id == "elf_woman_02_sur" ? "V2ElfWomanColor" : "ElfWomanColor");
                }
                bool verifyElementalMelee = id is "ele_mb_melee_champion_sur" or "ele_mb_melee_sur";
                if (verifyElementalMelee && model)
                    model = actor.MaxHealth == 1100 && jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length == 20;
                bool verifyPoisonMage = id is "ele_mb_plague_sur" or "ele_mb_venom_sur";
                bool poisonOrbAudioPlayed = false;
                bool verifyPlagueSplash = id == "ele_mb_plague_sur";
                AudioSource plagueFlight = null;
                string plagueFlightClip = null;
                if (verifyPoisonMage && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    model = actor.MaxHealth == 1100 && skin.bones.Length == 54 && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == (id == "ele_mb_plague_sur" ? "PlagueColor" : "VenomColor");
                }
                bool verifyMage = id is "ele_mb_mage_sur" or "ele_mb_mage_v2_sur";
                bool verifyMageFire = id == "ele_mb_mage_v2_sur" && Array.IndexOf(Environment.GetCommandLineArgs(), "-mageFire") >= 0;
                bool mageFireSound = false;
                int mageFireDamage = 0;
                if (verifyMage && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    model = actor.MaxHealth == 1100 && skin.bones.Length == 54 && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == (id == "ele_mb_mage_v2_sur" ? "V2MageColor" : "MageColor");
                }
                bool verifyBird = id is "bird_noobj_sur" or "bird_sur";
                if (verifyBird && model)
                    model = actor.MaxHealth == 1100 && jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length == 32;
                bool birdCastSound = false, birdImpactSound = false;
                bool verifyConstruct = id == "jotunn_construct_sur";
                if (verifyConstruct && model) model = actor.MaxHealth == 4000 && jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length == 75;
                bool verifyMuspel = id is "jotunn_muspel_sur" or "jotunn_muspel_endless" or "jotunn_muspel_hel_form_sur";
                if (verifyMuspel && id != "jotunn_muspel_sur" && model)
                {
                    var skins = jotunnVisual.GetComponentsInChildren<SkinnedMeshRenderer>();
                    model = actor.MaxHealth == 1100 && skins.Length == 2 &&
                        Array.TrueForAll(skins, skin => skin.sharedMaterial.mainTexture &&
                            skin.sharedMaterial.mainTexture.name == "khosatral_dead_dm");
                }
                bool verifyEttin = id == "ettin_boss";
                bool verifyEttinEndless = id is "ettin_endless" or "ettin_hel_form_boss";
                bool novaImpact = false;
                bool novaLayer = false;
                var poisonShots = verifyEttinEndless ? new HashSet<int>() : null;
                bool poisonAudio = false;
                bool poisonWarning = false;
                EttinPoisonVolley observedPoison = null;
                int maxPoisonShots = 0;
                var ettinBlastSources = verifyEttin ? new HashSet<AudioSource>() : null;
                if ((verifyEttin || verifyEttinEndless) && model)
                {
                    var skins = jotunnVisual.GetComponentsInChildren<SkinnedMeshRenderer>();
                    model = skins.Length == 2 && skins[0].sharedMaterial.mainTexture &&
                        skins[1].sharedMaterial.mainTexture && skins[0].sharedMaterial.mainTexture != skins[1].sharedMaterial.mainTexture;
                    if (verifyEttinEndless && model)
                        model = Array.TrueForAll(skins, skin => skin.sharedMaterial.mainTexture.name.Contains("_dead_dm"));
                }
                bool verifyHorrorAlf = id == "horror_expl_alf_sur";
                if (verifyHorrorAlf && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    model = skin.bones.Length == 25 && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == "horror_expl_alf_dm";
                }
                bool followingGhost = id == "ghost_red_following_sur";
                bool redGhost = followingGhost || id is "ghost_red_sur" or "ghost_red_sur_bf";
                bool svartGhost = id == "ghost_svart_sur";
                bool skullGhost = id == "ghost_skull_sur";
                bool verifyGhost = id == "ghost_sur" || redGhost || svartGhost || skullGhost;
                Transform skullTail = null;
                Quaternion? firstTailPose = null;
                bool tailAnimated = false;
                string skullTailPath = null;
                float tailAngle = 0f;
                if (skullGhost && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    skullTail = Array.Find(skin.bones, bone => bone && bone.name == "tail1_Joint1");
                    for (var bone = skullTail; bone && bone != jotunnVisual.transform; bone = bone.parent)
                        skullTailPath = skullTailPath == null ? bone.name : bone.name + "/" + skullTailPath;
                    model = skin.bones.Length == 4 && skullTail && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == "ghost_skull2_dm" && actor.MaxHealth == 14;
                }
                if (svartGhost && model)
                {
                    var skin = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>();
                    model = skin.bones.Length == 31 && skin.sharedMaterial.mainTexture &&
                        skin.sharedMaterial.mainTexture.name == "horror_jotunheim_dm" && actor.MaxHealth == 14;
                }
                if (redGhost && model)
                {
                    var material = jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                    var albedo = material.GetTexture("_BaseMap");
                    var emission = material.GetTexture("_EmissionMap");
                    model = albedo && albedo.name == "ghost_red_dm" && emission && emission.name == "ghost_red_em" &&
                        (!followingGhost || actor.MaxHealth == 36);
                }
                bool ghostCancellation = false;
                var teleportModel = verifyTeleport && jotunnVisual ? jotunnVisual.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                bool arrivedHidden = false, reappeared = false;
                if (verifyGhost && actor)
                {
                    JotunnMonsterGm.SetCombat(target, true);
                    yield return new WaitForSeconds(.2f);
                    JotunnMonsterGm.SetCombat(target, false);
                    yield return new WaitForSeconds(.7f);
                    ghostCancellation = actor && target.CurrentHealth == health &&
                        HorizontalDistanceSquared(actor.transform.position, idlePosition) < .0225f &&
                        actor.GetComponentInChildren<LineRenderer>() is { enabled: false };
                }
                if (verifyExplosiveBeetle) {
                    JotunnMonsterGm.SetCombat(target, true);
                    float cancelDeadline=Time.realtimeSinceStartup+8f;
                    while(actor && !beetleExplosion.IsCasting && Time.realtimeSinceStartup<cancelDeadline) yield return null;
                    bool pending=actor && beetleExplosion.IsCasting;
                    int beforeStop=target.CurrentHealth;
                    JotunnMonsterGm.SetCombat(target,false);
                    yield return new WaitForSeconds(1f);
                    beetleCancelled=pending && actor && !beetleExplosion.IsCasting && !beetleExplosion.BurstVisible && !beetleExplosion.AttackAudio.isPlaying && target.CurrentHealth>=beforeStop;
                    Debug.Log($"MONSTER_BEETLE_CANCEL {id} pending={pending} cancelled={beetleCancelled} health={beforeStop}->{target.CurrentHealth}");
                    if(actor) SetSmokePlayerPosition(actor,idlePosition);
                }
                var constructArgs = Environment.GetCommandLineArgs();
                bool constructFar = verifyConstruct && Array.IndexOf(constructArgs, "-constructFarHammer") >= 0;
                bool constructHammer = constructFar || verifyConstruct && Array.IndexOf(constructArgs, "-constructNearHammer") >= 0;
                bool constructFlame = verifyConstruct && Array.IndexOf(constructArgs, "-constructFlame") >= 0;
                if (constructFlame) combat.SetConstructSkillSet(2);
                bool constructLaser = verifyConstruct && Array.IndexOf(constructArgs, "-constructLaser") >= 0;
                bool cancelConstructLaser = constructLaser && Array.IndexOf(constructArgs, "-cancelConstructLaser") >= 0;
                if (constructLaser) combat.SetConstructSkillSet(3);
                Vector3 constructPlayerStart = target.transform.position;
                if (constructHammer)
                {
                    combat.SetConstructSkillSet(1);
                    attackAudio = combat.ConstructHammerAudio;
                    Vector3 heading = Vector3.ProjectOnPlane(constructPlayerStart - idlePosition, Vector3.up).normalized;
                    SetSmokePlayerPosition(target, idlePosition + heading * (constructFar ? 10.5f : 7.3f));
                }
                if (verifyChampionMissiles && TryFindMonsterChasePoint(id, idlePosition, out var missilePoint))
                    SetSmokePlayerPosition(target, missilePoint);
                if (verifyWarrior && !whiteboxOnly)
                {
                    Vector3 center = originalPosition + Vector3.up * 30f;
                    warriorFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    warriorFloor.transform.position = center - Vector3.up * .5f;
                    warriorFloor.transform.localScale = new Vector3(60f, 1f, 60f);
                    Physics.SyncTransforms();
                    idlePosition = center + Vector3.up * .15f;
                    SetSmokePlayerPosition(actor, idlePosition);
                    SetSmokePlayerPosition(target, idlePosition + Vector3.forward * (verifyWarriorSpikes ? 10f : 4f));
                }
                bool movedPlayerForRangedAttack = false;
                if (jotunn) JotunnMonsterGm.SetCombat(target, true);
                else lab.SetMonsterCombat(true);

                bool moved = false;
                bool attacked = false;
                bool cueSeen = false;
                bool audioStarted = false;
                bool verifyQueen = id == "BEETLEQUEEN_BODY";
                bool queenSpitAudio = false;
                bool verifyGup = id is "GUP_BODY" or "GEEP_BODY" or "GIP_BODY";
                int gupDamage = id == "GEEP_BODY" ? 18 : id == "GIP_BODY" ? 9 : 36;
                if (verifyGup)
                {
                    int expectedHealth = id == "GEEP_BODY" ? 500 : id == "GIP_BODY" ? 250 : 1000;
                    string textureName = id == "GEEP_BODY" ? "texGeep_Diffuse" :
                        id == "GIP_BODY" ? "texGip_Diffuse" : "texGup_Diffuse";
                    bool originalTexture = false;
                    foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                        originalTexture |= renderer.sharedMaterial && renderer.sharedMaterial.mainTexture &&
                            renderer.sharedMaterial.mainTexture.name == textureName;
                    if (actor.MaxHealth != expectedHealth || !originalTexture)
                        Debug.Log($"{marker}_GUP_MODEL {id} hp={actor.MaxHealth} expected={expectedHealth} texture={originalTexture}");
                    model &= actor.MaxHealth == expectedHealth && originalTexture;
                }
                bool gupCharge = false, gupRelease = false;
                var gupBone = verifyGup && rainVisual ? rainVisual.FindModelBone("mainBody.2") : null;
                string gupBonePath = null;
                if (gupBone)
                {
                    var gupAnimator = gupBone.GetComponentInParent<Animator>();
                    for (var bone = gupBone; bone && (!gupAnimator || bone != gupAnimator.transform); bone = bone.parent)
                        gupBonePath = gupBonePath == null ? bone.name : bone.name + "/" + gupBonePath;
                }
                Vector3? firstGupScale = null;
                Vector3 firstGupPosition = default;
                int gupPoseSamples = 0;
                bool gupPoseChanged = false;
                AudioClip selectedAttackSound = null;
                int damage = 0;
                float parentHitObservedAt = -1f;
                float nextParentFrameAt = 0f;
                int parentFrameIndex = 0;
                bool changedCourse = false;
                float deadline = Time.realtimeSinceStartup + (verifyElfWoman ? 25f : id is "hyb_sub_boss_sur_VarEasy" or "hyb_sub_boss_sur_VarNormal" ? 25f : verifyGoatNormal ? 50f : verifyEttinEndless ? 55f : verifyEttin || verifyMuspel ? 35f : verifyPoisonMage || verifyElfSummons ? 25f : 12f);
                while (target && Time.realtimeSinceStartup < deadline)
                {
                    if (actor)
                    {
                        moved |= HorizontalDistanceSquared(actor.transform.position, idlePosition) >= .04f;
                        if ((id is "CHILD_BODY" or "CLAYGRENADIER_BODY" or "CLAYBRUISER_BODY" or "MINIMUSHROOM_BODY" or "BELL_BODY" or "elf_woman_sur" or "elf_woman_02_sur" or "skeleton_bow_VarRegular" or "skeleton_guardian_sur" or "skeleton_1h_sphinx_sur" or "PARENT_BODY" or "skeleton_goat_sur_boss_VarEasy" or "jotunn_construct_sur" or "hyb_sub_boss_sur_VarEasy" or "hyb_sub_boss_sur_VarNormal") && captureDirectory != null && parentFrameIndex < 32 &&
                            Time.realtimeSinceStartup >= nextParentFrameAt)
                        {
                            nextParentFrameAt = Time.realtimeSinceStartup + .2f;
                            CaptureMonsterFrame(captureDirectory, id, $"sequence-{parentFrameIndex++:D2}", actor, aimAtActor: true);
                        }
                        if ((id is "HERMIT_CRAB_BODY" or "GREATERWISP_BODY") && moved && !movedPlayerForRangedAttack &&
                            TryFindMonsterChasePoint(id, actor.transform.position, out var mortarPoint))
                        {
                            SetSmokePlayerPosition(target, mortarPoint);
                            movedPlayerForRangedAttack = true;
                        }
                        if (followingGhost && !changedCourse && target.CurrentHealth == health &&
                            HorizontalDistanceSquared(actor.transform.position, idlePosition) >= .36f)
                        {
                            Vector3 right = Vector3.Cross(Vector3.up, originalPosition - idlePosition).normalized;
                            for (int side = -1; side <= 1; side += 2)
                            {
                                Vector3 destination = originalPosition + right * (2.5f * side);
                                if (!Physics.Raycast(destination + Vector3.up * 3f, Vector3.down, out var ground, 6f,
                                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                                    ground.normal.y < .75f) continue;
                                SetSmokePlayerPosition(target, ground.point + Vector3.up * .15f);
                                changedCourse = true;
                                break;
                            }
                        }
                        if (teleportModel)
                        {
                            arrivedHidden |= moved && teleportModel.forceRenderingOff;
                            reappeared |= arrivedHidden && !teleportModel.forceRenderingOff;
                        }
                        if (beetleExplosion && beetleExplosion.BurstVisible) {
                            beetleBurstSeen=true;
                            if(captureDirectory!=null && !signatureFrameCaptured && beetleExplosion.BurstDiameter>1.4f) {
                                CaptureMonsterFrame(captureDirectory,id,"signature",actor); signatureFrameCaptured=true;
                            }
                        }
                        cueSeen |= (rainEnemy && rainEnemy.HasVisibleAttackWarning) ||
                            actor.RainTelegraph != 0 || actor.GetComponentInChildren<LineRenderer>() is { enabled: true };
                        if (verifyGoatNormal && jotunnVisual &&
                            jotunnVisual.CurrentMotion == Skeleton1HVisual.Motion.BossJump)
                        {
                            goatJumpWarning |= actor.GetComponentInChildren<LineRenderer>() is { enabled: true };
                            if (attackAudio && attackAudio.isPlaying && attackAudio.clip)
                            {
                                goatJumpLaunchPlayed |= attackAudio.clip.name == "OriginalGoatJumpLaunch";
                                goatJumpHit |= goatJumpWarning && attackAudio.clip.name == "OriginalGoatJumpImpact" &&
                                    target.CurrentHealth < lastHealth;
                            }
                        }
                        if (!verifyMageFire) lastHealth = target.CurrentHealth;
                        if (verifyEttin)
                            foreach (var source in actor.GetComponentsInChildren<AudioSource>())
                                if (source && source.isPlaying && source.clip && source.clip.name == "OriginalLightningImpact")
                                    ettinBlastSources.Add(source);
                        if (verifyEttinEndless && jotunnVisual && jotunnVisual.CurrentMotion == Skeleton1HVisual.Motion.HybSummon)
                            poisonWarning |= actor.GetComponentInChildren<LineRenderer>() is { enabled: true };
                        if (verifyEttinEndless && actor.TryGetComponent(out EttinPoisonVolley poison))
                        {
                            if (observedPoison != poison)
                            {
                                observedPoison = poison;
                                poisonShots.Clear();
                            }
                            foreach (var source in actor.GetComponentsInChildren<AudioSource>())
                            {
                                if (source.name != "Ettin poison projectile" || !source.isPlaying) continue;
                                poisonShots.Add(source.GetInstanceID());
                                poisonAudio |= source.clip && source.clip.name == "OriginalPoisonFlight";
                            }
                            maxPoisonShots = Mathf.Max(maxPoisonShots, poisonShots.Count);
                        }
                        if (verifyEttinEndless && attackAudio && attackAudio.isPlaying && attackAudio.clip)
                        {
                            novaImpact |= attackAudio.clip.name == "OriginalGoatHornsImpact";
                            if (!novaLayer && attackAudio.clip.name == "OriginalGoatHornsCast")
                                foreach (var source in actor.GetComponents<AudioSource>())
                                    novaLayer |= source.isPlaying && source.clip && source.clip.name == "OriginalNovaLayer";
                        }
                        audioStarted |= rainVisual ? rainVisual.AttackAudioPlaying : attackAudio && attackAudio.isPlaying;
                        if (sampleAttackAudio && attackAudio && attackAudio.isPlaying && attackAudio.clip)
                        {
                            attackAudio.GetOutputData(attackSamples, 0);
                            foreach (float sample in attackSamples)
                                attackSourcePeak = Mathf.Max(attackSourcePeak, Mathf.Abs(sample));
                            AudioListener.GetOutputData(attackSamples, 0);
                            foreach (float sample in attackSamples)
                                listenerPeak = Mathf.Max(listenerPeak, Mathf.Abs(sample));
                        }
                        if (verifyBird && attackAudio && attackAudio.isPlaying && attackAudio.clip)
                        {
                            birdCastSound |= attackAudio.clip.name == "OriginalBirdJumpCast";
                            birdImpactSound |= attackAudio.clip.name == "OriginalBirdJumpImpact";
                        }
                        if (verifyMageFire && attackAudio && attackAudio.isPlaying && attackAudio.clip &&
                            attackAudio.clip.name == "OriginalMageFireRing")
                        {
                            mageFireSound = true;
                            if (combat.MageFireReleased > 0)
                                mageFireDamage += Mathf.Max(0, lastHealth - target.CurrentHealth);
                        }
                        if (verifyChampionMissiles && attackAudio && attackAudio.isPlaying && attackAudio.clip)
                            championAudioPlayed |= attackAudio.clip.name == "OriginalChampionMissiles";
                        if (verifyPoisonMage && attackAudio && attackAudio.isPlaying && attackAudio.clip)
                            poisonOrbAudioPlayed |= attackAudio.clip.name == "OriginalPoisonOrbCast";
                        if (verifyPlagueSplash && combat.PlagueSplashFlightAudio is { isPlaying: true } flight && flight.clip)
                        {
                            plagueFlight = flight;
                            plagueFlightClip = flight.clip.name;
                        }
                        if (verifyElfWoman)
                        {
                            elfCastAudio |= attackAudio && attackAudio.isPlaying && attackAudio.clip &&
                                attackAudio.clip.name == "OriginalElfSpikeCast";
                            if (!elfBlastAudio)
                                foreach (var source in actor.GetComponentsInChildren<AudioSource>())
                                    elfBlastAudio |= source.isPlaying && source.clip &&
                                        source.clip.name.StartsWith("OriginalElfSpikeBlast", StringComparison.Ordinal);
                        }
                        if (verifyGhostKing)
                        {
                            ghostKingCastPlayed |= attackAudio && attackAudio.isPlaying && attackAudio.clip &&
                                attackAudio.clip.name == "OriginalGhostKingCast";
                            var impact = combat.GhostKingImpactAudio;
                            ghostKingImpactPlayed |= impact && impact.isPlaying && impact.clip &&
                                impact.clip.name == "OriginalGhostKingImpact";
                        }
                        if (verifyWarrior && attackAudio && attackAudio.isPlaying && attackAudio.clip &&
                            attackAudio.clip.name == "OriginalWarriorStompBase")
                        {
                            if (warriorAudioAt < 0f) warriorAudioAt = Time.time;
                            var layer = combat.WarriorSecondaryAudio;
                            warriorLayeredAudio |= layer && layer.isPlaying && layer.clip &&
                                layer.clip.name.StartsWith("OriginalWarriorStomp", StringComparison.Ordinal) &&
                                attackAudio.transform == layer.transform && layer.transform.localPosition.sqrMagnitude < .0001f;
                        }
                        if (verifyWarrior)
                        {
                            if (warriorWarningAt < 0f && actor.GetComponentInChildren<LineRenderer>() is { enabled: true })
                                warriorWarningAt = Time.time;
                            var art = combat.WarriorStompWarningArt;
                            if (warriorStompArtLayers != 15 && art && art.activeInHierarchy)
                            {
                                warriorStompSystems ??= art.GetComponentsInChildren<ParticleSystem>();
                                if (warriorStompSystems.Length == 4)
                                    for (int index = 0; index < warriorStompSystems.Length; index++)
                                    {
                                        var system = warriorStompSystems[index];
                                        if (system.particleCount > 0 && system.TryGetComponent<ParticleSystemRenderer>(out var renderer) &&
                                            renderer.enabled && renderer.mesh && renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported)
                                            warriorStompArtLayers |= 1 << index;
                                    }
                            }
                            if (warriorHitAt < 0f && combat.WarriorStompHits > 0)
                            {
                                warriorHitAt = Time.time;
                                warriorStompArtCleared = art && !art.activeSelf;
                            }
                            var impactArt = combat.WarriorStompImpactArt;
                            if (impactArt && impactArt.activeInHierarchy)
                            {
                                warriorImpactSystems ??= impactArt.GetComponentsInChildren<ParticleSystem>();
                                if (warriorImpactSystems.Length == 19)
                                    for (int index = 0; index < warriorImpactSystems.Length; index++)
                                    {
                                        var system = warriorImpactSystems[index];
                                        if (system.particleCount > 0 && system.TryGetComponent<ParticleSystemRenderer>(out var renderer) &&
                                            renderer.enabled && renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported)
                                            warriorImpactLayers |= 1 << index;
                                    }
                            }
                            var crater = combat.WarriorStompCrater;
                            if (warriorHitAt >= 0f && crater && crater.enabled && crater.gameObject.activeInHierarchy &&
                                crater.sharedMaterial && crater.sharedMaterial.mainTexture &&
                                crater.sharedMaterial.mainTexture.name == "WarriorImpactCrater")
                            {
                                warriorCraterSample ??= new MaterialPropertyBlock();
                                crater.GetPropertyBlock(warriorCraterSample);
                                float elapsed = Time.time - warriorHitAt;
                                float alpha = warriorCraterSample.GetColor("_Color").a;
                                if (elapsed is > .06f and < .23f && alpha is > .1f and < .85f) warriorCraterPhases |= 1;
                                if (elapsed is > .4f and < .9f && alpha > .97f) warriorCraterPhases |= 2;
                                if (elapsed is > 1.35f and < 1.65f && alpha is > .6f and < .9f) warriorCraterPhases |= 4;
                            }
                        }
                        if (verifyWarriorSpikes && jotunnVisual.CurrentMotion == Skeleton1HVisual.Motion.AttackAlternate)
                        {
                            warriorSpikeMotion = true;
                            if (warriorSpikeCastAt < 0f && attackAudio && attackAudio.isPlaying &&
                                attackAudio.clip && attackAudio.clip.name == "OriginalGhostKingCast" &&
                                (attackAudio.transform.localPosition - Vector3.up * 4.647f).sqrMagnitude < .0001f)
                                warriorSpikeCastAt = Time.time;
                            var impact = combat.WarriorSecondaryAudio;
                            if (warriorSpikeImpactAt < 0f && impact && impact.isPlaying && impact.clip &&
                                impact.clip.name == (expectedWarriorSpikes == 18 ? "OriginalWarriorSpikeCone" : "OriginalGhostKingImpact") &&
                                (impact.transform.localPosition - Vector3.up * 4.647f).sqrMagnitude < .0001f)
                                warriorSpikeImpactAt = Time.time;
                            if (warriorWarnings == 0)
                            {
                                var warnings = Array.FindAll(actor.GetComponentsInChildren<LineRenderer>(), line => line.enabled);
                                warriorWarnings = warnings.Length;
                                warriorSpikeLayout = VerifyWarriorSpikeLayout(warnings, actor.transform.position, expectedWarriorSpikes == 18);
                            }
                            if (warriorSpikeCastAt >= 0f && actor.GetComponentInChildren<LineRenderer>() is { enabled: true } firstWarning)
                            {
                                float elapsed = Time.time - warriorSpikeCastAt;
                                if (warriorTurnStage == 0 && elapsed >= .35f)
                                {
                                    warriorFixtureYaw = actor.lookInput.y;
                                    warriorFixtureOrigin = actor.transform.position;
                                    SetSmokePlayerPosition(target, warriorFixtureOrigin +
                                        Quaternion.Euler(0f, warriorFixtureYaw + 35f, 0f) * Vector3.forward * 7f);
                                    warriorTurnStage++;
                                }
                                else if (warriorTurnStage == 1 && elapsed >= 1.2f)
                                {
                                    warriorTrackedPoint = firstWarning.GetPosition(0);
                                    var expectedCenter = warriorFixtureOrigin + Quaternion.Euler(0f, warriorFixtureYaw + 35f, 0f) *
                                        Vector3.forward * (expectedWarriorSpikes == 18 ? 5f : 1.4f);
                                    var center = (warriorTrackedPoint + firstWarning.GetPosition(24)) * .5f;
                                    warriorTracked = Mathf.Abs(Mathf.DeltaAngle(actor.lookInput.y, warriorFixtureYaw + 35f)) < 2f &&
                                        HorizontalDistanceSquared(center, expectedCenter) < .04f;
                                    warriorTurnStage++;
                                }
                                else if (warriorTurnStage == 2 && elapsed >= 1.72f)
                                {
                                    SetSmokePlayerPosition(target, warriorFixtureOrigin +
                                        Quaternion.Euler(0f, warriorFixtureYaw - 35f, 0f) * Vector3.forward * 7f);
                                    warriorTurnStage++;
                                }
                                else if (warriorTurnStage == 3 && elapsed >= 1.90f)
                                {
                                    warriorLocked = Mathf.Abs(Mathf.DeltaAngle(actor.lookInput.y, warriorFixtureYaw + 35f)) < 2f &&
                                        HorizontalDistanceSquared(firstWarning.GetPosition(0), warriorTrackedPoint) < .0025f;
                                    SetSmokePlayerPosition(target, warriorFixtureOrigin +
                                        Quaternion.Euler(0f, warriorFixtureYaw + 35f, 0f) * Vector3.forward * 7f);
                                    warriorTurnStage++;
                                }
                            }
                        }
                        selectedAttackSound = rainVisual ? rainVisual.SelectedAttackSound : attackAudio ? attackAudio.clip : null;
                        if (verifyQueen && rainVisual && rainVisual.AttackAudioPlaying)
                            queenSpitAudio |= selectedAttackSound && selectedAttackSound.name == "OriginalAcidSpit";
                        if (verifyGup && rainVisual && rainVisual.AttackAudioPlaying)
                            gupCharge |= selectedAttackSound && selectedAttackSound.name == "OriginalGupCharge";
                        if (gupBone && actor.RainEnemyAnimation == 2)
                        {
                            if (!firstGupScale.HasValue) firstGupPosition = gupBone.localPosition;
                            firstGupScale ??= gupBone.localScale;
                            gupPoseSamples++;
                            gupPoseChanged |= (gupBone.localScale - firstGupScale.Value).sqrMagnitude > .00001f ||
                                (gupBone.localPosition - firstGupPosition).sqrMagnitude > .00001f;
                        }
                        if (jotunnVisual)
                        {
                            var motion = jotunnVisual.CurrentMotion;
                            if (skullTail && motion == Skeleton1HVisual.Motion.Walk)
                            {
                                var pose = skullTail.localRotation;
                                firstTailPose ??= pose;
                                tailAngle = Mathf.Max(tailAngle, Quaternion.Angle(firstTailPose.Value, pose));
                                tailAnimated |= tailAngle > .2f;
                            }
                            attacked |= motion != Skeleton1HVisual.Motion.Spawn && motion != Skeleton1HVisual.Motion.Idle &&
                                motion != Skeleton1HVisual.Motion.Walk && motion != Skeleton1HVisual.Motion.Hit &&
                                motion != Skeleton1HVisual.Motion.PhaseChange;
                        }
                        else attacked |= actor.RainTelegraph != 0;
                        if (captureDirectory != null && (attacked || (verifyGhost || rainEnemy) && cueSeen) && !attackFrameCaptured)
                        {
                            attackFrameCaptured = true;
                            CaptureMonsterFrame(captureDirectory, id, "attack", actor);
                            if (aimMeleeFrames)
                                CaptureMonsterFrame(captureDirectory, id, "attack-aimed", actor, aimAtActor: true);
                        }
                        if (id == "GREATERWISP_BODY" && !greaterWispShotLogged && actor.RainGreaterWispShots.Count > 0)
                        {
                            greaterWispShotLogged = true;
                            foreach (var shot in actor.RainGreaterWispShots.Values)
                            {
                                bool wall = Physics.SphereCast(shot.Origin, .5f, shot.Direction, out var collision, 30f,
                                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                                Debug.Log($"MONSTER_GREATERWISP_SHOT origin={shot.Origin:F3} direction={shot.Direction:F3} target={target.CombatAimPoint:F3} wall={wall} contact={(wall ? collision.point : Vector3.zero):F3} health={target.CurrentHealth}");
                            }
                        }
                        bool arrowInFlight = false;
                        if (captureDirectory != null && !signatureFrameCaptured && (id is "skeleton_bow_VarRegular" or "elf_ranged_sur" or "skeleton_bow_sphinx" or "hyb_sub_boss_sur_VarEasy" or "hyb_sub_boss_sur_VarNormal"))
                        {
                            var arrow = FindFirstObjectByType<SkeletonBowArrow>();
                            arrowInFlight = arrow && (arrow.transform.position - actor.CombatAimPoint).sqrMagnitude > 1f;
                        }
                        if (captureDirectory != null && !signatureFrameCaptured && (id is "CHILD_BODY" or "CLAYGRENADIER_BODY" or "MINIMUSHROOM_BODY" or "BELL_BODY"))
                        {
                            var orb = actor.transform.Find("Pending ranged orb");
                            arrowInFlight |= orb && orb.gameObject.activeSelf && (orb.position - actor.CombatAimPoint).sqrMagnitude > 1f;
                        }
                        if (captureDirectory != null && !signatureFrameCaptured &&
                            (id == "ACIDLARVA_BODY" && actor.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLeapLoopAnimation ||
                             id == "LUNAREXPLODER_BODY" && actor.RainEnemyAnimation == 2 ||
                             id == "PARENT_BODY" && actor.RainEnemyAnimation == RangeResearchEnemy.ParentSlamAnimation ||
                             id == "GREATERWISP_BODY" && actor.RainEnemyAnimation == 25 ||
                             verifyTeleport && reappeared && cueSeen ||
                             arrowInFlight))
                        {
                            signatureFrameCaptured = true;
                            CaptureMonsterFrame(captureDirectory, id, "signature", actor, aimAtActor: true);
                        }
                        if (captureDirectory != null && !releaseFrameCaptured && attackAudio && attackAudio.isPlaying)
                        {
                            releaseFrameCaptured = true;
                            CaptureMonsterFrame(captureDirectory, id, "release", actor);
                        }
                        if (captureDirectory != null && !impactFrameCaptured && target.CurrentHealth < health)
                        {
                            impactFrameCaptured = true;
                            CaptureMonsterFrame(captureDirectory, id, "impact", actor);
                            if (aimMeleeFrames)
                                CaptureMonsterFrame(captureDirectory, id, "impact-aimed", actor, aimAtActor: true);
                        }
                    }
                    if (cancelConstructLaser && actor && attackAudio && attackAudio.isPlaying &&
                        attackAudio.clip && attackAudio.clip.name.StartsWith("OriginalLaser", StringComparison.Ordinal))
                    {
                        var beams = actor.GetComponentsInChildren<LineRenderer>();
                        if (beams.Length == 7 && Array.TrueForAll(beams, beam => beam.enabled))
                        {
                            int beforeCancel = target.CurrentHealth;
                            JotunnMonsterGm.SetCombat(target, false);
                            yield return new WaitForSeconds(2.1f);
                            bool clean = target && target.CurrentHealth == beforeCancel && combat.ConstructLaserHits == 0 &&
                                !attackAudio.isPlaying && Array.TrueForAll(beams, beam => !beam.enabled);
                            Debug.Log($"{marker}_CONSTRUCT_LASER_CANCEL_{(clean ? "PASS" : "FAIL")} beams={beams.Length} damageAfterCancel={beforeCancel - target.CurrentHealth} audioStopped={!attackAudio.isPlaying}");
                            break;
                        }
                    }
                    if (verifyMageFire) lastHealth = target.CurrentHealth;
                    damage = Mathf.Max(damage, health - target.CurrentHealth);
                    if ((verifyHorrorAlf || id == "horror_expl_jotun_sur") && damage > 0)
                        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                            if (source.isPlaying && source.clip && source.clip.name == "OriginalAlfExplosion")
                            {
                                audioStarted = true;
                                selectedAttackSound = source.clip;
                                attackAudio = source;
                                if (sampleAttackAudio)
                                {
                                    source.GetOutputData(attackSamples, 0);
                                    foreach (float sample in attackSamples) attackSourcePeak = Mathf.Max(attackSourcePeak, Mathf.Abs(sample));
                                    AudioListener.GetOutputData(attackSamples, 0);
                                    foreach (float sample in attackSamples) listenerPeak = Mathf.Max(listenerPeak, Mathf.Abs(sample));
                                }
                            }
                    if (verifyGup && damage > 0 && !gupRelease)
                        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                            gupRelease |= source.isPlaying && source.clip && source.clip.name == "OriginalGupAttack";
                    if (damage > 0) attacked = true;
                    if ((id is "CHILD_BODY" or "CLAYGRENADIER_BODY" or "CLAYBRUISER_BODY" or "MINIMUSHROOM_BODY" or "BELL_BODY" or "elf_woman_sur" or "elf_woman_02_sur" or "skeleton_bow_VarRegular" or "skeleton_guardian_sur" or "skeleton_1h_sphinx_sur" or "PARENT_BODY" or "skeleton_goat_sur_boss_VarEasy" or "jotunn_construct_sur" or "hyb_sub_boss_sur_VarEasy" or "hyb_sub_boss_sur_VarNormal") && damage > 0)
                    {
                        if (parentHitObservedAt < 0f) parentHitObservedAt = Time.realtimeSinceStartup;
                        if (Time.realtimeSinceStartup < parentHitObservedAt + .8f)
                        {
                            yield return null;
                            continue;
                        }
                    }
                    if (captureDirectory != null && (id is "hyb_sub_boss_sur_VarEasy" or "hyb_sub_boss_sur_VarNormal") && !signatureFrameCaptured && actor)
                    {
                        yield return null;
                        continue;
                    }
                    if (verifyElfExpiry)
                    {
                        foreach (var spawnedSpider in NetworkServer.spawned)
                        {
                            if (elfExpiryIds.Count >= 6) break;
                            if (spawnedSpider.Value.TryGetComponent(out Skeleton1HCombat summoned) && summoned.SuppressSummonRewards)
                                elfExpiryIds.TryAdd(spawnedSpider.Key, Time.time);
                        }
                        if (elfExpiryIds.Count == 6)
                        {
                            elfExpiredCount = 0;
                            foreach (var spawnedSpider in elfExpiryIds)
                            {
                                if (NetworkServer.spawned.ContainsKey(spawnedSpider.Key)) continue;
                                elfExpiredCount++;
                                if (observedElfExpiries.Add(spawnedSpider.Key) && (Time.time - spawnedSpider.Value < 9.8f || Time.time - spawnedSpider.Value > 10.25f))
                                    elfExpiryTiming = false;
                            }
                        }
                    }
                    if (verifyElfSummons && (!verifyElfExpiry || elfExpiredCount == 6) && combat.ElfSummons &&
                        combat.ElfSummons.ElfNormalSpawned >= 6 &&
                        (id != "elf_woman_02_sur" || combat.ElfSummons.ElfLargeSpawned >= 4)) break;
                    if (verifyWarrior && (verifyWarriorSpikes ? combat.WarriorSpikesResolved == expectedWarriorSpikes :
                        combat.WarriorStompHits > 0 && Time.time >= warriorHitAt + 1.6f))
                        break;
                    if (verifyElfWoman && combat.ElementalSpikeWarnings < (id == "elf_woman_02_sur" ? 7 : 4) && actor)
                    {
                        yield return null;
                        continue;
                    }
                    if (sampleAttackAudio && (attackSourcePeak <= requiredAudioPeak || listenerPeak <= requiredAudioPeak) && (actor || attackAudio && attackAudio.isPlaying) && target.CurrentHealth > 0)
                    {
                        yield return null;
                        continue;
                    }
                    if (verifyChampionMissiles && combat.ChampionMissilesReleased >= 3 && combat.ChampionMissileDamage > 0 || !verifyChampionMissiles && (verifyElementalMelee || verifyElfWoman || verifyGhostKing) && combat.ElementalSpikeHits > 0 && cueSeen || verifyMageFire && mageFireDamage > 0 && cueSeen || (!verifyWarrior && !verifyMageFire && moved && damage > 0 && (!verifyBird || combat.DiveMoved && birdCastSound && birdImpactSound && cueSeen) && (!verifyPoisonMage || combat.PoisonOrbDamage > 0 && poisonOrbAudioPlayed && (!verifyPlagueSplash || combat.PlagueSplashLanded > 0)) && (!verifyMage || combat.DiveMoved && combat.DiveHits > 0 && cueSeen) && !verifyHorrorAlf && !verifyExplosiveBeetle && (!verifyTeleport || reappeared && cueSeen) && (!verifyGup || gupRelease) && (!verifyGoatNormal || goatJumpHit && goatJumpLaunchPlayed) && (!verifyMuspel || combat.MuspelWaveDamage > 0 && cueSeen && audioStarted) && (!verifyEttin || ettinBlastSources.Count == 15 && combat.EttinLightningDamage > 0) && (!verifyEttinEndless || novaImpact && novaLayer && combat.EttinNovaHits > 0 && maxPoisonShots == EttinPoisonVolley.ShotCount && poisonAudio && poisonWarning)) ||
                        verifyConstruct && (constructLaser ? combat.ConstructLaserHits > 0 : constructFlame ? combat.ConstructFlameHits > 0 : constructHammer ? attacked && damage > 0 : combat.ConstructSawHits >= 2) && cueSeen || target.CurrentHealth <= 0 || !actor) break;
                    yield return null;
                }

                // Ranged actors can fire from the spawn point. Give them a second look outside attack range.
                if (!verifyChampionMissiles && !verifyConstruct && !moved && damage > 0 && target && target.CurrentHealth > 0 && actor &&
                    TryFindMonsterChasePoint(id, actor.transform.position, out var chasePoint))
                {
                    SetSmokePlayerPosition(target, chasePoint);
                    Debug.Log($"MONSTER_CHASE_POINT {id} origin={actor.transform.position:F3} player={chasePoint:F3}");
                    float chaseDeadline = Time.realtimeSinceStartup + (id == "BEETLEQUEEN_BODY" ? 16f : 8f);
                    while (actor && target && target.CurrentHealth > 0 && Time.realtimeSinceStartup < chaseDeadline)
                    {
                        moved = HorizontalDistanceSquared(actor.transform.position, idlePosition) >= .04f;
                        if (moved) break;
                        if (id == "PARENT_BODY" && (Time.frameCount % 60) == 0)
                        {
                            var controller = actor.GetComponent<CharacterController>();
                            var motor = actor.GetComponent<CharacterMotor>();
                            Debug.Log($"PARENT_CHASE_DIAGNOSTIC input={actor.movementInput:F3} blocked={actor.Block} motor={motor.enabled} controller={controller.enabled} grounded={controller.isGrounded} collision={controller.collisionFlags} velocity={controller.velocity:F3} position={actor.transform.position:F3}");
                        }
                        yield return null;
                    }
                    SetSmokePlayerPosition(target, originalPosition);
                }

                string rangedSound = id switch
                {
                    "LEMURIAN_BODY" => "OriginalFireball",
                    "BEETLEGUARD_BODY" => "OriginalSunder",
                    _ => null
                };
                bool rangedCue = false;
                int rangedDamage = 0;
                if (rangedSound != null && actor && rainVisual && target && target.CurrentHealth > 0)
                {
                    Vector3 center = originalPosition + Vector3.up * 30f;
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = center - Vector3.up * .5f;
                    floor.transform.localScale = new Vector3(42f, 1f, 42f);
                    Physics.SyncTransforms();
                    float distance = id == "LEMURIAN_BODY" ? 18f : 13f;
                    SetSmokePlayerPosition(actor, center + Vector3.up * .15f);
                    SetSmokePlayerPosition(target, center + Vector3.forward * distance + Vector3.up * .15f);
                    int rangedHealth = target.CurrentHealth;
                    float rangedDeadline = Time.realtimeSinceStartup + 8f;
                    while (actor && target && target.CurrentHealth > 0 && Time.realtimeSinceStartup < rangedDeadline)
                    {
                        var clip = rainVisual.SelectedAttackSound;
                        rangedCue |= clip && clip.name == rangedSound;
                        if (rangedCue)
                        {
                            rangedDamage = Mathf.Max(0, rangedHealth - target.CurrentHealth);
                            if (rangedDamage > 0) break;
                        }
                        yield return null;
                    }
                    SetSmokePlayerPosition(target, originalPosition);
                    Destroy(floor);
                }

                int laserHits = constructLaser && combat ? combat.ConstructLaserHits : 0;
                int laserBeams = constructLaser && combat ? combat.ConstructLaserBeamCount : 0;
                bool laserCleared = false;
                int flameHits = constructFlame && combat ? combat.ConstructFlameHits : 0;
                var flameMotion = constructFlame ? jotunnVisual.CurrentMotion : default;
                var elfSummons = verifyElfSummons && combat ? combat.ElfSummons : null;
                int elfNormalCount = elfSummons ? elfSummons.ElfNormalSpawned : 0;
                int elfLargeCount = elfSummons ? elfSummons.ElfLargeSpawned : 0;
                int elfActive = elfSummons ? elfSummons.ActiveSummons : 0;
                bool elfSummonCleanup = false;
                int plagueLanded = verifyPlagueSplash && combat ? combat.PlagueSplashLanded : 0;
                int poisonOrbDamage = verifyPoisonMage && combat ? combat.PoisonOrbDamage : 0;
                int championDamage = verifyChampionMissiles && combat ? combat.ChampionMissileDamage : 0;
                int championShots = verifyChampionMissiles && combat ? combat.ChampionMissilesReleased : 0;
                int elementalWarnings = (verifyElementalMelee || verifyElfWoman || verifyGhostKing) && combat ? combat.ElementalSpikeWarnings : 0;
                int elementalHits = (verifyElementalMelee || verifyElfWoman || verifyGhostKing) && combat ? combat.ElementalSpikeHits : 0;
                int mageFireReleased = verifyMageFire && combat ? combat.MageFireReleased : 0;
                var activeSaw = verifyConstruct ? FindFirstObjectByType<ConstructSaw>() : null;
                bool sawWasActive = activeSaw;
                bool constructAudioWasPlaying = verifyConstruct && attackAudio && attackAudio.isPlaying;
                bool hammerSoundAtImpact = constructHammer && constructAudioWasPlaying && attackAudio.clip &&
                    attackAudio.clip.name.StartsWith("OriginalHammerSmash", StringComparison.Ordinal) &&
                    Mathf.Abs(Vector3.ProjectOnPlane(attackAudio.transform.position - idlePosition, Vector3.up).magnitude -
                        (constructFar ? 10.5f : 7.3f)) < .05f;
                int sawHits = verifyConstruct && combat ? combat.ConstructSawHits : 0;
                bool selfDestructed = (verifyHorrorAlf || verifyExplosiveBeetle) && !NetworkServer.spawned.ContainsKey(identity);
                if (verifyWarriorSpikes && actor && combat.WarriorSpikesResolved == expectedWarriorSpikes)
                {
                    SetSmokePlayerPosition(actor, idlePosition);
                    SetSmokePlayerPosition(target, idlePosition + Vector3.forward * 7f);
                    JotunnMonsterGm.SetCombat(target, true);
                    float cancelDeadline = Time.realtimeSinceStartup + 4f;
                    while (actor && Time.realtimeSinceStartup < cancelDeadline &&
                        !Array.Exists(actor.GetComponentsInChildren<LineRenderer>(), line => line.enabled))
                        yield return null;
                    bool pendingWarnings = actor && Array.Exists(actor.GetComponentsInChildren<LineRenderer>(), line => line.enabled);
                    int healthBeforeStop = target.CurrentHealth;
                    int spikeHitsBeforeStop = combat.WarriorSpikeHits;
                    bool castWasPlaying = attackAudio && attackAudio.isPlaying;
                    JotunnMonsterGm.SetCombat(target, false);
                    bool audioStopped = castWasPlaying && !attackAudio.isPlaying &&
                        combat.WarriorSecondaryAudio && !combat.WarriorSecondaryAudio.isPlaying;
                    yield return new WaitForSeconds(2.9f);
                    bool warningsCleared = actor && Array.TrueForAll(actor.GetComponentsInChildren<LineRenderer>(), line => !line.enabled);
                    bool spikeDamageStopped = combat.WarriorSpikeHits == spikeHitsBeforeStop;
                    warriorSpikesCleared = pendingWarnings && warningsCleared && spikeDamageStopped && audioStopped &&
                        combat.WarriorSpikesResolved == expectedWarriorSpikes;
                    Debug.Log($"{marker}_WARRIOR_SPIKES_STOP {id} pending={pendingWarnings} cleared={warningsCleared} spikeDamageStopped={spikeDamageStopped} audioStopped={audioStopped} resolved={combat.WarriorSpikesResolved} playerHealth={healthBeforeStop}->{target.CurrentHealth}");
                }
                if (jotunn)
                {
                    JotunnMonsterGm.SetCombat(target, false);
                    if (verifyElfWoman && actor)
                    {
                        int stoppedHealth = target.CurrentHealth;
                        int stoppedSpikeHits = combat.ElementalSpikeHits;
                        yield return new WaitForSeconds(1.7f);
                        bool hidden = Array.TrueForAll(actor.GetComponentsInChildren<Transform>(true), t => t.name != "Elf spike impact" || !t.gameObject.activeSelf);
                        bool quiet = Array.TrueForAll(actor.GetComponentsInChildren<AudioSource>(), source => !source.isPlaying);
                        bool noLateDamage = target.CurrentHealth >= stoppedHealth && combat.ElementalSpikeHits == stoppedSpikeHits;
                        if (!hidden || !quiet || !noLateDamage) failures++;
                        Debug.Log($"MONSTER_ELF_SPIKE_STOP {id} hidden={hidden} quiet={quiet} noLateDamage={noLateDamage} health={stoppedHealth}->{target.CurrentHealth} hits={stoppedSpikeHits}->{combat.ElementalSpikeHits}");
                    }
                    if (verifyWarrior && combat && combat.WarriorStompImpactArt)
                        warriorImpactCleared = !combat.WarriorStompImpactArt.activeSelf &&
                            combat.WarriorStompCrater && !combat.WarriorStompCrater.gameObject.activeInHierarchy;
                    constructAudioWasPlaying &= attackAudio && !attackAudio.isPlaying;
                    if (constructLaser)
                        laserCleared = Array.TrueForAll(actor.GetComponentsInChildren<LineRenderer>(), line => !line.enabled);
                    if (verifyElfSummons)
                        elfSummonCleanup = elfSummons && elfActive > 0 && elfSummons.ActiveSummons == 0;
                    JotunnMonsterGm.Remove(target, id);
                    if (warriorFloor)
                    {
                        SetSmokePlayerPosition(target, originalPosition);
                        Destroy(warriorFloor);
                    }
                    if (changedCourse) SetSmokePlayerPosition(target, originalPosition);
                }
                else
                {
                    lab.SetMonsterCombat(false);
                    if (id is "CHILD_BODY" or "CLAYGRENADIER_BODY" or "MINIMUSHROOM_BODY" or "BELL_BODY")
                    {
                        lab.SetMonsterCombat(true);
                        float cancelDeadline = Time.realtimeSinceStartup + 8f;
                        while (actor && !rainEnemy.PendingOrbVisible && Time.realtimeSinceStartup < cancelDeadline) yield return null;
                        bool observedOrb = actor && rainEnemy.PendingOrbVisible;
                        int stoppedHealth = target.CurrentHealth;
                        lab.SetMonsterCombat(false);
                        yield return new WaitForSeconds(1.2f);
                        bool cancelled = actor && !rainEnemy.PendingOrbVisible && target.CurrentHealth >= stoppedHealth && (!attackAudio || !attackAudio.isPlaying);
                        if (!observedOrb || !cancelled) failures++;
                        Debug.Log($"MONSTER_PENDING_ORB_CANCEL {id} observed={observedOrb} cancelled={cancelled} orbVisible={rainEnemy.PendingOrbVisible} audioPlaying={(attackAudio && attackAudio.isPlaying)} health={stoppedHealth}->{target.CurrentHealth}");
                    }
                    if (id == "CLAYBRUISER_BODY")
                    {
                        lab.SetMonsterCombat(true);
                        float clayDeadline = Time.realtimeSinceStartup + 8f;
                        while (actor && !rainEnemy.ClayTracerVisible && Time.realtimeSinceStartup < clayDeadline) yield return null;
                        bool observed = actor && rainEnemy.ClayTracerVisible;
                        int stoppedHealth = target.CurrentHealth;
                        lab.SetMonsterCombat(false);
                        yield return new WaitForSeconds(1.2f);
                        bool cancelled = actor && !rainEnemy.ClayTracerVisible && target.CurrentHealth >= stoppedHealth && (!attackAudio || !attackAudio.isPlaying);
                        if (!observed || !cancelled) failures++;
                        Debug.Log($"CLAY_BURST_CANCEL observed={observed} cancelled={cancelled} health={stoppedHealth}->{target.CurrentHealth}");
                    }
                    lab.RemoveCatalog(entry);
                    if (movedPlayerForRangedAttack) SetSmokePlayerPosition(target, originalPosition);
                }
                yield return null;
                bool removed = !NetworkServer.spawned.ContainsKey(identity);
                if (target && target.CurrentHealth > 0)
                {
                    target.CurrentHealth = health;
                    target.RangeInvulnerable = invulnerable;
                }
                if (constructLaser && !cancelConstructLaser)
                {
                    bool laserPass = idle && model && !moved && cueSeen && laserHits == 1 && laserBeams == 7 &&
                        removed && audioStarted && constructAudioWasPlaying && laserCleared && selectedAttackSound &&
                        selectedAttackSound.name.StartsWith("OriginalLaser", StringComparison.Ordinal);
                    Debug.Log($"{marker}_CONSTRUCT_LASER_{(laserPass ? "PASS" : "FAIL")} beams={laserBeams} hits={laserHits} damage={damage} warning={cueSeen} soundStopped={constructAudioWasPlaying} cleared={laserCleared}");
                }
                else if (constructFlame)
                {
                    bool flamePass = idle && model && !moved && cueSeen && flameHits > 0 && removed && audioStarted && constructAudioWasPlaying;
                    Debug.Log($"{marker}_CONSTRUCT_FLAME_{(flamePass ? "PASS" : "FAIL")} motion={flameMotion} hits={flameHits} damage={damage} warning={cueSeen} soundStopped={constructAudioWasPlaying}");
                }
                else if (constructHammer)
                {
                    SetSmokePlayerPosition(target, constructPlayerStart);
                    bool hammerPass = idle && model && !moved && cueSeen && attacked && damage > 0 && removed &&
                        hammerSoundAtImpact && constructAudioWasPlaying;
                    Debug.Log($"{marker}_CONSTRUCT_HAMMER_{(hammerPass ? "PASS" : "FAIL")} far={constructFar} damage={damage} warning={cueSeen} removed={removed} soundAtImpact={hammerSoundAtImpact} soundStopped={constructAudioWasPlaying}");
                }
                else if (verifyConstruct && !cancelConstructLaser)
                {
                    bool cleaned = sawWasActive && !activeSaw;
                    bool stationaryPass = idle && model && moved && sawHits >= 1 && cueSeen && damage > 0 && cleaned && removed && audioStarted && constructAudioWasPlaying;
                    Debug.Log($"{marker}_CONSTRUCT_PLAYABILITY_{(stationaryPass ? "PASS" : "FAIL")} stationary={!moved} sawHits={sawHits} warning={cueSeen} sawCleaned={cleaned} sawAudioStopped={constructAudioWasPlaying}");
                }
                bool birdPass = !verifyBird || combat.DiveMoved && combat.DiveHits > 0 && birdCastSound && birdImpactSound && cueSeen;
                if (verifyBird) Debug.Log($"{marker}_BIRD {id} diveMoved={combat.DiveMoved} hits={combat.DiveHits} castSound={birdCastSound} impactSound={birdImpactSound} warning={cueSeen}");
                bool mageAudio = verifyMage && audioStarted && selectedAttackSound && selectedAttackSound.name == "OriginalMageJump";
                bool magePass = !verifyMage || combat.DiveMoved && combat.DiveHits > 0 && cueSeen && mageAudio;
                if (verifyMage) Debug.Log($"{marker}_MAGE {id} jumpMoved={combat.DiveMoved} hits={combat.DiveHits} warning={cueSeen} material={model} castAudio={mageAudio}");
                bool mageFirePass = verifyMageFire && idle && model && attacked && removed && cueSeen && mageFireSound &&
                    mageFireReleased >= 7 && mageFireReleased % 7 == 0 && mageFireDamage > 0 &&
                    Array.TrueForAll(FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None),
                        arrow => arrow.name != "Elemental mage fire ring");
                if (verifyMageFire)
                    Debug.Log($"{marker}_MAGE_FIRE_{(mageFirePass ? "PASS" : "FAIL")} shots={mageFireReleased} damage={mageFireDamage} originalAudio={mageFireSound} warning={cueSeen} removed={removed}");
                if (verifyElementalMelee)
                    Debug.Log($"{marker}_ELEMENTAL_MELEE {id} warnings={elementalWarnings} hits={elementalHits} moved={moved} cue={cueSeen}");
                bool elementalMeleeAudio = verifyElementalMelee && audioStarted && selectedAttackSound &&
                    selectedAttackSound.name.StartsWith("OriginalElementalSpikes", StringComparison.Ordinal);
                bool elfWomanPass = !verifyElfWoman || elementalWarnings >= (id == "elf_woman_02_sur" ? 7 : 4) &&
                    elementalHits > 0 && cueSeen && elfCastAudio && elfBlastAudio;
                if (verifyElfWoman)
                    Debug.Log($"{marker}_ELF_WOMAN {id} warnings={elementalWarnings} hits={elementalHits} moved={moved} material={model} castAudio={elfCastAudio} blastAudio={elfBlastAudio}");
                float warriorReleaseDelay = warriorWarningAt - warriorAudioAt;
                float warriorFuse = warriorHitAt - warriorWarningAt;
                float warriorSpikeAudioDelay = warriorSpikeImpactAt - warriorSpikeCastAt;
                bool warriorSpikeAudio = warriorSpikeCastAt >= 0f && warriorSpikeAudioDelay is > 1.96f and < 2.18f;
                bool warriorPass = !verifyWarrior || (verifyWarriorSpikes
                    ? warriorSpikeMotion && warriorSpikeLayout && warriorTracked && warriorLocked && warriorWarnings == expectedWarriorSpikes && combat.WarriorSpikesResolved == expectedWarriorSpikes && combat.WarriorSpikeHits > 0 && warriorSpikesCleared && warriorSpikeAudio
                    : combat.WarriorStompHits > 0 && cueSeen && warriorLayeredAudio && warriorStompArtLayers == 15 && warriorStompArtCleared &&
                      warriorImpactLayers == 0x7fffe && warriorImpactCleared && warriorCraterPhases == 7 &&
                      warriorAudioAt >= 0f && warriorReleaseDelay is > .10f and < .29f && warriorFuse is > 1.24f and < 1.46f);
                if (verifyWarriorSpikes) Debug.Log($"{marker}_WARRIOR_SPIKES {id} warnings={warriorWarnings} resolved={combat.WarriorSpikesResolved} hits={combat.WarriorSpikeHits} animation={warriorSpikeMotion} layout={warriorSpikeLayout} tracked={warriorTracked} locked={warriorLocked} cancelled={warriorSpikesCleared} originalAudio={warriorSpikeAudio} audioDelay={warriorSpikeAudioDelay:F3}");
                else if (verifyWarrior) Debug.Log($"{marker}_WARRIOR {id} stompHits={combat.WarriorStompHits} layeredAudio={warriorLayeredAudio} warning={cueSeen} artLayers={warriorStompArtLayers:X} artCleared={warriorStompArtCleared} impactLayers={warriorImpactLayers:X} impactCleared={warriorImpactCleared} craterPhases={warriorCraterPhases} release={warriorReleaseDelay:F3} fuse={warriorFuse:F3} moved={moved} material={model}");
                bool ghostKingPass = !verifyGhostKing || elementalWarnings >= 4 && elementalHits > 0 && cueSeen && ghostKingCastPlayed && ghostKingImpactPlayed;
                if (verifyGhostKing)
                    Debug.Log($"{marker}_GHOST_KING warnings={elementalWarnings} hits={elementalHits} moved={moved} model={model} castAudio={ghostKingCastPlayed} impactAudio={ghostKingImpactPlayed}");
                bool elementalMeleePass = !verifyElementalMelee || elementalWarnings > 0 && elementalHits > 0 && cueSeen && elementalMeleeAudio;
                bool championPass = false;
                if (verifyChampionMissiles)
                {
                    SetSmokePlayerPosition(target, originalPosition);
                    bool cleared = Array.TrueForAll(FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None),
                        shot => shot.name != "Elemental champion missile");
                    championPass = idle && model && attacked && cueSeen && championShots >= 3 && championDamage > 0 && championAudioPlayed && cleared && removed;
                    Debug.Log($"{marker}_CHAMPION_MISSILES_{(championPass ? "PASS" : "FAIL")} shots={championShots} damage={championDamage} audio={championAudioPlayed} cleared={cleared} warning={cueSeen} removed={removed}");
                }
                bool plaguePass = !verifyPlagueSplash || plagueLanded > 0 && plagueFlightClip != null && !plagueFlight && !GameObject.Find("Elemental plague splash");
                if (verifyPlagueSplash)
                    Debug.Log($"{marker}_PLAGUE_SPLASH landed={plagueLanded} flightAudio={plagueFlightClip ?? "none"} cleared={plaguePass}");
                bool poisonMagePass = !verifyPoisonMage || poisonOrbDamage > 0 && cueSeen && poisonOrbAudioPlayed &&
                    Array.TrueForAll(FindObjectsByType<SkeletonBowArrow>(FindObjectsSortMode.None),
                        orb => orb.name != "Elemental poison orb");
                if (verifyPoisonMage)
                    Debug.Log($"{marker}_POISON_MAGE {id} orbDamage={poisonOrbDamage} originalCastAudio={poisonOrbAudioPlayed} cleared={poisonMagePass} moved={moved} material={model}");
                bool elfSummonPass = verifyElfSummons && idle && model && elfNormalCount >= 6 &&
                    (id != "elf_woman_02_sur" || elfLargeCount >= 4) && elfSummonCleanup && removed && (!verifyElfExpiry || elfExpiredCount == 6 && elfExpiryTiming);
                if (verifyElfSummons)
                    Debug.Log($"{marker}_ELF_SUMMONS_{(elfSummonPass ? "PASS" : "FAIL")} {id} normal={elfNormalCount} large={elfLargeCount} activeBeforeStop={elfActive} cleared={elfSummonCleanup} expired={elfExpiredCount} expiryTiming={elfExpiryTiming}");
                bool passed = elfSummonPass || championPass || mageFirePass || !verifyMageFire && idle && model && moved && attacked && damage > 0 && removed && birdPass && magePass && elementalMeleePass && elfWomanPass && ghostKingPass && warriorPass && poisonMagePass && plaguePass &&
                    (!verifyEttinEndless || cueSeen && novaImpact && novaLayer && combat.EttinNovaHits > 0 && maxPoisonShots == EttinPoisonVolley.ShotCount && poisonAudio && poisonWarning) &&
                    (!verifyMuspel || combat.MuspelWaveDamage > 0 && cueSeen && audioStarted) &&
                    (!verifyEttin || cueSeen && audioStarted && ettinBlastSources.Count == 15 && combat.EttinLightningDamage > 0) &&
                    (!verifyGoatNormal || goatJumpWarning && goatJumpLaunchPlayed && goatJumpHit && audioStarted) &&
                    (!verifyTeleport || arrivedHidden && reappeared && cueSeen) &&
                    (!verifyQueen || cueSeen && queenSpitAudio) &&
                    (!verifyGup || damage == gupDamage && cueSeen && gupCharge && gupRelease && gupPoseChanged) &&
                    (!verifyHorrorAlf || cueSeen && audioStarted && selfDestructed) &&
                    (!verifyGhost || cueSeen && ghostCancellation) && (!followingGhost || changedCourse) &&
                    (!skullGhost || tailAnimated);
                if (sampleAttackAudio)
                {
                    Debug.Log($"{marker}_ATTACK_DSP {id} sourcePeak={attackSourcePeak:F6} listenerMixPeak={listenerPeak:F6}");
                    passed &= attackSourcePeak > requiredAudioPeak && listenerPeak > requiredAudioPeak;
                }
                if (verifyExplosiveBeetle) { passed &= beetleCancelled && beetleBurstSeen && selfDestructed; Debug.Log($"MONSTER_BEETLE_EXPLOSION {id} cancelled={beetleCancelled} burst={beetleBurstSeen} selfDestructed={selfDestructed}"); }
                if (!passed) failures++;
                if (verifyEttinEndless) Debug.Log($"{marker}_ETTIN_NOVA impact={novaImpact} layeredCast={novaLayer} knockbacks={combat.EttinNovaHits} poisonShots={maxPoisonShots} poisonAudio={poisonAudio} poisonWarning={poisonWarning}");
                if (verifyMuspel) Debug.Log($"{marker}_MUSPEL waveDamage={combat.MuspelWaveDamage} audio={audioStarted} warning={cueSeen}");
                if (verifyEttin) Debug.Log($"{marker}_ETTIN cast={audioStarted} impactSources={ettinBlastSources.Count} lightningDamage={combat.EttinLightningDamage}");
                if (verifyGoatNormal) Debug.Log($"{marker}_GOAT_JUMP {id} warning={goatJumpWarning} launchAudio={goatJumpLaunchPlayed} hitWithImpactAudio={goatJumpHit}");
                Debug.Log($"{marker}_{(passed ? "PASS" : "FAIL")} {id} spawn=True model={model} idle={idle} " +
                    $"moved={moved} attack={attacked} damage={damage} cue={cueSeen} " +
                    $"audioCue={(selectedAttackSound ? selectedAttackSound.name : "none")} audioStarted={audioStarted} removed={removed}");
                if (verifyTeleport)
                    Debug.Log($"{marker}_TELEPORT {id} arrivedHidden={arrivedHidden} reappeared={reappeared}");
                if (verifyHorrorAlf)
                    Debug.Log($"{marker}_HORROR_ALF selfDestructed={selfDestructed}");
                if (verifyGhost)
                    Debug.Log($"{marker}_GHOST_CANCEL {id} cancelledBeforeCharge={ghostCancellation}");
                if (followingGhost)
                    Debug.Log($"{marker}_GHOST_FOLLOW {id} targetShifted={changedCourse} damage={damage}");
                if (skullGhost)
                    Debug.Log($"{marker}_GHOST_SKULL_POSE {id} path={skullTailPath} sampled={firstTailPose.HasValue} tailAnimated={tailAnimated} angle={tailAngle}");
                if (verifyGup)
                    Debug.Log($"{marker}_GUP_PRESENTATION charge={gupCharge} release={gupRelease} spikePose={gupPoseChanged} samples={gupPoseSamples} path={gupBonePath}");
                if (rangedSound != null)
                    Debug.Log($"{marker}_RANGED {id} audioCue={rangedCue} damage={rangedDamage}");
            }
            if (target && target.CurrentHealth > 0)
            {
                target.MaxHealth = originalMaxHealth;
                target.CurrentHealth = originalHealth;
            }
            if (Array.IndexOf(args, "-gupSplitSmoke") >= 0)
                yield return VerifyGupSplit(lab, target, passed => { if (!passed) failures++; });
            ReportAlfheimSmoke(marker, tested > 0 && failures == 0, $"tested={tested} failed={failures}");
        }

        static IEnumerator VerifyGupSplit(RangeResearchLab lab, CharacterInstance player, Action<bool> report)
        {
            RangeResearchEnemy[] Family()
            {
                var result = new List<RangeResearchEnemy>();
                foreach (var actor in FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None))
                    if (actor.IsLabSummon && RangeResearchEnemy.IsGupFamilyKind(actor.Archetype)) result.Add(actor);
                return result.ToArray();
            }

            void Kill(RangeResearchEnemy actor)
            {
                var body = actor.GetComponent<CharacterInstance>();
                body.Server_ChangeHealthStateRaw(2000, CharacterPart.body, AttackType.hitscan, player, 0, 0f);
            }

            var entry = new BrowserEntry { id = "GUP_BODY", category = 1 };
            lab.ExecuteCatalog(entry);
            yield return null;
            var initial = Family();
            if (initial.Length != 1 || initial[0].Archetype != RangeResearchEnemy.GupKind)
            {
                Debug.LogError("MONSTER_PLAYTEST_GUP_SPLIT_MISSING_PARENT");
                report(false);
                yield break;
            }
            var parent = initial[0];
            uint parentId = parent.GetComponent<CharacterInstance>().netId;
            Kill(parent);
            yield return new WaitForSeconds(.2f);
            bool delayed = parent && Family().Length == 1 && parent.GetComponent<CharacterInstance>().RainEnemyAnimation == 8;
            bool audio = false;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                audio |= source.isPlaying && source.clip && source.clip.name == "OriginalGupDeath";
            yield return new WaitForSeconds(.45f);
            var geeps = Family();
            bool geepPair = geeps.Length == 2 && !NetworkServer.spawned.ContainsKey(parentId);
            foreach (var geep in geeps)
                geepPair &= geep.Archetype == RangeResearchEnemy.GeepKind &&
                    geep.GetComponent<CharacterInstance>().CurrentHealth == 500;
            if (geepPair)
            {
                foreach (var geep in geeps) Kill(geep);
                yield return new WaitForSeconds(.65f);
            }
            var gips = Family();
            bool gipQuartet = gips.Length == 4;
            foreach (var gip in gips)
                gipQuartet &= gip.Archetype == RangeResearchEnemy.GipKind &&
                    gip.GetComponent<CharacterInstance>().CurrentHealth == 250;
            if (gipQuartet)
            {
                foreach (var gip in gips) Kill(gip);
                yield return new WaitForSeconds(.65f);
            }
            bool cleared = Family().Length == 0;
            foreach (var actor in Family()) actor.RemoveFromRange();
            lab.ExecuteCatalog(entry);
            yield return null;
            var cancellation = Family();
            bool cancelled = false;
            if (cancellation.Length == 1 && cancellation[0].Archetype == RangeResearchEnemy.GupKind)
            {
                Kill(cancellation[0]);
                yield return new WaitForSeconds(.2f);
                lab.RemoveCatalog(entry);
                yield return new WaitForSeconds(.55f);
                cancelled = Family().Length == 0;
            }
            foreach (var actor in Family()) actor.RemoveFromRange();
            Debug.Log($"MONSTER_PLAYTEST_GUP_SPLIT delayed={delayed} geeps={geeps.Length} gips={gips.Length} cleared={cleared} cancelled={cancelled} audio={audio}");
            report(delayed && geepPair && gipQuartet && cleared && cancelled && audio);
        }

        static void CaptureMonsterFrame(string directory, string id, string phase, CharacterInstance actor, bool aimAtActor = false)
        {
            var camera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (!camera || !camera.isActiveAndEnabled)
            {
                Debug.LogWarning($"MONSTER_FRAME_UNAVAILABLE {id} {phase}: gameplay camera missing or disabled");
                return;
            }

            if (actor)
            {
                var rootViewport = camera.WorldToViewportPoint(actor.transform.position);
                Debug.Log($"MONSTER_FRAME_ACTOR {id} {phase} position={actor.transform.position:F3} viewport={rootViewport:F3}");
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    bool cameraIncludesLayer = (camera.cullingMask & (1 << skin.gameObject.layer)) != 0;
                    var bounds = skin.bounds;
                    var center = camera.WorldToViewportPoint(bounds.center);
                    if (skin.enabled && (id is "GUP_BODY" or "GEEP_BODY" or "GIP_BODY" or "ghost_skull_sur"))
                    {
                        var mesh = new Mesh();
                        skin.BakeMesh(mesh);
                        var bakedCenter = camera.WorldToViewportPoint(skin.transform.TransformPoint(mesh.bounds.center));
                        Debug.Log($"MONSTER_FRAME_BAKED {id} {phase} skin={skin.name} bounds={mesh.bounds.size:F3} viewport={bakedCenter:F3}");
                        Destroy(mesh);
                    }
                    var material = skin.sharedMaterial;
                    Debug.Log($"MONSTER_FRAME_SKIN {id} {phase} skin={skin.name} active={skin.gameObject.activeInHierarchy} " +
                        $"enabled={skin.enabled} forceOff={skin.forceRenderingOff} cameraLayer={cameraIncludesLayer} " +
                        $"bounds={bounds.size:F3} viewport={center:F3} shader={(material && material.shader ? material.shader.name : "none")}");
                }
            }

            var frame = RenderTexture.GetTemporary(960, 540, 24);
            var previous = RenderTexture.active;
            Quaternion originalCameraRotation = camera.transform.rotation;
            Texture2D pixels = null;
            try
            {
                if (aimAtActor && actor)
                {
                    var skin = Array.Find(actor.GetComponentsInChildren<SkinnedMeshRenderer>(), renderer => renderer.enabled);
                    if (skin) camera.transform.LookAt(skin.bounds.center);
                }
                var request = new RenderPipeline.StandardRequest { destination = frame };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    Debug.LogWarning($"MONSTER_FRAME_UNAVAILABLE {id} {phase}: render request unsupported");
                    return;
                }
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = frame;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
                System.IO.Directory.CreateDirectory(directory);
                string path = System.IO.Path.Combine(directory, $"{id}-{phase}.png");
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"MONSTER_FRAME {id} {phase} {path}");
            }
            finally
            {
                if (pixels) Destroy(pixels);
                if (aimAtActor) camera.transform.rotation = originalCameraRotation;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(frame);
            }
        }

        static IEnumerable<(string id, bool jotunn)> MonsterPlaytestOrder(string[] rainIds)
        {
            yield return ("skeleton_1h_sur", true);
            yield return ("milipede_sur", true);
            foreach (var (id, _) in JotunnMonsterGm.Catalog)
                if (id is not ("skeleton_1h_sur" or "milipede_sur" or "bat_loot_sur"))
                    yield return (id, true);
            foreach (string id in rainIds) yield return (id, false);
            string[] args = Environment.GetCommandLineArgs();
            foreach (string option in new[] { "-unregisteredJotunn", "-unregisteredRain" })
            {
                int index = Array.IndexOf(args, option) + 1;
                if (index <= 0 || index >= args.Length) continue;
                bool jotunn = option == "-unregisteredJotunn";
                foreach (string id in args[index].Split(','))
                {
                    if (string.IsNullOrWhiteSpace(id) || Array.IndexOf(rainIds, id) >= 0 || JotunnMonsterGm.Supports(id)) continue;
                    yield return (id, jotunn);
                }
            }
        }

        static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
        {
            Vector3 delta = first - second;
            return delta.x * delta.x + delta.z * delta.z;
        }

        static bool VerifyWarriorSpikeLayout(LineRenderer[] warnings, Vector3 origin, bool threeWay)
        {
            int lanes = threeWay ? 3 : 1;
            if (warnings.Length != 6 * lanes) return false;
            static Vector3 Center(LineRenderer line) => (line.GetPosition(0) + line.GetPosition(24)) * .5f;
            var start = Center(warnings[0]);
            var forward = Vector3.ProjectOnPlane(Center(warnings[lanes]) - start, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            if (HorizontalDistanceSquared(start, origin + forward * (threeWay ? 5f : 1.4f)) > .04f)
                return false;
            for (int step = 0; step < 6; step++)
            {
                int index = step * lanes;
                var center = start + forward * (step * 3f);
                if (HorizontalDistanceSquared(Center(warnings[index]), center) > .0025f) return false;
                var side = right * ((step + 2) * 1.5f);
                if (threeWay && (HorizontalDistanceSquared(Center(warnings[index + 1]), center - side) > .0025f ||
                    HorizontalDistanceSquared(Center(warnings[index + 2]), center + side) > .0025f)) return false;
            }
            return true;
        }

        static bool TryFindMonsterChasePoint(string id, Vector3 origin, out Vector3 point)
        {
            float distance = id switch
            {
                "jotunn_stone_sur" or "jotunn_stone_insane_sur" => 8f,
                "skeleton_mb_sphinx_sur" => 8f,
                "hyb_boss_sur_VarEasy" or "hyb_boss_sur_VarNormal" or "hyb_boss_sur_VarHard" => 34f,
                "BEETLEQUEEN_BODY" or "LEMURIANBRUISER_BODY" => 24f,
                "PARENT_BODY" => 20f,
                "GREATERWISP_BODY" or "HERMIT_CRAB_BODY" => 21f,
                "ele_mb_melee_champion_sur" or "ele_mb_melee_sur" => 17f,
                "elf_woman_sur" or "elf_woman_02_sur" or "ghost_king_sur" => 10f,
                "jotunn_warrior_sur" or "jotunn_warrior_v2_sur" => 10f,
                "skeleton_bow_VarRegular" or "skeleton_bow_sphinx" or "elf_ranged_sur" => 16f,
                "GOLEM_BODY" or "TITAN_BODY" or "TITANGOLD_BODY" or
                    "GM_SNOW_GOLEM" or "GM_NATURE_GOLEM" or "GM_SANDY_GOLEM" => 23f,
                _ => 0f
            };
            point = default;
            if (distance <= 0f) return false;
            int mask = GameManager.environmentLayer;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Vector3 candidate = origin + direction * distance;
                if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var ground, 10f,
                    mask, QueryTriggerInteraction.Ignore) || ground.normal.y < .8f ||
                    Mathf.Abs(ground.point.y - origin.y) > (id == "GREATERWISP_BODY" ? 7f : 1.5f) ||
                    Physics.Linecast(origin + Vector3.up, ground.point + Vector3.up, mask, QueryTriggerInteraction.Ignore))
                    continue;
                if (id == "PARENT_BODY" && Physics.CapsuleCast(
                    origin + Vector3.up * RangeResearchEnemy.ParentBodyRadius,
                    origin + Vector3.up * (RangeResearchEnemy.ParentBodyHeight - RangeResearchEnemy.ParentBodyRadius),
                    RangeResearchEnemy.ParentBodyRadius, direction, distance, mask, QueryTriggerInteraction.Ignore)) continue;
                if (id == "GREATERWISP_BODY")
                {
                    Vector3 muzzle = origin - Vector3.up;
                    Vector3 aim = ground.point + Vector3.up - muzzle;
                    if (Physics.SphereCast(muzzle, .65f, aim.normalized, out _, aim.magnitude,
                        mask, QueryTriggerInteraction.Ignore)) continue;
                }
                point = ground.point + Vector3.up * .15f;
                return true;
            }
            return false;
        }

        static IEnumerator VerifyGmUi(CharacterInstance target)
        {
            const string marker = "JOTUNN_MONSTER_GM_UI_SMOKE";
            var gm = FindFirstObjectByType<RangeResearchLab>();
            if (!gm)
            {
                Debug.LogError(marker + "_FAILED in-match GM missing.");
                yield break;
            }

            gm.SendMessage("Toggle");
            yield return null;
            var browser = gm.GetComponentInChildren<JournalBrowser>();
            if (!browser)
            {
                Debug.LogError(marker + "_FAILED browser did not open.");
                yield break;
            }

            var configurations = new (string id, int health)[]
            {
                (Skeleton1HCombat.ConfigurationId, Skeleton1HCombat.BaseHealth),
                ("skeleton_bow_VarRegular", Skeleton1HCombat.BaseHealth),
                ("spider_sur_VarRegular", 14),
                ("spider_sur_VarInsane", Skeleton1HCombat.BaseHealth),
                ("skeleton_guardian_sur", 140),
                ("skeleton_goat_sur_boss_VarEasy", 4000),
                ("spider_alf_sur", 14),
                ("spider_alf_veteran_sur", 140),
                ("elf_melee_sur", 36),
                ("elf_ranged_sur", 36),
                ("jotunn_stone_sur", 140),
                ("jotunn_stone_insane_sur", 140),
                ("sentinel_alf_sur", 36),
                ("sentinel_veteran_alf_sur", 140),
                ("elf_spider_sur", 1100),
                ("elf_spider_02_sur", 1100),
                ("jotunn_alfheim_boss_sur", 4000),
                ("jotunn_alfheim_boss_phase2_sur", 4000),
                ("elf_melee_insane_sur", 36),
                ("skeleton_1h_sphinx_sur", 36),
                ("skeleton_bow_sphinx", 36),
                ("skeleton_guardian_sphinx_sur", 140),
                ("skeleton_mb_sphinx_sur", 1100),
                ("milipede_sur", 140),
                ("milipede_insane_sur", 140),
                ("horror_expl_jotun_sur", HorrorJotunCombat.LocalBaseHealth),
                ("hyb_sub_boss_sur_VarEasy", 1100),
                ("hyb_boss_sur_VarEasy", 4000)
            };
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-monsterGmRestoredEntries") >= 0)
                configurations = new[] { ("ghost_sur", 0), ("GEEP_BODY", 0), ("GIP_BODY", 0) };
            int failures = 0;
            foreach (var (id, health) in configurations)
            {
                bool rainEntry = id is "GEEP_BODY" or "GIP_BODY";
                bool clickedCategory = ClickGmButton(browser, rainEntry ? "怪物" : "怪物（巨人杀手）");
                yield return null;
                bool clickedEntry = false;
                for (int page = 0; clickedCategory && page < 12; page++)
                {
                    string label = id switch { "skeleton_1h_sur" => "近战骷髅", "milipede_sur" => "蜈蚣", "spider_sur_VarInsane" => "疯狂蜘蛛", "GEEP_BODY" => "基普", "GIP_BODY" => "格普", _ => id };
                    clickedEntry = ClickGmButton(browser, label);
                    if (clickedEntry || !ClickGmButton(browser, ">")) break;
                    yield return null;
                }

                yield return null;
                bool clickedSpawn = clickedEntry && ClickGmButton(browser, "召唤");
                yield return null;
                var combat = clickedSpawn ? FindFirstObjectByType<Skeleton1HCombat>() : null;
                var horror = clickedSpawn && id == "horror_expl_jotun_sur"
                    ? FindFirstObjectByType<HorrorJotunCombat>() : null;
                var rainActor = clickedSpawn && rainEntry ? FindFirstObjectByType<RangeResearchEnemy>() : null;
                var ghost = clickedSpawn && id == "ghost_sur" ? FindFirstObjectByType<MonsterPassThrough>() : null;
                var actor = rainEntry ? rainActor && rainActor.IsLabSummon ? rainActor.GetComponent<CharacterInstance>() : null
                    : ghost ? ghost.GetComponent<CharacterInstance>()
                    : id == "horror_expl_jotun_sur"
                    ? horror ? horror.GetComponent<CharacterInstance>() : null
                    : combat ? combat.GetComponent<CharacterInstance>() : null;
                uint identity = actor ? actor.netId : 0u;
                bool spawned = identity != 0u && (health == 0 || actor.MaxHealth == health) &&
                    NetworkServer.spawned.ContainsKey(identity);

                bool clickedRemove = spawned && ClickGmButton(browser, "移除");
                yield return null;
                bool removed = clickedRemove && !NetworkServer.spawned.ContainsKey(identity);
                if (clickedCategory && clickedEntry && spawned && removed)
                {
                    Debug.Log($"{marker}_ENTRY_PASSED {id}");
                    continue;
                }

                failures++;
                if (id == "skeleton_guardian_sphinx_sur" && !spawned)
                    LogSphinxGuardianAssets();
                Debug.LogWarning($"{marker}_ENTRY_FAILED {id}: category={clickedCategory}, entry={clickedEntry}, " +
                    $"spawnButton={clickedSpawn}, spawned={spawned}, removeButton={clickedRemove}, removed={removed}");
                if (rainEntry) gm.RemoveCatalog(new BrowserEntry { id = id, category = 1 });
                else JotunnMonsterGm.Clear(target);
                yield return null;
                if (FindFirstObjectByType<Skeleton1HCombat>()) break;
            }

            browser.Close();
            ReportAlfheimSmoke(marker, failures == 0, $"{failures} GM entries failed. See entry results.");
        }

        static bool ClickGmButton(JournalBrowser browser, string label)
        {
            foreach (var button in browser.GetComponentsInChildren<Button>())
            {
                if (!button.interactable) continue;
                foreach (var text in button.GetComponentsInChildren<TMP_Text>())
                {
                    if (text.text != label) continue;
                    button.onClick.Invoke();
                    return true;
                }
            }
            return false;
        }

        static IEnumerator VerifyAlfheimEndlessDriderWave(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_ENDLESS_DRIDER_WAVE_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            float previousTimeScale = Time.timeScale;
            target.RangeInvulnerable = true;
            var spawnIds = new List<uint>();
            float matchStartedAt = Time.time - 178f;
            bool began = AlfheimEndlessDriderWave.TryStart(target, matchStartedAt,
                new System.Random(1), out var wave);
            bool passed = began;
            try
            {
                yield return new WaitForSeconds(.5f);
                passed &= wave && wave.Actors.Count == 0;
                Time.timeScale = 5f;
                int[] startTimes = { 180, 219, 258, 297 };
                for (int round = 1; passed && round <= startTimes.Length; round++)
                {
                    int count = round is 1 or 4 ? 1 : 2;
                    int previousCount = spawnIds.Count;
                    float startAt = matchStartedAt + startTimes[round - 1];
                    float deadline = Time.realtimeSinceStartup + 25f;
                    while (wave && (wave.Round < round || wave.SpawnedThisRound < count) &&
                           Time.realtimeSinceStartup < deadline)
                        yield return null;

                    if (round == 4) yield return new WaitForSeconds(.5f);
                    int normal = 0;
                    int variant = 0;
                    if (wave)
                    {
                        for (int i = previousCount; i < wave.Actors.Count; i++)
                        {
                            var actor = wave.Actors[i];
                            var visual = actor ? actor.GetComponentInChildren<Skeleton1HVisual>() : null;
                            if (!visual || actor.MaxHealth != 1100) continue;
                            if (visual.name.StartsWith("elf_spider_02_sur", StringComparison.Ordinal)) variant++;
                            else if (visual.name.StartsWith("elf_spider_sur", StringComparison.Ordinal)) normal++;
                            spawnIds.Add(actor.netId);
                        }
                    }
                    bool roundPassed = wave && wave.Round == round &&
                        wave.Actors.Count == previousCount + count &&
                        normal == (round < 3 ? count : round == 3 ? 1 : 0) &&
                        variant == (round >= 3 ? 1 : 0) && Time.time >= startAt;
                    passed &= roundPassed;
                    Debug.Log($"{marker} round {round}: normal={normal}, variant={variant}, passed={roundPassed}");
                }
            }
            finally
            {
                if (wave) Destroy(wave);
                if (target) target.RangeInvulnerable = wasInvulnerable;
                Time.timeScale = previousTimeScale;
            }

            yield return null;
            bool cleaned = spawnIds.Count == 6 && spawnIds.TrueForAll(id => !NetworkServer.spawned.ContainsKey(id));
            ReportAlfheimSmoke(marker, passed && cleaned, $"rounds={passed}, cleanup={cleaned}");
        }

        static IEnumerator VerifyAlfheimEliteWaves(CharacterInstance target, bool drider)
        {
            string marker = drider ? "JOTUNN_ALFHEIM_DRIDER_WAVES_SMOKE" : "JOTUNN_ALFHEIM_STONE_WAVES_SMOKE";
            string modelName = drider ? "elf_spider_sur" : "jotunn_stone_insane_sur";
            int expectedHealth = drider ? 1100 : 140;
            var cases = drider ? new[] { (26, 300f, 1), (55, 600f, 1) } : new[] { (62, 720f, 1), (74, 840f, 2) };
            bool passed = true;
            foreach (var (index, startTime, count) in cases)
            {
                float now = Time.time;
                bool began = AlfheimNormalOpeningWave.TryStart(target, now - startTime + 1f,
                    new System.Random(index), out var wave, index);
                yield return new WaitForSeconds(.5f);
                bool waited = wave && wave.SpawnedCount == 0;
                float deadline = Time.realtimeSinceStartup + 15f;
                while (wave && wave.SpawnedCount < count && Time.realtimeSinceStartup < deadline)
                    yield return null;

                bool correctActors = wave && wave.SpawnedCount == count &&
                    (drider ? wave.DriderCount : wave.StoneJotunnCount) == count;
                uint[] ids = new uint[count];
                for (int i = 0; correctActors && i < count; i++)
                {
                    var actor = wave.Actors[i];
                    var visual = actor ? actor.GetComponentInChildren<Skeleton1HVisual>() : null;
                    correctActors = visual && visual.name.StartsWith(modelName, StringComparison.Ordinal) &&
                        actor.MaxHealth == expectedHealth;
                    ids[i] = actor ? actor.netId : 0u;
                }

                if (wave) Destroy(wave);
                yield return null;
                bool cleaned = Array.TrueForAll(ids, id => id != 0u && !NetworkServer.spawned.ContainsKey(id));
                bool wavePassed = began && waited && correctActors && cleaned;
                passed &= wavePassed;
                Debug.Log($"{marker} W{index}: started={began}, waited={waited}, actors={correctActors}, cleanup={cleaned}");
            }

            ReportAlfheimSmoke(marker, passed, drider ? "Check W26 and W55 results." : "Check W62 and W74 results.");
        }

        static IEnumerator VerifyAlfheimRangedCap(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_NORMAL_W8_RANGED_CAP_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            bool began = AlfheimNormalOpeningWave.TryStart(target, Time.time - 65f,
                new System.Random(976808), out var wave, index: 8);
            float deadline = Time.realtimeSinceStartup + 18f;
            while (wave && wave.SpawnedCount < 15 && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool valid = wave && wave.SpawnedCount == 15 && wave.RangedCount == 2 &&
                wave.ActiveRangedCount == 2 && wave.ElfCount == 13 && wave.SpiderCount == 0;
            if (wave) Destroy(wave);
            if (target) target.RangeInvulnerable = wasInvulnerable;

            ReportAlfheimSmoke(marker, began && valid, $"began={began}, compositionAndCap={valid}");
        }

        static IEnumerator VerifyAlfheimStampede(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_NORMAL_W2_STAMPEDE_SMOKE";
            Vector3 previousPosition = target.transform.position;
            // A 30-lane formation needs more open ground than the default training spawn provides.
            var floor = new GameObject("Alfheim W2 test ground");
            Vector3 center = previousPosition + Vector3.up * 80f;
            floor.transform.position = center - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(64f, 1f, 64f);
            SetSmokePlayerPosition(target, center + Vector3.up * .15f);
            Physics.SyncTransforms();

            bool began = AlfheimNormalOpeningWave.TryStart(target, Time.time - 15f,
                new System.Random(976802), out var wave, index: 2);
            yield return null;
            yield return null;
            bool batch = wave && wave.SpiderCount == 30 && wave.ElfCount == 0;
            var first = batch ? wave.Actors[0] : null;
            bool hasController = first && first.TryGetComponent<MonsterPassThrough>(out _) &&
                first.GetComponent<CharacterMotor>().WalkSpeed == 13f;
            Vector3 firstPosition = first ? first.transform.position : default;
            bool moved = false;
            var ids = new uint[batch ? wave.Actors.Count : 0];
            for (int i = 0; i < ids.Length; i++)
                if (wave.Actors[i]) ids[i] = wave.Actors[i].netId;

            float deadline = Time.realtimeSinceStartup + 8f;
            bool despawned = false;
            while (batch && Time.realtimeSinceStartup < deadline)
            {
                if (first && (first.transform.position - firstPosition).sqrMagnitude > 4f) moved = true;
                despawned = Array.TrueForAll(ids, id => id != 0 && !NetworkServer.spawned.ContainsKey(id));
                if (despawned) break;
                yield return null;
            }

            if (wave) Destroy(wave);
            if (target) SetSmokePlayerPosition(target, previousPosition);
            Destroy(floor);
            ReportAlfheimSmoke(marker, began && batch && hasController && moved && despawned,
                $"batch={batch}, controller={hasController}, moved={moved}, despawned={despawned}");
        }

        static void SetSmokePlayerPosition(CharacterInstance player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            player.transform.position = position;
            if (wasEnabled) controller.enabled = true;
            if (player.TryGetComponent<NetworkTransformBase>(out var networkTransform))
                networkTransform.ResetState();
        }

        static IEnumerator VerifyAlfheimActiveCap(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_NORMAL_W4_CAP_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            AlfheimNormalOpeningWave.TryStart(target, Time.time - 25f, new System.Random(976804), out var wave, index: 4);
            float deadline = Time.realtimeSinceStartup + 55f;
            while (wave && wave.SpawnedCount < 40 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSeconds(.75f);

            bool respectedCap = wave && wave.SpiderCount == 40 && wave.ElfCount == 0;
            if (respectedCap)
            {
                var first = wave.Actors[0];
                if (first) first.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.melee, target, 0);
            }

            deadline = Time.realtimeSinceStartup + 15f;
            while (respectedCap && wave && wave.SpawnedCount == 40 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSeconds(.75f);
            bool replacedOne = wave && wave.SpiderCount == 41 && wave.ElfCount == 0;
            if (wave) Destroy(wave);
            if (target) target.RangeInvulnerable = wasInvulnerable;

            ReportAlfheimSmoke(marker, respectedCap && replacedOne, $"capped={respectedCap}, replacedOne={replacedOne}");
        }

        static IEnumerator VerifyAlfheimOpening(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_NORMAL_W1_SMOKE";
            bool wasInvulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            float start = Time.time;
            bool began = AlfheimNormalOpeningWave.TryStart(target, start, new System.Random(97680), out var wave);
            bool duplicateRejected = began && !AlfheimNormalOpeningWave.TryStart(target, start);
            yield return new WaitForSeconds(2f);
            bool waited = wave && wave.SpawnedCount == 0;
            float deadline = Time.realtimeSinceStartup + 38f;
            while (wave && wave.SpawnedCount < AlfheimNormalOpeningWave.SpawnCount &&
                Time.realtimeSinceStartup < deadline)
                yield return null;

            bool spawnedBoth = wave && wave.SpawnedCount == AlfheimNormalOpeningWave.SpawnCount &&
                wave.SpiderCount > 0 && wave.ElfCount > 0;
            if (wave) Destroy(wave);
            if (target) target.RangeInvulnerable = wasInvulnerable;
            ReportAlfheimSmoke(marker, began && duplicateRejected && waited && spawnedBoth,
                $"start={began}, duplicateRejected={duplicateRejected}, waited={waited}, both={spawnedBoth}");
        }

        static IEnumerator VerifyAlfheimEncounter(CharacterInstance target)
        {
            const string marker = "JOTUNN_ALFHEIM_BOSS_ENCOUNTER_SMOKE";
            Vector3 forward = Quaternion.Euler(0, target.lookInput.y, 0) * Vector3.forward;
            Vector3 candidate = target.transform.position + forward * 6f;
            if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var ground, 12f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                Debug.LogError(marker + "_FAILED no ground at test anchors.");
                yield break;
            }

            var anchor = new GameObject("Alfheim phase smoke anchors");
            anchor.transform.SetPositionAndRotation(ground.point + Vector3.up * .15f, Quaternion.LookRotation(-forward));
            var encounter = anchor.AddComponent<AlfheimBossEncounter>();
            var playerReturn = new GameObject("Alfheim smoke player return").transform;
            playerReturn.SetParent(anchor.transform);
            playerReturn.SetPositionAndRotation(target.transform.position, Quaternion.LookRotation(forward));
            encounter.BindAnchors(anchor.transform, null, playerReturn);
            var sequence = anchor.AddComponent<AlfheimNormalEncounterSequence>();
            sequence.BindBoss(encounter);
            bool coreBegan = sequence.TryStartCore(target, Time.time);
            bool coreScheduled = coreBegan && target.GetComponents<AlfheimNormalOpeningWave>().Length ==
                AlfheimNormalEncounterSequence.OpeningWaveCount && !sequence.TryStartCore(target, Time.time);
            bool began = sequence.TryStartBoss() && !sequence.TryStartBoss();
            yield return null;
            bool coreStopped = target.GetComponents<AlfheimNormalOpeningWave>().Length == 0;
            var first = encounter.FirstActor;
            uint firstId = first ? first.netId : 0u;
            bool firstOnly = coreScheduled && coreStopped && began && first && first.MaxHealth == 4000 && !encounter.SecondActor && !encounter.Cleared;

            var playerInput = PlayerGameplayInput.Instance;
            bool verifyPlayerControl = target.isOwned && !target.Block;
            bool inputWasEnabled = playerInput && playerInput.enabled;
            bool aboveThreshold = false;
            if (firstOnly)
            {
                first.Server_ChangeHealthStateRaw(1999, CharacterPart.body, AttackType.melee, target, 0);
                yield return null;
                aboveThreshold = first && first.CurrentHealth == 2001 && !encounter.SecondActor;
                if (aboveThreshold)
                    first.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, target, 0);
            }

            float transitionStartedAt = Time.time;
            float deadline = Time.realtimeSinceStartup + 9f;
            var gameplayView = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            bool verifyCamera = gameplayView && gameplayView.isActiveAndEnabled;
            bool playedTransition = false;
            bool playedCamera = false;
            bool lockedPlayer = false;
            while (aboveThreshold && !encounter.SecondActor && Time.realtimeSinceStartup < deadline)
            {
                playedTransition |= encounter.TransitionModelPlaying;
                lockedPlayer |= target.Block && (!inputWasEnabled || (playerInput && !playerInput.enabled));
                playedCamera |= encounter.TransitionCameraPlaying && gameplayView && !gameplayView.enabled;
                yield return null;
            }

            bool cameraRestored = !verifyCamera || (playedCamera && gameplayView && gameplayView.enabled &&
                !encounter.TransitionCameraPlaying);
            bool playerReturned = !verifyPlayerControl || (lockedPlayer && !target.Block &&
                (!inputWasEnabled || (playerInput && playerInput.enabled)) &&
                Vector3.ProjectOnPlane(target.transform.position - playerReturn.position, Vector3.up).sqrMagnitude < .01f &&
                Mathf.Abs(Mathf.DeltaAngle(target.lookInput.y, playerReturn.eulerAngles.y)) < 1f);
            var second = encounter.SecondActor;
            uint secondId = second ? second.netId : 0u;
            bool swapped = second && secondId != firstId && second.MaxHealth == 4000 &&
                second.CurrentHealth == 2000 && playedTransition && Time.time - transitionStartedAt >= 5.4f &&
                (second.transform.position - anchor.transform.position).sqrMagnitude < .01f &&
                !NetworkServer.spawned.ContainsKey(firstId) && !encounter.Cleared;

            if (swapped)
                second.Server_ChangeHealthStateRaw(40000, CharacterPart.body, AttackType.melee, target, 0);

            bool cleared = encounter.Cleared;
            Destroy(anchor);
            yield return null;
            yield return null;

            bool cleanedUp = secondId != 0u && !NetworkServer.spawned.ContainsKey(secondId);
            ReportAlfheimSmoke(marker,
                firstOnly && aboveThreshold && swapped && cleared && cleanedUp && cameraRestored && playerReturned,
                $"coreScheduled={coreScheduled}, coreStopped={coreStopped}, firstOnly={firstOnly}, aboveThreshold={aboveThreshold}, swapped={swapped}, cleared={cleared}, cleanup={cleanedUp}, camera={cameraRestored}, playerReturned={playerReturned}");
        }

        static IEnumerator VerifyBossEncounter(CharacterInstance target)
        {
            const string marker = "JOTUNN_NIFLHEIM_BOSS_ENCOUNTER_SMOKE";
            Vector3 forward = Quaternion.Euler(0, target.lookInput.y, 0) * Vector3.forward;
            Vector3 candidate = target.transform.position + forward * 6f;
            if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var ground, 12f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                Debug.LogError(marker + "_FAILED no ground at test anchor.");
                yield break;
            }

            var anchor = new GameObject("Niflheim fixed encounter smoke anchor");
            anchor.transform.SetPositionAndRotation(ground.point + Vector3.up * .15f, Quaternion.LookRotation(-forward));
            var encounter = anchor.AddComponent<NiflheimEasyBossEncounter>();
            bool began = encounter.TryBegin(target, anchor.transform);
            var boss = encounter.SpawnedBoss;
            bool fixedSpawn = boss && Vector3.Distance(boss.transform.position, anchor.transform.position) < .1f;
            bool rejectedDuplicate = !encounter.TryBegin(target, anchor.transform);
            bool prematureClear = encounter.Cleared;
            uint identity = boss ? boss.netId : 0u;

            if (boss)
                boss.Server_ChangeHealthStateRaw(boss.MaxHealth * 10, CharacterPart.body, AttackType.melee, target, 0);

            bool killed = encounter.Cleared && (!boss || boss.CurrentHealth <= 0);
            Destroy(anchor);
            yield return null;
            yield return null;

            bool cleanedUp = identity != 0u && !NetworkServer.spawned.ContainsKey(identity);
            if (began && fixedSpawn && rejectedDuplicate && !prematureClear && killed && cleanedUp)
                Debug.Log(marker + "_PASSED");
            else
                Debug.LogError($"{marker}_FAILED began={began}, position={fixedSpawn}, duplicateRejected={rejectedDuplicate}, " +
                    $"prematureClear={prematureClear}, killed={killed}, cleanup={cleanedUp}");
        }

        IEnumerator VerifyGoatRotator(CharacterInstance actor, CharacterInstance target)
        {
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(36f, 1f, 36f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(actor, center + Vector3.up * .15f);
            SetSmokePlayerPosition(target, center + Vector3.forward * 4f + Vector3.up * .15f);
            bool invulnerable = target.RangeInvulnerable;
            int health = target.CurrentHealth;
            int maxHealth = target.MaxHealth;
            target.MaxHealth = target.CurrentHealth = 4000;
            target.RangeInvulnerable = false;
            actor.CurrentHealth = actor.MaxHealth / 2;
            int visible = 0;
            int hit = 0;
            bool moved = false;
            bool audio = false;
            Vector3? first = null;
            float deadline = Time.realtimeSinceStartup + 85f;
            while (actor && target && Time.realtimeSinceStartup < deadline)
            {
                var volley = actor.GetComponent<GoatRotatorVolley>();
                if (volley)
                {
                    int count = 0;
                    foreach (var child in actor.GetComponentsInChildren<Transform>())
                    {
                        if (child.name != "Goat rotator projectile") continue;
                        if (count++ == 1)
                        {
                            Vector3 position = child.position;
                            if (!first.HasValue)
                            {
                                first = position;
                                Vector3 radial = Vector3.ProjectOnPlane(position - actor.transform.position, Vector3.up);
                                SetSmokePlayerPosition(target, actor.transform.position + Quaternion.Euler(0f, 28f, 0f) * radial);
                            }
                            moved |= (position - first.Value).sqrMagnitude > .25f;
                        }
                        audio |= child.GetComponent<AudioSource>() is { isPlaying: true };
                    }
                    hit = Mathf.Max(hit, volley.ConfirmedDamage);
                    visible = Mathf.Max(visible, count);
                    if (visible == 4 && moved && hit > 0 && audio) break;
                }
                yield return null;
            }
            uint id = actor ? actor.netId : 0;
            JotunnMonsterGm.SetCombat(target, false);
            yield return null;
            yield return null;
            bool stopped = actor && !actor.GetComponent<GoatRotatorVolley>();
            JotunnMonsterGm.Clear(target);
            yield return null;
            bool removed = id != 0 && !NetworkServer.spawned.ContainsKey(id);
            if (target)
            {
                SetSmokePlayerPosition(target, original);
                target.RangeInvulnerable = invulnerable;
                target.MaxHealth = maxHealth;
                target.CurrentHealth = health;
            }
            Destroy(floor);
            ReportAlfheimSmoke("JOTUNN_GOAT_NORMAL_ROTATOR", visible == 4 && moved && hit > 0 && audio && stopped && removed,
                $"projectiles={visible}, moved={moved}, damage={hit}, audio={audio}, stopped={stopped}, removed={removed}");
        }

        IEnumerator VerifyGoatHorns(CharacterInstance actor, CharacterInstance target)
        {
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(24f, 1f, 24f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(actor, center + Vector3.up * .15f);
            SetSmokePlayerPosition(target, center + Vector3.forward * 3f + Vector3.up * .15f);
            bool invulnerable = target.RangeInvulnerable;
            int maxHealth = target.MaxHealth;
            int health = target.CurrentHealth;
            target.MaxHealth = target.CurrentHealth = 2000;
            target.RangeInvulnerable = false;
            var combat = actor.GetComponent<Skeleton1HCombat>();
            var audio = combat.AttackAudio;
            float castAt = -1f;
            float delay = 0f;
            bool warning = false;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (actor && target && Time.realtimeSinceStartup < deadline)
            {
                if (audio && audio.isPlaying && audio.clip)
                {
                    if (audio.clip.name == "OriginalGoatHornsCast")
                    {
                        if (castAt < 0f) castAt = Time.time;
                        warning |= actor.GetComponentInChildren<LineRenderer>() is { enabled: true };
                    }
                    if (audio.clip.name == "OriginalGoatHornsImpact" && combat.EndlessHornsDamage > 0 && castAt >= 0f)
                    {
                        delay = Time.time - castAt;
                        break;
                    }
                }
                yield return null;
            }
            int damage = combat ? combat.EndlessHornsDamage : 0;
            uint id = actor ? actor.netId : 0;
            JotunnMonsterGm.SetCombat(target, false);
            bool stopped = audio && !audio.isPlaying;
            JotunnMonsterGm.Clear(target);
            yield return null;
            bool removed = id != 0 && !NetworkServer.spawned.ContainsKey(id);
            if (target)
            {
                SetSmokePlayerPosition(target, original);
                target.RangeInvulnerable = invulnerable;
                target.MaxHealth = maxHealth;
                target.CurrentHealth = health;
            }
            Destroy(floor);
            ReportAlfheimSmoke("JOTUNN_GOAT_HEL_HORNS", warning && damage > 0 && delay >= .8f && delay <= 1.05f && stopped && removed,
                $"warning={warning}, damage={damage}, castToImpact={delay:F3}, stopped={stopped}, removed={removed}");
        }

        IEnumerator VerifyGoatRing(CharacterInstance actor, CharacterInstance target)
        {
            Vector3 original = target.transform.position;
            Vector3 center = original + Vector3.up * 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = center - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(36f, 1f, 36f);
            Physics.SyncTransforms();
            SetSmokePlayerPosition(actor, center + Vector3.up * .15f);
            SetSmokePlayerPosition(target, center + Vector3.forward * 6f + Vector3.up * .15f);
            bool invulnerable = target.RangeInvulnerable;
            target.RangeInvulnerable = true;
            actor.CurrentHealth = actor.MaxHealth / 2;
            var guardians = new HashSet<uint>();
            var projectiles = new HashSet<int>();
            float deadline = Time.realtimeSinceStartup + 85f;
            while (actor && target && Time.realtimeSinceStartup < deadline)
            {
                if (HorizontalDistanceSquared(actor.transform.position, target.transform.position) < 5.5f * 5.5f)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(target.transform.position - actor.transform.position, Vector3.up).normalized;
                    Vector3 position = actor.transform.position + direction * 7f;
                    if (Mathf.Abs(position.x - center.x) < 15f && Mathf.Abs(position.z - center.z) < 15f)
                        SetSmokePlayerPosition(target, position);
                }
                foreach (var child in actor.GetComponentsInChildren<Transform>())
                    if (child.name == "Hyb summon projectile") projectiles.Add(child.GetInstanceID());
                foreach (var identity in NetworkServer.spawned.Values)
                {
                    var visual = identity.GetComponentInChildren<Skeleton1HVisual>();
                    if (visual && visual.name.StartsWith("skeleton_guardian_sur", StringComparison.Ordinal))
                        guardians.Add(identity.netId);
                }
                if (guardians.Count >= 10) break;
                yield return null;
            }
            JotunnMonsterGm.SetCombat(target, false);
            yield return null;
            yield return null;
            bool clean = !guardians.Overlaps(NetworkServer.spawned.Keys) && actor && !actor.GetComponent<HybSummonVolley>();
            uint id = actor ? actor.netId : 0;
            JotunnMonsterGm.Clear(target);
            yield return null;
            bool removed = id != 0 && !NetworkServer.spawned.ContainsKey(id);
            SetSmokePlayerPosition(target, original);
            target.RangeInvulnerable = invulnerable;
            Destroy(floor);
            ReportAlfheimSmoke("JOTUNN_GOAT_NORMAL_RING", projectiles.Count == 10 && guardians.Count == 10 && clean && removed,
                $"projectiles={projectiles.Count}, guardians={guardians.Count}, cleanup={clean}, removed={removed}");
        }

        IEnumerator VerifyGoatFreeze(CharacterInstance actor, CharacterInstance target)
        {
            int previousMax = target.MaxHealth;
            int previousHealth = target.CurrentHealth;
            bool previousInvulnerability = target.RangeInvulnerable;
            double previousRoot = target.ResearchRootUntil;
            target.RangeInvulnerable = false;
            target.MaxHealth = target.CurrentHealth = 2000;
            actor.CurrentHealth = actor.MaxHealth / 2;
            var combat = actor.GetComponent<Skeleton1HCombat>();
            var warnings = new HashSet<LineRenderer>();
            var blasts = new HashSet<AudioSource>();
            bool cast = false;
            bool rooted = false;
            float deadline = Time.realtimeSinceStartup + 45f;
            while (actor && Time.realtimeSinceStartup < deadline)
            {
                foreach (var ring in actor.GetComponentsInChildren<LineRenderer>())
                    if (ring.enabled && ring.name == "Enemy ground warning") warnings.Add(ring);
                foreach (var source in actor.GetComponentsInChildren<AudioSource>())
                {
                    if (!source.isPlaying || !source.clip) continue;
                    cast |= source.clip.name == "OriginalGoatFreezeCast";
                    if (source.clip.name == "OriginalGoatFreezeBlast") blasts.Add(source);
                }
                rooted |= target.ResearchRooted;
                if (warnings.Count >= 5 && blasts.Count == 5 && cast && rooted) break;
                yield return null;
            }
            int hit = 2000 - target.CurrentHealth;
            JotunnMonsterGm.SetCombat(target, false);
            bool stopped = combat.AttackAudio && !combat.AttackAudio.isPlaying;
            foreach (var source in blasts) stopped &= source && !source.isPlaying;
            deadline = Time.realtimeSinceStartup + 3f;
            while (target.ResearchRooted && Time.realtimeSinceStartup < deadline) yield return null;
            bool expired = !target.ResearchRooted;
            uint id = actor ? actor.netId : 0;
            JotunnMonsterGm.Clear(target);
            yield return null;
            bool removed = id != 0 && !NetworkServer.spawned.ContainsKey(id);
            target.MaxHealth = previousMax;
            target.CurrentHealth = previousHealth;
            target.RangeInvulnerable = previousInvulnerability;
            target.ResearchRootUntil = previousRoot;
            ReportAlfheimSmoke("JOTUNN_GOAT_NORMAL_FREEZE", warnings.Count >= 5 && hit >= 90 &&
                cast && blasts.Count == 5 && rooted && expired && stopped && removed,
                $"warnings={warnings.Count}, damage={hit}, cast={cast}, blasts={blasts.Count}, rooted={rooted}, expired={expired}, stopped={stopped}, removed={removed}");
        }

        IEnumerator VerifyBoss(CharacterInstance actor, CharacterInstance target)
        {
            uint identity = actor.netId;
            var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
            int hits = 0;
            void OnDamage(int health, int damage, CharacterPart part, AttackType attack, Health victim)
            {
                if (victim == target && damage > 0) hits++;
            }

            actor.Client_OnDamageDealt += OnDamage;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (actor && target && hits == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            int beforeTransition = hits;
            if (actor && beforeTransition > 0)
                actor.CurrentHealth = actor.MaxHealth / 2;

            float transitionStarted = -1f;
            float transitionEnded = -1f;
            bool attackedDuringTransition = false;
            deadline = Time.realtimeSinceStartup + 18f;
            while (actor && target && beforeTransition > 0 && Time.realtimeSinceStartup < deadline)
            {
                if (visual && visual.CurrentMotion == Skeleton1HVisual.Motion.PhaseChange)
                {
                    if (transitionStarted < 0f) transitionStarted = Time.time;
                    attackedDuringTransition |= hits != beforeTransition;
                }
                else if (transitionStarted >= 0f)
                {
                    if (transitionEnded < 0f) transitionEnded = Time.time;
                    if (hits > beforeTransition) break;
                }
                yield return null;
            }

            bool sameActor = actor && actor.netId == identity &&
                NetworkServer.spawned.TryGetValue(identity, out var spawned) && spawned.gameObject == actor.gameObject;
            if (actor)
            {
                actor.Client_OnDamageDealt -= OnDamage;
            }
            JotunnMonsterGm.Clear(target);
            yield return null;

            float duration = transitionEnded - transitionStarted;
            bool resumed = hits > beforeTransition;
            if (beforeTransition > 0 && transitionStarted >= 0f && duration >= 7f &&
                !attackedDuringTransition && resumed && sameActor && !actor)
                Debug.Log(ResultPrefix + "_PASSED");
            else
                Debug.LogError($"{ResultPrefix}_FAILED initialHits={beforeTransition}, phaseDuration={duration:F2}, " +
                    $"hitDuringPhase={attackedDuringTransition}, resumed={resumed}, sameActor={sameActor}, cleanup={!actor}");
        }

        static IEnumerator VerifyWave(CharacterInstance target, int index)
        {
            string marker = $"JOTUNN_NIFLHEIM_W{index}_SMOKE";
            float start = Time.time;
            float gate = start + 2f;
            float scheduled = NiflheimEasySkeletonWave.GetStartTime(index);
            if (!NiflheimEasySkeletonWave.TryStart(target, gate - scheduled, index))
            {
                Debug.LogError(marker + "_FAILED wave did not start.");
                yield break;
            }

            var wave = target.GetComponent<NiflheimEasySkeletonWave>();
            const float ClockTolerance = .001f;
            bool emptyBeforeStart = true;
            float firstSeen = -1f;
            float secondSeen = -1f;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (wave && Time.realtimeSinceStartup < deadline)
            {
                int count = wave.SpawnedCount;
                if (Time.time < gate - ClockTolerance && count != 0) emptyBeforeStart = false;
                if (count == 1 && firstSeen < 0f) firstSeen = Time.time;
                if (count == 2)
                {
                    secondSeen = Time.time;
                    break;
                }
                yield return null;
            }

            // One frame of observer skew is possible because spawn and smoke run separately.
            float minimumSpacing = index switch { 7 => .70f, 8 => 1.30f, _ => 1.15f };
            bool spawnedOnSchedule = firstSeen >= gate - ClockTolerance && secondSeen - firstSeen >= minimumSpacing;
            if (wave) Destroy(wave);
            yield return null;
            yield return null;

            bool cleanedUp = !FindFirstObjectByType<Skeleton1HCombat>() &&
                !FindFirstObjectByType<SkeletonBowArrow>();
            if (emptyBeforeStart && spawnedOnSchedule && cleanedUp)
                Debug.Log(marker + "_PASSED");
            else
                Debug.LogError($"{marker}_FAILED earlyGate={emptyBeforeStart}, first={firstSeen - gate:F3}, spacing={secondSeen - firstSeen:F3}, cleanup={cleanedUp}");
        }

        static void ReportAlfheimSmoke(string marker, bool passed, string details)
        {
            passed &= runtimeErrorCount == 0;
            anySmokeFailed |= !passed;
            if (runtimeErrorCount != 0)
                details += $", runtimeErrors={runtimeErrorCount}, first={firstRuntimeError}";
            if (passed)
            {
                Debug.Log(marker + "_CHECKED " + details);
                Debug.Log(marker + "_PASSED");
            }
            else Debug.LogError($"{marker}_FAILED {details}");

            if (SessionState.GetBool(BatchRunKey, false))
                SessionState.SetInt(BatchResultKey, passed && !anySmokeFailed ? 0 : 1);
        }

        void OnDestroy()
        {
            if (startedHost && DNNetworkManager.Instance && NetworkServer.activeHost)
                DNNetworkManager.Instance.StopHost();

            Application.logMessageReceived -= OnRuntimeLog;
            if (!SessionState.GetBool(BatchRunKey, false)) return;
            int result = runtimeErrorCount == 0 ? SessionState.GetInt(BatchResultKey, 1) : 1;
            SessionState.EraseBool(BatchRunKey);
            SessionState.EraseInt(BatchResultKey);
            EditorApplication.delayCall += () => EditorApplication.Exit(result);
        }

        void Fail(string reason)
        {
            Debug.LogError(ResultPrefix + "_FAILED " + reason);
            Destroy(gameObject);
        }
    }
}
#endif
