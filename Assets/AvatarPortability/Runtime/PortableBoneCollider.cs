using UnityEngine;

namespace AvatarPortability
{
    /// <summary>Independent positional collision primitive. No VRChat or Dynamic Bone dependency.</summary>
    [AddComponentMenu("Avatar Portability/Bone Collider")]
    public sealed class PortableBoneCollider : MonoBehaviour
    {
        public Transform rootTransform;
        [Tooltip("0 sphere, 1 capsule along local Y, 2 plane with local Y normal.")]
        public int shapeType;
        public bool insideBounds;
        [Min(0)] public float radius = .05f;
        [Min(0)] public float height = .2f;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;

        // A caller-scoped snapshot: another solver may move this collider later in the same frame.
        public Snapshot Capture()
        {
            if (!isActiveAndEnabled) return default(Snapshot);
            Transform space = rootTransform != null ? rootTransform : transform;
            Vector3 scale = space.lossyScale;
            float s = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            float worldRadius = Mathf.Max(0, radius) * s;
            return new Snapshot { active = true, shapeType = shapeType, insideBounds = insideBounds,
                center = space.TransformPoint(position), axis = space.rotation * rotation * Vector3.up,
                radius = worldRadius, height = height * s,
                halfSegment = Mathf.Max(0, height * s * .5f - worldRadius) };
        }

        public Vector3 ResolveFixedLength(Vector3 anchor, Vector3 point, float length, float particleRadius)
        { return Capture().ResolveFixedLength(anchor, point, length, particleRadius); }

        public Vector3 Resolve(Vector3 point, float particleRadius)
        { return Capture().Resolve(point, particleRadius); }

        public struct Snapshot
        {
            public bool active, insideBounds;
            public int shapeType;
            public Vector3 center, axis;
            public float radius, height, halfSegment;

        private Vector3 ClosestCenter(Vector3 point)
        {
            return shapeType == 1 ? center + axis * Mathf.Clamp(Vector3.Dot(point - center, axis), -halfSegment, halfSegment) : center;
        }

        private bool Satisfied(Vector3 point, float boundary, Vector3 closestCenter)
        {
            float squared = (point - closestCenter).sqrMagnitude;
            return insideBounds ? squared <= boundary * boundary : squared >= boundary * boundary;
        }

        /// <summary>Projects the direction onto the intersection of a fixed bone-length sphere and this primitive.</summary>
        public Vector3 ResolveFixedLength(Vector3 anchor, Vector3 point, float length, float particleRadius)
        {
            if (!active || length < .000001f) return point;
            Vector3 center = this.center, axis = this.axis;
            Vector3 radial = point - anchor;
            float lengthSquared = length * length;
            // Step supplies points on this sphere; repair arbitrary public-call input before testing contact.
            if (Mathf.Abs(radial.sqrMagnitude - lengthSquared) > Mathf.Max(.0000000001f, lengthSquared * .000001f))
                point = anchor + (radial.sqrMagnitude > .000000000001f ? radial.normalized : axis) * length;
            float boundary = insideBounds ? Mathf.Max(0, radius - particleRadius) : radius + particleRadius;
            if (shapeType == 2)
            {
                Vector3 normal = insideBounds ? -axis : axis;
                if (Vector3.Dot(point - center, normal) >= particleRadius) return point;
            }
            else if (Satisfied(point, boundary, ClosestCenter(point))) return point;
            Vector3 direction = (point - anchor).normalized;
            if (direction.sqrMagnitude < .5f) direction = axis;
            if (shapeType == 2)
            {
                if (insideBounds) axis = -axis;
                float minimum = (particleRadius - Vector3.Dot(anchor - center, axis)) / length;
                direction = RestrictDot(direction, axis, minimum, true);
                return anchor + direction * length;
            }
            int iterations = shapeType == 1 ? 6 : 1;
            Vector3 capsuleCenter = center;
            for (int i = 0; i < iterations; i++)
            {
                point = anchor + direction * length;
                if (shapeType == 1)
                {
                    capsuleCenter = center + axis * Mathf.Clamp(Vector3.Dot(point - center, axis), -halfSegment, halfSegment);
                }
                if (Satisfied(point, boundary, capsuleCenter)) break;
                Vector3 toCenter = capsuleCenter - anchor;
                float distance = toCenter.magnitude;
                // Concentric spheres have no directional solution if incompatible. Keep a stable direction.
                if (distance < .000001f) break;
                float threshold = (lengthSquared + distance * distance - boundary * boundary) / (2 * length * distance);
                direction = RestrictDot(direction, toCenter / distance, threshold, insideBounds);
            }
            return anchor + direction * length;
        }

        private static Vector3 RestrictDot(Vector3 direction, Vector3 axis, float threshold, bool minimum)
        {
            float dot = Vector3.Dot(direction, axis);
            if (minimum ? dot >= threshold : dot <= threshold) return direction;
            float bound = Mathf.Clamp(threshold, -1, 1);
            Vector3 tangent = direction - axis * dot;
            if (tangent.sqrMagnitude < .00000001f)
            {
                Vector3 helper = Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right;
                tangent = Vector3.Cross(axis, helper);
            }
            return (axis * bound + tangent.normalized * Mathf.Sqrt(Mathf.Max(0, 1 - bound * bound))).normalized;
        }

        public Vector3 Resolve(Vector3 point, float particleRadius)
        {
            if (!active) return point;
            Vector3 center = this.center, axis = this.axis;
            if (shapeType == 2)
            {
                if (insideBounds) axis = -axis;
                float distance = Vector3.Dot(point - center, axis) - particleRadius;
                return distance < 0 ? point - axis * distance : point;
            }
            float r = radius;
            if (shapeType == 1)
            {
                center += axis * Mathf.Clamp(Vector3.Dot(point - center, axis), -halfSegment, halfSegment);
            }
            Vector3 delta = point - center;
            float boundary = insideBounds ? Mathf.Max(0, r - particleRadius) : r + particleRadius;
            float squared = delta.sqrMagnitude;
            if (insideBounds ? squared <= boundary * boundary : squared >= boundary * boundary) return point;
            float length = Mathf.Sqrt(squared);
            return center + (length > .000001f ? delta / length : axis) * boundary;
        }
        }
    }
}
