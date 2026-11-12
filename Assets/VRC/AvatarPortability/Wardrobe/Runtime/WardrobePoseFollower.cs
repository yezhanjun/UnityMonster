using UnityEngine;

namespace AvatarPortability.Wardrobe
{
    /// <summary>Drives retained clothing bones before ordinary LateUpdate physics solvers.</summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class WardrobePoseFollower : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Transform[] sources;
        [SerializeField, HideInInspector] private Transform[] targets;
        [SerializeField, HideInInspector] private bool preserveRestRotationOffsets;
        [SerializeField, HideInInspector] private Quaternion[] restRotationOffsets;

        internal void Initialize(Transform[] sourceBones, Transform[] targetBones, bool preserveOffsets = false, Quaternion[] authoredOffsets = null)
        {
            Quaternion[] nextOffsets = null;
            if (preserveOffsets && authoredOffsets != null)
            {
                if (sourceBones == null || authoredOffsets.Length != sourceBones.Length) throw new System.ArgumentException("Authored rest offset count mismatch");
                nextOffsets = (Quaternion[])authoredOffsets.Clone();
            }
            else if (preserveOffsets && sourceBones != null && targetBones != null && sourceBones.Length == targetBones.Length)
            {
                nextOffsets = new Quaternion[sourceBones.Length];
                bool reuse = preserveRestRotationOffsets && sources != null && targets != null &&
                    restRotationOffsets != null && sources.Length == targets.Length && restRotationOffsets.Length == sources.Length;
                // Capture all world rotations before parent-first Synchronize changes any source transform.
                // Keep existing pairs' offsets on rebinding, even if targets moved since the last sync.
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    int previous = -1;
                    if (reuse)
                        for (int j = 0; j < sources.Length; j++)
                            if (sources[j] == sourceBones[i] && targets[j] == targetBones[i]) { previous = j; break; }
                    nextOffsets[i] = previous >= 0 ? restRotationOffsets[previous] :
                        sourceBones[i] && targetBones[i] ? Quaternion.Inverse(targetBones[i].rotation) * sourceBones[i].rotation : Quaternion.identity;
                }
            }
            sources = sourceBones;
            targets = targetBones;
            preserveRestRotationOffsets = preserveOffsets;
            restRotationOffsets = nextOffsets;
            Synchronize();
        }

        private void LateUpdate() { Synchronize(); }

        public void Synchronize()
        {
            if (sources == null || targets == null) return;
            bool useOffsets = preserveRestRotationOffsets && sources.Length == targets.Length &&
                restRotationOffsets != null && restRotationOffsets.Length == sources.Length;
            // Sources are stored parent first so descendants see the updated parent pose.
            for (int i = 0; i < sources.Length && i < targets.Length; i++)
            {
                Transform source = sources[i];
                Transform target = targets[i];
                if (!source || !target) continue;
                Quaternion rotation = target.rotation;
                if (useOffsets) rotation *= restRotationOffsets[i];
                source.SetPositionAndRotation(target.position, rotation);
                Vector3 parentScale = source.parent ? source.parent.lossyScale : Vector3.one;
                Vector3 scale = target.lossyScale;
                source.localScale = new Vector3(Divide(scale.x, parentScale.x),
                    Divide(scale.y, parentScale.y), Divide(scale.z, parentScale.z));
            }
        }

        private static float Divide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.000001f ? value / divisor : 1f;
        }
    }
}
