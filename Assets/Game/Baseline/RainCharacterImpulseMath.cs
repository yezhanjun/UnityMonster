using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Original BlastAttack.Linear and default CharacterMotor PhysForceInfo path.
    internal static class RainCharacterImpulseMath
    {
        internal static Vector3 BlastImpulse(Vector3 hitOffset, float radius, float baseForce)
        {
            float distance = hitOffset.magnitude;
            if (distance <= 0f || radius <= 0f) return Vector3.zero;
            return hitOffset / distance * (baseForce * (1f - Mathf.Clamp01(distance / radius)));
        }

        internal static Vector3 VelocityChange(Vector3 impulse, float mass, bool grounded)
        {
            if (mass == 0f) return Vector3.zero;
            Vector3 delta = impulse / mass;
            if (grounded && delta.y < 6f) delta.y = 0f;
            return delta;
        }

        internal static Vector3 PreMove(Vector3 velocity, Vector3 desiredVelocity, float acceleration,
            float airControl, bool grounded, float gravity, float deltaTime)
        {
            desiredVelocity.y = velocity.y;
            velocity = Vector3.MoveTowards(velocity, desiredVelocity,
                acceleration * (grounded ? 1f : airControl) * deltaTime);
            velocity.y += gravity * deltaTime;
            if (grounded) velocity.y = Mathf.Max(velocity.y, 0f);
            return velocity;
        }
    }
}
