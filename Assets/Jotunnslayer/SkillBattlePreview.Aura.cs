using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string AuraEvolution = "active_freya_C_evolve";
        private int auraRank;
        private float auraReady;

        private bool CanChooseAura() => PendingRewards > 0 && auraRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[auraRank] && (auraRank < 3 || progress.IsLearned(AuraEvolution));

        internal bool TryChooseAura()
        {
            if (!CanChooseAura())
                return false;

            auraRank++;
            rewardsSpent++;
            return true;
        }

        private void TryAura(float now)
        {
            if (auraRank == 0 || now < auraReady)
                return;

            int rank = Mathf.Min(auraRank, 3);
            bool evolved = auraRank == 4 && progress.IsLearned(AuraEvolution);
            auraReady = now + ScaleSkillCooldown(evolved ? 0.5f : 5f - rank * 0.5f);
            int radius = evolved ? 6 : 4 + rank * 2;
            StrikeGodDamage(caster.transform.position, radius, evolved ? 7 : radius);
            if (RollAnsuzRepeat(ref auraReady))
                StrikeGodDamage(caster.transform.position, radius, evolved ? 7 : radius);
        }
    }
}
