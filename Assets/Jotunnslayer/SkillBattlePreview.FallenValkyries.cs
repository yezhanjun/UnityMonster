using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string FallenValkyriesEvolution = "active_valkyrie_C_evolve";
        private readonly Vector3[] fallenValkyrieCenters = new Vector3[3];
        private int fallenValkyriesRank;
        private float fallenValkyriesReady;

        private bool CanChooseFallenValkyries() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 &&
            fallenValkyriesRank < RequiredLevels.Length && raidLevel >= RequiredLevels[fallenValkyriesRank] &&
            (fallenValkyriesRank < 3 || progress.IsLearned(FallenValkyriesEvolution));

        internal bool TryChooseFallenValkyries()
        {
            if (!CanChooseFallenValkyries())
                return false;

            fallenValkyriesRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFallenValkyries(float now)
        {
            if (fallenValkyriesRank == 0 || now < fallenValkyriesReady)
                return;

            bool evolved = fallenValkyriesRank == 4 && progress.IsLearned(FallenValkyriesEvolution);
            int count = SelectNearestImpactCenters(caster.transform.position, 12f, fallenValkyrieCenters, evolved ? 3 : 1);
            if (count == 0)
                return;

            fallenValkyriesReady = now + ScaleSkillCooldown(4f - Mathf.Min(fallenValkyriesRank, 3) * 0.5f);
            for (int i = 0; i < count; i++)
            {
                Vector3 center = fallenValkyrieCenters[i];
                PullTargets(center, 4f, int.MaxValue);
                CollectTargets(center, 4f);
                foreach (var target in targets)
                {
                    int before = target.CurrentHealth;
                    Deal(target, ScaleValkyrieDamage(30));
                    if (evolved && target && target.CurrentHealth > 0 && target.CurrentHealth < before &&
                        Random.value < 0.25f)
                        StopSkillTarget(target, now + 2f);
                }
            }
        }
    }
}
