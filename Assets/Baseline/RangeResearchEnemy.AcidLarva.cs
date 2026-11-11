using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int AcidLarvaKind = 15;
        internal const int AcidLarvaHealth = 45;
        internal const float AcidLarvaSpawnDuration = 1.5f;
        internal const byte AcidLarvaLeapStartAnimation = 42, AcidLarvaLeapLoopAnimation = 43,
            AcidLarvaLandingAnimation = 44;
        const float AcidLarvaMinimumLeap = .3f;
        const float AcidLarvaBlastRadius = 7f;
        const float AcidLarvaSprintSpeed = 12f;
        static readonly Vector3 AcidLarvaGravity = Vector3.up * RainOriginalGravityY;
        static readonly Vector3 AcidLarvaWarningScale = new Vector3(14f, .08f, 14f);

        CharacterController acidLarvaController;
        Vector3 acidLarvaLeapVelocity;
        float acidLarvaLeapAge, acidLarvaNextLeap;
        bool acidLarvaHit, acidLarvaMotorSuspended, acidLarvaMotorWasEnabled;

        void TickAcidLarva()
        {
            if (stage == 1) return;
            float now = Time.time;
            if (stage == 2)
            {
                if (now < until) return;
                stage = 0;
            }

            Vector3 offset = target.transform.position - transform.position;
            float distanceSqr = offset.sqrMagnitude;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
            if (!rooted && visible && distanceSqr <= 24f * 24f && now >= acidLarvaNextLeap &&
                acidLarvaController && acidLarvaController.enabled && configuredMotor)
            {
                StartAcidLarvaLeap(now);
                return;
            }

            Vector2 input = Vector2.zero;
            if (!rooted)
            {
                if (visible && distanceSqr <= 24f * 24f && now < acidLarvaNextLeap)
                    input = GetGroundStrafe();
                else if (SteerChase(visible, distanceSqr <= 100f, now)) input = Vector2.up;
            }
            body.SetMovementInput(input);
            PlayVisual(input.sqrMagnitude > 0f ? (byte)1 : (byte)0);
        }

        void StartAcidLarvaLeap(float now)
        {
            Vector3 aim = (target.CombatAimPoint - body.CombatAimPoint).normalized;
            aim.y = Mathf.Max(.05f, aim.y);
            Vector3 forward = new Vector3(aim.x, 0f, aim.z).normalized;
            acidLarvaLeapVelocity = aim.normalized * 18f + Vector3.up * 22f + forward * 12f;
            acidLarvaLeapAge = 0f;
            acidLarvaHit = false;
            acidLarvaNextLeap = now + 5f;
            acidLarvaMotorWasEnabled = configuredMotor.enabled;
            acidLarvaMotorSuspended = true;
            configuredMotor.enabled = false;
            body.SetMovementInput(Vector2.zero);
            stage = 1;

            float flight = -2f * acidLarvaLeapVelocity.y / RainOriginalGravityY;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(acidLarvaLeapVelocity, Vector3.up);
            float brakingTime = Mathf.Min(flight * .5f, (planarVelocity - forward * AcidLarvaSprintSpeed).magnitude / 80f);
            Vector3 landing = transform.position + forward * (AcidLarvaSprintSpeed * flight) +
                (planarVelocity - forward * AcidLarvaSprintSpeed) * (.5f * brakingTime);
            if (Physics.Raycast(landing + Vector3.up, Vector3.down, out var ground, 8f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) landing = ground.point;
            warning.transform.SetPositionAndRotation(landing + Vector3.up * .06f, Quaternion.identity);
            warning.transform.localScale = AcidLarvaWarningScale;
            warning.GetComponent<MeshRenderer>().enabled = true;
            laser.enabled = false;
            material.color = Color.red;
            warning.SetActive(true);
            PlayVisual(AcidLarvaLeapStartAnimation);
        }

        void AdvanceAcidLarvaLeap()
        {
            if (!acidLarvaMotorSuspended || !body || !body.isServer || body.CurrentHealth <= 0 || body.Block ||
                !acidLarvaController || !acidLarvaController.enabled) return;
            float step = Time.fixedDeltaTime;
            acidLarvaLeapAge += step;
            if (acidLarvaLeapVelocity.y >= 0f && target)
            {
                Vector3 aim = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
                Vector3 desired = aim * AcidLarvaSprintSpeed + Vector3.up * acidLarvaLeapVelocity.y;
                acidLarvaLeapVelocity = Vector3.MoveTowards(acidLarvaLeapVelocity, desired, 80f * step);
            }
            CollisionFlags collision = acidLarvaController.Move(
                acidLarvaLeapVelocity * step + AcidLarvaGravity * (.5f * step * step));
            acidLarvaLeapVelocity += AcidLarvaGravity * step;
            acidLarvaHit |= collision != CollisionFlags.None;
            if (acidLarvaLeapAge < AcidLarvaMinimumLeap) return;
            if (!acidLarvaHit)
            {
                PlayVisual(AcidLarvaLeapLoopAnimation);
                return;
            }

            RestoreAcidLarvaMotor();
            stage = 2;
            until = Time.time + .3333333f;
            warning.SetActive(false);
            PlayVisual(AcidLarvaLandingAnimation);
            DetonateAcidLarvaLeap();
        }

        void RestoreAcidLarvaMotor()
        {
            if (!acidLarvaMotorSuspended) return;
            acidLarvaMotorSuspended = false;
            if (configuredMotor) configuredMotor.enabled = acidLarvaMotorWasEnabled;
        }

        void DetonateAcidLarvaLeap()
        {
            attackHits.Clear();
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(transform.position, AcidLarvaBlastRadius, attackContacts,
                       GameManager.characterLayer & ~GameManager.environmentLayer,
                       QueryTriggerInteraction.Collide)) == attackContacts.Length)
                System.Array.Resize(ref attackContacts, attackContacts.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var victim = attackContacts[i].GetComponentInParent<Health>();
                attackContacts[i] = null;
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                    !attackHits.Add(victim)) continue;
                victim.Server_ChangeHealthStateRaw(35, CharacterPart.body, AttackType.explosion, body, 0);
            }
            body.Server_ChangeHealthStateRaw(Mathf.RoundToInt(body.MaxHealth * .25f),
                CharacterPart.body, AttackType.explosion, body, 0, 0f);
        }
    }
}
