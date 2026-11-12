using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AvatarPortability.Expressions;

namespace AvatarPortability.ExpressionClips
{
    /// <summary>Optional uGUI controls for the clip player on this same avatar. Never writes blendshapes directly.</summary>
    [DisallowMultipleComponent]
    public sealed class WendyExpressionClipUI : MonoBehaviour
    {
        public WendyExpressionClipPlayer player;
        public bool buildOnEnable = true, startExpanded = true, showUtilityEntries;
        [Min(240)] public float panelWidth = 320;
        [Range(.5f, 2)] public float uiScale = 1;
        [Min(0)] public float screenMargin = 16;
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        public Font fontOverride;
        [Range(-100, 100)] public int sortingOrder = 30;

        public Canvas CanvasRoot { get; private set; }
        public Button DefaultFaceButton { get; private set; }
        public Button RestoreOriginalButton { get; private set; }
        public Button ReplayButton { get; private set; }
        public Button PauseResumeButton { get; private set; }
        public Slider ProgressSlider { get; private set; }
        public Slider TransitionDurationSlider { get; private set; }

        enum Command { TogglePanel, DefaultFace, RestoreOriginal, Replay, PauseResume, Utility, Entry }
        sealed class Section { public GameObject root; public int visible; }
        sealed class EntryView { public int index; public bool utility; public Button button; public Section section; }
        readonly List<EntryView> entryViews = new List<EntryView>();
        readonly List<Section> sections = new List<Section>();
        Font font, ownedFont;
        EventSystem ownedEventSystem;
        BaseInputModule addedInputModule, borrowedInputModule;
        GameObject panel, progressRow;
        Text currentText, statusText, pauseText, utilityText, toggleText;
        Text transitionText;
        int displayedDuration = -1;
        bool expanded;
        int displayedIndex = int.MinValue;
        bool displayedPlaying;
        string displayedStatus;
        static readonly Color Normal = new Color(.22f, .27f, .35f, 1);
        static readonly Color Selected = new Color(.23f, .47f, .63f, 1);
        // Some projects retain ENABLE_INPUT_SYSTEM without the package assembly installed.
        // Resolve once, without a compile-time package reference.
        static readonly Type InputModuleType = ResolveInputModuleType();
        static readonly string[] DurationLabels = CreateDurationLabels();

        static string[] CreateDurationLabels()
        {
            var result = new string[101]; result[0] = "过渡时长：立即切换（0秒）";
            for (int i = 1; i <= 100; i++) result[i] = "过渡时长：" + (i * .01f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "秒";
            return result;
        }

        static Type ResolveInputModuleType()
        {
#if ENABLE_INPUT_SYSTEM
            Type type = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem", false);
            if (type != null && typeof(BaseInputModule).IsAssignableFrom(type)) return type;
#endif
            return typeof(StandaloneInputModule);
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!CanvasRoot)
            {
                if (buildOnEnable && !BuildUI(out string report)) Debug.LogWarning(report, this);
                return;
            }
            if (ownedEventSystem)
                ownedEventSystem.gameObject.SetActive(!EventSystem.current || EventSystem.current == ownedEventSystem);
            CanvasRoot.gameObject.SetActive(true);
            EnsureEventSystem();
        }
        void OnDisable() { if (CanvasRoot) CanvasRoot.gameObject.SetActive(false); }
        void OnDestroy() { DestroyUI(); }

