using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Consume Animator root deltas without moving its child model outside the character capsule.
    public sealed class MeleeRootMotionReceiver : MonoBehaviour
    {
        Animator animatorSource;
        CharacterMotor motor;
        CharacterAnimator characterAnimator;

        public void Initialize(Animator source, CharacterMotor owner, CharacterAnimator animationOwner)
        {
            animatorSource = source;
            motor = owner;
            characterAnimator = animationOwner;
        }

        void OnAnimatorMove()
        {
            if (!animatorSource) return;
            if (motor) motor.QueueRootMotion(animatorSource.deltaPosition);
            if (characterAnimator) characterAnimator.ApplyTurnRootRotation(animatorSource.deltaRotation);
        }
    }
}
