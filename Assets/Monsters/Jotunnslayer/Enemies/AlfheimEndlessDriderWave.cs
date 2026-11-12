using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // waves_alf_endless_bonus (97669), W5.
    [DisallowMultipleComponent]
    public sealed class AlfheimEndlessDriderWave : MonoBehaviour
    {
        const int FirstRoundTime = 180;
        const float RoundDelayDelta = -.03f;

        struct PendingRound
        {
            internal int Number;
            internal int Spawned;
            internal CharacterInstance Variant;
            internal float RetryAt;
            internal Skeleton1HSpawn.Kind? PendingKind;
        }

        readonly List<CharacterInstance> actors = new List<CharacterInstance>();
        readonly List<PendingRound> pendingRounds = new List<PendingRound>();
        CharacterInstance target;
        System.Random random;
        Camera gameplayCamera;
        int placementAttempt;
        int nextRoundTime = FirstRoundTime;
        float roundDelay = 39.4f;
        float matchStartedAt;

        internal int Round { get; private set; }
        internal int SpawnedThisRound { get; private set; }
        internal IReadOnlyList<CharacterInstance> Actors => actors;

        public static bool TryStart(CharacterInstance target, float matchStartedAt) =>
            TryStart(target, matchStartedAt, new System.Random(), out _);

        internal static bool TryStart(CharacterInstance target, float matchStartedAt, System.Random random,
            out AlfheimEndlessDriderWave wave)
        {
            wave = null;
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0 || target.GetComponent<AlfheimEndlessDriderWave>())
                return false;

            wave = target.gameObject.AddComponent<AlfheimEndlessDriderWave>();
            wave.target = target;
            wave.random = random;
            wave.matchStartedAt = matchStartedAt;
            return true;
        }

        void Update()
        {
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0)
            {
                Destroy(this);
                return;
            }

            float now = Time.time;
            if (now >= matchStartedAt + nextRoundTime)
            {
                Round++;
                SpawnedThisRound = 0;
                actors.RemoveAll(actor => !actor);
                pendingRounds.Add(new PendingRound { Number = Round });
                // W5 has no actor delay, so its estimated end equals its scheduled start.
                nextRoundTime = Mathf.RoundToInt(nextRoundTime + roundDelay);
                roundDelay = Mathf.Max(1f, roundDelay + RoundDelayDelta);
            }

            for (int i = 0; i < pendingRounds.Count;)
            {
                var round = pendingRounds[i];
                if (now >= round.RetryAt) TrySpawn(ref round, now);
                if (round.Number == Round) SpawnedThisRound = round.Spawned;
                if (round.Spawned == (round.Number == 1 ? 1 : 2))
                    pendingRounds.RemoveAt(i);
                else
                    pendingRounds[i++] = round;
            }
        }

        void TrySpawn(ref PendingRound round, float now)
        {
            bool normalEligible = round.Number <= 3;
            bool variantEligible = round.Number >= 3 && (!round.Variant || round.Variant.CurrentHealth <= 0);
            if (!normalEligible && !variantEligible) return;

            var kind = round.PendingKind ??= variantEligible && (!normalEligible || random.Next(11) < 10)
                ? Skeleton1HSpawn.Kind.AlfheimDriderVariant
                : Skeleton1HSpawn.Kind.AlfheimDrider;
            if (!Skeleton1HSpawn.TrySpawnAtScreenEdge(target, kind, ref gameplayCamera,
                ref placementAttempt, out var enemy))
            {
                round.RetryAt = now + .5f;
                return;
            }

            actors.Add(enemy);
            if (kind == Skeleton1HSpawn.Kind.AlfheimDriderVariant) round.Variant = enemy;
            round.PendingKind = null;
            round.Spawned++;
        }

        void OnDestroy()
        {
            if (!NetworkServer.active) return;
            foreach (var actor in actors)
                if (actor) NetworkServer.Destroy(actor.gameObject);
        }
    }
}
