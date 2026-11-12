using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [CreateAssetMenu(menuName = "TPS/Wendy Rifle Presentation")]
    public sealed class WendyRifleProfile : ScriptableObject
    {
        public AnimationClip idle, aimIdle, sprint, take, fire, reload;
        public AnimationClip run, runStop;
        public AnimationClip runStopLeft, runStopRight;
        public int[] runStopFeet;
        public bool oppositeFootReinput = true;
        [Min(.01f)] public float runStopBlend = .15f, runStopReinputBlend = .15f;
        [Range(.1f, 1f)] public float runStopExit = 1f;
        public bool StopRightFootAt(float phase) => runStopFeet != null && runStopFeet.Length > 0
            ? runStopFeet[Mathf.Min(runStopFeet.Length - 1, Mathf.FloorToInt(Mathf.Repeat(phase, 1f) * runStopFeet.Length))] == 1
            : Mathf.Repeat(phase, 1f) < .4f || Mathf.Repeat(phase, 1f) >= .8f;
        public AnimationClip runArcLeft, runArcRight, sprintArcLeft, sprintArcRight;
        public AnimationClip sprintTurnLeft, sprintTurnRight, sprintStopLeft, sprintStopRight;
        public int[] sprintStopFeet, rollStopFeet;
        public float sprintStopBlend = .1f, sprintStopStart = .1f, sprintStopExit = 1f, sprintStopIdleBlend = .8f;
        public bool SprintStopRightFootAt(float phase, bool fromRoll)
        {
            var feet = fromRoll ? rollStopFeet : sprintStopFeet;
            float t = fromRoll ? Mathf.Clamp01(phase) : Mathf.Repeat(phase, 1f);
            return feet == null || feet.Length == 0 ? (fromRoll ? t < .5f : true) :
                feet[Mathf.Min(feet.Length - 1, Mathf.FloorToInt(t * feet.Length))] == 1;
        }
        public float arcSensitivity = 1.5f, arcLerpSpeed = 3f;
        public float turnThreshold = 130f, turnRotationStart = 7f/33f, turnRotationDuration = 12f/33f;
        public float turnExit = .7f, turnEnterBlend = .15f, turnExitBlend = .2f;
        public AnimationClip reloadEmpty;
        public string weaponResource = "FpsRifle", modelChild = "Assault Rifle_1";
        public AvatarMask armsMask;
        // Forward, forward-left, left, back-left, back, back-right, right, forward-right.
        public AnimationClip[] hipMove, aimMove;
        public Vector3 handPosition = new Vector3(-.0018120939f, .02901669f, -.0007832991f);
        public Vector3 handEuler = new Vector3(276.027f, 354.53543f, 91.48657f);
        public float handScale = .74922043f;
        [Min(0f)] public float hipFirePoseHold = 1f;
        [Tooltip("Time to settle approximately 95% of a locomotion transition.")]
        [Min(.01f)] public float locomotionBlend = .16f;
        [Min(.01f)] public float reloadBlend = .12f;
        [Min(.01f)] public float reloadBlendOut = .22f;
        [Min(.01f)] public float fireBlend = .035f;
    }
}
