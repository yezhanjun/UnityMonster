using UnityEngine;
using Mirror;
using TMPro;
namespace MTPSKIT.Gameplay
{
    public sealed class RangeResearchLab : MonoBehaviour
    {
        internal const string InvulnerabilityEntry = "gm_invulnerability";
        internal const string ExperienceEntry = "gm_rain_experience";
        bool rain;
        PlayerInstance localPlayer;
        Canvas canvas;
        TMP_Text equipmentStatus;
        int displayedFruitCooldown = -2;
        RangeResearchItems.Equipment displayedEquipment;
        bool displayedAutoActivation;


        CharacterInstance character;
        RangeResearchItems items;
        bool opened, inputWasEnabled;
        bool fullBench;
        bool monstersFighting;
        RainImported.JournalBrowser browser;
        RainImported.FullBuildBench simulator;
        float refresh;
        void Start()
        {
            rain = GetComponent<Gamemodes.RangeGamemode>().IsRainCoop;
            if (rain && !NetworkServer.active) { enabled = false; return; }
            canvas=FrontEndUI.Canvas(transform,110);
            FrontEndUI.Text(canvas.transform,"= 打开 GM",95,770,450,35,20);
            equipmentStatus = FrontEndUI.Text(canvas.transform, "", 95, 805, 450, 35, 20);
        }
        internal static string MonsterVariant(string id) => id switch
        {
            "GM_SNOW_GOLEM" => "mdlSnowyGolem",
            "GM_NATURE_GOLEM" => "mdlNatureGolem",
            "GM_SANDY_GOLEM" => RangeResearchEnemy.SandyGolemVariant,
            "TITANGOLD_BODY" => RangeResearchEnemy.GoldTitanVariant,
            _ => null
        };
        bool CanExecuteCatalog(RainImported.BrowserEntry entry)=>entry.id==InvulnerabilityEntry||(rain&&entry.id==ExperienceEntry)||RangeResearchItems.EquipmentForCatalog(entry.id)!=RangeResearchItems.Equipment.None||MonsterVariant(entry.id)!=null||RangeTestBuffs.Supports(entry.id)||RangeResearchItems.CatalogIndex(entry.id)>=0||entry.id=="BEETLE_BODY"||entry.id=="GOLEM_BODY"||entry.id=="TITAN_BODY"||IsGun(entry)||RangeCharacterSelection.TryGetSelection(entry.id,out _)||entry.drone!=null;
        static int AdditionalMonsterKind(string id) => id switch
        {
            "CHILD_BODY" => RangeResearchEnemy.ChildKind,
            "CLAYGRENADIER_BODY" => RangeResearchEnemy.ClayGrenadierKind,
            "CLAYBRUISER_BODY" => RangeResearchEnemy.ClayBruiserKind,
            "MINIMUSHROOM_BODY" => RangeResearchEnemy.MiniMushroomKind,
            "BELL_BODY" => RangeResearchEnemy.BellKind,
            "GUP_BODY" => RangeResearchEnemy.GupKind,
            "GEEP_BODY" => RangeResearchEnemy.GeepKind,
            "GIP_BODY" => RangeResearchEnemy.GipKind,
            "BEETLEGUARD_BODY" => RangeResearchEnemy.BeetleGuardKind,
            "LEMURIAN_BODY" => RangeResearchEnemy.LemurianKind,
            "WISP_BODY" => RangeResearchEnemy.WispKind,
            "GREATERWISP_BODY" => RangeResearchEnemy.GreaterWispKind,
            "IMP_BODY" => RangeResearchEnemy.ImpKind,
            "BISON_BODY" => RangeResearchEnemy.BisonKind,
            "VERMIN_BODY" => RangeResearchEnemy.VerminKind,
            "FLYINGVERMIN_BODY" => RangeResearchEnemy.FlyingVerminKind,
            "HERMIT_CRAB_BODY" => RangeResearchEnemy.HermitCrabKind,
            "ACIDLARVA_BODY" => RangeResearchEnemy.AcidLarvaKind,
            "LUNAREXPLODER_BODY" => RangeResearchEnemy.LunarExploderKind,
            "PARENT_BODY" => RangeResearchEnemy.ParentKind,
            "JELLYFISH_BODY" => RangeResearchEnemy.JellyfishKind,
            "LEMURIANBRUISER_BODY" => RangeResearchEnemy.BruiserKind,
            "BEETLEQUEEN_BODY" => RangeResearchEnemy.QueenKind,
            _ => -1
        };

