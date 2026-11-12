using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string CuttingEvolution = "active_berserker_C_evolve";
        private int cuttingRank, cuttingStep;
        private float cuttingReady;

        private bool CanChooseCuttingEdge() => AllowsRaidClass(RaidClass.Berserker) && PendingRewards > 0 && cuttingRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[cuttingRank] && (cuttingRank < 3 || progress.IsLearned(CuttingEvolution));

        internal bool TryChooseCuttingEdge()
        {
            if (!CanChooseCuttingEdge())
                return false;

            cuttingRank++;
            rewardsSpent++;
            return true;
        }

        private void TryCuttingEdge(float now)
        {
            if (cuttingRank == 0 || now < cuttingReady)
                return;

            Vector3 origin = caster.transform.position;
            if (cuttingRank == 4 && progress.IsLearned(CuttingEvolution))
            {
                cuttingReady = now + ScaleSkillCooldown(0.5f);
                // Sample the moving blade at successive points around the caster.
                Vector3 offset = Quaternion.Euler(0f, cuttingStep * 45f, 0f) * Vector3.forward * 2f;
                cuttingStep = (cuttingStep + 1) % 8;
                Strike(origin + offset, 0.75f, ScaleClassSkillDamage(14));
                return;
            }

            int rank = Mathf.Min(cuttingRank, 3);
            cuttingReady = now + ScaleSkillCooldown(5.5f - rank);
            Strike(origin, 2f, ScaleClassSkillDamage(6 + rank * 6));
        }
    }
}
