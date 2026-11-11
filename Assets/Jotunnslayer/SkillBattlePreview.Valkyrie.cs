using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ValkyrieEvolution = "active_freya_B_evolve";
        private int valkyrieRank;
        private float valkyrieReady;
        private float? soulmateReady;
        internal const float SoulmateWindup = 0.15f;
        private Health soulmateTarget;
        private float soulmateCastAt;
        private readonly List<ValkyrieZone> valkyrieZones = new List<ValkyrieZone>(3);

        private void TickSoulmate(float now)
        {
            if (!LiveMatch || SelectedRaidClass != RaidClass.Valkyrie)
            {
                if (soulmateReady.HasValue)
                    ClearSoulmate();
                return;
            }

            if (!soulmateReady.HasValue)
            {
                soulmateReady = now + 1f;
                if (caster is CharacterInstance actor)
                    actor.RaidSoulmateActive = true;
            }
            if (soulmateTarget && now >= soulmateCastAt)
            {
                var lockedTarget = soulmateTarget;
                soulmateTarget = null;
                if (lockedTarget.isServer && lockedTarget.CurrentHealth > 0 && lockedTarget.Team != caster.Team &&
                    (lockedTarget.transform.position - caster.transform.position).sqrMagnitude <= 49f)
                    ResolveSoulmateAttack(lockedTarget, now);
            }
            if (!HasLiveCaster)
                return;
            if (!soulmateReady.HasValue || now < soulmateReady.Value)
                return;

            soulmateReady = now + 1f;
            Vector3 origin = caster.transform.position;
            CollectTargets(origin, 7f);
            if (targets.Count == 0)
                return;

            var target = targets[0];
            float nearestDistance = (target.transform.position - origin).sqrMagnitude;
            for (int i = 1; i < targets.Count; i++)
            {
                var candidate = targets[i];
                float distance = (candidate.transform.position - origin).sqrMagnitude;
                if (distance < nearestDistance || (distance == nearestDistance && candidate.netId < target.netId))
                {
                    target = candidate;
                    nearestDistance = distance;
                }
            }
            soulmateTarget = target;
            soulmateCastAt = now + SoulmateWindup;
            if (caster is CharacterInstance source && source.netId != 0)
                source.RpcRaidSoulmateAttack(null, Vector3.zero, true);
        }

        private void ResolveSoulmateAttack(Health target, float now)
        {
            int before = target.CurrentHealth;
            if (caster is CharacterInstance source && source.netId != 0)
                source.RpcRaidSoulmateAttack(target, RaidSkillAimPoint(target), false);
            Deal(target, ScaleClassSkillDamage(20));
            if (target && target.CurrentHealth < before)
                TryApplyRaidCurse(target, now, 0.2f, 3f);
        }

        private void ClearSoulmate()
        {
            soulmateReady = null;
            soulmateTarget = null;
            if (caster is CharacterInstance actor && actor && actor.isServer)
                actor.RaidSoulmateActive = false;
        }

        private struct ValkyrieZone
        {
            public Vector3 center;
            public float nextTick, expiry;
            public bool evolved;

            public bool Contains(Vector3 position)
            {
                Vector3 offset = position - center;
                if (!evolved)
                    return offset.sqrMagnitude <= 2.25f;

                Vector3 area = Mathf.Abs(offset.x) > Mathf.Abs(offset.z)
                    ? Vector3.right * (offset.x >= 0 ? 1.5f : -1.5f)
                    : Vector3.forward * (offset.z >= 0 ? 1.5f : -1.5f);
                return (offset - area).sqrMagnitude <= 2.25f;
            }
        }

        private bool CanChooseValkyrie() => PendingRewards > 0 && valkyrieRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[valkyrieRank] && (valkyrieRank < 3 || progress.IsLearned(ValkyrieEvolution));

        internal bool TryChooseValkyrie()
        {
            if (!CanChooseValkyrie())
                return false;

            valkyrieRank++;
            rewardsSpent++;
            return true;
        }

        private void TryValkyrie(float now)
        {
            if (valkyrieRank == 0 || now < valkyrieReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            int rank = Mathf.Min(valkyrieRank, 3);
            bool evolved = valkyrieRank == 4 && progress.IsLearned(ValkyrieEvolution);
            var zone = new ValkyrieZone
            {
                center = targets[Random.Range(0, targets.Count)].transform.position,
                nextTick = now + 1f,
                expiry = now + (evolved ? 6f : 2f + rank),
                evolved = evolved
            };
            valkyrieReady = now + ScaleSkillCooldown(evolved ? 2f : 4f - rank * 0.5f);
            valkyrieZones.Add(zone);
            DamageValkyrieZone(zone, 35);
            if (RollAnsuzRepeat(ref valkyrieReady))
            {
                valkyrieZones.Add(zone);
                DamageValkyrieZone(zone, 35);
            }
        }

        private void TickValkyrie(float now)
        {
            for (int i = valkyrieZones.Count - 1; i >= 0; i--)
            {
                var zone = valkyrieZones[i];
                while (zone.nextTick <= zone.expiry && now >= zone.nextTick)
                {
                    DamageValkyrieZone(zone, 6);
                    zone.nextTick += 1f;
                }

                if (now >= zone.expiry)
                    valkyrieZones.RemoveAt(i);
                else
                    valkyrieZones[i] = zone;
            }
        }

        private void DamageValkyrieZone(ValkyrieZone zone, int damage)
        {
            CollectTargets(zone.center, zone.evolved ? 3f : 1.5f);
            foreach (var target in targets)
            {
                if (zone.Contains(target.transform.position))
                    DealGodDamage(target, damage);
            }
        }
    }
}
