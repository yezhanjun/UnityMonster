using System;
using System.Collections.Generic;
using System.Linq;
using MTPSKIT.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MTPSKIT.RainImported
{
    [Serializable] internal sealed class ReferenceJournalRow
    {
        public string key,group,name,summary,description,stats,asset;
    }
    [Serializable] internal sealed class ReferenceJournal { public ReferenceJournalRow[] entries; }
    internal sealed class BrowserEntry
    {
        public string id,name,description,stats,icon,scope;
        public int category;
        public PickupRow pickup;
        public DroneSpec drone;
        public Sprite sprite;
        public Texture2D texture;
        public GameObject prefab;
        public Color color= new Color(.34f,.4f,.46f);
    }

    // One catalog and one browsing interaction for the journal and the GM selector.
    public sealed class JournalBrowser : MonoBehaviour
    {
        static readonly string[] Categories={"物品和装备","怪物","幸存者","武器","无人机","挑战（成就）","档案属性"};
        static readonly HashSet<string> WhiteboxVerifiedMonsters = new()
        {
            "beetle_big_sur",
            "beetle_desert_sur",
            "beetle_sur",
            "skeleton_1h_sur",
            "spider_sur_VarRegular",
            "spider_sur_VarInsane",
            "elf_spider_02_sur",
            "elf_spider_sur",
            "jotunn_stone_insane_sur",
            "jotunn_stone_sur",
            "elf_melee_insane_sur",
            "elf_melee_sur",
            "sentinel_alf_sur",
            "sentinel_veteran_alf_sur",
            "spider_alf_sur",
            "spider_alf_veteran_sur",
            "skeleton_goat_endless",
            "skeleton_goat_hel_form_sur_boss",
            "ghost_sur",
            "ghost_red_sur",
            "ghost_red_sur_bf",
            "ghost_red_following_sur",
            "ghost_svart_sur",
            "jotunn_alfheim_boss_sur",
            "ettin_endless",
            "ettin_hel_form_boss",
            "jotunn_muspel_sur",
            "jotunn_muspel_endless",
            "jotunn_muspel_hel_form_sur",
            "ghost_king_sur",
            "bird_noobj_sur",
            "bird_sur",
            "ele_mb_melee_champion_sur",
            "ele_mb_melee_sur",
            "ele_mb_plague_sur",
            "ele_mb_venom_sur",
            "ele_mb_mage_sur",
            "ele_mb_mage_v2_sur",
            "milipede_sur",
            "skeleton_mb_sphinx_sur",
            "milipede_insane_sur",
            "skeleton_guardian_sphinx_sur",
            "LEMURIAN_BODY",
            "BEETLEGUARD_BODY",
            "BEETLE_BODY",
            "GOLEM_BODY",
            "TITAN_BODY",
            "LEMURIANBRUISER_BODY",
            "WISP_BODY",
            "IMP_BODY",
            "JELLYFISH_BODY",
            "VERMIN_BODY",
            "FLYINGVERMIN_BODY",
            "HERMIT_CRAB_BODY",
            "BISON_BODY",
            "BEETLEQUEEN_BODY",
            "TITANGOLD_BODY",
            "GM_SANDY_GOLEM",
            "GM_SNOW_GOLEM",
            "GM_NATURE_GOLEM",
            "GUP_BODY",
            "GEEP_BODY",
            "GIP_BODY",
            "ACIDLARVA_BODY",
            "skeleton_goat_sur_boss_VarNormal",
            "jotunn_alfheim_boss_phase2_sur",
            "elf_ranged_sur",
            "skeleton_bow_sphinx",
            "LUNAREXPLODER_BODY",
            "GREATERWISP_BODY",
            "horror_expl_jotun_sur",
            "horror_expl_alf_sur",
            "ghost_skull_sur",
            "ettin_boss",
            "jotunn_warrior_sur",
            "jotunn_warrior_v2_sur",
            "hyb_boss_sur_VarEasy",
            "hyb_boss_sur_VarNormal",
            "hyb_boss_sur_VarHard",
            "PARENT_BODY",
            "CHILD_BODY",
            "BELL_BODY",
            "MINIMUSHROOM_BODY",
            "CLAYGRENADIER_BODY",
            "CLAYBRUISER_BODY",
            "elf_woman_sur",
            "elf_woman_02_sur",
            "skeleton_bow_VarRegular",
            "skeleton_guardian_sur",
            "skeleton_1h_sphinx_sur",
            "hyb_sub_boss_sur_VarEasy",
            "hyb_sub_boss_sur_VarNormal",
            "jotunn_construct_sur",
            "skeleton_goat_sur_boss_VarEasy",
        };
        // Configurations that passed the D3D11 training-room run.
        static readonly HashSet<string> RuntimeVerifiedMonsters = new()
        {
            "hyb_boss_sur_VarEasy",
            "hyb_boss_sur_VarNormal",
            "hyb_boss_sur_VarHard",
            "ettin_boss",
            "ettin_endless",
            "ettin_hel_form_boss",
            "jotunn_muspel_sur",
            "jotunn_muspel_endless",
            "jotunn_muspel_hel_form_sur",
            "bird_noobj_sur",
            "bird_sur",
            "ele_mb_mage_sur",
            "ele_mb_mage_v2_sur",
            "ele_mb_plague_sur",
            "ele_mb_venom_sur",
            "elf_woman_sur",
            "elf_woman_02_sur",
            "ghost_king_sur",
            "jotunn_warrior_sur",
            "jotunn_warrior_v2_sur",
            "ele_mb_melee_champion_sur",
            "ele_mb_melee_sur",
            "skeleton_mb_sphinx_sur",
            "LEMURIANBRUISER_BODY",
            "GOLEM_BODY",
            "TITAN_BODY",
            "TITANGOLD_BODY",
            "GM_SNOW_GOLEM",
            "GM_NATURE_GOLEM",
            "GM_SANDY_GOLEM",
            "HERMIT_CRAB_BODY",
            "GREATERWISP_BODY",
            "beetle_big_sur",
            "beetle_desert_sur",
            "beetle_sur",
            "skeleton_1h_sur",
            "milipede_sur",
            "skeleton_bow_VarRegular",
            "spider_sur_VarRegular",
            "spider_sur_VarInsane",
            "elf_spider_02_sur",
            "elf_spider_sur",
            "jotunn_stone_insane_sur",
            "jotunn_stone_sur",
            "elf_ranged_sur",
            "elf_melee_insane_sur",
            "elf_melee_sur",
            "sentinel_alf_sur",
            "sentinel_veteran_alf_sur",
            "spider_alf_sur",
            "spider_alf_veteran_sur",
            "skeleton_guardian_sur",
            "skeleton_goat_sur_boss_VarEasy",
            "skeleton_goat_sur_boss_VarNormal",
            "skeleton_goat_endless",
            "skeleton_goat_hel_form_sur_boss",
            "ghost_sur",
            "ghost_red_sur",
            "ghost_red_sur_bf",
            "ghost_red_following_sur",
            "ghost_svart_sur",
            "ghost_skull_sur",
            "jotunn_alfheim_boss_sur",
            "jotunn_alfheim_boss_phase2_sur",
            "skeleton_1h_sphinx_sur",
            "skeleton_bow_sphinx",
            "skeleton_guardian_sphinx_sur",
            "horror_expl_jotun_sur",
            "horror_expl_alf_sur",
            "LEMURIAN_BODY",
            "BEETLEGUARD_BODY",
            "BEETLEQUEEN_BODY",
            "GUP_BODY",
            "GEEP_BODY",
            "GIP_BODY",
            "BEETLE_BODY",
            "WISP_BODY",
            "IMP_BODY",
            "JELLYFISH_BODY",
            "VERMIN_BODY",
            "FLYINGVERMIN_BODY",
            "BISON_BODY",
            "ACIDLARVA_BODY",
            "LUNAREXPLODER_BODY",
            "PARENT_BODY",
            "milipede_insane_sur",
        };
        static readonly HashSet<string> RuntimeVerifiedItemsAndGodSkills = new()
        {
            "Lightning",
            "HalfAttackSpeedHalfCooldowns",
            "AlienHead",
            "PowerOrbSphere",
            "AttackSpeedPerNearbyAllyOrEnemy",
            "SpeedBoostPickup",
            "SpeedOnPickup",
            "PowerCube",
            "ArmorPlate",
            "ArmorReductionOnHit",
            "AttackSpeedOnCrit",
            "AutoCastEquipment",
            "BarrierOnKill",
            "BarrierOnOverHeal",
            "Bear",
            "BearVoid",
            "MushroomVoid",
            "Mushroom",
            "Behemoth",
            "BleedOnHit",
            "BleedOnHitVoid",
            "BleedOnHitAndExplode",
            "BonusGoldPackOnKill",
            "BossDamageBonus",
            "ChainLightning",
            "ChainLightningVoid",
            "DeathMark",
            "CritGlasses",
            "Crowbar",
            "Clover",
            "EnergizedOnEquipmentUse",
            "EquipmentMagazine",
            "IceRing",
            "FireRing",
            "Dagger",
            "ExplodeOnDeath",
            "IgniteOnKill",
            "StickyBomb",
            "StunChanceOnHit",
            "Syringe",
            "NearbyDamageBonus",
            "SprintBonus",
            "Hoof",
            "FlatHealth",
            "CookedSteak",
            "FragileDamageBonus",
            "FragileDamageBonusConsumed",
            "HealingPotion",
            "HealingPotionConsumed",
            "HalfSpeedDoubleHealth",
            "AttackSpeedAndMoveSpeed",
            "HealWhileSafe",
            "IncreaseHealing",
            "ParentEgg",
            "Pearl",
            "LunarDagger",
            "OutOfCombatArmor",
            "SprintArmor",
            "PersonalShield",
            "MissileVoid",
            "Missile",
            "MoreMissile",
            "HealOnCrit",
            "Seed",
            "CritGlassesVoid",
            "IncreaseDamageOnMultiKill",
            "PermanentDebuffOnHit",
            "SlowOnHitVoid",
            "SlowOnHit",
            "Talisman",
            "Fruit",
            "GainArmor",
            "CritOnUse",
            "Medkit",
            "Tooth",
            "MoveSpeedOnKill",
            "Infusion",
            "ExplodeOnDeathVoid",
            "active_thor_D",
            "active_thor_A_evolve",
            "active_thor_B_evolve",
            "active_thor_E_evolve",
            "active_freya_C_evolve",
            "active_freya_A_evolve",
            "active_freya_E_evolve",
            "active_freya_D_evolve",
            "active_freya_B_evolve",
            "active_loki_E_evolve",
            "active_loki_B_evolve",
            "active_loki_A_evolve",
            "active_loki_D_evolve",
            "active_skadi_A_evolve",
            "active_skadi_B_evolve",
            "active_skadi_D_evolve",
            "active_skadi_C_evolve",
            "active_odin_A_evolve",
            "active_odin_B_evolve",
            "active_odin_C_evolve",
            "active_odin_D_evolve",
            "active_odin_E_evolve",
            "active_njord_C_evolve",
            "active_njord_D_evolve",
            "active_njord_B_evolve",
            "active_njord_A_evolve",
            "active_njord_E_evolve",
            "active_nidhogg_D_evolve",
            "active_nidhogg_E_evolve",
            "active_nidhogg_B_evolve",
            "active_nidhogg_A_evolve",
            "active_nidhogg_C_evolve",
        };
        static readonly string[] GmCategories={"物品和装备","怪物","幸存者（英雄）","武器","无人机","Buff","快速调试","神祇","怪物（巨人杀手）"};
        const int GodCategory = 7, JotunnMonsterCategory = 8;
        string[] VisibleCategories=>gm?GmCategories:Categories;
        readonly List<BrowserEntry> entries=new List<BrowserEntry>();
        readonly List<KeyValuePair<string,Action>> footerActions=new List<KeyValuePair<string,Action>>();
        LogbookCatalog catalog;
        Transform root,detail;
        TMP_Text hover;
        int category,pageIndex;
        int PageSize=>category==0?84:!gm&&category==5?12:32;
        Action close;
        Func<BrowserEntry,string> execute;
        Func<BrowserEntry,bool> available;
        Func<BrowserEntry,string> remove;
        BrowserEntry selected;
        string actionResult="";
        Action<bool> setCombat;
        Func<bool> getCombat;
        bool gm;
        bool jotunnMonsters;

        bool IsJotunnMonster(BrowserEntry entry) =>
            gm && entry != null && entry.category == JotunnMonsterCategory && Jotunnslayer.JotunnMonsterGm.Supports(entry.id);

        bool CanRunEntry(BrowserEntry entry) => gm && entry.category == GodCategory
            ? Jotunnslayer.GodSkillGm.CanTest(GameManager.myPlayerInstance ? GameManager.myPlayerInstance.MyCharacter : null)
            : IsJotunnMonster(entry)
            ? true // Let the spawn path explain host/connection failures instead of silently disabling the button.
            : available == null || available(entry);

        string RunEntry(BrowserEntry entry, bool removing)
        {
            if (gm && entry.category == GodCategory)
            {
                var owner = GameManager.myPlayerInstance ? GameManager.myPlayerInstance.MyCharacter : null;
                return removing ? Jotunnslayer.GodSkillGm.Stop(owner) : Jotunnslayer.GodSkillGm.Activate(owner, entry.id, false);
            }
            if (!IsJotunnMonster(entry)) return removing ? remove(entry) : execute(entry);
            var player = GameManager.myPlayerInstance;
            if (!player || !player.MyCharacter) return "需要存活房主角色。";
            if (removing)
                return Jotunnslayer.JotunnMonsterGm.Remove(player.MyCharacter, entry.id)
                    ? "已移除该配置生成的怪物。" : "当前没有该配置的怪物。";
            return Jotunnslayer.JotunnMonsterGm.TrySpawn(player.MyCharacter, entry.id)
                ? "怪物已召唤，当前待机。关闭 GM 查看，点击战斗开始。" : "召唤失败：" + (Jotunnslayer.Skeleton1HSpawn.LastSpawnError ?? "请检查角色状态与怪物配置。");
        }
        internal static JournalBrowser Open(Transform parent,Action onClose,Func<BrowserEntry,string> action=null,Func<BrowserEntry,bool> canExecute=null,Func<BrowserEntry,string> removeAction=null)
        {
            var host=FrontEndUI.Rect(parent,"JournalBrowser",0,0,1920,1080).gameObject;
            var view=host.AddComponent<JournalBrowser>();view.close=onClose;view.execute=action;view.available=canExecute;view.gm=action!=null;
            view.remove=removeAction;
            if(view.gm)
            {
                // Scale the whole browser, including detail pages, around the screen centre.
                var rect=(RectTransform)host.transform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                rect.anchoredPosition=new Vector2(-720,405);
                rect.localScale=Vector3.one*.75f;
            }
            view.catalog=new LogbookCatalog();view.Load();view.Draw();return view;
        }
        void Load()
        {
            if (gm)
                foreach (var skill in Jotunnslayer.GodSkillGm.Catalog)
                    entries.Add(new BrowserEntry{id=skill.id,name=skill.name,description=skill.description,stats=skill.stats,
                        category=GodCategory,texture=skill.icon,scope="巨人杀手神祇 · 技能效果测试"});
            foreach(var p in catalog.Entries)entries.Add(new BrowserEntry{id=p.catalogName,name=p.name,description=p.description,
                stats=p.family=="equipment"?$"主动装备 · 冷却 {p.cooldown} 秒":"被动物品 · 可叠加",icon=p.icon,pickup=p,category=0,
                scope="参考道具目录",color=p.family=="equipment"?new Color(.65f,.4f,.12f):p.tier==1?new Color(.3f,.55f,.18f):p.tier==2?new Color(.65f,.18f,.2f):new Color(.55f,.53f,.33f)});
            var reference=JsonUtility.FromJson<ReferenceJournal>(Resources.Load<TextAsset>("RainImported/RainJournal").text);
            foreach(var p in reference.entries.Where(e=>e.group=="怪物" || gm && (e.key is "GEEP_BODY" or "GIP_BODY")).GroupBy(e=>e.key).Select(g=>g.First()))
                entries.Add(new BrowserEntry{id=p.key,name=p.name,description=gm&&p.key=="TITANGOLD_BODY"?"使用黄金泰坦网格，沿用训练场泰坦的重拳和巨型激光。":string.IsNullOrEmpty(p.description)?"怪物战斗资料。基础属性与成长数值见下方。":p.description,stats=p.stats,category=1,scope="参考怪物资料",icon=p.key});
            if(gm)
            {
                entries.Add(new BrowserEntry{id="GM_SNOW_GOLEM",name="雪地石巨人",category=1,icon="GOLEM_BODY",description="雪地模型与材质。沿用训练场石巨人的追踪、锁定与激光结算。",scope="外观变体"});
                entries.Add(new BrowserEntry{id="GM_NATURE_GOLEM",name="苔岩石巨人",category=1,icon="GOLEM_BODY",description="苔岩模型与材质。沿用训练场石巨人的追踪、锁定与激光结算。",scope="外观变体"});
                entries.Add(new BrowserEntry{id="GM_SANDY_GOLEM",name="沙地石巨人",category=1,icon="GOLEM_BODY",description="共用石巨人网格与骨架，替换沙地材质和贴图。沿用追踪、锁定与激光结算。",scope="外观变体"});
            }
            foreach (int choice in RangeCharacterSelection.PlayerChoices)
                entries.Add(new BrowserEntry{id=RangeCharacterSelection.CatalogId(choice),name=RangeCharacterSelection.DisplayName(choice),
                    description="使用当前项目的移动、枪械、近战与完整布娃娃。",stats="在出战准备中选择",category=2,icon="texDefaultSocialUserIcon",sprite=RangeCharacterSelection.Portrait(choice),scope="当前项目角色"});
            foreach(var resource in Gameplay.FpsGunBinding.ResourceNames)
            {
                var prefab=Resources.Load<GameObject>(resource);if(!prefab)continue;
                var item=prefab.GetComponent<Gameplay.Item>();var gun=prefab.GetComponent<Gameplay.FpsGunBinding>();if(!item||!gun)continue;
                var s=gun.settings;entries.Add(new BrowserEntry{id=resource,name=item.ItemName,prefab=prefab,description="当前三维训练场使用的武器。",stats=$"伤害 {s.damage} · 射程 {s.range}\n弹匣 {s.magazineCapacity} · 装填 {s.reloadTime}秒",category=3,sprite=item.ItemIcon,scope="当前项目武器"});
            }
            if(DNNetworkManager.Instance)
                foreach(var prefab in DNNetworkManager.Instance.spawnPrefabs)
                {
                    if(!prefab)continue;
                    var item=prefab.GetComponent<Gameplay.Item>();
                    if(!item||entries.Any(e=>e.category==3&&e.name==item.ItemName))continue;
                    entries.Add(new BrowserEntry{id=prefab.name,name=item.ItemName,prefab=prefab,category=3,sprite=item.ItemIcon,description="原 TPS 武器，使用项目原有攻击与动画。",stats="装备后使用对应武器槽位",scope="当前项目武器"});
                }
            foreach(var d in DroneCatalog.Load().units)entries.Add(new BrowserEntry{id=d.id,name=d.name,description=$"技能：{d.skill}\n\n编队指令：{d.command}",stats=$"生命 {d.hp}（每级 +{d.hpGain}）\n伤害 {d.damage}（每级 +{d.damageGain}）\n移动速度 {d.speed}",category=4,drone=d,icon=(d.bodyToken??"").Replace("_NAME",""),scope="参考无人机配置"});
            if(gm)
            {
                entries.Add(new BrowserEntry{id=RangeTestBuffs.Lifesteal,name="吸血",category=5,icon="texToothNecklaceIcon",description="对敌人造成实际伤害时，恢复实际伤害的 20%。",stats="30 秒 · 重复添加刷新时间",scope="暗黑之潮机制参考 · 训练场参数"});
                entries.Add(new BrowserEntry{id=RangeTestBuffs.Regeneration,name="生命再生",category=5,icon="texSteakIcon",description="每秒恢复 5 点生命。",stats="30 秒 · 重复添加刷新时间",scope="暗黑之潮机制参考 · 训练场参数"});
                entries.Add(new BrowserEntry{id=RangeTestBuffs.DamageReduction,name="伤害减免",category=5,icon="texRepulsionArmorPlateIcon",description="承伤降低 25%，再结算固定减伤和屏障。",stats="30 秒 · 重复添加刷新时间",scope="暗黑之潮机制参考 · 训练场参数"});
            }
            bool firstClear = false;
            string profileStatus = "本地档案";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (SteamRoomSession.Instance)
            {
                firstClear = SteamRoomSession.Instance.RainFirstClear;
                profileStatus = SteamRoomSession.Instance.RainProfileStatus;
            }
#endif
            string clearStatus = firstClear ? "已解锁" : "未解锁";
            if (!gm)
            {
                entries.Add(new BrowserEntry{id="TPS_FIRST_CLEAR",name="首次击败泰坦",description="完成一局雨中冒险并击败石之泰坦。",
                    stats=clearStatus,category=5,icon="texUnlockIconSmall",scope="当前项目成就"});
                foreach(var c in catalog.Table.extras.Where(e=>e.kind=="challenge"&&!string.IsNullOrEmpty(e.name)))
                    entries.Add(new BrowserEntry{id=c.key,name=c.name,description=c.info,stats="成就条件参考",category=5,icon="texUnlockIconSmall",scope="挑战资料"});
            }
            if(gm)entries.Add(new BrowserEntry{id=RangeResearchLab.InvulnerabilityEntry,name="无敌",description="当前房主角色免疫伤害。可随时关闭；换角色或重生后重新开启。",category=6,icon="texRepulsionArmorPlateIcon",scope="训练场快速调试"});
            if (gm && GameManager.Gamemode is Gameplay.Gamemodes.RangeGamemode rain && rain.IsRainCoop)
                entries.Add(new BrowserEntry{id=RangeResearchLab.ExperienceEntry,name="经验 +20",description="给房主添加 20 点个人经验，用于验证等级与生命成长。",
                    category=6,icon="texUnlockIconSmall",scope="雨中冒险快速调试"});
            if (!gm) entries.Add(new BrowserEntry{id="profile",name="当前档案",description="永久进度与平台同步状态。",
                stats="首次击败泰坦：" + clearStatus + "\n" + profileStatus,category=6,icon="texDefaultSocialUserIcon",scope="玩家档案"});
        }
        void Clear(ref Transform target){if(target){target.gameObject.SetActive(false);Destroy(target.gameObject);target=null;}}
        void Draw()
        {
            Clear(ref root);root=FrontEndUI.Rect(transform,"Catalog",0,0,1920,1080);
            FrontEndUI.Box(root,"Background",0,0,1920,1080,new Color(.008f,.012f,.019f));
            FrontEndUI.Text(root,gm?"GM · 测试图鉴":"日志",95,65,1400,70,48);
            var tabs=VisibleCategories;float tabStride=1736f/tabs.Length;
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;
                var button=FrontEndUI.Button(root,tabs[i],95+i*tabStride,150,tabStride-12,48,()=>{category=tab;pageIndex=0;selected=null;actionResult="";Draw();},i==category?FrontEndUI.Gold:(Color?)null);
                var title=button.GetComponentInChildren<TMP_Text>();title.enableAutoSizing=true;title.fontSizeMin=14;title.fontSizeMax=25;title.textWrappingMode=TextWrappingModes.NoWrap;
            }
            var rows = entries.Where(entry => entry.category == category)
                .OrderByDescending(entry => gm && WhiteboxVerifiedMonsters.Contains(entry.id))
                .ThenByDescending(entry => gm && (RuntimeVerifiedMonsters.Contains(entry.id) ||
                    RuntimeVerifiedItemsAndGodSkills.Contains(entry.id)))
                .ThenByDescending(entry => gm && CanRunEntry(entry))
                .ToArray();
            int pages = Mathf.Max(1, (rows.Length + PageSize - 1) / PageSize);
            pageIndex = Mathf.Clamp(pageIndex, 0, pages - 1);
            FrontEndUI.Text(root,tabs[category],125,240,950,55,32);
            FrontEndUI.Text(root,$"{pageIndex+1} / {pages}   ·   {rows.Length} 项",980,240,280,55,22);
            FrontEndUI.Button(root,"<",1260,245,52,45,()=>{pageIndex=Mathf.Max(0,pageIndex-1);Draw();});
            FrontEndUI.Button(root,">",1325,245,52,45,()=>{pageIndex=Mathf.Min(pages-1,pageIndex+1);Draw();});
            FrontEndUI.Box(root,"Grid frame",125,315,1255,600,new Color(.12f,.17f,.21f));
            FrontEndUI.Box(root,"Grid",130,320,1245,590,Color.black);
            hover=FrontEndUI.Text(root,selected!=null?Describe(selected):"选择条目查看详情",1420,330,370,gm?385:550,26,TextAlignmentOptions.TopLeft);
            hover.enableAutoSizing=true;hover.fontSizeMin=16;hover.fontSizeMax=26;
            if(!gm&&category==6)
            {
                var profile=entries.Find(e=>e.category==6);profile.name=SteamRoomSession.Instance?SteamRoomSession.Instance.PersonaName:"本地档案";
                FrontEndUI.Button(root,profile.name+"\n查看档案属性",150,350,590,95,()=>ShowProfile(profile));
            }
            for(int i=pageIndex*PageSize;i<Mathf.Min(rows.Length,(pageIndex+1)*PageSize);i++)
            {
                if(!gm&&category==6)break;
                var entry=rows[i];int cell=i%PageSize;int columns=category==0?14:8;float width=category==0?81:145,height=category==0?81:130;
                if(!gm&&category==5)
                {
                    float cx=150+(cell%2)*600,cy=350+(cell/2)*90;
                    var challenge=FrontEndUI.Button(root,"",cx,cy,590,82,()=>ShowDetail(entry));Picture(challenge.transform,entry,3,3,76);
                    FrontEndUI.Text(challenge.transform,entry.name,90,4,490,30,22);
                    var condition=FrontEndUI.Text(challenge.transform,entry.description,90,35,490,42,18);condition.enableAutoSizing=true;condition.fontSizeMin=12;condition.fontSizeMax=18;
                    continue;
                }
                float x=150+(cell%columns)*(width+5),y=350+(cell/columns)*(height+6);
                var button=FrontEndUI.Button(root,"",x,y,width,height,()=>{if(gm){selected=entry;actionResult="";Draw();}else ShowDetail(entry);},entry.color);
                if(gm&&selected==entry)
                {
                    var border=button.gameObject.AddComponent<Outline>();border.effectColor=Color.yellow;border.effectDistance=new Vector2(3,3);
                }
                Picture(button.transform,entry,(width-(height-10))/2,5,height-10);
                if(category!=0){FrontEndUI.Box(button.transform,"Caption",0,height-31,width,31,new Color(0,0,0,.7f));var title=FrontEndUI.Text(button.transform,entry.name,3,height-31,width-6,31,18,TextAlignmentOptions.Center);title.enableAutoSizing=true;title.fontSizeMin=12;title.fontSizeMax=18;}
                var hint=button.gameObject.AddComponent<JournalHover>();hint.enter=()=>{if(!gm||selected==null)hover.text=Describe(entry);};
            }
            if(gm)
            {
                bool enabled=selected!=null&&CanRunEntry(selected);
                bool grantExperience = selected?.id == RangeResearchLab.ExperienceEntry;
                bool godControls = category == GodCategory;
                bool monsterControls = category == 1 || category == JotunnMonsterCategory;
                FrontEndUI.Button(root,grantExperience?"添加经验":godControls?"启用基础":category==6?"开启":monsterControls?"召唤":category==5?"添加 Buff":"装备",1420,760,175,60,enabled?()=>{actionResult=RunEntry(selected, removing: false);Draw();}:null);
                if (!grantExperience)
                    FrontEndUI.Button(root,godControls?"停用技能":category==6?"关闭":monsterControls||category==5?"移除":"拆卸",1610,760,175,60,enabled&&remove!=null?()=>{actionResult=RunEntry(selected, removing: true);Draw();}:null);
                if (godControls)
                    FrontEndUI.Button(root,"启用进化",1420,830,365,48,enabled?()=>{
                        var owner=GameManager.myPlayerInstance?GameManager.myPlayerInstance.MyCharacter:null;
                        actionResult=Jotunnslayer.GodSkillGm.Activate(owner,selected.id,true);Draw();}:null);
                bool combatControls=monsterControls&&setCombat!=null;
                if(combatControls)
                {
                    bool jotunn = category == JotunnMonsterCategory;
                    var owner=GameManager.myPlayerInstance?GameManager.myPlayerInstance.MyCharacter:null;
                    bool fighting=jotunn?Jotunnslayer.JotunnMonsterGm.IsFighting(owner):getCombat();
                    FrontEndUI.Button(root,"待机",1420,830,175,48,()=>{if(jotunn)Jotunnslayer.JotunnMonsterGm.SetCombat(owner,false);else setCombat(false);Draw();},!fighting?FrontEndUI.Gold:(Color?)null);
                    FrontEndUI.Button(root,"战斗",1610,830,175,48,()=>{if(jotunn)Jotunnslayer.JotunnMonsterGm.SetCombat(owner,true);else setCombat(true);Draw();},fighting?FrontEndUI.Gold:(Color?)null);
                }
                var status=FrontEndUI.Text(root,selected!=null&&!enabled?"此条目暂未接入三维测试操作":actionResult,1420,combatControls||godControls?890:835,370,combatControls||godControls?90:120,20,TextAlignmentOptions.TopLeft);
                status.enableAutoSizing=true;status.fontSizeMin=14;status.fontSizeMax=20;
            }
            FrontEndUI.Button(root,gm?"关闭（=）":"返回",95,995,220,48,Close);
            for(int i=0;i<footerActions.Count;i++){var command=footerActions[i];FrontEndUI.Button(root,command.Key,350+i*375,995,355,48,command.Value);}
        }
        internal void AddFooterAction(string label,Action action){footerActions.Add(new KeyValuePair<string,Action>(label,action));Draw();}
        internal void SetCombatControls(Func<bool> current, Action<bool> change)
        {
            getCombat = current;
            setCombat = change;
            if (!jotunnMonsters)
            {
                jotunnMonsters = true;
                foreach (var entry in Jotunnslayer.JotunnMonsterGm.Catalog)
                {
                    if (entry.id == "bat_loot_sur") continue;
                    entries.Add(new BrowserEntry
                    {
                        id = entry.id,
                        name = entry.id switch { "skeleton_1h_sur" => "近战骷髅", "milipede_sur" => "蜈蚣", "spider_sur_VarInsane" => "疯狂蜘蛛", _ => entry.id },
                        category = JotunnMonsterCategory,
                        scope = "巨人杀手 · 单人训练场",
                        description = entry.id == "skeleton_1h_sur" ? "近战骷髅样板。召唤后默认待机，切换战斗后追踪并挥砍玩家。" : entry.id == "milipede_sur" ? "召唤后默认待机，切换战斗后测试近战与吐弹。" : "召唤后默认待机，切换战斗后测试该配置的行为。",
                        stats = "召唤／移除 · 待机／战斗\n" + entry.id
                    });
                }
            }
            Draw();
        }
        static string Describe(BrowserEntry entry)=>entry.name+"\n\n"+Clean(entry.description)+"\n\n"+entry.stats+
            (entry.drone!=null?"\n\n三维训练版 · F7 编队指令\n"+(Gameplay.RangeDrone.ModelFor(entry.id)!=entry.id?"复用模型："+Gameplay.RangeDrone.ModelFor(entry.id):"已接入对应模型")+"\n部分技能为简化测试实现，非原作完整复刻。":"");
        public void Close(){gameObject.SetActive(false);Destroy(gameObject);var callback=close;close=null;callback?.Invoke();}
        internal static string Clean(string value)=>System.Text.RegularExpressions.Regex.Replace(value??"","</?style[^>]*>","");
        void Picture(Transform parent,BrowserEntry entry,float x,float y,float size)
        {
            if(entry.texture){var raw=FrontEndUI.Rect(parent,"Icon",x,y,size,size).gameObject.AddComponent<RawImage>();raw.texture=entry.texture;raw.raycastTarget=false;return;}
            if(entry.sprite){var im=FrontEndUI.Rect(parent,"Icon",x,y,size,size).gameObject.AddComponent<Image>();im.sprite=entry.sprite;im.preserveAspect=true;im.raycastTarget=false;return;}
            var texture=catalog.Icon(entry.icon);
            if(!texture&&entry.id=="DTHaulerDrone")texture=catalog.Icon("DRONE_HAULER_BODY");
            if(!texture&&entry.id=="DTHealDrone")texture=catalog.Icon("DRONE_HEALING_BODY");
            if(!texture)texture=Resources.Load<Texture2D>("FrontEndReference/"+entry.icon);
            if(texture){var raw=FrontEndUI.Rect(parent,"Icon",x,y,size,size).gameObject.AddComponent<RawImage>();raw.texture=texture;raw.raycastTarget=false;}
            else FrontEndUI.Text(parent,entry.name.Length>2?entry.name.Substring(0,2):entry.name,x,y,size,size,22,TextAlignmentOptions.Center);
        }
        void ShowDetail(BrowserEntry entry)
        {
            Clear(ref detail);detail=FrontEndUI.Rect(transform,"Detail",0,0,1920,1080);
            FrontEndUI.Box(detail,"Background",0,0,1920,1080,new Color(.008f,.012f,.019f));
            FrontEndUI.Box(detail,"Information",95,85,1040,865,FrontEndUI.Panel);
            Picture(detail,entry,120,105,70);FrontEndUI.Text(detail,entry.name,210,105,850,60,34);
            FrontEndUI.Text(detail,VisibleCategories[entry.category]+" · "+entry.scope,210,163,850,40,20);
            FrontEndUI.Box(detail,"Description",125,230,980,255,new Color(.25f,.31f,.36f));
            var desc=FrontEndUI.Text(detail,"描述：\n"+Clean(entry.description),145,250,940,215,25,TextAlignmentOptions.TopLeft);desc.enableAutoSizing=true;desc.fontSizeMin=16;desc.fontSizeMax=25;
            FrontEndUI.Box(detail,"Stats",125,500,980,270,new Color(.2f,.26f,.31f));
            string stats = entry.stats;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!gm && SteamRoomSession.Instance)
            {
                string logId = RainMonsterLogs.ForJournal(entry.id);
                if (logId != null)
                    stats += SteamRoomSession.Instance.HasRainMonsterLog(logId)
                        ? "\n图鉴日志：已解锁" : "\n图鉴日志：未解锁";
            }
#endif
            FrontEndUI.Text(detail,stats,145,520,940,230,25,TextAlignmentOptions.TopLeft);
            FrontEndUI.Box(detail,"Preview",1170,85,650,865,new Color(.08f,.035f,.045f));Picture(detail,entry,1300,290,390);
            FrontEndUI.Text(detail,entry.name,1200,760,590,70,32,TextAlignmentOptions.Center);
            if(gm){var result=FrontEndUI.Text(detail,"",145,850,950,70,22);bool enabled=available==null||available(entry);
                FrontEndUI.Button(detail,enabled?"执行测试操作":"此条目暂未接入测试操作",145,785,910,55,enabled?()=>{result.text=execute(entry);}:null);}
            FrontEndUI.Button(detail,"返回图鉴",95,995,250,48,()=>Clear(ref detail));
        }
        void ShowProfile(BrowserEntry entry)
        {
            Clear(ref detail);detail=FrontEndUI.Rect(transform,"Profile",0,0,1920,1080);
            FrontEndUI.Box(detail,"Background",0,0,1920,1080,Color.black);
            FrontEndUI.Box(detail,"Frame",95,85,1730,865,FrontEndUI.Panel);
            Picture(detail,entry,120,105,70);FrontEndUI.Text(detail,entry.name+"\n档案属性",215,105,1000,80,28);
            FrontEndUI.Box(detail,"Completion",145,220,1620,45,new Color(.06f,.07f,.09f));
            FrontEndUI.Text(detail,"总计完成       —     （解锁统计尚未接入）",165,220,1550,45,24);
            string[][] labels={new[]{"记录","最长存活时间","完成的最大关卡数","最高等级","造成的最高伤害","收集的最多金币","最多持有物品","游戏方式","最常使用角色","喜爱的装备","完成","挑战完成","发现的物品和装备","幸存者已游玩"},
                new[]{"总计","游戏已游玩","存活时间","被击败","获胜次数","击败","击败精英怪物","造成的伤害","已完成关卡","受到的伤害","生命值已恢复","已收集的金币","购买","无人机已修复"}};
            for(int col=0;col<2;col++)
            {
                float x=145+col*550;FrontEndUI.Box(detail,"Column",x,285,535,615,new Color(.07f,.09f,.12f));
                for(int row=0;row<labels[col].Length;row++)
                {
                    bool header=row==0||col==0&&(row==7||row==10);float y=290+row*42;
                    FrontEndUI.Box(detail,"Row",x+5,y,525,39,header?new Color(.13f,.11f,.17f):new Color(.11f,.14f,.18f));
                    FrontEndUI.Text(detail,labels[col][row],x+15,y,365,39,22);
                    if(!header)FrontEndUI.Text(detail,"—",x+390,y,120,39,22);
                }
            }
            FrontEndUI.Box(detail,"Survivor statistics",1250,285,515,615,new Color(.07f,.09f,.12f));
            FrontEndUI.Text(detail,"幸存者属性",1280,300,455,50,28,TextAlignmentOptions.Center);
            FrontEndUI.Text(detail,"获胜次数",1280,390,455,55,26,TextAlignmentOptions.Center);
            FrontEndUI.Box(detail,"Empty distribution",1320,475,360,360,new Color(.3f,.34f,.4f),"texUICircleOutline");
            FrontEndUI.Text(detail,"暂无对局数据",1330,580,340,120,26,TextAlignmentOptions.Center);
            FrontEndUI.Button(detail,"返回图鉴",95,995,250,48,()=>Clear(ref detail));
        }
    }
    public sealed class JournalHover : MonoBehaviour,IPointerEnterHandler
    {
        public Action enter;
        public void OnPointerEnter(PointerEventData eventData)=>enter?.Invoke();
    }
}
