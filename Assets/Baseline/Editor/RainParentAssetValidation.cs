using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    // Asset-level validation only; combat, network, and visual fidelity require separate runs.
    public static class RainParentAssetValidation
    {
        private const string Folder = "Assets/Resources/RainActors/RoR2/Base/Characters/Parent/";

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this validation in the isolated batch project.");

            GameObject instance = null;
            Mesh baked = null;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "ParentMesh.asset");
                Require(mesh != null && mesh.vertexCount == 3202 && mesh.bindposes.Length == 60,
                    "Imported mesh or bind poses differ from the source.");
                var normal = AssetImporter.GetAtPath(Folder + "texParentNormal.png") as TextureImporter;
                Require(normal != null && normal.textureType == TextureImporterType.NormalMap && !normal.sRGBTexture,
                    "Normal map importer must use linear normal data.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "mdlParent.prefab");
                Require(prefab != null, "Model prefab failed to import.");
                instance = UnityEngine.Object.Instantiate(prefab);
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Require(skins.Length == 1, "Expected one Parent skin.");
                var skin = skins[0];
                Require(skin.sharedMesh == mesh && skin.bones.Length == 60 && skin.bones.All(b => b != null)
                    && skin.rootBone != null, "Skin has missing mesh or bone references.");
                Require(skin.sharedMaterial != null && skin.sharedMaterial.shader != null
                    && skin.sharedMaterial.GetTexture("_BaseMap") != null
                    && skin.sharedMaterial.GetTexture("_BumpMap") != null, "Material references are incomplete.");
                var slamZone = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SlamZone");
                Require(slamZone.parent.name == "Par_TrajectorySHJnt"
                    && Vector3.Distance(slamZone.localPosition, new Vector3(0, 0, 345)) < 0.001f,
                    "Slam locator hierarchy differs from the runtime source.");

                var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder.TrimEnd('/') })
                    .Select(id => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
                Require(clips.Length == 20, "Expected all twenty source animation clips.");
                baked = new Mesh();
                foreach (var clip in clips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    Require(bindings.Length > 0 && clip.length > 0, "Animation imported without curves: " + clip.name);
                    foreach (var binding in bindings)
                    {
                        if (binding.type == typeof(Transform))
                            Require(string.IsNullOrEmpty(binding.path) || instance.transform.Find(binding.path) != null,
                                "Unresolved animation path: " + clip.name + " / " + binding.path);
                    }

                    // Sample actual Unity curves, then bake the animated skin at three points.
                    Vector3[] initialVertices = null;
                    var deformed = false;
                    foreach (var fraction in new[] { 0f, 0.5f, 1f })
                    {
                        clip.SampleAnimation(instance, clip.length * fraction);
                        foreach (var bone in skin.bones)
                            Require(Finite(bone.localPosition) && Finite(bone.localEulerAngles) && Finite(bone.localScale),
                                "Non-finite animated pose: " + clip.name);
                        skin.BakeMesh(baked);
                        var vertices = baked.vertices;
                        Require(baked.vertexCount == 3202 && vertices.All(Finite)
                            && baked.bounds.size.sqrMagnitude > 0.01f, "Invalid animated skin: " + clip.name);
                        if (initialVertices == null) initialVertices = vertices;
                        else
                            for (var i = 0; i < vertices.Length; i++)
                                if ((vertices[i] - initialVertices[i]).sqrMagnitude > 0.000001f)
                                {
                                    deformed = true;
                                    break;
                                }
                    }
                    if (clip.name == "ParentArmature_Slam" || clip.name == "ParentArmature_WalkForward"
                        || clip.name == "ParentArmature_Spawn" || clip.name == "ParentArmature_Death")
                        Require(deformed, "Animation did not deform the imported mesh: " + clip.name);
                    Debug.Log("RAIN_PARENT_ANIMATION_CHECKED " + clip.name + " bindings=" + bindings.Length);
                }

                var slam = clips.Single(c => c.name == "ParentArmature_Slam");
                foreach (var property in new[] { "Slam.hitBoxActive", "armIKStrength" })
                {
                    var curve = AnimationUtility.GetEditorCurve(slam,
                        EditorCurveBinding.FloatCurve("", typeof(Animator), property));
                    Require(curve != null && curve.keys.Length > 1, "Missing Animator curve: " + property);
                    if (property == "Slam.hitBoxActive")
                    {
                        Require(curve.keys.Any(k => k.value > 0.5f) && curve.Evaluate(0) <= 0.5f,
                            "Slam activation curve has no inactive-to-active transition.");
                        Require(curve.Evaluate(0.7194f) < 0.5f && curve.Evaluate(0.7198f) > 0.5f,
                            "Slam activation timing differs from the original compressed polynomial.");
                    }
                    else
                        Require(curve.Evaluate(0.7f) < 0.01f && curve.Evaluate(0.8f) > 0.9f,
                            "Arm IK parameter lost its original step transition.");
                }
                Debug.Log("RAIN_PARENT_ASSETS_PASSED mesh=3202 bones=60 clips=20 locator=SlamZone");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
            finally
            {
                if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
