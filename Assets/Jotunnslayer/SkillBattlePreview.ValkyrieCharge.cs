using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ValkyrieChargeUnlock = "active_valkyrie_E";
        public const string ValkyrieChargeEvolution = "active_valkyrie_E_evolve";
        private const float ValkyrieChargeRange = 10f;
        private const float ValkyrieChargeRadius = 0.6f;
        private const float ValkyrieChargeSpacing = 1.5f;
        private readonly HashSet<Health> valkyrieChargeHits = new HashSet<Health>();
        private int valkyrieChargeRank, valkyrieChargeLanes, valkyrieChargeDamage;
        private float valkyrieChargeReady, valkyrieChargeStarted, valkyrieChargeDistance;
        private Vector3 valkyrieChargeOrigin, valkyrieChargeForward, valkyrieChargeRight;
        private int spearheadRank;

        private bool CanChooseSpearhead() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 &&
            spearheadRank < 2 && (spearheadRank == 0 ? progress.IsLearned("passive_valkyrie_O") : raidLevel >= 8);

        internal bool TryChooseSpearhead()
        {
            if (!CanChooseSpearhead())
                return false;

            spearheadRank++;
            rewardsSpent++;
            return true;
        }

        private int ScaleValkyrieDamage(int damage) => ScaleClassSkillDamage((damage * (4 + spearheadRank) + 2) / 4);

        private bool CanChooseValkyrieCharge() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 && valkyrieChargeRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[valkyrieChargeRank] && progress.IsLearned(ValkyrieChargeUnlock) &&
            (valkyrieChargeRank < 3 || progress.IsLearned(ValkyrieChargeEvolution));

        internal bool TryChooseValkyrieCharge()
        {
            if (!CanChooseValkyrieCharge())
                return false;

            valkyrieChargeRank++;
            rewardsSpent++;
            return true;
        }

        private void TryValkyrieCharge(float now)
        {
            if (valkyrieChargeRank == 0 || valkyrieChargeLanes != 0 || now < valkyrieChargeReady ||
                !progress.IsLearned(ValkyrieChargeUnlock))
                return;

            int rank = valkyrieChargeRank == 4 && !progress.IsLearned(ValkyrieChargeEvolution)
                ? 3 : valkyrieChargeRank;
            valkyrieChargeLanes = rank == 4 ? 7 : 3;
            valkyrieChargeDamage = 40 + 10 * rank;
            valkyrieChargeReady = now + ScaleSkillCooldown(12f);
            valkyrieChargeStarted = now;
            valkyrieChargeDistance = 0f;
            valkyrieChargeOrigin = caster.transform.position;
            var rotation = Quaternion.Euler(0f, caster.transform.eulerAngles.y, 0f);
            valkyrieChargeForward = rotation * Vector3.forward;
            valkyrieChargeRight = rotation * Vector3.right;
        }

        private void TickValkyrieCharge(float now)
        {
            if (valkyrieChargeLanes == 0)
                return;

            float distance = Mathf.Clamp01(now - valkyrieChargeStarted) * ValkyrieChargeRange;
            float previous = valkyrieChargeDistance;
            if (distance <= previous)
                return;

            int outerLane = valkyrieChargeLanes / 2;
            Vector3 center = valkyrieChargeOrigin + valkyrieChargeForward * ((previous + distance) * 0.5f);
            float radius = (distance - previous) * 0.5f + outerLane * ValkyrieChargeSpacing + ValkyrieChargeRadius;
            CollectTargets(center, radius);
            foreach (var target in targets)
            {
                if (!target || valkyrieChargeHits.Contains(target))
                    continue;

                Vector3 offset = target.transform.position - valkyrieChargeOrigin;
                float lateral = Vector3.Dot(offset, valkyrieChargeRight);
                int lane = Mathf.Clamp(Mathf.RoundToInt(lateral / ValkyrieChargeSpacing), -outerLane, outerLane);
                float along = Mathf.Clamp(Vector3.Dot(offset, valkyrieChargeForward), previous, distance);
                Vector3 closest = valkyrieChargeForward * along + valkyrieChargeRight * (lane * ValkyrieChargeSpacing);
                if ((offset - closest).sqrMagnitude > ValkyrieChargeRadius * ValkyrieChargeRadius)
                    continue;

                valkyrieChargeHits.Add(target);
                Deal(target, ScaleValkyrieDamage(valkyrieChargeDamage));
            }

            valkyrieChargeDistance = distance;
            if (distance >= ValkyrieChargeRange)
                ClearValkyrieCharge();
        }

        private void ClearValkyrieCharge()
        {
            valkyrieChargeLanes = 0;
            valkyrieChargeHits.Clear();
        }
    }
}
