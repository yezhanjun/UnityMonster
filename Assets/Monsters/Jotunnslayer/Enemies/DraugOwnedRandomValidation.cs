#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class DraugOwnedRandomValidation
    {
        [Serializable] sealed class Corpus { public string status, corpusSHA256; public int[] seeds; public int draws; }
        [Serializable] sealed class SeedExample { public string entropy; public int seedSigned; }
        [Serializable] sealed class SeedEvidence { public SeedExample[] examples; }
        [Serializable] sealed class Result
        {
            public string status, unityVersion, corpusSHA256, limits;
            public int seeds, draws, guidCases;
            public bool lazyInitialization, warmSeedPreserved, explicitReset, instancesIndependent, unityRandomUnchanged;
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static int FloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        public static void Run()
        {
            try
            {
                var input = JsonUtility.FromJson<Corpus>(File.ReadAllText("Docs/Validation/DraugOwnedRandomNativeVerification.json"));
                Check(input.status == "PASS_NATIVE_OWNED_RANDOM_CORPUS", "Native corpus unavailable");
                string globalBefore = JsonUtility.ToJson(UnityEngine.Random.state);
                string digest;
                int draws = 0;
                using (var bytes = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                    {
                        foreach (int seed in input.seeds)
                        {
                            var source = new DraugRandomSource(seed);
                            Check(!source.IsInitialized, "Constructor initialized early");
                            source.Seed = seed;
                            Check(!source.IsInitialized, "Cold seed assignment initialized early");
                            for (int phase = 0; phase < 3; phase++)
                            {
                                if (phase == 1) source.Seed = ~seed;
                                if (phase == 2) source.Initialize();
                                for (int i = 0; i < 128; i++)
                                {
                                    writer.Write(seed); writer.Write(phase); writer.Write(source.Value); draws++;
                                    Check(source.IsInitialized, "First value did not initialize");
                                }
                            }
                        }
                    }
                    using (var sha = SHA256.Create())
                        digest = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
                }
                Check(draws == input.draws && digest == input.corpusSHA256, "Native output mismatch");
                var left = new DraugRandomSource(13); var right = new DraugRandomSource(29);
                var expectedLeft = new System.Random(13); var expectedRight = new System.Random(29);
                for (int i = 0; i < 256; i++)
                {
                    Check(FloatBits(left.Value) == FloatBits(expectedLeft.Next(1000000) / 999999f), "Left float32 stream contaminated at " + i);
                    if ((i & 1) == 0) Check(FloatBits(right.Value) == FloatBits(expectedRight.Next(1000000) / 999999f), "Right float32 stream contaminated at " + i);
                }
                var seedProof = JsonUtility.FromJson<SeedEvidence>(File.ReadAllText("Docs/Validation/DraugSeedConstructionVerification.json"));
                foreach (var example in seedProof.examples)
                {
                    var bytes = new byte[16];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(example.entropy.Substring(i * 2, 2), 16);
                    bytes[7] = (byte)((bytes[7] & 15) | 64); bytes[8] = (byte)((bytes[8] & 63) | 128);
                    Check(DraugRandomSource.SeedFromGuid(new Guid(bytes)) == example.seedSigned, "GUID seed mismatch");
                }
                Check(!new DraugRandomSource().IsInitialized, "Default constructor initialized early");
                Check(globalBefore == JsonUtility.ToJson(UnityEngine.Random.state), "Changed Unity arrow RNG stream");
                var result = new Result
                {
                    status = "PASS_UNITY_OWNED_RANDOM", unityVersion = Application.unityVersion, corpusSHA256 = digest,
                    seeds = input.seeds.Length, draws = draws, guidCases = seedProof.examples.Length,
                    lazyInitialization = true, warmSeedPreserved = true, explicitReset = true, instancesIndependent = true, unityRandomUnchanged = true,
                    limits = "Component not yet used by Combat. Source debug-global seed override, full startup consumers/network authority and Player build remain pending."
                };
                File.WriteAllText("Docs/Validation/DraugOwnedRandomUnityVerification.json", JsonUtility.ToJson(result, true));
                Debug.Log($"DRAUG_OWNED_RANDOM_UNITY_PASS seeds={result.seeds} draws={draws} independent/lazy/warmSeed/explicitReset/guid/globalUnityUnchanged sha256={digest}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
#endif
