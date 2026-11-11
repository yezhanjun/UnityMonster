using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string RavenEvolution = "active_odin_B_evolve";
        private int ravenRank, ravenTicks;
        private float ravenReady, ravenNext, ravenRadius;
        private bool ravenEvolved;
        private Vector3 ravenOrigin, ravenPoint;
        private bool winterRavenChosen;
        private float? winterRavenReady;
        private float winterRavenImpactAt;
        private Vector3 winterRavenPoint;

        private bool CanChooseWinterRaven() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && !winterRavenChosen;

        internal bool TryChooseWinterRaven()
        {
            if (!CanChooseWinterRaven())
                return false;

            winterRavenChosen = true;
            rewardsSpent++;
            return true;
        }

        private void TryWinterRaven(float now)
        {
            if (!winterRavenChosen || winterRavenImpactAt > 0f)
                return;
            if (!winterRavenReady.HasValue)
                winterRavenReady = now + ScaleSkillCooldown(7f);
            if (now < winterRavenReady.Value)
                return;

            Vector3 origin = caster.transform.position;
            winterRavenReady = now + ScaleSkillCooldown(7f);
            CollectTargets(origin, MissileRange);
            if (targets.Count == 0)
                return;

            winterRavenPoint = targets[Random.Range(0, targets.Count)].transform.position;
            winterRavenImpactAt = now + Mathf.Max(0.01f, Vector3.Distance(origin, winterRavenPoint) / 11f);
        }

        private void TickWinterRaven(float now)
        {
            if (winterRavenImpactAt <= 0f || now < winterRavenImpactAt)
                return;

            winterRavenImpactAt = 0f;
            CollectTargets(winterRavenPoint, 1.5f);
            foreach (var target in targets)
            {
                Deal(target, ScaleClassSkillDamage(16));
                StopSkillTarget(target, now + 2.5f, frozen: true);
            }
        }

        private bool CanChooseRavens() => PendingRewards > 0 && ravenRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[ravenRank] && (ravenRank < 3 || progress.IsLearned(RavenEvolution));

        internal bool TryChooseRavens()
        {
            if (!CanChooseRavens())
                return false;

            ravenRank++;
            rewardsSpent++;
            return true;
        }

        private void TryRavens(float now)
        {
            if (ravenRank == 0 || ravenTicks > 0 || now < ravenReady)
                return;

            var pose = caster.transform;
            ravenOrigin = pose.position;
            ravenEvolved = ravenRank == 4 && progress.IsLearned(RavenEvolution);
            if (ravenEvolved)
            {
                ravenPoint = ravenOrigin + pose.forward * 10f;
                ravenReady = now + ScaleSkillCooldown(3f);
                ravenNext = now + 0.5f;
                ravenTicks = 2;
                ravenCastCount = RollAnsuzRepeat(ref ravenReady) ? 2 : 1;
                ravenRepeatPoint = ravenPoint;
                return;
            }

            CollectTargets(ravenOrigin, MissileRange);
            if (targets.Count == 0)
                return;

            int rank = Mathf.Min(ravenRank, 3);
            ravenPoint = targets[Random.Range(0, targets.Count)].transform.position;
            ravenRadius = 1f + rank * 0.5f;
            ravenReady = now + ScaleSkillCooldown(9f - rank);
            ravenNext = now + 1f;
            ravenTicks = 3;
            ravenCastCount = RollAnsuzRepeat(ref ravenReady) ? 2 : 1;
            if (ravenCastCount == 2)
                ravenRepeatPoint = targets[Random.Range(0, targets.Count)].transform.position;
        }

        private void TickRavens(float now)
        {
            while (ravenTicks > 0 && now >= ravenNext)
            {
                if (ravenEvolved)
                {
                    Vector3 endpoint = ravenTicks == 2 ? ravenOrigin : caster.transform.position;
                    SweepGodDamage(endpoint, ravenPoint, 20);
                    if (ravenCastCount == 2)
                        SweepGodDamage(endpoint, ravenRepeatPoint, 20);
                    ravenNext += 0.5f;
                }
                else
                {
                    StrikeGodDamage(ravenPoint, ravenRadius, 14);
                    if (ravenCastCount == 2)
                        StrikeGodDamage(ravenRepeatPoint, ravenRadius, 14);
                    ravenNext += 1f;
                }

                ravenTicks--;
            }
        }

        private int ravenCastCount;
        private Vector3 ravenRepeatPoint;
    }
}
