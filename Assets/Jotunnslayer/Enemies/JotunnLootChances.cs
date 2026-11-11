namespace Jotunnslayer
{
    // Snapshot of the original RPGActorValue fields. Default(struct) is not the
    // neutral modifier; use Identity when the participant has no drop modifier.
    internal readonly struct LootChanceModifier
    {
        internal readonly float Add;
        internal readonly float AddMultiplier;
        internal readonly float Multiply;

        internal LootChanceModifier(float add, float addMultiplier, float multiply)
        {
            Add = add;
            AddMultiplier = addMultiplier;
            Multiply = multiply;
        }

        internal static LootChanceModifier Identity => new LootChanceModifier(0f, 0f, 1f);

        internal float Apply(float value)
        {
            // Match native RPGActorValue.Apply(value, 0) operation ordering.
            float added = JotunnLootFloat.Round(value + Add);
            float multiplied = JotunnLootFloat.Round(added * Multiply);
            float additiveMultiplier = JotunnLootFloat.Round(AddMultiplier + 1f);
            additiveMultiplier = JotunnLootFloat.Round(additiveMultiplier + 0f);
            return JotunnLootFloat.Round(multiplied * additiveMultiplier);
        }
    }

    internal static class JotunnLootChances
    {
        // Native branches bypass all modifiers at base chance >= 100. Do not
        // clamp the resolved value: the subsequent roll uses strict roll < chance.
        internal static float Gold(float baseChance, LootChanceModifier player,
            float gameGoldMultiplier, bool extraGold, bool firstGoldRow)
        {
            if (baseChance >= 100f) return baseChance;
            float chance = JotunnLootFloat.Round(player.Apply(baseChance) * gameGoldMultiplier);
            chance = JotunnLootFloat.Round(chance * 1f);
            if (extraGold)
            {
                if (firstGoldRow) return 100f;
                if (chance < 70f) return 70f;
            }
            return chance;
        }

        internal static float Heart(float baseChance, LootChanceModifier player,
            float managerHeartMultiplier)
        {
            if (baseChance >= 100f) return baseChance;
            float chance = JotunnLootFloat.Round(player.Apply(baseChance) * 1f);
            return JotunnLootFloat.Round(chance * managerHeartMultiplier);
        }

        internal static float Healing(float baseChance, LootChanceModifier player)
        {
            if (baseChance >= 100f) return baseChance;
            return JotunnLootFloat.Round(player.Apply(baseChance) * 1f);
        }
    }
}
