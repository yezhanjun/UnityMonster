using UnityEngine;

namespace MTPSKIT.Gameplay
{
    /// <summary>Independent copy of guard-success sparks; world-space emission and ground collision.</summary>
    public sealed class CombatStampSparkPool : MonoBehaviour
    {
        const int Capacity = 24;
        static CombatStampSparkPool instance;
        readonly ParticleSystem[] particles = new ParticleSystem[Capacity];
        readonly float[] expires = new float[Capacity];
        int cursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Warmup()
        {
            if (instance != null) return;
            var prefab = Resources.Load<GameObject>("CombatScratch/Contact/StampGuardSparks");
            if (prefab == null) return;
            var root = new GameObject("Stamp Guard Spark Pool"); DontDestroyOnLoad(root);
            instance = root.AddComponent<CombatStampSparkPool>();
            for (int i = 0; i < Capacity; i++)
            {
                var go = Instantiate(prefab, root.transform);
                var ps = go.GetComponent<ParticleSystem>();
                instance.particles[i] = ps;
                var collision = ps.collision;
                collision.collidesWith = GameManager.environmentLayer;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                go.SetActive(false);
            }
        }

        public static void Play(Vector3 position, Vector3 outward, Color stampColor)
        {
            if (!Application.isPlaying) return;
            if (instance == null) Warmup();
            if (instance == null) return;
            int slot = instance.cursor++ % Capacity;
            var ps = instance.particles[slot];
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Vector3 direction = Vector3.up * .8f + (outward.sqrMagnitude > .0001f ? outward.normalized : Vector3.forward) * .6f;
            ps.transform.SetPositionAndRotation(position + direction.normalized * .025f, Quaternion.LookRotation(direction));
            float peak = Mathf.Max(.001f, Mathf.Max(stampColor.r, Mathf.Max(stampColor.g, stampColor.b)));
            Color tint = new Color(stampColor.r / peak * 2, stampColor.g / peak * 2, stampColor.b / peak * 2, 1);
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(tint * new Color(.65f,.65f,.65f,1), tint);
            ps.gameObject.SetActive(true); ps.Play(true);
            instance.expires[slot] = Time.time + main.duration + main.startLifetime.constantMax + .1f;
        }

        void LateUpdate()
        {
            for (int i = 0; i < Capacity; i++)
                if (particles[i] != null && particles[i].gameObject.activeSelf && Time.time >= expires[i])
                {
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particles[i].gameObject.SetActive(false);
                }
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
