using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    // Editor-only source recovery. The prepared prefab deliberately contains no game scripts.
    public static class RainAntlerConsumedImport
    {
        const string Folder = "Assets/Resources/RainImported/AntlersConsumed/";
        static int leaves;

        public static void ImportAndValidate(Shader shader)
        {
            var source = JObject.Parse(File.ReadAllText("Docs/FrontEnd/antlers-consumed-effect-audio-source.json"));
            var manifest = JObject.Parse(File.ReadAllText("Docs/FrontEnd/antlers-consumed-assets.json"));
            var dependencies = (JObject)source["dependencies"];
            var assets = (JObject)manifest["assets"];
            foreach (var entry in assets.Properties())
                Require(Hash(File.ReadAllBytes((string)entry.Value["assetPath"])) == (string)entry.Value["sha256"],
                    "Exported source asset changed: " + entry.Name);
            Require(Hash(File.ReadAllBytes((string)manifest["nativePrefab"])) == (string)manifest["nativePrefabSHA256"],
                "Native consumed prefab changed before import.");
            int textures = 0, meshes = 0;
            foreach (var entry in dependencies.Properties())
            {
                var record = entry.Value;
                if ((string)record["type"] == "Texture2D")
                {
                    ImportTexture((string)assets[entry.Name]["assetPath"], record["sampling"]);
                    textures++;
                }
                else if ((string)record["type"] == "Mesh")
                {
                    ValidateMesh((string)assets[entry.Name]["assetPath"], record);
                    meshes++;
                }
            }
            Require(textures == 10 && meshes == 2, "Incomplete consumed source dependencies.");
            var materials = new Dictionary<string, Material>();
            foreach (var entry in dependencies.Properties())
            {
                var record = entry.Value;
                if ((string)record["type"] != "Material") continue;
                var data = record["data"];
                string[] keywords = data["m_ValidKeywords"].Values<string>().OrderBy(x => x).ToArray();
                string flags = string.Join(" ", keywords);
                Require(flags == "DISABLEREMAP" || flags == "DISABLEREMAP USE_CLOUDS" ||
                    flags == "CLOUDOFFSET USE_CLOUDS" || flags == "VERTEXCOLOR", "Unsupported source variant: " + flags);
                string path = Folder + (string)record["name"] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.shaderKeywords = keywords;
                material.enableInstancing = (bool)data["m_EnableInstancingVariants"];
                material.doubleSidedGI = (bool)data["m_DoubleSidedGI"];
                material.globalIlluminationFlags = (MaterialGlobalIlluminationFlags)(int)data["m_LightmapFlags"];
                foreach (var pair in data["m_SavedProperties"]["m_Floats"])
                    if (material.HasProperty((string)pair[0])) material.SetFloat((string)pair[0], (float)pair[1]);
                foreach (var pair in data["m_SavedProperties"]["m_Colors"])
                {
                    var c = pair[1];
                    if (material.HasProperty((string)pair[0]))
                        material.SetVector((string)pair[0], new Vector4((float)c["r"], (float)c["g"], (float)c["b"], (float)c["a"]));
                }
                foreach (var pair in data["m_SavedProperties"]["m_TexEnvs"])
                {
                    string property = (string)pair[0];
                    if (!material.HasProperty(property)) continue;
                    material.SetTexture(property, null);
                    material.SetTextureScale(property, pair[1]["m_Scale"].ToObject<Vector2>());
                    material.SetTextureOffset(property, pair[1]["m_Offset"].ToObject<Vector2>());
                }
                foreach (var binding in record["textures"])
                {
                    string property = (string)binding["property"];
                    Require(material.HasProperty(property), "Shader lost source texture property: " + property);
                    string texturePath = (string)assets[(string)binding["dependency"]]["assetPath"];
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    Require(texture, "Missing consumed texture: " + texturePath);
                    material.SetTexture(property, texture);
                }
                material.renderQueue = (int)data["m_CustomRenderQueue"];
                EditorUtility.SetDirty(material);
                materials.Add(entry.Name, material);
            }
            Require(materials.Count == 4, "Expected four original consumed materials.");
            AssetDatabase.SaveAssets();
            var nodes = (JArray)source["effectNodes"];
            var root = PrefabUtility.LoadPrefabContents((string)manifest["nativePrefab"]);
            try
            {
                var transforms = Resolve(root, nodes);
                int renderers = 0;
                for (int i = 0; i < nodes.Count; i++)
                    foreach (var component in nodes[i]["components"])
                    {
                        if ((string)component["type"] != "ParticleSystemRenderer") continue;
                        var renderer = transforms[i].GetComponent<ParticleSystemRenderer>();
                        Require(renderer, "Missing native consumed renderer.");
                        renderer.sharedMaterials = component["resolvedMaterials"].Select(k => materials[(string)k]).ToArray();
                        var originalMesh = component["resolvedMeshes"]["m_Mesh"];
                        renderer.mesh = originalMesh.Type == JTokenType.Null ? null :
                            AssetDatabase.LoadAssetAtPath<Mesh>((string)assets[(string)originalMesh]["assetPath"]);
                        renderers++;
                    }
                Require(renderers == 4, "Incomplete consumed renderer binding.");
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "ElusiveAntlersConsumed-Prepared.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ElusiveAntlersConsumed-Prepared.prefab");
            Require(prefab && prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "Consumed prefab must use explicit runtime script attachment.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            leaves = 0;
            try
            {
                var transforms = Resolve(instance, nodes);
                int particleCount = 0;
                for (int i = 0; i < nodes.Count; i++)
                    foreach (var record in nodes[i]["components"])
                    {
                        string type = (string)record["type"];
                        if (type != "ParticleSystem" && type != "ParticleSystemRenderer") continue;
                        Component component = type == "ParticleSystem" ? (Component)transforms[i].GetComponent<ParticleSystem>() :
                            transforms[i].GetComponent<ParticleSystemRenderer>();
                        Require(component, "Missing consumed native component.");
                        var serialized = JObject.Parse(EditorJsonUtility.ToJson(component));
                        var actual = serialized[type] as JObject ?? serialized;
                        if (component is ParticleSystemRenderer renderer)
                        {
                            actual["m_LightmapIndex"] = renderer.lightmapIndex & 0xffff;
                            actual["m_LightmapIndexDynamic"] = renderer.realtimeLightmapIndex & 0xffff;
                            actual["m_LightmapTilingOffset"] = Vector(renderer.lightmapScaleOffset);
                            actual["m_LightmapTilingOffsetDynamic"] = Vector(renderer.realtimeLightmapScaleOffset);
                            var slots = record["resolvedMaterials"].Select(k => materials[(string)k]).ToArray();
                            Require(renderer.sharedMaterials.SequenceEqual(slots), "Saved consumed material references differ.");
                            var meshSlots = new Mesh[4];
                            renderer.GetMeshes(meshSlots);
                            for (int slot = 0; slot < 4; slot++)
                            {
                                var key = record["resolvedMeshes"][slot == 0 ? "m_Mesh" : "m_Mesh" + slot];
                                var expected = key.Type == JTokenType.Null ? null :
                                    AssetDatabase.LoadAssetAtPath<Mesh>((string)assets[(string)key]["assetPath"]);
                                Require(meshSlots[slot] == expected, "Saved consumed mesh slot differs.");
                            }
                        }
                        Compare(record["data"], actual, (string)nodes[i]["name"] + "." + type);
                        if (component is ParticleSystem system)
                        {
                            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                            system.Simulate(.1f, false, true, true);
                            Require(system.particleCount > 0, "Consumed native system emitted no particles: " + nodes[i]["name"]);
                            Debug.Log("RAIN_ANTLER_CONSUMED_PARTICLE_CHECKED " + nodes[i]["name"] + " count=" + system.particleCount);
                            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                            particleCount++;
                        }
                    }
                // Exact scalar count from these eight source components, excluding remapped PPtrs/version tags.
                Require(particleCount == 4 && leaves == 7681,
                    "Incomplete consumed native parameter comparison: systems=" + particleCount + " leaves=" + leaves);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            Debug.Log($"RAIN_ANTLER_CONSUMED_BINDINGS_PASSED nodes=5 systems=4 materials=4 meshes=2 textures=10 comparedLeaves={leaves} decoded_texture_gpu_format_equivalence_pending=True");
        }

        static void ImportTexture(string path, JToken source)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer, "Missing source texture importer: " + path);
            var settings = source["m_TextureSettings"];
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = (int)source["m_ColorSpace"] == 1; // Native TextureColorSpace, not public ColorSpace.
            importer.mipmapEnabled = (int)source["m_MipCount"] > 1;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Math.Max((int)source["m_Width"], (int)source["m_Height"]);
            importer.textureCompression = TextureImporterCompression.Uncompressed; // Do not recompress decoded source channels.
            importer.filterMode = (FilterMode)(int)settings["m_FilterMode"];
            importer.wrapModeU = (TextureWrapMode)(int)settings["m_WrapU"];
            importer.wrapModeV = (TextureWrapMode)(int)settings["m_WrapV"];
            importer.wrapModeW = (TextureWrapMode)(int)settings["m_WrapW"];
            importer.anisoLevel = (int)settings["m_Aniso"];
            importer.mipMapBias = (float)settings["m_MipBias"];
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(texture && texture.width == (int)source["m_Width"] && texture.height == (int)source["m_Height"] &&
                texture.mipmapCount == (int)source["m_MipCount"] && texture.isDataSRGB == ((int)source["m_ColorSpace"] == 1) &&
                texture.filterMode == importer.filterMode && texture.wrapModeU == importer.wrapModeU &&
                texture.wrapModeV == importer.wrapModeV && texture.wrapModeW == importer.wrapModeW &&
                texture.anisoLevel == importer.anisoLevel && Math.Abs(texture.mipMapBias - importer.mipMapBias) < .000001f,
                "Imported consumed texture sampling differs: " + path);
        }

        static void ValidateMesh(string path, JToken source)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Require(mesh && mesh.vertexCount == (int)source["vertexCount"] &&
                mesh.subMeshCount == source["data"]["m_SubMeshes"].Count(), "Consumed mesh topology differs.");
            for (int i = 0; i < mesh.subMeshCount; i++)
                Require(mesh.GetIndexCount(i) == (uint)source["data"]["m_SubMeshes"][i]["indexCount"], "Consumed mesh index count differs.");
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                Require(Hash(data[0].GetVertexData<byte>().ToArray()) == (string)source["vertexBufferSHA256"] &&
                    Hash(data[0].GetIndexData<byte>().ToArray()) == (string)source["indexBufferSHA256"], "Consumed mesh raw buffers differ.");
        }

        static Transform[] Resolve(GameObject root, JArray nodes)
        {
            Require(root.GetComponentsInChildren<Transform>(true).Length == nodes.Count, "Consumed hierarchy node count differs.");
            var result = new Transform[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                result[i] = i == 0 ? root.transform : result[(int)nodes[i]["parent"]].Find((string)nodes[i]["name"]);
                Require(result[i] && result[i].gameObject.activeSelf == (bool)nodes[i]["active"], "Consumed hierarchy differs.");
                var t = nodes[i]["components"].First(c => (string)c["type"] == "Transform")["data"];
                Require(Vector3.Distance(result[i].localPosition, t["m_LocalPosition"].ToObject<Vector3>()) < .00001f &&
                    Quaternion.Angle(result[i].localRotation, t["m_LocalRotation"].ToObject<Quaternion>()) < .01f &&
                    Vector3.Distance(result[i].localScale, t["m_LocalScale"].ToObject<Vector3>()) < .00001f, "Consumed local transform differs.");
            }
            return result;
        }

        static JObject Vector(Vector4 v) => new JObject { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z, ["w"] = v.w };
        static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        static void Compare(JToken expected, JToken actual, string path)
        {
            if (expected is JObject reference && reference["m_PathID"] != null) return; // Bindings checked separately.
            Require(actual != null, path + ": missing native field.");
            if (expected is JObject obj)
            {
                foreach (var property in obj.Properties())
                    if (property.Name != "serializedVersion" && property.Name != "m_GameObject")
                        Compare(property.Value, actual[property.Name], path + "." + property.Name);
            }
            else if (expected is JArray array)
            {
                Require(actual is JArray other && other.Count == array.Count, path + ": array count differs.");
                for (int i = 0; i < array.Count; i++) Compare(array[i], actual[i], path + "[" + i + "]");
            }
            else
            {
                if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
                {
                    double a = (double)expected, b = actual.Type == JTokenType.Boolean ? ((bool)actual ? 1d : 0d) : (double)actual;
                    Require(a.Equals(b) || Math.Abs(a - b) <= .000002 * Math.Max(1d, Math.Abs(a)), path + ": expected=" + expected + " actual=" + actual);
                }
                else Require(JToken.DeepEquals(expected, actual), path + ": native value differs.");
                leaves++;
            }
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
