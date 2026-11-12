using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SwirlingEvolution = "active_berserker_A_evolve";
        private int swirlingRank;
        private float swirlingReady;
        private SwirlingCast swirling;

        private sealed class SwirlingCast
        {
            public Vector3 origin;
            public float started, elapsed, yaw, radius;
            public int count, damage;
            public readonly Dictionary<Health, int> hits = new Dictionary<Health, int>();
        }

        private bool CanChooseSwirling() => AllowsRaidClass(RaidClass.Berserker) && PendingRewards > 0 && swirlingRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[swirlingRank] && (swirlingRank < 3 || progress.IsLearned(SwirlingEvolution));

        internal bool TryChooseSwirling()
        {
            if (!CanChooseSwirling())
                return false;

            swirlingRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySwirling(float now)
        {
            if (swirlingRank == 0 || now < swirlingReady || swirling != null)
                return;

            int rank = Mathf.Min(swirlingRank, 3);
            bool evolved = swirlingRank == 4 && progress.IsLearned(SwirlingEvolution);
            swirlingReady = now + ScaleSkillCooldown(evolved ? 3f : 2.5f + rank * 0.5f);
            swirling = new SwirlingCast
            {
                origin = caster.transform.position,
                yaw = caster.transform.eulerAngles.y,
                started = now,
                radius = evolved ? 0.9f : 0.45f,
                count = evolved ? 1 : rank,
                damage = evolved ? 30 : 12 + rank * 2
            };
        }

        private void TickSwirling(float now)
        {
            var cast = swirling;
            if (cast == null)
                return;

            float elapsed = Mathf.Clamp(now - cast.started, cast.elapsed, 1.5f);
            CollectTargets(cast.origin, 7f + cast.radius);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - cast.origin;
                cast.hits.TryGetValue(target, out int mask);
                int samples = Mathf.Max(1, Mathf.CeilToInt((elapsed - cast.elapsed) / 0.05f));
                for (int i = 0; i < cast.count; i++)
                {
                    int bit = 1 << i;
                    if ((mask & bit) != 0)
                        continue;

                    float phase = cast.yaw + i * 360f / cast.count;
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        float time = Mathf.Lerp(cast.elapsed, elapsed, (float)sample / samples);
                        Vector3 point = Quaternion.Euler(0, phase + time * 240f, 0) * Vector3.forward * (1f + time * 4f);
                        if ((offset - point).sqrMagnitude > cast.radius * cast.radius)
                            continue;

                        mask |= bit;
                        cast.hits[target] = mask;
                        Deal(target, ScaleClassSkillDamage(cast.damage));
                        break;
                    }

                    if (!target || target.CurrentHealth <= 0)
                        break;
                }
            }

            cast.elapsed = elapsed;
            if (elapsed >= 1.5f)
                swirling = null;
        }
    }
}
