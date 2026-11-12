using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string WispEvolution = "active_seer_C_evolve";
        private int wispRank;
        private float wispReady;
        private WispCast wisps;
        private const float WispOrbitSpeed = 135f;

        private sealed class WispCast
        {
            public float started, duration, lastRotation;
            public Vector3 lastOrigin;
            public int count, completedRotations;
            public bool evolved;
            public readonly Dictionary<Health, int> hits = new Dictionary<Health, int>();
        }

        private bool CanChooseWisps() => AllowsRaidClass(RaidClass.Seeress) && PendingRewards > 0 && wispRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[wispRank] && (wispRank < 3 || progress.IsLearned(WispEvolution));

        internal bool TryChooseWisps()
        {
            if (!CanChooseWisps())
                return false;

            wispRank++;
            rewardsSpent++;
            return true;
        }

        private void TryWisps(float now)
        {
            if (wispRank == 0 || now < wispReady || wisps != null)
                return;

            bool evolved = wispRank == 4 && progress.IsLearned(WispEvolution);
            wispReady = now + ScaleSkillCooldown(8f);
            wisps = new WispCast
            {
                started = now,
                lastOrigin = caster.transform.position,
                duration = ScaleSeerSkillDuration(evolved ? 4.5f : 4f),
                count = evolved ? 9 : 1 + Mathf.Min(wispRank, 3),
                evolved = evolved
            };
        }

        private void TickWisps(float now)
        {
            var cast = wisps;
            if (cast == null)
                return;

            float elapsed = Mathf.Max(0, now - cast.started);
            bool expired = elapsed >= cast.duration;
            float rotation = Mathf.Min(elapsed, cast.duration) * WispOrbitSpeed;
            Vector3 fromOrigin = cast.lastOrigin;
            float frameRotation = elapsed * WispOrbitSpeed - cast.lastRotation;
            Vector3 origin = Vector3.Lerp(fromOrigin, caster.transform.position, frameRotation > 0f
                ? (rotation - cast.lastRotation) / frameRotation : 1f);
            float radius = cast.evolved ? 3f : 5f;
            float from = cast.lastRotation;
            while (rotation >= (cast.completedRotations + 1) * 360f)
            {
                float boundary = (cast.completedRotations + 1) * 360f;
                Vector3 boundaryOrigin = Vector3.Lerp(cast.lastOrigin, origin,
                    (boundary - cast.lastRotation) / (rotation - cast.lastRotation));
                SweepWispFrame(cast, fromOrigin, boundaryOrigin, radius, from, boundary);
                cast.hits.Clear();
                cast.completedRotations++;
                from = boundary;
                fromOrigin = boundaryOrigin;
            }
            SweepWispFrame(cast, fromOrigin, origin, radius, from, rotation);
            cast.lastRotation = rotation;
            cast.lastOrigin = origin;
            if (!expired)
                return;

            wisps = null;
            if (cast.evolved)
            {
                for (int i = 0; i < cast.count; i++)
                {
                    float angle = 360f * i / cast.count + rotation;
                    Vector3 position = origin + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
                    Strike(position, 2f, ScaleClassSkillDamage(30));
                }
            }
        }

        private void SweepWispFrame(WispCast cast, Vector3 start, Vector3 end, float radius, float from, float to)
        {
            if ((end - start).sqrMagnitude < 0.000001f)
            {
                SweepWisps(cast, end, radius, from, to);
                return;
            }

            const float contactRadius = 0.5f;
            CollectTargets((start + end) * 0.5f, radius + contactRadius + Vector3.Distance(start, end) * 0.5f);
            if (targets.Count == 0)
                return;

            // A 12-degree chord stays within 3 cm of a five-metre orbit.
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / 12f));
            for (int i = 0; i < cast.count; i++)
            {
                int bit = 1 << i;
                float phase = 360f * i / cast.count;
                Vector3 previous = start + Quaternion.Euler(0f, phase + from, 0f) * Vector3.forward * radius;
                for (int step = 1; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    Vector3 point = Vector3.Lerp(start, end, t) +
                        Quaternion.Euler(0f, phase + Mathf.Lerp(from, to, t), 0f) * Vector3.forward * radius;
                    Vector3 path = point - previous;
                    float lengthSquared = path.sqrMagnitude;
                    foreach (Health target in targets)
                    {
                        if (!target || target.CurrentHealth <= 0)
                            continue;
                        cast.hits.TryGetValue(target, out int mask);
                        if ((mask & bit) != 0)
                            continue;
                        Vector3 offset = target.transform.position - previous;
                        float fraction = lengthSquared > 0f ? Mathf.Clamp01(Vector3.Dot(offset, path) / lengthSquared) : 0f;
                        if ((offset - path * fraction).sqrMagnitude > contactRadius * contactRadius)
                            continue;
                        cast.hits[target] = mask | bit;
                        Deal(target, ScaleClassSkillDamage(12));
                    }
                    previous = point;
                }
            }
        }

        private void SweepWisps(WispCast cast, Vector3 origin, float radius, float from, float to)
        {
            const float contactRadius = 0.5f;
            CollectTargets(origin, radius + contactRadius);
            float halfSweep = (to - from) * 0.5f;
            float middle = from + halfSweep;
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - origin;
                float targetAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                cast.hits.TryGetValue(target, out int mask);
                for (int i = 0; i < cast.count && target && target.CurrentHealth > 0; i++)
                {
                    int bit = 1 << i;
                    if ((mask & bit) != 0)
                        continue;

                    float center = middle + 360f * i / cast.count;
                    float delta = Mathf.DeltaAngle(center, targetAngle);
                    float closestAngle = center + Mathf.Clamp(delta, -halfSweep, halfSweep);
                    Vector3 closest = Quaternion.Euler(0, closestAngle, 0) * Vector3.forward * radius;
                    if ((offset - closest).sqrMagnitude > contactRadius * contactRadius)
                        continue;

                    mask |= bit;
                    cast.hits[target] = mask;
                    Deal(target, ScaleClassSkillDamage(12));
                }
            }
        }
    }
}