        public bool BuildUI(out string report)
        {
            if (!Application.isPlaying) { report = "请在运行模式使用表情界面。"; return false; }
            if (!player) player = GetComponent<WendyExpressionClipPlayer>();
            if (!player || player.gameObject != gameObject)
            { report = "请指定当前角色根节点上的表情播放器。"; return false; }
            if (!player.library || player.library.entries == null)
            { report = "表情播放器尚未配置表情库。"; return false; }
            if (CanvasRoot) { report = "表情界面已创建。"; return true; }
            try
            {
                font = fontOverride ? fontOverride : MakeFont();
                GameObject canvasObject = new GameObject("Wendy Expression Clip Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(transform, false);
                CanvasRoot = canvasObject.GetComponent<Canvas>();
                CanvasRoot.renderMode = RenderMode.ScreenSpaceOverlay; CanvasRoot.sortingOrder = sortingOrder;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(Mathf.Max(320, referenceResolution.x), Mathf.Max(320, referenceResolution.y)) / Mathf.Clamp(uiScale, .5f, 2);
                scaler.matchWidthOrHeight = 1;
                expanded = startExpanded;
                RectTransform toggle = Rect("Toggle Panel", canvasObject.transform);
                toggle.anchorMin = toggle.anchorMax = Vector2.one; toggle.pivot = Vector2.one;
                toggle.anchoredPosition = new Vector2(-screenMargin, -screenMargin); toggle.sizeDelta = new Vector2(100, 32);
                Button toggleButton = ConfigureButton(toggle, "", Command.TogglePanel);
                toggleText = toggleButton.GetComponentInChildren<Text>();
                RectTransform panelRect = Rect("Expression Panel", canvasObject.transform);
                panel = panelRect.gameObject;
                panelRect.anchorMin = new Vector2(1, 0); panelRect.anchorMax = Vector2.one; panelRect.pivot = new Vector2(1, .5f);
                panelRect.anchoredPosition = new Vector2(-screenMargin, -24);
                panelRect.sizeDelta = new Vector2(Mathf.Max(240, panelWidth), -2 * screenMargin - 48);
                Image panelImage = panel.AddComponent<Image>(); panelImage.color = new Color(.075f, .09f, .13f, .96f);
                VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 8;
                layout.childControlWidth = true; layout.childForceExpandWidth = true;
                layout.childControlHeight = true; layout.childForceExpandHeight = false;
                Label("Title", panel.transform, "Wendy表情", 21, 30);
                currentText = Label("Current Expression", panel.transform, "请选择表情", 16, 44);
                statusText = Label("Status", panel.transform, "", 12, 40);
                Transform actions = Row("Face Actions", panel.transform);
                DefaultFaceButton = Button("Default Face", actions, "默认脸", Command.DefaultFace);
                RestoreOriginalButton = Button("Restore Original", actions, "恢复原脸", Command.RestoreOriginal);
                Transform playback = Row("Playback Actions", panel.transform);
                ReplayButton = Button("Replay", playback, "重播", Command.Replay);
                PauseResumeButton = Button("Pause Resume", playback, "暂停", Command.PauseResume);
                pauseText = PauseResumeButton.GetComponentInChildren<Text>();
                progressRow = Rect("Dynamic Clip Progress", panel.transform).gameObject;
                FixedHeight(progressRow, 28); ProgressSlider = CreateSlider(progressRow.transform, "Playback Progress", false);
                transitionText = Label("Transition Duration Label", panel.transform, "", 13, 22);
                GameObject transitionRow = Rect("Transition Duration Row", panel.transform).gameObject;
                FixedHeight(transitionRow, 26);
                TransitionDurationSlider = CreateSlider(transitionRow.transform, "Transition Duration", true);
                displayedDuration = -1;
                Button utility = Button("Show Utility", panel.transform, "", Command.Utility);
                utilityText = utility.GetComponentInChildren<Text>();
                BuildEntryList(panel.transform);
                EnsureEventSystem();
                ApplyVisibility();
                RefreshUI();
                report = "表情界面已创建。"; return true;
            }
            catch (Exception ex)
            {
                DestroyUI(); report = "表情界面创建失败，请查看控制台技术详情。";
                Debug.LogException(ex, this); return false;
            }
        }

        void EnsureEventSystem()
        {
            EventSystem system = EventSystem.current;
            if (!system)
            {
                if (!ownedEventSystem)
                {
                    GameObject events = new GameObject("Expression Clip EventSystem", typeof(EventSystem));
                    events.transform.SetParent(CanvasRoot.transform, false); ownedEventSystem = events.GetComponent<EventSystem>();
                }
                ownedEventSystem.gameObject.SetActive(true); ownedEventSystem.enabled = true; system = ownedEventSystem;
            }
            BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++) if (modules[i].isActiveAndEnabled) return;
            BaseInputModule module = system.GetComponent(InputModuleType) as BaseInputModule;
            if (module)
            {
                if (system != ownedEventSystem) borrowedInputModule = module;
                module.enabled = true;
            }
            else
            {
                addedInputModule = system.gameObject.AddComponent(InputModuleType) as BaseInputModule;
            }
        }

