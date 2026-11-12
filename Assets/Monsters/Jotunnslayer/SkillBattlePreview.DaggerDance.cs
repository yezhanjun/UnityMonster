using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string DaggerEvolution = "active_rogue_A_evolve";
        private int daggerRank;
        private float daggerReady;

        private bool CanChooseDaggerDance() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 && daggerRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[daggerRank] && (daggerRank < 3 || progress.IsLearned(DaggerEvolution));

        internal bool TryChooseDaggerDance()
        {
            if (!CanChooseDaggerDance())
                return false;

            daggerRank++;
            rewardsSpent++;
            return true;
        }

        private void TryDaggerDance(float now)
        {
            if (daggerRank == 0 || now < daggerReady)
                return;

            int rank = Mathf.Min(daggerRank, 3);
            bool evolved = daggerRank == 4 && progress.IsLearned(DaggerEvolution);
            daggerReady = now + ScaleSkillCooldown(evolved ? 5f : 3.5f);
            int daggers = evolved ? 37 : 1 + rank * 4;
            float arc = evolved ? 720f : 80f * rank;
            var pose = caster.transform;
            Vector3 origin = pose.position;
            float start = pose.eulerAngles.y - arc * 0.5f;
            for (int i = 0; i < daggers; i++)
            {
                float yaw = start + arc * i / (daggers - 1);
                Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                SweepDamageLine(origin, origin + direction * 8f, ScaleClassSkillDamage(evolved ? 15 : 13), 0.4f);
            }
        }
    }
}
