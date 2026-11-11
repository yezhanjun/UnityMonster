using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int ImpKind = 10;
        internal const int ImpHealth = 140;
        internal const float ImpSpawnDuration = 1.5f;
        internal const float ImpSlashDuration = 1.2f;
        internal const float ImpBlinkDuration = .2f;
        internal const byte ImpBlinkAnimation = 27, ImpBlinkExitAnimation = 28;
        static readonly Vector3 ImpSwipeHalfSize = new Vector3(.65f, .9425f, .65f);
        static readonly Vector3 ImpWarningScale = new Vector3(2.8f, .08f, 2.6f);
        static GameObject impBlinkEffectPrefab;
        static GameObject impSwipePrefab, impSwipeImpactPrefab;
        static readonly Quaternion ImpRightSwipeRotation = new Quaternion(-.7129572f, .021876605f, .29127312f, .63747424f);
        static readonly Quaternion ImpLeftSwipeRotation = new Quaternion(.29718533f, .59708166f, -.7314996f, .14171354f);
        Transform impRightHand, impLeftHand, impModel;
        float impNextSlash, impSlashStarted;
        int impSwipes;
        float impNextBlink, impBlinkStarted, impBlinkExitUntil;
        Vector3 impBlinkStart, impBlinkDestination;
        bool impBlinking, impMotorWasEnabled, impControllerWasEnabled, impWasInvulnerable;
        CharacterController impController;
        List<Collider> impDisabledHitboxes;

        void TickImp()
        {
            float now = Time.time;
            if (stage == 2)
            {
                transform.position = Vector3.Lerp(impBlinkStart, impBlinkDestination,
                    Mathf.Clamp01((now - impBlinkStarted) / ImpBlinkDuration));
                if (now >= until)
                {
                    SetImpBlinking(false);
                    EmitImpBlinkEffect();
                    stage = 0;
                    impBlinkExitUntil = now + 1.1833333f;
                    PlayVisual(ImpBlinkExitAnimation);
                }
                return;
            }
            Vector3 offset = target.transform.position - transform.position;
            float distanceSquared = offset.sqrMagnitude;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);

            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
            if (stage == 0)
            {
                if (now < impNextSlash || distanceSquared > 81f)
                {
                    bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                    bool blinkRange = (distanceSquared >= 64f && distanceSquared <= 625f) ||
                        (distanceSquared >= 2500f && visible);
                    if (!rooted && blinkRange && now >= impNextBlink && TryStartImpBlink(now, offset))
                        return;
                    bool moving = !rooted && distanceSquared > 1f && SteerChase(visible, false, now);
                    body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
                    PlayVisual(now < impBlinkExitUntil ? ImpBlinkExitAnimation : moving ? (byte)1 : (byte)0);
                    return;
                }
                stage = 1;
                impSlashStarted = now;
                until = now + ImpSlashDuration;
                impNextSlash = until + 1f;
                impSwipes = 0;
                warning.SetActive(true);
                warning.GetComponent<MeshRenderer>().enabled = true;
                warning.transform.localScale = ImpWarningScale;
                laser.enabled = false;
                material.color = Color.red;
                PlayVisual(26);
            }

            body.SetMovementInput(!rooted && distanceSquared > 1f ? Vector2.up * .5f : Vector2.zero);
            Vector3 forward = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.forward;
            headbuttForward = forward;
            warning.transform.SetPositionAndRotation(transform.position + forward * 1.3f + Vector3.up * .06f,
                Quaternion.LookRotation(forward));

            float age = now - impSlashStarted;
            if (impSwipes == 0 && age >= ImpSlashDuration * (.5586265f / 1.45f))
            {
                StrikeImpSwipe(impRightHand, right: true);
                impSwipes++;
            }
            if (impSwipes == 1 && age >= ImpSlashDuration * (.79179233f / 1.45f))
            {
                StrikeImpSwipe(impLeftHand, right: false);
                impSwipes++;
                warning.SetActive(false);
            }
            if (now < until) return;
            stage = 0;
            PlayVisual(0);
        }

        bool TryStartImpBlink(float now, Vector3 offset)
        {
            offset.y = 0f;
            if (offset.sqrMagnitude < .01f) return false;
            Vector3 desired = transform.position + offset.normalized * 9f;
            if (!NavMesh.SamplePosition(desired, out var landing, 2f, NavMesh.AllAreas)) return false;
            const float radius = .45f;
            Vector3 feet = landing.position + Vector3.up * .1f;
            if (Physics.CheckCapsule(feet + Vector3.up * radius, feet + Vector3.up * (2f - radius),
                radius, GameManager.characterLayer, QueryTriggerInteraction.Ignore)) return false;

            stage = 2;
            impBlinkStarted = now;
            until = now + ImpBlinkDuration;
            impNextBlink = now + 5f;
            impBlinkStart = transform.position;
            impBlinkDestination = feet;
            body.SetMovementInput(Vector2.zero);
            warning.SetActive(false);
            SetImpBlinking(true);
            EmitImpBlinkEffect();
            PlayVisual(ImpBlinkAnimation);
            return true;
        }

        void EmitImpBlinkEffect()
        {
            Vector3 origin = body.CombatAimPoint;
            Vector3 heading = impBlinkDestination - impBlinkStart;
            Quaternion rotation = heading.sqrMagnitude > .001f ? Quaternion.LookRotation(heading) : transform.rotation;
            PlayImpBlinkEffect(origin, rotation);
            if (body.RainEnemyKind > 0) body.RpcRainImpBlinkEffect(origin, rotation);
        }

        internal static void PlayImpBlinkEffect(Vector3 origin, Quaternion rotation)
        {
            if (!impBlinkEffectPrefab)
                impBlinkEffectPrefab = Resources.Load<GameObject>("RainActors/RoR2/Base/Characters/Imp/ImpBlinkEffect");
            if (!impBlinkEffectPrefab) return;
            var effect = Instantiate(impBlinkEffectPrefab, origin, rotation);
            var flash = effect.GetComponentInChildren<Light>();
            if (flash) Destroy(flash.gameObject, .2f);
            Destroy(effect, 3f);
        }

        void SetImpBlinking(bool blinking)
        {
            if (impBlinking == blinking) return;
            impBlinking = blinking;
            if (visual) visual.SetModelVisible(!blinking);
            if (blinking)
            {
                impDisabledHitboxes ??= new List<Collider>();
                foreach (var hitbox in GetComponentsInChildren<HitBox>())
                    if (hitbox.TryGetComponent<Collider>(out var hitCollider) && hitCollider.enabled)
                    {
                        hitCollider.enabled = false;
                        impDisabledHitboxes.Add(hitCollider);
                    }
            }
            else
            {
                foreach (var hitCollider in impDisabledHitboxes)
                    if (hitCollider) hitCollider.enabled = true;
                impDisabledHitboxes.Clear();
            }
            if (!body || !body.isServer) return;
            if (blinking)
            {
                impController = GetComponent<CharacterController>();
                impControllerWasEnabled = impController && impController.enabled;
                impMotorWasEnabled = configuredMotor && configuredMotor.enabled;
                impWasInvulnerable = body.RangeInvulnerable;
                if (configuredMotor) configuredMotor.enabled = false;
                if (impController) impController.enabled = false;
                body.RangeInvulnerable = true;
            }
            else
            {
                if (impController) impController.enabled = impControllerWasEnabled;
                if (configuredMotor) configuredMotor.enabled = impMotorWasEnabled;
                body.RangeInvulnerable = impWasInvulnerable;
            }
        }

        void StrikeImpSwipe(Transform hand, bool right)
        {
            PlayImpSwipe(right);
            if (body.RainEnemyKind > 0) body.RpcRainImpSwipe(right);
            if (!hand) return;
            attackHits.Clear();
            Vector3 center = hand.TransformPoint(0f, .28f, 0f);
            int count = Physics.OverlapBoxNonAlloc(center,
                Vector3.Scale(ImpSwipeHalfSize, hand.lossyScale), attackContacts, hand.rotation,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var contact = attackContacts[i];
                attackContacts[i] = null;
                if (!contact) continue;
                var victim = contact.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                    !attackHits.Add(victim)) continue;
                Vector3 impact = contact.ClosestPoint(center);
                Quaternion rotation = Quaternion.LookRotation(-headbuttForward);
                victim.Server_ChangeHealthStateRaw(15, CharacterPart.body, AttackType.melee, body, 0, .4f);
                PlayImpSwipeImpact(impact, rotation);
                if (body.RainEnemyKind > 0) body.RpcRainImpSwipeImpact(impact, rotation);
            }
        }

        internal void PlayImpSwipe(bool right)
        {
            if (!impModel) return;
            if (!impSwipePrefab)
                impSwipePrefab = Resources.Load<GameObject>("RainActors/RoR2/Base/Characters/Imp/ImpSwipeEffect");
            if (!impSwipePrefab) return;
            var effect = Instantiate(impSwipePrefab, impModel, false);
            effect.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.008f, 0f),
                right ? ImpRightSwipeRotation : ImpLeftSwipeRotation);
            Destroy(effect, 2f);
        }

        internal static void PlayImpSwipeImpact(Vector3 position, Quaternion rotation)
        {
            if (!impSwipeImpactPrefab)
                impSwipeImpactPrefab = Resources.Load<GameObject>("RainActors/RoR2/Base/Characters/Imp/ImpactImpSwipe");
            if (impSwipeImpactPrefab) Destroy(Instantiate(impSwipeImpactPrefab, position, rotation), 2f);
        }
    }
}
