using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [RequireComponent(typeof(CharacterMotor))]
    internal sealed class MonsterPassThrough : MonoBehaviour
    {
        const float ContactRadius = .85f;
        float travelDistance;
        const float TravelTimeout = 4f;

        CharacterInstance body;
        CharacterInstance target;
        Skeleton1HVisual visual;
        Vector3 origin;
        Vector3 previous;
        float yaw;
        float startsAt;
        bool moving;
        bool hit;
        bool followsTarget;
        bool gmControlled;
        bool gmActive;
        float gmContactRadius;
        LineRenderer chargeWarning;
        Material chargeMaterial;
        internal AudioSource ChargeAudio { get; private set; }

        internal bool Begin(CharacterInstance actor, CharacterInstance victim, Skeleton1HVisual presentation,
            Vector3 forward, Skeleton1HSpawn.Kind kind)
        {
            if (!actor || !actor.isServer || !victim || !presentation ||
                !presentation.Play(Skeleton1HVisual.Motion.Spawn)) return false;

            body = actor;
            target = victim;
            visual = presentation;
            followsTarget = kind == Skeleton1HSpawn.Kind.GhostRedFollowing;
            if (kind is Skeleton1HSpawn.Kind.Ghost or Skeleton1HSpawn.Kind.GhostSvart or Skeleton1HSpawn.Kind.GhostSkull ||
                Skeleton1HSpawn.IsRedGhost(kind))
            {
                var clip = Resources.Load<AudioClip>("Jotunnslayer/Skeleton1H/SwingPlaceholder");
                if (clip)
                {
                    ChargeAudio = gameObject.AddComponent<AudioSource>();
                    ChargeAudio.playOnAwake = false;
                    ChargeAudio.spatialBlend = 1f;
                    ChargeAudio.maxDistance = 24f;
                    ChargeAudio.volume = .7f;
                    ChargeAudio.clip = clip;
                }
            }
            var (speed, health, distance) = kind switch
            {
                Skeleton1HSpawn.Kind.GhostRedFollowing => (4f, 36, 0f),
                Skeleton1HSpawn.Kind.AlfheimSpider => (13f, 14, 24f),
                Skeleton1HSpawn.Kind.AlfheimSpiderVeteran => (13f, 140, 24f),
                Skeleton1HSpawn.Kind.AlfheimElfMelee or Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane
                    or Skeleton1HSpawn.Kind.AlfheimElfRanged => (10f, 36, 24f),
                _ => (7.2f, 14, 18f)
            };
            body.MaxHealth = body.CurrentHealth = health;
            if (TryGetComponent<BotAI>(out var bot))
            {
                bot.enabled = false;
                bot.StopAllCoroutines();
            }

            var motor = GetComponent<CharacterMotor>();
            motor.WalkSpeed = motor.RunSpeed = speed;
            travelDistance = distance;
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            body.lookInput = new Vector2(0f, yaw);
            body.SetMovementInput(Vector2.zero);
            origin = previous = body.transform.position;
            startsAt = Time.time + presentation.SpawnDuration;
            return true;
        }

        internal void SetGmCombat(bool active)
        {
            if (gmControlled && gmActive == active) return;
            gmControlled = true;
            gmActive = active;
            body.SetMovementInput(Vector2.zero);
            if (!active || !target)
            {
                gmActive = false;
                if (ChargeAudio) ChargeAudio.Stop();
                if (chargeWarning) chargeWarning.enabled = false;
                visual.Play(Skeleton1HVisual.Motion.Idle);
                return;
            }

            origin = previous = body.transform.position;
            Vector3 offset = target.transform.position - origin;
            if (offset.x * offset.x + offset.z * offset.z > .01f)
                yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            body.lookInput = new Vector2(0f, yaw);
            var ownController = GetComponent<CharacterController>();
            var targetController = target.GetComponent<CharacterController>();
            gmContactRadius = ownController && targetController
                ? Mathf.Max(ContactRadius, ownController.radius + targetController.radius + .1f)
                : ContactRadius;
            moving = hit = false;
            startsAt = Time.time + .6f;
            visual.Play(Skeleton1HVisual.Motion.Spawn);
            ShowChargeWarning();
        }

        void ShowChargeWarning()
        {
            if (!chargeWarning)
            {
                var shader = Shader.Find("Sprites/Default");
                if (!shader) return;
                chargeMaterial = new Material(shader);
                var indicator = new GameObject("Ghost charge warning");
                indicator.transform.SetParent(transform);
                chargeWarning = indicator.AddComponent<LineRenderer>();
                chargeWarning.sharedMaterial = chargeMaterial;
                chargeWarning.widthMultiplier = .12f;
                chargeWarning.startColor = chargeWarning.endColor = new Color(1f, .22f, .06f);
                chargeWarning.positionCount = 2;
            }
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 start = origin + Vector3.up * .16f;
            chargeWarning.SetPosition(0, start);
            chargeWarning.SetPosition(1, start + direction * 6f);
            chargeWarning.enabled = true;
        }

        void Update()
        {
            if (!body || !body.isServer) return;
            if (body.CurrentHealth <= 0 || !target || target.CurrentHealth <= 0 ||
                (!followsTarget && !gmControlled && (Time.time >= startsAt + TravelTimeout ||
                 (body.transform.position - origin).sqrMagnitude >= travelDistance * travelDistance)))
            {
                enabled = false;
                NetworkServer.Destroy(gameObject);
                return;
            }

            if (gmControlled)
            {
                if (!gmActive) return;
                if (!followsTarget && (Time.time >= startsAt + TravelTimeout ||
                    (body.transform.position - origin).sqrMagnitude >= travelDistance * travelDistance))
                {
                    SetGmCombat(false);
                    return;
                }
            }
            if (Time.time < startsAt) return;
            if (!moving)
            {
                moving = true;
                if (ChargeAudio) ChargeAudio.Play();
                if (chargeWarning) chargeWarning.enabled = false;
                visual.Play(Skeleton1HVisual.Motion.Walk);
            }

            Vector3 current = body.transform.position;
            if ((!hit || followsTarget) && !body.Block && target.Team != body.Team)
            {
                bool contact = TouchesTarget(previous, current);
                if (!hit && contact)
                    target.Server_ChangeHealthStateRaw(5, CharacterPart.body, AttackType.melee, body, 0);
                hit = followsTarget ? contact : hit || contact;
            }

            if (followsTarget)
            {
                Vector3 offset = target.transform.position - current;
                if (offset.x * offset.x + offset.z * offset.z > .01f)
                    yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            }
            previous = current;
            body.lookInput = new Vector2(0f, yaw);
            body.SetMovementInput(body.Block ? Vector2.zero : Vector2.up);
        }

        void OnDestroy()
        {
            if (chargeMaterial) Destroy(chargeMaterial);
        }

        bool TouchesTarget(Vector3 from, Vector3 to)
        {
            Vector3 offset = target.transform.position - from;
            Vector3 travel = to - from;
            if (gmControlled)
            {
                if (Mathf.Abs(offset.y) > 1.1f) return false;
                offset.y = 0f;
                travel.y = 0f;
            }
            float lengthSquared = travel.sqrMagnitude;
            if (!followsTarget && lengthSquared < .0001f) return false;

            float fraction = lengthSquared < .0001f ? 0f : Mathf.Clamp01(Vector3.Dot(offset, travel) / lengthSquared);
            Vector3 closest = Vector3.LerpUnclamped(from, to, fraction);
            float radius = gmControlled ? gmContactRadius : ContactRadius;
            return (offset - travel * fraction).sqrMagnitude <= radius * radius &&
                !Physics.Linecast(closest + Vector3.up, target.CombatAimPoint, GameManager.environmentLayer,
                    QueryTriggerInteraction.Ignore);
        }
    }
}
