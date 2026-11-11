using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    // Shared screen-space layout primitives. Reference resolution follows the supplied screenshots.
    public static class FrontEndUI
    {
        public static TMP_FontAsset Font;
        public static readonly Color Ink = new Color(.055f, .065f, .09f, .96f);
        public static readonly Color Panel = new Color(.10f, .12f, .17f, .95f);
        public static readonly Color Gold = new Color(.66f, .57f, .28f);
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            return r;
        }
        public static Sprite Sprite(string name) => Resources.Load<Sprite>("FrontEndReference/" + name);
        public static Image Box(Transform p, string name, float x, float y, float w, float h, Color color, string sprite = null)
        {
            var r = Rect(p, name, x, y, w, h); var im = r.gameObject.AddComponent<Image>();
            im.color = color;
            if (sprite != null) { im.sprite = Sprite(sprite); im.type = Image.Type.Sliced; }
            return im;
        }
        public static TMP_Text Text(Transform p, string content, float x, float y, float w, float h, int size = 24,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var r = Rect(p, "Text", x, y, w, h); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font ? Font : TMP_Settings.defaultFontAsset; t.text = content; t.fontSize = size;
            t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal; return t;
        }
        public static Button Button(Transform p, string title, float x, float y, float w, float h, Action action, Color? color = null)
        {
            var bg = Box(p, title, x, y, w, h, color ?? new Color(.28f,.33f,.39f), "texUICleanButton");
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg;
            var c = b.colors; c.highlightedColor = new Color(1f,.93f,.62f); c.selectedColor = c.highlightedColor;
            c.pressedColor = new Color(.65f,.74f,.84f); b.colors = c;
            Text(bg.transform, title, 16, 0, w-32, h, 25);
            if (action != null) b.onClick.AddListener(() => action());
            else b.interactable = false;
            return b;
        }
        public static Canvas Canvas(Transform owner, int order)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(owner, false);
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920,1080); s.matchWidthOrHeight = .5f;
            return c;
        }
    }
}