        static bool IsGun(RainImported.BrowserEntry entry)=>entry.category==3&&entry.prefab;
        internal string ExecuteCatalog(RainImported.BrowserEntry entry)
        {
            RefreshCharacter();
            if(!items||!character||character.CurrentHealth<=0)return "需要房主存活角色。";
            var equipment = RangeResearchItems.EquipmentForCatalog(entry.id);
            if (equipment != RangeResearchItems.Equipment.None)
            {
                if (!items.Equip(equipment)) return "装备失败。";
                RecordRainItems();
                return "已装备" + entry.name + (equipment == RangeResearchItems.Equipment.ExecutiveCard
                    ? "。关卡购买返还 10%。" : "。关闭 GM 后按 F10 使用。");
            }
            if(entry.id==InvulnerabilityEntry)
            {
                character.RangeInvulnerable=true;
                return "无敌已开启。";
            }
            if (rain && entry.id == ExperienceEntry)
            {
                localPlayer.GrantRainExperience(20);
                return "已添加 20 点经验。";
            }
            if(RangeTestBuffs.Supports(entry.id))
            {
                if(!character.TryGetComponent<RangeTestBuffs>(out var buffs))
                    buffs=character.gameObject.AddComponent<RangeTestBuffs>();
                buffs.Apply(entry.id);
                return "已添加 Buff，持续 30 秒。";
            }
            if(entry.category==2&&RangeCharacterSelection.TryGetSelection(entry.id,out int heroSelection))return SwitchHero(heroSelection);
            if(entry.drone!=null)return RangeDrone.Recruit(character,entry.drone);
            if(IsGun(entry))return character.CharacterItemManager.ServerEquipTestWeapon(entry.prefab)?"已装备武器。":"没有可用武器槽位。";
            int itemIndex=RangeResearchItems.CatalogIndex(entry.id);
            if(itemIndex>=0){items.Grant(itemIndex);RecordRainItems();return "已添加至当前角色。";}
            string variant=MonsterVariant(entry.id);
            int kind = MonsterKind(entry.id, variant);
            if (kind < 0) return "该怪物尚未接入召唤。";
            if (!SpawnMonster(kind, variant)) return "召唤失败：前方没有足够的落脚空间。";
            return "怪物已召唤，当前待机。点击下方战斗开始。";
        }
        internal string RemoveCatalog(RainImported.BrowserEntry entry)
        {
            RefreshCharacter();
            if(!character||!NetworkServer.active)return "需要房主角色。";
            var equipment = RangeResearchItems.EquipmentForCatalog(entry.id);
            if (equipment != RangeResearchItems.Equipment.None)
            {
                if (!items || !items.Remove(equipment)) return "当前未装备该主动装备。";
                RecordRainItems();
                return "已拆卸" + entry.name + "。";
            }
            if(entry.id==InvulnerabilityEntry)
            {
                character.RangeInvulnerable=false;
                return "无敌已关闭。";
            }
            if(RangeTestBuffs.Supports(entry.id))
            {
                if(character.TryGetComponent<RangeTestBuffs>(out var buffs))buffs.Remove(entry.id);
                return "已清除对应 Buff。";
            }
            if(entry.category==2)return SwitchHero(0);
            if(entry.drone!=null)return RangeDrone.Recall(entry.id);
            if(IsGun(entry))return character.CharacterItemManager.ServerRemoveTestWeapon(entry.prefab)?"已拆卸武器。":"未装备该武器。";
            if(entry.category==0)
            {
                if (!items || !items.Remove(RangeResearchItems.CatalogIndex(entry.id))) return "当前未装备该道具。";
                RecordRainItems();
                return "已拆卸一层道具。";
            }
            string variant=MonsterVariant(entry.id);
            int kind = MonsterKind(entry.id, variant);
            if (kind < 0) return "该怪物尚未接入召唤。";
            foreach(var actor in FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None))
                if(IsTestMonster(actor) && actor.Archetype==kind && actor.ModelVariant==variant){actor.RemoveFromRange();return "已移除该实验怪物。";}
            return "当前没有该怪物。";
        }
        static int MonsterKind(string id, string variant)
        {
            int addedKind = AdditionalMonsterKind(id);
            if (addedKind >= 0) return addedKind;
            if (id == "BEETLE_BODY") return 0;
            if (id == "GOLEM_BODY" || (variant != null && variant != RangeResearchEnemy.GoldTitanVariant)) return 1;
            return id == "TITAN_BODY" || variant == RangeResearchEnemy.GoldTitanVariant ? 2 : -1;
        }

