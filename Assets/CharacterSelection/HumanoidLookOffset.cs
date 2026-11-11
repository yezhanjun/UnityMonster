using System;
using System.Collections.Generic;
using KINEMATION.FPSAnimationFramework.Runtime.Layers.LookLayer;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Retarget the FAF look profile to the Animator that also drives the TPS weapon socket.
    sealed class HumanoidLookOffset
    {
        struct Link { public Transform bone; public Vector2 limits; }
        readonly Link[] pitch, yaw;
        readonly Transform leftArm, rightArm;
        readonly Vector2 shoulderPitch;

        public HumanoidLookOffset(Animator animator, LookLayerSettings settings)
        {
            pitch = Bind(animator, settings.pitchOffsetElements);
            yaw = Bind(animator, settings.yawOffsetElements);
            leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            foreach (var link in pitch)
                if (rightArm && rightArm.IsChildOf(link.bone)) shoulderPitch += link.limits;
        }

        static Link[] Bind(Animator animator, List<LookLayerElement> elements)
        {
            var links = new List<Link>(elements.Count);
            foreach (var element in elements)
            {
                if (!Enum.TryParse(element.rigElement.name, out HumanBodyBones id)) continue;
                var bone = animator.GetBoneTransform(id);
                // Some TPS avatars have no UpperChest. Its share belongs to the preceding joint.
                if (bone) links.Add(new Link { bone = bone, limits = element.clampedAngle });
                else if (links.Count > 0)
                {
                    var last = links[links.Count - 1];
                    last.limits += element.clampedAngle;
                    links[links.Count - 1] = last;
                }
            }
            return links.ToArray();
        }

        public void Apply(Quaternion root, float yawDegrees, float pitchDegrees)
        {
            float y = Mathf.Clamp(yawDegrees / 90f, -1f, 1f);
            foreach (var link in yaw)
                Rotate(link.bone, root, Vector3.up, y * (y >= 0 ? link.limits.x : link.limits.y));
            var pitchSpace = root * Quaternion.Euler(0f, yawDegrees, 0f);
            float p = Mathf.Clamp(pitchDegrees / 90f, -1f, 1f);
            foreach (var link in pitch)
                Rotate(link.bone, pitchSpace, Vector3.right, p * (p >= 0 ? link.limits.x : link.limits.y));
            // The weapon is attached to the hand. Complete the pitch not inherited from the spine.
            float remaining = p * (90f - (p >= 0 ? shoulderPitch.x : shoulderPitch.y));
            if (leftArm) Rotate(leftArm, pitchSpace, Vector3.right, remaining);
            if (rightArm) Rotate(rightArm, pitchSpace, Vector3.right, remaining);
        }

        static void Rotate(Transform bone, Quaternion space, Vector3 axis, float degrees)
        {
            bone.rotation = KAnimationMath.RotateInSpace(space, bone.rotation, Quaternion.AngleAxis(degrees, axis), 1f);
        }
    }
}
