using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string KrakenEvolution = "active_njord_B_evolve";
        private int krakenRank, krakenStage, krakenTentacles;
        private float krakenReady, krakenNext;
        private bool krakenEvolved;
        private Vector3 krakenOrigin;

        private bool CanChooseKraken() => PendingRewards > 0 && krakenRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[krakenRank] && (krakenRank < 3 || progress.IsLearned(KrakenEvolution));

        internal bool TryChooseKraken()
        {
            if (!CanChooseKraken())
                return false;

            krakenRank++;
            rewardsSpent++;
            return true;
        }

        private void TryKraken(float now)
        {
            if (krakenRank == 0 || krakenStage != 0 || now < krakenReady)
                return;

            krakenOrigin = caster.transform.position;
            CollectTargets(krakenOrigin, 5f);
            if (targets.Count == 0)
                return;

            krakenEvolved = krakenRank == 4 && progress.IsLearned(KrakenEvolution);
            krakenTentacles = krakenEvolved ? 5 : 1 + Mathf.Min(krakenRank, 3);
            krakenReady = now + ScaleSkillCooldown(8f);
            krakenNext = now + 0.25f;
            krakenStage = 1;
            krakenCastCount = RollAnsuzRepeat(ref krakenReady) ? 2 : 1;
        }

        private void TickKraken(float now)
        {
            if (krakenStage == 0 || now < krakenNext)
                return;

            bool barrels = krakenStage == 2;
            for (int i = 0; i < krakenCastCount; i++)
                StrikeKraken(barrels);
            krakenStage = !barrels && krakenEvolved ? 2 : 0;
            krakenNext = now + 0.35f;
        }

        private int krakenCastCount;

        private void StrikeKraken(bool barrels)
        {
            CollectTargets(krakenOrigin, barrels ? 8f : 5f);
            int strikes = barrels ? 5 : krakenTentacles;
            int damage = barrels ? 18 : krakenEvolved ? 10 : 8;
            int cursor = 0;
            for (int i = 0; i < strikes && targets.Count > 0; i++)
            {
                var target = targets[cursor];
                IndicatePath(krakenOrigin, target.transform.position, .2f);
                DealGodDamage(target, damage);
                if (!target || target.CurrentHealth <= 0)
                    targets.RemoveAt(cursor);
                else
                    cursor++;

                if (cursor >= targets.Count)
                    cursor = 0;
            }

        }
    }
}
