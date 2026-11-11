using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string MindEvolution = "active_seer_D_evolve";
        private int mindRank;
        private float mindReady;
        private static readonly float[] MindSlowChance = { 0.2f, 0.35f, 0.5f };
        private const float MindKnockbackDistance = 0.5f;

        private bool CanChooseMind() => AllowsRaidClass(RaidClass.Seeress) && PendingRewards > 0 && mindRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[mindRank] && (mindRank < 3 || progress.IsLearned(MindEvolution));

        internal bool TryChooseMind()
        {
            if (!CanChooseMind())
                return false;

            mindRank++;
            rewardsSpent++;
            return true;
        }

        private void TryMindPulse(float now)
        {
            if (mindRank == 0 || now < mindReady)
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, MissileRange);
            if (targets.Count == 0)
                return;

            if (mindRank != 4 || !progress.IsLearned(MindEvolution))
            {
                int rank = Mathf.Min(mindRank, 3);
                mindReady = now + ScaleSkillCooldown(3f - rank * 0.5f);
                DealMindBlast(targets[Random.Range(0, targets.Count)], origin, 14 + rank * 2, now, MindSlowChance[rank - 1]);
                return;
            }

            mindReady = now + ScaleSkillCooldown(4.5f);
            // Model the eight fast pulses as radial lanes, with one hit per target.
            float facing = caster.transform.eulerAngles.y;
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - origin;
                float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg - facing;
                float lane = Mathf.Round(angle / 45f) * 45f;
                Vector3 direction = Quaternion.Euler(0, lane + facing, 0) * Vector3.forward;
                Vector3 lateral = offset - direction * Vector3.Dot(offset, direction);
                if (lateral.sqrMagnitude <= 0.5625f)
                    DealMindBlast(target, origin, 20, now, 0.2f);
            }
        }

        private void DealMindBlast(Health target, Vector3 origin, int damage, float now, float slowChance)
        {
            int before = target.CurrentHealth;
            Deal(target, ScaleClassSkillDamage(damage));
            if (!target || target.CurrentHealth <= 0 || target.CurrentHealth >= before)
                return;

            if (Random.value < slowChance)
            {
                float expires = now + 2f;
                ApplyRaidSkillSlow(target, expires, attackSlowUntil: expires);
                SyncRaidAttackSlow(target, now);
            }

            Vector3 away = target.transform.position - origin;
            away.y = 0f;
            Vector3 displacement = away.normalized * MindKnockbackDistance;
            if (displacement == Vector3.zero)
                return;

            if (target is CharacterInstance character)
            {
                if (character.TryGetComponent<CharacterMotor>(out var motor))
                    motor.ApplyServerRaidKnockback(displacement);
            }
            else if (target.TryGetComponent<CharacterController>(out var controller) && controller.enabled)
                controller.Move(displacement);
            else
                target.transform.position += displacement;
        }
    }
}
