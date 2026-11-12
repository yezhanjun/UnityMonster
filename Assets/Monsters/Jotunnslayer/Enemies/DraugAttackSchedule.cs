using UnityEngine;

namespace Jotunnslayer
{
    // Source gates and timing: DraugNativeChance/AttackTimer/SkillEligibility/CooldownTime verification.
    // Caller owns animation, projectile spawning and synchronized random generation.
    internal sealed class DraugAttackSchedule
    {
        internal enum Action { None, Arrow, Flask }
        internal const float Interval = 4f, Chance = .8f;
        internal Action Current { get; private set; }
        internal int ArrowIndex { get; private set; }
        internal float AttackRemaining { get; private set; }
        internal float SkillRemaining { get; private set; }
        internal uint Generation { get; private set; }
        bool released, castEnded, arrowEntered;

        internal void Tick(float scaledDeltaTime)
        {
            if (scaledDeltaTime <= 0f) return;
            AttackRemaining -= scaledDeltaTime;
            SkillRemaining -= Mathf.Min(scaledDeltaTime, .05f);
        }

        internal bool CanBegin(float distance, bool attackImpaired) =>
            Current == Action.None && !attackImpaired && AttackRemaining <= .001f && distance >= 0f && distance <= 10f;

        internal uint Begin(float distance, bool attackImpaired, float skillSample, int nextArrowIndex)
        {
            if (!CanBegin(distance, attackImpaired)) return 0;
            if (++Generation == 0) ++Generation;
            released = castEnded = arrowEntered = false;
            Current = SkillRemaining <= 0f && Chance > skillSample ? Action.Flask : Action.Arrow;
            if (Current == Action.Arrow)
            {
                AttackRemaining = Interval;
                // Current clip consumes the old index. The caller caches it before Begin.
                ArrowIndex = Mathf.Clamp(nextArrowIndex, 0, 2);
            }
            return Generation;
        }

        internal bool EnterArrow(uint generation, int nextIndex)
        {
            if (!Matches(generation) || Current != Action.Arrow || arrowEntered) return false;
            arrowEntered = true;
            AttackRemaining = Interval;
            SetNextArrowIndex(nextIndex);
            return true;
        }

        internal void SetNextArrowIndex(int index) => ArrowIndex=Mathf.Clamp(index,0,2);

        internal bool Release(uint generation)
        {
            if (!Matches(generation) || released || castEnded) return false;
            released = true;
            if (Current == Action.Flask) SkillRemaining = Interval;
            return true;
        }

        internal bool CastEnd(uint generation)
        {
            if (!Matches(generation) || Current != Action.Flask || castEnded) return false;
            castEnded = true;
            AttackRemaining = Interval;
            return true;
        }

        internal bool Finish(uint generation)
        {
            if (!Matches(generation)) return false;
            // Source cast-state exit calls OnCastEndEvent even when its clip event was missed.
            if (Current == Action.Flask && !castEnded) CastEnd(generation);
            Current = Action.None;
            return true;
        }

        internal void Cancel()
        {
            if (Current == Action.Flask && !castEnded) AttackRemaining = Interval;
            Current = Action.None;
            if (++Generation == 0) ++Generation;
        }

        bool Matches(uint generation) => generation != 0 && generation == Generation && Current != Action.None;
    }
}
