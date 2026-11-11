using UnityEngine;
namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int ClayBruiserKind = 23;
        internal bool ClayTracerVisible => laser && laser.enabled && warning && warning.activeSelf;
        int clayBurstRemaining;
        float clayNextShot, clayTraceUntil, clayNextBurst;
        void CancelClayBurst()
        {
            clayBurstRemaining = 0;
            if (laser) laser.enabled = false;
            if (visual && visual.AttackAudio) visual.AttackAudio.Stop();
        }
        void TickClayBurst()
        {
            float now = Time.time;
            Vector3 delta = target.CombatAimPoint - body.CombatAimPoint;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            body.lookInput = new Vector2(0f, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
            transform.rotation = Quaternion.Euler(0f, body.lookInput.y, 0f);
            if (now >= clayTraceUntil && warning) warning.SetActive(false);
            bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint, GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
            if (stage == 0)
            {
                bool approach = flat.sqrMagnitude > 16f || !visible;
                body.SetMovementInput(approach && SteerChase(visible, false, now) ? Vector2.up : Vector2.zero);
                PlayVisual(approach ? (byte)1 : (byte)0);
                if (approach || now < clayNextBurst) return;
                body.SetMovementInput(Vector2.zero);
                stage = 1; until = now + .7f; clayNextBurst = now + 3f;
                PlayVisual(2);
            }
            if (stage == 1 && now >= until)
            {
                stage = 2; clayBurstRemaining = 5; clayNextShot = now;
            }
            if (stage == 2 && clayBurstRemaining > 0 && now >= clayNextShot)
            {
                Vector3 muzzle = body.CombatAimPoint + transform.right * .5f + transform.forward * .5f;
                Vector3 end = target.CombatAimPoint;
                bool blocked = Physics.Linecast(muzzle, end, out var hit, GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                warning.SetActive(true); warning.GetComponent<MeshRenderer>().enabled = false;
                material.color = Color.yellow; laser.startColor = laser.endColor = Color.yellow;
                laser.enabled = true; laser.widthMultiplier = .14f; laser.SetPosition(0, muzzle); laser.SetPosition(1, blocked ? hit.point : end);
                clayTraceUntil = now + .09f;
                if (!blocked) target.Server_ChangeHealthStateRaw(3, CharacterPart.body, AttackType.explosion, body, 0);
                if (visual && visual.AttackAudio) visual.AttackAudio.Play();
                clayBurstRemaining--; clayNextShot = now + .12f;
                if (clayBurstRemaining == 0) { stage = 3; until = now + .5f; }
            }
            if (stage == 3 && now >= until) { stage = 0; PlayVisual(0); }
        }
    }
}