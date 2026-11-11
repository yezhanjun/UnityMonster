using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.Battlefield
{
    /// <summary>Client-only, pooled overhead health bars. Reads replicated Health values.</summary>
    public sealed class BattlefieldHealthBars : MonoBehaviour
    {
        sealed class Bar
        {
            public BattlefieldEntity Entity;
            public RectTransform Root, Fill;
            public Text Label, Status;
            public int Health = -1, MaxHealth = -1, Team = -1;
            public BattlefieldBuildingState State = (BattlefieldBuildingState)(-1);
            public bool Building;
        }

        readonly List<Bar> active = new List<Bar>(80);
        readonly Stack<Bar> pool = new Stack<Bar>(64);
        readonly Dictionary<BattlefieldEntity, Bar> bindings = new Dictionary<BattlefieldEntity, Bar>(80);
        readonly List<Rect> occupied = new List<Rect>(80);
        BattlefieldGamemode mode;
        Camera worldCamera;
        RectTransform layer;
        public int VisibleCount { get; private set; }

        public void Initialize(BattlefieldGamemode gamemode)
        {
            mode = gamemode;
            var root = new GameObject("Battlefield Health Bars", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            layer = (RectTransform)root.transform;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            for (int i = 0; i < 64; i++) pool.Push(CreateBar());
        }

        Bar CreateBar()
        {
            var bar = new Bar();
            bar.Root = new GameObject("Overhead Health", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.Root.SetParent(layer, false);
            bar.Root.anchorMin = bar.Root.anchorMax = new Vector2(.5f, .5f);
            bar.Root.pivot = new Vector2(.5f, 0);
            var background = CreateImage("Health Background", bar.Root, new Color(.02f, .025f, .035f, .95f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = new Vector2(1, 0);
            background.pivot = new Vector2(.5f, 0);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(0, 10);
            bar.Fill = CreateImage("Health Fill", background, Color.white);
            bar.Fill.anchorMin = Vector2.zero;
            bar.Fill.anchorMax = Vector2.one;
            bar.Fill.offsetMin = new Vector2(0, 1);
            bar.Fill.offsetMax = new Vector2(0, -1);
            bar.Label = CreateText("Health Value", bar.Root, 15);
            bar.Label.rectTransform.anchoredPosition = new Vector2(0, 10);
            bar.Status = CreateText("Protection State", bar.Root, 12);
            bar.Status.rectTransform.anchoredPosition = new Vector2(0, 31);
            bar.Root.gameObject.SetActive(false);
            return bar;
        }

        static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        Text CreateText(string name, Transform parent, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = mode.Settings.UIFont;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = new Vector2(0, 0);
            text.rectTransform.anchorMax = new Vector2(1, 0);
            text.rectTransform.pivot = new Vector2(.5f, 0);
            text.rectTransform.sizeDelta = new Vector2(0, 21);
            return text;
        }

        void LateUpdate()
        {
            if (!mode || !layer) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var bar = active[i];
                if (bar.Entity && bar.Entity.Alive) continue;
                bindings.Remove(bar.Entity);
                active.RemoveAt(i);
                bar.Entity = null;
                bar.Root.gameObject.SetActive(false);
                pool.Push(bar);
            }
            for (int i = 0; i < mode.Entities.Count; i++)
            {
                var entity = mode.Entities[i];
                if (!entity || !entity.Alive || entity.Kind == BattlefieldKind.Player || bindings.ContainsKey(entity)) continue;
                var bar = pool.Count > 0 ? pool.Pop() : CreateBar();
                bar.Entity = entity;
                bar.Health = bar.MaxHealth = bar.Team = -1;
                bar.State = (BattlefieldBuildingState)(-1);
                bar.Building = entity.Kind >= BattlefieldKind.Tower;
                bar.Root.sizeDelta = new Vector2(bar.Building ? 180 : 112, bar.Building ? 52 : 31);
                bar.Status.gameObject.SetActive(bar.Building);
                active.Add(bar);
                bindings.Add(entity, bar);
            }

            if (!worldCamera || !worldCamera.isActiveAndEnabled)
                worldCamera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            VisibleCount = 0;
            occupied.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var bar = active[i];
                UpdateHealth(bar);
                bool visible = TryPosition(bar, out var position);
                if (bar.Root.gameObject.activeSelf != visible) bar.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                var size = bar.Root.sizeDelta;
                var rect = new Rect(position.x - size.x * .5f, position.y, size.x, size.y);
                // Preserve the entity order; only offset colliding labels, without sorting every frame.
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    bool overlap = false;
                    for (int n = 0; n < occupied.Count; n++)
                    {
                        if (!rect.Overlaps(occupied[n])) continue;
                        rect.y = occupied[n].yMax + 3;
                        overlap = true;
                        break;
                    }
                    if (!overlap) break;
                }
                bar.Root.anchoredPosition = new Vector2(position.x, rect.y);
                occupied.Add(rect);
                VisibleCount++;
            }
        }

        void UpdateHealth(Bar bar)
        {
            var health = bar.Entity.Health;
            if (bar.Health != health.CurrentHealth || bar.MaxHealth != health.MaxHealth)
            {
                bar.Health = health.CurrentHealth;
                bar.MaxHealth = health.MaxHealth;
                string name = bar.Entity.Kind == BattlefieldKind.Minion ? "小兵" : bar.Entity.Kind == BattlefieldKind.Tower ? "防御塔" : "基地";
                bar.Label.text = name + "  " + bar.Health + " / " + bar.MaxHealth;
                bar.Fill.anchorMax = new Vector2(Mathf.Clamp01((float)bar.Health / Mathf.Max(1, bar.MaxHealth)), 1);
            }
            if (bar.Team != health.Team)
            {
                bar.Team = health.Team;
                bar.Fill.GetComponent<Image>().color = health.Team == 0 ? new Color(.15f, .8f, 1) : new Color(1, .38f, .12f);
            }
            if (bar.Building && bar.State != bar.Entity.BuildingState)
            {
                bar.State = bar.Entity.BuildingState;
                bar.Status.text = bar.State == BattlefieldBuildingState.Locked ? "前置未摧毁 · 保护中" :
                    bar.Entity.Kind == BattlefieldKind.Base ? "基地已开放" :
                    bar.State == BattlefieldBuildingState.Active ? "交战中" : "警戒保护";
                bar.Status.color = bar.State == BattlefieldBuildingState.Active ? new Color(1, .8f, .4f) : new Color(.8f, .85f, .9f);
            }
        }

        bool TryPosition(Bar bar, out Vector2 position)
        {
            position = default;
            if (!worldCamera) return false;
            float range = bar.Building ? mode.Settings.BuildingHealthBarDistance : mode.Settings.MinionHealthBarDistance;
            if ((bar.Entity.transform.position - worldCamera.transform.position).sqrMagnitude > range * range) return false;
            var screen = worldCamera.WorldToScreenPoint(bar.Entity.LabelPosition);
            if (screen.z <= .1f || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return false;
            if (mode.Settings.HealthBarsRespectWalls && Physics.Linecast(worldCamera.transform.position,
                bar.Entity.Health.GetPositionToAttack(), 1, QueryTriggerInteraction.Ignore)) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out position);
        }

        public bool TryGetDisplayedHealth(BattlefieldEntity entity, out int health, out float ratio, out bool visible)
        {
            health = 0; ratio = 0; visible = false;
            if (!entity || !bindings.TryGetValue(entity, out var bar)) return false;
            health = bar.Health;
            ratio = bar.Fill.anchorMax.x;
            visible = bar.Root.gameObject.activeSelf;
            return true;
        }
    }
}
