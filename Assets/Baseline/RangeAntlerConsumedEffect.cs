using System;
using UnityEngine;
using MTPSKIT.Gameplay.Gamemodes;

namespace MTPSKIT.Gameplay
{
    // Grow alone owns the effect lifetime, returning to the pool or using the source destroy fallback.
    // Explicit attachment avoids relying on imported MonoScript identities in the native prefab.
    public sealed class RangeAntlerConsumedEffect : MonoBehaviour
    {
        RangeGamemode owner;
        ParticleSystem tracked;
        ParticleSystem[] systems;
        bool pooled;

        public static RangeAntlerConsumedEffect Create(RangeGamemode owner, Transform recipient, Vector3 origin)
        {
            var effect = CreateInactive(owner, false);
            effect.Activate(recipient, origin);
            return effect;
        }

        internal static RangeAntlerConsumedEffect CreateInactive(RangeGamemode owner, bool pooled)
        {
            var prefab = Resources.Load<GameObject>("RainImported/AntlersConsumed/ElusiveAntlersConsumed-Prepared");
            if (!prefab) throw new InvalidOperationException("Antler consumed native prefab missing.");
            var root = Instantiate(prefab);
            root.SetActive(false);
            try
            {
                root.name = "Rain antler consumed effect";
                var effect = root.AddComponent<RangeAntlerConsumedEffect>();
                var grow = root.transform.Find("Grow");
                effect.tracked = grow ? grow.GetComponent<ParticleSystem>() : null;
                if (!effect.tracked) throw new InvalidOperationException("Antler consumed tracked Grow missing.");
                effect.owner = owner;
                effect.pooled = pooled;
                effect.systems = root.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var system in effect.systems)
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                return effect;
            }
            catch
            {
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
                throw;
            }
        }

        internal void Activate(Transform recipient, Vector3 origin)
        {
            // SetNetworkedObjectReference(body) resolves the body root; applyScale=false.
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            transform.SetPositionAndRotation(recipient ? recipient.position : origin,
                recipient ? recipient.rotation : Quaternion.identity);
            if (recipient) transform.SetParent(recipient, true);
            foreach (var system in systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(true);
            foreach (var system in systems) system.Play(false);
        }

        void Update()
        {
            // IsAlive uses the particle system's scaled simulation, including pause and emission lifetime.
            // Do not wait for all four systems or use the prefab's one-second duration as a timer.
            if (tracked && !tracked.IsAlive()) Release();
        }

        internal void Release()
        {
            if (pooled && owner)
            {
                owner.ReturnAntlerConsumedEffect(this);
                return;
            }
            if (owner) owner.ForgetAntlerConsumedEffect(this);
            owner = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        internal void ReleaseFromOwner()
        {
            owner = null; // The owner clears its collection after enumeration.
            pooled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (owner) owner.ForgetAntlerConsumedEffect(this);
            owner = null;
        }
    }
}
