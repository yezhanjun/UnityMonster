using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    // Exact native-module staging validation. Materials and original-game MonoBehaviours remain separate gates.
    public static class RainParentNativeParticleValidation
    {
        const string Evidence = "Docs/玩法参考设计/雨中冒险2/原版依据/";
        static int comparedLeaves;

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateTextures();
                RainParentEffectsAssetValidation.ValidateMeshes();
                var source = JObject.Parse(File.ReadAllText(Evidence + "Parent原生粒子校验源.json"));
                int particles = 0;
                comparedLeaves = 0;
                foreach (var effect in source["effects"])
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)effect["assetPath"]);
                    Require(prefab, "Missing native prefab: " + effect["name"]);
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    instance.name = (string)effect["name"];
                    try
                    {
                        var nodes = (JArray)effect["nodes"];
                        Require(instance.GetComponentsInChildren<Transform>(true).Length == nodes.Count,
                            instance.name + ": hierarchy node count differs.");
                        foreach (var node in nodes)
                        {
                            var transform = Find(instance, (string)node["path"]);
                            Require(transform && transform.gameObject.activeSelf == (bool)node["active"],
                                node["path"] + ": missing or incorrect activation.");
                            var position = node["position"].ToObject<Vector3>();
                            var rotation = node["rotation"].ToObject<Quaternion>();
                            var scale = node["scale"].ToObject<Vector3>();
                            Require(Vector3.Distance(transform.localPosition, position) < .00001f &&
                                Quaternion.Angle(transform.localRotation, rotation) < .01f &&
                                Vector3.Distance(transform.localScale, scale) < .00001f,
                                node["path"] + ": local transform differs.");
                        }
                        foreach (var record in effect["nativeComponents"])
                        {
                            string type = (string)record["type"];
                            if (type == "MeshFilter")
                            {
                                var filter = Find(instance, (string)record["path"]).GetComponent<MeshFilter>();
                                var reference = record["data"]["m_Mesh"];
                                Require(filter && filter.sharedMesh &&
                                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string meshGuid, out long meshId)
                                    && meshGuid == (string)reference["guid"] && meshId == (long)reference["fileID"],
                                    record["path"] + ": original decal volume binding differs.");
                                continue;
                            }
                            if (type != "ParticleSystem" && type != "ParticleSystemRenderer") continue;
                            var node = Find(instance, (string)record["path"]);
                            Component component = type == "ParticleSystem"
                                ? (Component)node.GetComponent<ParticleSystem>() : node.GetComponent<ParticleSystemRenderer>();
                            Require(component, record["path"] + ": component missing: " + type);
                            var serialized = JObject.Parse(EditorJsonUtility.ToJson(component));
                            var actual = serialized[type] as JObject ?? serialized;
                            if (component is ParticleSystemRenderer renderer)
                            {
                                // Player serialization stores lightmap caches that EditorJsonUtility omits.
                                // Compare their public API equivalents instead of dropping these source fields.
                                actual["m_LightmapIndex"] = renderer.lightmapIndex & 0xffff;
                                actual["m_LightmapIndexDynamic"] = renderer.realtimeLightmapIndex & 0xffff;
                                actual["m_LightmapTilingOffset"] = Vector(renderer.lightmapScaleOffset);
                                actual["m_LightmapTilingOffsetDynamic"] = Vector(renderer.realtimeLightmapScaleOffset);
                                var meshes = new Mesh[4];
                                renderer.GetMeshes(meshes);
                                for (int i = 0; i < meshes.Length; i++)
                                {
                                    var reference = record["data"][i == 0 ? "m_Mesh" : "m_Mesh" + i];
                                    if (reference?["guid"] == null) continue;
                                    Require(meshes[i] && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(meshes[i], out string guid, out long id)
                                        && guid == (string)reference["guid"] && id == (long)reference["fileID"],
                                        record["path"] + ": original particle mesh binding differs.");
                                }
                            }
                            File.WriteAllText("ParentNative-" + record["pathId"] + ".json", actual.ToString());
                            Compare(record["data"], actual, (string)record["path"] + "." + type);
                            if (component is ParticleSystem system)
                            {
                                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                                system.Simulate(.5f, false, true, true);
                                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                                particles++;
                            }
                        }
                        Debug.Log("RAIN_PARENT_NATIVE_PARTICLES_CHECKED " + instance.name);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
                Require(particles == 34 && comparedLeaves > 10000, "Incomplete native particle comparison.");
                Debug.Log($"RAIN_PARENT_NATIVE_PARTICLES_PASSED particles={particles} comparedLeaves={comparedLeaves} materials_and_game_scripts_pending=True");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_NATIVE_PARTICLES_FAILED");
                EditorApplication.Exit(1);
            }
        }

        static Transform Find(GameObject root, string path)
        {
            int slash = path.IndexOf('/');
            return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
        }

        static JObject Vector(Vector4 value) => new JObject
        {
            ["x"] = value.x, ["y"] = value.y, ["z"] = value.z, ["w"] = value.w
        };

        static void Compare(JToken expected, JToken actual, string path)
        {
            if (expected is JObject reference && reference["fileID"] != null) return;
            Require(actual != null, path + ": native field missing after import.");
            if (expected is JObject obj)
            {
                // Object references are remapped by Unity; these need separate binding checks before runtime use.
                foreach (var property in obj.Properties())
                {
                    if (property.Name == "serializedVersion") continue;
                    Compare(property.Value, actual[property.Name], path + "." + property.Name);
                }
            }
            else if (expected is JArray array)
            {
                Require(actual is JArray other && other.Count == array.Count, path + ": native array length differs.");
                for (int i = 0; i < array.Count; i++) Compare(array[i], actual[i], path + "[" + i + "]");
            }
            else
            {
                if (expected.Type == JTokenType.Float || expected.Type == JTokenType.Integer)
                {
                    double a = (double)expected;
                    double b = actual.Type == JTokenType.Boolean ? ((bool)actual ? 1d : 0d) : (double)actual;
                    Require(Math.Abs(a - b) <= .000002 * Math.Max(1d, Math.Abs(a)),
                        path + ": value differs, expected=" + expected + " actual=" + actual);
                }
                else Require(JToken.DeepEquals(expected, actual), path + ": value differs.");
                comparedLeaves++;
            }
        }

        static void ValidateTextures()
        {
            var source = JObject.Parse(File.ReadAllText(Evidence + "Parent共享特效贴图校验.json"));
            int count = 0;
            foreach (var record in source["textures"])
            {
                string path = (string)record["assetPath"];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Require(texture && texture.width == (int)record["size"][0] && texture.height == (int)record["size"][1],
                    path + ": texture import differs.");
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer, path + ": texture importer missing.");
                if ((bool)record["normalDXT5nmReconstructed"])
                    Require(importer.textureType == TextureImporterType.NormalMap && !importer.sRGBTexture,
                        path + ": normal encoding differs.");
                count++;
            }
            Require(count == 33, "Expected 30 particle textures plus three additional death decal textures.");
            Debug.Log("RAIN_PARENT_SHARED_FX_TEXTURES_CHECKED count=33 includes_death_decal=True");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
