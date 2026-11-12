using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed class HeroRootMotionLibrary : ScriptableObject
    {
        [System.Serializable]
        public sealed class Track
        {
            public AnimationClip clip;
            public AnimationCurve x, z;
            public float plantedScale = 1f;
            public Vector3 Position(float time, bool loop)
            {
                float length = clip.length;
                int cycles = loop ? Mathf.FloorToInt(time / length) : 0;
                float t = loop ? Mathf.Repeat(time, length) : Mathf.Clamp(time, 0, length);
                return new Vector3(x.Evaluate(t) + cycles * x.Evaluate(length), 0,
                    z.Evaluate(t) + cycles * z.Evaluate(length)) * plantedScale;
            }
        }
        public Track[] tracks;
        public Track Find(AnimationClip clip)
        {
            if (tracks != null) for (int i = 0; i < tracks.Length; i++)
                if (tracks[i].clip == clip) return tracks[i];
            return null;
        }
    }
}
