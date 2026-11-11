using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SwordEvolution = "active_freya_A_evolve";
        private int swordRank, swordSwings;
        private float swordReady, nextSwordSwing;

        private bool CanChooseMagicSword() => PendingRewards > 0 && swordRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[swordRank] && (swordRank < 3 || progress.IsLearned(SwordEvolution));

        internal bool TryChooseMagicSword()
        {
            if (!CanChooseMagicSword())
                return false;

            swordRank++;
            rewardsSpent++;
            return true;
        }

        private void TryMagicSword(float now)
        {
            if (swordRank == 0 || swordSwings > 0 || now < swordReady)
                return;

            if (swordRank == 4 && progress.IsLearned(SwordEvolution))
            {
                swordReady = now + ScaleSkillCooldown(3f);
                var pose = caster.transform;
                SweepGodDamage(pose.position, pose.position + pose.forward * 8f, 45);
                if (RollAnsuzRepeat(ref swordReady))
                    SweepGodDamage(pose.position, pose.position + pose.forward * 8f, 45);
                return;
            }

            int rank = Mathf.Min(swordRank, 3);
            swordReady = now + ScaleSkillCooldown(2f + rank);
            swordSwings = 1 + rank;
            swordCastCount = RollAnsuzRepeat(ref swordReady) ? 2 : 1;
            nextSwordSwing = now;
        }

        private void TickMagicSword(float now)
        {
            if (swordSwings == 0 || now < nextSwordSwing)
                return;

            swordSwings--;
            nextSwordSwing = now + 0.2f;
            for (int i = 0; i < swordCastCount; i++)
                StrikeMagicSword();
        }

        private int swordCastCount;

        private void StrikeMagicSword()
        {
            var pose = caster.transform;
            Vector3 origin = pose.position;
            IndicateSector(origin, pose.forward, 3f, 60f);
            CollectTargets(origin, 3f);
            foreach (var target in targets)
            {
                if (Vector3.Angle(pose.forward, target.transform.position - origin) <= 60f)
                    DealGodDamage(target, 20);
            }
        }
    }
}
