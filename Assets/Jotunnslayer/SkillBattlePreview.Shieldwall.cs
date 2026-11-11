using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ShieldwallEvolution = "active_warden_C_evolve";
        private const float ShieldwallDuration = 3f;
        private const float ShieldwallOrbit = 2f;
        private const float ShieldwallContact = 0.6f;
        private readonly Dictionary<Health, int> shieldwallHits = new Dictionary<Health, int>();
        private int shieldwallRank, shieldwallCount;
        private float shieldwallReady, shieldwallStarted, shieldwallElapsed;
        private Vector3 shieldwallOrigin, shieldwallFacing;

        private bool CanChooseShieldwall() => AllowsRaidClass(RaidClass.Warden) && PendingRewards > 0 && shieldwallRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[shieldwallRank] &&
            (shieldwallRank < 3 || progress.IsLearned(ShieldwallEvolution));

        internal bool TryChooseShieldwall()
        {
            if (!CanChooseShieldwall())
                return false;

            shieldwallRank++;
            rewardsSpent++;
            return true;
        }

        private void TryShieldwall(float now)
        {
            if (shieldwallRank == 0 || shieldwallCount != 0 || now < shieldwallReady)
                return;

            int rank = shieldwallRank == 4 && !progress.IsLearned(ShieldwallEvolution) ? 3 : shieldwallRank;
            shieldwallCount = rank + 1;
            shieldwallReady = now + ScaleSkillCooldown(rank == 4 ? 4f : 5f);
            shieldwallStarted = now;
            shieldwallElapsed = 0f;
            shieldwallOrigin = caster.transform.position;
            shieldwallFacing = Quaternion.Euler(0f, caster.transform.eulerAngles.y, 0f) * Vector3.forward;
            shieldwallHits.Clear();
            SweepShieldwall(shieldwallOrigin, shieldwallOrigin, 0f, 0f);
        }

        private void TickShieldwall(float now)
        {
            if (shieldwallCount == 0)
                return;

            float elapsed = Mathf.Clamp(now - shieldwallStarted, shieldwallElapsed, ShieldwallDuration);
            Vector3 origin = caster.transform.position;
            int segments = Mathf.Max(1, Mathf.CeilToInt((elapsed - shieldwallElapsed) * 8f));
            for (int i = 0; i < segments; i++)
            {
                float from = (float)i / segments;
                float to = (float)(i + 1) / segments;
                SweepShieldwall(Vector3.Lerp(shieldwallOrigin, origin, from), Vector3.Lerp(shieldwallOrigin, origin, to),
                    Mathf.Lerp(shieldwallElapsed, elapsed, from), Mathf.Lerp(shieldwallElapsed, elapsed, to));
            }

            shieldwallOrigin = origin;
            shieldwallElapsed = elapsed;
            if (elapsed >= ShieldwallDuration)
                ClearShieldwall();
        }

        private void SweepShieldwall(Vector3 origin, Vector3 destination, float from, float to)
        {
            int damage = 9 + 3 * shieldwallCount;
            for (int i = 0; i < shieldwallCount; i++)
            {
                float angle = 360f * i / shieldwallCount;
                Vector3 start = origin + Quaternion.AngleAxis(angle + from * 120f, Vector3.up) * shieldwallFacing * ShieldwallOrbit;
                Vector3 end = destination + Quaternion.AngleAxis(angle + to * 120f, Vector3.up) * shieldwallFacing * ShieldwallOrbit;
                Vector3 path = end - start;
                float length = path.magnitude;
                int bit = 1 << i;
                CollectTargets(start, length + ShieldwallContact);
                foreach (var target in targets)
                {
                    shieldwallHits.TryGetValue(target, out int hits);
                    if ((hits & bit) != 0)
                        continue;

                    Vector3 offset = target.transform.position - start;
                    float fraction = length > 0 ? Mathf.Clamp01(Vector3.Dot(offset, path) / (length * length)) : 0f;
                    if ((offset - fraction * path).sqrMagnitude > ShieldwallContact * ShieldwallContact)
                        continue;

                    shieldwallHits[target] = hits | bit;
                    int before = target.CurrentHealth;
                    Deal(target, ScaleClassSkillDamage(damage));
                    if (target && target.CurrentHealth > 0 && target.CurrentHealth < before)
                        PushSkillTarget(target, Vector3.Lerp(origin, destination, fraction), 1f);
                }
            }
        }

        private static void PushSkillTarget(Health target, Vector3 center, float distance)
        {
            if (target is CharacterInstance character && !character.BOT)
                return;

            Vector3 offset = target.transform.position - center;
            offset.y = 0f;
            Vector3 step = offset.normalized * distance;
            if (target.TryGetComponent<CharacterController>(out var controller) && controller.enabled)
                controller.Move(step);
            else if (target is not CharacterInstance)
                target.transform.position += step;
        }

        private void ClearShieldwall()
        {
            shieldwallCount = 0;
            shieldwallHits.Clear();
        }
    }
}
