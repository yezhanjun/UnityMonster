using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace MTPSKIT.UI.DamageNumbers
{
    /// <summary>Client presentation only; authoritative damage still belongs to Health.</summary>
    public sealed class DamageNumbersPresenter : MonoBehaviour
    {
        static readonly Vector2[] Spread =
        {
            new Vector2(0, 0), new Vector2(-34, -8), new Vector2(30, -4), new Vector2(-14, -22),
            new Vector2(44, -14), new Vector2(-50, -20), new Vector2(16, -32), new Vector2(-28, -30)
        };

        sealed class TotalEntry
        {
            public Health Target;
            public AccumulatedDamageTextItem Item;
        }

        readonly Stack<DamageFloatingTextItem> singlePool = new Stack<DamageFloatingTextItem>(48);
        readonly List<DamageFloatingTextItem> singles = new List<DamageFloatingTextItem>(48);
        readonly Stack<AccumulatedDamageTextItem> totalPool = new Stack<AccumulatedDamageTextItem>(8);
        readonly List<TotalEntry> totals = new List<TotalEntry>(8);
        readonly Stack<TotalEntry> entryPool = new Stack<TotalEntry>(8);
        readonly string[] amounts = new string[1025];
        RectTransform layer;
        Canvas canvas;
        Camera worldCamera;
        DamageNumbersSettings settings;
        int serial;
        public int Presented { get; private set; }
        public int SingleCount => singles.Count;
        public int TotalCount => totals.Count;
        public int SinglesCreated { get; private set; }
        public int TotalsCreated { get; private set; }
        public bool Ready => layer && settings && settings.Font;

        public void Initialize()
        {
            settings = Resources.Load<DamageNumbersSettings>("DamageNumbersSettings");
            canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (!settings || !settings.Font || !canvas)
            {
                Debug.LogError("DamageNumbers: settings/font/root Canvas missing.");
                enabled = false;
                return;
            }

            var root = new GameObject("DamageNumbersLayer", typeof(RectTransform), typeof(CanvasGroup));
            layer = (RectTransform)root.transform;
            layer.SetParent(canvas.transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            for (int i = 0; i < amounts.Length; i++) amounts[i] = i.ToString();
            for (int i = 0; i < Mathf.Clamp(settings.PrewarmSingles, 0, settings.MaxSingles); i++) singlePool.Push(CreateSingle());
            for (int i = 0; i < Mathf.Max(1, settings.MaxTotals); i++)
            {
                totalPool.Push(CreateTotal());
                entryPool.Push(new TotalEntry());
            }
        }

        DamageFloatingTextItem CreateSingle()
        {
            var item = new GameObject("DamageNumber", typeof(RectTransform)).AddComponent<DamageFloatingTextItem>();
            item.Initialize(layer);
            item.ConfigureStyle(settings.Font, settings.FontSize);
            SinglesCreated++;
            return item;
        }

        AccumulatedDamageTextItem CreateTotal()
        {
            var item = new GameObject("AccumulatedDamage", typeof(RectTransform)).AddComponent<AccumulatedDamageTextItem>();
            item.Initialize(layer);
            item.ConfigureStyle(settings.Font, settings.TotalFontSize);
            TotalsCreated++;
            return item;
        }

        public void Show(int amount, CharacterPart part, Health victim)
        {
            if (!Ready || amount <= 0 || !victim || !TryAnchor(victim, out var anchor, out float scale)) return;
            int max = Mathf.Max(1, settings.MaxSingles);
            if (singles.Count >= max) RecycleSingle(0);
            var item = singlePool.Count > 0 ? singlePool.Pop() : CreateSingle();
            bool headshot = part == CharacterPart.head;
            Color color = headshot ? settings.HeadshotColor : settings.NormalColor;
            var start = Spread[serial++ % Spread.Length] + new Vector2(
                Random.Range(-settings.StartJitter.x, settings.StartJitter.x),
                Random.Range(-settings.StartJitter.y, settings.StartJitter.y));
            var travel = new Vector2(start.x * .24f + Random.Range(-settings.DriftJitter.x, settings.DriftJitter.x),
                settings.MoveDistance + Random.Range(-settings.DriftJitter.y, settings.DriftJitter.y));
            item.Play(amount < amounts.Length ? amounts[amount] : amount.ToString(), color,
                anchor, start * scale, travel * scale, settings.LifeTime,
                scale * (headshot ? settings.HeadshotScale : 1), false, true);
            singles.Add(item);
            Presented++;
            if (settings.ShowAccumulated) AddTotal(victim, amount, anchor, scale);
        }

        void AddTotal(Health victim, int amount, Vector2 anchor, float scale)
        {
            TotalEntry entry = null;
            for (int i = 0; i < totals.Count; i++) if (totals[i].Target == victim) { entry = totals[i]; break; }
            if (entry == null)
            {
                if (totals.Count >= Mathf.Max(1, settings.MaxTotals)) RecycleTotal(0);
                entry = entryPool.Pop();
                entry.Target = victim;
                entry.Item = totalPool.Pop();
                totals.Add(entry);
            }
            entry.Item.AddDamage(amount, anchor + settings.TotalOffset * scale, settings.TotalHoldTime,
                settings.TotalScale * scale, settings.TotalTop, settings.TotalBottom, settings.TotalShine);
        }

        bool TryAnchor(Health victim, out Vector2 position, out float scale)
        {
            position = default;
            scale = 1;
            if (!worldCamera || !worldCamera.isActiveAndEnabled)
                worldCamera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            var camera = worldCamera;
            if (!camera) return false;
            Vector3 world = victim.GetPositionToAttack();
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return false;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, uiCamera, out position)) return false;
            float distance = Vector3.Distance(camera.transform.position, world);
            float t = Mathf.InverseLerp(settings.NearDistance, Mathf.Max(settings.NearDistance + .01f, settings.FarDistance), distance);
            t = t * t * (3 - 2 * t);
            scale = Mathf.Lerp(settings.NearScale, settings.FarScale, t);
            position += settings.Offset * scale;
            return true;
        }

        void LateUpdate()
        {
            // UI continues settling during a pause; it never advances gameplay.
            float delta = Time.unscaledDeltaTime;
            for (int i = singles.Count - 1; i >= 0; i--)
            {
                singles[i].Tick(delta);
                if (singles[i].IsFinished) RecycleSingle(i);
            }
            for (int i = totals.Count - 1; i >= 0; i--)
            {
                var entry = totals[i];
                if (!entry.Target || entry.Target.CurrentHealth <= 0) entry.Item.BeginFadeOut();
                else if (TryAnchor(entry.Target, out var anchor, out float scale))
                    entry.Item.SetAnchorPosition(anchor + settings.TotalOffset * scale, settings.TotalScale * scale);
                else entry.Item.BeginFadeOut();
                entry.Item.Tick(delta);
                if (entry.Item.IsFinished) RecycleTotal(i);
            }
        }

        void RecycleSingle(int index)
        {
            var item = singles[index];
            singles.RemoveAt(index);
            item.Release();
            singlePool.Push(item);
        }

        void RecycleTotal(int index)
        {
            var entry = totals[index];
            totals.RemoveAt(index);
            entry.Item.Release();
            totalPool.Push(entry.Item);
            entry.Target = null;
            entry.Item = null;
            entryPool.Push(entry);
        }

        public void Clear()
        {
            for (int i = singles.Count - 1; i >= 0; i--) RecycleSingle(i);
            for (int i = totals.Count - 1; i >= 0; i--) RecycleTotal(i);
        }

        void OnDisable() { Clear(); }
        void OnDestroy() { if (layer) Destroy(layer.gameObject); }
    }
}
