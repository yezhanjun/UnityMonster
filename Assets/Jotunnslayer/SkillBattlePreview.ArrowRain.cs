using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string RainEvolution = "active_ranger_C_evolve";
        private int rainRank, rainWaves;
        private float rainReady, nextRainWave;
        private Vector3 rainCenter;
        private int curseStunPercent;
        private static readonly List<RaidCurse> RaidCurses = new List<RaidCurse>();

        private struct RaidCurse
        {
            public SkillBattlePreview source;
            public Health target;
            public float expires;
        }

        private void TryApplyRaidCurse(Health target, float now, float chance, float duration = 4f)
        {
            if (!target || target.CurrentHealth <= 0 || chance <= 0f || (chance < 1f && Random.value >= chance))
                return;

            var curse = new RaidCurse { source = this, target = target, expires = now + duration };
            int index = 0;
            while (index < RaidCurses.Count && (RaidCurses[index].source != this || RaidCurses[index].target != target))
                index++;
            if (index == RaidCurses.Count)
                RaidCurses.Add(curse);
            else
                RaidCurses[index] = curse;

            if (curseStunPercent > 0 && Random.value < curseStunPercent / 100f)
                StopSkillTarget(target, now + 2f);
        }

        internal static bool HasRaidCurse(Health target, float now)
        {
            foreach (var curse in RaidCurses)
                if (curse.target == target && target && target.CurrentHealth > 0 && now < curse.expires && curse.source.HasLiveCaster)
                    return true;

            return false;
        }

        private void ClearRaidCurses(float now = float.PositiveInfinity)
        {
            for (int i = RaidCurses.Count - 1; i >= 0; i--)
            {
                var curse = RaidCurses[i];
                if (curse.source == this && (now >= curse.expires || !curse.target || curse.target.CurrentHealth <= 0))
                    RaidCurses.RemoveAt(i);
            }
        }

        private bool CanChooseArrowRain() => AllowsRaidClass(RaidClass.Ranger) && PendingRewards > 0 && rainRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[rainRank] && (rainRank < 3 || progress.IsLearned(RainEvolution));

        internal bool TryChooseArrowRain()
        {
            if (!CanChooseArrowRain())
                return false;

            rainRank++;
            rewardsSpent++;
            return true;
        }

        private void TryArrowRain(float now)
        {
            if (rainRank == 0 || rainWaves > 0 || now < rainReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            Vector3 center = targets[Random.Range(0, targets.Count)].transform.position;
            if (rainRank == 4 && progress.IsLearned(RainEvolution))
            {
                rainReady = now + ScaleSkillCooldown(0.7f);
                CollectTargets(center, 3f);
                foreach (var target in targets)
                {
                    int before = target.CurrentHealth;
                    DealRangerSkillDamage(target, 24);
                    if (target && target.CurrentHealth < before)
                        TryApplyRaidCurse(target, now, 0.2f);
                }
                return;
            }

            int rank = Mathf.Min(rainRank, 3);
            rainReady = now + ScaleSkillCooldown(5.5f - rank * 0.5f);
            rainCenter = center;
            nextRainWave = now;
            rainWaves = 2 + rank;
        }

        private void TickArrowRain(float now)
        {
            if (rainWaves == 0 || now < nextRainWave)
                return;

            rainWaves--;
            nextRainWave = now + 0.6f;
            StrikeRangerSkillDamage(rainCenter, 3f, 14);
        }
    }
}
