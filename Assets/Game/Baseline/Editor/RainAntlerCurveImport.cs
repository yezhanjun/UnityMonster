using System;
using System.Reflection;
using MTPSKIT.Gameplay;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerCurveImport
    {
        public static void ImportAndValidate()
        {
            var root = RangeAntlerParticleFactory.Create(Vector3.zero);
            root.SetActive(false);
            try
            {
                var fades = root.GetComponentsInChildren<RangeAntlerParticleFade>(true);
                Require(fades.Length == 3, "runtime component count");
                foreach (var fade in fades)
                {
                    var light = fade.GetComponent<Light>();
                    float baseIntensity = light ? light.intensity : 0f;
                    var renderer = fade.GetComponent<Renderer>();
                    var block = new MaterialPropertyBlock();
                    foreach (float age in new[] { 0f, .25f, .5f, 1f, 2f })
                    {
                        fade.EvaluateAt(age);
                        float actual;
                        if (light) actual = light.intensity / baseIntensity;
                        else { renderer.GetPropertyBlock(block); actual = block.GetFloat("_ExternalAlpha"); }
                        float t = Mathf.Clamp01(age);
                        float expected = light ? t * (2f - t) : t;
                        Require(Mathf.Abs(actual - expected) < .00001f, fade.name + " independent curve sample");
                    }
                    if (light)
                    {
                        typeof(RangeAntlerParticleFade).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fade, null);
                        Require(light.intensity == 0f, "light enable reset");
                        typeof(RangeAntlerParticleFade).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fade, null);
                        Require(Mathf.Approximately(light.intensity, baseIntensity), "light disable restore");
                    }
                    Debug.Log("RAIN_ANTLER_SCRIPT_CURVE_CHECKED " + fade.name);
                }
                RainAntlerBuildValidation.ValidateNativeRecipe();
                Debug.Log("RAIN_ANTLER_RUNTIME_CURVE_RECIPE_PASSED native_asset_and_dynamic_components=True player_lifecycle_and_shader_pending=True");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Require(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException("RAIN_ANTLER_RUNTIME_CURVE_RECIPE_FAILED " + detail);
        }
    }
}
