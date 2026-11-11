using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Local-host Fleeing adapter. TPS outside-view placement is an adaptation; original flowfield,
    // directional hit reactions, ragdoll, contact damage, loot and remote presentation are pending.
    [RequireComponent(typeof(CharacterInstance), typeof(CharacterMotor))]
    internal sealed class BatLootFleeing : MonoBehaviour
    {
        internal const int LocalBaseHealth = 140;
        const float StartRadius = 2.5f;
        const float DisableDelay = 45f;
        const float HitDuration = .6666667f;
        CharacterInstance body;
        CharacterInstance target;
        Skeleton1HVisual visual;
        Camera gameplayCamera;
        float spawnEnds;
        float waitEnds;
        float hitEnds;
        float expiresAt;
        float nextPlacementAttempt;
        float fleeOffset = 3f;
        Vector3 fleePoint;
        bool hasFleePoint;
        bool started;
        bool enteredRadius;
        bool hitting;
        bool wasWaitingOnHit;
        bool wasControlled;
        internal bool FleeingStarted => started;
        internal float ExpiresAt => expiresAt;

        internal bool Begin(CharacterInstance victim, Skeleton1HVisual presentation)
        {
            var actor = GetComponent<CharacterInstance>();
            if (body || !isActiveAndEnabled || !actor.isServer || actor.CurrentHealth <= 0 ||
                !victim || victim.CurrentHealth <= 0 || victim.Team == actor.Team || !presentation ||
                !presentation.Play(Skeleton1HVisual.Motion.Spawn)) return false;
            body = actor;
            target = victim;
            visual = presentation;
            started = enteredRadius = hitting = hasFleePoint = wasControlled = false;
            waitEnds = hitEnds = expiresAt = nextPlacementAttempt = 0f;
            fleeOffset = 3f;
            if (TryGetComponent<BotAI>(out var bot))
            {
                bot.enabled = false;
                bot.StopAllCoroutines();
            }
            var motor = GetComponent<CharacterMotor>();
            motor.WalkSpeed = motor.RunSpeed = 5.5f;
            body.MaxHealth = body.CurrentHealth = LocalBaseHealth;
            body.SetMovementInput(Vector2.zero);
            body.Server_OnDamaged += OnDamaged;
            spawnEnds = Time.time + visual.SpawnDuration;
            return true;
        }

        void OnDamaged(int health, CharacterPart part, AttackType attack, Health attacker, int force)
        {
            if (!body || health <= 0) return;
            wasWaitingOnHit = !hasFleePoint;
            hitting = true;
            hitEnds = Time.time + HitDuration;
            body.SetMovementInput(Vector2.zero);
            visual.Play(Skeleton1HVisual.Motion.Hit, restart: true);
        }

        void StartFleeing(float now)
        {
            started = true;
            expiresAt = now + DisableDelay;
            waitEnds = now;
        }

        void WaitAtPoint(float now)
        {
            hasFleePoint = false;
            enteredRadius = false;
            waitEnds = now + Random.Range(2.5f, 5f);
            body.SetMovementInput(Vector2.zero);
            visual.Play(Skeleton1HVisual.Motion.Idle);
        }

        void Update()
        {
            if (!body || !body.isServer) return;
            if (body.CurrentHealth <= 0) { enabled = false; return; }
            float now = Time.time;
            if (started && now >= expiresAt)
            {
                // Unsummon bypasses Health kill/reward callbacks, unlike a player kill.
                NetworkServer.Destroy(gameObject);
                return;
            }
            if (!target || target.CurrentHealth <= 0 || target.Team == body.Team)
            {
                body.SetMovementInput(Vector2.zero);
                return;
            }
            if (hitting)
            {
                if (now < hitEnds) return;
                hitting = false;
                // Original OnHitEnd starts the first escape unconditionally. Later idle hits
                // have a 65% chance to clear the current wait, without resetting the lifetime.
                if (!started) StartFleeing(now);
                else if (wasWaitingOnHit && Random.value <= .65f) waitEnds = now;
            }
            if (now < spawnEnds) return;
            Vector3 playerOffset = target.transform.position - body.transform.position;
            playerOffset.y = 0f;
            if (!started)
            {
                if (playerOffset.sqrMagnitude > StartRadius * StartRadius)
                {
                    visual.Play(Skeleton1HVisual.Motion.Idle);
                    return;
                }
                StartFleeing(now);
            }
            if (body.Block || SkillBattlePreview.IsStoppedByRaidControl(body, now))
            {
                wasControlled = true;
                body.SetMovementInput(Vector2.zero);
                visual.Play(Skeleton1HVisual.Motion.Idle);
                return;
            }
            if (wasControlled)
            {
                wasControlled = false;
                WaitAtPoint(now);
            }
            if (!hasFleePoint && !enteredRadius && playerOffset.sqrMagnitude <= StartRadius * StartRadius)
            {
                enteredRadius = true;
                if (Random.value <= .3f) waitEnds = now;
            }
            if (now < waitEnds || now < nextPlacementAttempt) return;
            if (!hasFleePoint)
            {
                hasFleePoint = TryChoosePoint(-playerOffset, out fleePoint);
                fleeOffset += .5f;
                if (!hasFleePoint)
                {
                    nextPlacementAttempt = now + .25f;
                    body.SetMovementInput(Vector2.zero);
                    visual.Play(Skeleton1HVisual.Motion.Idle);
                    return;
                }
            }
            Vector3 toPoint = fleePoint - body.transform.position;
            toPoint.y = 0f;
            Vector3 pointFromPlayer = fleePoint - target.transform.position;
            pointFromPlayer.y = 0f;
            // Native GetFleeDirection compares actor/player distance with point/player distance.
            if (toPoint.sqrMagnitude < .25f || playerOffset.sqrMagnitude >= pointFromPlayer.sqrMagnitude)
            {
                WaitAtPoint(now);
                return;
            }
            body.lookInput = new Vector2(0f, Mathf.Atan2(toPoint.x, toPoint.z) * Mathf.Rad2Deg);
            body.SetMovementInput(Vector2.up);
            visual.Play(Skeleton1HVisual.Motion.Walk);
        }

        bool TryChoosePoint(Vector3 away, out Vector3 point)
        {
            point = default;
            if (!gameplayCamera)
                gameplayCamera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (!gameplayCamera) return false;
            if (away.sqrMagnitude < .0001f) away = body.transform.forward;
            away.Normalize();
            const int mask = GameManager.environmentLayer;
            for (int i = 0; i < 12; i++)
            {
                // Original samples outside the top-down view with a growing offset. Here the
                // established TPS radius and ground/clearance tests replace that camera geometry.
                var direction = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * away;
                var candidate = target.transform.position + direction * (18f + fleeOffset);
                if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var ground, 12f, mask,
                        QueryTriggerInteraction.Ignore) || ground.normal.y < .65f) continue;
                candidate = ground.point + Vector3.up * .15f;
                var viewport = gameplayCamera.WorldToViewportPoint(candidate + Vector3.up);
                if (viewport.z > 0f && viewport.x >= -.05f && viewport.x <= 1.05f &&
                    viewport.y >= -.05f && viewport.y <= 1.05f) continue;
                if (Physics.CheckCapsule(candidate + Vector3.up * .5f, candidate + Vector3.up * 1.5f,
                        .45f, mask, QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(body.transform.position + Vector3.up, candidate + Vector3.up, mask,
                        QueryTriggerInteraction.Ignore)) continue;
                point = candidate;
                return true;
            }
            return false;
        }

        void OnDisable()
        {
            if (body)
            {
                body.Server_OnDamaged -= OnDamaged;
                if (body.isServer) body.SetMovementInput(Vector2.zero);
            }
            body = null;
            target = null;
            visual = null;
            gameplayCamera = null;
        }
    }
}
