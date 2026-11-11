using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int VerminKind = 12;
        internal const int VerminHealth = 90;
        internal const float VerminSpawnDuration = 1f;
        internal const byte VerminFirstBiteAnimation = 33;
        internal static bool IsVerminBiteAnimation(int animation) =>
            animation is >= VerminFirstBiteAnimation and <= VerminFirstBiteAnimation + 2;
        static readonly Vector3 VerminLashOffset = new Vector3(0f, 2.3f, 2.68f);
        static readonly Vector3 VerminLashHalfSize = new Vector3(2.81790925f, 1.9455325f, 2.248242f);
        static readonly Vector3 VerminWarningScale = new Vector3(2.8f, .08f, 2.6f);
        static readonly AnimationCurve VerminLashDisplacement = new AnimationCurve(
            new Keyframe(.17f, 0f, 300.1803f, float.PositiveInfinity),
            new Keyframe(.44694823f, .44623947f, 1.611274f, -2.2907414f),
            new Keyframe(.6f, 0f, -.41948193f, -.41948193f));
        const float VerminHitPause = .07f;
        Transform verminModel;
        Transform verminHead;
        static GameObject verminBiteEffectPrefab;
        GameObject verminBiteEffect;
        CharacterController verminController;
        float verminBiteStarted, verminPauseUntil;
        bool verminSampledLash;

        void TickVermin()
        {
            float now = Time.time;
            Vector3 offset = target.transform.position - transform.position;
            float distanceSquared = offset.sqrMagnitude;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
            if (stage == 0 && !rooted && distanceSquared <= 9f)
            {
                Vector2 strafe = GetGroundStrafe();
                if (strafe != Vector2.zero)
                {
                    stage = 2;
                    until = now + .2f;
                    body.SetMovementInput(strafe);
                    PlayVisual(1);
                    return;
                }
            }
            if (stage == 2 && now < until && !rooted) return;
            if (stage != 1)
            {
                if (distanceSquared > 16f && (distanceSquared <= 49f || distanceSquared > 144f))
                {
                    stage = 0;
                    bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                    bool moving = !rooted && SteerChase(visible, false, now);
                    body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
                    PlayVisual(moving ? (byte)1 : (byte)0);
                    return;
                }
                stage = 1;
                verminBiteStarted = now;
                verminPauseUntil = 0f;
                body.RainVerminPauseUntil = 0d;
                until = now + 1f;
                verminSampledLash = false;
                attackHits.Clear();
                body.SetMovementInput(Vector2.zero);
                warning.GetComponent<MeshRenderer>().enabled = true;
                warning.transform.localScale = VerminWarningScale;
                laser.enabled = false;
                material.color = Color.red;
                warning.SetActive(true);
                PlayVisual((byte)(VerminFirstBiteAnimation + Random.Range(0, 3)));
            }

            if (now < verminPauseUntil) return;
            headbuttForward = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.forward;
            warning.transform.SetPositionAndRotation(transform.position + headbuttForward * 1.3f + Vector3.up * .06f,
                Quaternion.LookRotation(headbuttForward));
            float clipTime = (now - verminBiteStarted) * .8333334f;
            if (clipTime >= .3574f && (clipTime <= .4174f || !verminSampledLash))
            {
                if (!verminSampledLash)
                {
                    PlayVerminLashEffect();
                    if (body.RainEnemyKind > 0) body.RpcRainVerminLashEffect();
                }
                verminSampledLash = true;
                if (StrikeVerminLash())
                {
                    verminPauseUntil = now + VerminHitPause;
                    verminBiteStarted += VerminHitPause;
                    until += VerminHitPause;
                    body.RainEnemyAnimationStartedAt += VerminHitPause;
                    body.RainVerminPauseUntil = Mirror.NetworkTime.time + VerminHitPause;
                }
            }
            if (clipTime >= .4174f) warning.SetActive(false);
            if (now < until) return;
            stage = 0;
            PlayVisual(0);
        }

        internal void PlayVerminLashEffect()
        {
            if (!verminHead) return;
            if (!verminBiteEffectPrefab)
                verminBiteEffectPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/DLC1/Characters/Vermin/VerminBiteEffect");
            if (!verminBiteEffectPrefab) return;
            ClearVerminLashEffect();
            verminBiteEffect = Instantiate(verminBiteEffectPrefab, verminHead, false);
            verminBiteEffect.transform.SetLocalPositionAndRotation(new Vector3(0f, .965f, 0f),
                Quaternion.Euler(-135f, 0f, 0f));
        }

        void ClearVerminLashEffect()
        {
            if (!verminBiteEffect) return;
            verminBiteEffect.SetActive(false);
            Destroy(verminBiteEffect);
            verminBiteEffect = null;
        }

        void FixedUpdate()
        {
            if (kind == AcidLarvaKind)
            {
                AdvanceAcidLarvaLeap();
                return;
            }
            if (kind != VerminKind || !body || !body.isServer || body.CurrentHealth <= 0 || body.Block || stage != 1 ||
                !verminController || !verminController.enabled || Time.time < verminPauseUntil ||
                Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body)) return;
            float displacement = VerminLashDisplacement.Evaluate(Time.time - verminBiteStarted);
            if (displacement > 0f) verminController.Move(headbuttForward * displacement);
        }

        bool StrikeVerminLash()
        {
            if (!verminModel) return false;
            bool hit = false;
            int count = Physics.OverlapBoxNonAlloc(verminModel.TransformPoint(VerminLashOffset),
                Vector3.Scale(VerminLashHalfSize, verminModel.lossyScale), attackContacts, verminModel.rotation,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var contact = attackContacts[i];
                attackContacts[i] = null;
                if (!contact) continue;
                var victim = contact.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                    !attackHits.Add(victim)) continue;
                victim.Server_ChangeHealthStateRaw(Mathf.RoundToInt(15f * 1.5f),
                    CharacterPart.body, AttackType.melee, body, 0);
                hit = true;
            }
            return hit;
        }
    }
}
