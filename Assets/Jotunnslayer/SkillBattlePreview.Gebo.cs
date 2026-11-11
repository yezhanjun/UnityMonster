using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string GeboEvolution = "passive_broksindri_N_evolve";
        private int geboRank, bonusRewards;
        private const string JeraEvolution = "passive_broksindri_O_evolve";
        private int jeraRank;
        private bool JeraEvolved => jeraRank == 4 && progress?.IsLearned(JeraEvolution) == true;

        private bool CanChooseJera() => HasLiveCaster && PendingRewards > 0 && jeraRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[jeraRank] && (jeraRank < 3 || progress.IsLearned(JeraEvolution));

        internal bool TryChooseJera()
        {
            if (!CanChooseJera())
                return false;

            jeraRank++;
            rewardsSpent++;
            return true;
        }

        internal long RaidExperience => experience;
        internal int BonusRewards => bonusRewards;
        private bool GeboEvolved => geboRank == 4 && progress.IsLearned(GeboEvolution);
        private int GeboExperienceBonus => GeboEvolved ? 40 : Mathf.Min(geboRank, 3) * 8;

        private bool CanChooseGebo() => PendingRewards > 0 && geboRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[geboRank] && (geboRank < 3 || progress.IsLearned(GeboEvolution));

        internal bool TryChooseGebo()
        {
            if (!CanChooseGebo())
                return false;

            geboRank++;
            rewardsSpent++;
            return true;
        }
    }
}
