using TMPro;
using UnityEngine;

namespace MTPSKIT.UI.DamageNumbers
{
    [CreateAssetMenu(menuName = "TPSBattle/UI/Damage Numbers")]
    public sealed class DamageNumbersSettings : ScriptableObject
    {
        public TMP_FontAsset Font;
        public int PrewarmSingles = 24, MaxSingles = 48, MaxTotals = 8;
        public float FontSize = 60, LifeTime = .75f, MoveDistance = 64;
        public Vector2 Offset = new Vector2(0, -64);
        public Vector2 StartJitter = new Vector2(8, 6), DriftJitter = new Vector2(10, 12);
        public float HeadshotScale = 1.5f;
        public Color NormalColor = new Color(.95f, .48f, 0);
        public Color HeadshotColor = new Color(1, .18f, .18f);
        public float NearDistance = 3, FarDistance = 18, NearScale = .72f, FarScale = .34f;
        public bool ShowAccumulated = true;
        public float TotalFontSize = 37, TotalHoldTime = .75f, TotalScale = 1.06f;
        public Vector2 TotalOffset = new Vector2(112, 54);
        public Color TotalTop = new Color(1, .98f, .22f), TotalBottom = new Color(1, .42f, .02f), TotalShine = new Color(1, 1, .78f);
    }
}
