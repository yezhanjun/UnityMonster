using System;

namespace Jotunnslayer
{
    // Source normal-seed path. The source game's debug-global-seed override is not modeled.
    // Keep this separate from Unity.Random, which owns the source arrow animation index.
    internal sealed class DraugRandomSource
    {
        System.Random random;

        internal int Seed { get; set; }
        internal bool IsInitialized => random != null;

        internal DraugRandomSource() : this(SeedFromGuid(Guid.NewGuid())) { }
        internal DraugRandomSource(int seed) { Seed = seed; }

        internal void Initialize() { random = new System.Random(Seed); }

        internal float Value
        {
            get
            {
                if (random == null) Initialize();
                return random.Next(1000000) / 999999f;
            }
        }

        internal static int SeedFromGuid(Guid guid)
        {
            // Explicit words avoid relying on framework-specific Guid.GetHashCode behavior.
            byte[] bytes = guid.ToByteArray();
            return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4) ^
                   BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);
        }
    }
}
