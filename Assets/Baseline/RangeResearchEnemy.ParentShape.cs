using System;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const float ParentBodyRadius = 1.5f;
        internal const float ParentBodyHeight = 8.60000038f;
        internal const float ParentSlopeLimit = 55f;
        internal const float ParentStepHeight = 4f;
        // Original body bottom is -0.3 - 8.6/2 = -4.6. TPS roots are at the feet.
        internal const float ParentModelFootOffset = -.19f; // ModelBase -4.79 + 4.6.
        [Serializable] sealed class ParentShapeData { public ParentHurtboxData[] hitboxes; }
        [Serializable] sealed class ParentHurtboxData
        {
            public string path;
            public Vector3 position, scale, center;
            public Quaternion rotation;
            public bool sphere, main;
            public float radius, height;
            public int direction;
        }
        readonly List<Collider> parentAvatarHitboxes = new List<Collider>(16);
        readonly List<Collider> parentOriginalHitboxes = new List<Collider>(13);
        readonly List<HitBox> parentHitboxBindings = new List<HitBox>(13);
        readonly List<GameObject> parentHurtboxObjects = new List<GameObject>(13);
        CharacterController parentShapeController;
        Vector3 parentPreviousCenter;
        float parentPreviousHeight, parentPreviousRadius;
        float parentPreviousSlope, parentPreviousStep;
        bool parentShapeConfigured;

        void ConfigureParentShape()
        {
            var model = visual.FindModelBone("mdlParent");
            var asset = Resources.Load<TextAsset>("RainActors/RoR2/Base/Characters/Parent/ParentHitboxes");
            if (!model || !asset) throw new InvalidOperationException("Parent original collision asset missing");
            var data = JsonUtility.FromJson<ParentShapeData>(asset.text);
            if (data.hitboxes.Length != 13) throw new InvalidOperationException("Parent requires 13 original hurtboxes");
            // Validate every binding before changing the existing avatar.
            foreach (var entry in data.hitboxes)
                if (!model.Find(entry.path.Substring(0, entry.path.LastIndexOf('/'))))
                    throw new InvalidOperationException("Parent hurtbox bone missing: " + entry.path);
            foreach (var hitbox in GetComponentsInChildren<HitBox>(true))
                if (hitbox.TryGetComponent<Collider>(out var collider) && collider.enabled)
                {
                    parentAvatarHitboxes.Add(collider);
                    collider.enabled = false;
                }
            parentShapeController = GetComponent<CharacterController>();
            parentPreviousCenter = parentShapeController.center;
            parentPreviousRadius = parentShapeController.radius;
            parentPreviousHeight = parentShapeController.height;
            parentPreviousSlope = parentShapeController.slopeLimit;
            parentPreviousStep = parentShapeController.stepOffset;
            parentShapeController.radius = ParentBodyRadius;
            parentShapeController.height = ParentBodyHeight;
            parentShapeController.center = Vector3.up * (ParentBodyHeight * .5f);
            parentShapeController.slopeLimit = ParentSlopeLimit;
            parentShapeController.stepOffset = ParentStepHeight;
            model.localPosition = Vector3.up * ParentModelFootOffset;
            foreach (var entry in data.hitboxes)
            {
                int separator = entry.path.LastIndexOf('/');
                var hurtboxObject = new GameObject(entry.path.Substring(separator + 1));
                parentHurtboxObjects.Add(hurtboxObject);
                var bone = hurtboxObject.transform;
                bone.SetParent(model.Find(entry.path.Substring(0, separator)), false);
                bone.localPosition = entry.position;
                bone.localRotation = entry.rotation;
                bone.localScale = entry.scale;
                bone.gameObject.layer = 8; // Project hitbox layer; original RoR2 uses layer 12.
                Collider collider;
                if (entry.sphere)
                {
                    var sphere = bone.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = entry.radius;
                    sphere.center = entry.center;
                    collider = sphere;
                }
                else
                {
                    var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                    capsule.radius = entry.radius;
                    capsule.height = entry.height;
                    capsule.direction = entry.direction;
                    capsule.center = entry.center;
                    collider = capsule;
                }
                var binding = bone.gameObject.AddComponent<HitBox>();
                binding._health = body;
                binding.part = CharacterPart.body; // All original damageModifier values are Normal.
                parentOriginalHitboxes.Add(collider);
                parentHitboxBindings.Add(binding);
                if (entry.main) body.RainMonsterAimCollider = collider;
            }
            body.Client_OnHealthStateChanged += OnParentShapeHealthChanged;
            parentShapeConfigured = true;
        }

        void OnParentShapeHealthChanged(int health, CharacterPart part, AttackType type, Health attacker)
        {
            if (health > 0) return;
            CancelParentAction();
            SetParentHurtboxesEnabled(false);
        }

        void SetParentHurtboxesEnabled(bool enabled)
        {
            foreach (var collider in parentOriginalHitboxes)
                if (collider) collider.enabled = enabled;
        }

        void RestoreParentShape()
        {
            if (!parentShapeConfigured) return;
            parentShapeConfigured = false;
            if (body)
            {
                body.Client_OnHealthStateChanged -= OnParentShapeHealthChanged;
                if (parentOriginalHitboxes.Contains(body.RainMonsterAimCollider)) body.RainMonsterAimCollider = null;
            }
            foreach (var collider in parentOriginalHitboxes)
                if (collider) { collider.enabled = false; Destroy(collider); }
            foreach (var binding in parentHitboxBindings) if (binding) Destroy(binding);
            foreach (var hurtboxObject in parentHurtboxObjects) if (hurtboxObject) Destroy(hurtboxObject);
            if (!OwnsCharacter && body && body.CurrentHealth > 0)
            {
                foreach (var collider in parentAvatarHitboxes) if (collider) collider.enabled = true;
                if (parentShapeController)
                {
                    // Restore the step before shrinking the capsule; a 4m step cannot fit a human capsule.
                    parentShapeController.stepOffset = parentPreviousStep;
                    parentShapeController.slopeLimit = parentPreviousSlope;
                    parentShapeController.radius = parentPreviousRadius;
                    parentShapeController.height = parentPreviousHeight;
                    parentShapeController.center = parentPreviousCenter;
                }
            }
            parentOriginalHitboxes.Clear();
            parentHitboxBindings.Clear();
            parentHurtboxObjects.Clear();
            parentAvatarHitboxes.Clear();
        }
    }
}
