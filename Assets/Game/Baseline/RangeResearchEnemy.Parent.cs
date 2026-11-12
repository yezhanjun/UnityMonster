using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int ParentKind = 17;
        internal const int ParentHealth = 585;
        internal const float ParentSpawnDuration = 3f;
        internal const float ParentSlamDuration = 3f;
        internal const byte ParentSlamAnimation = 45;
        internal const float ParentSlamRadius = 10f;
        // First crossing of the original compressed Slam.hitBoxActive polynomial, scaled to the state.
        internal const float ParentSlamHitTime = .78980257f;
        internal const float ParentBlinkDuration = .15f;
        internal const float ParentOriginalForce = 6000f;
        static readonly Vector3 ParentWarningScale = new Vector3(20f, .08f, 20f);
        Transform parentSlamZone;
        float parentSlamStarted, parentNextSlam, parentNextBlink, parentBlinkStarted;
        bool parentSlamHit, parentBlinking;
        double parentObservedAnimationStart = double.NaN;
        Vector3 parentBlinkStart, parentBlinkDestination;
        CharacterController parentController;
        CharacterMotor parentBlinkMotor;
        bool parentControllerWasEnabled, parentMotorWasEnabled, parentWasInvulnerable;
        readonly List<Collider> parentDisabledHitboxes = new List<Collider>(16);
        readonly Dictionary<Health, Vector3> parentBlastOffsets = new Dictionary<Health, Vector3>(16);

        void TickParent()
        {
            float now = Time.time;
            Vector3 offset = target.transform.position - transform.position;
            float distance = offset.magnitude;
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);

            if (parentBlinking)
            {
                transform.position = Vector3.Lerp(parentBlinkStart, parentBlinkDestination,
                    (now - parentBlinkStarted) / ParentBlinkDuration);
                if (now - parentBlinkStarted >= ParentBlinkDuration)
                {
                    SetParentBlinking(false);
                    body.lookInput = new Vector2(0, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
                }
            }

            if (stage == 1)
            {
                visual.EvaluateCurrentPose(now - parentSlamStarted);
                if (!parentSlamHit && now - parentSlamStarted >= ParentSlamHitTime)
                {
                    // Set the latch before damage callbacks can cancel or destroy this actor.
                    parentSlamHit = true;
                    warning.SetActive(false);
                    StrikeParentSlam();
                }
                if (!body || body.CurrentHealth <= 0) return;
                if (now - parentSlamStarted >= ParentSlamDuration)
                {
                    stage = 0;
                    PlayVisual(0);
                }
            }

            // Master driver order: ready primary within 18m, then low-health utility at 20–200m.
            bool slamDriver = distance <= 18f && now >= parentNextSlam;
            if (stage == 0 && slamDriver && !parentBlinking)
            {
                body.lookInput = new Vector2(0, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
                transform.rotation = Quaternion.Euler(0, body.lookInput.y, 0);
                stage = 1;
                parentSlamStarted = now;
                parentNextSlam = now + 2f;
                parentSlamHit = false;
                body.SetMovementInput(Vector2.zero);
                PlayVisual(ParentSlamAnimation);
                visual.EvaluateCurrentPose(0);
                warning.GetComponent<MeshRenderer>().enabled = true;
                laser.enabled = false;
                warning.transform.localScale = ParentWarningScale;
                warning.SetActive(true);
            }
            else if (!slamDriver && !parentBlinking && !rooted && distance >= 20f && distance <= 200f &&
                body.CurrentHealth <= body.MaxHealth * .8f && now >= parentNextBlink)
            {
                TryStartParentBlink(now, offset);
            }

            if (stage == 1 && !parentSlamHit)
            {
                Vector3 center = parentSlamZone.position;
                warning.transform.position = new Vector3(center.x, transform.position.y + .05f, center.z);
            }
            if (parentBlinking) return;
            body.lookInput = new Vector2(0, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            // GroundSlam does not root the motor. The ready primary driver ignores the node graph;
            // during recharge, the Path driver keeps chasing while Body continues its animation.
            bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
            bool moving = !rooted && distance > 1f && SteerChase(visible, slamDriver, now);
            body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
            if (stage != 0) return;
            PlayVisual(moving ? (byte)1 : (byte)0);
        }

        void StrikeParentSlam()
        {
            if (!parentSlamZone) return;
            Vector3 center = parentSlamZone.position;
            parentBlastOffsets.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, ParentSlamRadius, attackContacts,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            // Full buffer is ambiguous: repeat with a complete query rather than silently dropping targets.
            Collider[] contacts = count == attackContacts.Length
                ? Physics.OverlapSphere(center, ParentSlamRadius, GameManager.characterLayer, QueryTriggerInteraction.Collide)
                : attackContacts;
            if (contacts != attackContacts) count = contacts.Length;
            for (int i = 0; i < count; i++)
            {
                var contact = contacts[i];
                contacts[i] = null;
                if (!contact) continue;
                var victim = contact.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0) continue;
                Vector3 hitOffset = contact.ClosestPoint(center) - center;
                if (!parentBlastOffsets.TryGetValue(victim, out Vector3 best) || hitOffset.sqrMagnitude < best.sqrMagnitude)
                    parentBlastOffsets[victim] = hitOffset;
            }
            foreach (var entry in parentBlastOffsets)
            {
                if (!entry.Key || entry.Key.CurrentHealth <= 0) continue;
                float falloff = 1f - Mathf.Clamp01(entry.Value.magnitude / ParentSlamRadius);
                int damage = Mathf.RoundToInt(16f * 4f * falloff);
                if (damage > 0)
                    entry.Key.ServerApplyRainBlast(damage, body,
                        RainCharacterImpulseMath.BlastImpulse(entry.Value, ParentSlamRadius, ParentOriginalForce));
            }
        }

        bool TryStartParentBlink(float now, Vector3 offset)
        {
            if (offset.sqrMagnitude < .001f) return false;
            Vector3 desired = transform.position + offset.normalized * 30f;
            if (!NavMesh.SamplePosition(desired, out var landing, 8f, NavMesh.AllAreas)) return false;
            // Preserve the actor's current ground-to-transform offset, as the original footPosition does.
            float footOffset = .15f;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var ground, 8f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                footOffset = transform.position.y - ground.point.y;
            Vector3 destination = landing.position + Vector3.up * footOffset;
            if (Physics.CheckCapsule(destination + Vector3.up * ParentBodyRadius,
                destination + Vector3.up * (ParentBodyHeight - ParentBodyRadius),
                ParentBodyRadius, GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) return false;
            parentBlinkStart = transform.position;
            parentBlinkDestination = destination;
            parentBlinkStarted = now;
            parentNextBlink = now + 4f;
            body.SetMovementInput(Vector2.zero);
            body.RainParentBlinkUntil = Mirror.NetworkTime.time + ParentBlinkDuration;
            SetParentBlinking(true);
            return true;
        }

        void SetParentBlinking(bool active)
        {
            if (active == parentBlinking) return;
            parentBlinking = active;
            // Invisibility must not disable the Animator: Body and BlinkMachine can overlap.
            if (visual) visual.SetModelRenderingVisible(!active);
            if (active)
            {
                foreach (var hitbox in GetComponentsInChildren<HitBox>(true))
                    if (hitbox.TryGetComponent<Collider>(out var collider) && collider.enabled)
                    {
                        parentDisabledHitboxes.Add(collider);
                        collider.enabled = false;
                    }
            }
            else
            {
                foreach (var collider in parentDisabledHitboxes)
                    if (collider) collider.enabled = body && body.CurrentHealth > 0;
                parentDisabledHitboxes.Clear();
            }
            if (active)
            {
                parentController = GetComponent<CharacterController>();
                parentBlinkMotor = configuredMotor ? configuredMotor : GetComponent<CharacterMotor>();
                parentControllerWasEnabled = parentController && parentController.enabled;
                parentMotorWasEnabled = parentBlinkMotor && parentBlinkMotor.enabled;
                if (parentController) parentController.enabled = false;
                if (parentBlinkMotor)
                {
                    parentBlinkMotor.ResetRainMonsterVelocity();
                    parentBlinkMotor.enabled = false;
                }
                if (body && body.isServer)
                {
                    parentWasInvulnerable = body.RangeInvulnerable;
                    body.RangeInvulnerable = true;
                }
            }
            else
            {
                if (parentController) parentController.enabled = parentControllerWasEnabled && body && body.CurrentHealth > 0;
                if (parentBlinkMotor) parentBlinkMotor.enabled = parentMotorWasEnabled && body && body.CurrentHealth > 0;
                if (body && body.isServer)
                {
                    body.RangeInvulnerable = parentWasInvulnerable;
                    body.RainParentBlinkUntil = 0;
                }
            }
        }

        void CancelParentAction()
        {
            SetParentBlinking(false);
            parentSlamHit = true;
            if (body && body.isServer) body.RainParentBlinkUntil = 0;
        }
    }
}
