using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Port of the original passive target. Parent uses no weight parameter or self planting.
    // Animated targets require the Animator to restore their pose before every LateUpdate.
    public sealed class RainParentIKTargetPassive : MonoBehaviour
    {
        public float minHeight = -4f;
        public float maxHeight = 1f;
        public float dampTime = .01f;
        public float baseOffset;
        public string animatorIKWeightFloat = "";
        public Animator animator;
        public bool selfPlant;
        public float selfPlantFrequency = -1f;
        public bool cacheFirstPosition;
        // Original LayerIndex.world must be mapped to this project's actual terrain layers.
        public LayerMask worldMask = 1;

        float smoothedTargetHeightOffset, targetHeightOffset, smoothdampVelocity;
        float selfPlantTimer;
        Vector3 cachedLocalPosition;

        void Awake()
        {
            if (cacheFirstPosition) cachedLocalPosition = transform.localPosition;
        }

        void LateUpdate()
        {
            selfPlantTimer -= Time.deltaTime;
            if (selfPlant && selfPlantTimer <= 0f)
            {
                selfPlantTimer = 1f / selfPlantFrequency;
                UpdateIKTargetPosition();
            }
            UpdateYOffset();
        }

        public void UpdateIKTargetPosition()
        {
            ResetTransformToCachedPosition();
            targetHeightOffset = Physics.Raycast(transform.position + Vector3.up * -minHeight,
                Vector3.down, out var hit, maxHeight - minHeight, worldMask)
                ? hit.point.y - transform.position.y : 0f;
            targetHeightOffset += baseOffset;
        }

        public void UpdateYOffset()
        {
            float weight = animator && !string.IsNullOrEmpty(animatorIKWeightFloat)
                ? animator.GetFloat(animatorIKWeightFloat) : 1f;
            smoothedTargetHeightOffset = Mathf.SmoothDamp(smoothedTargetHeightOffset, targetHeightOffset,
                ref smoothdampVelocity, dampTime, float.PositiveInfinity, Time.deltaTime);
            ResetTransformToCachedPosition();
            transform.position += Vector3.up * Mathf.Lerp(0f, smoothedTargetHeightOffset, weight);
        }

        void ResetTransformToCachedPosition()
        {
            if (cacheFirstPosition) transform.localPosition = cachedLocalPosition;
        }
    }
}
