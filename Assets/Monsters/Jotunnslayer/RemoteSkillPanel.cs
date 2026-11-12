using Mirror;
using System.Collections.Generic;
using MTPSKIT.Gameplay;
using MTPSKIT.UI;
using UnityEngine;

namespace Jotunnslayer
{
    // Owner-only UI. It renders server offers and never runs or chooses combat skills locally.
    public sealed class RemoteSkillPanel : MonoBehaviour
    {
        private PlayerInstance player;
        private SkillProgress profile;
        private uint session, lastRevision;
        private int creditedGold;
        private bool initialized, waiting, open, ownsCursor, english;
        private JotunnSkillSnapshot state;
        private CharacterInstance statsActor;
        private bool statsPending;
        private Font font;
        private GUIStyle label, button;
        private Vector2 scroll;
        private string error;
        private string summary, openLabel, rerollLabel, banishLabel, sealLabel;
        private string[] detailLabels;
        private readonly List<int> navigation = new List<int>(32);
        private int focusedControl, lastDirection;
        private float nextRepeat;
        private bool scrollFocus;
        private Rect focusedRect;
        private const int AcceptControl = 100, BanishControl = 200, SealControl = 300, ExcludeControl = 400;
        private const int RerollControl = 500, UnsealControl = 600, CloseControl = 700, LeaveControl = 701;
        private static readonly string[] ClassNames = { "狂战士", "卜士", "返生之人", "盗贼", "监守者", "女武神" };
        internal JotunnSkillSnapshot Snapshot => state;
        internal int FocusedControl => focusedControl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static SkillProgress ValidationProfile { get; set; }
        internal static bool? ValidationEnglish { get; set; }
        internal int ValidationGold => profile.Gold;
#endif

