using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MTPSKIT
{
    public static class RangeCharacterSelection
    {
        // IDs 2..11 belong to the existing SuperBot appearances.
        public const int FirstPortable = 12;
        public static readonly int[] PlayerChoices = { 0, 1, 12, 13, 14, 15 };
        static readonly string[] PortableNames = { "温蒂 Wendy", "诗乃 Shinano", "萨菲 Sapphy", "米尔缇娜 Milltina FC" };
        static readonly string[] PortableKeys = { "Wendy", "Shinano", "Sapphy", "Milltina" };
        public static int Selected { get; private set; } = FirstPortable;
        internal static int SavedDefault()
        {
            // One-time migration from the previous FAF/soldier default, then preserve later choices.
            if (PlayerPrefs.GetInt("FrontEnd.WendyDefault.v1", 0) == 0)
            {
                PlayerPrefs.SetInt("FrontEnd.LastCharacter", FirstPortable);
                PlayerPrefs.SetInt("HeroWeapon." + FirstPortable, -1);
                PlayerPrefs.SetInt("FrontEnd.WendyDefault.v1", 1);
                PlayerPrefs.Save();
            }
            return Validate(PlayerPrefs.GetInt("FrontEnd.LastCharacter", FirstPortable));
        }
        public static bool IsPortable(int selected) => selected >= FirstPortable && selected < FirstPortable + PortableKeys.Length;
        public static int Validate(int selected) => selected == 1 || IsPortable(selected) ? selected : 0;
        public static void SetSelected(int selected) => Selected = Validate(selected);
        public static string DisplayName(int selected) => IsPortable(selected) ? PortableNames[selected - FirstPortable] : selected == 1 ? "FAF 小白人" : "原版士兵";
        public static string CatalogId(int selected) => IsPortable(selected) ? PortableKeys[selected - FirstPortable].ToLowerInvariant() : selected == 1 ? "faf" : "soldier";
        public static string ResourcePath(int selected) => IsPortable(selected) ? "PortableCharacters/" + PortableKeys[selected - FirstPortable] : selected == 1 ? "RangeRobot" : null;
        public static Sprite Portrait(int selected) => IsPortable(selected)
            ? Resources.Load<Sprite>("CharacterPortraits/" + PortableKeys[selected - FirstPortable] + "_Icon") : null;
        public static void AddPortrait(Button button, int selected, float size)
        {
            var portrait = Portrait(selected);
            if (!portrait) return;
            var image = FrontEndUI.Rect(button.transform, "Character portrait", 5, 5, size, size).gameObject.AddComponent<Image>();
            image.sprite = portrait;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (!caption) return;
            var buttonRect = (RectTransform)button.transform;
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = captionRect.anchorMax = captionRect.pivot = new Vector2(0, 1);
            captionRect.anchoredPosition = new Vector2(size + 12, 0);
            captionRect.sizeDelta = new Vector2(buttonRect.rect.width - size - 20, buttonRect.rect.height);
            caption.fontSize = buttonRect.rect.height < 60 ? 20 : 25;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
        }
        public static bool IsAvailable(int selected) => selected == 0 || (Validate(selected) == selected && Resources.Load<GameObject>(ResourcePath(selected)));
        public static bool TryGetSelection(string id, out int selected)
        {
            foreach (int choice in PlayerChoices)
                if (CatalogId(choice) == id) { selected = choice; return true; }
            selected = 0;
            return false;
        }
        public static int Next(int selected)
        {
            int index = Array.IndexOf(PlayerChoices, selected);
            return PlayerChoices[(index + 1) % PlayerChoices.Length];
        }
        public static void Show(RoomCreator owner, Action enter)
        {
            if (owner.transform.Find("CharacterSelection")) return;
            var root = new GameObject("CharacterSelection", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(owner.transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var background = root.AddComponent<Image>();
            background.color = new Color(.035f, .045f, .065f, .98f);
            var font = owner.MapselectionDropdown.captionText.font;
            TMP_Text Label(string text, Vector2 position, Vector2 size)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(root.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = size; rect.anchoredPosition = position;
                var label = go.GetComponent<TextMeshProUGUI>(); label.font = font;
                label.text = text; label.fontSize = 30; label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white; label.raycastTarget = false;
                return label;
            }
            Button Button(string title, Vector2 position, Action action)
            {
                var label = Label(title, position, new Vector2(380, 70));
                var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(root.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(380, 70); rect.anchoredPosition = position;
                go.GetComponent<Image>().color = new Color(.12f,.22f,.32f);
                label.transform.SetParent(go.transform, false);
                label.rectTransform.anchoredPosition = Vector2.zero;
                var button = go.GetComponent<Button>(); button.onClick.AddListener(() => action());
                return button;
            }
            Label("选择靶场角色", new Vector2(0, 220), new Vector2(650, 65));
            var status = Label("", new Vector2(0, -115), new Vector2(1000, 65));
            void Refresh() => status.text = "当前角色：" + DisplayName(Selected);
            for (int i = 0; i < PlayerChoices.Length; i++)
            {
                int choice = PlayerChoices[i];
                var button = Button(DisplayName(choice), new Vector2(i % 2 == 0 ? -210 : 210, 135 - i / 2 * 85), () => { SetSelected(choice); Refresh(); });
                button.interactable = IsAvailable(choice);
                AddPortrait(button, choice, 60);
            }
            if (!IsAvailable(Selected)) Selected = 0;
            Refresh();
            Button("进入靶场", new Vector2(0, -190), () => { root.SetActive(false); UnityEngine.Object.Destroy(root); enter(); });
            Button("返回", new Vector2(0, -260), () => UnityEngine.Object.Destroy(root));
        }
    }
}
