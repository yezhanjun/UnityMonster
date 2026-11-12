using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string TrapEvolution = "active_seer_A_evolve";
        private static readonly List<TimeTrap> ActiveTimeTraps = new List<TimeTrap>();
        private static readonly float[] TrapDurations = { 3.5f, 4.2f, 5f, 6f };
        private int trapRank;
        private float trapReady;
        private const float TrapProjectileRadius = 3.2f;

        private sealed class TimeTrap
        {
            public SkillBattlePreview owner;
            public Vector3 center;
            public float expires, nextDamage;
            public int vulnerability;
            public bool evolved;
        }

        private bool CanChooseTrap() => AllowsRaidClass(RaidClass.Seeress) && PendingRewards > 0 && trapRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[trapRank] && (trapRank < 3 || progress.IsLearned(TrapEvolution));

        internal bool TryChooseTrap()
        {
            if (!CanChooseTrap())
                return false;

            trapRank++;
            rewardsSpent++;
            return true;
        }

        private void TryTimeTrap(float now)
        {
            if (trapRank == 0 || now < trapReady)
                return;

            int rank = trapRank == 4 && !progress.IsLearned(TrapEvolution) ? 3 : trapRank;
            bool evolved = rank == 4;
            trapReady = now + ScaleSkillCooldown(evolved ? 6f : 9f);
            ActiveTimeTraps.Add(new TimeTrap
            {
                owner = this,
                center = caster.transform.position,
                expires = now + ScaleSeerSkillDuration(TrapDurations[rank - 1]),
                nextDamage = now + 1f,
                vulnerability = evolved ? 45 : 12 * rank,
                evolved = evolved
            });
        }

        private void ClearTimeTraps()
        {
            for (int i = ActiveTimeTraps.Count - 1; i >= 0; i--)
            {
                if (ActiveTimeTraps[i].owner == this)
                    ActiveTimeTraps.RemoveAt(i);
            }
        }

        private void TickTimeTraps(float now)
        {
            for (int i = ActiveTimeTraps.Count - 1; i >= 0; i--)
            {
                var trap = ActiveTimeTraps[i];
                if (trap.owner != this)
                    continue;

                while (trap.evolved && HasLiveCaster && now >= trap.nextDamage && trap.nextDamage <= trap.expires)
                {
                    trap.nextDamage += 1f;
                    Strike(trap.center, 3f, ScaleClassSkillDamage(10));
                }

                if (now >= trap.expires)
                    ActiveTimeTraps.RemoveAt(i);
            }
        }

        private static bool ContainsEnemy(TimeTrap trap, Health victim)
        {
            var source = trap.owner.caster;
            return source && source.CurrentHealth > 0 && source.Team != victim.Team &&
                (victim.transform.position - trap.center).sqrMagnitude <= 9f;
        }

        internal static bool IsStoppedByTimeTrap(Health victim) => IsStoppedByTimeTrap(victim, Time.time);

        internal static bool IsStoppedByTimeTrap(Health victim, float now)
        {
            foreach (var trap in ActiveTimeTraps)
            {
                if (now < trap.expires && ContainsEnemy(trap, victim))
                    return true;
            }

            return false;
        }

        internal static float TimeTrapProjectileContact(Health source, Vector3 start, Vector3 direction, float distance, float now)
        {
            float nearest = float.PositiveInfinity;
            foreach (var trap in ActiveTimeTraps)
            {
                if (now >= trap.expires || !trap.owner.HasLiveCaster || trap.owner.caster.Team == source.Team)
                    continue;

                Vector3 offset = trap.center - start;
                float along = Vector3.Dot(offset, direction);
                float lateralSquared = (offset - direction * along).sqrMagnitude;
                float radiusSquared = TrapProjectileRadius * TrapProjectileRadius;
                if (lateralSquared > radiusSquared)
                    continue;

                float halfChord = Mathf.Sqrt(radiusSquared - lateralSquared);
                if (along + halfChord >= 0f && along - halfChord <= distance)
                    nearest = Mathf.Min(nearest, Mathf.Max(0f, along - halfChord));
            }
            return nearest;
        }

        internal static int ModifyRaidVulnerabilityDamage(Health victim, int damage, float now)
        {
            int vulnerability = 0;
            foreach (var trap in ActiveTimeTraps)
            {
                if (ContainsEnemy(trap, victim))
                    vulnerability = Mathf.Max(vulnerability, trap.vulnerability);
            }

            if (HasRaidCurse(victim, now))
                vulnerability += 30;

            return vulnerability == 0 ? damage :
                (int)System.Math.Min(int.MaxValue, (long)damage * (100 + vulnerability) / 100);
        }
    }
}
