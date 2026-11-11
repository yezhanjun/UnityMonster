using System;
using Mirror;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [Serializable]
    public struct HeroStatBonuses
    {
        public float regeneration, manaRegeneration, damage, critical, armor, health, speed, cooldown;
        public int goldPerSecond;
        public bool Same(HeroStatBonuses b) => regeneration == b.regeneration && manaRegeneration == b.manaRegeneration &&
            damage == b.damage && critical == b.critical && armor == b.armor && health == b.health &&
            speed == b.speed && cooldown == b.cooldown && goldPerSecond == b.goldPerSecond;
        public void Add(HeroPassiveDefinition item)
        {
            if (item == null) return;
            regeneration += item.regeneration; manaRegeneration += item.manaRegeneration;
            damage += item.damage; critical += item.critical; armor += item.armor;
            health += item.health; speed += item.speed;
        }
    }

    public partial class PlayerInstance
    {
        [SyncVar] internal string HeroQ = "", HeroE = "", HeroV = "";
        [SyncVar] internal string HeroPassive1 = "", HeroPassive2 = "", HeroPassive3 = "";
        [SyncVar] internal int HeroAura;
        [SyncVar] internal string HeroTrait = "";
        [SyncVar] internal float HeroMana;
        [SyncVar] internal double HeroQReady, HeroEReady, HeroVReady;
        [SyncVar] internal int HeroWeapon = -1;
        int sentHero = -1;
        float nextHeroAggregate, heroGoldRemainder;
        CharacterInstance heroBody;
        string appliedTrait;
        int appliedWeapon = -2;
        int manaHeroSelection = -1;
        readonly System.Collections.Generic.List<Health> heroTargets = new System.Collections.Generic.List<Health>(32);
        internal bool HasHeroLoadout => !BOT && HeroLoadoutCatalog.Hero(characterSelection) != null;
        internal int HeroSelection => characterSelection;
        internal string HeroSlot(int slot) => slot == 0 ? HeroQ : slot == 1 ? HeroE : slot == 2 ? HeroV :
            slot == 3 ? HeroPassive1 : slot == 4 ? HeroPassive2 : HeroPassive3;
        internal double HeroReady(int slot) => slot == 0 ? HeroQReady : slot == 1 ? HeroEReady : HeroVReady;

        // Called by the existing player Update. Commands remain on the existing spawned NetworkIdentity.
        void TickHeroLoadout()
        {
            if (isOwned && sentHero != characterSelection && HasHeroLoadout)
            {
                sentHero = characterSelection;
                CmdSetHeroLoadout(HeroLoadoutCatalog.Saved(sentHero, 0), HeroLoadoutCatalog.Saved(sentHero, 1),
                    HeroLoadoutCatalog.Saved(sentHero, 2), HeroLoadoutCatalog.Saved(sentHero, 3),
                    HeroLoadoutCatalog.Saved(sentHero, 4), HeroLoadoutCatalog.Saved(sentHero, 5),
                    HeroLoadoutCatalog.SavedAura(sentHero), PlayerPrefs.GetString("HeroTrait." + sentHero, ""),
                    PlayerPrefs.GetInt("HeroWeapon." + sentHero, -1));
            }
            if (!isServer) return;
            if (heroBody != MyCharacter)
            {
                heroBody = MyCharacter; appliedTrait = null; appliedWeapon = -2;
                manaHeroSelection = -1;
            }
            if (manaHeroSelection != characterSelection)
            {
                manaHeroSelection = characterSelection;
                HeroMana = HasHeroLoadout ? HeroLoadoutCatalog.Hero(characterSelection).maximumMana : 0f;
            }
            if (BOT || GameManager.Gamemode is not RangeGamemode) return;
            if (!MyCharacter || MyCharacter.CurrentHealth <= 0) { heroGoldRemainder = 0; appliedTrait = null; return; }
            if (Time.time >= nextHeroAggregate)
            {
                nextHeroAggregate = Time.time + .2f;
                MyCharacter.ServerSetHeroBonuses(CalculateHeroBonuses());
            }
            var definition = HeroLoadoutCatalog.Hero(characterSelection);
            if (definition != null) HeroMana = Mathf.Min(definition.maximumMana, HeroMana +
                (definition.manaRegeneration + MyCharacter.HeroBonuses.manaRegeneration) * Time.deltaTime);
            heroGoldRemainder += MyCharacter.HeroBonuses.goldPerSecond * Time.deltaTime;
            if (heroGoldRemainder >= 1f)
            {
                int amount = Mathf.FloorToInt(heroGoldRemainder); heroGoldRemainder -= amount;
                RainGold = (int)Math.Min(int.MaxValue, (long)RainGold + amount);
            }
            if (appliedTrait != HeroTrait)
            {
                Jotunnslayer.GodSkillGm.Stop(MyCharacter);
                appliedTrait = HeroTrait;
                if (!string.IsNullOrEmpty(HeroTrait))
                    Jotunnslayer.GodSkillGm.Activate(MyCharacter, HeroTrait, false, false);
            }
            if (appliedWeapon != HeroWeapon && MyCharacter.CharacterItemManager &&
                MyCharacter.CharacterItemManager.Slots.Count > 0 &&
                MyCharacter.CharacterItemManager.Slots[0].Item)
            {
                var prefab = HeroWeapon >= 0 ? HeroWeaponPrefab(HeroWeapon) : appliedWeapon >= 0
                    ? MyCharacter.CharacterItemManager.Slots[0].ItemOnSpawn : null;
                appliedWeapon = HeroWeapon;
                if (prefab) MyCharacter.CharacterItemManager.ServerEquipTestWeapon(prefab);
            }
        }

        internal HeroStatBonuses CalculateHeroBonuses()
        {
            HeroStatBonuses result = default;
            if (HasHeroLoadout)
            {
                result.Add(HeroLoadoutCatalog.Passive(HeroPassive1));
                result.Add(HeroLoadoutCatalog.Passive(HeroPassive2));
                result.Add(HeroLoadoutCatalog.Passive(HeroPassive3));
            }
            // Identical effects from duplicate heroes do not stack in the initial ruleset.
            int mask = 0;
            foreach (var pair in GameManager.Players)
            {
                var source = pair.Value;
                if (!source || !source.HasHeroLoadout || !source.MyCharacter || source.MyCharacter.CurrentHealth <= 0 ||
                    source.Team != Team || Team < 0 || (GameManager.Gamemode.FFA && source != this)) continue;
                mask |= 1 << ((source.characterSelection - 12) * 2 + source.HeroAura);
            }
            if ((mask & 1) != 0) { result.regeneration += 1; result.manaRegeneration += 1; }
            if ((mask & 2) != 0) result.cooldown += .1f;
            if ((mask & 4) != 0) result.goldPerSecond = 1;
            if ((mask & 8) != 0) result.speed += .01f;
            if ((mask & 16) != 0) result.damage += .1f;
            if ((mask & 32) != 0) result.critical += .1f;
            if ((mask & 64) != 0) result.armor += .1f;
            if ((mask & 128) != 0) result.health += .1f;
            return result;
        }

        internal static GameObject HeroWeaponPrefab(int index)
        {
            if (!ItemManager.Instance || ItemManager.Instance.SlotsLodout.Length == 0 || index < 0) return null;
            var options = ItemManager.Instance.SlotsLodout[0].availableItemsForSlot;
            return index < options.Length && options[index] ? options[index].gameObject : null;
        }

        internal void ServerRefreshHeroBonuses() => nextHeroAggregate = 0;

        [Command]
        internal void CmdSetHeroLoadout(string q, string e, string v, string p1, string p2, string p3, int aura, string trait, int weapon)
        {
            if (!HasHeroLoadout || GameManager.Gamemode is not RangeGamemode) return;
            HeroQ = HeroLoadoutCatalog.Valid(characterSelection, 0, q);
            HeroE = HeroLoadoutCatalog.Valid(characterSelection, 1, e);
            HeroV = HeroLoadoutCatalog.Valid(characterSelection, 2, v);
            HeroPassive1 = HeroLoadoutCatalog.Valid(characterSelection, 3, p1);
            HeroPassive2 = p2 == HeroPassive1 ? "" : HeroLoadoutCatalog.Valid(characterSelection, 4, p2);
            HeroPassive3 = p3 == HeroPassive1 || p3 == HeroPassive2 ? "" : HeroLoadoutCatalog.Valid(characterSelection, 5, p3);
            HeroAura = Mathf.Clamp(aura, 0, 1);
            HeroTrait = Array.IndexOf(Jotunnslayer.SkillBattlePreview.GmGodSkillIds, trait) >= 0 ? trait : "";
            HeroWeapon = HeroWeaponPrefab(weapon) ? weapon : -1;
            nextHeroAggregate = 0;
        }

        [Command]
        internal void CmdUseHeroSkill(int slot)
        {
            if (!HasHeroLoadout || GameManager.Gamemode is not RangeGamemode || !MyCharacter ||
                MyCharacter.CurrentHealth <= 0 || slot < 0 || slot > 2 || Time.timeScale <= 0 ||
                NetworkTime.time < HeroReady(slot)) return;
            var motor = MyCharacter.GetComponent<CharacterMotor>();
            if (motor && (motor.RaidControlStopsActions || motor.IsFourWayRolling)) return;
            var action = HeroLoadoutCatalog.Action(HeroSlot(slot));
            if (action == null || HeroMana < action.mana) return;
            if (action.effect != "heal" && action.effect != "blast" && action.effect != "armor") return;
            HeroMana -= action.mana;
            double ready = NetworkTime.time + Mathf.Max(.1f, action.cooldown * MyCharacter.HeroCooldownMultiplier);
            if (slot == 0) HeroQReady = ready; else if (slot == 1) HeroEReady = ready; else HeroVReady = ready;
            if (action.effect == "heal")
                MyCharacter.ServerHeal(Mathf.RoundToInt(MyCharacter.MaxHealth * action.power), MyCharacter.netId, false);
            else if (action.effect == "armor")
            {
                MyCharacter.HeroTemporaryArmor = action.power;
                MyCharacter.HeroArmorUntil = NetworkTime.time + action.duration;
            }
            else
            {
                heroTargets.Clear();
                foreach (var pair in GameManager.HealthInstances)
                {
                    var target = pair.Value;
                    if (!target || target == MyCharacter || target.CurrentHealth <= 0 ||
                        (!GameManager.Gamemode.FFA && target.Team == Team) ||
                        (target.transform.position - MyCharacter.transform.position).sqrMagnitude > action.radius * action.radius) continue;
                    heroTargets.Add(target);
                }
                for (int i=0;i<heroTargets.Count;i++)
                    if(heroTargets[i] && heroTargets[i].CurrentHealth>0)
                        heroTargets[i].ServerApplyRaidSkillDamage(Mathf.RoundToInt(MyCharacter.EffectiveRainAttackPower * action.power), MyCharacter);
                heroTargets.Clear();
            }
            RpcHeroSkillIndicator(MyCharacter.transform.position, action.radius, action.effect == "heal");
        }

        [ClientRpc]
        void RpcHeroSkillIndicator(Vector3 center, float radius, bool healing)
            => HeroSkillIndicator.Show(center, radius, healing);
    }
}
