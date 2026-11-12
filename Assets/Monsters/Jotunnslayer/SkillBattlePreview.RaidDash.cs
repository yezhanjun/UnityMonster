using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        internal const float RaidDashDuration = 0.22f;
        internal const float RaidDashSpeed = 18f;
        internal Vector3 ActiveRaidDashDirection => raidDashDirection;
        internal int DashCharges => raidDashCharges;
        private float raidDashCooldown, raidDashReady, raidDashEnd, raidDashAppliedAt;
        private int raidDashCharges, raidDashCapacity;
        private float RaidDashRechargePeriod => raidDashCooldown * (RaidoEvolved ? 0.85f : 1f);
        private float ghostwalkDuration, ghostwalkUntil;
        private Vector3 raidDashDirection;
        private static readonly int[] SoulShardsLevels = { 1, 8, 15 };
        private int soulShardsRank, spectralArrowsRemaining;
        private float spectralArrowAt;
        private static readonly int[] ThorPassiveLevels = { 1, 8, 15 };
        private int thorPotentialRank, thunderDischargeRank;

        private bool CanChooseThorPassive(int rank, int maxRank, string id) =>
            HasLiveCaster && PendingRewards > 0 && rank < maxRank &&
            raidLevel >= ThorPassiveLevels[rank] && (rank != 0 || progress.IsLearned(id));

        private bool CanChooseThorPotential() => CanChooseThorPassive(thorPotentialRank, 2, "passive_thor_H");

        internal bool TryChooseThorPotential()
        {
            if (!CanChooseThorPotential())
                return false;

            thorPotentialRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseThunderDischarge() => CanChooseThorPassive(thunderDischargeRank, 3, "passive_thor_I");

        internal bool TryChooseThunderDischarge()
        {
            if (!CanChooseThunderDischarge())
                return false;

            thunderDischargeRank++;
            rewardsSpent++;
            return true;
        }

        private void TriggerThunderDischarge(float now)
        {
            if (thunderDischargeRank == 0)
                return;

            CollectTargets(caster.transform.position, 2f);
            foreach (var target in targets)
            {
                int before = target.CurrentHealth;
                DealThorDamage(target, 6 + thunderDischargeRank * 4);
                if (target && target.CurrentHealth > 0 && target.CurrentHealth < before &&
                    Random.value < thunderDischargeRank * 0.1f)
                    StopSkillTarget(target, now + 3f);
            }
        }

        private void ResetRaidDash(SkillProgress profile)
        {
            raidDashReady = 0f;
            raidDashCapacity = LiveMatch && SelectedRaidClass == RaidClass.Rogue ? 2 : 1;
            raidDashCharges = raidDashCapacity;
            soulShardsRank = 0;
            RefreshRaidDashPermanentStats(profile);
        }

        // Read again on the next dash request: refunds/relearning keep the current body,
        // active dash and already scheduled recharge, but must affect newly started effects.
        private void RefreshRaidDashPermanentStats(SkillProgress profile)
        {
            raidDashCooldown = 4f;
            ghostwalkDuration = 0f;
            if (!LiveMatch)
                return;

            if (SelectedRaidClass == RaidClass.Ranger)
            {
                ghostwalkDuration = 1f;
                if (profile != null && profile.IsLearned("virtue_ranger_03_a")) ghostwalkDuration += 0.3f;
                if (profile != null && profile.IsLearned("virtue_ranger_03_b")) ghostwalkDuration += 0.3f;
            }
            else if (SelectedRaidClass == RaidClass.Valkyrie && profile != null)
            {
                int reduction = 0;
                if (profile.IsLearned("virtue_valkyrie_01_d")) reduction = 14;
                else if (profile.IsLearned("virtue_valkyrie_01_c")) reduction = 8;
                else if (profile.IsLearned("virtue_valkyrie_01_b")) reduction = 4;
                else if (profile.IsLearned("virtue_valkyrie_01_a")) reduction = 2;
                raidDashCooldown *= (100 - reduction) / 100f;
            }
        }

        internal bool RequestRaidDash(CharacterInstance actor)
        {
            if (!actor || actor != caster || !actor.IsGrounded || actor.Block ||
                !actor.TryGetComponent<CharacterMotor>(out var motor) || motor.UsingAttackRootMotion)
                return false;

            Vector2 input = actor.movementInput;
            Vector3 direction = input.sqrMagnitude > 0.01f
                ? actor.transform.TransformDirection(new Vector3(input.x, 0f, input.y))
                : actor.transform.forward;
            return TryStartRaidDash(direction, Time.time);
        }

        internal bool TryStartRaidDash(Vector3 direction, float now)
        {
            if (!LiveMatch || !HasLiveCaster || now < raidDashEnd)
                return false;

            RefreshRaidDashPermanentStats(progress);
            RechargeRaidDash(now);
            direction.y = 0f;
            if (raidDashCharges == 0 || direction.sqrMagnitude < 0.0001f)
                return false;

            raidDashDirection = direction.normalized;
            if (raidDashCharges == raidDashCapacity)
                raidDashReady = now + RaidDashRechargePeriod;
            raidDashCharges--;
            raidDashAppliedAt = now;
            raidDashEnd = now + RaidDashDuration;
            TriggerThunderDischarge(now);
            if (ghostwalkDuration > 0f)
            {
                ghostwalkUntil = now + ghostwalkDuration;
                if (soulShardsRank > 0)
                {
                    spectralArrowsRemaining = 1 + 2 * soulShardsRank;
                    spectralArrowAt = now;
                }
            }
            return true;
        }

        private void RechargeRaidDash(float now)
        {
            while (raidDashCharges < raidDashCapacity && now >= raidDashReady)
            {
                raidDashCharges++;
                raidDashReady += RaidDashRechargePeriod;
            }
        }

        private bool CanChooseSoulShards() => LiveMatch && SelectedRaidClass == RaidClass.Ranger &&
            PendingRewards > 0 && soulShardsRank < SoulShardsLevels.Length &&
            raidLevel >= SoulShardsLevels[soulShardsRank] &&
            (soulShardsRank != 0 || progress.IsLearned("passive_ranger_P"));

        internal bool TryChooseSoulShards()
        {
            if (!CanChooseSoulShards())
                return false;

            soulShardsRank++;
            rewardsSpent++;
            return true;
        }

        private void TickSoulShards(float now)
        {
            while (spectralArrowsRemaining > 0 && now >= spectralArrowAt)
            {
                spectralArrowsRemaining--;
                spectralArrowAt += 0.1f;
                Vector3 origin = caster.transform.position;
                CollectTargets(origin, 12f);
                Health closest = null;
                float nearest = float.PositiveInfinity;
                foreach (var target in targets)
                {
                    float distance = (target.transform.position - origin).sqrMagnitude;
                    if (distance >= nearest)
                        continue;

                    closest = target;
                    nearest = distance;
                }

                if (closest)
                    DealRangerSkillDamage(closest, 10);
            }
        }

        internal static Vector3 ApplyRaidDash(Health actor, Vector3 displacement, float now)
        {
            if (!RaidStatOwners.TryGetValue(actor, out var owner))
            {
                var remote = actor ? actor.GetComponent<RemoteRaidDash>() : null;
                return remote ? remote.Apply(displacement, now) : displacement;
            }
            if (owner.raidDashAppliedAt >= owner.raidDashEnd)
                return displacement;

            float next = Mathf.Min(now, owner.raidDashEnd);
            if (next <= owner.raidDashAppliedAt)
                return displacement;

            Vector3 step = owner.raidDashDirection * ((next - owner.raidDashAppliedAt) * RaidDashSpeed);
            owner.raidDashAppliedAt = next;
            return new Vector3(step.x, displacement.y, step.z);
        }

        internal static bool HasGhostwalk(Health actor, float now) =>
            RaidStatOwners.TryGetValue(actor, out var owner) && now < owner.ghostwalkUntil;

        private void ClearRaidDash()
        {
            raidDashEnd = 0f;
            raidDashAppliedAt = 0f;
            ghostwalkUntil = 0f;
            spectralArrowsRemaining = 0;
        }

        private void DrawRaidDashStatus(float now)
        {
            if (!LiveMatch || Event.current.type != EventType.Repaint)
                return;

            string label = now < ghostwalkUntil
                ? RewardText("幽行疾步", "Ghostwalk")
                : RewardText("V 短冲", "V Dash");
            if (raidDashCapacity > 1)
                label += $"  {raidDashCharges}/{raidDashCapacity}";
            if (raidDashCharges < raidDashCapacity && now < raidDashReady)
                label += $"  ·  {Mathf.CeilToInt(raidDashReady - now)}s";
            GUI.Label(new Rect(16, 78, 260, 26), label, RewardLabelStyle);
        }
    }
}
