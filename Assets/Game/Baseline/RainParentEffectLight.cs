using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [RequireComponent(typeof(Light))]
    public sealed class RainParentEffectLight : MonoBehaviour
    {
        public AnimationCurve curve;
        public float timeMax = .2f;
        public bool loop;
        public bool randomStart;
        public bool enableNegativeLights;
        Light target;
        float maximumIntensity;
        Color originalColor;
        float age;

        void Awake()
        {
            target = GetComponent<Light>();
            maximumIntensity = target.intensity;
            originalColor = target.color;
        }

        void OnEnable()
        {
            age = randomStart ? Random.Range(0f, timeMax) : 0f;
            target.intensity = 0f;
            target.color = enableNegativeLights
                ? new Color(-originalColor.r, -originalColor.g, -originalColor.b, originalColor.a)
                : originalColor;
        }

        void Update()
        {
            age += Time.deltaTime;
            target.intensity = curve.Evaluate(timeMax > 0f ? age / timeMax : 1f) * maximumIntensity;
            if (age >= timeMax && loop) age = 0f;
        }

        void OnDisable()
        {
            if (!target) return;
            target.intensity = maximumIntensity;
            target.color = originalColor;
        }
    }
}
