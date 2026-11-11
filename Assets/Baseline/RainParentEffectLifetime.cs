using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Original Parent FX uses scaled fixed time for timer effects and the first particle system for others.
    // These instances are owned by their spawner and destroyed; no pool owns them yet.
    public sealed class RainParentEffectLifetime : MonoBehaviour
    {
        public bool useTimer;
        public float duration = 3f;
        public bool resetAgeOnDisable;
        public ParticleSystem trackedParticleSystem;
        float age;

        void Start()
        {
            if (!useTimer && !trackedParticleSystem)
                trackedParticleSystem = GetComponentInChildren<ParticleSystem>();
        }

        void FixedUpdate()
        {
            if (!useTimer) return;
            age += Time.fixedDeltaTime;
            if (age > duration) Destroy(gameObject);
        }

        void Update()
        {
            if (!useTimer && trackedParticleSystem && !trackedParticleSystem.IsAlive())
                Destroy(gameObject);
        }

        void OnDisable()
        {
            if (resetAgeOnDisable) age = 0f;
        }
    }
}
