using System.IO;
using MTPSKIT.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public sealed class RainAntlerBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!report.summary.outputPath.Replace('\\', '/').EndsWith("/RainItemsFormal/TPSShoot.exe", System.StringComparison.OrdinalIgnoreCase)) return;
            ValidateNativeRecipe();
        }

        public static void ValidateNativeRecipe()
        {
            const string folder = "Assets/Resources/RainImported/Antlers/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "ElusiveAntlersWorldParticles-Materials.prefab");
            if (!prefab || prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 3)
                throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED native particles");
            foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0 || node.GetComponents<MonoBehaviour>().Length != 0)
                    throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED native asset contains runtime scripts: " + node.name);
            foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                string expected = renderer.name == "WispBase" ? "RainItems/AntlersIntersectionRemap" : "RainActors/ParentCloudRemap";
                if (!material || material.shader.name != expected || ShaderUtil.ShaderHasError(material.shader))
                    throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED original material port: " + renderer.name);
            }
            if (File.Exists(folder + "ElusiveAntlersWorldParticles-Curves.prefab"))
                throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED obsolete serialized-script prefab");
            if (File.ReadAllText(folder + "WorldScriptCurves.json") != File.ReadAllText("Docs/FrontEnd/antlers-world-script-curves-source.json"))
                throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED curve source provenance");
            var instance = RangeAntlerParticleFactory.Create(Vector3.zero);
            try
            {
                if (instance.GetComponentsInChildren<RangeAntlerParticleFade>(true).Length != 3)
                    throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED runtime curves");
            }
            finally { Object.DestroyImmediate(instance); }
            var consumed = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/RainImported/AntlersConsumed/ElusiveAntlersConsumed-Prepared.prefab");
            if (!consumed || consumed.GetComponentsInChildren<ParticleSystem>(true).Length != 4)
                throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED consumed native particles");
            foreach (var node in consumed.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0 || node.GetComponents<MonoBehaviour>().Length != 0)
                    throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED consumed script binding");
            foreach (var renderer in consumed.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (!renderer.sharedMaterial || renderer.sharedMaterial.shader.name != "RainItems/AntlersConsumedCloudRemap" ||
                    ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))
                    throw new BuildFailedException("RAIN_ANTLER_BUILD_PREFLIGHT_FAILED consumed material port");
            Debug.Log("RAIN_ANTLER_CONSUMED_BUILD_PREFLIGHT_PASSED systems=4");
            Debug.Log("RAIN_ANTLER_BUILD_PREFLIGHT_PASSED native_particles=3 runtime_curves=3");
        }
    }
}
