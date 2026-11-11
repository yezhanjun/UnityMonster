using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public enum HyboreaInteriorDifficulty { Easy, Normal, Hard, Insane }

    // Actor waves from EncounterGroups 97730, 97752, 97741 and 97746.
    public sealed class HyboreaInteriorWave : MonoBehaviour
    {
        const int BurstPlacementAttempts = 12;
        readonly List<CharacterInstance> actors = new List<CharacterInstance>(70);
        int waveIndex;
        int total;
        int activeLimit;
        float firstInterval;
        float lastInterval;
        int swords;
        int bows;
        bool limitBow;
        int guardians;
        CharacterInstance target;
        CharacterInstance bow;
        HyboreaInteriorEncounterSequence sequence;
        System.Random random;
        Camera gameplayCamera;
        int placementAttempt;
        float nextSpawn;
        Skeleton1HSpawn.Kind? pendingKind;

        internal int SpawnedCount { get; private set; }
        internal int BowSpawnCount { get; private set; }
        internal IReadOnlyList<CharacterInstance> Actors => actors;

        internal void BindSequence(HyboreaInteriorEncounterSequence owner) => sequence = owner;

        public static bool TryStart(CharacterInstance target, float matchStartedAt, int index = 1,
            HyboreaInteriorDifficulty difficulty = HyboreaInteriorDifficulty.Easy) =>
            TryStart(target, matchStartedAt, new System.Random(), out _, index, difficulty);

        internal static int GetWaveCount(HyboreaInteriorDifficulty difficulty) => difficulty switch
        {
            HyboreaInteriorDifficulty.Easy or HyboreaInteriorDifficulty.Normal => 5,
            HyboreaInteriorDifficulty.Hard => 6,
            HyboreaInteriorDifficulty.Insane => 10,
            _ => 0
        };

        internal static bool TryStart(CharacterInstance target, float matchStartedAt, System.Random random,
            out HyboreaInteriorWave wave, int index = 1,
            HyboreaInteriorDifficulty difficulty = HyboreaInteriorDifficulty.Easy)
        {
            wave = null;
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0 || index < 1 || index > GetWaveCount(difficulty))
                return false;

            foreach (var existing in target.GetComponents<HyboreaInteriorWave>())
                if (existing.waveIndex == index) return false;

            wave = target.gameObject.AddComponent<HyboreaInteriorWave>();
            wave.waveIndex = index;
            wave.limitBow = difficulty == HyboreaInteriorDifficulty.Easy;
            (float start, int total, int active, float first, float last, int swords, int bows, int guardians) =
                wave.limitBow ? index switch
            {
                1 => (3f, 450, 40, .70f, .61f, 10, 1, 0),
                2 => (300f, 250, 50, .50f, .45f, 10, 1, 2),
                3 => (420f, 300, 60, .45f, .33f, 8, 1, 2),
                4 => (540f, 450, 70, .45f, .345f, 8, 1, 3),
                _ => (720f, 460, 70, .35f, .35f, 8, 2, 3)
            } : index switch
            {
                1 => (3f, 655, 50, .50f, .40f, 10, 1, 0),
                2 => (300f, 340, 75, .40f, .30f, 10, 1, 2),
                3 => (420f, 400, 100, .35f, .22f, 8, 1, 2),
                4 => (540f, 600, 120, .35f, .24f, 8, 1, 3),
                5 => (720f, difficulty == HyboreaInteriorDifficulty.Normal ? 620 : 680, 120, .32f, .20f, 8, 2, 3),
                6 => (900f, difficulty == HyboreaInteriorDifficulty.Hard ? 430 : 470, 120, .30f, .20f, 6, 2, 3),
                7 => (1020f, 500, 140, .27f, .20f, 6, 2, 4),
                8 => (1140f, 200, 200, .20f, .20f, 6, 2, 4),
                9 => (1185f, 30, 30, 0f, 0f, 1, 0, 0),
                _ => (1188f, 35, 35, 0f, 0f, 1, 0, 0)
            };
            wave.total = total;
            wave.activeLimit = active;
            wave.firstInterval = first;
            wave.lastInterval = last;
            wave.swords = swords;
            wave.bows = bows;
            wave.guardians = guardians;
            wave.target = target;
            wave.random = random;
            wave.nextSpawn = matchStartedAt + start;
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
            if (SpawnedCount >= total || now < nextSpawn) return;
            int active = 0;
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i];
                if (!actor) actors.RemoveAt(i);
                else if (actor.CurrentHealth > 0) active++;
            }
            int slots = activeLimit - active;
            if (sequence && slots > 0)
                slots = Mathf.Min(slots, sequence.GetAvailableSlots(this, active, now));
            for (; slots > 0 && SpawnedCount < total && now >= nextSpawn; slots--)
                if (!TrySpawnNext(now)) break;
        }

        internal int CountActiveActors()
        {
            int count = 0;
            foreach (var actor in actors)
                if (actor && actor.CurrentHealth > 0) count++;
            return count;
        }

        bool TrySpawnNext(float now)
        {
            var kind = pendingKind ??= PickActor();
            int attempts = firstInterval == 0f && lastInterval == 0f ? BurstPlacementAttempts : 1;
            for (int i = 0; i < attempts; i++)
            {
                if (!Skeleton1HSpawn.TrySpawnAtScreenEdge(target, kind, ref gameplayCamera,
                    ref placementAttempt, out var enemy))
                    continue;

                actors.Add(enemy);
                SpawnedCount++;
                if (kind == Skeleton1HSpawn.Kind.SphinxBow)
                {
                    bow = enemy;
                    BowSpawnCount++;
                }
                pendingKind = null;
                nextSpawn = now + Mathf.Lerp(firstInterval, lastInterval, (float)SpawnedCount / total);
                return true;
            }

            nextSpawn = now + .5f;
            return false;
        }

        Skeleton1HSpawn.Kind PickActor()
        {
            int eligibleBows = limitBow && bow && bow.CurrentHealth > 0 ? 0 : bows;
            int roll = random.Next(swords + eligibleBows + guardians);
            if (roll < eligibleBows) return Skeleton1HSpawn.Kind.SphinxBow;
            return roll < eligibleBows + swords
                ? Skeleton1HSpawn.Kind.SphinxSkeleton
                : Skeleton1HSpawn.Kind.SphinxGuardian;
        }

        void OnDestroy()
        {
            if (!NetworkServer.active) return;
            foreach (var actor in actors)
                if (actor) NetworkServer.Destroy(actor.gameObject);
        }
    }
}
