using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Server physics for the original ElusiveAntlersPickup/GravitatePickup behavior.
    // Position replication and pickup ownership belong to RangeGamemode.
    public sealed class RangeAntlerShardMotion : MonoBehaviour
    {
        Rigidbody physicsBody;
        Transform target;
        int team;

        internal Vector3 Position => physicsBody.position;
        internal bool HasTarget => target;
        internal Vector3 Velocity => physicsBody.linearVelocity;

        internal static RangeAntlerShardMotion Create(Transform parent, Vector3 position, int team)
        {
            var root = new GameObject("Rain antler server physics");
            root.layer = LayerMask.NameToLayer("item");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = 2f;
            body.angularDamping = .05f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var collision = root.AddComponent<SphereCollider>();
            collision.radius = .85f;
            collision.sharedMaterial = Resources.Load<PhysicsMaterial>("RainImported/Antlers/physmatItems");
            // Map the source pickup/world collision to TPS world geometry without blocking characters.
            collision.excludeLayers = ~GameManager.environmentLayer;
            var attraction = new GameObject("GravitationController");
            attraction.layer = LayerMask.NameToLayer("trigger");
            attraction.transform.SetParent(root.transform, false);
            var trigger = attraction.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 6f;
            trigger.excludeLayers = ~GameManager.characterLayer;
            var motion = root.AddComponent<RangeAntlerShardMotion>();
            motion.physicsBody = body;
            motion.team = team;
            return motion;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active || target) return;
            var character = other.GetComponentInParent<CharacterInstance>();
            // Source permits full-health bodies and does not require the target to own the item.
            // Death rejection is a separate check at actual pickup, not at attraction selection.
            if (character && character.Team == team) target = character.transform;
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active || !target) return;
            // Source acceleration is a velocity step per FixedUpdate, deliberately not deltaTime-scaled.
            physicsBody.linearVelocity = Vector3.MoveTowards(physicsBody.linearVelocity,
                (target.position - physicsBody.position).normalized * 40f, 5f);
        }
    }
}
