using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class ConstructSaw : MonoBehaviour
    {
        const float OutboundSpeed = 80f;
        const float OutboundDuration = .12f;
        const float HoldDuration = 1.2f;
        const float ReturnSpeed = 5f;
        internal const float OutboundDistance = OutboundSpeed * OutboundDuration;

        CharacterInstance caster;
        CharacterInstance target;
        Skeleton1HCombat combat;
        Vector3 origin;
        float elapsed;
        bool hitThisLeg;
        bool returning;

        internal static ConstructSaw Launch(CharacterInstance caster, CharacterInstance target,
            Skeleton1HCombat combat, float yaw)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            model.name = "Construct saw";
            model.transform.SetPositionAndRotation(caster.CombatAimPoint, Quaternion.Euler(0f, yaw, 0f));
            model.transform.localScale = new Vector3(2f, .08f, 2f);
            var collider = model.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var saw = model.AddComponent<ConstructSaw>();
            saw.caster = caster;
            saw.target = target;
            saw.combat = combat;
            saw.origin = model.transform.position;
            return saw;
        }

        internal void Cancel()
        {
            enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void FixedUpdate()
        {
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0)
            {
                Cancel();
                return;
            }

            elapsed += Time.fixedDeltaTime;
            Vector3 start = transform.position;
            Vector3 end = start;
            if (elapsed <= OutboundDuration + HoldDuration)
                end = origin + transform.forward * (Mathf.Min(elapsed, OutboundDuration) * OutboundSpeed);
            else
            {
                if (!returning)
                {
                    returning = true;
                    hitThisLeg = false;
                }
                end = Vector3.MoveTowards(start, caster.CombatAimPoint, ReturnSpeed * Time.fixedDeltaTime);
            }

            if (Physics.Linecast(start, end, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                Cancel();
                return;
            }

            if (!hitThisLeg && target && target.CurrentHealth > 0 && target.Team != caster.Team)
            {
                Vector3 aim = target.CombatAimPoint;
                Vector3 travel = end - start;
                float fraction = travel.sqrMagnitude > 0f
                    ? Mathf.Clamp01(Vector3.Dot(aim - start, travel) / travel.sqrMagnitude)
                    : 0f;
                Vector3 nearest = start + travel * fraction;
                if ((aim - nearest).sqrMagnitude <= 1f &&
                    !Physics.Linecast(nearest, aim, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                {
                    hitThisLeg = true;
                    int before = target.CurrentHealth;
                    target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, caster, 0);
                    if (target && target.CurrentHealth < before && combat) combat.RecordConstructSawHit();
                }
            }

            transform.position = end;
            if (returning && (end - caster.CombatAimPoint).sqrMagnitude < .001f) Cancel();
        }
    }
}
