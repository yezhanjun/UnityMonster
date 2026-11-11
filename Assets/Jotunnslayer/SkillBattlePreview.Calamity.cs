using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string CalamityUnlock = "active_nidhogg_E";
        public const string CalamityEvolution = "active_nidhogg_E_evolve";
        private int breathRank, breathTicks;
        private float breathReady, breathNext;
        private Vector3 breathOrigin, breathDirection;
        private static readonly List<RaidSkillRoot> RaidSkillRoots = new List<RaidSkillRoot>();

        private struct RaidSkillRoot
        {
            public SkillBattlePreview owner;
            public Health target;
            public float expiry;
        }

        private bool CanChooseCalamity() => PendingRewards > 0 && breathRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[breathRank] && progress.IsLearned(CalamityUnlock) &&
            (breathRank < 3 || progress.IsLearned(CalamityEvolution));

        internal bool TryChooseCalamity()
        {
            if (!CanChooseCalamity())
                return false;

            breathRank++;
            rewardsSpent++;
            return true;
        }

        private void TryCalamity(float now)
        {
            if (breathRank == 0 || now < breathReady || !progress.IsLearned(CalamityUnlock))
                return;

            var pose = caster.transform;
            breathOrigin = pose.position;
            breathDirection = Quaternion.Euler(0f, pose.eulerAngles.y, 0f) * Vector3.forward;
            breathReady = now + ScaleSkillCooldown(10f);
            int rank = Mathf.Min(breathRank, 3);
            bool evolved = breathRank == 4 && progress.IsLearned(CalamityEvolution);
            float rootChance = evolved ? 0.4f : 0.1f * rank;
            StrikeCalamity(now, rank, rootChance);
            breathCastCount = RollAnsuzRepeat(ref breathReady) ? 2 : 1;
            if (breathCastCount == 2)
                StrikeCalamity(now, rank, rootChance);

            breathTicks = evolved ? 3 : 0;
            breathNext = now + 1f;
        }

        private int breathCastCount;

        private void StrikeCalamity(float now, int rank, float rootChance)
        {
            IndicatePath(breathOrigin, breathOrigin + breathDirection * 12f, 1.5f);
            CollectTargets(breathOrigin, 13.5f);
            foreach (var target in targets)
            {
                if (!IsInBreath(target.transform.position))
                    continue;

                DealGodDamage(target, 40 + rank * 10);
                if (target && target.CurrentHealth > 0 && Random.value < rootChance)
                    ApplyRaidSkillRoot(target, now + 3f);
            }

        }

        private void ApplyRaidSkillRoot(Health target, float expiry)
        {
            for (int i = 0; i < RaidSkillRoots.Count; i++)
            {
                var root = RaidSkillRoots[i];
                if (root.owner != this || root.target != target)
                    continue;
                root.expiry = Mathf.Max(root.expiry, expiry);
                RaidSkillRoots[i] = root;
                return;
            }
            RaidSkillRoots.Add(new RaidSkillRoot { owner = this, target = target, expiry = expiry });
        }

        private bool IsInBreath(Vector3 position)
        {
            Vector3 offset = position - breathOrigin;
            float along = Vector3.Dot(offset, breathDirection);
            return along >= 0f && along <= 12f && (offset - breathDirection * along).sqrMagnitude <= 2.25f;
        }

        private void TickCalamity(float now)
        {
            for (int i = RaidSkillRoots.Count - 1; i >= 0; i--)
            {
                var root = RaidSkillRoots[i];
                if (root.owner == this && (now >= root.expiry || !root.target || root.target.CurrentHealth <= 0))
                    RaidSkillRoots.RemoveAt(i);
            }

            while (breathTicks > 0 && now >= breathNext)
            {
                breathTicks--;
                breathNext += 1f;
                for (int i = 0; i < breathCastCount; i++)
                {
                    CollectTargets(breathOrigin, 13.5f);
                    foreach (var target in targets)
                    {
                        if (IsInBreath(target.transform.position))
                            DealGodDamage(target, 15);
                    }
                }
            }
        }

        private void ClearCalamity()
        {
            breathTicks = 0;
            for (int i = RaidSkillRoots.Count - 1; i >= 0; i--)
            {
                if (RaidSkillRoots[i].owner == this)
                    RaidSkillRoots.RemoveAt(i);
            }
        }

        // Keep the existing enemy AI entry point while sharing the root state across skills.
        internal static bool IsRootedByCalamity(Health target) => IsRootedByRaidSkill(target, float.NegativeInfinity);

        internal static bool IsRootedByRaidSkill(Health target, float now)
        {
            foreach (var root in RaidSkillRoots)
            {
                if (root.target == target && now < root.expiry && root.owner.HasLiveCaster)
                    return true;
            }

            return false;
        }
    }
}
