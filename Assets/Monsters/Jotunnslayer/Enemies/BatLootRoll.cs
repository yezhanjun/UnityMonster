using System;

namespace Jotunnslayer
{
    internal enum BatLootResource : byte { Gold, Jotunnheart, HealingEssence, HealingEssenceWeak }

    internal struct BatLootDrop
    {
        internal BatLootResource Resource;
        internal int Count;
        internal int Seed;
    }

    // Original bat_loot_sur algorithm after actor/player/location chance modifiers
    // have been resolved. No spawn, death ownership or reward side effects here.
    internal sealed class BatLootRoll
    {
        internal const int MaximumDrops = 15 + 8 + 1;
        readonly JotunnLootRandom random = new JotunnLootRandom(0);

        internal int Roll(int actorSeed, float quantityScale, float goldChance,
            float heartChance, float healingChance, bool weakEssenceModifier,
            float weakEssenceChance, BatLootDrop[] destination)
        {
            // Reject bad context/capacity before consuming any random state or
            // changing output. Caller owns and can reuse the complete buffer.
            if (destination == null || destination.Length < MaximumDrops)
                throw new ArgumentException("A complete bat drop buffer is required.", nameof(destination));
            // int.MaxValue rounds up to 2^31 as a float. Reject that endpoint
            // before cvttss2si / managed float-to-int overflow can occur.
            if (!Finite(quantityScale) || quantityScale < 0f || quantityScale * 5f >= 2147483648f)
                throw new ArgumentOutOfRangeException(nameof(quantityScale));
            if (!Finite(goldChance) || !Finite(heartChance) || !Finite(healingChance) || !Finite(weakEssenceChance))
                throw new ArgumentException("Resolved drop chances must be finite.");

            random.Reset(actorSeed);
            int count = 0;
            // Native DropLoot groups by type, rather than serialized row order.
            RollCurrency(BatLootResource.Gold, 15, goldChance, quantityScale, destination, ref count);
            RollCurrency(BatLootResource.Jotunnheart, 8, heartChance, quantityScale, destination, ref count);
            if (random.Range(0f, 100f) < healingChance)
            {
                int quantity = RollQuantity(quantityScale);
                bool weak = weakEssenceModifier && random.Range(0, 100) < weakEssenceChance;
                destination[count++] = new BatLootDrop
                {
                    Resource = weak ? BatLootResource.HealingEssenceWeak : BatLootResource.HealingEssence,
                    Count = quantity,
                    Seed = random.Range(0, 1000000)
                };
            }
            // Entries beyond returned count belong to the caller, not this roll.
            return count;
        }

        void RollCurrency(BatLootResource resource, int trials, float chance,
            float quantityScale, BatLootDrop[] destination, ref int count)
        {
            for (int i = 0; i < trials; i++)
            {
                if (!(random.Range(0f, 100f) < chance)) continue;
                // Original consumes this quantity draw even though the emitted
                // gold/heart resource Count is always 1. Preserve the stream.
                RollQuantity(quantityScale);
                destination[count++] = new BatLootDrop
                {
                    Resource = resource,
                    Count = 1,
                    Seed = random.Range(0, 1000000)
                };
            }
        }

        int RollQuantity(float scale)
        {
            float value = random.Range(scale * 1.5f, scale * 5f);
            return Math.Max(1, (int)value);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
