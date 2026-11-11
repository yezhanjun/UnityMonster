using UnityEngine;

namespace Jotunnslayer
{
    // Shared curve from the Alfheim and Hyborea summon projectile assets.
    internal static class MonsterSummonArc
    {
        static readonly AnimationCurve Curve = new AnimationCurve(
            new Keyframe(0f, 0f, 4.2028694f, 4.2028694f),
            new Keyframe(.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -4.9455233f, -4.9455233f));

        internal static float Evaluate(float progress) => Curve.Evaluate(progress);
    }
}
