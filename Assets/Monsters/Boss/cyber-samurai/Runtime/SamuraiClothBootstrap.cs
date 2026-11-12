using MagicaCloth2;
using UnityEngine;

namespace CyberSamurai
{
    // Build the cloth against the authored bind pose before animation starts.
    [DefaultExecutionOrder(-10000)]
    public sealed class SamuraiClothBootstrap : MonoBehaviour
    {
        public Animator animator;
        public MagicaCloth cloth;
        public MagicaCloth[] cloths;
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        bool resumeAnimator;
        int remaining;

        void Awake()
        {
            if (!animator || !cloth) { Failed = true; enabled = false; return; }
            resumeAnimator = animator.enabled;
            animator.enabled = false;
            if (cloths == null || cloths.Length == 0) cloths = new[] { cloth };
            remaining = cloths.Length;
            foreach (var item in cloths) item.OnBuildComplete += OnBuilt;
        }

        void OnBuilt(MagicaCloth sender, bool success)
        {
            sender.OnBuildComplete -= OnBuilt;
            Failed |= !success;
            remaining--;
            Ready = remaining == 0 && !Failed;
            if (remaining == 0 && animator) animator.enabled = resumeAnimator;
            if (!success) Debug.LogError("Cyber Samurai cloth build failed: " + sender.Process.Result.GetResultString(), this);
        }

        void OnDestroy()
        {
            if (cloths != null) foreach (var item in cloths) if (item) item.OnBuildComplete -= OnBuilt;
        }
    }
}
