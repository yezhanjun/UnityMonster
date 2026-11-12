using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string PotEvolution = "active_rogue_B_evolve";
        private int potRank, potStage, potDamage;
        private float potReady, potDetonation;
        private Vector3 potCenter;
        private bool potEvolved;

        private bool CanChooseShrapnelPot() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 && potRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[potRank] && (potRank < 3 || progress.IsLearned(PotEvolution));

        internal bool TryChooseShrapnelPot()
        {
            if (!CanChooseShrapnelPot())
                return false;

            potRank++;
            rewardsSpent++;
            return true;
        }

        private void TryShrapnelPot(float now)
        {
            if (potRank == 0 || potStage != 0 || now < potReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            int rank = Mathf.Min(potRank, 3);
            potEvolved = potRank == 4 && progress.IsLearned(PotEvolution);
            potReady = now + ScaleSkillCooldown(potEvolved ? 2.5f : 4.5f - 0.5f * rank);
            potDamage = potEvolved ? 35 : 18 + 4 * rank;
            potCenter = targets[0].transform.position;
            potDetonation = now + 0.6f;
            potStage = 1;
        }

        private void TickShrapnelPot(float now)
        {
            while (potStage != 0 && now >= potDetonation)
            {
                bool primary = potStage == 1;
                CollectTargets(potCenter, 3f);
                foreach (var target in targets)
                {
                    Deal(target, ScaleClassSkillDamage(primary ? potDamage : 20));
                    if (primary && target && target.CurrentHealth > 0 && Random.value < 0.5f)
                        ApplyPreviewPoison(target, potDetonation);
                }

                potStage = primary && potEvolved ? 2 : 0;
                potDetonation += 0.3f;
            }
        }
    }
}
