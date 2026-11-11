using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // The world pickup uses two one-second alpha curves and one light curve.
    // This component restores only those source configurations, not general RoR2 script behavior.
    [DisallowMultipleComponent]
    public sealed class RangeAntlerParticleFade : MonoBehaviour
    {
        [SerializeField] AnimationCurve curve;
        [SerializeField] float duration = 1f;
        [SerializeField] bool drivesLight;
        [SerializeField] float sourceLightIntensity;
        Renderer targetRenderer;
        Light targetLight;
        MaterialPropertyBlock properties;
        float age;
        static readonly int ExternalAlpha = Shader.PropertyToID("_ExternalAlpha");

        public void ConfigureSource(AnimationCurve sourceCurve, float sourceDuration, bool lightCurve)
        {
            curve = sourceCurve;
            duration = sourceDuration;
            drivesLight = lightCurve;
            CacheTargets();
            if (drivesLight && targetLight) sourceLightIntensity = targetLight.intensity;
            age = 0f;
        }

        void CacheTargets()
        {
            if (drivesLight)
            {
                if (!targetLight) targetLight = GetComponent<Light>();
            }
            else
            {
                if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
                if (properties == null) properties = new MaterialPropertyBlock();
            }
        }

        void OnEnable()
        {
            CacheTargets();
            if (drivesLight)
            {
                age = 0f;
                if (targetLight) targetLight.intensity = 0f;
            }
            // Source alpha scripts use resetOnEnable=false, so their age is retained.
        }

        void Update()
        {
            age = drivesLight ? age + Time.deltaTime : Mathf.Min(duration, age + Time.deltaTime);
            EvaluateAt(age);
        }

        // Also used to preview the source curve without advancing the scene clock.
        public void EvaluateAt(float scaledAge)
        {
            if (curve == null || duration <= 0f) return;
            CacheTargets();
            float value = curve.Evaluate(scaledAge / duration);
            if (drivesLight)
            {
                if (targetLight) targetLight.intensity = value * sourceLightIntensity;
            }
            else if (targetRenderer)
            {
                targetRenderer.GetPropertyBlock(properties);
                properties.SetFloat(ExternalAlpha, value);
                targetRenderer.SetPropertyBlock(properties);
            }
        }

        void OnDisable()
        {
            if (drivesLight && targetLight) targetLight.intensity = sourceLightIntensity;
        }
    }
}
