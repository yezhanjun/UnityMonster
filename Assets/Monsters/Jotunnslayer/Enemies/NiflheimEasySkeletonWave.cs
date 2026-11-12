using System.Collections.Generic;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Niflheim easy core, EncounterGroup 97795, waves 4, 7 and 8.
    public sealed class NiflheimEasySkeletonWave : MonoBehaviour
    {
        int totalSpawned;
        float firstInterval;
        float lastInterval;
        int waveIndex;
        Skeleton1HSpawn.Kind pendingKind;
        const float RetryInterval = .5f;
        const int MaxConsecutiveFailures = 24;
        const float MaximumSpawnDistance = 36f;

        readonly List<CharacterInstance> actors = new List<CharacterInstance>(120);
        CharacterInstance target;
        CharacterInstance summoner;
        Vector3 formationCenter;
        float formationYaw;
        Camera gameplayCamera;
        float nextSpawn;
        int spawned;
        int placementAttempt;
        int consecutiveFailures;
        bool stopped;

        internal int SpawnedCount => spawned;

        // The caller supplies the match's scaled clock origin.
        public static bool TryStart(CharacterInstance target, float matchStartedAt, int index = 4) =>
            TryStart(target, matchStartedAt, index, out _);

        internal static bool TryStart(CharacterInstance target, float matchStartedAt, int index,
            out NiflheimEasySkeletonWave wave)
        {
            wave = null;
            if ((index != 4 && index != 7 && index != 8) || !NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0)
                return false;

            foreach (var existing in target.GetComponents<NiflheimEasySkeletonWave>())
                if (existing.waveIndex == index) return false;

            wave = target.gameObject.AddComponent<NiflheimEasySkeletonWave>();
            wave.waveIndex = index;
            (wave.totalSpawned, wave.firstInterval, wave.lastInterval) = index switch
            {
                7 => (120, .75f, .35f),
                8 => (17, 1.4f, .85f),
                _ => (100, 1.2f, .7f)
            };
            wave.pendingKind = wave.SelectNextActor();
            wave.target = target;
            wave.nextSpawn = Mathf.Max(Time.time, matchStartedAt + GetStartTime(index));
            return true;
        }

        bool IsBossSummon => waveIndex == 1 || waveIndex == 3;

        internal static bool CanSummon(CharacterInstance caster, bool ghosts)
        {
            if (!caster || !caster.isServer) return false;
            int index = ghosts ? 3 : 1;
            foreach (var existing in caster.GetComponents<NiflheimEasySkeletonWave>())
                if (existing.waveIndex == index) return false;
            return true;
        }

        internal static void StartBossSummon(CharacterInstance caster, CharacterInstance target, bool ghosts)
        {
            if (!CanSummon(caster, ghosts) || !target || target.CurrentHealth <= 0) return;
            var wave = caster.gameObject.AddComponent<NiflheimEasySkeletonWave>();
            wave.waveIndex = ghosts ? 3 : 1;
            wave.summoner = caster;
            wave.target = target;
            wave.totalSpawned = 30;
            wave.firstInterval = wave.lastInterval = ghosts ? 0f : .1f;
            wave.pendingKind = ghosts ? Skeleton1HSpawn.Kind.Ghost : Skeleton1HSpawn.Kind.Melee;
            wave.nextSpawn = Time.time;
            wave.formationCenter = target.transform.position;
            wave.formationYaw = target.lookInput.y;
        }

        public static float GetStartTime(int index) => index switch
        {
            4 => 70f,
            7 => 185f,
            8 => 255f,
            _ => throw new System.ArgumentOutOfRangeException(nameof(index))
        };

        void Update()
        {
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0 ||
                (IsBossSummon && (!summoner || summoner.CurrentHealth <= 0)))
            {
                Destroy(this);
                return;
            }

            float now = Time.time;
            if (IsBossSummon && (stopped || spawned >= totalSpawned))
            {
                foreach (var actor in actors)
                    if (actor && actor.CurrentHealth > 0) return;
                Destroy(this);
                return;
            }
            if (stopped || spawned >= totalSpawned || now < nextSpawn) return;

            // PointMoveThrough uses zero delay. Keep each batch bounded by the wave size.
            int budget = waveIndex == 3 ? totalSpawned - placementAttempt : 1;
            for (int i = 0; i < budget; i++)
            {
                if (!SpawnNext(now)) break;
            }
            if (waveIndex == 3 && placementAttempt >= totalSpawned) stopped = true;
        }

        bool SpawnNext(float now)
        {
            if (!TrySpawn(out var enemy))
            {
                if (waveIndex == 3) return true;
                consecutiveFailures++;
                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    stopped = true;
                    Debug.LogWarning($"Niflheim W{waveIndex} stopped after {spawned}/{totalSpawned} spawns. Check ground and skeleton assets.", this);
                }
                nextSpawn = now + RetryInterval;
                return false;
            }

            consecutiveFailures = 0;
            actors.Add(enemy);
            spawned++;
            if (spawned < totalSpawned) pendingKind = SelectNextActor();
            nextSpawn = now + Mathf.Lerp(firstInterval, lastInterval, (float)spawned / totalSpawned);
            return true;
        }

        Skeleton1HSpawn.Kind SelectNextActor()
        {
            if (waveIndex == 3) return Skeleton1HSpawn.Kind.Ghost;
            if (waveIndex == 1 || waveIndex == 4) return Skeleton1HSpawn.Kind.Melee;
            if (waveIndex == 8)
                return Random.Range(0, 7) == 0 ? Skeleton1HSpawn.Kind.Guardian : Skeleton1HSpawn.Kind.Melee;
            int slot = Random.Range(0, 31);
            if (slot < 15) return Skeleton1HSpawn.Kind.Spider;
            return slot < 30 ? Skeleton1HSpawn.Kind.Melee : Skeleton1HSpawn.Kind.Bow;
        }

        bool TrySpawn(out CharacterInstance enemy)
        {
            enemy = null;
            if (waveIndex == 3) return TrySpawnGhost(out enemy);
            return Skeleton1HSpawn.TrySpawnAtScreenEdge(target, pendingKind, ref gameplayCamera,
                ref placementAttempt, out enemy);
        }

        bool TrySpawnGhost(out CharacterInstance enemy)
        {
            enemy = null;
            const int mask = GameManager.environmentLayer;
            float angle = formationYaw - 90f + placementAttempt++ * (180f / (totalSpawned - 1));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 12f;
            if (!Physics.Raycast(formationCenter + offset + Vector3.up * 4f, Vector3.down,
                    out var ground, 12f, mask, QueryTriggerInteraction.Ignore) || ground.normal.y < .65f)
                return false;

            Vector3 position = ground.point + Vector3.up * .15f;
            if (Physics.CheckCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.5f,
                    .45f, mask, QueryTriggerInteraction.Ignore)) return false;

            enemy = Skeleton1HSpawn.TrySpawn(target, position, Quaternion.LookRotation(-offset), pendingKind);
            return enemy;
        }

        void OnDestroy()
        {
            if (!NetworkServer.active) return;
            foreach (var actor in actors)
                if (actor) NetworkServer.Destroy(actor.gameObject);
        }
    }
}
