using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // waves_hyb_endless_interior (97734), W1 or the shared W1-W4 group.
    [DisallowMultipleComponent]
    public sealed class HyboreaEndlessInteriorOpeningWave : MonoBehaviour
    {
        const int ActiveLimit = 150;
        const int FirstTotal = 110;
        static readonly (Skeleton1HSpawn.Kind kind, int weight, int firstRound, int lastRound, int cap, int capStep)[] Slots =
        {
            (Skeleton1HSpawn.Kind.SphinxSkeleton, 70, 1, int.MaxValue, 200, -5),
            (Skeleton1HSpawn.Kind.SphinxBow, 10, 3, 12, 8, 1),
            (Skeleton1HSpawn.Kind.SphinxGuardian, 30, 5, int.MaxValue, 4, 6),
            (Skeleton1HSpawn.Kind.Milipede, 4, 10, int.MaxValue, 1, 1)
        };

        sealed class PendingRound
        {
            internal int Track;
            internal bool Started;
            internal float ScheduledAt;
            internal int Number;
            internal int Total;
            internal int Spawned;
            internal int PendingSlot = -1;
            internal float FirstInterval;
            internal float LastInterval;
            internal float NextSpawn;
        }

        readonly List<PendingRound> pending = new List<PendingRound>();
        readonly List<(CharacterInstance actor, int track, int round, int slot)> actors = new List<(CharacterInstance, int, int, int)>(ActiveLimit);
        readonly int[] slotCounts = new int[Slots.Length];
        CharacterInstance target;
        Camera gameplayCamera;
        int placementAttempt;
        float nextRoundAt;
        int roundNumber;
        bool group;
        float matchStartedAt;
        float lastScheduledEnd;
        internal int SpawnedCount { get; private set; }
        internal readonly int[] SpawnedByTrack = new int[4];
        internal int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var entry in actors)
                    if (entry.actor && entry.actor.CurrentHealth > 0) count++;
                return count;
            }
        }

        internal CharacterInstance FirstLivingActor
        {
            get
            {
                foreach (var entry in actors)
                    if (entry.actor && entry.actor.CurrentHealth > 0) return entry.actor;
                return null;
            }
        }

        public bool TryStart(CharacterInstance player, float matchStartedAt)
            => TryStart(player, matchStartedAt, false);

        public bool TryStartGroup(CharacterInstance player, float matchStartedAt)
            => TryStart(player, matchStartedAt, true);

        bool TryStart(CharacterInstance player, float startedAt, bool includeCircular)
        {
            if (!isActiveAndEnabled || target || pending.Count > 0 || actors.Count > 0 || !NetworkServer.activeHost ||
                NetworkServer.connections.Count != 1 || !player || player.CurrentHealth <= 0)
                return false;

            target = player;
            group = includeCircular;
            matchStartedAt = startedAt;
            nextRoundAt = startedAt + 3f;
            if (group)
            {
                Schedule(0, 1, 3f);
                Schedule(1, 1, 48f);
                Schedule(2, 1, 52f);
                Schedule(3, 1, 56f);
            }
            return true;
        }

        void Schedule(int track, int number, float at)
        {
            float average = track == 0 ? Mathf.Max(.05f, .4f - (number - 1) * .007f) : 0f;
            int total = track == 0 ? FirstTotal + (number - 1) * 2 : 12 + number - 1;
            lastScheduledEnd = at + average * total;
            pending.Add(new PendingRound
            {
                Track = track, Number = number, Total = total, ScheduledAt = at,
                FirstInterval = average * 1.75f,
                LastInterval = average * .25f, NextSpawn = matchStartedAt + at
            });
        }

        internal static int GetCircularSlotWeight(int track, int slot, int round, int active)
        {
            int first = slot == 0 ? 2 : slot == 1 ? 7 : slot == 2 ? (track == 1 ? 6 : 8) : 14;
            int cap = slot == 0 ? 0 : slot == 1 || slot == 3 ? round :
                (track == 1 ? 2 + (round - 1) * 4 : 8 + (round - 1) * 8);
            int weight = slot == 0 ? 70 : slot == 1 ? 10 : slot == 2 ? 30 : 5;
            return round >= first && (cap == 0 || active < cap) ? weight : 0;
        }

        internal static int GetSlotWeight(int slot, int round, int active)
        {
            var row = Slots[slot];
            int cap = Mathf.Max(1, row.cap + (round - 1) * row.capStep);
            return round >= row.firstRound && round <= row.lastRound && active < cap ? row.weight : 0;
        }

        void Update()
        {
            if (!target || !NetworkServer.activeHost || NetworkServer.connections.Count != 1 || target.CurrentHealth <= 0)
            {
                if (roundNumber > 0 || target || pending.Count > 0 || actors.Count > 0) StopWave();
                return;
            }

            float now = Time.time;
            if (!group && now >= nextRoundAt) QueueRound();
            if (group)
            {
                // Only rounds present on entry may start this frame, including zero-delay tracks.
                int scheduledCount = pending.Count;
                for (int i = 0; i < scheduledCount; i++)
                {
                    var scheduled = pending[i];
                    if (scheduled.Started || now < matchStartedAt + scheduled.ScheduledAt) continue;
                    scheduled.Started = true;
                    float delay = scheduled.Track == 0 ? 4f + (scheduled.Number - 1) * .1f :
                        scheduled.Track == 1 ? 0f : 4f + (scheduled.Number - 1) * .05f;
                    Schedule(scheduled.Track, scheduled.Number + 1, (int)(lastScheduledEnd + delay));
                }
            }
            if (pending.Count == 0) return;
            int active = 0;
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i].actor;
                if (!actor) actors.RemoveAt(i);
                else if (actor.CurrentHealth > 0) active++;
            }
            for (int i = 0; i < pending.Count && active < ActiveLimit;)
            {
                var round = pending[i];
                if (group && !round.Started) { i++; continue; }
                int waveActive = 0;
                foreach (var entry in actors)
                    if (entry.track == round.Track && entry.round == round.Number && entry.actor && entry.actor.CurrentHealth > 0)
                        waveActive++;
                int limit = round.Track == 0 ? ActiveLimit : 120;
                while (active < ActiveLimit && waveActive < limit && round.Spawned < round.Total && now >= round.NextSpawn)
                {
                    if (!TrySpawn(round, now)) break;
                    active++;
                    waveActive++;
                    if (round.FirstInterval > 0f) break;
                }
                if (round.Spawned == round.Total) pending.RemoveAt(i);
                else i++;
            }
        }

        void QueueRound()
        {
            int number = ++roundNumber;
            int total = FirstTotal + (number - 1) * 2;
            float average = Mathf.Max(.05f, .4f - (number - 1) * .007f);
            pending.Add(new PendingRound
            {
                Number = number,
                Total = total,
                FirstInterval = average * 1.75f,
                LastInterval = average * .25f,
                NextSpawn = nextRoundAt
            });
            nextRoundAt += average * total + 4f + (number - 1) * .1f;
        }

        bool TrySpawn(PendingRound round, float now)
        {
            if (round.PendingSlot < 0)
            {
                System.Array.Clear(slotCounts, 0, slotCounts.Length);
                foreach (var entry in actors)
                    if (entry.track == round.Track && entry.round == round.Number && entry.actor && entry.actor.CurrentHealth > 0)
                        slotCounts[entry.slot]++;

                int weight = 0;
                for (int i = 0; i < Slots.Length; i++) weight += SlotWeight(round, i);
                if (weight == 0)
                {
                    // Circular first rounds contain no unlocked candidates in the original table.
                    if (round.Track > 0 && round.Number == 1) round.Total = round.Spawned;
                    round.NextSpawn = now + .25f;
                    return false;
                }
                int roll = Random.Range(0, weight);
                for (int i = 0; i < Slots.Length; i++)
                {
                    roll -= SlotWeight(round, i);
                    if (roll >= 0) continue;
                    round.PendingSlot = i;
                    break;
                }
            }

            int slot = round.PendingSlot;
            CharacterInstance actor;
            bool placed = round.Track == 0
                ? Skeleton1HSpawn.TrySpawnAtScreenEdge(target, Slots[slot].kind, ref gameplayCamera, ref placementAttempt, out actor)
                : Skeleton1HSpawn.TrySpawnOutsideViewCircle(target, Slots[slot].kind, ref gameplayCamera, out actor);
            if (!placed)
            {
                round.NextSpawn = now + .5f;
                return false;
            }

            actors.Add((actor, round.Track, round.Number, slot));
            round.PendingSlot = -1;
            round.Spawned++;
            SpawnedCount++;
            SpawnedByTrack[round.Track]++;
            round.NextSpawn = now + Mathf.Lerp(round.FirstInterval, round.LastInterval, (float)round.Spawned / round.Total);
            return true;
        }

        int SlotWeight(PendingRound round, int slot) => round.Track == 0
            ? GetSlotWeight(slot, round.Number, slotCounts[slot])
            : GetCircularSlotWeight(round.Track, slot, round.Number, slotCounts[slot]);

        public void StopWave()
        {
            if (NetworkServer.active)
                foreach (var entry in actors)
                    if (entry.actor) NetworkServer.Destroy(entry.actor.gameObject);
            actors.Clear();
            pending.Clear();
            target = null;
            gameplayCamera = null;
            placementAttempt = 0;
            roundNumber = SpawnedCount = 0;
            group = false;
            lastScheduledEnd = 0f;
            System.Array.Clear(SpawnedByTrack, 0, SpawnedByTrack.Length);
        }

        void OnDisable() => StopWave();
    }
}
