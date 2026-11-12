using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ShipEvolution = "active_njord_D_evolve";
        private int shipRank, shipDamage, shipArrows;
        private int shipCastCount;
        private float shipCurseChance;
        private float shipReady, shipStarted;
        private bool shipActive;
        private Vector3 shipOrigin, shipDirection, shipPrevious;
        private readonly HashSet<Health> shipHits = new HashSet<Health>();
        private static readonly int[] NjordPassiveLevels = { 1, 8, 15 };
        private static readonly float[] TorrentsCooldownFactors = { 1f, 0.94f, 0.87f, 0.8f };
        private const string LaguzEvolution = "passive_broksindri_H_evolve";
        private static readonly float[] LaguzCooldownFactors = { 1f, 0.92f, 0.84f, 0.76f, 0.65f };
        private int laguzRank;
        private float LaguzCooldownFactor => LaguzCooldownFactors[
            laguzRank == 4 && progress?.IsLearned(LaguzEvolution) != true ? 3 : laguzRank];

        private bool CanChooseLaguz() => HasLiveCaster && PendingRewards > 0 && laguzRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[laguzRank] && (laguzRank < 3 || progress.IsLearned(LaguzEvolution));

        internal bool TryChooseLaguz()
        {
            if (!CanChooseLaguz())
                return false;

            laguzRank++;
            rewardsSpent++;
            return true;
        }

        private int mightyOarsRank, torrentsRank;
        private bool movementSampled;
        private Vector3 lastMovementPosition;
        private float movementSampleTime, oarsDistance, jeraDistance;

        private bool CanChooseNjordPassive(int rank, int maxRank, string id) =>
            HasLiveCaster && PendingRewards > 0 && rank < maxRank &&
            raidLevel >= NjordPassiveLevels[rank] && (rank != 0 || progress.IsLearned(id));

        private bool CanChooseTorrents() => CanChooseNjordPassive(torrentsRank, 3, "passive_njord_I");

        internal bool TryChooseTorrents()
        {
            if (!CanChooseTorrents())
                return false;

            torrentsRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseMightyOars() => CanChooseNjordPassive(mightyOarsRank, 2, "passive_njord_H");

        internal bool TryChooseMightyOars()
        {
            if (!CanChooseMightyOars())
                return false;

            mightyOarsRank++;
            rewardsSpent++;
            return true;
        }

        private void TickDistancePassives(float now)
        {
            bool jeraEvolved = JeraEvolved;
            if (mightyOarsRank == 0 && !jeraEvolved)
            {
                movementSampled = false;
                return;
            }

            Vector3 origin = caster.transform.position;
            Vector3 step = origin - lastMovementPosition;
            step.y = 0f;
            float elapsed = now - movementSampleTime;
            bool sampled = movementSampled;
            movementSampled = true;
            lastMovementPosition = origin;
            movementSampleTime = now;
            if (!sampled || elapsed <= 0f)
                return;

            float distance = step.magnitude;
            if (distance > 30f * Mathf.Min(elapsed, 0.25f) + 0.5f)
                return;

            if (jeraEvolved)
            {
                jeraDistance += distance;
                if (jeraDistance >= 150f)
                {
                    jeraDistance -= 150f;
                    CollectNearbyGold(collectAll: true);
                }
            }

            if (mightyOarsRank == 0)
                return;

            oarsDistance += distance;
            float threshold = mightyOarsRank == 1 ? 30f : 20f;
            if (oarsDistance < threshold)
                return;

            oarsDistance -= threshold;
            CollectTargets(origin, 4f);
            foreach (var target in targets)
            {
                int before = target.CurrentHealth;
                DealGodDamage(target, 22);
                if (target && target.CurrentHealth > 0 && target.CurrentHealth < before)
                    PushSkillTarget(target, origin, 1f);
            }
        }

        private bool CanChooseShip() => PendingRewards > 0 && shipRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[shipRank] && (shipRank < 3 || progress.IsLearned(ShipEvolution));

        internal bool TryChooseShip()
        {
            if (!CanChooseShip())
                return false;

            shipRank++;
            rewardsSpent++;
            return true;
        }

        private void TryShip(float now)
        {
            if (shipRank == 0 || shipActive || now < shipReady)
                return;

            int rank = Mathf.Min(shipRank, 3);
            bool evolved = shipRank == 4 && progress.IsLearned(ShipEvolution);
            shipOrigin = shipPrevious = caster.transform.position;
            shipDirection = Quaternion.Euler(0, caster.transform.eulerAngles.y, 0) * Vector3.forward;
            shipDamage = evolved ? 40 : 18 + rank * 6;
            shipCurseChance = (rank - 1) * 0.25f;
            shipArrows = evolved ? 20 : 0;
            shipStarted = now;
            shipReady = now + ScaleSkillCooldown(10f - rank);
            shipHits.Clear();
            shipActive = true;
            shipCastCount = RollAnsuzRepeat(ref shipReady) ? 2 : 1;
        }

        private void TickShip(float now)
        {
            if (!shipActive)
                return;

            float elapsed = Mathf.Clamp(now - shipStarted, 0f, 4f);
            Vector3 current = shipOrigin + shipDirection * (elapsed * 5f);
            float distance = (current - shipPrevious).magnitude;
            CollectTargets(shipPrevious, distance + 1.25f);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - shipPrevious;
                float along = Mathf.Clamp(Vector3.Dot(offset, shipDirection), 0f, distance);
                if ((offset - shipDirection * along).sqrMagnitude <= 1.5625f && shipHits.Add(target))
                {
                    for (int i = 0; i < shipCastCount && target && target.CurrentHealth > 0; i++)
                    {
                        int before = target.CurrentHealth;
                        DealGodDamage(target, shipDamage);
                        if (target && target.CurrentHealth < before)
                            TryApplyRaidCurse(target, now, shipCurseChance);
                    }
                }
            }
            shipPrevious = current;

            while (shipArrows > 0 && elapsed >= (21 - shipArrows) * 0.2f)
            {
                Vector3 source = shipOrigin + shipDirection * (21 - shipArrows);
                shipArrows--;
                for (int i = 0; i < shipCastCount; i++)
                {
                    CollectTargets(source, 4f);
                    if (targets.Count > 0)
                        DealGodDamage(targets[Random.Range(0, targets.Count)], 7);
                }
            }

            if (elapsed >= 4f)
            {
                shipActive = false;
                shipHits.Clear();
            }
        }
    }
}
