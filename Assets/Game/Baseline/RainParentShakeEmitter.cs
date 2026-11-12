using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Original ShakeEmitter displacement path. Input rumble and production camera registration are separate.
    public sealed class RainParentShakeEmitter : MonoBehaviour
    {
        static readonly List<RainParentShakeEmitter> instances = new List<RainParentShakeEmitter>(16);
        public bool shakeOnStart = true, shakeOnEnable, shakeOnDelay;
        public float startDelay = 1;
        public float amplitude = 1, frequency = 1, cycleOffset;
        public float duration = 1, radius = 10;
        public bool scaleShakeRadiusWithLocalScale, amplitudeTimeDecay = true;
        float stopwatch = float.PositiveInfinity, halfPeriodTimer, delayStartTimer;
        Vector3 halfPeriodVector, currentOffset;
        bool hasShakedOnDelay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstances() => instances.Clear();

        public float Stopwatch => stopwatch;
        public Vector3 CurrentOffset => currentOffset;

        public void StartShake()
        {
            stopwatch = 0;
            halfPeriodVector = Random.onUnitSphere;
            halfPeriodTimer = (1f / frequency) * .5f;
        }

        void Start()
        {
            if (scaleShakeRadiusWithLocalScale) radius *= transform.localScale.x;
            if (shakeOnStart) StartShake();
        }

        void OnEnable()
        {
            instances.Add(this);
            delayStartTimer = startDelay;
            if (shakeOnEnable) StartShake();
        }

        void OnDisable()
        {
            currentOffset = Vector3.zero;
            instances.Remove(this);
        }

        void Update()
        {
            if (Time.deltaTime != 0) ManualUpdate(Time.deltaTime);
        }

        public void ResetShake()
        {
            stopwatch = float.PositiveInfinity;
            delayStartTimer = startDelay;
            hasShakedOnDelay = false;
            if (shakeOnStart) StartShake();
        }

        public void ManualUpdate(float deltaTime)
        {
            stopwatch += deltaTime;
            if (stopwatch < duration)
            {
                float fade = amplitudeTimeDecay ? 1 - stopwatch / duration : 1;
                halfPeriodTimer -= deltaTime;
                if (halfPeriodTimer < 0)
                {
                    halfPeriodVector = Vector3.Slerp(Random.onUnitSphere, -halfPeriodVector, .5f);
                    // Intentionally one correction, matching original high-frequency/large-step behavior.
                    halfPeriodTimer += (1f / frequency) * .5f;
                }
                currentOffset = halfPeriodVector * (Mathf.Sin(Mathf.PI * 2 * (frequency * halfPeriodTimer + cycleOffset)) * amplitude) * fade;
            }
            else currentOffset = Vector3.zero;
            if (shakeOnDelay)
            {
                delayStartTimer -= deltaTime;
                if (delayStartTimer < 0 && !hasShakedOnDelay)
                {
                    hasShakedOnDelay = true;
                    StartShake();
                }
            }
        }

        public static Vector3 ComputeTotalShakeAtPoint(Vector3 position)
        {
            Vector3 total = Vector3.zero;
            foreach (var emitter in instances)
            {
                float distanceSquared = (position - emitter.transform.position).sqrMagnitude;
                if (distanceSquared < emitter.radius * emitter.radius)
                    total += emitter.currentOffset * (1 - Mathf.Sqrt(distanceSquared) / emitter.radius);
            }
            return total;
        }

        // Call with the camera's unshaken state, never its previously displaced transform.
        public static Vector3 ResolveCameraPosition(Vector3 position, float shakeScale, float nearClipPlane, LayerMask worldMask)
        {
            Vector3 displacement = ComputeTotalShakeAtPoint(position) * shakeScale;
            if (displacement != Vector3.zero && Physics.SphereCast(position, nearClipPlane, displacement,
                out var hit, displacement.magnitude, worldMask, QueryTriggerInteraction.Ignore))
                return position + displacement.normalized * hit.distance;
            return position + displacement;
        }
    }
}
