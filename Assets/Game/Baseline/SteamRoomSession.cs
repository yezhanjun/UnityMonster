#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.IO;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

namespace MTPSKIT
{
    // Single owner of Steam API, callbacks and Lobby lifetime. AppID is supplied externally.
    public sealed class SteamRoomSession : MonoBehaviour
    {
        const string Protocol = "tpsshoot-steam-28";
        public const uint DevelopmentAppId = 4069010; // Confirmed in the owner's Steamworks dashboard.
        public static SteamRoomSession Instance { get; private set; }
        public string Status { get; private set; } = "Steam 尚未初始化";
        public readonly List<CSteamID> Rooms = new List<CSteamID>();
        public bool Busy => phase != Phase.Idle || requests.Count != 0;
        public bool IsReady => initialized;
        SteamRainProfile profile;
        internal bool RainFirstClear => profile?.FirstClear == true;
        internal string RainProfileStatus => profile?.Status ?? "本地档案未加载";
        bool ProfileSteamReady => initialized && appId == DevelopmentAppId && !Application.isBatchMode;
        public string PersonaName => initialized ? SteamFriends.GetPersonaName() : "Steam 离线";
        public string SteamId => initialized ? SteamUser.GetSteamID().m_SteamID.ToString() : "—";
        public bool IsRainRoom(CSteamID id) => initialized && RoomCreator.IsRainScene(SteamMatchmaking.GetLobbyData(id, "map"));
        public string RoomLabel(CSteamID id) => initialized ? SteamMatchmaking.GetLobbyData(id, "name") + "  " +
            SteamMatchmaking.GetNumLobbyMembers(id) + "/" + SteamMatchmaking.GetLobbyMemberLimit(id) : string.Empty;
        enum Phase { Idle, Creating, Joining, Hosting, Connecting, Connected, Leaving }
        Phase phase;
        DNNetworkManager manager;
        DNTransportImplementer selector;
        FizzySteamworks transport;
        bool initialized, listed;
        CSteamID lobby, pendingJoin;
        float deadline;
        uint appId;
        string roomName, map, mode;
        Action configure;
        CallResult<LobbyMatchList_t> matched;
        readonly List<IDisposable> requests = new List<IDisposable>();
        int generation;
        Callback<GameLobbyJoinRequested_t> invited;
        Callback<LobbyDataUpdate_t> changed;
        Callback<LobbyChatUpdate_t> members;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (LocalAcceptanceClient.Requested) return;
            if (!Instance) new GameObject("Steam Room Session").AddComponent<SteamRoomSession>();
        }

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id))
                    pendingJoin = new CSteamID(id);
        }

        void Start()
        {
            if (LocalAcceptanceClient.Requested) return;
            Initialize();
            string testRoot = Debug.isDebugBuild && Application.isBatchMode
                ? Environment.GetEnvironmentVariable("TPSSHOOT_PROFILE_TEST_DIR") : null;
            if (!Application.isBatchMode || !string.IsNullOrWhiteSpace(testRoot))
            {
                profile = new SteamRainProfile(testRoot ?? Path.Combine(Application.persistentDataPath, "accounts"));
                profile.Tick(ProfileSteamReady);
            }
        }

        internal void GrantRainFirstClear()
        {
            profile?.Tick(ProfileSteamReady);
            profile?.GrantFirstClear();
        }

        internal bool HasRainMonsterLog(string id) => profile?.HasMonsterLog(id) == true;

        internal void GrantRainMonsterLog(string id)
        {
            profile?.Tick(ProfileSteamReady);
            profile?.GrantMonsterLog(id);
        }

        bool Initialize()
        {
            if (LocalAcceptanceClient.Requested) { Status = "本地专项验收"; return false; }
            if (initialized) return true;
            string configured = Environment.GetEnvironmentVariable("TPSSHOOT_STEAM_APP_ID");
            try
            {
                if (string.IsNullOrWhiteSpace(configured))
                {
                    string root = Path.GetDirectoryName(Application.dataPath);
                    string path = Path.Combine(root, "steam_appid.txt");
                    if (File.Exists(path)) configured = File.ReadAllText(path).Trim();
                    else if (Application.isEditor || Debug.isDebugBuild) configured = DevelopmentAppId.ToString();
                }
            }
            catch (Exception e) { Status = "读取 Steam 配置失败：" + e.Message; return false; }
            if (!uint.TryParse(configured, out appId) || appId == 0)
            { Status = "Steam AppID 配置无效，请检查游戏目录的 steam_appid.txt"; return false; }
            try
            {
                Environment.SetEnvironmentVariable("SteamAppId", configured);
                Environment.SetEnvironmentVariable("SteamGameId", configured);
                if (!Packsize.Test() || !DllCheck.Test() || !SteamAPI.Init())
                { Status = "Steam 初始化失败，请检查客户端、应用访问资格及原生库"; return false; }
                initialized = true;
                if (SteamUtils.GetAppID().m_AppId != appId)
                { SteamAPI.Shutdown(); initialized = false; Status = "Steam AppID 与配置不符"; return false; }
                matched = CallResult<LobbyMatchList_t>.Create(OnMatched);
                invited = Callback<GameLobbyJoinRequested_t>.Create(x => Join(x.m_steamIDLobby));
                changed = Callback<LobbyDataUpdate_t>.Create(x => CheckLobby());
                members = Callback<LobbyChatUpdate_t>.Create(x => CheckLobby());
                Status = appId == 480 ? "Steam 已初始化（480 示例测试应用）" : "Steam 已初始化（AppID " + appId + "）";
                return true;
            }
            catch (Exception e) { Status = "Steam 初始化异常：" + e.Message; return false; }
        }

        bool Bind()
        {
            manager = DNNetworkManager.Instance;
            if (!manager) { Status = "未找到网络管理器"; return false; }
            selector = manager.GetComponent<DNTransportImplementer>();
            if (!selector) { Status = "未找到传输选择器"; return false; }
            transport = manager.GetComponent<FizzySteamworks>();
            if (!transport) transport = manager.gameObject.AddComponent<FizzySteamworks>();
            transport.UseNextGenSteamNetworking = true;
            return true;
        }

        public void Create(string title, string scene, string gamemode, int capacity, Action prepare)
        {
            if (Busy || NetworkClient.active || NetworkServer.active) { Status = "请先离开当前会话"; return; }
            if (!Initialize() || !Bind()) return;
            roomName = title; map = scene; mode = gamemode; configure = prepare;
            phase = Phase.Creating; deadline = Time.realtimeSinceStartup + 40;
            int attempt = ++generation;
            CallResult<LobbyCreated_t> request = null;
            request = CallResult<LobbyCreated_t>.Create((result, failed) =>
            {
                requests.Remove(request); request.Dispose();
                if (attempt != generation)
                {
                    if (!failed && result.m_eResult == EResult.k_EResultOK)
                        SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
                    return;
                }
                OnCreated(result, failed);
            });
            requests.Add(request);
            request.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, capacity));
            Status = "正在创建 Steam 房间……";
        }

        void OnCreated(LobbyCreated_t result, bool failed)
        {
            if (phase != Phase.Creating) return;
            if (failed || result.m_eResult != EResult.k_EResultOK) { Leave("创建房间失败：" + result.m_eResult); return; }
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            try
            {
                SteamMatchmaking.SetLobbyJoinable(lobby, false);
                configure?.Invoke(); configure = null;
                if (!selector.SelectSessionTransport(transport)) throw new InvalidOperationException("无法切换 Steam 传输");
                Set("protocol", Protocol); Set("build", Application.version); Set("app", appId.ToString());
                Set("name", roomName); Set("map", map); Set("mode", mode);
                Set("capacity", manager.maxConnections.ToString());
                Set("host", SteamUser.GetSteamID().m_SteamID.ToString()); Set("state", "starting");
                manager.StartHost();
                if (!NetworkServer.active || !transport.ServerActive()) throw new InvalidOperationException("主机启动失败");
                phase = Phase.Hosting;
                Status = "主机加载中……";
            }
            catch (Exception e) { Leave("建房失败：" + e.Message); }
        }

        void Set(string key, string value)
        {
            if (!SteamMatchmaking.SetLobbyData(lobby, key, value)) throw new InvalidOperationException("大厅数据写入失败：" + key);
        }
        string Get(string key) => SteamMatchmaking.GetLobbyData(lobby, key);

        public void Refresh()
        {
            if (!Initialize() || listed) return;
            SteamMatchmaking.AddRequestLobbyListStringFilter("protocol", Protocol, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("app", appId.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            listed = true;
            matched.Set(SteamMatchmaking.RequestLobbyList());
        }
        void OnMatched(LobbyMatchList_t result, bool failed)
        {
            listed = false; Rooms.Clear();
            if (failed) { Status = "获取房间列表失败"; return; }
            for (int i = 0; i < result.m_nLobbiesMatching; i++) Rooms.Add(SteamMatchmaking.GetLobbyByIndex(i));
            Status = "找到 " + Rooms.Count + " 个房间";
        }
        public void Join(CSteamID id)
        {
            if (!id.IsValid()) return;
            if (Busy || NetworkClient.active || NetworkServer.active)
            { Status = "请先离开当前会话，再接受邀请"; return; }
            if (!Initialize() || !Bind()) return;
            phase = Phase.Joining; deadline = Time.realtimeSinceStartup + 40;
            pendingJoin = id;
            int attempt = ++generation;
            CallResult<LobbyEnter_t> request = null;
            request = CallResult<LobbyEnter_t>.Create((result, failed) =>
            {
                requests.Remove(request); request.Dispose();
                if (attempt != generation)
                {
                    if (!failed && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                        SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
                    return;
                }
                OnEntered(result, failed);
            });
            requests.Add(request);
            request.Set(SteamMatchmaking.JoinLobby(id));
            Status = "正在加入 Steam 房间……";
        }
        void OnEntered(LobbyEnter_t result, bool failed)
        {
            if (phase != Phase.Joining) return;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Leave("加入失败：房间已满、邀请过期或无访问资格"); return; }
            lobby = new CSteamID(result.m_ulSteamIDLobby); pendingJoin = default;
            if (Get("app") != appId.ToString() || Get("protocol") != Protocol || Get("build") != Application.version)
            { Leave("房间应用或版本不兼容"); return; }
            if (Get("state") != "ready" || !ulong.TryParse(Get("host"), out ulong host) ||
                host != SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID)
            { Leave("房间尚未就绪或房主已离开"); return; }
            if (!selector.SelectSessionTransport(transport)) { Leave("无法切换 Steam 传输"); return; }
            manager.networkAddress = host.ToString(); phase = Phase.Connecting;
            try { manager.StartClient(); Status = "正在连接房主……"; }
            catch (Exception e) { Leave("连接失败：" + e.Message); }
        }

        void CheckLobby()
        {
            if (!lobby.IsValid() || (phase != Phase.Connected && phase != Phase.Connecting)) return;
            if (Get("state") != "ready" || Get("host") != SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID.ToString())
                Leave("房主已结束会话");
        }

        public void Invite()
        {
            if (initialized && lobby.IsValid() && Get("state") == "ready")
                SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        }

        public void Leave(string message = "已离开 Steam 房间")
        {
            if (phase == Phase.Leaving) return;
            bool hosting = phase == Phase.Hosting;
            phase = Phase.Leaving;
            ++generation;
            configure = null;
            if (initialized)
            {
                if (lobby.IsValid())
                {
                    if (hosting) { SteamMatchmaking.SetLobbyData(lobby, "state", "closed"); SteamMatchmaking.SetLobbyJoinable(lobby, false); }
                    SteamMatchmaking.LeaveLobby(lobby);
                }
                if (pendingJoin.IsValid()) SteamMatchmaking.LeaveLobby(pendingJoin);
            }
            lobby = default; pendingJoin = default;
            if (manager && manager.transport == transport)
            {
                if (NetworkServer.active) manager.StopHost();
                else if (NetworkClient.active) manager.StopClient();
                selector.RestoreLocalTransport();
            }
            phase = Phase.Idle; Status = message;
        }

        void Update()
        {
            profile?.Tick(ProfileSteamReady);
            if (!initialized)
            {
                if (pendingJoin.IsValid()) Initialize();
                return;
            }
            SteamAPI.RunCallbacks();
            if (phase == Phase.Idle && pendingJoin.IsValid()) { var id = pendingJoin; pendingJoin = default; Join(id); }
            if (phase == Phase.Hosting)
            {
                if (!NetworkServer.active) { Leave("主机会话已结束"); return; }
                if (NetworkClient.localPlayer && Get("state") != "ready")
                {
                    try { Set("state", "ready"); SteamMatchmaking.SetLobbyJoinable(lobby, true); Status = "Steam 房间已就绪"; }
                    catch (Exception e) { Leave(e.Message); }
                }
                else if (Get("state") != "ready" && Time.realtimeSinceStartup > deadline) Leave("主机加载超时");
            }
            if (phase == Phase.Connecting && NetworkClient.localPlayer) { phase = Phase.Connected; Status = "已连接 Steam 房间"; }
            if (phase == Phase.Connected && !NetworkClient.isConnected) Leave("已与房主断开连接");
            if ((phase == Phase.Creating || phase == Phase.Joining || phase == Phase.Connecting) && Time.realtimeSinceStartup > deadline)
                Leave("连接超时，请重试");
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Leave(); matched?.Dispose();
            foreach (var request in requests) request.Dispose();
            requests.Clear();
            invited?.Dispose(); changed?.Dispose(); members?.Dispose();
            profile?.Dispose();
            if (initialized) SteamAPI.Shutdown();
            initialized = false; Instance = null;
        }

        // F8 is only used by legacy integration scenes.
        bool visible;
        Vector2 scroll;
        void OnGUI()
        {
            if (NewFrontEnd.UseReferenceHud) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F8) visible = !visible;
            if (!visible) return;
            GUILayout.BeginArea(new Rect(12, 12, 500, 520), GUI.skin.box);
            GUILayout.Label("Steam 联机测试（F8）"); GUILayout.Label(Status);
            if (initialized) GUILayout.Label("AppID: " + appId + (appId == 480 ? "（示例测试应用）" : ""));
            if (!Busy && !NetworkClient.active && !NetworkServer.active)
            {
                if (GUILayout.Button("使用当前建房参数创建 Steam 房间"))
                {
                    var creator = FindFirstObjectByType<RoomCreator>();
                    if (creator) creator.StartSteamGame(); else Status = "请先打开现有创建房间界面";
                }
                if (GUILayout.Button("刷新 Steam 房间列表")) Refresh();
                scroll = GUILayout.BeginScrollView(scroll);
                foreach (var id in Rooms)
                {
                    string label = SteamMatchmaking.GetLobbyData(id, "name") + " | " + SteamMatchmaking.GetLobbyData(id, "map") + " | " +
                        SteamMatchmaking.GetLobbyData(id, "mode") + " | " + SteamMatchmaking.GetNumLobbyMembers(id) + "/" + SteamMatchmaking.GetLobbyMemberLimit(id);
                    if (GUILayout.Button(label)) { Join(id); break; }
                }
                GUILayout.EndScrollView();
            }
            if (Busy)
            {
                if (GUILayout.Button("邀请 Steam 好友")) Invite();
                if (GUILayout.Button("取消 / 离开 Steam 会话")) Leave();
            }
            GUILayout.EndArea();
        }
    }
}
#endif
