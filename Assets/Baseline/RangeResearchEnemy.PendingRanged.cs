using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int MiniMushroomKind = 21;
        internal const int BellKind = 22;
        internal const int ClayGrenadierKind = 24;
        internal const int ChildKind = 25;
        const float PendingOrbSpeed = 9f;
        static readonly int PendingBaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock pendingTint;

        bool IsPendingRanged => kind is MiniMushroomKind or BellKind or ClayGrenadierKind or ChildKind;
        internal bool PendingOrbVisible => pendingOrb && pendingOrb.activeSelf;
        GameObject pendingOrb;
        Vector3 pendingDirection, pendingVelocity;
        float pendingExpires, pendingNextCast;

        // Whitebox adaptation: one reusable shot; the owner also owns its cancellation path.
        void CancelPendingRanged(bool stopAudio = false)
        {
            if (pendingOrb) pendingOrb.SetActive(false);
            if (stopAudio && visual && visual.AttackAudio) visual.AttackAudio.Stop();
        }

        void AdvancePendingOrb(float now)
        {
            if (!PendingOrbVisible) return;
            Vector3 start = pendingOrb.transform.position;
            float dt = Time.deltaTime;
            Vector3 end = kind == ClayGrenadierKind ? start + pendingVelocity * dt + Vector3.down * (7.5f * dt * dt) : start + pendingDirection * (PendingOrbSpeed * dt);
            if (kind == ClayGrenadierKind) pendingVelocity += Vector3.down * (15f * dt);
            Vector3 travel = end - start;
            float fraction = travel.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(target.CombatAimPoint - start, travel) / travel.sqrMagnitude)
                : 0f;
            if (now >= pendingExpires || Physics.Linecast(start, end, GameManager.environmentLayer,
                    QueryTriggerInteraction.Ignore))
                CancelPendingRanged();
            else if ((target.CombatAimPoint - (start + travel * fraction)).sqrMagnitude < .7f * .7f)
            {
                target.Server_ChangeHealthStateRaw(kind == BellKind ? 18 : 12, CharacterPart.body,
                    AttackType.explosion, body, 0);
                CancelPendingRanged();
            }
            else pendingOrb.transform.position = end;
        }

        void LaunchPendingOrb(Vector3 muzzle, Vector3 aim, float now)
        {
            if (!pendingOrb)
            {
                pendingOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pendingOrb.name = "Pending ranged orb";
                pendingOrb.transform.SetParent(transform, false);
                var collider = pendingOrb.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                pendingOrb.transform.localScale = Vector3.one * .5f;
                pendingTint ??= new MaterialPropertyBlock();
                pendingTint.SetColor(PendingBaseColor, kind is BellKind or ChildKind
                    ? new Color(1f, .6f, .1f) : new Color(.6f, 1f, .2f));
                pendingOrb.GetComponent<MeshRenderer>().SetPropertyBlock(pendingTint);
            }
            pendingOrb.transform.position = muzzle;
            pendingDirection = aim.normalized;
            pendingVelocity = aim / .65f + Vector3.up * 4.875f;
            pendingExpires = now + 3f;
            pendingOrb.SetActive(true);
        }

        void TickPendingRanged()
        {
            float now = Time.time;
            AdvancePendingOrb(now);
            Vector3 muzzle = transform.position + Vector3.up * (kind == BellKind ? .8f : kind is ClayGrenadierKind or ChildKind ? 1.6f : .9f);
            Vector3 delta = target.CombatAimPoint - muzzle;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            float distance = flat.magnitude;
            if (flat.sqrMagnitude > .001f)
            {
                body.lookInput = new Vector2(0f, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
                transform.rotation = Quaternion.Euler(0f, body.lookInput.y, 0f);
            }
            bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
            if (stage == 1 && now >= until)
            {
                LaunchPendingOrb(muzzle, delta, now);
                stage = 2;
                until = now + .65f;
            }
            if (stage == 2 && now >= until)
            {
                stage = 0;
                PlayVisual(0);
            }
            if (stage != 0) return;

            bool approach = distance > 3.5f || !visible;
            if (kind == BellKind)
            {
                if (approach && flyingController)
                    flyingController.Move(flat.normalized * (2.5f * Time.deltaTime));
                body.SetMovementInput(Vector2.zero);
            }
            else body.SetMovementInput(approach && SteerChase(visible, false, now)
                ? Vector2.up : Vector2.zero);
            PlayVisual(approach ? (byte)1 : (byte)0);
            if (approach || now < pendingNextCast || PendingOrbVisible) return;
            body.SetMovementInput(Vector2.zero);
            stage = 1;
            until = now + .45f;
            pendingNextCast = now + 2.8f;
            PlayVisual(2);
        }
    }
}