using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // All clips in these slots target the same imported hands and weapon hierarchy.
    public sealed class AaaViewAnimations : MonoBehaviour
    {
        public FPSPlayablesController player;
        public FPSAnimationAsset idle, equip, fire, reload, reloadAimed, finishReload, finishReloadAimed;
        public ParticleSystem muzzle;
        public Vector3 aimOffset;
        public Vector3 sprintPosition, sprintRotation, walkPosition, walkRotation, walkPositionFrequency, walkRotationFrequency;
        float phase, walkSpeed, walkAmount, sprintWeight;
        float shotStabilization;
        public Vector3 MotionPosition { get; private set; }
        public Vector3 MotionRotation { get; private set; }

        public void UpdateMovement(float speed, bool grounded, bool sprinting, float aimWeight)
        {
            float dt = Time.deltaTime;
            walkSpeed = Mathf.Lerp(walkSpeed, speed, 5 * dt);
            shotStabilization = Mathf.MoveTowards(shotStabilization, 0f, dt * 5f);
            float gait = grounded ? Mathf.Clamp01(speed / 3f) : 0f;
            float stability = 1f - Mathf.Max(aimWeight * 0.9f, shotStabilization * 0.65f);
            walkAmount = Mathf.Lerp(walkAmount, gait * stability, 1f - Mathf.Exp(-12f * dt));
            sprintWeight = Mathf.MoveTowards(sprintWeight, sprinting ? 1 : 0, 5 * dt);
            phase += dt * walkSpeed;
            MotionPosition = Wave(walkPosition, walkPositionFrequency) + sprintPosition * sprintWeight;
            MotionRotation = Wave(walkRotation, walkRotationFrequency) + sprintRotation * sprintWeight;
        }

        Vector3 Wave(Vector3 amount, Vector3 frequency)
        {
            var angles = phase * frequency;
            var wave = new Vector3(Mathf.Sin(angles.x), Mathf.Sin(angles.y), Mathf.Sin(angles.z));
            return Vector3.Scale(amount, wave) * walkAmount;
        }

        public bool Ready { get; private set; }

        public bool Initialize()
        {
            var animator = player.GetComponent<Animator>();
            animator.fireEvents = false; // TPS owns ammo, damage and camera kick.
            animator.Update(0);
            if (!player.InitializeController()) return false;
            player.enabled = true;
            Ready = player.PlayPose(idle);
            if (Ready) Play(equip);
            return Ready;
        }

        public void Fire()
        {
            shotStabilization = 1f;
            if (muzzle) muzzle.Play();
            Play(fire);
        }

        public void Reload(bool aiming) => Play(aiming && reloadAimed ? reloadAimed : reload);
        public void FinishReload(bool aiming) => Play(aiming && finishReloadAimed ? finishReloadAimed : finishReload);

        void Play(FPSAnimationAsset slot)
        {
            if (Ready && slot) player.PlayAnimation(slot);
        }
    }
}
