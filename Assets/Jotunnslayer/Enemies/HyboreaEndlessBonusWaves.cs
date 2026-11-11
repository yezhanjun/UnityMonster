using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Group 97735. W1/W2/W5 spawn actors; exported W3/W4 have zero total and zero total delta.
    // Preserve their shared-clock entries without inventing ghost amounts. Runtime table overrides
    // in the original game have not been verified.
    [DisallowMultipleComponent]
    public sealed class HyboreaEndlessBonusWaves : MonoBehaviour
    {
        sealed class Pending
        {
            internal HyboreaEndlessBonusSchedule.Wave Wave;
            internal int Spawned;
            internal float NextSpawn;
        }

        readonly HyboreaEndlessBonusSchedule clock = new HyboreaEndlessBonusSchedule();
        readonly List<Pending> pending = new List<Pending>(5);
        readonly List<(CharacterInstance actor, int track, int round)> actors =
            new List<(CharacterInstance, int, int)>(16);
        CharacterInstance target;
        Camera gameplayCamera;
        float matchStartedAt;
        int placementAttempt;
        bool running;
        internal readonly int[] SpawnedByTrack = new int[6];
        internal int EmptyWaveStarts { get; private set; }
        internal bool Running => running;
        internal int PendingCount => pending.Count;
        internal int ActorCount => actors.Count;
        internal CharacterInstance GetActor(int index) => actors[index].actor;
        internal int GetActorRound(int index) => actors[index].round;
        internal int GetActorTrack(int index) => actors[index].track;

        public bool TryStart(CharacterInstance player, float startedAt)
        {
            if (!isActiveAndEnabled || running || target || pending.Count > 0 || actors.Count > 0 ||
                !NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !player || player.CurrentHealth <= 0) return false;
            target = player;
            matchStartedAt = startedAt;
            clock.Reset(wholeGroup: true);
            running = true;
            return true;
        }

        internal CharacterInstance FirstLivingActor(int track)
        {
            foreach (var entry in actors)
                if (entry.track == track && entry.actor && entry.actor.CurrentHealth > 0) return entry.actor;
            return null;
        }

        void Update()
        {
            if (!running) return;
            if (!NetworkServer.activeHost || NetworkServer.connections.Count != 1 || !target || target.CurrentHealth <= 0)
            {
                StopWaves();
                return;
            }
            float now = Time.time;
            // Keep catch-up bounded; scheduled times remain unchanged on later frames.
            for (int i = 0; i < 5 && clock.TryTakeDue(now - matchStartedAt, out var row); i++)
            {
                if (row.Track == 3 || row.Track == 4)
                {
                    // Native UpdateFight finishes GlobalTime waves once SpawnedCount >= TotalSpawned.
                    // Both values are zero for these unmodified exported rows.
                    EmptyWaveStarts++;
                    continue;
                }
                pending.Add(new Pending { Wave = row, NextSpawn = matchStartedAt + row.At });
            }
            for (int i = actors.Count - 1; i >= 0; i--)
                if (!actors[i].actor) actors.RemoveAt(i);

            for (int i = 0; i < pending.Count;)
            {
                var round = pending[i];
                int active = 0;
                foreach (var entry in actors)
                    if (entry.track == round.Wave.Track && entry.round == round.Wave.Round &&
                        entry.actor && entry.actor.CurrentHealth > 0) active++;
                while (round.Spawned < round.Wave.Total && active < round.Wave.MaxActive && now >= round.NextSpawn)
                {
                    var kind = round.Wave.Track == 1 ? Skeleton1HSpawn.Kind.HorrorJotun :
                        round.Wave.Track == 2 ? Skeleton1HSpawn.Kind.BatLoot : Skeleton1HSpawn.Kind.SphinxMiniBoss;
                    if (!Skeleton1HSpawn.TrySpawnAtScreenEdge(target, kind,
                        ref gameplayCamera, ref placementAttempt, out var actor))
                    {
                        round.NextSpawn = now + .5f;
                        break;
                    }
                    actors.Add((actor, round.Wave.Track, round.Wave.Round));
                    round.Spawned++;
                    SpawnedByTrack[round.Wave.Track]++;
                    active++;
                    round.NextSpawn = now + round.Wave.Interval;
                    if (round.Wave.Interval > 0f) break;
                }
                if (round.Spawned == round.Wave.Total) pending.RemoveAt(i);
                else i++;
            }
        }

        public void StopWaves()
        {
            if (NetworkServer.active)
                foreach (var entry in actors)
                    if (entry.actor) NetworkServer.Destroy(entry.actor.gameObject);
            actors.Clear();
            pending.Clear();
            clock.Clear();
            running = false;
            System.Array.Clear(SpawnedByTrack, 0, SpawnedByTrack.Length);
            EmptyWaveStarts = 0;
            target = null;
            gameplayCamera = null;
            matchStartedAt = 0f;
            placementAttempt = 0;
        }

        void OnDisable() => StopWaves();
    }
}
