using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string BubbleUnlock = "active_njord_E";
        public const string BubbleEvolution = "active_njord_E_evolve";
        private static readonly HashSet<Health> FortifiedCasters = new HashSet<Health>();
        private Health bubbleActor;
        private int bubbleRank, bubbleFish, bubbleCastCount;
        private float bubbleReady, bubbleExpiry;

        private bool CanChooseTidalBarrier() => PendingRewards > 0 && bubbleRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[bubbleRank] && progress.IsLearned(BubbleUnlock) &&
            (bubbleRank < 3 || progress.IsLearned(BubbleEvolution));

        internal bool TryChooseTidalBarrier()
        {
            if (!CanChooseTidalBarrier())
                return false;

            bubbleRank++;
            rewardsSpent++;
            return true;
        }

        private void TryTidalBarrier(float now)
        {
            if (bubbleRank == 0 || bubbleActor || now < bubbleReady || !progress.IsLearned(BubbleUnlock))
                return;

            bool evolved = bubbleRank == 4 && progress.IsLearned(BubbleEvolution);
            bubbleReady = now + ScaleSkillCooldown(10f);
            bubbleExpiry = now + (evolved ? 4f : 3f);
            bubbleActor = caster;
            FortifiedCasters.Add(bubbleActor);
            bubbleFish = evolved ? 0 : 1 + 3 * Mathf.Min(bubbleRank, 3);
            bubbleCastCount = RollAnsuzRepeat(ref bubbleReady) ? 2 : 1;
            if (evolved)
                for (int i = 0; i < bubbleCastCount; i++)
                    ReleaseBarrierFish(20);
        }

        private void TickTidalBarrier(float now)
        {
            if (!bubbleActor || now < bubbleExpiry)
                return;

            int fish = bubbleFish;
            int count = bubbleCastCount;
            ClearTidalBarrier();
            for (int i = 0; i < count; i++)
                ReleaseBarrierFish(fish);
        }

        private void ReleaseBarrierFish(int fish)
        {
            if (fish == 0)
                return;

            CollectTargets(caster.transform.position, 6f);
            int cursor = 0;
            for (int i = 0; i < fish && targets.Count > 0; i++)
            {
                var target = targets[cursor];
                DealGodDamage(target, 10);
                if (!target || target.CurrentHealth <= 0)
                    targets.RemoveAt(cursor);
                else
                    cursor++;

                if (cursor >= targets.Count)
                    cursor = 0;
            }
        }

        private void ClearTidalBarrier()
        {
            if (!ReferenceEquals(bubbleActor, null))
                FortifiedCasters.Remove(bubbleActor);
            bubbleActor = null;
            bubbleFish = 0;
            bubbleCastCount = 0;
        }

        internal static int ModifyFortifiedDamage(Health victim, int damage)
        {
            int permanentReduction = RaidStatOwners.TryGetValue(victim, out var owner) ? owner.damageReductionPercent : 0;
            int barrierPercent = FortifiedCasters.Contains(victim) ? 70 : 100;
            int ghostwalkPercent = HasGhostwalk(victim, Time.time) ? 50 : 100;
            return (int)((long)damage * (100 - permanentReduction) * barrierPercent * ghostwalkPercent / 1000000);
        }
    }
}
