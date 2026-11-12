using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string IronFistEvolution = "active_warden_A_evolve";
        private static readonly float[] IronFistStunChance = { 0f, 0.08f, 0.15f, 0.20f };
        private int ironFistRank, ironFistCastRank, ironFistStrikes;
        private float ironFistReady, ironFistNextStrike, ironFistInterval;

        private bool CanChooseIronFist() => AllowsRaidClass(RaidClass.Warden) && PendingRewards > 0 && ironFistRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[ironFistRank] &&
            (ironFistRank < 3 || progress.IsLearned(IronFistEvolution));

        internal bool TryChooseIronFist()
        {
            if (!CanChooseIronFist())
                return false;

            ironFistRank++;
            rewardsSpent++;
            return true;
        }

        private void TryIronFist(float now)
        {
            if (ironFistRank == 0 || now < ironFistReady || ironFistStrikes > 0)
                return;

            ironFistCastRank = ironFistRank == 4 && !progress.IsLearned(IronFistEvolution) ? 3 : ironFistRank;
            ironFistReady = now + ScaleSkillCooldown(5f);
            ironFistStrikes = ironFistCastRank + 1;
            ironFistInterval = 2f / ironFistStrikes;
            ironFistNextStrike = now + ironFistInterval;
        }

        private void TickIronFist(float now)
        {
            while (ironFistStrikes > 0 && now >= ironFistNextStrike)
            {
                ironFistStrikes--;
                ironFistNextStrike += ironFistInterval;
                bool evolved = ironFistCastRank == 4;
                float stunChance = IronFistStunChance[ironFistCastRank - 1];
                CollectTargets(caster.transform.position, evolved ? 3f : 2f);
                foreach (var target in targets)
                {
                    if (!target || target.CurrentHealth <= 0)
                        continue;

                    int before = target.CurrentHealth;
                    Deal(target, ScaleClassSkillDamage(evolved ? 30 : 22));
                    if (target && target.CurrentHealth > 0 && target.CurrentHealth < before &&
                        stunChance > 0f && Random.value < stunChance &&
                        target.TryGetComponent<RangeResearchEnemy>(out var enemy))
                        enemy.ApplyStun();
                }
            }
        }
    }
}
