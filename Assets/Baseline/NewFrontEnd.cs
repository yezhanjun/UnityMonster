#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MTPSKIT
{
    public sealed class NewFrontEnd : MonoBehaviour
    {
        public TMP_FontAsset font;
        [SerializeField, HideInInspector] Canvas designPreview;
        RoomCreator creator;
        Canvas canvas;
        Transform page;
        TMP_Text identity, status;
        string screen = "home", tab = "overview";
        bool multiplayer, english, rain;
        int selectedMap, selectedCharacter = RangeCharacterSelection.FirstPortable, difficulty = 1;
        Jotunnslayer.RaidClass selectedRaidClass;
        Jotunnslayer.HyboreaMatchMode selectedHyboreaMode;
        static readonly string[] RaidClassNames = { "狂战士", "卜士", "返生之人", "盗贼", "监守者", "女武神" };
        float nextRefresh;
        string roomsSignature;
        string trainingStatus;
        Transform roomList;
        public static bool UseReferenceHud { get; private set; }
        string L(string cn, string en) => english ? en : cn;
        SteamRoomSession Steam => SteamRoomSession.Instance;

        IEnumerator Start()
        {
            yield return null;
            if(designPreview){designPreview.gameObject.SetActive(false);Destroy(designPreview.gameObject);designPreview=null;}
            creator = FindFirstObjectByType<RoomCreator>();
            selectedCharacter = RangeCharacterSelection.SavedDefault();
            RangeCharacterSelection.SetSelected(selectedCharacter);
            FrontEndUI.Font = font;
            UseReferenceHud = true;
            english = PlayerPrefs.GetInt("FrontEnd.English",0) != 0;
            AudioListener.volume = PlayerPrefs.GetFloat("FrontEnd.Volume",1);
            canvas = FrontEndUI.Canvas(transform, 50);
            Show("home");
            UI.ClientFrontend.ShowCursor(true);
#if UNITY_EDITOR
            string trainingScene = UnityEditor.SessionState.GetString(EditorTrainingLaunchKey, "");
            if (!string.IsNullOrEmpty(trainingScene))
            {
                UnityEditor.SessionState.EraseString(EditorTrainingLaunchKey);
                LaunchTraining(trainingScene);
            }
#endif
        }

#if UNITY_EDITOR
        public const string EditorTrainingLaunchKey = "TPSShoot.EditorLaunchTraining";

        // A static saved preview makes the startup layout visible without entering Play mode.
        public void BuildEditorPreview()
        {
            if(designPreview)DestroyImmediate(designPreview.gameObject);
            FrontEndUI.Font=font;
            canvas=FrontEndUI.Canvas(transform,50);
            Show("home");
            designPreview=canvas;
        }
#endif

        void OnDestroy()
        {
            if (canvas) UI.ClientFrontend.ShowCursor(false);
        }

        void Show(string target)
        {
            screen = target;
            trainingStatus = null;
            if (page) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            page = FrontEndUI.Rect(canvas.transform, "Page " + target,0,0,1920,1080);
            FrontEndUI.Box(page,"Background",0,0,1920,1080,new Color(.025f,.055f,.08f));
            FrontEndUI.Box(page,"Backdrop band",0,200,1920,650,new Color(.045f,.09f,.12f));
            FrontEndUI.Box(page,"Accent",0,0,1920,5,new Color(.24f,.54f,.63f));
            FrontEndUI.Text(page,"TPSSHOOT  /  INTERNAL TEST",32,20,800,40,19);
            FrontEndUI.Button(page,english?"English":"简体中文",1410,50,160,42,ToggleLanguage);
            identity = FrontEndUI.Text(page,"Steam",1590,45,300,75,18,TextAlignmentOptions.MidlineRight);
            status = FrontEndUI.Text(page,"",40,1005,1500,52,19);
            if (target == "home") Home();
            else
            {
                FrontEndUI.Button(page,L("返回","Back"),1630,1000,240,48,()=>Show(target=="prepare"&&multiplayer?"multiplayer":"home"));
                if(target=="prepare") Prepare();
                if(target=="multiplayer") Multiplayer();
                if(target=="settings") Settings();
                if(target=="journal") Journal();
            }
            RefreshIdentity();
        }

        void ToggleLanguage()
        {
            english = !english; PlayerPrefs.SetInt("FrontEnd.English",english?1:0); PlayerPrefs.Save(); Show(screen);
        }
        void Home()
        {
            FrontEndUI.Text(page,"UNITY\nMONSTER",520,120,880,270,116,TextAlignmentOptions.Center);
            FrontEndUI.Text(page,L("怪物白盒验收","Monster whitebox acceptance"),520,395,880,50,25,TextAlignmentOptions.Center);
            string[] cn={"白盒训练场","日志","设置","退出至桌面"};
            string[] en={"Whitebox Range","Logbook","Settings","Quit to Desktop"};
            for(int i=0;i<cn.Length;i++)
            {
                int index=i;
                FrontEndUI.Button(page,L(cn[i],en[i]),96,480+i*65,410,55,()=>
                {
                    if(index==0)LaunchTraining(RoomCreator.TrainingScenePath);
                    if(index==1)Show("journal");
                    if(index==2)Show("settings");
                    if(index==3)Application.Quit();
                });
            }
            FrontEndUI.Text(page,L("独立怪物验收工程","Standalone monster acceptance project"),1290,925,560,40,19,TextAlignmentOptions.MidlineRight);
        }

        void LaunchTraining(string scenePath)
        {
            if (!creator || !DNNetworkManager.Instance)
            {
                SetTrainingStatus(L("训练场启动组件未就绪", "Training services are not ready"));
                return;
            }
            if (NetworkClient.active || NetworkServer.active || (Steam && Steam.Busy))
            {
                SetTrainingStatus(L("请先结束当前会话，再进入训练场", "Leave the current session before opening the range"));
                return;
            }
            if (SceneUtility.GetBuildIndexByScenePath(scenePath) < 0)
            {
                SetTrainingStatus(L("当前构建配置未启用此训练场场景", "This range scene is disabled in the active build profile"));
                return;
            }
            int mapIndex = ResolveTrainingMap(scenePath);
            if (mapIndex < 0)
            {
                SetTrainingStatus(L("训练场地图资源缺失或场景路径不匹配", "The range map asset is missing or points to a different scene"));
                Debug.LogError("Training map registration failed for " + scenePath);
                return;
            }

            SetTrainingStatus(L("正在进入训练场…", "Opening training range…"));
            Debug.Log("Training range requested: " + scenePath);
            bool wasListeningDisabled = NetworkServer.dontListen;
            NetworkServer.dontListen = true;
            try
            {
                creator.LaunchFromFrontEnd(mapIndex, selectedCharacter, false);
            }
            finally
            {
                if (!NetworkServer.active)
                {
                    NetworkServer.dontListen = wasListeningDisabled;
                    SetTrainingStatus(L("训练场本地主机未启动，请查看 Console", "Local training host did not start. Check the Console."));
                }
            }
        }

        int ResolveTrainingMap(string scenePath)
        {
            var maps = creator.Maps ?? System.Array.Empty<MapRepresenter>();
            int index = System.Array.FindIndex(maps, map => map && map.Scene == scenePath);
            if (index >= 0) return index;

            string resourceName = scenePath == RoomCreator.ModularTrainingScenePath ? "ModularTrainingMap" : "TrainingMap";
            var configuredMap = Resources.Load<MapRepresenter>(resourceName);
            if (!configuredMap || configuredMap.Scene != scenePath) return -1;

            index = maps.Length;
            System.Array.Resize(ref maps, index + 1);
            maps[index] = configuredMap;
            creator.Maps = maps;
            Debug.Log("Restored training map registration from Resources/" + resourceName);
            return index;
        }


        void SetTrainingStatus(string message)
        {
            trainingStatus = message;
            if (status) status.text = message;
        }

#if UNITY_EDITOR
        public void LaunchModularTrainingFromEditor() => LaunchTraining(RoomCreator.ModularTrainingScenePath);
#endif

        void Prepare()
        {
            if(!creator || creator.Maps.Length==0){FrontEndUI.Text(page,"Map configuration unavailable",100,200,900,80);return;}
            if(rain) selectedMap=System.Array.FindIndex(creator.Maps,m=>RoomCreator.IsRainScene(m.Scene));
            if(selectedMap<0){FrontEndUI.Text(page,L("雨中冒险场景未配置","Rain co-op scene is missing"),100,200,900,80);return;}
            selectedMap=Mathf.Clamp(selectedMap,0,creator.Maps.Length-1);
            FrontEndUI.Box(page,"Left",70,125,610,805,FrontEndUI.Ink,"texDetailPanel");
            FrontEndUI.Box(page,"Left frame",70,125,610,805,new Color(.55f,.57f,.62f),"texDetailPanelFrame").raycastTarget=false;
            FrontEndUI.Text(page,L("选择角色","Select character"),95,143,540,40,25);
            for (int i = 0; i < RangeCharacterSelection.PlayerChoices.Length; i++)
            {
                int choice = RangeCharacterSelection.PlayerChoices[i];
                var button = FrontEndUI.Button(page,RangeCharacterSelection.DisplayName(choice),95+i%2*275,200+i/2*57,265,50,
                    ()=>{selectedCharacter=choice;Show("prepare");},selectedCharacter==choice?FrontEndUI.Gold:(Color?)null);
                button.interactable = RangeCharacterSelection.IsAvailable(choice);
                RangeCharacterSelection.AddPortrait(button, choice, 40);
            }
            var labels=new[]{L("总览","Overview"),L("技能","Skills"),L("装备","Equipment")};
            var tabs=new[]{"overview","skills","equipment"};
            for(int i=0;i<3;i++){string id=tabs[i];FrontEndUI.Button(page,labels[i],95+i*185,390,175,47,()=>{tab=id;Show("prepare");},tab==id?FrontEndUI.Gold:(Color?)null);}
            string desc=tab=="overview"?L("行动定位：战斗测试\n\n保留枪械射击、长刀连段、投掷物与完整布娃娃。\n\n选择地图后即可部署。", "Combat test operator\n\nFirearms, melee combos, throwables and full-body ragdolls.\n\nSelect a map to deploy."):
                tab=="skills"?L("主攻击 · 当前武器\n副攻击 · 瞄准 / 防御\n机动 · 冲刺 / 闪避\n近战 · 长刀连段\n\n具体键位可在局内原有设置中查看。","Primary · Current weapon\nSecondary · Aim / guard\nMobility · Sprint / dodge\nMelee · Katana combos\n\nSee in-game controls for bindings."):
                L("装备沿用当前战斗配置\n\n1 · 主武器\n2 · 副武器\n3 · 长刀\n4 / 5 · 投掷物\n\n此页展示装备分类，不更改原有武器配表。","Current combat loadout\n\n1 · Primary\n2 · Secondary\n3 · Katana\n4 / 5 · Throwables\n\nThis page displays the existing loadout.");
            if (tab == "skills" || tab == "equipment")
            {
                var loadoutRoot = FrontEndUI.Rect(page,"Character loadout",95,460,550,400);
                HeroLoadoutUI.Draw(loadoutRoot,selectedCharacter,()=>Show("prepare"));
            }
            else FrontEndUI.Text(page,desc,100,460,550,405,25,TextAlignmentOptions.TopLeft);
            FrontEndUI.Text(page,RangeCharacterSelection.DisplayName(selectedCharacter),730,230,680,80,48,TextAlignmentOptions.Center);
            var preview=FrontEndUI.Box(page,"Operator preview",885,350,360,360,new Color(.08f,.15f,.2f),"texDetailPanel");
            var portrait = RangeCharacterSelection.Portrait(selectedCharacter);
            if (portrait)
            {
                var image = FrontEndUI.Box(preview.transform,"Character portrait",4,4,352,352,Color.white);
                image.sprite = portrait;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                FrontEndUI.Text(preview.transform,(System.Array.IndexOf(RangeCharacterSelection.PlayerChoices,selectedCharacter)+1).ToString("00"),20,45,320,180,112,TextAlignmentOptions.Center);
                FrontEndUI.Text(preview.transform,L("角色展示位","OPERATOR"),20,245,320,65,27,TextAlignmentOptions.Center);
            }
            FrontEndUI.Box(preview.transform,"Frame",0,0,360,360,new Color(.35f,.52f,.6f),"texDetailPanelFrame").raycastTarget=false;
            FrontEndUI.Text(page,portrait?L("角色头像","Character portrait"):L("角色预览占位","Operator preview placeholder"),740,730,660,40,21,TextAlignmentOptions.Center);
            FrontEndUI.Text(page,L("地图","Map"),735,810,150,40,23);
            FrontEndUI.Button(page,creator.Maps[selectedMap].Name,735,855,650,55,rain ? null : ()=>{selectedMap=(selectedMap+1)%creator.Maps.Length;Show("prepare");});
            FrontEndUI.Button(page,multiplayer?L("创建 Steam 房间","Create Steam room"):L("准备 / 进入游戏","Ready / Deploy"),865,950,410,64,Deploy,new Color(.18f,.55f,.1f));
            if (!multiplayer && Jotunnslayer.SkillPlayground.SupportsMatch(creator.Maps[selectedMap].Scene))
            {
                string className = L(RaidClassNames[(int)selectedRaidClass], selectedRaidClass.ToString());
                FrontEndUI.Button(page,L("巨人杀手职业 · ","Jotunnslayer class · ")+className,100,875,530,45,()=>
                {
                    selectedRaidClass = (Jotunnslayer.RaidClass)(((int)selectedRaidClass + 1) % RaidClassNames.Length);
                    Show("prepare");
                });
                FrontEndUI.Button(page,L("部署 · 巨人杀手","Deploy · Jotunnslayer"),1290,950,310,64,DeployJotunnslayer);
                string encounter = selectedHyboreaMode switch
                {
                    Jotunnslayer.HyboreaMatchMode.NormalInterior => L("海伯里亚内场 · 普通", "Hyborea Interior · Normal"),
                    Jotunnslayer.HyboreaMatchMode.EndlessInterior => L("海伯里亚内场 · 无尽", "Hyborea Interior · Endless"),
                    _ => L("默认", "Default")
                };
                FrontEndUI.Button(page,L("怪物遭遇 · ", "Enemy encounter · ")+encounter,100,930,530,45,()=>
                {
                    selectedHyboreaMode = (Jotunnslayer.HyboreaMatchMode)(((int)selectedHyboreaMode + 1) % 3);
                    Show("prepare");
                });
            }
            FrontEndUI.Box(page,"Rules",1490,125,350,805,FrontEndUI.Ink,"texDetailPanel");
            FrontEndUI.Text(page,L("困难度","Difficulty"),1510,145,300,55,32,TextAlignmentOptions.Center);
            string[] dif={L("简单","Easy"),L("普通","Normal"),L("困难","Hard")};
            string[] difIcons={"texDifficultyEasyIcon","texDifficultyNormalIcon","texDifficultyHardIcon"};
            for(int i=0;i<3;i++)
            {
                int d=i;var b=FrontEndUI.Button(page,"",1510+i*105,210,98,100,()=>{difficulty=d;Show("prepare");},i==difficulty?FrontEndUI.Gold:(Color?)null);
                b.interactable=i==1;
                var icon=FrontEndUI.Box(b.transform,"Difficulty icon",24,8,50,50,Color.white,difIcons[i]);icon.preserveAspect=true;icon.raycastTarget=false;
                FrontEndUI.Text(b.transform,dif[i],0,65,98,30,19,TextAlignmentOptions.Center);
            }
            FrontEndUI.Text(page,L("当前测试固定为普通规则","Current test uses normal rules"),1510,315,305,42,18);
            Rule(L("扩展包","Expansions"),365,new Color(.53f,.23f,.24f));
            Rule(L("神器","Artifacts"),540,new Color(.38f,.23f,.56f));
            Rule(L("业力","Karma"),715,new Color(.17f,.33f,.62f));
        }
        void Rule(string title,float y,Color color)
        {
            FrontEndUI.Box(page,title,1505,y,320,55,color,"texUICleanButton");
            FrontEndUI.Text(page,title,1525,y,280,55,29,TextAlignmentOptions.Center);
            for(int i=0;i<3;i++)
            {
                var im=FrontEndUI.Box(page,"Locked",1535+i*90,y+78,48,48,Color.white,"texUnlockIconSmall");im.preserveAspect=true;
            }
            FrontEndUI.Text(page,L("尚未开放","Unavailable"),1525,y+132,280,30,18,TextAlignmentOptions.Center);
        }
        void Deploy()
        {
            if(!creator || NetworkClient.active || NetworkServer.active || (Steam && Steam.Busy)) return;
            PlayerPrefs.SetInt("FrontEnd.LastCharacter",selectedCharacter); PlayerPrefs.Save();
            creator.LaunchFromFrontEnd(selectedMap,selectedCharacter,multiplayer);
        }

        void DeployJotunnslayer()
        {
            if (!creator || NetworkClient.active || NetworkServer.active || (Steam && Steam.Busy)) return;
            PlayerPrefs.SetInt("FrontEnd.LastCharacter", selectedCharacter);
            PlayerPrefs.Save();
            Jotunnslayer.HyboreaMatchEntry.LaunchMatch(creator, selectedMap, selectedCharacter, selectedRaidClass,
                selectedHyboreaMode);
        }
        void Multiplayer()
        {
            FrontEndUI.Text(page,L("多人游戏 · 雨中冒险","Multiplayer · Rain Co-op"),100,150,1000,90,60);
            FrontEndUI.Button(page,L("创建房间 / 准备","Host / Prepare"),100,290,480,65,()=>Show("prepare"));
            var characterButton = FrontEndUI.Button(page,L("角色 · ","Character · ")+RangeCharacterSelection.DisplayName(selectedCharacter),100,470,480,65,()=>
            {
                selectedCharacter=RangeCharacterSelection.Next(selectedCharacter);
                RangeCharacterSelection.SetSelected(selectedCharacter);
                PlayerPrefs.SetInt("FrontEnd.LastCharacter",selectedCharacter);
                PlayerPrefs.Save();
                Show("multiplayer");
            });
            RangeCharacterSelection.AddPortrait(characterButton,selectedCharacter,55);
            FrontEndUI.Button(page,L("刷新 Steam 房间","Refresh Steam rooms"),100,380,480,65,()=>{if(Steam)Steam.Refresh();roomsSignature=null;});
            FrontEndUI.Text(page,L("可加入的房间","Available rooms"),700,260,900,60,32);
            roomList=FrontEndUI.Rect(page,"Room list",700,340,1100,580); roomsSignature=null;
            RefreshRooms();
        }
        void RefreshRooms()
        {
            if(!roomList || !Steam)return;
            string signature=""; foreach(var id in Steam.Rooms)signature+=id.m_SteamID+Steam.RoomLabel(id);
            if(signature==roomsSignature)return; roomsSignature=signature;
            foreach(Transform c in roomList){c.gameObject.SetActive(false);Destroy(c.gameObject);}
            var rainRooms = new System.Collections.Generic.List<Steamworks.CSteamID>();
            foreach(var id in Steam.Rooms) if(Steam.IsRainRoom(id)) rainRooms.Add(id);
            if(rainRooms.Count==0)FrontEndUI.Text(roomList,L("暂无房间，请刷新或创建房间。","No rooms. Refresh or host a room."),0,0,1000,70);
            for(int i=0;i<rainRooms.Count&&i<7;i++)
            {
                var id=rainRooms[i];var b=FrontEndUI.Button(roomList,Steam.RoomLabel(id),0,i*75,1020,60,()=>Steam.Join(id));
                b.interactable=!Steam.Busy;
            }
        }
        void Settings()
        {
            FrontEndUI.Text(page,L("设置","Settings"),100,160,1000,90,60);
            FrontEndUI.Button(page,L("语言：简体中文 / English","Language: English / 简体中文"),100,320,720,60,ToggleLanguage);
            FrontEndUI.Button(page,L("切换全屏 / 窗口","Toggle fullscreen / windowed"),100,420,720,60,()=>Screen.fullScreen=!Screen.fullScreen);
            FrontEndUI.Text(page,L("主音量","Master volume"),100,530,240,50,30);
            FrontEndUI.Button(page,"-",350,530,100,55,()=>ChangeVolume(-.1f));
            FrontEndUI.Button(page,"+",470,530,100,55,()=>ChangeVolume(.1f));
            FrontEndUI.Text(page,Mathf.RoundToInt(AudioListener.volume*100)+"%",600,530,200,55,30);
        }
        void ChangeVolume(float delta)
        {
            AudioListener.volume=Mathf.Clamp01(AudioListener.volume+delta);
            PlayerPrefs.SetFloat("FrontEnd.Volume",AudioListener.volume);PlayerPrefs.Save();Show("settings");
        }
        void Journal()
        {
            RainImported.JournalBrowser.Open(page,()=>Show("home"));
        }
        void Update()
        {
            if(!canvas || Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.5f; RefreshIdentity();
            if(screen=="multiplayer")RefreshRooms();
        }
        void RefreshIdentity()
        {
            if(!identity)return;
            identity.text=Steam?Steam.PersonaName+"\nSteamID "+Steam.SteamId:L("Steam 离线","Steam offline");
            status.text = trainingStatus ?? (Steam ? Steam.Status : L("单人模式可用","Singleplayer available"));
            if(Steam&&Steam.Busy)
            {
                // A visible cancel path during create/join; the page remains until Mirror changes scenes.
                if(!page.Find("CancelSession"))
                {
                    var b=FrontEndUI.Button(page,L("取消连接","Cancel connection"),1180,960,330,55,()=>{Steam.Leave();Show(screen);});
                    b.name="CancelSession";
                }
            }
        }
    }
}
#endif
