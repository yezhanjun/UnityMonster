using System;
using UnityEngine;

namespace MTPSKIT
{
    [Serializable] public sealed class HeroActionDefinition
    {
        public string id, name, description, icon, effect;
        public float cooldown, mana, power, radius, duration;
    }
    [Serializable] public sealed class HeroPassiveDefinition
    {
        public string id, name, description, icon;
        public float regeneration, damage, critical, armor, health, speed, manaRegeneration;
    }
    [Serializable] public sealed class HeroDefinition
    {
        public int selection;
        public string name;
        public float maximumMana = 100, manaRegeneration = 1;
        public string[] q, e, v, passives;
    }
    [Serializable] public sealed class HeroLoadoutData
    {
        public HeroDefinition[] heroes;
        public HeroActionDefinition[] actions;
        public HeroPassiveDefinition[] passives;
    }
    public static class HeroLoadoutCatalog
    {
        static HeroLoadoutData cached;
        public static HeroLoadoutData Data => cached ??= JsonUtility.FromJson<HeroLoadoutData>(
            Resources.Load<TextAsset>("HeroLoadouts").text);
        public static HeroDefinition Hero(int choice)
        {
            foreach (var hero in Data.heroes) if (hero.selection == choice) return hero;
            return null;
        }
        public static HeroActionDefinition Action(string id)
        {
            foreach (var item in Data.actions) if (item.id == id) return item;
            return null;
        }
        public static HeroPassiveDefinition Passive(string id)
        {
            foreach (var item in Data.passives) if (item.id == id) return item;
            return null;
        }
        public static string[] Options(int hero, int slot)
        {
            var definition = Hero(hero);
            if (definition == null) return Array.Empty<string>();
            return slot == 0 ? definition.q : slot == 1 ? definition.e : slot == 2 ? definition.v : definition.passives;
        }
        public static string Valid(int hero, int slot, string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var options = Options(hero, slot);
            return options != null && Array.IndexOf(options, id) >= 0 ? id : "";
        }
        public static string Saved(int hero, int slot) => Valid(hero, slot,
            PlayerPrefs.GetString("HeroLoadout." + hero + "." + slot, ""));
        public static void Save(int hero, int slot, string id)
        {
            PlayerPrefs.SetString("HeroLoadout." + hero + "." + slot, Valid(hero, slot, id));
            PlayerPrefs.Save();
        }
        public static string AuraName(int hero, int option) => hero switch
        {
            12 => option == 0 ? "生命与法力恢复" : "技能加速",
            13 => option == 0 ? "金币补给" : "轻装行军",
            14 => option == 0 ? "全队攻击" : "全队暴击",
            15 => option == 0 ? "全队防御" : "全队生命",
            _ => "无角色光环"
        };
        public static string AuraDescription(int hero, int option) => hero switch
        {
            12 => option == 0 ? "全队每秒生命恢复 +1，法力恢复 +1。" : "全队角色技能与特性技能冷却时间 -10%。",
            13 => option == 0 ? "每位存活队员每秒获得1金币。" : "全队移动速度 +1%。",
            14 => option == 0 ? "全队攻击力 +10%。" : "全队暴击率增加10个百分点。",
            15 => option == 0 ? "全队总护甲 +10%；不是伤害减免10%，零护甲仍为零。" : "全队最大生命值 +10%；切换不额外治疗。",
            _ => "当前角色无全队光环。"
        };
        public static int SavedAura(int hero) => Mathf.Clamp(PlayerPrefs.GetInt("HeroAura." + hero, 0), 0, 1);
    }
}
