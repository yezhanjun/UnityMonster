using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Actor waves from EncounterGroup 97680. W0 and W82 have empty pools.
    public sealed class AlfheimNormalOpeningWave : MonoBehaviour
    {
        internal const int SpawnCount = 25;
        const int MaxPlacementFailures = 24;

        readonly List<CharacterInstance> actors = new List<CharacterInstance>(40);
        readonly List<CharacterInstance> rangedActors = new List<CharacterInstance>(3);
        int waveIndex;
        int total;
        int maxActive;
        int maxRanged;
        int spiderWeight;
        int meleeWeight;
        int rangedWeight;
        int veteranSpiderWeight;
        readonly List<CharacterInstance> insaneElves = new List<CharacterInstance>(2);
        float initialInterval;
        float finalInterval;
        CharacterInstance target;
        System.Random random;
        Camera gameplayCamera;
        Skeleton1HSpawn.Kind? pendingKind;
        int placementAttempt;
        int failures;
        float nextSpawn;
        bool stopped;

        internal int SpiderCount { get; private set; }
        internal int ElfCount { get; private set; }
        internal int RangedCount { get; private set; }
        internal int StoneJotunnCount { get; private set; }
        internal int VeteranSpiderCount { get; private set; }
        internal int DriderCount { get; private set; }
        internal int InsaneElfCount { get; private set; }
        internal int SpawnedCount => SpiderCount + ElfCount + RangedCount + StoneJotunnCount + VeteranSpiderCount + DriderCount + InsaneElfCount;
        internal int ActiveRangedCount => CountAlive(rangedActors);
        internal int ActiveInsaneElfCount => CountAlive(insaneElves);
        internal IReadOnlyList<CharacterInstance> Actors => actors;
        bool IsStampede => waveIndex is 2 or 6 or 7 or 13 or 14 or 15 or 19 or 23 or 24
            or 27 or 28 or 29 or 32 or 35 or 36 or 37 or 43 or 44 or 45 or 49 or 50 or 52 or 54 or 59 or 60 or 61
            or 65 or 66 or 67 or 68 or 71 or 72 or 73 or 77 or 78 or 79 or 80 or 81;

        public static bool TryStart(CharacterInstance target, float matchStartedAt, int index = 1) =>
            TryStart(target, matchStartedAt, new System.Random(), out _, index);

        internal static bool TryStart(CharacterInstance target, float matchStartedAt, System.Random random,
            out AlfheimNormalOpeningWave wave, int index = 1)
        {
            wave = null;
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !target || target.CurrentHealth <= 0 || !Supports(index))
                return false;

            foreach (var existing in target.GetComponents<AlfheimNormalOpeningWave>())
                if (existing.waveIndex == index) return false;

            var (start, total, active, first, last) = index switch
            {
                2 => (15f, 30, 30, 0f, 0f),
                3 => (17f, 13, 15, .7f, .4f),
                4 => (25f, 65, 40, .3f, .3f),
                5 => (45f, 40, 40, .45f, .25f),
                6 => (60f, 20, 20, 0f, 0f),
                7 => (63f, 30, 30, 0f, 0f),
                8 => (65f, 15, 15, 0f, 0f),
                9 => (70f, 1, 1, 0f, 0f),
                10 => (72f, 103, 60, .6f, .3f),
                11 => (120f, 1, 1, 0f, 0f),
                12 => (125f, 150, 80, .5f, .1f),
                13 => (175f, 30, 32, 0f, 0f),
                14 => (177f, 30, 32, 0f, 0f),
                15 => (179f, 30, 32, 0f, 0f),
                16 => (180f, 1, 1, 0f, 0f),
                17 => (185f, 80, 70, .3f, .3f),
                18 => (210f, 135, 80, .3f, .1f),
                19 => (238f, 40, 42, 0f, 0f),
                20 => (240f, 1, 1, 0f, 0f),
                21 => (241f, 50, 500, .25f, .25f),
                22 => (255f, 50, 400, .6f, .3f),
                23 => (280f, 30, 32, 0f, 0f),
                24 => (282f, 30, 32, 0f, 0f),
                25 => (285f, 50, 50, .4f, .1f),
                26 => (300f, 1, 1, 0f, 0f),
                27 => (305f, 35, 37, 0f, 0f),
                28 => (307f, 4, 4, 0f, 0f),
                29 => (309f, 35, 37, 0f, 0f),
                30 => (311f, 70, 40, .25f, .25f),
                31 => (330f, 120, 80, .25f, .15f),
                32 => (355f, 6, 6, 0f, 0f),
                33 => (360f, 1, 1, 0f, 0f),
                34 => (362f, 210, 80, .2f, .15f),
                35 => (400f, 25, 25, 0f, 0f),
                36 => (403f, 50, 52, 0f, 0f),
                37 => (406f, 25, 25, 0f, 0f),
                38 => (410f, 50, 50, .2f, .18f),
                39 => (420f, 1, 1, 0f, 0f),
                40 => (421f, 230, 230, .4f, .1f),
                41 => (480f, 1, 1, 0f, 0f),
                42 => (485f, 135, 80, .3f, .2f),
                43 => (520f, 40, 40, 0f, 0f),
                44 => (523f, 6, 6, 0f, 0f),
                45 => (526f, 40, 40, 0f, 0f),
                46 => (530f, 40, 135, .25f, .2f),
                47 => (540f, 1, 1, 0f, 0f),
                48 => (545f, 80, 80, .3f, .3f),
                49 => (570f, 40, 40, 0f, 0f),
                50 => (573f, 30, 30, 0f, 0f),
                51 => (576f, 40, 40, .15f, .15f),
                52 => (583f, 30, 30, 0f, 0f),
                53 => (586f, 54, 210, .15f, .15f),
                54 => (595f, 40, 40, 0f, 0f),
                55 => (600f, 1, 1, 0f, 0f),
                56 => (605f, 200, 80, .35f, .18f),
                57 => (660f, 1, 1, 0f, 0f),
                58 => (665f, 170, 80, .25f, .25f),
                59 => (710f, 35, 40, 0f, 0f),
                60 => (713f, 35, 35, 0f, 0f),
                61 => (716f, 35, 40, 0f, 0f),
                62 => (720f, 1, 1, 0f, 0f),
                63 => (725f, 150, 80, .2f, .1f),
                64 => (750f, 35, 35, .1f, .3f),
                65 => (760f, 35, 35, 0f, 0f),
                66 => (765f, 35, 35, 0f, 0f),
                67 => (770f, 8, 8, 0f, 0f),
                68 => (775f, 35, 35, 0f, 0f),
                69 => (780f, 2, 2, 0f, 0f),
                70 => (785f, 150, 80, .25f, .25f),
                71 => (825f, 40, 40, 0f, 0f),
                72 => (830f, 10, 10, 0f, 0f),
                73 => (835f, 40, 40, 0f, 0f),
                74 => (840f, 2, 2, 0f, 0f),
                75 => (845f, 120, 80, .25f, .1f),
                76 => (870f, 2, 2, 0f, 0f),
                77 => (872f, 30, 30, 0f, 0f),
                78 => (874f, 35, 35, 0f, 0f),
                79 => (876f, 35, 35, 0f, 0f),
                80 => (878f, 30, 30, 0f, 0f),
                81 => (880f, 35, 35, 0f, 0f),
                _ => (3f, SpawnCount, 40, .5f, .4f)
            };
            wave = target.gameObject.AddComponent<AlfheimNormalOpeningWave>();
            wave.waveIndex = index;
            wave.total = total;
            wave.maxActive = active;
            (wave.spiderWeight, wave.meleeWeight, wave.rangedWeight, wave.maxRanged) = index switch
            {
                1 or 5 => (15, 1, 0, 0),
                3 => (0, 4, 1, 2),
                8 => (0, 3, 1, 2),
                10 => (15, 8, 1, 3),
                12 => (0, 5, 1, 3),
                17 or 22 or 38 or 51 => (10, 0, 0, 0),
                18 => (10, 5, 1, 3),
                21 => (5, 3, 1, 3),
                25 => (0, 5, 1, 4),
                28 or 32 or 44 or 67 or 72 => (0, 0, 0, 0),
                31 => (0, 8, 3, 4),
                34 => (10, 0, 1, 4),
                35 or 37 or 50 or 52 or 60 or 64 => (0, 1, 0, 0),
                40 or 65 => (0, 12, 4, 5),
                42 or 46 => (10, 10, 5, 5),
                48 => (0, 8, 1, 5),
                53 => (10, 0, 1, 5),
                56 => (6, 0, 1, 5),
                58 => (0, 13, 4, 5),
                63 => (7, 0, 0, 0),
                70 => (0, 13, 4, 6),
                75 => (5, 3, 1, 6),
                77 or 80 => (0, 5, 1, 8),
                _ => (1, 0, 0, 0)
            };
            wave.veteranSpiderWeight = index switch
            {
                17 or 22 or 28 or 32 or 38 or 42 or 44 or 46 or 51 or 56 or 63 or 67 or 72 => 1,
                58 or 70 => 2,
                _ => 0
            };
            wave.initialInterval = first;
            wave.finalInterval = last;
            wave.target = target;
            wave.random = random;
            wave.nextSpawn = Mathf.Max(Time.time, matchStartedAt + start);
            return true;
        }

        static bool Supports(int index) => index >= 1 && index <= AlfheimNormalEncounterSequence.OpeningWaveCount;

        Skeleton1HSpawn.Kind PickActor()
        {
            if (waveIndex is 62 or 74) return Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane;
            if (waveIndex is 26 or 55) return Skeleton1HSpawn.Kind.AlfheimDrider;
            if (waveIndex is 9 or 11 or 16 or 20 or 33 or 39 or 41 or 47 or 57 or 69 or 76) return Skeleton1HSpawn.Kind.AlfheimStoneJotunn;

            int ranged = maxRanged > 0 && ActiveRangedCount >= maxRanged ? 0 : rangedWeight;
            int insane = waveIndex switch
            {
                40 or 65 when ActiveInsaneElfCount < 2 => 4,
                75 when ActiveInsaneElfCount < 3 => 1,
                _ => 0
            };
            int roll = random.Next(spiderWeight + meleeWeight + ranged + veteranSpiderWeight + insane);
            if (roll < insane) return Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane;
            roll -= insane;
            if (roll < meleeWeight) return Skeleton1HSpawn.Kind.AlfheimElfMelee;
            if (roll < meleeWeight + ranged) return Skeleton1HSpawn.Kind.AlfheimElfRanged;
            return roll < meleeWeight + ranged + spiderWeight
                ? Skeleton1HSpawn.Kind.AlfheimSpider : Skeleton1HSpawn.Kind.AlfheimSpiderVeteran;
        }

        static int CountAlive(List<CharacterInstance> list)
        {
            int alive = 0;
            foreach (var actor in list)
                if (actor && actor.CurrentHealth > 0) alive++;
            return alive;
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
            if (stopped || SpawnedCount >= total || now < nextSpawn) return;
            if (IsStampede)
            {
                SpawnStampede();
                return;
            }

            while (SpawnedCount < total && now >= nextSpawn)
            {
                if (actors.Count >= maxActive && CountAlive(actors) >= maxActive) return;
                if (!SpawnNext(now)) return;
            }
        }

        bool SpawnNext(float now)
        {
            var kind = pendingKind ??= PickActor();
            if (!Skeleton1HSpawn.TrySpawnAtScreenEdge(target, kind, ref gameplayCamera,
                ref placementAttempt, out var enemy))
            {
                nextSpawn = now + .5f;
                if (++failures >= MaxPlacementFailures)
                {
                    stopped = true;
                    Debug.LogWarning($"Alfheim 97680 W{waveIndex} stopped after {SpawnedCount}/{total} spawns.", this);
                }
                return false;
            }

            failures = 0;
            RecordSpawn(enemy, kind);
            pendingKind = null;
            nextSpawn = now + Mathf.Lerp(initialInterval, finalInterval, (float)SpawnedCount / total);
            return true;
        }

        void RecordSpawn(CharacterInstance enemy, Skeleton1HSpawn.Kind kind)
        {
            actors.Add(enemy);
            switch (kind)
            {
                case Skeleton1HSpawn.Kind.AlfheimSpider:
                    SpiderCount++;
                    break;
                case Skeleton1HSpawn.Kind.AlfheimSpiderVeteran:
                    VeteranSpiderCount++;
                    break;
                case Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane:
                    InsaneElfCount++;
                    insaneElves.Add(enemy);
                    break;
                case Skeleton1HSpawn.Kind.AlfheimElfMelee:
                    ElfCount++;
                    break;
                case Skeleton1HSpawn.Kind.AlfheimDrider:
                    DriderCount++;
                    break;
                case Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane:
                case Skeleton1HSpawn.Kind.AlfheimStoneJotunn:
                    StoneJotunnCount++;
                    break;
                case Skeleton1HSpawn.Kind.AlfheimElfRanged:
                    RangedCount++;
                    rangedActors.Add(enemy);
                    break;
            }

        }

        void SpawnStampede()
        {
            Vector3 formationCenter = target.transform.position;
            Quaternion formationFacing = Quaternion.Euler(0f, target.lookInput.y + 180f, 0f);
            Vector3 forward = formationFacing * Vector3.forward;
            Vector3 right = formationFacing * Vector3.right;
            const int mask = MTPSKIT.GameManager.environmentLayer;
            for (int slot = 0; slot < total; slot++)
            {
                var kind = PickActor();
                float gap = kind == Skeleton1HSpawn.Kind.AlfheimSpiderVeteran ? 1.08f : .9f;
                int lane = (slot + 1) / 2 * ((slot & 1) == 0 ? -1 : 1);
                Vector3 candidate = formationCenter - forward * 12f + right * (lane * gap);
                if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var hit, 12f,
                    mask, QueryTriggerInteraction.Ignore) || hit.normal.y < .65f)
                    continue;

                Vector3 spawn = hit.point + Vector3.up * .15f;
                if (Physics.CheckCapsule(spawn + Vector3.up * .5f, spawn + Vector3.up * 1.5f, .45f,
                    mask, QueryTriggerInteraction.Ignore))
                    continue;

                var actor = Skeleton1HSpawn.TrySpawn(target, spawn, formationFacing,
                    kind, passThrough: true);
                if (!actor) continue;

                RecordSpawn(actor, kind);
            }

            stopped = true;
            if (SpawnedCount != total)
                Debug.LogWarning($"Alfheim 97680 W{waveIndex} placed {SpawnedCount}/{total} stampede actors.", this);
        }

        void OnDestroy()
        {
            if (!NetworkServer.active) return;
            foreach (var actor in actors)
                if (actor) NetworkServer.Destroy(actor.gameObject);
        }
    }
}
