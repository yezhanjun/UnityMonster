using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchItems
    {
        const int CollectorIndex = 105;
        int collectors, collectorBuffs;
        float collectorBuffUntil;
        internal int CollectorBuffCount => collectorBuffs;
        internal float CollectorBuffRemaining => Mathf.Max(0f, collectorBuffUntil - Time.time);
        internal static int CollectorBuffCap(int stacks) =>
            (int)System.Math.Min(int.MaxValue, 3L + 2L * (System.Math.Max(1, stacks) - 1));
        float CollectorMultiplier => 1f + .03f * collectorBuffs;

        void SyncCollector()
        {
            if (owner is CharacterInstance body) body.SetResearchCollectorStacks(collectors, collectorBuffs);
        }

        void RefreshCollectorStats()
        {
            SyncCollector();
            // RecalculateItemMaximum removes the previous item adjustment before rebuilding it.
            owner.ServerSetMaximumHealth(owner.MaxHealth);
            if (owner is CharacterInstance body) body.RefreshResearchShield();
        }

        void ApplyCollectorPickup(in RangePickupEvent pickup)
        {
            if (collectors <= 0 || owner.CurrentHealth <= 0 || !isActiveAndEnabled) return;
            if (collectorBuffs < CollectorBuffCap(collectors)) collectorBuffs++;
            // All source timed stacks refresh even when the item-dependent cap prevents adding one.
            collectorBuffUntil = Time.time + 20f;
            RefreshCollectorStats();
            // Source tier VFX and sound depend on class and pre-increment count; integration pending.
        }

        void ClearCollectorBuff()
        {
            if (collectorBuffs == 0) return;
            collectorBuffs = 0;
            collectorBuffUntil = 0f;
            RefreshCollectorStats();
        }

        void UpdateCollectorBuff()
        {
            if (collectorBuffs > 0 && (owner.CurrentHealth <= 0 || Time.time >= collectorBuffUntil))
                ClearCollectorBuff();
        }
    }
}