        internal void Begin(PlayerInstance owner, uint token)
        {
            if (session == token && player == owner) return;
            if (statsActor && statsActor.TryGetComponent<RemoteRaidStats>(out var previousStats)) previousStats.Clear();
            statsActor = null;
            statsPending = true;
            player = owner;
            session = token;
            lastRevision = 0;
            creditedGold = 0;
            state = default;
            error = null;
            initialized = waiting = false;
            focusedControl = 0;
            scroll = Vector2.zero;
            english = PlayerPrefs.GetInt("FrontEnd.English", 0) != 0;
            profile = new SkillProgress(SkillCatalog.Load(), "jotunnslayer.preview.v1", 10, 15);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ValidationProfile != null) { profile = ValidationProfile; ValidationProfile = null; }
            if (ValidationEnglish.HasValue) { english = ValidationEnglish.Value; ValidationEnglish = null; }
#endif
            if (!font) font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18);
            RebuildNavigation();
            SetOpen(true);
        }

        internal void ChooseClass(RaidClass raidClass)
        {
            if (initialized || waiting || !player || !player.isLocalPlayer) return;
            waiting = true;
            error = null;
            player.CmdBeginJotunnSkills(session, (byte)raidClass, profile.ExportLearnedNodes(), english);
        }

        internal void Receive(JotunnSkillSnapshot snapshot)
        {
            if (snapshot.session != session || snapshot.revision <= lastRevision) return;
            lastRevision = snapshot.revision;
            if (snapshot.gold > creditedGold)
            {
                profile.TryCollectGold(snapshot.gold - creditedGold);
                creditedGold = snapshot.gold;
            }
            state = snapshot;
            statsPending = true;
            initialized = true;
            waiting = false;
            summary = Text($"等级 {state.level} | 经验 {state.experience} | 可选 {state.pending} | 本局金币 {state.gold}",
                $"Level {state.level} | XP {state.experience} | Choices {state.pending} | Raid gold {state.gold}");
            openLabel = Text($"F6 / View 技能奖励 ({state.pending})", $"F6 / View Skill Rewards ({state.pending})");
            rerollLabel = Text($"重掷 ({state.rerolls})", $"Reroll ({state.rerolls})");
            banishLabel = Text($"放逐 ({state.banishes})", $"Banish ({state.banishes})");
            sealLabel = Text($"封存 ({state.sealedLabels.Length}/{state.sealCapacity})",
                $"Seal ({state.sealedLabels.Length}/{state.sealCapacity})");
            detailLabels = new string[state.labels.Length];
            for (int i = 0; i < detailLabels.Length; i++)
                detailLabels[i] = state.details != null && i < state.details.Length && !string.IsNullOrEmpty(state.details[i])
                    ? Text("下一阶效果：", "Next rank: ") + state.details[i] : string.Empty;
            RebuildNavigation();
            if (!state.alive) SetOpen(false);
        }

        internal void Request(JotunnSkillAction action, int index)
        {
            if (!initialized || waiting || !state.alive || !player || !player.isLocalPlayer) return;
            // A new server snapshot releases the button lock, including for a rejected request.
            waiting = true;
            player.CmdJotunnSkillAction(session, state.revision, action, index);
        }

        private void Update()
        {
            if (!NetworkClient.active || !player || !player.isLocalPlayer) { Destroy(this); return; }
            if (statsPending || statsActor != player.MyCharacter) ApplyOwnerStats();
            if (ClientFrontend.Pause) return;
            bool toggle = Input.GetKeyDown(KeyCode.F6) || Input.GetKeyDown(KeyCode.JoystickButton6);
            if (initialized && toggle && (open || ClientFrontend.GamePlayInput())) SetOpen(!open);
            if (open)
            {
                float vertical = Input.GetAxisRaw("Vertical"), horizontal = Input.GetAxisRaw("Horizontal");
                int direction = Mathf.Abs(vertical) >= Mathf.Abs(horizontal)
                    ? (vertical > .5f ? -1 : vertical < -.5f ? 1 : 0)
                    : (horizontal > .5f ? 1 : horizontal < -.5f ? -1 : 0);
                if (direction != 0 && (direction != lastDirection || Time.unscaledTime >= nextRepeat))
                {
                    MoveFocus(direction);
                    nextRepeat = Time.unscaledTime + (direction != lastDirection ? .35f : .12f);
                }
                lastDirection = direction;
                if (Input.GetButtonDown("Submit")) ActivateFocused();
                // Escape remains owned by the existing pause menu.
                if (initialized && (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1))) SetOpen(false);
            }
            if (initialized && !open && ClientFrontend.GamePlayInput() && Input.GetKeyDown(player && player.HasHeroLoadout ? KeyCode.H : KeyCode.V))
                Request(JotunnSkillAction.Dash, -1);
        }

        private void ApplyOwnerStats()
        {
            var body = player ? player.MyCharacter : null;
            if (statsActor && (statsActor != body || !state.alive || statsActor.netId != state.character))
            {
                if (statsActor.TryGetComponent<RemoteRaidStats>(out var previous)) previous.Clear();
                statsActor = null;
            }
            if (!initialized || !state.alive || !body || body.netId != state.character) return;
            if (!body.TryGetComponent<RemoteRaidStats>(out var stats)) stats = body.gameObject.AddComponent<RemoteRaidStats>();
            stats.Receive(body, state.attackSpeed);
            statsActor = body;
            statsPending = false;
        }

        private void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            lastDirection = 0;
            scrollFocus = value;
            ownsCursor = value;
            ClientFrontend.ShowCursor(value);
        }

        private string Text(string chinese, string en) => english ? en : chinese;

        private void OnGUI()
        {
            if (!player || !player.isLocalPlayer || ClientFrontend.Pause) return;
            label ??= new GUIStyle(GUI.skin.label) { font = font, wordWrap = true };
            button ??= new GUIStyle(GUI.skin.button) { font = font, wordWrap = true };
            if (initialized && !open)
            {
                if (GUI.Button(new Rect(16, 16, Mathf.Min(330, Screen.width - 32), 34), openLabel, button)) SetOpen(true);
                return;
            }
            Rect panelRect = new Rect(16, 16, Mathf.Min(520, Screen.width - 32), Mathf.Min(760, Screen.height - 32));
            // Keep gameplay HUD and world text from showing through the reward descriptions.
            Color previousColor = GUI.color;
            GUI.color = new Color(.065f, .075f, .09f, 1f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            bool enabledBefore = GUI.enabled;
            GUILayout.Label(initialized ? summary : Text("选择本局职业", "Choose your raid class"), label);
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, label);
            GUILayout.Label(Text("方向键 / 摇杆选择 · 回车 / A 确认", "Arrows / Stick: navigate · Enter / A: confirm"), label);
            scroll = GUILayout.BeginScrollView(scroll);
            focusedRect = default;
            if (!initialized)
            {
                for (int i = 0; i < ClassNames.Length; i++)
                    DrawControl(i, english ? ((RaidClass)i).ToString() : ClassNames[i], enabledBefore, true);
            }
            else
            {
                for (int i = 0; i < state.labels.Length; i++)
                {
                    byte flags = state.actions[i];
                    DrawControl(AcceptControl + i, state.labels[i], enabledBefore, true);
                    GUI.enabled = enabledBefore;
                    if (!string.IsNullOrEmpty(detailLabels[i])) GUILayout.Label(detailLabels[i], label);
                    if ((flags & 14) != 0)
                    {
                        GUILayout.BeginHorizontal();
                        if ((flags & 2) != 0) DrawControl(BanishControl + i, banishLabel, enabledBefore, true);
                        if ((flags & 4) != 0) DrawControl(SealControl + i, sealLabel, enabledBefore, true);
                        if ((flags & 8) != 0) DrawControl(ExcludeControl + i, Text("排除神祇", "Exclude God"), enabledBefore, true);
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Space(8);
                }
                if (state.rerolls > 0) DrawControl(RerollControl, rerollLabel, enabledBefore, true);
                for (int i = 0; i < state.sealedLabels.Length; i++)
                    DrawControl(UnsealControl + i, Text("解除封存 · ", "Unseal · ") + state.sealedLabels[i], enabledBefore, true);
            }
            GUILayout.EndScrollView();
            if (scrollFocus && Event.current.type == EventType.Repaint && focusedRect.height > 0)
            {
                float visible = GUILayoutUtility.GetLastRect().height;
                if (focusedRect.yMin < scroll.y) scroll.y = focusedRect.yMin;
                else if (focusedRect.yMax > scroll.y + visible) scroll.y = focusedRect.yMax - visible;
                scrollFocus = false;
            }
            GUILayout.BeginHorizontal();
            if (initialized) DrawControl(CloseControl, Text("关闭 (F6 / B)", "Close (F6 / B)"), enabledBefore, false);
            DrawControl(LeaveControl, Text("离开对局", "Leave match"), enabledBefore, false);
            GUILayout.EndHorizontal();
            GUI.enabled = enabledBefore;
            GUILayout.EndArea();
        }

        private void DrawControl(int id, string caption, bool enabledBefore, bool inScroll)
        {
            GUI.enabled = enabledBefore && IsControlEnabled(id);
            Color background = GUI.backgroundColor;
            if (id == focusedControl) GUI.backgroundColor = new Color(.88f, .65f, .32f);
            if (GUILayout.Button(caption, button, GUILayout.MinHeight(34)))
            {
                focusedControl = id;
                ActivateFocused();
            }
            if (inScroll && id == focusedControl && Event.current.type == EventType.Repaint)
                focusedRect = GUILayoutUtility.GetLastRect();
            GUI.backgroundColor = background;
            GUI.enabled = enabledBefore;
        }

        private bool IsControlEnabled(int id)
        {
            if (id == LeaveControl) return true;
            if (id == CloseControl) return initialized;
            if (waiting) return false;
            if (!initialized) return id >= 0 && id < ClassNames.Length;
            if (!state.alive) return false;
            if (id == RerollControl) return state.rerolls > 0 && state.labels.Length > 0;
            if (id >= UnsealControl && id < CloseControl) return id - UnsealControl < state.sealedLabels.Length;
            int group = id / 100, index = id % 100;
            int bit = group == 1 ? 1 : group == 2 ? 2 : group == 3 ? 4 : group == 4 ? 8 : 0;
            return bit != 0 && index < state.actions.Length && (state.actions[index] & bit) != 0;
        }

        private void RebuildNavigation()
        {
            navigation.Clear();
            if (!initialized) for (int i = 0; i < ClassNames.Length; i++) navigation.Add(i);
            else
            {
                for (int i = 0; i < state.actions.Length; i++)
                {
                    byte flags = state.actions[i];
                    if ((flags & 1) != 0) navigation.Add(AcceptControl + i);
                    if ((flags & 2) != 0) navigation.Add(BanishControl + i);
                    if ((flags & 4) != 0) navigation.Add(SealControl + i);
                    if ((flags & 8) != 0) navigation.Add(ExcludeControl + i);
                }
                if (state.rerolls > 0 && state.labels.Length > 0) navigation.Add(RerollControl);
                for (int i = 0; i < state.sealedLabels.Length; i++) navigation.Add(UnsealControl + i);
                navigation.Add(CloseControl);
            }
            navigation.Add(LeaveControl);
            if (!navigation.Contains(focusedControl)) focusedControl = navigation[0];
            scrollFocus = focusedControl < CloseControl;
        }

        internal void MoveFocus(int direction)
        {
            int index = navigation.IndexOf(focusedControl), step = direction < 0 ? -1 : 1;
            for (int i = index + step; i >= 0 && i < navigation.Count; i += step)
            {
                if (!IsControlEnabled(navigation[i])) continue;
                focusedControl = navigation[i];
                scrollFocus = focusedControl < CloseControl;
                break;
            }
        }

        internal void ActivateFocused()
        {
            int id = focusedControl;
            if (!IsControlEnabled(id)) return;
            if (id == LeaveControl) { if (MTPSKIT.DNNetworkManager.Instance) MTPSKIT.DNNetworkManager.Instance.StopClient(); return; }
            if (id == CloseControl) { SetOpen(false); return; }
            if (!initialized) { ChooseClass((RaidClass)id); return; }
            if (id == RerollControl) { Request(JotunnSkillAction.Reroll, -1); return; }
            if (id >= UnsealControl) { Request(JotunnSkillAction.Unseal, id - UnsealControl); return; }
            int group = id / 100;
            Request(group == 1 ? JotunnSkillAction.Accept : group == 2 ? JotunnSkillAction.Banish :
                group == 3 ? JotunnSkillAction.Seal : JotunnSkillAction.ExcludeGod, id % 100);
        }

        internal void End(uint token) { if (token == session) Destroy(this); }

        internal void ApproveDash(uint token, uint revision, uint character, Vector3 direction)
        {
            if (token != session || !initialized || !player || !player.MyCharacter ||
                player.MyCharacter.netId != character) return;
            RemoteRaidDash.Approve(player.MyCharacter, token, revision, direction);
        }

        internal void RejectBegin(uint token)
        {
            if (token != session || initialized) return;
            waiting = false;
            error = Text("技能档案校验失败，请返回主菜单检查技能树。", "Loadout rejected. Check your skill tree in the main menu.");
        }

        private void OnDestroy()
        {
            if (statsActor && statsActor.TryGetComponent<RemoteRaidStats>(out var stats)) stats.Clear();
            if (player && player.MyCharacter && player.MyCharacter.TryGetComponent<RemoteRaidDash>(out var dash)) dash.Cancel();
            if (ownsCursor) ClientFrontend.ShowCursor(false);
            if (font) Destroy(font);
        }
    }
}
