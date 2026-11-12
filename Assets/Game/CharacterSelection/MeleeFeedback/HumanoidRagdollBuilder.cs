using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Builds physics for the selected FAF body; never changes the source prefab.
    public static class HumanoidRagdollBuilder
    {
        static readonly HumanBodyBones[] Bones = {
            HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };
        static readonly HumanBodyBones[] Ends = {
            HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.LastBone,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
        static readonly int[] Parents = { -1, 0, 1, 1, 3, 1, 5, 0, 7, 0, 9 };

        public static RagDoll Create(Animator source, Transform owner)
        {
            var root = new GameObject("FAF Physics Ragdoll");
            root.transform.SetPositionAndRotation(owner.position, owner.rotation);
            var model = Object.Instantiate(source.gameObject, source.transform.position, source.transform.rotation, root.transform);
            model.transform.localScale = source.transform.lossyScale;
            model.SetActive(true);
            var animator = model.GetComponent<Animator>();
            animator.enabled = false;
            animator.runtimeAnimatorController = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.gameObject.name == "CombatScratchOverlay")
                { skin.gameObject.SetActive(false); Object.Destroy(skin.gameObject); }
            var all = model.GetComponentsInChildren<Transform>(true);
            foreach (var t in all) t.gameObject.layer = 9;
            var bodies = new Rigidbody[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                var bone = animator.GetBoneTransform(Bones[i]);
                var body = bone.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = i == 0 ? 9f : i == 1 ? 7f : 2f;
                body.linearDamping = .08f; body.angularDamping = .15f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                bodies[i] = body;
                var end = Ends[i] == HumanBodyBones.LastBone ? null : animator.GetBoneTransform(Ends[i]);
                Vector3 delta = end ? bone.InverseTransformPoint(end.position) : Vector3.up * .18f;
                var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                float x = Mathf.Abs(delta.x), y = Mathf.Abs(delta.y), z = Mathf.Abs(delta.z);
                capsule.direction = x > y && x > z ? 0 : z > y ? 2 : 1;
                capsule.center = delta * .5f;
                capsule.radius = i < 2 ? .13f : i == 2 ? .085f : .055f;
                capsule.height = Mathf.Max(capsule.radius * 2, delta.magnitude * .9f);
            }
            for (int i = 1; i < bodies.Length; i++)
            {
                var joint = bodies[i].gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = bodies[Parents[i]];
                joint.enablePreprocessing = false;
                joint.axis = Vector3.right; joint.swingAxis = Vector3.up;
                joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
                joint.highTwistLimit = new SoftJointLimit { limit = 25f };
                joint.swing1Limit = new SoftJointLimit { limit = i == 4 || i == 6 || i == 8 || i == 10 ? 65f : 40f };
                joint.swing2Limit = new SoftJointLimit { limit = 25f };
            }
            var colliders = model.GetComponentsInChildren<Collider>();
            foreach (var a in colliders)
                foreach (var b in colliders) if (a != b) Physics.IgnoreCollision(a, b);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            { skin.forceRenderingOff = false; skin.updateWhenOffscreen = true; }
            var rag = root.AddComponent<RagDoll>();
            rag.ConfigureRuntimeBodies(bodies);
            rag._hips = bodies[0]; rag._head = bodies[2]; rag._footL = bodies[8]; rag._footR = bodies[10];
            rag.rigidBodies = new Transform[] { bodies[0].transform };
            rag.limbFlexors = new Transform[0];
            return rag;
        }
    }
}
