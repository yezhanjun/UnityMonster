using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string HammertimeUnlock = "active_warden_E";
        public const string HammertimeEvolution = "active_warden_E_evolve";
        private const float HammertimeStepDuration = 1.5f / 4f;
        private readonly HashSet<Health> hammertimeHits = new HashSet<Health>();
        private readonly HashSet<Health> secondHammerHits = new HashSet<Health>();
        private int hammertimeRank, hammertimeStep, hammertimeCastRank;
        private float hammertimeReady, hammertimeNextSwing;

        private bool CanChooseHammertime() => AllowsRaidClass(RaidClass.Warden) && PendingRewards > 0 && hammertimeRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[hammertimeRank] && progress.IsLearned(HammertimeUnlock) &&
            (hammertimeRank < 3 || progress.IsLearned(HammertimeEvolution));

        internal bool TryChooseHammertime()
        {
            if (!CanChooseHammertime())
                return false;

            hammertimeRank++;
            rewardsSpent++;
            return true;
        }

        private void TryHammertime(float now)
        {
            if (hammertimeRank == 0 || now < hammertimeReady || !progress.IsLearned(HammertimeUnlock))
                return;

            hammertimeCastRank = hammertimeRank == 4 && progress.IsLearned(HammertimeEvolution)
                ? 4 : Mathf.Min(hammertimeRank, 3);
            hammertimeReady = now + ScaleSkillCooldown(Mathf.Max(3f, 4.5f - hammertimeCastRank * 0.5f));
            hammertimeNextSwing = now + HammertimeStepDuration;
            hammertimeStep = 0;
            hammertimeHits.Clear();
            secondHammerHits.Clear();
        }

        private void TickHammertime(float now)
        {
            while (hammertimeCastRank != 0 && now >= hammertimeNextSwing)
            {
                var pose = caster.transform;
                Vector3 direction = Quaternion.AngleAxis(hammertimeStep * 90f, Vector3.up) * pose.forward;
                float range = 2f + hammertimeCastRank;
                bool evolved = hammertimeCastRank == 4;
                SweepHammertime(pose.position, direction, range, evolved ? 14 : 10, hammertimeHits);
                if (evolved)
                    SweepHammertime(pose.position, -direction, range, 14, secondHammerHits);

                hammertimeNextSwing += HammertimeStepDuration;
                if (++hammertimeStep == 4)
                    ClearHammertime();
            }
        }

        private void SweepHammertime(Vector3 origin, Vector3 direction, float range, int damage,
            HashSet<Health> hit)
        {
            const float radius = 0.6f;
            CollectTargets(origin, range + radius);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - origin;
                float distance = Mathf.Clamp(Vector3.Dot(offset, direction), 0f, range);
                if ((offset - direction * distance).sqrMagnitude <= radius * radius && hit.Add(target))
                    Deal(target, ScaleClassSkillDamage(damage));
            }
        }

        private void ClearHammertime()
        {
            if (hammertimeCastRank == 0)
                return;

            hammertimeCastRank = 0;
            hammertimeHits.Clear();
            secondHammerHits.Clear();
        }
    }
}
