using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Only the character owner integrates approved movement, using the existing CharacterMotor hook.
    public sealed class RemoteRaidDash : MonoBehaviour
    {
        private CharacterInstance actor;
        private uint session, revision;
        private Vector3 direction;
        private float appliedAt, end;

        internal static void Approve(CharacterInstance character, uint token, uint version, Vector3 heading)
        {
            if (!character || !character.isOwned || character.isServer || character.CurrentHealth <= 0) return;
            var motion = character.GetComponent<RemoteRaidDash>();
            if (!motion) motion = character.gameObject.AddComponent<RemoteRaidDash>();
            if (motion.session == token && version <= motion.revision) return;
            motion.actor = character;
            motion.session = token;
            motion.revision = version;
            heading.y = 0f;
            motion.direction = heading.normalized;
            motion.appliedAt = Time.time;
            motion.end = Time.time + SkillBattlePreview.RaidDashDuration;
        }

        internal Vector3 Apply(Vector3 displacement, float now)
        {
            if (!actor || !actor.isOwned || actor.CurrentHealth <= 0)
            {
                Cancel();
                return displacement;
            }
            float next = Mathf.Min(now, end);
            if (next <= appliedAt) return displacement;
            Vector3 step = direction * ((next - appliedAt) * SkillBattlePreview.RaidDashSpeed);
            appliedAt = next;
            return new Vector3(step.x, displacement.y, step.z);
        }

        internal void Cancel() { appliedAt = end = 0f; }
        private void OnDisable() => Cancel();
    }
}
