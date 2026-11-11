using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerAssetValidation
    {
        public static void Validate()
        {
            const string root = "Assets/Resources/RainImported/Antlers/";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(root + "meshAntlerShield.asset");
            Require(mesh && mesh.vertexCount == 1434 && mesh.subMeshCount == 1, "mesh import/count");
            Require(mesh.GetIndexCount(0) == 6018, "triangle count");
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                Require(Hash(data[0].GetVertexData<byte>().ToArray()) ==
                    "c6b2324f96e321a2a07344b21c9034d917fe94cdffceeaccd56676ab0b0f5a0e", "original vertex buffer");
                Require(Hash(data[0].GetIndexData<byte>().ToArray()) ==
                    "3a11c451b50791e333fc8d01261fd62760de59cf860ff6f8717a20b67702cf50", "original index buffer");
            }
            foreach (string name in new[] { "texElusiveAntlersDiffuse", "texElusiveAntlersNormal",
                "texElusiveAntlersMask", "texElusiveAntlersRamp" })
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(root + name + ".png");
                Require(texture && texture.width > 0 && texture.height > 0, name);
                Debug.Log($"RAIN_ANTLER_TEXTURE_CHECKED name={name} size={texture.width}x{texture.height}");
            }
            Debug.Log($"RAIN_ANTLER_ORIGINAL_ASSETS_PASSED vertices={mesh.vertexCount} triangles={mesh.GetIndexCount(0) / 3} bounds={mesh.bounds}");
        }

        static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("RAIN_ANTLER_ASSETS_FAILED " + detail);
        }
    }
}
