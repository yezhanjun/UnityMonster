using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SerpentEvolution = "active_loki_A_evolve";
        private int serpentRank;
        private float serpentReady;

        private bool CanChooseSerpent() => PendingRewards > 0 && serpentRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[serpentRank] && (serpentRank < 3 || progress.IsLearned(SerpentEvolution));

        internal bool TryChooseSerpent()
        {
            if (!CanChooseSerpent())
                return false;

            serpentRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySerpent(float now)
        {
            if (serpentRank == 0 || now < serpentReady)
                return;

            serpentReady = now + ScaleSkillCooldown(3f);
            bool evolved = serpentRank == 4 && progress.IsLearned(SerpentEvolution);
            int damage = evolved ? 35 : 14 + Mathf.Min(serpentRank, 3) * 6;
            StrikeSerpent(evolved, damage);
            if (RollAnsuzRepeat(ref serpentReady))
                StrikeSerpent(evolved, damage);
        }

        private void StrikeSerpent(bool evolved, int damage)
        {
            var pose = caster.transform;
            Vector3 origin = pose.position;
            Vector3 facing = Quaternion.Euler(0, pose.eulerAngles.y, 0) * Vector3.forward;
            if (evolved) IndicateRing(origin, 7f);
            else IndicateSector(origin, facing, 7f, 30f);
            CollectTargets(origin, 7f);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - origin;
                offset.y = 0;
                if (evolved || Vector3.Angle(facing, offset) <= 30f)
                    DealGodDamage(target, damage);
            }
        }
    }
}
