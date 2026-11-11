using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MeleeHitStopService : MonoBehaviour
    {
        private const int MaxActiveAnimators = 16;
        private const float SpeedComparisonEpsilon = 0.0001f;

        private struct HitStopSlot
        {
            public Animator Animator;
            public float BaseSpeed;
            public float AppliedSpeed;
            public float Rate;
            public float EndTime;
            public bool Active;
        }

        private readonly HitStopSlot[] slots = new HitStopSlot[MaxActiveAnimators];

        public void PlayHitStop(Animator animator, float duration)
        {
            PlayHitStop(animator, 0f, duration);
        }

        public void PlayHitStop(Animator animator, float rate, float duration)
        {
            if (animator == null || duration <= 0f)
            {
                return;
            }

            float now = Time.unscaledTime;
            int slotIndex = FindAnimatorSlot(animator);
            if (slotIndex < 0)
            {
                slotIndex = FindFreeOrEarliestEndingSlot();
                RestoreAndClear(slotIndex);

                ref HitStopSlot newSlot = ref slots[slotIndex];
                newSlot.Animator = animator;
                newSlot.BaseSpeed = animator.speed;
                newSlot.AppliedSpeed = animator.speed;
                newSlot.Rate = Mathf.Clamp01(rate);
                newSlot.EndTime = now + duration;
                newSlot.Active = true;
            }
            else
            {
                ref HitStopSlot existingSlot = ref slots[slotIndex];
                CaptureExternalSpeedChange(ref existingSlot);
                float remainingDuration = Mathf.Max(0f, existingSlot.EndTime - now);
                existingSlot.Rate = Mathf.Min(existingSlot.Rate, Mathf.Clamp01(rate));
                existingSlot.EndTime = now + Mathf.Max(remainingDuration, duration);
            }

            ApplySlot(ref slots[slotIndex]);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < slots.Length; i++)
            {
                ref HitStopSlot slot = ref slots[i];
                if (!slot.Active)
                {
                    continue;
                }

                if (slot.Animator == null || now >= slot.EndTime)
                {
                    RestoreAndClear(i);
                    continue;
                }

                CaptureExternalSpeedChange(ref slot);
                ApplySlot(ref slot);
            }
        }

        // Use the applied slot until Update restores it, so animation and movement
        // observe the same rate even on the expiry frame. Never include BaseSpeed.
        public float GetMotionDeltaTime(Animator animator, float deltaTime)
        {
            int index = animator != null ? FindAnimatorSlot(animator) : -1;
            return deltaTime * (index >= 0 ? slots[index].Rate : 1f);
        }

        public void CancelHitStop(Animator animator)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Active || (animator != null && slots[i].Animator != animator))
                {
                    continue;
                }

                RestoreAndClear(i);
            }
        }

        /// <summary>Change the unpaused animation speed without losing an active hit stop.</summary>
        public void SetAnimatorBaseSpeed(Animator animator, float speed)
        {
            if (animator == null) return;
            int index = FindAnimatorSlot(animator);
            if (index < 0) { animator.speed = speed; return; }
            ref HitStopSlot slot = ref slots[index];
            slot.BaseSpeed = speed;
            if (Time.unscaledTime >= slot.EndTime)
            {
                // RestoreAndClear must see our last applied value, not an external write.
                ApplySlot(ref slot);
                RestoreAndClear(index);
                return;
            }
            ApplySlot(ref slot);
        }

        private void OnDisable()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                RestoreAndClear(i);
            }
        }

        private int FindAnimatorSlot(Animator animator)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Active && slots[i].Animator == animator)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindFreeOrEarliestEndingSlot()
        {
            int earliestSlot = 0;
            float earliestEndTime = float.PositiveInfinity;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Active)
                {
                    return i;
                }

                if (slots[i].EndTime < earliestEndTime)
                {
                    earliestEndTime = slots[i].EndTime;
                    earliestSlot = i;
                }
            }

            return earliestSlot;
        }

        private static void CaptureExternalSpeedChange(ref HitStopSlot slot)
        {
            if (slot.Animator == null ||
                Mathf.Abs(slot.Animator.speed - slot.AppliedSpeed) <= SpeedComparisonEpsilon)
            {
                return;
            }

            slot.BaseSpeed = slot.Animator.speed;
        }

        private static void ApplySlot(ref HitStopSlot slot)
        {
            if (slot.Animator == null)
            {
                return;
            }

            slot.AppliedSpeed = slot.BaseSpeed * slot.Rate;
            slot.Animator.speed = slot.AppliedSpeed;
        }

        private void RestoreAndClear(int slotIndex)
        {
            ref HitStopSlot slot = ref slots[slotIndex];
            if (!slot.Active)
            {
                return;
            }

            if (slot.Animator != null &&
                Mathf.Abs(slot.Animator.speed - slot.AppliedSpeed) <= SpeedComparisonEpsilon)
            {
                slot.Animator.speed = slot.BaseSpeed;
            }

            slot = default;
        }
    }
}
