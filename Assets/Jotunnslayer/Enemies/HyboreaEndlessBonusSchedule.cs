using System;
using System.Collections.Generic;

namespace Jotunnslayer
{
    // EncounterGroup 97735. This clock is independent of spawn success and living actors.
    // Native EnemyFight.GenerateNewEndlessWave uses the LAST appended estimated end,
    // plus the parent's delay, then rounds to nearest even before appending the successor.
    internal sealed class HyboreaEndlessBonusSchedule
    {
        internal readonly struct Wave
        {
            internal readonly int Track;
            internal readonly int Round;
            internal readonly int At;
            internal readonly int Total;
            internal readonly int MaxActive;
            internal readonly float Interval;
            internal readonly float NextDelay;

            internal Wave(int track, int round, int at, int total, int maxActive, float interval, float nextDelay)
            {
                Track = track;
                Round = round;
                At = at;
                Total = total;
                MaxActive = maxActive;
                Interval = interval;
                NextDelay = nextDelay;
            }

            internal float EstimatedEnd => At + Interval * Total;
        }

        readonly List<Wave> pending = new List<Wave>(5);
        float lastScheduledEnd;

        // Full group planning includes W3/W4 even though their exported total is zero.
        // Their zero-total spawn semantics still require verification of the original fight update.
        internal void Reset(bool wholeGroup)
        {
            Clear();
            if (wholeGroup)
            {
                Append(new Wave(1, 1, 45, 3, 3, 4f, 45f));
                Append(new Wave(2, 1, 90, 1, 3, 0f, 33f));
                Append(new Wave(3, 1, 135, 0, 0, .03f, 45f));
                Append(new Wave(4, 1, 140, 0, 0, .03f, 4.4f));
            }
            Append(new Wave(5, 1, 180, 1, 2, 0f, 39.4f));
        }

        internal bool TryTakeDue(float elapsed, out Wave wave)
        {
            if (pending.Count == 0 || elapsed < pending[0].At)
            {
                wave = default;
                return false;
            }

            wave = pending[0];
            pending.RemoveAt(0);
            int at = checked((int)Math.Round((double)(lastScheduledEnd + wave.NextDelay), MidpointRounding.ToEven));
            int total = wave.Track == 1 ? checked(wave.Total + 1) : wave.Track == 5 ? 2 : wave.Total;
            float delay = wave.Track == 2 ? Math.Max(1f, wave.NextDelay - 4f) :
                wave.Track == 4 ? Math.Max(1f, wave.NextDelay - .06f) :
                wave.Track == 5 ? Math.Max(1f, wave.NextDelay - .03f) : wave.NextDelay;
            Append(new Wave(wave.Track, checked(wave.Round + 1), at, total,
                wave.MaxActive, wave.Interval, delay));
            return true;
        }

        void Append(Wave wave)
        {
            pending.Add(wave);
            lastScheduledEnd = wave.EstimatedEnd;
        }

        internal void Clear()
        {
            pending.Clear();
            lastScheduledEnd = 0f;
        }
    }
}
