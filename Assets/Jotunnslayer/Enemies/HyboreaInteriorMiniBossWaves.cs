using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DisallowMultipleComponent]
    public sealed class HyboreaInteriorMiniBossWaves : MonoBehaviour
    {
        // Groups 97729, 97751, 97740 and 97745. Each row spawns one actor.
        static readonly float[] EasyAndNormal = { 600f };
        static readonly float[] Hard = { 600f, 900f, 950f };
        static readonly float[] Insane = { 600f, 900f, 950f, 1023f, 1123f };
        static readonly float[] EasyMilipedes = { 120f, 240f, 420f, 540f, 720f, 840f, 870f };
        static readonly float[] MilipedeGates =
        {
            70f, 120f, 180f, 240f, 360f, 420f, 480f, 540f, 660f, 720f, 780f, 840f, 870f,
            903f, 940f, 980f, 1020f, 1060f, 1080f, 1140f
        };
        readonly HyboreaEndlessBonusSchedule endlessSchedule = new HyboreaEndlessBonusSchedule();

        readonly List<CharacterInstance> actors = new List<CharacterInstance>(Insane.Length);
        CharacterInstance target;
        Camera gameplayCamera;
        float[] schedule;
        float[] milipedeSchedule;
        int milipedeGateCount;
        int nextMiniBoss;
        int nextMilipede;
        float matchStartedAt;
        float retryAt;
        int placementAttempt;
        int pendingEndlessActors;
        bool endless;

        internal int SpawnedCount { get; private set; }
        internal bool Running => schedule != null || endless;
        internal int EndlessRound { get; private set; }
        internal CharacterInstance GetActor(int index) => actors[index];

        public bool TryStart(CharacterInstance player, float matchStartedAt, HyboreaInteriorDifficulty difficulty,
            bool includeMilipedes = false)
        {
            if (!CanStart(player)) return false;

            schedule = difficulty switch
            {
                HyboreaInteriorDifficulty.Easy or HyboreaInteriorDifficulty.Normal => EasyAndNormal,
                HyboreaInteriorDifficulty.Hard => Hard,
                HyboreaInteriorDifficulty.Insane => Insane,
                _ => null
            };
            if (schedule == null) return false;
            if (includeMilipedes)
            {
                milipedeSchedule = difficulty == HyboreaInteriorDifficulty.Easy ? EasyMilipedes : MilipedeGates;
                milipedeGateCount = difficulty switch
                {
                    HyboreaInteriorDifficulty.Normal => 13,
                    HyboreaInteriorDifficulty.Hard => 16,
                    _ => milipedeSchedule.Length
                };
            }

            target = player;
            this.matchStartedAt = matchStartedAt;
            return true;
        }

        // waves_hyb_endless_interior_bonus (97735), W5.
        public bool TryStartEndless(CharacterInstance player, float matchStartedAt)
        {
            if (!CanStart(player)) return false;

            target = player;
            this.matchStartedAt = matchStartedAt;
            endlessSchedule.Reset(wholeGroup: false);
            endless = true;
            return true;
        }

        bool CanStart(CharacterInstance player) =>
            isActiveAndEnabled && schedule == null && !endless && NetworkServer.activeHost &&
            NetworkServer.connections.Count == 1 && player && player.CurrentHealth > 0;

        void Update()
        {
            if (schedule == null && !endless) return;
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0)
            {
                StopWaves();
                return;
            }

            float now = Time.time;
            if (endless)
            {
                UpdateEndless(now);
                return;
            }
            while (now >= retryAt)
            {
                float miniBossGate = nextMiniBoss < schedule.Length ? schedule[nextMiniBoss] : float.PositiveInfinity;
                float milipedeGate = nextMilipede < milipedeGateCount ? milipedeSchedule[nextMilipede] : float.PositiveInfinity;
                if (now < matchStartedAt + Mathf.Min(miniBossGate, milipedeGate)) return;
                bool isMilipede = milipedeGate < miniBossGate;
                if (!TrySpawn(now, isMilipede ? Skeleton1HSpawn.Kind.Milipede : Skeleton1HSpawn.Kind.SphinxMiniBoss))
                    return;
                if (isMilipede) nextMilipede++;
                else nextMiniBoss++;
            }
        }

        void UpdateEndless(float now)
        {
            if (endlessSchedule.TryTakeDue(now - matchStartedAt, out var round))
            {
                EndlessRound = round.Round;
                pendingEndlessActors += round.Total;
                for (int i = actors.Count - 1; i >= 0; i--)
                    if (!actors[i]) actors.RemoveAt(i);
            }

            for (int i = 0; i < 2 && pendingEndlessActors > 0 && now >= retryAt; i++)
            {
                if (!TrySpawn(now, Skeleton1HSpawn.Kind.SphinxMiniBoss)) return;
                pendingEndlessActors--;
            }
        }

        bool TrySpawn(float now, Skeleton1HSpawn.Kind kind)
        {
            if (!Skeleton1HSpawn.TrySpawnAtScreenEdge(target, kind,
                ref gameplayCamera, ref placementAttempt, out var actor))
            {
                retryAt = now + .5f;
                return false;
            }
            actors.Add(actor);
            SpawnedCount++;
            return true;
        }

        public void StopWaves()
        {
            if (NetworkServer.active)
                foreach (var actor in actors)
                    if (actor) NetworkServer.Destroy(actor.gameObject);
            actors.Clear();
            SpawnedCount = 0;
            EndlessRound = 0;
            pendingEndlessActors = 0;
            endlessSchedule.Clear();
            endless = false;
            schedule = null;
            milipedeSchedule = null;
            milipedeGateCount = 0;
            nextMiniBoss = nextMilipede = 0;
            target = null;
            gameplayCamera = null;
            retryAt = 0f;
            placementAttempt = 0;
        }

        void OnDisable() => StopWaves();
    }
}
