using System.Collections.Generic;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public sealed class ReferenceBattleHud : MonoBehaviour
    {
        Image healthFill;
        TMP_Text healthValue, ammo, timer, objective;
        TMP_Text wallet, chestPrompt, location;
        string[] rainChestRewardNames;
        TMP_Text rainLevel;
        Image rainExperienceFill;
        RectTransform allyDronePanel;
        readonly List<(TMP_Text label, Image fill)> allyDroneRows = new List<(TMP_Text, Image)>();
        RectTransform rainRadar;
        RectTransform[] chestDots, beaconDots;
        Image[] scannerMarkers;
        bool[] scannerRevealed;
        AudioClip scannerFindChestAudio;
        Sprite scannerChestIcon, scannerBeaconIcon, scannerOutsideArrow;
        static readonly Color ScannerArrowColor = new Color(1f, .8313726f, .4470588f);
        RectTransform bossDot, playerDot;
        const float RadarScale = 2.25f;
        readonly List<Image> icons = new List<Image>();
        readonly List<Image> borders = new List<Image>();
        Gamemode mode;
        Canvas display;
        bool english;
        CombatInventoryPanel bag;
        CharacterItemManager inventory;
        bool inputHeld;
        float refreshAt;
        Sprite steak, glasses, tooth, plate, medkit, rounds, crowbar, bear, hoof, soda, egg, watch, brokenWatch, healingRack, slug, potion, emptyBottle, brooch, aegis, dagger;
        Sprite hexOfPain, hexOfFamine, cookedSteak, ultimateMeal, stew, leptonDaisy, hikingBoots, quickFix, whiteScrap, bolsteringLantern, elusiveAntlers, collector;
        Sprite syringe, stickyBomb, stunGrenade, behemoth, focusCrystal, wisp, warbanner, ukulele, gasoline, iceRing, fireRing, shieldGenerator, buckler, opal, bauble, seed;
        TMP_Text buffStatus;
        public static bool Active { get; private set; }
        static ReferenceBattleHud current;
        public static RectTransform MonsterOverlay => current && current.display ? (RectTransform)current.display.transform : null;
        public static void CloseInspection()
        {
            if (RainScrapperMenu.Current) RainScrapperMenu.Current.Close();
            if(current)current.CloseBag();
        }
        public void Initialize(TMP_FontAsset font)
        {
            FrontEndUI.Font = font;
            english = PlayerPrefs.GetInt("FrontEnd.English",0)!=0;
            // This prefab is nested below the legacy HUD. A nested Canvas inherits its small
            // RectTransform; use an owned root Canvas and mirror this component's lifetime.
            display=FrontEndUI.Canvas(null,20);
            var root=display.transform;
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var rangeLayout = activeScene == RoomCreator.TrainingScenePath
                ? GameObject.Find("Reference Room Layout")
                : activeScene == RoomCreator.ModularTrainingScenePath ? GameObject.Find("SciFi Training Facility") : null;
            float rightPanelOffset = rangeLayout ? 270f : 0f;
            if (rangeLayout)
                display.gameObject.AddComponent<TrainingRangeMinimap>().Initialize(root, rangeLayout.transform, english);
            Active=true;
            current=this;
            // The shot HUD has no wallet/XP systems yet; avoid displaying invented progression.
            FrontEndUI.Box(root,"Wallet",90,70,240,95,new Color(.04f,.055f,.08f,.68f),"texDetailPanel");
            var coin=FrontEndUI.Box(root,"Coin",103,83,30,30,FrontEndUI.Gold,"texCoinsIcon");coin.preserveAspect=true;
            wallet=FrontEndUI.Text(root,english?"Currency  —":"金币  —",145,78,175,35,24);
            chestPrompt=FrontEndUI.Text(root,"",560,865,800,44,23,TextAlignmentOptions.Center);
            location=FrontEndUI.Text(root,english?"Training Range":"训练场",108,123,210,28,18);
            FrontEndUI.Box(root,"Run",1540,70+rightPanelOffset,290,115,new Color(.04f,.055f,.08f,.72f),"texDetailPanel");
            FrontEndUI.Text(root,english?"COMBAT TEST":"战斗测试",1555,75+rightPanelOffset,260,32,24);
            timer=FrontEndUI.Text(root,"— : —",1555,105+rightPanelOffset,260,40,36,TextAlignmentOptions.MidlineRight);
            FrontEndUI.Box(root,"Difficulty",1550,150+rightPanelOffset,270,30,new Color(.18f,.48f,.1f),"texUIDifficultySegment");
            FrontEndUI.Text(root,english?"Normal":"普通",1550,150+rightPanelOffset,270,30,22,TextAlignmentOptions.Center);
            FrontEndUI.Box(root,"Objective panel",1540,195+rightPanelOffset,290,140,new Color(0,0,0,.4f));
            objective=FrontEndUI.Text(root,english?"Objective\nCombat practice":"目标\n□ 完成战斗训练",1555,215+rightPanelOffset,260,110,25);
            FrontEndUI.Box(root,"Health frame",95,948,475,54,new Color(.2f,.25f,.2f),"texUICombatHealthbarFrame");
            healthFill=FrontEndUI.Box(root,"Health",101,956,463,37,new Color(.32f,.75f,.12f),"texUICombatHealthbar");
            healthFill.type=Image.Type.Filled;healthFill.fillMethod=Image.FillMethod.Horizontal;healthFill.fillOrigin=0;
            healthValue=FrontEndUI.Text(root,"",101,953,463,40,23,TextAlignmentOptions.Center);
            FrontEndUI.Text(root,english?"HEALTH":"生命",95,909,300,35,22);
            var shield=FrontEndUI.Box(root,"Shield",95,860,46,46,Color.white,"texChevronEngiShield");shield.preserveAspect=true;
            FrontEndUI.Text(root,"Tab · 背包   Shift · 翻滚",1120,1008,300,30,18);
            ammo=FrontEndUI.Text(root,"",1480,845,380,55,32,TextAlignmentOptions.MidlineRight);
            for(int i=0;i<5;i++)
            {
                float x=1420+i*88;
                var bg=FrontEndUI.Box(root,"Slot "+i,x,929,78,78,new Color(.1f,.12f,.15f,.9f),"texDetailPanel");
                var border=FrontEndUI.Box(root,"Slot frame "+i,x,929,78,78,new Color(.6f,.6f,.65f),"texDetailPanelFrame");border.raycastTarget=false;
                var icon=FrontEndUI.Box(root,"Item "+i,x+8,937,62,62,Color.white);icon.preserveAspect=true;icon.enabled=false;
                icons.Add(icon);borders.Add(border);
                FrontEndUI.Text(root,(i+1).ToString(),x,1010,78,32,23,TextAlignmentOptions.Center);
            }
            GameManager.GameEvent_OnGamemodeSet+=BindMode;
            if(GameManager.Gamemode)BindMode(GameManager.Gamemode);
            bag=new GameObject("Combat inventory",typeof(RectTransform)).AddComponent<CombatInventoryPanel>();bag.Initialize(root);
            buffStatus=FrontEndUI.Text(root,"",95,815,640,40,22);
            plate=Resources.Load<Sprite>("RainImported/LogIcons/texRepulsionArmorPlateIcon");
            medkit=Resources.Load<Sprite>("RainImported/LogIcons/texMedkitIcon");
            crowbar=Resources.Load<Sprite>("RainImported/LogIcons/texCrowbarIcon");
            hoof=Resources.Load<Sprite>("RainImported/LogIcons/texHoofIcon");
            egg=Resources.Load<Sprite>("RainImported/LogIcons/texParentEggIcon");
            watch=Resources.Load<Sprite>("RainImported/LogIcons/texDelicateWatchIcon");
            healingRack=Resources.Load<Sprite>("RainImported/LogIcons/texAntlerIcon");
            potion=Resources.Load<Sprite>("RainImported/LogIcons/texHealingPotion");
            dagger=Resources.Load<Sprite>("RainImported/LogIcons/texTriTipIcon");
            syringe=Resources.Load<Sprite>("RainImported/LogIcons/texSyringeIcon");
            stickyBomb=Resources.Load<Sprite>("RainImported/LogIcons/texStickyBombIcon");
            stunGrenade=Resources.Load<Sprite>("RainImported/LogIcons/texStunGrenadeIcon");
            behemoth=Resources.Load<Sprite>("RainImported/LogIcons/texBehemothIcon");
            focusCrystal=Resources.Load<Sprite>("RainImported/LogIcons/texDiamondIcon");
            wisp=Resources.Load<Sprite>("RainImported/LogIcons/texWilloWispIcon");
            warbanner=Resources.Load<Sprite>("RainImported/LogIcons/texWarbannerIcon");
            ukulele=Resources.Load<Sprite>("RainImported/LogIcons/texUkuleleIcon");
            gasoline=Resources.Load<Sprite>("RainImported/LogIcons/texGasolineIcon");
            iceRing=Resources.Load<Sprite>("RainImported/LogIcons/texIceRingIcon");
            fireRing=Resources.Load<Sprite>("RainImported/LogIcons/texFireRingIcon");
            shieldGenerator=Resources.Load<Sprite>("RainImported/LogIcons/texPersonalShieldIcon");
            buckler=Resources.Load<Sprite>("RainImported/LogIcons/texBucklerIcon");
            opal=Resources.Load<Sprite>("RainImported/LogIcons/texOddlyShapedOpalIcon");
            bauble=Resources.Load<Sprite>("RainImported/LogIcons/texBaubleIcon");
            seed=Resources.Load<Sprite>("RainImported/LogIcons/texSeedIcon");
            hexOfPain=CreateInventoryIcon("texHexCurseIcon");
            hexOfFamine=CreateInventoryIcon("texLowerHealingIcon");
            cookedSteak=CreateInventoryIcon("texCookedSteakIcon");
            ultimateMeal=CreateInventoryIcon("texUltimateMealIcon");
            stew=CreateInventoryIcon("texStewIcon");
            leptonDaisy=CreateInventoryIcon("texGlowFlowerIcon");
            hikingBoots=CreateInventoryIcon("texCritAtLowerElevationIcon");
            quickFix=CreateInventoryIcon("texQuickFixIcon");
            whiteScrap=CreateInventoryIcon("texScrapWhiteIcon");
            bolsteringLantern=Resources.Load<Sprite>("RainImported/LogIcons/texRageCrystalIcon");
            elusiveAntlers=Resources.Load<Sprite>("RainImported/LogIcons/texElusiveAntlersIcon");
            collector=Resources.Load<Sprite>("RainImported/LogIcons/texSpeedOnPickupIcon");
            aegis=Resources.Load<Sprite>("RainImported/LogIcons/texAegisIcon");
            brooch=Resources.Load<Sprite>("RainImported/LogIcons/texBroochIcon");
            emptyBottle=Resources.Load<Sprite>("RainImported/LogIcons/texHealingPotionConsumed");
            slug=Resources.Load<Sprite>("RainImported/LogIcons/texSnailIcon");
            brokenWatch=Resources.Load<Sprite>("RainImported/LogIcons/texDelicateWatchBrokenIcon");
            soda=Resources.Load<Sprite>("RainImported/LogIcons/texSodaIcon");
            bear=Resources.Load<Sprite>("RainImported/LogIcons/texBearIcon");
            rounds=Resources.Load<Sprite>("RainImported/LogIcons/texAPRoundsIcon");
            steak=Resources.Load<Sprite>("RainImported/LogIcons/texSteakIcon");
            glasses=Resources.Load<Sprite>("RainImported/LogIcons/texGlassesIcon");
            tooth=Resources.Load<Sprite>("RainImported/LogIcons/texToothNecklaceIcon");
        }
        void BindMode(Gamemode next)
        {
            if(mode)mode.GamemodeEvent_Timer-=OnTimer;
            mode=next;
            if(mode)mode.GamemodeEvent_Timer+=OnTimer;
        }
        void OnTimer(int seconds)
        {
            seconds=Mathf.Max(0,seconds);timer.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
        }
        public void SetHealth(int value,int maximum)
        {
            healthFill.fillAmount=maximum>0?(float)value/maximum:0;
            healthValue.SetText("{0} / {1}",value,maximum);
        }
        public void SetAmmo(string value,string item) => ammo.text=string.IsNullOrEmpty(value)?item:value;
        public void SetSlots(CharacterItemManager inventory)
        {
            this.inventory=inventory;
            for(int i=0;i<icons.Count;i++)
            {
                Item item=i<inventory.Slots.Count?inventory.Slots[i].Item:null;
                icons[i].sprite=item?item.ItemIcon:null;icons[i].enabled=icons[i].sprite;
                borders[i].color=i==inventory.currentlyUsedSlotID?new Color(1,.88f,.4f):new Color(.45f,.46f,.5f);
            }
        }
        void Update()
        {
            if(!bag)return;
            bool desired=Input.GetKey(KeyCode.Tab)&&!Input.GetKey(KeyCode.Equals)&&!Input.GetKey(KeyCode.Escape)&&Application.isFocused;
            if(desired&&!bag.IsOpen&&ClientFrontend.GamePlayInput()&&PlayerGameplayInput.Instance&&PlayerGameplayInput.Instance.enabled)
            {
                inputHeld=true;PlayerGameplayInput.Instance.enabled=false;
                if(inventory)
                {
                    inventory.UsePrimaryInput=inventory.UseSecondaryInput=false;
                    var character=inventory.GetComponent<CharacterInstance>();
                    character.SetMovementInput(Vector2.zero);character.IsAiming=false;
                    character.SetActionKeyCode(ActionCodes.Sprint,false);character.SetActionKeyCode(ActionCodes.BladeWalk,false);
                }
                bag.SetOpen(true);ClientFrontend.ShowCursor(true);
            }
            if(!desired&&bag.IsOpen)CloseBag();
            if(Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+.2f;
            var items=inventory?inventory.GetComponent<RangeResearchItems>():null;
            var player = GameManager.myPlayerInstance;
            RefreshAllyDrones(player ? player.MyCharacter : null);
            var rainMode = mode as RangeGamemode;
            bool rain = rainMode && rainMode.IsRainCoop;
            CharacterInstance rainCharacter = rain && player ? player.MyCharacter : null;
            UpdateScannerMarkers(rainMode, rainCharacter);
            if (rain && player)
            {
                wallet.text = $"金币  {player.RainGold}";
                location.text = english ? "Chapter One" : "第一章 · 白盒";
                var coOpMode = rainMode;
                if (rainCharacter) SetHealth(rainCharacter.CurrentHealth, rainCharacter.MaxHealth);
                UpdateRainLevel(player);
                string bossName = coOpMode.RainBossKind == RangeResearchEnemy.QueenKind ? "甲壳虫女王" : "石之泰坦";
                string goal = coOpMode.RainVictory ? $"挑战完成 · {bossName}已击败" : coOpMode.RainBossActive ? $"击败{bossName}" :
                    $"清理怪物 {Mathf.Min(coOpMode.RainKills, RangeGamemode.RainKillsForBoss)} / {RangeGamemode.RainKillsForBoss}\n信标 {coOpMode.RainBeaconsActivated} / {RangeGamemode.RainBeaconCount}";
                objective.text = "雨中冒险\n" + goal;
                UpdateRainRadar(coOpMode, rainCharacter);
                int nearbyChest = coOpMode.NearbyRainChest(rainCharacter);
                bool nearBeacon = coOpMode.NearbyRainBeacon(rainCharacter) >= 0;
                string equipment = player.RainEquipment switch
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
                chestPrompt.text = bag.IsOpen ? "" : nearBeacon ? "E 激活任务信标" :
                    RangeGamemode.IsRainCookStation(nearbyChest) && RangeGamemode.RainChestRewardId(nearbyChest) == "UltimateMeal"
                        ? (rainCharacter && rainCharacter.ResearchClovers >= 2
                            ? "E 烹饪终极大餐 · 2 份 57 叶四叶草" : "料理台 · 需要 2 份 57 叶四叶草") :
                    RangeGamemode.IsRainCookStation(nearbyChest) && RangeGamemode.RainChestRewardId(nearbyChest) == "UltimateMealFrostRelic"
                        ? "E 烹饪终极大餐 · 57 叶四叶草＋冰霜遗物" :
                    RangeGamemode.IsRainWhiteItemStation(nearbyChest)
                        ? (RangeGamemode.RainChestRewardId(nearbyChest) == "ScrapBisonSteak"
                            ? "E 选择要粉碎的普通物品 · 每次最多 10 层" : "E 打印士兵的注射器 · 优先消耗白色物品碎片") :
                    RangeGamemode.IsRainCookStation(nearbyChest) && RangeGamemode.RainChestRewardId(nearbyChest) == "BonusHealthBoost"
                        ? (rainCharacter && rainCharacter.ResearchWhiteScrap > 0 && rainCharacter.ResearchMochas > 0
                            ? "E 烹饪快速修复 · 白色物品碎片＋摩卡" : "料理台 · 需要白色物品碎片＋摩卡") :
                    RangeGamemode.IsRainCookStation(nearbyChest) && RangeGamemode.RainChestRewardId(nearbyChest) == "StewDoubleDaisy"
                        ? (rainCharacter && rainCharacter.ResearchLeptonDaisies >= 2
                            ? "E 烹饪丰盛炖菜 · 2 份轻粒子雏菊" : "料理台 · 需要 2 份轻粒子雏菊") :
                    RangeGamemode.IsRainCookStation(nearbyChest) && RangeGamemode.RainChestRewardId(nearbyChest) == "Stew"
                        ? (rainCharacter && rainCharacter.ResearchLeptonDaisies > 0 && rainCharacter.ResearchSeeds > 0
                            ? "E 烹饪丰盛炖菜 · 轻粒子雏菊＋吸血种子" : "料理台 · 需要轻粒子雏菊＋吸血种子") :
                    RangeGamemode.IsRainCookStation(nearbyChest) && RangeGamemode.RainChestRewardId(nearbyChest) == "CookedSteakMocha"
                        ? (player.RainSteak > 0 && rainCharacter && rainCharacter.ResearchMochas > 0
                            ? "E 烹饪灸烤牛排 · 野牛肉排＋摩卡" : "料理台 · 需要野牛肉排＋摩卡") :
                    RangeGamemode.IsRainCookStation(nearbyChest) ? (player.RainSteak >= 2
                        ? "E 烹饪灸烤牛排 · 2 份野牛肉排" : "料理台 · 需要 2 份野牛肉排") :
                    nearbyChest >= 0 ? (player.RainGold >= RangeGamemode.RainChestPrice
                        ? "E 购买 " + RainChestRewardName(nearbyChest) +
                            (RangeGamemode.IsRainMultishopChest(nearbyChest) ? " · 多重商店" : "") + " · 20 金币"
                        : RainChestRewardName(nearbyChest) + " · 需要 20 金币")
                        : equipment.Length > 0 ? "装备：" + equipment +
                            (player.RainEquipment == RangeResearchItems.Equipment.ExecutiveCard ? " · 购买返还 10%" : " · F10") : "";
            }
            if (player && player.HasHeroLoadout && chestPrompt.text.StartsWith("E ", System.StringComparison.Ordinal))
                chestPrompt.text = "F " + chestPrompt.text.Substring(2);
            buffStatus.text="";
            if(inventory && inventory.TryGetComponent<Health>(out var owner) && owner.RangeInvulnerable)
                buffStatus.text="无敌开启    ";
            if(inventory && inventory.TryGetComponent<RangeTestBuffs>(out var buffs))
            {
                int lifesteal=Mathf.CeilToInt(buffs.Remaining(RangeTestBuffs.Lifesteal));
                int regen=Mathf.CeilToInt(buffs.Remaining(RangeTestBuffs.Regeneration));
                if(lifesteal>0)buffStatus.text+=$"吸血 {lifesteal}s";
                if(regen>0)buffStatus.text+=$"    生命再生 {regen}s";
                int reduction = Mathf.CeilToInt(buffs.Remaining(RangeTestBuffs.DamageReduction));
                if (reduction > 0) buffStatus.text += $"    减伤 {reduction}s";
            }
            if (items && items.Barrier >= 1f) buffStatus.text += $"    屏障 {Mathf.FloorToInt(items.Barrier)}";
            if (player && player.MyCharacter && player.MyCharacter.ResearchShieldGenerators > 0)
                buffStatus.text += $"    护盾 {player.MyCharacter.ResearchShield}/{player.MyCharacter.ResearchShieldCapacity}";
            bag.SetItem(0,steak,"野牛肉排","每层增加 25 点生命上限。",rain&&player?player.RainSteak:items?items.Steak:0);
            bag.SetItem(1,glasses,"透镜制作者的眼镜","每层增加 10% 暴击率，最多 100%；暴击造成双倍伤害。",rain&&player?player.RainGlasses:items?items.Glasses:0);
            bag.SetItem(2,tooth,"怪物牙齿","击杀掉落治疗球。同阵营拾取后恢复 8 点生命，加上每层 2% 拾取者最大生命。",rain&&player?player.RainTooth:items?items.Tooth:0);
            bag.SetItem(3,plate,"推斥护甲板","每层减少 5 点承伤，最低为 1 点。",items?items.ArmorPlates:0);
            bag.SetItem(4,medkit,"医疗包","最后一次受击 2 秒后恢复 20 点生命，加上每层 5% 最大生命。",items?items.Medkits:0);
            bag.SetItem(5,rounds,"穿甲弹","每层对训练场石之泰坦额外造成 20% 伤害。",items?items.ArmorPiercingRounds:0);
            bag.SetItem(6,crowbar,"撬棍","目标生命至少 90% 时，每层提高 75% 伤害。",items?items.Crowbars:0);
            bag.SetItem(7,bear,"更艰难的时光","有概率完全格挡敌方伤害，叠加收益递减。",items?items.Bears:0);
            bag.SetItem(8,hoof,"保罗的山羊蹄","每层提高 14% 移速，走路与冲刺均生效。",items?items.Hooves:0);
            bag.SetItem(9,soda,"能量饮料","冲刺时每层额外增加基础走路速度的 25%。",items?items.EnergyDrinks:0);
            bag.SetItem(10,egg,"浮浪幼体","每层在实际扣血前恢复 15 点生命。",items?items.ParentEggs:0);
            bag.SetItem(11,watch,"易碎手表","每层提高 20% 伤害；受击后生命降至 25% 或以下时损坏。",items?items.Watches:0);
            bag.SetItem(12,brokenWatch,"易碎手表（已损坏）","已经失去增伤效果。",items?items.BrokenWatches:0);
            bag.SetItem(13,healingRack,"回春支架","每层提高 100% 治疗量，受生命上限限制。",items?items.HealingRacks:0);
            bag.SetItem(14,slug,"谨慎的蛞蝓","连续 7 秒未扣血后，每层每秒恢复 3 点生命。",items?items.Slugs:0);
            bag.SetItem(15,potion,"强力万能药","受击后存活且生命降至 25% 或以下时，消耗一瓶并恢复 75% 最大生命。",items?items.HealingPotions:0);
            bag.SetItem(16,emptyBottle,"空瓶","万能药消耗后留下的空瓶。",items?items.EmptyBottles:0);
            bag.SetItem(17,brooch,"黄晶胸针","每层在击杀时获得 15 点临时屏障，优先吸收伤害并持续衰减。",items?items.Brooches:0);
            bag.SetItem(18,aegis,"庇护","每层将直接治疗的 50% 溢出量转为临时屏障。",items?items.Aegises:0);
            bag.SetItem(19,dagger,"三尖匕首","每层提供 10% 流血几率，3 秒内累计造成该次基础命中伤害的 240%。",items?items.Daggers:0);
            var itemOwner = player ? player.MyCharacter : null;
            bag.SetItem(20,syringe,"士兵注射器","每层增加 15% 攻击速度。",itemOwner?itemOwner.ResearchSyringes:0);
            bag.SetItem(21,stickyBomb,"黏性炸弹","每层提供 5% 命中触发率，延迟爆炸造成 180% 触发伤害。",itemOwner?itemOwner.ResearchStickyBombs:0);
            bag.SetItem(22,stunGrenade,"眩晕手雷","命中有概率打断普通怪物并眩晕 2 秒。首层约 4.8%，叠层收益递减。",itemOwner?itemOwner.ResearchStunGrenades:0);
            bag.SetItem(23,behemoth,"完美巨兽","命中爆炸，造成 60% 额外伤害。首层半径 4 米，后续每层 +2.5 米，半径受触发系数影响。",itemOwner?itemOwner.ResearchBehemoths:0);
            bag.SetItem(24,focusCrystal,"聚焦水晶","对 13 米内的敌人，每层额外造成 20% 伤害。",itemOwner?itemOwner.ResearchFocusCrystals:0);
            bag.SetItem(25,wisp,"鬼火","击杀后延迟 0.5 秒爆炸。首层造成 350% 攻击力伤害，半径 12 米；外圈只受四分之一伤害。",itemOwner?itemOwner.ResearchWisps:0);
            bag.SetItem(26,warbanner,"战旗","升级时放置战旗。圈内友军攻速、移速各 +30%。首层半径 16 米，后续每层 +8 米。",itemOwner?itemOwner.ResearchWarbanners:0);
            bag.SetItem(27,ukulele,"尤克里里","命中有 25% 基础概率发出连锁闪电，造成 80% 触发伤害。首层最多跳向 3 个额外敌人，每层再加 2 个。",itemOwner?itemOwner.ResearchUkuleles:0);
            bag.SetItem(28,gasoline,"汽油","击杀时造成 150% 攻击力的范围伤害并点燃敌人。首层半径 12 米、燃烧总伤害 150%，后续每层 +4 米、燃烧总伤害 +75%。",itemOwner?itemOwner.ResearchGasoline:0);
            bag.SetItem(29,iceRing,"冰环","单次伤害达到攻击力 400% 时，每层追加 250% 触发伤害并减速 3 秒。冷却 10 秒。",itemOwner?itemOwner.ResearchIceRings:0);
            bag.SetItem(30,fireRing,"火环","单次伤害达到攻击力 400% 时生成移动火旋风。每层总伤害为触发伤害的 300%，与冰环共享 10 秒冷却。",itemOwner?itemOwner.ResearchFireRings:0);
            bag.SetItem(31,shieldGenerator,"个人护盾发生器","每层提供最大生命 8% 的护盾。脱离伤害 7 秒后开始充能，约 2 秒充满。",itemOwner?itemOwner.ResearchShieldGenerators:0);
            bag.SetItem(32,buckler,"玫瑰圆盾","冲刺期间每层增加 30 点护甲。",itemOwner?itemOwner.ResearchBucklers:0);
            bag.SetItem(33,opal,"异形欧泊","脱离危险时每层增加 100 点护甲。受击后失效，7 秒未再受击后恢复。",itemOwner?itemOwner.ResearchOpals:0);
            bag.SetItem(34,bauble,"时空装饰品","有效命中使敌人移速除以 1.6，每层持续 2 秒。叠层延长持续时间。",itemOwner?itemOwner.ResearchBaubles:0);
            bag.SetItem(35,seed,"吸血种子","有效命中时，每层恢复 1 点生命，乘以该攻击的触发系数。",itemOwner?itemOwner.ResearchSeeds:0);
            for(int i=0;i<6;i++)
            {
                var item=inventory&&i<inventory.Slots.Count?inventory.Slots[i].Item:null;
                bag.SetItem(i+36,item?item.ItemIcon:null,item?item.ItemName:"",item?"已装备武器 / 装备；快捷键 "+(i+1):"",item?1:0, inspectionOnly:true);
            }
            bag.SetItem(42,hexOfPain,"苦痛诅咒","每层为生命上限的惩罚分母增加 0.3，个人护盾容量随生命上限缩放。",itemOwner?itemOwner.ResearchHexOfPain:0);
            bag.SetItem(43,hexOfFamine,"饥荒诅咒","治疗量除以 1 + 0.4 × 层数，小数余量会计入后续治疗。",itemOwner?itemOwner.ResearchHexOfFamine:0);
            bag.SetItem(44,cookedSteak,"灸烤牛排","每层增加 50 点固定生命，并为生命上限倍率增加 5%。",itemOwner?itemOwner.ResearchCookedSteaks:0);
            bag.SetItem(45,ultimateMeal,"终极大餐","满血时每层增加两次有利重掷，受伤后停用。",itemOwner?itemOwner.ResearchUltimateMeals:0);
            bag.SetItem(46,stew,"丰盛炖菜","每层增加基础生命再生，满血时将生命再生加入基础攻击。",itemOwner?itemOwner.ResearchStews:0);
            bag.SetItem(47,leptonDaisy,"轻粒子雏菊","每层增加一次信标治疗新星，为范围内盟友恢复 50% 最大生命。",itemOwner?itemOwner.ResearchLeptonDaisies:0);
            bag.SetItem(48,hikingBoots,"登山靴","从高处命中可增加暴击率和暴击伤害。有效命中刷新 10 秒持续时间。",itemOwner?itemOwner.ResearchHikingBoots:0);
            bag.SetItem(49,quickFix,"快速修复","放大道具提供的固定生命、生命倍率增益和再生，首层 50%，之后每层增加 15%。",itemOwner?itemOwner.ResearchQuickFixes:0);
            bag.SetItem(50,whiteScrap,"物品碎片，白色","可作为快速修复的料理材料。",itemOwner?itemOwner.ResearchWhiteScrap:0);
            bag.SetItem(51,bolsteringLantern,"强化提灯","20 米内每名存活敌友提高攻速，最多计入 2＋道具层数名。首层每名 20%，追加每层每名增加 3.5%。",itemOwner?itemOwner.ResearchLanterns:0);
            bag.SetItem(52,elusiveAntlers,"难以捉摸的鹿角","周期生成同队可拾取的碎片。每层拾取增益提高 12% 移速并刷新至 12 秒，最多 3×持有层数层；无道具的队友最多三层。",itemOwner?itemOwner.ResearchAntlers:0);
            bag.SetItem(53,collector,"收藏者的冲动","拾取使生命、回复、伤害、暴击率、攻速、移速、护甲及护盾提高 3%，刷新全部增益至 20 秒。最多 3 层，每额外道具增加 2 层上限。",itemOwner?itemOwner.ResearchCollectors:0);
        }
        void RefreshAllyDrones(CharacterInstance viewer)
        {
            int index = 0;
            if (viewer)
            {
                foreach (var identity in Mirror.NetworkClient.spawned.Values)
                {
                    if (!identity || !identity.TryGetComponent<CharacterInstance>(out var drone) ||
                        drone.RainDroneOwner == 0 || drone.Team != viewer.Team) continue;
                    if (!allyDronePanel)
                    {
                        allyDronePanel = FrontEndUI.Rect(display.transform, "Allied drones", 90, 185, 270, 580);
                        FrontEndUI.Text(allyDronePanel, "友方无人机", 0, 0, 270, 25, 19);
                    }
                    if (index == allyDroneRows.Count)
                    {
                        float y = 28 + index * 25;
                        var background = FrontEndUI.Box(allyDronePanel, "Drone row " + index, 0, y, 260, 23,
                            new Color(.05f, .07f, .09f, .7f));
                        background.raycastTarget = false;
                        var fill = FrontEndUI.Box(background.transform, "Health", 2, 19, 256, 3, new Color(.4f, .9f, .24f));
                        fill.raycastTarget = false;
                        var label = FrontEndUI.Text(background.transform, "", 6, 0, 247, 18, 15);
                        allyDroneRows.Add((label, fill));
                    }
                    var row = allyDroneRows[index++];
                    row.label.transform.parent.gameObject.SetActive(true);
                    var spec = RangeDrone.FindSpec(drone.RainDroneId);
                    row.label.text = $"{spec?.name ?? drone.RainDroneId}   {drone.CurrentHealth}/{drone.MaxHealth}";
                    row.fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        256 * Mathf.Clamp01(drone.CurrentHealth / (float)Mathf.Max(1, drone.MaxHealth)));
                }
            }
            if (allyDronePanel) allyDronePanel.gameObject.SetActive(index > 0);
            for (; index < allyDroneRows.Count; index++)
                allyDroneRows[index].label.transform.parent.gameObject.SetActive(false);
        }
        string RainChestRewardName(int index)
        {
            if (rainChestRewardNames == null)
            {
                var tables = new RainImported.LogbookCatalog().Table;
                rainChestRewardNames = new string[RangeGamemode.RainChestCount];
                for (int i = 0; i < rainChestRewardNames.Length; i++)
                {
                    string id = RangeGamemode.RainChestRewardId(i);
                    var entries = RangeGamemode.IsRainEquipmentChest(i) ? tables.equipment : tables.items;
                    rainChestRewardNames[i] = System.Array.Find(entries, row => row.catalogName == id)?.name ?? id;
                }
            }
            return rainChestRewardNames[index];
        }

        void UpdateScannerMarkers(RangeGamemode mode, CharacterInstance character)
        {
            bool scanning = mode && character && character.CurrentHealth > 0 && mode.HasRainScannerPulses;
            Camera camera = scanning && !bag.IsOpen ? Camera.main : null;
            if (scanning && scannerMarkers == null)
            {
                scannerMarkers = new Image[RangeGamemode.RainChestCount + RangeGamemode.RainBeaconCount];
                scannerRevealed = new bool[scannerMarkers.Length];
                scannerFindChestAudio = Resources.Load<AudioClip>("RainImported/ScannerFindChestOriginal");
                scannerChestIcon = Resources.Load<Sprite>("RainImported/LogIcons/texInventoryIconOutlined");
                scannerBeaconIcon = Resources.Load<Sprite>("RainImported/LogIcons/texShrineIconOutlined");
                scannerOutsideArrow = Resources.Load<Sprite>("RainImported/LogIcons/ScannerOutsideArrow");
                for (int i = 0; i < scannerMarkers.Length; i++)
                {
                    var marker = FrontEndUI.Box(display.transform, "Scanner marker " + i, 0, 0, 24, 24, Color.white);
                    marker.preserveAspect = true;
                    marker.raycastTarget = false;
                    marker.rectTransform.pivot = new Vector2(.5f, .5f);
                    scannerMarkers[i] = marker;
                }
            }
            if (scannerMarkers == null) return;

            var root = (RectTransform)display.transform;
            double now = scanning ? mode.RainScannerTime : 0d;
            for (int i = 0; i < scannerMarkers.Length; i++)
            {
                bool beacon = i >= RangeGamemode.RainChestCount;
                int index = beacon ? i - RangeGamemode.RainChestCount : i;
                var marker = scannerMarkers[i];
                Vector3 point = default;
                bool revealed = scanning && mode.TryGetRainMapNode(index, beacon, out point) &&
                    mode.IsRainScannerPointRevealed(point, now);
                if (revealed && !scannerRevealed[i] && scannerFindChestAudio)
                    AudioSource.PlayClipAtPoint(scannerFindChestAudio, point);
                scannerRevealed[i] = revealed;

                bool visible = false;
                if (camera && revealed)
                {
                    Vector2 screen = ProjectRainScannerMarker(camera.pixelRect,
                        camera.WorldToScreenPoint(point + Vector3.up), out bool outside, out float rotation);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var position))
                    {
                        marker.sprite = outside ? scannerOutsideArrow : (beacon ? scannerBeaconIcon : scannerChestIcon);
                        marker.color = outside ? ScannerArrowColor : Color.white;
                        marker.rectTransform.localPosition = position;
                        marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                        visible = marker.sprite;
                    }
                }
                marker.gameObject.SetActive(visible);
            }
        }

        static Vector2 ProjectRainScannerMarker(Rect viewport, Vector3 screen, out bool outside, out float rotation)
        {
            Vector2 center = viewport.center;
            Vector2 halfSize = viewport.size * .475f;
            Vector2 direction = (Vector2)screen - center;
            outside = screen.z <= 0f || Mathf.Abs(direction.x) > halfSize.x || Mathf.Abs(direction.y) > halfSize.y;
            rotation = 0f;
            if (!outside || halfSize.x <= 0f || halfSize.y <= 0f) return screen;

            if (screen.z <= 0f) direction = -direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
            float edge = Mathf.Max(Mathf.Abs(direction.x) / halfSize.x, Mathf.Abs(direction.y) / halfSize.y);
            rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            return center + direction / edge;
        }

        void UpdateRainRadar(RangeGamemode mode, CharacterInstance character)
        {
            if (!rainRadar)
            {
                rainRadar = FrontEndUI.Box(display.transform, "Rain radar", 1540, 360, 290, 270,
                    new Color(.04f, .06f, .08f, .78f)).rectTransform;
                rainRadar.GetComponent<Image>().raycastTarget = false;
                FrontEndUI.Text(rainRadar, "北 ↑   小地图", 10, 0, 270, 30, 20, TextAlignmentOptions.Center);
                FrontEndUI.Text(rainRadar, "黄：箱子　蓝：信标　红：Boss", 9, 238, 272, 29, 17, TextAlignmentOptions.Center);
                DrawRainTerrain();
                chestDots = new RectTransform[RangeGamemode.RainChestCount];
                beaconDots = new RectTransform[RangeGamemode.RainBeaconCount];
                for (int i = 0; i < chestDots.Length; i++) chestDots[i] = CreateRadarDot("Chest " + i, Color.yellow);
                for (int i = 0; i < beaconDots.Length; i++) beaconDots[i] = CreateRadarDot("Beacon " + i, Color.cyan);
                bossDot = CreateRadarDot("Boss", new Color(1f, .25f, .2f));
                playerDot = CreateRadarDot("You", Color.white);
            }
            rainRadar.gameObject.SetActive(character && character.CurrentHealth > 0);
            if (!rainRadar.gameObject.activeSelf) return;
            PositionRadarDot(playerDot, character.transform.position);
            UpdateNodeDots(mode, chestDots, false);
            UpdateNodeDots(mode, beaconDots, true);
            bossDot.gameObject.SetActive(false);
            if (!mode.RainBossActive) return;
            foreach (var identity in Mirror.NetworkClient.spawned.Values)
            {
                if (!identity || !identity.TryGetComponent<CharacterInstance>(out var enemy) ||
                    enemy.RainEnemyKind != mode.RainBossKind + 1 || enemy.CurrentHealth <= 0) continue;
                bossDot.gameObject.SetActive(true);
                PositionRadarDot(bossDot, enemy.transform.position);
                break;
            }
        }

        void UpdateRainLevel(PlayerInstance player)
        {
            if (!rainLevel)
            {
                rainLevel = FrontEndUI.Text(display.transform, "", 150, 877, 650, 30, 22);
                rainLevel.gameObject.name = "Rain player level";
                var track = FrontEndUI.Box(display.transform, "Rain experience track", 100, 938, 470, 7,
                    new Color(.09f, .15f, .18f, .9f));
                rainExperienceFill = FrontEndUI.Box(track.transform, "Rain experience", 0, 0, 470, 7,
                    new Color(.42f, .82f, .96f), "texUICombatHealthbar");
                rainExperienceFill.type = Image.Type.Filled;
                rainExperienceFill.fillMethod = Image.FillMethod.Horizontal;
            }

            int level = player.RainLevel;
            long start = RainImported.RunLedger.Experience(level);
            long required = RainImported.RunLedger.Experience(level + 1) - start;
            long earned = player.RainExperience - start;
            float attack = player.MyCharacter ? player.MyCharacter.ResearchStews > 0 ? player.MyCharacter.EffectiveRainAttackPower : player.MyCharacter.RainAttackPower : 0;
            float regen = player.MyCharacter ? player.MyCharacter.EffectiveRainHealthRegen : 0;
            rainLevel.SetText(english ? "Lv. {0}   XP {1}/{2}   ATK {3:0.0}   Regen {4:0.0}/s"
                : "等级 {0}   经验 {1}/{2}   攻击 {3:0.0}   回血 {4:0.0}/秒", level, earned, required, attack, regen);
            rainExperienceFill.fillAmount = (float)earned / required;
        }

        void DrawRainTerrain()
        {
            var arena = GameObject.Find("Chapter One");
            if (!arena) return;
            var terrain = FrontEndUI.Rect(rainRadar, "Chapter One terrain", 0, 0, 290, 270);
            foreach (Transform block in arena.transform)
            {
                if (!block.TryGetComponent<BoxCollider>(out var collider)) continue;
                var bounds = collider.bounds;
                bool floor = block.name.StartsWith("Sector ", System.StringComparison.Ordinal);
                var color = floor
                    ? new Color(.24f, .34f, .39f) : new Color(.60f, .68f, .69f);
                var tile = FrontEndUI.Box(terrain, block.name,
                    145 + bounds.min.x * RadarScale, 133 - bounds.max.z * RadarScale,
                    Mathf.Max(1.5f, bounds.size.x * RadarScale),
                    Mathf.Max(1.5f, bounds.size.z * RadarScale), color);
                tile.raycastTarget = false;
                if (floor) tile.transform.SetAsFirstSibling();
            }
        }

        RectTransform CreateRadarDot(string name, Color color)
        {
            var dot = FrontEndUI.Box(rainRadar, name, 0, 0, 10, 10, color);
            dot.raycastTarget = false;
            return dot.rectTransform;
        }

        static void UpdateNodeDots(RangeGamemode mode, RectTransform[] dots, bool beacon)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                bool visible = mode.TryGetRainMapNode(i, beacon, out var point);
                dots[i].gameObject.SetActive(visible);
                if (visible) PositionRadarDot(dots[i], point);
            }
        }

        static void PositionRadarDot(RectTransform dot, Vector3 point)
        {
            dot.anchoredPosition = new Vector2(140 + Mathf.Clamp(point.x * RadarScale, -101, 101),
                -138 + Mathf.Clamp(point.z * RadarScale, -101, 101));
        }
        void CloseBag()
        {
            if(bag)bag.SetOpen(false);
            if(inputHeld){if(PlayerGameplayInput.Instance)PlayerGameplayInput.Instance.enabled=true;ClientFrontend.ShowCursor(false);inputHeld=false;}
        }
        static Sprite CreateInventoryIcon(string name)
        {
            var texture = Resources.Load<Texture2D>("RainImported/LogIcons/" + name);
            if (!texture) return null;
            var icon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f));
            icon.name = name;
            return icon;
        }

        void OnDestroy()
        {
            if (RainScrapperMenu.Current) RainScrapperMenu.Current.Close();
            CloseBag();Active=false;
            GameManager.GameEvent_OnGamemodeSet-=BindMode;
            if(mode)mode.GamemodeEvent_Timer-=OnTimer;
            if(display)Destroy(display.gameObject);
            if (hexOfPain) Destroy(hexOfPain);
            if (hexOfFamine) Destroy(hexOfFamine);
            if (cookedSteak) Destroy(cookedSteak);
            if (ultimateMeal) Destroy(ultimateMeal);
            if (stew) Destroy(stew);
            if (leptonDaisy) Destroy(leptonDaisy);
            if (hikingBoots) Destroy(hikingBoots);
            if (quickFix) Destroy(quickFix);
            if (whiteScrap) Destroy(whiteScrap);
        }
        void OnEnable(){if(display)display.gameObject.SetActive(true);}
        void OnDisable(){if(RainScrapperMenu.Current)RainScrapperMenu.Current.Close();CloseBag();if(display)display.gameObject.SetActive(false);}
    }
}
