using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterMotor
    {
        // The TPS human receiver uses original Commando mass/acceleration as its
        // explicit adapter profile. Other original monster profiles remain separate work.
        internal float RainPhysicsMass => _charInstance && _charInstance.RainEnemyKind == RangeResearchEnemy.ParentKind + 1 ? 300f : 100f;
        internal float RainPhysicsAcceleration => _charInstance && _charInstance.RainEnemyKind == RangeResearchEnemy.ParentKind + 1 ? 40f : 80f;
        internal Vector3 RainImpulseVelocity => rainImpulseActive ? rainImpulseVelocity : Vector3.zero;
        internal bool RainImpulseActive => rainImpulseActive;
        Vector3 rainImpulseVelocity, rainPreviousVelocity;
        float rainForceUngroundUntil;
        bool rainImpulseActive;

        [Server]
        internal void ServerApplyRainImpulse(Vector3 impulse)
        {
            if (!isActiveAndEnabled || !_charInstance || _charInstance.CurrentHealth <= 0) return;
            if (isOwned || _charInstance.BOT) ApplyRainImpulse(impulse);
            else if (connectionToClient != null) TargetApplyRainImpulse(connectionToClient, impulse);
        }

        [TargetRpc]
        void TargetApplyRainImpulse(NetworkConnectionToClient owner, Vector3 impulse)
        {
            if (isOwned) ApplyRainImpulse(impulse);
        }

        void ApplyRainImpulse(Vector3 impulse)
        {
            if (!isActiveAndEnabled || !_charInstance || _charInstance.CurrentHealth <= 0 ||
                !_controller || !_controller.enabled) return;
            Vector3 delta = RainCharacterImpulseMath.VelocityChange(impulse, RainPhysicsMass,
                _controller.isGrounded && Time.time >= rainForceUngroundUntil);
            if (delta == Vector3.zero) return;
            if (!rainImpulseActive) rainImpulseVelocity = rainPreviousVelocity;
            rainImpulseVelocity += delta;
            rainImpulseActive = true;
            // Original KinematicCharacterMotor.ForceUnground defaults to .1 seconds.
            if (delta.y > 0f) rainForceUngroundUntil = Time.time + .1f;
        }

        Vector3 ResolveRainImpulseDisplacement(Vector3 baselineDisplacement, float deltaTime)
        {
            if (deltaTime <= 0f) return Vector3.zero;
            Vector3 desired = baselineDisplacement / deltaTime;
            if (!rainImpulseActive)
            {
                rainPreviousVelocity = desired;
                return baselineDisplacement;
            }
            bool grounded = _controller.isGrounded && Time.time >= rainForceUngroundUntil;
            rainImpulseVelocity = RainCharacterImpulseMath.PreMove(rainImpulseVelocity, desired,
                RainPhysicsAcceleration, .25f, grounded, -30f, deltaTime);
            force.y = rainImpulseVelocity.y;
            rainPreviousVelocity = rainImpulseVelocity;
            Vector3 horizontalError = rainImpulseVelocity - desired;
            horizontalError.y = 0f;
            if (grounded && rainImpulseVelocity.y <= 0f && horizontalError.sqrMagnitude < .000001f)
                rainImpulseActive = false;
            return rainImpulseVelocity * deltaTime;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!rainImpulseActive || !(isOwned || isServer && _charInstance && _charInstance.BOT)) return;
            float intoSurface = Vector3.Dot(rainImpulseVelocity, hit.normal);
            if (intoSurface < 0f) rainImpulseVelocity -= hit.normal * intoSurface;
        }

        void ResetRainImpulse()
        {
            if (rainImpulseActive) force.y = 0f;
            rainImpulseActive = false;
            rainImpulseVelocity = rainPreviousVelocity = Vector3.zero;
            rainForceUngroundUntil = 0f;
        }
    }
}
