using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace AvatarPortability
{
    /// <summary>Original fixed-step particle bone solver; source settings are an approximation, not VRC emulation.</summary>
    [DefaultExecutionOrder(10000)]
    [AddComponentMenu("Avatar Portability/Bone Physics")]
    public sealed class PortableBonePhysics : MonoBehaviour, IBoneSimulationOwnership
    {
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AvatarPortability.Physics.Update");
        private static readonly ProfilerMarker LateMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AvatarPortability.Physics.LateUpdate");
        private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AvatarPortability.Physics.Rebuild");
        private static readonly ProfilerMarker StepMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AvatarPortability.Physics.Step");
        public Transform rootTransform;
        public List<Transform> ignoreTransforms = new List<Transform>();
        public bool ignoreOtherPhysBones = true;
        public Vector3 endpointPosition;
        public int version = 1, multiChildType, integrationType, immobileType;
        [Range(0, 1)] public float pull = .2f, spring = .6f, stiffness = .1f, immobile;
        public float gravity, gravityFalloff, radius;
        public AnimationCurve pullCurve, springCurve, stiffnessCurve, gravityCurve, gravityFalloffCurve,
            immobileCurve, radiusCurve;
        public bool allowCollision = true;
        public List<PortableBoneCollider> colliders = new List<PortableBoneCollider>();
        [Tooltip("0 free, 1 cone, 2 hinge, 3 polar pitch/yaw approximation.")]
        public int limitType;
        public float maxAngleX = 45, maxAngleZ = 45;
        public AnimationCurve maxAngleXCurve, maxAngleZCurve;
        public Vector3 limitRotation;
        public AnimationCurve limitRotationXCurve, limitRotationYCurve, limitRotationZCurve;
        public float maxStretch, maxSquish, stretchMotion;
        public AnimationCurve maxStretchCurve, maxSquishCurve, stretchMotionCurve;
        public bool isAnimated, resetWhenDisabled;
        [Min(15)] public float simulationRate = 60;
        [Range(1, 8)] public int maxSubsteps = 4;
        public bool useUnscaledTime;
        [Min(0)] public float teleportDistance = 1;

        private sealed class Particle
        {
            public Transform bone;
            public int parent;
            public int firstChild = -1, nextSibling = -1;
            public float depth, length;
            public bool anchored;
            public bool writesRotation, writesPosition;
            public Vector3 localPosition, bindPosition, endpoint, position, previous, target;
            public Quaternion localRotation, bindRotation, targetRotation;
        }
        private readonly List<Particle> particles = new List<Particle>();
        private readonly HashSet<Transform> otherRoots = new HashSet<Transform>();
        private float accumulator;
        private Vector3 lastRoot;
        private Matrix4x4 lastMotionMatrix;
        private Vector3 lastScenePosition;
        private bool initialized, applied;
        private PortableBoneCollider.Snapshot[] colliderSnapshots = new PortableBoneCollider.Snapshot[0];
        private int colliderCount;
        private Vector3 stepGravity;
        private float stepRadiusScale;

        public bool OwnsBone(Transform bone)
        {
            if (bone == null) return false;
            if (!initialized) Rebuild();
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].bone != bone) continue;
                return particles[i].writesRotation || particles[i].writesPosition;
            }
            return false;
        }

        private static float Value(float value, AnimationCurve curve, float t)
        {
            return value * (curve != null && curve.length > 0 ? curve.Evaluate(t) : 1);
        }

        private void OnEnable() { Rebuild(); }
        private void OnDisable()
        {
            RestorePose(resetWhenDisabled);
            particles.Clear(); initialized = false; applied = false; accumulator = 0;
        }
        private void OnDestroy() { RestorePose(false); }

        /// <summary>Call after changing the hierarchy, roots, exclusions, or endpoint configuration.</summary>
        public void Rebuild()
        {
            using (RebuildMarker.Auto()) RebuildCore();
        }

        private void RebuildCore()
        {
            RestorePose(false);
            particles.Clear(); accumulator = 0; applied = false;
            Transform root = rootTransform != null ? rootTransform : transform;
            otherRoots.Clear();
            if (ignoreOtherPhysBones)
            {
                PortableBonePhysics[] others = root.root.GetComponentsInChildren<PortableBonePhysics>(true);
                for (int i = 0; i < others.Length; i++)
                    if (others[i] != this) otherRoots.Add(others[i].rootTransform != null ? others[i].rootTransform : others[i].transform);
            }
            Add(root, -1, 0);
            for (int i = particles.Count - 1; i > 0; i--)
            {
                Particle p = particles[i], parent = particles[p.parent];
                p.nextSibling = parent.firstChild; parent.firstChild = i;
            }
            for (int i = 0; i < particles.Count; i++)
            {
                int children = 0;
                for (int j = particles[i].firstChild; j >= 0; j = particles[j].nextSibling) children++;
                particles[i].writesRotation = children == 1 || (children > 1 && multiChildType != 0);
                particles[i].writesPosition = i > 0 && (maxStretch > 0 || maxSquish > 0);
                if (children < 2 || multiChildType == 2) continue;
                bool first = true;
                for (int j = particles[i].firstChild; j >= 0; j = particles[j].nextSibling)
                {
                    particles[j].anchored = multiChildType == 0 || !first;
                    first = false;
                }
            }
            float longest = .000001f;
            for (int i = 0; i < particles.Count; i++) longest = Mathf.Max(longest, particles[i].depth);
            for (int i = 0; i < particles.Count; i++) particles[i].depth /= longest;
            initialized = particles.Count > 0;
            lastRoot = root.position;
            lastMotionMatrix = root.parent != null ? root.parent.localToWorldMatrix : Matrix4x4.identity;
            lastScenePosition = root.root.position;
            CaptureTargets(); ResetSimulation();
        }

        private void Add(Transform bone, int parent, float depth)
        {
            if (bone == null || (parent >= 0 && ignoreTransforms.Contains(bone))) return;
            if (parent >= 0 && ignoreOtherPhysBones && otherRoots.Contains(bone)) return;
            float length = parent < 0 ? 0 : Vector3.Distance(bone.position, particles[parent].bone.position);
            int index = particles.Count;
            particles.Add(new Particle { bone = bone, parent = parent, depth = depth + length, length = length,
                localPosition = bone.localPosition, bindPosition = bone.localPosition,
                localRotation = bone.localRotation, bindRotation = bone.localRotation });
            for (int i = 0; i < bone.childCount; i++) Add(bone.GetChild(i), index, depth + length);
            bool hasChild = particles.Count > index + 1;
            if (!hasChild && endpointPosition.sqrMagnitude > .000000001f)
            {
                float endLength = bone.TransformVector(endpointPosition).magnitude;
                particles.Add(new Particle { parent = index, endpoint = endpointPosition,
                    depth = depth + length + endLength, length = endLength });
            }
        }

        private void Update() { using (UpdateMarker.Auto()) RestorePose(false); }

        private void RestorePose(bool bind)
        {
            if (!applied && !bind) return;
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                if (p.bone == null) continue;
                if (p.writesPosition) p.bone.localPosition = bind ? p.bindPosition : p.localPosition;
                if (p.writesRotation) p.bone.localRotation = bind ? p.bindRotation : p.localRotation;
            }
            applied = false;
        }

        private void CaptureTargets()
        {
            if (!isAnimated)
                for (int i = 0; i < particles.Count; i++)
                    if (particles[i].bone != null)
                    {
                        if (particles[i].writesPosition) particles[i].bone.localPosition = particles[i].localPosition;
                        if (particles[i].writesRotation) particles[i].bone.localRotation = particles[i].localRotation;
                    }
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                if (p.bone != null)
                {
                    if (isAnimated) { p.localPosition = p.bone.localPosition; p.localRotation = p.bone.localRotation; }
                    p.target = p.bone.position; p.targetRotation = p.bone.rotation;
                }
                else if (p.parent >= 0)
                {
                    Particle parent = particles[p.parent];
                    p.target = parent.bone != null ? parent.bone.TransformPoint(p.endpoint) : parent.target;
                    p.targetRotation = parent.targetRotation;
                }
            }
        }

        /// <summary>Restore the animation input before manually evaluating an Animator or PlayableGraph.</summary>
        /// <remarks>Call before evaluation, then ResetSimulation afterwards for a discontinuous pose change.</remarks>
        public void RestoreAnimationPose()
        {
            RestorePose(false);
        }

        public void ResetSimulation()
        {
            RestorePose(false);
            CaptureTargets();
            for (int i = 0; i < particles.Count; i++) particles[i].position = particles[i].previous = particles[i].target;
            // A reset establishes a new motion origin, including teleport detection.
            // Do not carry the previous animation's reference transform into the new pose.
            if (particles.Count > 0 && particles[0].bone != null)
            {
                Transform root = particles[0].bone;
                lastRoot = particles[0].target;
                lastMotionMatrix = root.parent != null ? root.parent.localToWorldMatrix : Matrix4x4.identity;
                lastScenePosition = root.root.position;
            }
            accumulator = 0;
        }

        /// <summary>Manual full update for previews/tests. Do not also advance automatically in the same frame.</summary>
        public void Simulate(float dt)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (!initialized) Rebuild();
            if (!ValidateHierarchy()) return;
            RestorePose(false);
            CaptureTargets();
            MoveWithReference();
            PrepareStepInputs();
            float step = 1 / Mathf.Max(15, simulationRate);
            int count = Mathf.Min(Mathf.Max(1, maxSubsteps), Mathf.CeilToInt(Mathf.Max(0, dt) / step));
            for (int i = 0; i < count; i++) Step(Mathf.Min(step, dt - i * step));
            ApplyPose();
        }

        private void LateUpdate()
        {
            using (LateMarker.Auto()) LateUpdateCore();
        }

        private void LateUpdateCore()
        {
            if (!initialized || !ValidateHierarchy()) return;
            // Update restores last frame's simulation before Animator runs; capture current animation in LateUpdate.
            CaptureTargets();
            Vector3 root = particles[0].target;
            Vector3 movement = root - lastRoot;
            lastRoot = root;
            if (teleportDistance > 0 && movement.sqrMagnitude > teleportDistance * teleportDistance) ResetSimulation();
            MoveWithReference();
            PrepareStepInputs();
            float step = 1 / Mathf.Max(15, simulationRate);
            accumulator = Mathf.Min(accumulator + (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime), step * Mathf.Max(1, maxSubsteps));
            int steps = 0;
            while (accumulator >= step && steps++ < maxSubsteps) { Step(step); accumulator -= step; }
            ApplyPose();
        }

        private void MoveWithReference()
        {
            if (particles.Count == 0) return;
            Transform root = particles[0].bone;
            if (root == null) return;
            Matrix4x4 matrix = root.parent != null ? root.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 delta = matrix * lastMotionMatrix.inverse;
            Vector3 sceneMovement = root.root.position - lastScenePosition;
            for (int i = 1; i < particles.Count; i++)
            {
                Particle p = particles[i];
                float weight = Mathf.Clamp01(Value(immobile, immobileCurve, p.depth));
                if (immobileType == 0)
                {
                    p.position = Vector3.Lerp(p.position, delta.MultiplyPoint3x4(p.position), weight);
                    p.previous = Vector3.Lerp(p.previous, delta.MultiplyPoint3x4(p.previous), weight);
                }
                else { p.position += sceneMovement * weight; p.previous += sceneMovement * weight; }
            }
            lastMotionMatrix = matrix; lastScenePosition = root.root.position;
        }

        private bool ValidateHierarchy()
        {
            if (particles.Count > 0 && particles[0].bone != null) return true;
            RestorePose(false);
            particles.Clear(); initialized = false; applied = false; accumulator = 0;
            return false;
        }

        private void Step(float dt)
        {
            using (StepMarker.Auto()) StepCore(dt);
        }

        private void PrepareStepInputs()
        {
            // The solver does not write any transforms between this snapshot and ApplyPose.
            // Refresh per invocation (not globally per frame), preserving cross-component update order.
            colliderCount = colliders.Count;
            if (colliderSnapshots.Length < colliderCount)
                System.Array.Resize(ref colliderSnapshots, colliderCount);
            for (int i = 0; i < colliderCount; i++)
                colliderSnapshots[i] = colliders[i] != null ? colliders[i].Capture() : default(PortableBoneCollider.Snapshot);
            Vector3 gravityVector = Physics.gravity;
            stepGravity = gravityVector.sqrMagnitude > .000001f ? gravityVector.normalized : Vector3.down;
            stepRadiusScale = colliderCount > 0 ? MaxScale(rootTransform != null ? rootTransform : transform) : 1;
        }

        private void StepCore(float dt)
        {
            particles[0].position = particles[0].previous = particles[0].target;
            float frame = dt * 60;
            for (int i = 1; i < particles.Count; i++)
            {
                Particle p = particles[i], parent = particles[p.parent];
                if (p.anchored)
                {
                    p.position = p.previous = parent.position + p.target - parent.target;
                    continue;
                }
                float t = p.depth;
                Vector3 old = p.position;
                float retention = Mathf.Pow(Mathf.Clamp01(Value(spring, springCurve, t)), frame);
                Vector3 velocity = (p.position - p.previous) * retention;
                Vector3 target = parent.position + p.target - parent.target;
                float attraction = 1 - Mathf.Pow(1 - Mathf.Clamp01(Value(pull, pullCurve, t)), frame);
                Vector3 down = stepGravity;
                float falloff = Mathf.Clamp01(Value(gravityFalloff, gravityFalloffCurve, t));
                float gravityValue = Value(gravity, gravityCurve, t);
                float deviation = Vector3.Angle(p.position - parent.position, target - parent.position) / 180;
                float gravityWeight = 1 - falloff * (1 - deviation);
                Vector3 previousDirection = (p.position - parent.position).normalized;
                if (version >= 1)
                {
                    Vector3 gravityDirection = gravityValue < 0 ? -down : down;
                    Vector3 restDirection = (target - parent.position).normalized;
                    target = parent.position + Vector3.Slerp(restDirection, gravityDirection, Mathf.Clamp01(Mathf.Abs(gravityValue) * gravityWeight)) * (target - parent.position).magnitude;
                }
                if (integrationType == 0)
                    p.position += velocity + (target - p.position) * attraction;
                else
                {
                    // Advanced mode treats pull as a spring acceleration rather than direct position relaxation.
                    velocity += (target - p.position) * Mathf.Clamp01(Value(pull, pullCurve, t)) * frame * frame;
                    p.position += velocity;
                    float stiff = Mathf.Clamp01(Value(stiffness, stiffnessCurve, t));
                    if (version >= 1)
                    {
                        float distanceBeforeStiffness = (p.position - parent.position).magnitude;
                        Vector3 blendedDirection = Vector3.Slerp((p.position - parent.position).normalized, previousDirection, stiff);
                        p.position = parent.position + blendedDirection * distanceBeforeStiffness;
                    }
                    else p.position = Vector3.Lerp(p.position, target, stiff);
                }
                if (version == 0)
                    p.position += down * (gravityValue * gravityWeight * 9.81f * dt * dt);
                p.previous = old;
                float length = (p.target - parent.target).magnitude;
                float stretch = Mathf.Max(0, Value(maxStretch, maxStretchCurve, t));
                float squish = Mathf.Clamp01(Value(maxSquish, maxSquishCurve, t));
                float motion = Mathf.Clamp01(Value(stretchMotion, stretchMotionCurve, t));
                float distance = (p.position - parent.position).magnitude;
                float desired = Mathf.Clamp(Mathf.Lerp(length, distance, version >= 1 ? motion : 0), length * (1 - squish), length * (1 + stretch));
                Vector3 direction = p.position - parent.position;
                if (direction.sqrMagnitude < .000000001f) direction = p.target - parent.target;
                direction = ConstrainDirection(p, parent, direction.normalized);
                p.position = parent.position + direction * desired;
                // Explicit colliders always apply. allowCollision describes VRC global permission, archived only.
                if (colliderCount > 0)
                {
                    float r = Mathf.Max(0, Value(radius, radiusCurve, t)) * stepRadiusScale;
                    bool stretchable = version >= 1 && (stretch > 0 || squish > 0);
                    for (int iteration = 0; iteration < 4; iteration++)
                    {
                        Vector3 beforeCollisionSweep = p.position;
                        for (int j = 0; j < colliderCount; j++)
                            if (colliderSnapshots[j].active)
                                p.position = stretchable ? colliderSnapshots[j].Resolve(p.position, r)
                                    : colliderSnapshots[j].ResolveFixedLength(parent.position, p.position, length, r);
                        // Exact fixed point: replaying the same ordered deterministic sweep cannot change it.
                        if ((p.position - beforeCollisionSweep).sqrMagnitude == 0) break;
                    }
                    if (version >= 1 && (stretch > 0 || squish > 0))
                    {
                        Vector3 collided = p.position - parent.position;
                        float permitted = Mathf.Clamp(collided.magnitude, length * (1 - squish), length * (1 + stretch));
                        if (collided.sqrMagnitude > .000000001f) p.position = parent.position + collided.normalized * permitted;
                    }
                    // Collision has priority when exact length/angle constraints conflict.
                }
            }
        }

        private static float MaxScale(Transform bone)
        {
            Vector3 s = bone.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        private Vector3 ConstrainDirection(Particle p, Particle parent, Vector3 direction)
        {
            if (limitType == 0) return direction;
            Vector3 rest = (p.target - parent.target).normalized;
            Quaternion basis = Quaternion.FromToRotation(Vector3.up, Quaternion.Inverse(parent.targetRotation) * rest);
            Vector3 angles = new Vector3(Value(limitRotation.x, limitRotationXCurve, p.depth),
                Value(limitRotation.y, limitRotationYCurve, p.depth), Value(limitRotation.z, limitRotationZCurve, p.depth));
            Quaternion frame = parent.targetRotation * basis * Quaternion.Euler(angles);
            Vector3 local = Quaternion.Inverse(frame) * direction;
            float xLimit = Mathf.Clamp(Mathf.Abs(Value(maxAngleX, maxAngleXCurve, p.depth)), 0, 180);
            if (limitType == 1)
            {
                float angle = Vector3.Angle(Vector3.up, local);
                if (angle > xLimit) local = Vector3.RotateTowards(Vector3.up, local, xLimit * Mathf.Deg2Rad, 0);
            }
            else if (limitType == 2)
            {
                float angle = Mathf.Clamp(Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg, -xLimit, xLimit);
                local = Quaternion.AngleAxis(angle, Vector3.right) * Vector3.up;
            }
            else
            {
                float zLimit = Mathf.Clamp(Mathf.Abs(Value(maxAngleZ, maxAngleZCurve, p.depth)), 0, 180);
                float x = Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
                float z = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
                x = Mathf.Clamp(x, -xLimit, xLimit);
                z = Mathf.Clamp(z, -zLimit, zLimit);
                if (xLimit < .0001f) x = 0;
                if (zLimit < .0001f) z = 0;
                local = Quaternion.Euler(x, 0, -z) * Vector3.up;
            }
            return frame * local.normalized;
        }

        private void ApplyPose()
        {
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                if (p.bone == null) continue;
                Vector3 rest = Vector3.zero, simulated = Vector3.zero;
                int count = 0;
                for (int j = p.firstChild; j >= 0; j = particles[j].nextSibling)
                {
                    Particle child = particles[j];
                    rest += (child.target - p.target).normalized;
                    simulated += (child.position - p.position).normalized;
                    count++;
                    if (multiChildType == 1) break;
                }
                // Ignore mode leaves branching joints in their animated pose; average mode rotates towards the mean.
                if (count > 1 && multiChildType == 0) continue;
                if (rest.sqrMagnitude > .000000001f && simulated.sqrMagnitude > .000000001f)
                    p.bone.rotation = Quaternion.FromToRotation(rest, simulated) * p.targetRotation;
                if (p.parent >= 0 && (Value(maxStretch, maxStretchCurve, p.depth) > 0 || Value(maxSquish, maxSquishCurve, p.depth) > 0))
                    p.bone.position = p.position;
            }
            applied = true;
        }
    }
}
