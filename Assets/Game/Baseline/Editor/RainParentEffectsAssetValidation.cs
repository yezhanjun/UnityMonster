using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    // Import/buffer verification only. Particle playback and original shader fidelity are separate gates.
    public static class RainParentEffectsAssetValidation
    {
        const string Folder = "Assets/Resources/RainActors/RoR2/Base/Characters/Parent/Effects/";
        const string Evidence = "Docs/玩法参考设计/雨中冒险2/原版依据/";

        [Serializable] sealed class TextureReport { public TextureRecord[] textures; }
        [Serializable] sealed class TextureRecord
        {
            public string name;
            public int[] size;
            public bool normalDXT5nmReconstructed;
        }
        [Serializable] sealed class MeshReport { public MeshRecord[] meshes; }
        [Serializable] sealed class MeshRecord
        {
            public string name;
            public int vertexCount, vertexBytes, indexBytes;
            public string vertexSHA256, indexSHA256;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the isolated batch project for asset validation.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var textures = JsonUtility.FromJson<TextureReport>(File.ReadAllText(Evidence + "Parent特效贴图校验.json"));
                Require(textures?.textures?.Length == 25, "Expected 25 original FX textures.");
                foreach (var record in textures.textures)
                {
                    string path = Folder + record.name + ".png";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Require(texture && texture.width == record.size[0] && texture.height == record.size[1],
                        record.name + ": imported texture dimensions differ.");
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Require(importer != null, record.name + ": missing texture importer.");
                    if (record.normalDXT5nmReconstructed)
                        Require(importer.textureType == TextureImporterType.NormalMap && !importer.sRGBTexture,
                            record.name + ": expected linear normal map.");
                }

                ValidateMeshes();
                Debug.Log("RAIN_PARENT_FX_ASSETS_PASSED textures=25 meshes=4");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_FX_ASSETS_FAILED");
                EditorApplication.Exit(1);
            }
        }

        internal static void ValidateMeshes()
        {
            var meshes = JsonUtility.FromJson<MeshReport>(File.ReadAllText(Evidence + "Parent特效网格校验.json"));
            Require(meshes?.meshes?.Length == 4, "Expected four original FX meshes including the death decal volume.");
            foreach (var record in meshes.meshes)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + record.name + ".asset");
                Require(mesh && mesh.vertexCount == record.vertexCount, record.name + ": vertex count differs.");
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    Require(data.Length == 1 && data[0].vertexBufferCount == 1,
                        record.name + ": unexpected vertex streams.");
                    var vertices = data[0].GetVertexData<byte>().ToArray();
                    var indices = data[0].GetIndexData<byte>().ToArray();
                    Require(vertices.Length == record.vertexBytes && Hash(vertices) == record.vertexSHA256,
                        record.name + ": imported vertex buffer differs from original.");
                    Require(indices.Length == record.indexBytes && Hash(indices) == record.indexSHA256,
                        record.name + ": imported index buffer differs from original.");
                }
                Debug.Log("RAIN_PARENT_FX_MESH_CHECKED " + record.name);
            }
        }

        static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