        internal void SetMonsterCombat(bool active)
        {
            monstersFighting=active;
            foreach(var actor in FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None))
                if (IsTestMonster(actor)) actor.SetCombat(active);
        }

        bool IsTestMonster(RangeResearchEnemy actor) =>
            !rain || actor.IsLabSummon || (actor.TryGetComponent<CharacterInstance>(out var body) && body.RainEnemyKind == 0);

        void RecordRainItems()
        {
            if (rain) localPlayer.RecordRainChestItems(items);
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F7))RangeDrone.CommandAll();
            if(Time.unscaledTime>=refresh){refresh=Time.unscaledTime+.25f;RefreshCharacter();}
            if(fullBench){if(browser&&browser.gameObject.activeInHierarchy&&Input.GetKeyDown(KeyCode.Equals))browser.Close();return;}
            if(Input.GetKeyDown(KeyCode.Equals)){Toggle();return;}
            var equipped = items ? items.Equipped : RangeResearchItems.Equipment.None;
            int cooldown = equipped != RangeResearchItems.Equipment.None ? Mathf.CeilToInt(items.EquipmentCooldownRemaining) : -1;
            bool autoActivation = items && items.EquipmentAutoActivates;
            if (cooldown == displayedFruitCooldown && equipped == displayedEquipment &&
                autoActivation == displayedAutoActivation) return;
            displayedFruitCooldown = cooldown;
            displayedEquipment = equipped;
            displayedAutoActivation = autoActivation;
            string equipmentName = equipped switch
            {
                RangeResearchItems.Equipment.Fruit => "外来水果",
                RangeResearchItems.Equipment.Elephant => "玉象",
                RangeResearchItems.Equipment.OcularHud => "眼部平视显示",
                RangeResearchItems.Equipment.Leech => "超巨型水蛭",
                RangeResearchItems.Equipment.TeamWarCry => "古拉格的杰作",
                RangeResearchItems.Equipment.Scanner => "雷达扫描装置",
                RangeResearchItems.Equipment.Capacitor => "皇家电容器",
                RangeResearchItems.Equipment.MissileLauncher => "一次性导弹发射器",
                RangeResearchItems.Equipment.BlastShower => "爆破雨阵",
                RangeResearchItems.Equipment.DroneBackup => "对讲机",
                RangeResearchItems.Equipment.Effigy => "悲伤雕像",
                RangeResearchItems.Equipment.ExecutiveCard => "钻石会员卡",
                _ => ""
            };
            if (cooldown < 0) equipmentStatus.text = "";
            else if (equipped == RangeResearchItems.Equipment.ExecutiveCard) equipmentStatus.text = equipmentName + " · 购买返还 10%";
            else if (cooldown == 0) equipmentStatus.text = autoActivation ? equipmentName + "自动激活" : "F10 使用" + equipmentName;
            else equipmentStatus.SetText(equipmentName + "冷却 {0} 秒", cooldown);
        }
        string SwitchHero(int selection)
        {
            var local=NetworkClient.localPlayer?NetworkClient.localPlayer.GetComponent<PlayerInstance>():null;
            if(!local||!local.ServerSetRangeHero(selection))return "角色切换失败：模型未就绪。";
            RangeCharacterSelection.SetSelected(selection);
            return "已切换为 " + RangeCharacterSelection.DisplayName(selection) + "。";
        }
        void RefreshCharacter()
        {
            localPlayer=NetworkClient.localPlayer?NetworkClient.localPlayer.GetComponent<PlayerInstance>():null;
            character=localPlayer?localPlayer.MyCharacter:null;
            if(character && NetworkServer.active){items=character.GetComponent<RangeResearchItems>();if(!items)items=character.gameObject.AddComponent<RangeResearchItems>();}
            else items=null;
        }
        void Toggle()
        {
            if(!opened)UI.HUD.ReferenceBattleHud.CloseInspection();
            opened=!opened;
            if(opened&&character)character.SetMovementInput(Vector2.zero);
            if(PlayerGameplayInput.Instance)
            {
                if(opened){inputWasEnabled=PlayerGameplayInput.Instance.enabled;PlayerGameplayInput.Instance.enabled=false;}
                else PlayerGameplayInput.Instance.enabled=inputWasEnabled;
            }
            UI.ClientFrontend.ShowCursor(opened);
            if(opened)
            {
                fullBench=true;
                browser=RainImported.JournalBrowser.Open(canvas.transform,()=>{fullBench=false;browser=null;if(opened)Toggle();},ExecuteCatalog,entry=>AdditionalMonsterKind(entry.id)>=0||CanExecuteCatalog(entry),RemoveCatalog);
                browser.SetCombatControls(()=>monstersFighting,SetMonsterCombat);
                browser.AddFooterAction("清空道具",()=>Select(3));
                browser.AddFooterAction("停止实验怪物",()=>Select(7));
                browser.AddFooterAction("全道具效果模拟台",()=>
                {
                    browser.gameObject.SetActive(false);
                    simulator=RainImported.FullBuildBench.Open(()=>{if(browser)browser.gameObject.SetActive(true);simulator=null;});
                });
            }
        }
        bool Select(int choice)
        {
            if(choice==7)
            {
                SetMonsterCombat(false);
                Jotunnslayer.JotunnMonsterGm.Clear(character);
                foreach(var actor in FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None))
                    if (IsTestMonster(actor)) actor.RemoveFromRange();
                return true;
            }
            if(!items||!character||character.CurrentHealth<=0)return false;
            if(choice<3){items.Grant(choice);return true;}
            if (choice != 3) return false;
            items.Clear();
            RecordRainItems();
            return true;
        }

        bool SpawnMonster(int kind, string variant)
        {
            if (!NetworkServer.active) return false;
            var forward=Quaternion.Euler(0,character.lookInput.y,0)*Vector3.forward;
            var position=character.transform.position+forward*6;
            if(Physics.Raycast(character.transform.position+Vector3.up,forward,6,GameManager.environmentLayer))return false;
            if(!Physics.Raycast(position+Vector3.up*4,Vector3.down,out var ground,12,GameManager.environmentLayer))return false;
            position=ground.point+Vector3.up*.15f;
            if (RangeResearchEnemy.IsAirborneKind(kind))
            {
                float height = kind == RangeResearchEnemy.BellKind ? 1.8f : RangeResearchEnemy.WispHoverHeight;
                if (Physics.Raycast(position, Vector3.up, height + 2f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) return false;
                position += Vector3.up * height;
            }
            float spawnRadius = kind == RangeResearchEnemy.ParentKind ? RangeResearchEnemy.ParentBodyRadius : .45f;
            float spawnBottom = kind == RangeResearchEnemy.ParentKind ? spawnRadius : .5f;
            float spawnTop = kind == RangeResearchEnemy.ParentKind ? RangeResearchEnemy.ParentBodyHeight - spawnRadius : 1.5f;
            if(Physics.CheckCapsule(position+Vector3.up*spawnBottom,position+Vector3.up*spawnTop,spawnRadius,GameManager.environmentLayer))return false;
            if (RangeResearchEnemy.IsGupFamilyKind(kind))
            {
                SetMonsterCombat(false);
                return RangeResearchEnemy.SpawnGupFamily(character, kind, character.Team == 0 ? 1 : 0,
                    position, Quaternion.LookRotation(-forward), labSummon: true);
            }
            var bot=Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),position,Quaternion.LookRotation(-forward)).GetComponent<CharacterInstance>();
            foreach(var slot in bot.CharacterItemManager.Slots)slot.ItemOnSpawn=null;
            GameManager.Gamemode.ConfigurePlayerCharacter(bot);
            bot.Team=character.Team==0?1:0;
            bot.MaxHealth = bot.CurrentHealth = kind switch
            {
                2 => 1500,
                RangeResearchEnemy.BeetleGuardKind => RangeResearchEnemy.BeetleGuardHealth,
                RangeResearchEnemy.LemurianKind => RangeResearchEnemy.LemurianHealth,
                RangeResearchEnemy.WispKind => RangeResearchEnemy.WispHealth,
                RangeResearchEnemy.GreaterWispKind => RangeResearchEnemy.GreaterWispHealth,
                RangeResearchEnemy.ImpKind => RangeResearchEnemy.ImpHealth,
                RangeResearchEnemy.BisonKind => RangeResearchEnemy.BisonHealth,
                RangeResearchEnemy.VerminKind => RangeResearchEnemy.VerminHealth,
                RangeResearchEnemy.FlyingVerminKind => RangeResearchEnemy.FlyingVerminHealth,
                RangeResearchEnemy.HermitCrabKind => RangeResearchEnemy.HermitCrabHealth,
                RangeResearchEnemy.AcidLarvaKind => RangeResearchEnemy.AcidLarvaHealth,
                RangeResearchEnemy.LunarExploderKind => RangeResearchEnemy.LunarExploderHealth,
                RangeResearchEnemy.ParentKind => RangeResearchEnemy.ParentHealth,
                RangeResearchEnemy.JellyfishKind => RangeResearchEnemy.JellyfishHealth,
                RangeResearchEnemy.BruiserKind => RangeResearchEnemy.BruiserHealth,
                RangeResearchEnemy.QueenKind => RangeResearchEnemy.QueenHealth,
                _ => 300
            };
            if (kind >= RangeResearchEnemy.BeetleGuardKind) bot.RainEnemyKind = kind + 1;
            NetworkServer.Spawn(bot.gameObject);
            bot.SetAsBOT(true);
            SetMonsterCombat(false);
            var spawnedActor=bot.gameObject.AddComponent<RangeResearchEnemy>();
            spawnedActor.IsLabSummon = true;
            spawnedActor.Configure(character,kind,variant);
            spawnedActor.OwnsCharacter=true;
            return true;
        }
        void OnDestroy()
        {
            if(NetworkServer.active)RangeDrone.ClearAll();
            if(simulator)Destroy(simulator.gameObject);
            if(opened&&PlayerGameplayInput.Instance)PlayerGameplayInput.Instance.enabled=inputWasEnabled;
        }
    }
}
