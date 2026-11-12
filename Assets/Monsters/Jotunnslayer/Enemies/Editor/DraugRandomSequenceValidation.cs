using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class DraugRandomSequenceValidation
    {
        [Serializable] sealed class SeedCase { public int seed; }
        [Serializable] sealed class NativeEvidence
        {
            public string status;
            public string corpusSHA256;
            public int draws;
            public SeedCase[] examples;
        }
        [Serializable] sealed class Verification
        {
            public string status, unityVersion, corpusSHA256, nativeCorpusSHA256;
            public int seeds, draws;
            public string limits;
        }

        public static void Run()
        {
            try
            {
                var source = JsonUtility.FromJson<NativeEvidence>(File.ReadAllText(
                    "Docs/Validation/DraugRandomSequenceVerification.json"));
                if (source.status != "PASS_NATIVE_SEEDED_RANDOM_SEQUENCE" || source.examples.Length != 23)
                    throw new InvalidOperationException("Unexpected native sequence evidence");
                int[] bounds = { 1000000, 3, 1, 0, int.MaxValue };
                int draws = 0;
                string digest;
                using (var bytes = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                    {
                        foreach (var item in source.examples)
                        {
                            var random = new System.Random(item.seed);
                            for (int i = 0; i < 1024; i++)
                            {
                                int maximum = bounds[i % bounds.Length];
                                writer.Write(item.seed);
                                writer.Write(maximum);
                                writer.Write(random.Next(maximum));
                                draws++;
                            }
                            for (int i = 0; i < 128; i++)
                            {
                                writer.Write(item.seed);
                                writer.Write(random.Next(1000000) / 999999f);
                                draws++;
                            }
                        }
                    }
                    using (var sha = SHA256.Create())
                        digest = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
                }
                bool passed = digest == source.corpusSHA256 && draws == source.draws;
                var result = new Verification
                {
                    status = passed ? "PASS_UNITY_NATIVE_RANDOM_SEQUENCE" : "FAIL_UNITY_NATIVE_RANDOM_SEQUENCE",
                    unityVersion = Application.unityVersion,
                    corpusSHA256 = digest,
                    nativeCorpusSHA256 = source.corpusSHA256,
                    seeds = source.examples.Length,
                    draws = draws,
                    limits = "Editor System.Random only; no Player build. Seed ownership/overrides, consumption order and combat integration remain pending."
                };
                File.WriteAllText("Docs/Validation/DraugUnityRandomSequenceVerification.json", JsonUtility.ToJson(result, true));
                if (!passed) throw new InvalidOperationException("Unity/native random corpus mismatch");
                Debug.Log($"DRAUG_UNITY_RANDOM_SEQUENCE_PASS seeds={result.seeds} draws={draws} sha256={digest}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
