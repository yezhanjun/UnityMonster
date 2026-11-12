using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [System.Serializable]
    public sealed class FourWayRollMotion
    {
        public AnimationClip clip;
        public AnimationCurve x, z;
        public float multiplier = 1f;
        public Vector3 Position(float time) => new Vector3(x.Evaluate(time) - x.Evaluate(0), 0,
            z.Evaluate(time) - z.Evaluate(0)) * multiplier;
    }

    [CreateAssetMenu(menuName = "TPS/Four Way Roll")]
    public sealed class FourWayRollProfile : ScriptableObject
    {
        // Forward, backward, left, right, as in the source ProjectFirearms Rifle.
        public FourWayRollMotion[] directions;
        [Range(.1f, 1f)] public float exitNormalized = .9f;
        [Min(0f)] public float recovery = 0f;
        [Min(.01f)] public float blendDuration = .25f;
        [Range(0f, 1f)] public float moveUnlockNormalized = .4f;
        public float forwardUnlockNormalized = .2f;
        [Range(0f, 1f)] public float reenterNormalized = .4f;
        [Min(.01f)] public float moveBlendDuration = .1f;
        public float Duration(int direction) => directions[direction].clip.length * exitNormalized;
    }
}
