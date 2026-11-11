using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterInstance
    {
        internal HeroStatBonuses HeroBonuses { get => researchStatItemStacks.Hero; set { var state = researchStatItemStacks; state.Hero = value; researchStatItemStacks = state; } }
        internal float HeroTemporaryArmor { get => researchStatItemStacks.HeroArmor; set { var state = researchStatItemStacks; state.HeroArmor = value; researchStatItemStacks = state; } }
        internal double HeroArmorUntil { get => researchStatItemStacks.HeroArmorUntil; set { var state = researchStatItemStacks; state.HeroArmorUntil = value; researchStatItemStacks = state; } }
        internal int HeroAppliedMaximum;
        internal float HeroCooldownMultiplier => Mathf.Clamp(1f - HeroBonuses.cooldown, .1f, 1f) *
            Mathf.Pow(.75f, ResearchAlienHeads) * Mathf.Pow(.5f, ResearchLightFlux);
        internal float HeroArmorMultiplier => 1f + HeroBonuses.armor;
        internal float HeroFlatArmor => NetworkTime.time < HeroArmorUntil ? HeroTemporaryArmor : 0f;

        [Server]
        internal void ServerSetHeroBonuses(HeroStatBonuses bonuses)
        {
            if (HeroBonuses.Same(bonuses)) return;
            float oldHealth = HeroBonuses.health;
            HeroBonuses = bonuses;
            if (!Mathf.Approximately(oldHealth, bonuses.health)) ServerSetMaximumHealth(MaxHealth);
        }

        internal int ApplyHeroMaximum(int unscaledMaximum)
        {
            int result = (int)System.Math.Min(int.MaxValue, System.Math.Round(
                System.Math.Max(1, unscaledMaximum) * (double)(1f + HeroBonuses.health)));
            HeroAppliedMaximum = result - unscaledMaximum;
            return result;
        }
    }
}