        void BuildEntryList(Transform parent)
        {
            RectTransform scrollRect = Rect("Expression Scroll", parent);
            var element = scrollRect.gameObject.AddComponent<LayoutElement>(); element.flexibleHeight = 1; element.minHeight = 80;
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            RectTransform viewport = Rect("Viewport", scrollRect); Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(.05f, .06f, .09f, .6f);
            viewport.gameObject.AddComponent<RectMask2D>(); scroll.viewport = viewport;
            RectTransform content = Rect("Author Categories", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var list = content.gameObject.AddComponent<VerticalLayoutGroup>(); list.spacing = 8;
            list.childControlHeight = true; list.childForceExpandHeight = false;
            list.childControlWidth = true; list.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            var byCategory = new Dictionary<string, Section>(StringComparer.Ordinal);
            for (int i = 0; i < player.library.entries.Length; i++)
            {
                var entry = player.library.entries[i];
                if (entry == null) continue;
                string category = WendyExpressionChinese.Category(entry.category);
                if (!byCategory.TryGetValue(category, out Section section))
                {
                    RectTransform group = Rect("Category " + category, content);
                    var groupLayout = group.gameObject.AddComponent<VerticalLayoutGroup>(); groupLayout.spacing = 4;
                    groupLayout.childControlWidth = true; groupLayout.childForceExpandWidth = true;
                    groupLayout.childControlHeight = true; groupLayout.childForceExpandHeight = false;
                    Label("Category Title", group, category, 14, 26).color = new Color(.53f, .8f, .94f);
                    section = new Section { root = group.gameObject }; byCategory.Add(category, section); sections.Add(section);
                }
                Button button = Button("Clip " + i.ToString("D3"), section.root.transform,
                    (entry.utility ? "[辅助] " : "") + WendyExpressionChinese.Name(entry.originalName), Command.Entry, i);
                entryViews.Add(new EntryView { index = i, utility = entry.utility, button = button, section = section });
            }
        }

        public bool TryGetEntryButton(int index, out Button button)
        {
            for (int i = 0; i < entryViews.Count; i++)
                if (entryViews[i].index == index) { button = entryViews[i].button; return true; }
            button = null; return false;
        }

        public void RefreshUI()
        {
            displayedIndex = int.MinValue; displayedStatus = null;
            if (!CanvasRoot || !player) return;
            pauseText.text = ""; statusText.text = ""; UpdateState();
        }
        void Update() { if (CanvasRoot && player) UpdateState(); }
        void UpdateState()
        {
            int index = player.CurrentIndex;
            bool valid = player.library && player.library.entries != null && index >= 0 && index < player.library.entries.Length;
            var entry = valid ? player.library.entries[index] : null;
            if (displayedIndex != index)
            {
                displayedIndex = index;
                currentText.text = entry != null ? "当前：" + WendyExpressionChinese.Name(entry.originalName) : "请选择表情";
                bool dynamic = entry != null && entry.timeVarying;
                progressRow.SetActive(dynamic); ReplayButton.interactable = valid;
                for (int i = 0; i < entryViews.Count; i++) entryViews[i].button.image.color = entryViews[i].index == index ? Selected : Normal;
            }
            bool playing = player.IsPlaying;
            PauseResumeButton.interactable = entry != null && (entry.timeVarying || player.IsTransitioning);
            if (displayedPlaying != playing || pauseText.text.Length == 0)
            { displayedPlaying = playing; pauseText.text = playing ? "暂停" : "继续"; }
            string status = player.StatusMessage;
            if (!string.Equals(displayedStatus, status, StringComparison.Ordinal))
            { displayedStatus = status; statusText.text = status ?? ""; }
            RestoreOriginalButton.interactable = player.HasBaseline;
            DefaultFaceButton.interactable = player.library && player.library.neutralIndex >= 0;
            ProgressSlider.SetValueWithoutNotify(Mathf.Clamp01(player.NormalizedTime));
            float duration = player.transitionDuration;
            duration = float.IsNaN(duration) || float.IsInfinity(duration) ? 0 : Mathf.Clamp01(duration);
            TransitionDurationSlider.SetValueWithoutNotify(duration);
            int hundredths = Mathf.RoundToInt(duration * 100);
            if (displayedDuration != hundredths)
            { displayedDuration = hundredths; transitionText.text = DurationLabels[hundredths]; }
        }

        void SetTransitionDuration(float value)
        {
            if (!player) return;
            player.transitionDuration = Mathf.RoundToInt(Mathf.Clamp01(value) * 100) * .01f;
            UpdateState();
        }

        void Seek(float value)
        {
            if (player && !player.Seek(value, out string report)) statusText.text = report;
        }
        internal void Execute(int command, int index)
        {
            if (!player) return;
            string report = "";
            switch ((Command)command)
            {
                case Command.TogglePanel: expanded = !expanded; ApplyVisibility(); return;
                case Command.Utility: showUtilityEntries = !showUtilityEntries; ApplyVisibility(); return;
                case Command.Entry: player.CrossFade(index, player.transitionDuration, out report); break;
                case Command.DefaultFace: player.CrossFadeNeutral(player.transitionDuration, out report); break;
                case Command.RestoreOriginal: player.FadeToBaseline(player.transitionDuration, out report); break;
                case Command.Replay: player.Play(player.CurrentIndex, out report); break;
                case Command.PauseResume: if (player.IsPlaying) player.Pause(); else player.Resume(); break;
            }
            RefreshUI();
            if (!string.IsNullOrEmpty(report)) statusText.text = report;
        }
        void ApplyVisibility()
        {
            panel.SetActive(expanded); toggleText.text = expanded ? "收起表情" : "展开表情";
            utilityText.text = showUtilityEntries ? "隐藏辅助项" : "显示辅助项";
            for (int i = 0; i < sections.Count; i++) sections[i].visible = 0;
            for (int i = 0; i < entryViews.Count; i++)
            {
                EntryView view = entryViews[i]; bool visible = showUtilityEntries || !view.utility;
                view.button.gameObject.SetActive(visible); if (visible) view.section.visible++;
            }
            for (int i = 0; i < sections.Count; i++) sections[i].root.SetActive(sections[i].visible > 0);
        }

        public void DestroyUI()
        {
            if (borrowedInputModule) borrowedInputModule.enabled = false;
            borrowedInputModule = null;
            if (addedInputModule && (!ownedEventSystem || addedInputModule.gameObject != ownedEventSystem.gameObject)) Destroy(addedInputModule);
            addedInputModule = null;
            if (CanvasRoot) { CanvasRoot.gameObject.SetActive(false); Destroy(CanvasRoot.gameObject); }
            CanvasRoot = null; ownedEventSystem = null;
            if (ownedFont) Destroy(ownedFont);
            ownedFont = null; font = null; entryViews.Clear(); sections.Clear();
        }

        Font MakeFont()
        {
            string[] preferred = { "Microsoft YaHei UI", "Microsoft YaHei", "Meiryo", "Yu Gothic", "Noto Sans CJK SC", "Noto Sans CJK JP", "Arial Unicode MS" };
            try
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                var available = new List<string>();
                for (int i = 0; i < preferred.Length; i++) if (installed.Contains(preferred[i])) available.Add(preferred[i]);
                if (available.Count > 0)
                {
                    ownedFont = Font.CreateDynamicFontFromOSFont(available.ToArray(), 18);
                    if (ownedFont) return ownedFont;
                }
            }
            catch (NotSupportedException) { }
            catch (ArgumentException) { }
#if UNITY_2023_1_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false); return result;
        }
        static void Stretch(RectTransform value)
        { value.anchorMin = Vector2.zero; value.anchorMax = Vector2.one; value.offsetMin = value.offsetMax = Vector2.zero; }
        static void FixedHeight(GameObject value, float height)
        {
            var size = value.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            size.flexibleHeight = 0;
        }
        Transform Row(string name, Transform parent)
        {
            RectTransform row = Rect(name, parent); FixedHeight(row.gameObject, 34);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true; return row;
        }
        Text Label(string name, Transform parent, string text, int size, float height)
        {
            RectTransform rect = Rect(name, parent); FixedHeight(rect.gameObject, height);
            Text label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size;
            label.text = text; label.color = Color.white; label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        Button Button(string name, Transform parent, string label, Command command, int index = -1)
        { RectTransform rect = Rect(name, parent); FixedHeight(rect.gameObject, 34); return ConfigureButton(rect, label, command, index); }
        Button ConfigureButton(RectTransform rect, string label, Command command, int index = -1)
        {
            Image image = rect.gameObject.AddComponent<Image>(); image.color = Normal;
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            RectTransform caption = Rect("Label", rect); Stretch(caption); caption.offsetMin = new Vector2(8, 0); caption.offsetMax = new Vector2(-8, 0);
            Text text = caption.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = 14;
            text.text = label; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
            var link = rect.gameObject.AddComponent<WendyExpressionClipUIAction>();
            link.owner = this; link.command = (int)command; link.index = index; button.onClick.AddListener(link.Invoke);
            return button;
        }
        Slider CreateSlider(Transform parent, string name, bool transition)
        {
            RectTransform rect = Rect(name, parent); Stretch(rect);
            Slider slider = rect.gameObject.AddComponent<Slider>();
            RectTransform background = Rect("Background", rect); Stretch(background);
            background.offsetMin = new Vector2(0, 9); background.offsetMax = new Vector2(0, -9);
            background.gameObject.AddComponent<Image>().color = Normal;
            RectTransform fillArea = Rect("Fill Area", rect); Stretch(fillArea);
            fillArea.offsetMin = new Vector2(8, 9); fillArea.offsetMax = new Vector2(-8, -9);
            RectTransform fill = Rect("Fill", fillArea); Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = Selected; slider.fillRect = fill;
            RectTransform handleArea = Rect("Handle Area", rect); Stretch(handleArea);
            handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
            RectTransform handle = Rect("Handle", handleArea); handle.sizeDelta = new Vector2(16, 22);
            Image image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.7f, .88f, 1);
            slider.handleRect = handle; slider.targetGraphic = image; slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1;
            if (transition) slider.onValueChanged.AddListener(SetTransitionDuration);
            else slider.onValueChanged.AddListener(Seek);
            return slider;
        }
    }

}
