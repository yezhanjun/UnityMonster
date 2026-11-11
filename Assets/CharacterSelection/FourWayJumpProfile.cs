using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [CreateAssetMenu(menuName = "TPS/Four Way Jump")]
    public sealed class FourWayJumpProfile : ScriptableObject
    {
        // Common, forward, backward, left, right; original ProjectFirearms Rifle clips.
        public AnimationClip[] starts;
        public AnimationClip loop, landing, landingRun;
        public float landingRunExit = .5f, landingRunBlend = .15f;
        public AnimationClip regularIdle, regularMove;
        public AnimationClip regularMoveRight;
        public AnimationClip idleTail, moveTail, rightTail, doubleJump, doubleTail;
        public float doubleToLoop = 1f, doubleEnterBlend = .08f, doubleLoopBlend = .1f;
        public int idleForceFrame = 4, moveForceFrame = 5;
        public float jumpPower = 10f, gravity = -30f, airSpeed = 5f;
        public float airAcceleration = 5f, airBraking = 2.5f;
        public float landingJumpWindow = .2f;
        public AnimationClip StartClip(bool directional, byte direction, bool right) => directional ? starts[direction] : direction == 0 ? regularIdle : right ? regularMoveRight : regularMove;
        public AnimationClip TailClip(bool directional, byte direction, bool right) => directional ? loop : direction == 0 ? idleTail : right ? rightTail : moveTail;
        public float ForceTime(bool directional, byte direction, bool right) => directional ? 0f :
            (direction == 0 ? idleForceFrame : moveForceFrame) / Mathf.Max(1f, StartClip(false, direction, right).frameRate);
        public int[] runJumpFeet;
        public bool RightFootAt(float phase) => runJumpFeet != null && runJumpFeet.Length > 0
            ? runJumpFeet[Mathf.Min(runJumpFeet.Length - 1, Mathf.FloorToInt(Mathf.Repeat(phase, 1f) * runJumpFeet.Length))] == 1
            : Mathf.Repeat(phase, 1f) >= .5f;
        [Range(.1f, 1f)] public float startToLoop = 1f;
        [Range(.1f, 1f)] public float landingExit = .8f;
        [Min(.01f)] public float blend = .25f;
    }
}
