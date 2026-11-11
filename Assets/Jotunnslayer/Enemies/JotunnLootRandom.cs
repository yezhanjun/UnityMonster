using System;
using System.Runtime.CompilerServices;

namespace Jotunnslayer
{
    internal static class JotunnLootFloat
    {
        // Mono can retain wider intermediates for managed float expressions.
        // A non-inlined float ABI boundary materializes each original SSE result.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float Round(float value) => value;
    }

    // Actor-owned drop stream. Initialize once per DropLoot call with the actor's
    // persisted seed. Do not share with combat or player pickup random streams.
    // Explicit original subtractive algorithm avoids runtime Random version drift.
    internal sealed class JotunnLootRandom
    {
        const int Big = int.MaxValue;
        readonly int[] state = new int[56];
        int next;
        int nextPartner;

        internal JotunnLootRandom(int seed) { Reset(seed); }

        internal void Reset(int seed)
        {
            Array.Clear(state, 0, state.Length);
            unchecked
            {
                int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
                int previous = 161803398 - subtraction;
                state[55] = previous;
                int value = 1;
                for (int i = 1; i < 55; i++)
                {
                    int index = 21 * i % 55;
                    state[index] = value;
                    value = previous - value;
                    if (value < 0) value += Big;
                    previous = state[index];
                }
                for (int pass = 0; pass < 4; pass++)
                    for (int i = 1; i < 56; i++)
                    {
                        state[i] -= state[1 + (i + 30) % 55];
                        if (state[i] < 0) state[i] += Big;
                    }
            }
            next = 0;
            nextPartner = 21;
        }

        internal int Next()
        {
            int index = next + 1;
            if (index >= 56) index = 1;
            int partner = nextPartner + 1;
            if (partner >= 56) partner = 1;
            int value = unchecked(state[index] - state[partner]);
            if (value == Big) value--;
            if (value < 0) value += Big;
            state[index] = value;
            next = index;
            nextPartner = partner;
            return value;
        }

        double Sample() => Next() * (1.0 / Big);

        internal int Range(int minimum, int maximum)
        {
            if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
            long width = (long)maximum - minimum;
            if (width <= Big) return (int)(Sample() * width) + minimum;
            int value = Next();
            if (Next() % 2 == 0) value = -value;
            double fraction = ((double)value + Big - 1) / (2.0 * Big - 1);
            return (int)((long)(fraction * width) + minimum);
        }

        internal float Range(float minimum, float maximum)
        {
            // Original Range can include the upper endpoint. Chance checks must
            // use roll < chance; even chance=100 has one rejected lattice point.
            float fraction = JotunnLootFloat.Round(Range(0, 1000000) / 999999f);
            float width = JotunnLootFloat.Round(maximum - minimum);
            float product = JotunnLootFloat.Round(fraction * width);
            return JotunnLootFloat.Round(product + minimum);
        }
    }
}
