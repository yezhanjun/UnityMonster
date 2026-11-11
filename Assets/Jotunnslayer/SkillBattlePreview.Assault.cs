using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string AssaultEvolution = "active_berserker_B_evolve";
        private int assaultRank;
        private float assaultReady;

        private bool CanChooseAssault() => AllowsRaidClass(RaidClass.Berserker) && PendingRewards > 0 && assaultRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[assaultRank] && (assaultRank < 3 || progress.IsLearned(AssaultEvolution));

        internal bool TryChooseAssault()
        {
            if (!CanChooseAssault())
                return false;

            assaultRank++;
            rewardsSpent++;
            return true;
        }

        private void TryAssault(float now)
        {
            if (assaultRank == 0 || now < assaultReady)
                return;

            assaultReady = now + ScaleSkillCooldown(3f);
            int rank = Mathf.Min(assaultRank, 3);
            bool evolved = assaultRank == 4 && progress.IsLearned(AssaultEvolution);
            float range = evolved ? 5f : 4f + rank;
            int strikes = evolved ? 4 : 1;
            int damage = evolved ? 30 : 16 + rank * 4;
            var pose = caster.transform;
            Vector3 origin = pose.position;
            Vector3 end = origin + pose.forward * range;
            for (int strike = 0; strike < strikes; strike++)
                SweepDamageLine(origin, end, ScaleClassSkillDamage(damage));
        }
    }
}
