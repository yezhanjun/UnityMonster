#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Jotunnslayer
{
    [InitializeOnLoad]
    public sealed class HyboreaMatchValidation : MonoBehaviour
    {
        const string Pending = "Jotunnslayer.HyboreaMatchValidation.Pending";
        const string NaturalBonusPending = Pending + ".NaturalBonus";
        bool validateNaturalBonus;
        int runtimeErrors;
        string firstRuntimeError;

        void Awake() => Application.logMessageReceived += OnRuntimeLog;

        void OnDestroy() => Application.logMessageReceived -= OnRuntimeLog;

        void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            runtimeErrors++;
            firstRuntimeError ??= message;
        }

        static HyboreaMatchValidation() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Game/Baseline/Scenes/Startup.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunNaturalBonusBatch()
        {
            SessionState.SetBool(NaturalBonusPending, true);
            RunBatch();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.EraseBool(Pending);
            EditorApplication.LockReloadAssemblies();
            var root = new GameObject("Hyborea formal match validation");
            DontDestroyOnLoad(root);
            var validation = root.AddComponent<HyboreaMatchValidation>();
            validation.validateNaturalBonus = SessionState.GetBool(NaturalBonusPending, false);
            SessionState.EraseBool(NaturalBonusPending);
        }

        static Button FindButton(string chinese, string english, string page = "home") =>
            GameObject.Find("Page " + page)?.GetComponentsInChildren<Button>().FirstOrDefault(button =>
                button.name == chinese || button.name == english);

        static Button FindEncounter() => GameObject.Find("Page prepare")?.GetComponentsInChildren<Button>()
            .FirstOrDefault(button => button.name.StartsWith("怪物遭遇 · ") || button.name.StartsWith("Enemy encounter · "));

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1f);
            for (int mode = 1; mode <= 2; mode++)
            {
                var single = FindButton("单人游戏", "Singleplayer");
                if (!single) { Fail("Startup singleplayer button missing"); yield break; }
                single.onClick.Invoke();
                yield return null;
                var characterButton = FindButton(RangeCharacterSelection.DisplayName(RangeCharacterSelection.FirstPortable),
                    RangeCharacterSelection.DisplayName(RangeCharacterSelection.FirstPortable), "prepare");
                if (!characterButton || !characterButton.interactable) { Fail("Playable character missing"); yield break; }
                characterButton.onClick.Invoke();
                yield return null;
                var creator = FindFirstObjectByType<RoomCreator>();
                var arena = creator ? creator.Maps.FirstOrDefault(map => map &&
                    System.IO.Path.GetFileNameWithoutExtension(map.Scene) == "map_arena") : null;
                if (!arena) { Fail("Arena map missing"); yield break; }
                for (int i = 0; i < creator.Maps.Length && !FindButton(arena.Name, arena.Name, "prepare"); i++)
                {
                    var mapButton = creator.Maps.Select(map => FindButton(map.Name, map.Name, "prepare")).FirstOrDefault(button => button);
                    if (!mapButton) { Fail("Map selection button missing"); yield break; }
                    mapButton.onClick.Invoke();
                    yield return null;
                }
                for (int i = 0; i < mode; i++)
                {
                    var encounterButton = FindEncounter();
                    if (!encounterButton) { Fail("Encounter selection button missing"); yield break; }
                    encounterButton.onClick.Invoke();
                    yield return null;
                }
                var deploy = FindButton("部署 · 巨人杀手", "Deploy · Jotunnslayer", "prepare");
                if (!deploy || !FindButton(arena.Name, arena.Name, "prepare")) { Fail("Deploy selection missing"); yield break; }
                DNNetworkManager.Instance.SetTransportPort(27964);
                deploy.onClick.Invoke();
                float deadline = Time.realtimeSinceStartup + 45f;
                HyboreaMatchEntry entry = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    entry = FindFirstObjectByType<HyboreaMatchEntry>();
                    if (entry && entry.Started) break;
                    yield return null;
                }
                if (!entry || !entry.Started || !FindFirstObjectByType<SkillPlayground>())
                {
                    var localPlayer = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerInstance>() : null;
                    Fail($"Encounter failed to start mode={mode} entry={entry != null} started={entry && entry.Started} " +
                        $"skills={FindFirstObjectByType<SkillPlayground>() != null} server={NetworkServer.active} " +
                        $"host={NetworkServer.activeHost} connections={NetworkServer.connections.Count} " +
                        $"local={localPlayer != null} character={localPlayer && localPlayer.MyCharacter}");
                    yield break;
                }
                bool correctMode = mode == 2 ? entry.GetComponent<HyboreaEndlessInteriorOpeningWave>() :
                    FindObjectsByType<HyboreaInteriorWave>(FindObjectsSortMode.None).Length > 0;
                if (!correctMode) { Fail($"Wrong encounter mode={mode}"); yield break; }
                var sharedBonus = entry.GetComponent<HyboreaEndlessBonusWaves>();
                if (mode == 2 && (!sharedBonus || !sharedBonus.Running ||
                    entry.GetComponent<HyboreaInteriorMiniBossWaves>()))
                { Fail("Endless match did not own the shared bonus group"); yield break; }
                if (mode == 2) Debug.Log("HYBOREA_FORMAL_MATCH_SHARED_BONUS_CHECKED");
                if (GameManager.Players.Values.Any(candidate => candidate && candidate.BOT))
                { Fail($"Arena bots coexist with encounter mode={mode}"); yield break; }
                var local = NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter;
                local.RangeInvulnerable = true;
                deadline = Time.realtimeSinceStartup + 25f;
                Skeleton1HCombat enemy = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    enemy = FindFirstObjectByType<Skeleton1HCombat>();
                    if (enemy) break;
                    yield return null;
                }
                if (!enemy || Time.time - entry.MatchStartedAt < 2.9f)
                { Fail($"Opening spawn or timing failed mode={mode}"); yield break; }
                uint enemyId = enemy.GetComponent<CharacterInstance>().netId;
                Debug.Log($"HYBOREA_FORMAL_MATCH_SPAWN_CHECKED mode={mode} elapsed={Time.time - entry.MatchStartedAt} enemy={enemyId}");
                var battle = FindFirstObjectByType<SkillBattlePreview>();
                if (!battle || !battle.TryChooseAssault())
                { Fail($"Class skill selection failed mode={mode}"); yield break; }
                var victim = enemy.GetComponent<CharacterInstance>();
                var motor = victim.GetComponent<CharacterMotor>();
                if (motor) motor.enabled = false;
                var controller = victim.GetComponent<CharacterController>();
                bool controllerEnabled = controller && controller.enabled;
                if (controllerEnabled) controller.enabled = false;
                victim.transform.position = local.transform.position + local.transform.forward * 2f;
                if (controllerEnabled) controller.enabled = true;
                Physics.SyncTransforms();
                bool killed = false;
                void OnKilled(Health target) { if (target == victim) killed = true; }
                local.Server_KilledCharacter += OnKilled;
                try
                {
                    deadline = Time.realtimeSinceStartup + 12f;
                    while (!killed && Time.realtimeSinceStartup < deadline)
                    {
                        battle.Cast(Time.time);
                        yield return null;
                    }
                }
                finally
                {
                    if (local) local.Server_KilledCharacter -= OnKilled;
                    if (motor) motor.enabled = true;
                }
                if (!killed) { Fail($"Class skill kill callback failed mode={mode}"); yield break; }
                Debug.Log($"HYBOREA_FORMAL_MATCH_SKILL_KILL_CHECKED mode={mode}");

                var bonusIds = new HashSet<uint>();
                if (mode == 2 && validateNaturalBonus)
                {
                    // Exercise the production match clock without moving its origin or changing timeScale.
                    int[] tracks = { 1, 2, 5 };
                    float[] gates = { 45f, 90f, 180f };
                    int[] health = { 54, 140, 1100 };
                    bool[] observed = new bool[3];
                    deadline = Time.realtimeSinceStartup + 205f;
                    while (Time.realtimeSinceStartup < deadline && !observed.All(value => value))
                    {
                        if (!entry || entry.Finished || !sharedBonus || !sharedBonus.Running ||
                            Mathf.Abs(Time.timeScale - 1f) > .001f)
                        { Fail("Natural bonus match stopped or timeScale changed"); yield break; }
                        float elapsed = Time.time - entry.MatchStartedAt;
                        for (int i = 0; i < tracks.Length; i++)
                        {
                            if (observed[i] || sharedBonus.SpawnedByTrack[tracks[i]] == 0) continue;
                            if (elapsed < gates[i] - .05f || elapsed > gates[i] + 15f)
                            { Fail($"Natural bonus timing track={tracks[i]} elapsed={elapsed}"); yield break; }
                            var first = sharedBonus.FirstLivingActor(tracks[i]);
                            if (!first || first.MaxHealth != health[i])
                            { Fail($"Natural bonus actor/health track={tracks[i]}"); yield break; }
                            bonusIds.Add(first.netId);
                            observed[i] = true;
                            Debug.Log($"HYBOREA_FORMAL_NATURAL_BONUS_CHECKED track={tracks[i]} elapsed={elapsed} hp={first.MaxHealth} total={sharedBonus.SpawnedByTrack[tracks[i]]}");
                        }
                        for (int i = 0; i < sharedBonus.ActorCount; i++)
                        {
                            var actor = sharedBonus.GetActor(i);
                            if (actor) bonusIds.Add(actor.netId);
                        }
                        yield return null;
                    }
                    if (!observed.All(value => value) || bonusIds.Count == 0 ||
                        sharedBonus.EmptyWaveStarts < 2 || !sharedBonus.FirstLivingActor(5))
                    { Fail("Natural bonus gates, empty waves or living W5 missing"); yield break; }
                    Debug.Log($"HYBOREA_FORMAL_NATURAL_BONUS_ALL_CHECKED tracked={bonusIds.Count} empty={sharedBonus.EmptyWaveStarts}");
                }

                local.RangeInvulnerable = false;
                local.Server_ChangeHealthStateRaw(local.MaxHealth + 100000, CharacterPart.body, AttackType.falldamage, local, 0);
                deadline = Time.realtimeSinceStartup + 5f;
                while (!entry.Finished && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(5f);
                if (!entry.Finished || FindFirstObjectByType<Skeleton1HCombat>() ||
                    FindFirstObjectByType<HyboreaEndlessInteriorOpeningWave>() || FindFirstObjectByType<HyboreaInteriorWave>() ||
                    (sharedBonus && (sharedBonus.Running || sharedBonus.ActorCount != 0 || sharedBonus.PendingCount != 0)) ||
                    FindFirstObjectByType<HorrorJotunCombat>() || FindFirstObjectByType<BatLootFleeing>())
                { Fail($"Player death did not stop encounter mode={mode}"); yield break; }
                if (bonusIds.Overlaps(NetworkServer.spawned.Keys))
                { Fail("Natural bonus actors remained registered after player death"); yield break; }
                Debug.Log($"HYBOREA_FORMAL_MATCH_DEATH_STOP_CHECKED mode={mode}");
                DNNetworkManager.Instance.StopHost();
                deadline = Time.realtimeSinceStartup + 20f;
                while (!FindButton("单人游戏", "Singleplayer") && Time.realtimeSinceStartup < deadline) yield return null;
                if (!FindButton("单人游戏", "Singleplayer") || FindFirstObjectByType<HyboreaMatchEntry>() ||
                    FindFirstObjectByType<HyboreaInteriorEncounterSequence>() || FindFirstObjectByType<Skeleton1HCombat>() ||
                    NetworkServer.spawned.ContainsKey(enemyId))
                { Fail($"Encounter teardown failed mode={mode}"); yield break; }
                Debug.Log($"HYBOREA_FORMAL_MATCH_TEARDOWN_CHECKED mode={mode}");
                // Startup briefly exposes its serialized design preview before NewFrontEnd.Start replaces it.
                yield return new WaitForSecondsRealtime(1f);
            }
            if (runtimeErrors != 0)
            {
                Fail($"Functional assertions completed but runtime errors={runtimeErrors}; first={firstRuntimeError}");
                yield break;
            }
            if (validateNaturalBonus) Debug.Log("JOTUNN_HYBOREA_FORMAL_NATURAL_BONUS_SMOKE_PASSED");
            Debug.Log("JOTUNN_HYBOREA_FORMAL_MATCH_SMOKE_PASSED");
            EditorApplication.Exit(0);
        }

        static void Fail(string reason)
        {
            Debug.LogError("JOTUNN_HYBOREA_FORMAL_MATCH_SMOKE_FAILED " + reason);
            EditorApplication.Exit(1);
        }
    }
}
#endif
