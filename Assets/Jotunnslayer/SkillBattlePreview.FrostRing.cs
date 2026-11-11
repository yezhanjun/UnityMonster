using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string RingEvolution = "active_skadi_B_evolve";
        private int ringRank, ringWaves;
        private float ringReady, nextRingWave;
        private Vector3 ringOrigin;

        private bool CanChooseFrostRing() => PendingRewards > 0 && ringRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[ringRank] && (ringRank < 3 || progress.IsLearned(RingEvolution));

        internal bool TryChooseFrostRing()
        {
            if (!CanChooseFrostRing())
                return false;

            ringRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFrostRing(float now)
        {
            if (ringRank == 0 || ringWaves > 0 || now < ringReady)
                return;

            ringOrigin = caster.transform.position;
            ringCastCount = RollAnsuzRepeat(ref ringReady) ? 2 : 1;
            if (ringRank == 4 && progress.IsLearned(RingEvolution))
            {
                ringReady = now + ScaleSkillCooldown(4f);
                nextRingWave = now;
                ringWaves = 3;
                return;
            }

            ringReady = now + ScaleSkillCooldown(4.5f);
            int rank = Mathf.Min(ringRank, 3);
            DamageFrostRing(now, 7f, 4 + rank * 2, 0.05f + rank * 0.1f);
        }

        private void TickFrostRing(float now)
        {
            if (ringWaves == 0 || now < nextRingWave)
                return;

            float radius = 5f + (3 - ringWaves) * 2f;
            ringWaves--;
            nextRingWave = now + 0.25f;
            DamageFrostRing(now, radius, 12, 0.4f);
        }

        private int ringCastCount;

        private void DamageFrostRing(float now, float radius, int damage, float freezeChance)
        {
            for (int i = 0; i < ringCastCount; i++)
                StrikeFrostRing(now, radius, damage, freezeChance);
        }

        private void StrikeFrostRing(float now, float radius, int damage, float freezeChance)
        {
            IndicateRing(ringOrigin, radius);
            CollectTargets(ringOrigin, radius);
            foreach (var target in targets)
            {
                DealGodDamage(target, damage);
                if (target && target.CurrentHealth > 0 && Random.value < freezeChance)
                    StopSkillTarget(target, now + 1.8f, frozen: true);
            }
        }
    }
}

