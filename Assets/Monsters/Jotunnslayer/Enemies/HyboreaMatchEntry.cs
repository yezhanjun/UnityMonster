using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jotunnslayer
{
    internal enum HyboreaMatchMode { None, NormalInterior, EndlessInterior }

    // Owns encounter setup for explicitly selected local-host matches.
    public sealed class HyboreaMatchEntry : MonoBehaviour
    {
        static string pendingScene;
        static HyboreaMatchMode pendingMode;
        HyboreaMatchMode mode;
        HyboreaInteriorEncounterSequence sequence;
        CharacterInstance player;
        bool started;
        bool finished;
        internal bool Started => started;
        internal bool Finished => finished;
        internal float MatchStartedAt { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            pendingScene = null;
            pendingMode = HyboreaMatchMode.None;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        internal static void LaunchMatch(RoomCreator creator, int map, int character, RaidClass raidClass,
            HyboreaMatchMode mode)
        {
            if (!creator || NetworkServer.active || NetworkClient.active || map < 0 || map >= creator.Maps.Length ||
                !SkillPlayground.SupportsMatch(creator.Maps[map].Scene)) return;

            pendingScene = mode == HyboreaMatchMode.None ? null : creator.Maps[map].Scene;
            pendingMode = mode;
            Debug.Log($"HYBOREA_MATCH_SELECTION mode={mode} scene={pendingScene}");
            try
            {
                SkillPlayground.LaunchMatch(creator, map, character, raidClass);
            }
            finally
            {
                if (!NetworkServer.active) pendingScene = null;
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (pendingMode != HyboreaMatchMode.None)
                Debug.Log($"HYBOREA_MATCH_SCENE scene={scene.path} expected={pendingScene} server={NetworkServer.active} host={NetworkServer.activeHost}");
            bool enter = pendingScene == scene.path && NetworkServer.active &&
                pendingMode != HyboreaMatchMode.None;
            HyboreaMatchMode selected = pendingMode;
            if (enter || loadMode == LoadSceneMode.Single)
            {
                pendingScene = null;
                pendingMode = HyboreaMatchMode.None;
            }
            if (!enter) return;

            RoomSetup.Properties.P_FillEmptySlotsWithBots = false;
            var root = new GameObject("Hyborea match encounter");
            SceneManager.MoveGameObjectToScene(root, scene);
            var entry = root.AddComponent<HyboreaMatchEntry>();
            entry.mode = selected;
            entry.sequence = root.AddComponent<HyboreaInteriorEncounterSequence>();
        }

        void Update()
        {
            if (finished) return;
            if (!NetworkServer.active || (started && !NetworkServer.activeHost) || NetworkServer.connections.Count > 1)
            {
                StopEncounter();
                return;
            }
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1) return;
            if (started)
            {
                if (!player || player.CurrentHealth <= 0) StopEncounter();
                return;
            }

            var local = NetworkClient.localPlayer;
            var instance = local ? local.GetComponent<PlayerInstance>() : null;
            if (instance && instance.Team == -1 && GameManager.Gamemode)
                instance.ProcessRequestToJoinTeam(0);
            var character = instance ? instance.MyCharacter : null;
            if (!character || !character.isServer || character.CurrentHealth <= 0) return;

            MatchStartedAt = Time.time;
            started = mode == HyboreaMatchMode.EndlessInterior
                ? sequence.TryStartEndlessCore(character, MatchStartedAt)
                : sequence.TryStartCore(character, MatchStartedAt, HyboreaInteriorDifficulty.Normal);
            if (!started)
            {
                Debug.LogError("HYBOREA_MATCH_ENCOUNTER_START_FAILED");
                StopEncounter();
                return;
            }
            player = character;
            Debug.Log($"HYBOREA_MATCH_ENCOUNTER_STARTED mode={mode} start={MatchStartedAt}");
        }

        void StopEncounter()
        {
            if (sequence) sequence.StopCore();
            player = null;
            finished = true;
        }

        void OnDisable() => StopEncounter();
    }
}
