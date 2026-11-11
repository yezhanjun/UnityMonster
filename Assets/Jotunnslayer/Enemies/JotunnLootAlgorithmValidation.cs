#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    // CPU numeric validation only. Does not start a match or grant resources.
    public static class JotunnLootAlgorithmValidation
    {
        [Serializable] sealed class Drop { public string resource; public int count, seed; }
        [Serializable] sealed class RollCase
        {
            public string name;
            public int seed;
            public float scale, goldChance, heartChance, healingChance, weakChance;
            public bool weakModifier;
            public Drop[] drops;
            public int[] finalState;
            public int nextIndex, nextPartner;
        }
        [Serializable] sealed class Rolls { public RollCase[] cases; }
        [Serializable] sealed class ChanceCase
        {
            public string name, resource;
            public float baseChance, add, addMultiplier, multiply, gameFactor, managerFactor;
            public bool extraGold, firstGoldRow;
            public long bits;
        }
        [Serializable] sealed class Chances { public ChanceCase[] cases; }
        [Serializable] sealed class Result
        {
            public string status, unityVersion, runtime, rollFixtureSha256, chanceFixtureSha256;
            public string randomSourceSha256, rollSourceSha256, chancesSourceSha256, validationSourceSha256;
            public int rollCalls, dropComparisons, finalStateValues, chanceComparisons, invalidContexts;
            public string limitations;
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            throw new ArgumentException("Missing " + name);
        }

        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        static FieldInfo RandomField(string name) => typeof(JotunnLootRandom).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static JotunnLootRandom Random(BatLootRoll roll) => (JotunnLootRandom)typeof(BatLootRoll).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(roll);
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        public static void RunBatch()
        {
            try
            {
                string folder = Argument("-jotunnLootEvidence");
                string rollPath = Path.Combine(folder, "bat-roll-native-fixtures.json");
                string chancePath = Path.Combine(folder, "context-native-fixtures.json");
                var rolls = JsonUtility.FromJson<Rolls>(File.ReadAllText(rollPath));
                var chances = JsonUtility.FromJson<Chances>(File.ReadAllText(chancePath));
                Require(rolls.cases.Length == 72 && chances.cases.Length == 147, "Fixture coverage changed");
                var result = new Result
                {
                    status = "passed", unityVersion = Application.unityVersion,
                    runtime = Type.GetType("Mono.Runtime") != null ? "Unity Editor Mono " + Environment.Version : Environment.Version.ToString(),
                    rollFixtureSha256 = Hash(rollPath), chanceFixtureSha256 = Hash(chancePath),
                    randomSourceSha256 = Hash("Assets/Jotunnslayer/Enemies/JotunnLootRandom.cs"),
                    rollSourceSha256 = Hash("Assets/Jotunnslayer/Enemies/BatLootRoll.cs"),
                    chancesSourceSha256 = Hash("Assets/Jotunnslayer/Enemies/JotunnLootChances.cs"),
                    validationSourceSha256 = Hash("Assets/Jotunnslayer/Enemies/JotunnLootAlgorithmValidation.cs"),
                    limitations = "Unity Editor CPU numeric tests only; not IL2CPP, full original DropLoot, death/spawn/pickup, match or multiplayer validation."
                };
                var roll = new BatLootRoll();
                var buffer = new BatLootDrop[25];
                for (int pass = 0; pass < 2; pass++)
                    for (int c = 0; c < rolls.cases.Length; c++)
                    {
                        var sample = rolls.cases[pass == 0 ? c : rolls.cases.Length - 1 - c];
                        for (int i = 0; i < buffer.Length; i++) buffer[i] = new BatLootDrop { Count = -777, Seed = -888 };
                        int count = roll.Roll(sample.seed, sample.scale, sample.goldChance, sample.heartChance,
                            sample.healingChance, sample.weakModifier, sample.weakChance, buffer);
                        string label = sample.seed + "/" + sample.name;
                        Require(count == sample.drops.Length, "Drop count " + label);
                        for (int i = 0; i < count; i++)
                        {
                            Require(buffer[i].Resource.ToString() == sample.drops[i].resource &&
                                buffer[i].Count == sample.drops[i].count && buffer[i].Seed == sample.drops[i].seed,
                                "Drop payload " + label + "/" + i + " actual=" + buffer[i].Resource + "," + buffer[i].Count + "," + buffer[i].Seed +
                                " expected=" + sample.drops[i].resource + "," + sample.drops[i].count + "," + sample.drops[i].seed);
                            result.dropComparisons++;
                        }
                        for (int i = count; i < buffer.Length; i++)
                            Require(buffer[i].Count == -777 && buffer[i].Seed == -888, "Unused output changed");
                        var random = Random(roll);
                        var state = (int[])RandomField("state").GetValue(random);
                        Require(sample.finalState.Length == 56, "Incomplete state fixture");
                        for (int i = 0; i < 56; i++)
                        {
                            Require(state[i] == sample.finalState[i], "RNG state " + label + "/" + i);
                            result.finalStateValues++;
                        }
                        Require((int)RandomField("next").GetValue(random) == sample.nextIndex &&
                            (int)RandomField("nextPartner").GetValue(random) == sample.nextPartner, "RNG indices " + label);
                        result.rollCalls++;
                    }
                foreach (var sample in chances.cases)
                {
                    var modifier = new LootChanceModifier(sample.add, sample.addMultiplier, sample.multiply);
                    float actual;
                    switch (sample.resource)
                    {
                        case "Gold": actual = JotunnLootChances.Gold(sample.baseChance, modifier, sample.gameFactor, sample.extraGold, sample.firstGoldRow); break;
                        case "Jotunnheart": actual = JotunnLootChances.Heart(sample.baseChance, modifier, sample.managerFactor); break;
                        case "HealingEssence": actual = JotunnLootChances.Healing(sample.baseChance, modifier); break;
                        default: throw new ArgumentException("Unknown fixture resource");
                    }
                    Require(BitConverter.ToUInt32(BitConverter.GetBytes(actual), 0) == (uint)sample.bits,
                        "Chance float bits " + sample.resource + "/" + sample.name + "/" + sample.baseChance);
                    result.chanceComparisons++;
                }
                Require(LootChanceModifier.Identity.Apply(30f) == 30f, "Neutral modifier");
                foreach (float scale in new[] { -1f, float.NaN, float.PositiveInfinity, 429496736f, float.MaxValue })
                {
                    Reject(roll, scale, 30, 50, 100, 0, buffer); result.invalidContexts++;
                }
                Reject(roll, 1, float.NaN, 50, 100, 0, buffer);
                Reject(roll, 1, 30, float.PositiveInfinity, 100, 0, buffer);
                Reject(roll, 1, 30, 50, float.NegativeInfinity, 0, buffer);
                Reject(roll, 1, 30, 50, 100, float.NaN, buffer);
                Reject(roll, 1, 30, 50, 100, 0, null);
                Reject(roll, 1, 30, 50, 100, 0, new BatLootDrop[23]);
                result.invalidContexts += 6;
                Require(result.rollCalls == 144 && result.dropComparisons == 1342 && result.finalStateValues == 8064 &&
                    result.chanceComparisons == 147 && result.invalidContexts == 11, "Incomplete checks");
                File.WriteAllText(Argument("-jotunnLootOutput"), JsonUtility.ToJson(result, true));
                Debug.Log("JOTUNN_LOOT_ALGORITHM_PASSED " + JsonUtility.ToJson(result));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void Reject(BatLootRoll roll, float scale, float gold, float heart, float healing, float weak, BatLootDrop[] output)
        {
            var random = Random(roll);
            var before = (int[])((int[])RandomField("state").GetValue(random)).Clone();
            int next = (int)RandomField("next").GetValue(random), partner = (int)RandomField("nextPartner").GetValue(random);
            if (output != null) for (int i = 0; i < output.Length; i++) output[i].Count = -999;
            bool rejected = false;
            try { roll.Roll(42, scale, gold, heart, healing, true, weak, output); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Invalid context accepted");
            var after = (int[])RandomField("state").GetValue(random);
            for (int i = 0; i < before.Length; i++) Require(before[i] == after[i], "Rejected roll mutated state");
            Require((int)RandomField("next").GetValue(random) == next && (int)RandomField("nextPartner").GetValue(random) == partner,
                "Rejected roll advanced indices");
            if (output != null) for (int i = 0; i < output.Length; i++) Require(output[i].Count == -999, "Rejected roll mutated output");
        }
    }
}
#endif
