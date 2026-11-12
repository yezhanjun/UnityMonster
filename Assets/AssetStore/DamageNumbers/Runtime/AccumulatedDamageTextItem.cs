using TMPro;
using UnityEngine;

namespace MTPSKIT.UI.DamageNumbers
{
    public sealed class AccumulatedDamageTextItem : MonoBehaviour
    {
        private const float DefaultOutlineWidth = 0.18f;
        private const float FadeInDuration = 0.08f;
        private const float FadeOutDuration = 0.22f;
        private const float HitPulseDuration = 0.18f;
        private const float AmountLerpSpeed = 18f;
        private const float PositionLerpSpeed = 24f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI valueText;
        private TMP_FontAsset configuredFont;
        private float baseFontSize = 34f;

        private bool active;
        private bool fadingOut;
        private float activeTimer;
        private float idleTimer;
        private float fadeTimer;
        private float hitPulseTimer;
        private float windowDuration = 0.75f;
        private float targetAmount;
        private float displayedAmount;
        private int lastDisplayedAmount = int.MinValue;
        private float fontScale = 1f;
        private Vector2 currentPosition;
        private Vector2 targetPosition;
        private Color topColor = Color.white;
        private Color bottomColor = Color.yellow;
        private Color shineColor = Color.white;

        public bool IsFinished => active && fadingOut && fadeTimer >= FadeOutDuration;

        public void Initialize(RectTransform parent)
        {
            rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(260f, 56f);

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            valueText = gameObject.GetComponent<TextMeshProUGUI>();
            if (valueText == null)
            {
                valueText = gameObject.AddComponent<TextMeshProUGUI>();
            }

            valueText.alignment = TextAlignmentOptions.Center;
            valueText.fontSize = baseFontSize;
            valueText.fontStyle = FontStyles.Bold;
            valueText.enableWordWrapping = false;
            valueText.overflowMode = TextOverflowModes.Overflow;
            valueText.raycastTarget = false;
            valueText.richText = false;
            valueText.enableVertexGradient = true;
            Release();
        }

        public void ConfigureStyle(TMP_FontAsset fontAsset, float fontSize)
        {
            configuredFont = fontAsset;
            if (fontSize > 0f)
            {
                baseFontSize = fontSize;
            }

            if (valueText == null)
            {
                return;
            }

            if (configuredFont != null)
            {
                valueText.font = configuredFont;
            }

            valueText.fontSize = baseFontSize;
            ApplyOutline();
        }

        public void AddDamage(
            float damage,
            Vector2 anchorPosition,
            float holdDuration,
            float scale,
            Color gradientTopColor,
            Color gradientBottomColor,
            Color flashColor)
        {
            float clampedDamage = Mathf.Max(0f, damage);
            bool wasInactive = !active;
            targetPosition = anchorPosition;
            windowDuration = Mathf.Max(0.05f, holdDuration);
            fontScale = Mathf.Max(0.1f, scale);
            topColor = gradientTopColor;
            bottomColor = gradientBottomColor;
            shineColor = flashColor;

            if (wasInactive)
            {
                gameObject.SetActive(true);
                active = true;
                fadingOut = false;
                activeTimer = 0f;
                idleTimer = 0f;
                fadeTimer = 0f;
                hitPulseTimer = 0f;
                targetAmount = 0f;
                displayedAmount = 0f;
                lastDisplayedAmount = int.MinValue;
                currentPosition = anchorPosition;
                rectTransform.anchoredPosition = currentPosition;
                rectTransform.localScale = Vector3.one * 0.72f * fontScale;
                canvasGroup.alpha = 0f;
            }

            targetAmount += clampedDamage;
            if (wasInactive)
            {
                displayedAmount = targetAmount;
            }

            idleTimer = 0f;
            fadeTimer = 0f;
            fadingOut = false;
            hitPulseTimer = HitPulseDuration;
            ApplyTextStyle(1f);
            UpdateDisplayedText(true);
        }

        public void SetAnchorPosition(Vector2 anchorPosition, float scale)
        {
            targetPosition = anchorPosition;
            fontScale = Mathf.Max(0.1f, scale);
        }

        public void BeginFadeOut()
        {
            if (!active || fadingOut)
            {
                return;
            }

            fadingOut = true;
            fadeTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!active)
            {
                return;
            }

            activeTimer += deltaTime;
            currentPosition = Vector2.Lerp(currentPosition, targetPosition, Mathf.Clamp01(deltaTime * PositionLerpSpeed));
            rectTransform.anchoredPosition = currentPosition + new Vector2(0f, Mathf.Sin(activeTimer * 16f) * 1.2f);

            if (!fadingOut)
            {
                idleTimer += deltaTime;
                if (idleTimer >= windowDuration)
                {
                    BeginFadeOut();
                }
            }
            else
            {
                fadeTimer += deltaTime;
            }

            float amountBlend = 1f - Mathf.Exp(-AmountLerpSpeed * deltaTime);
            displayedAmount = Mathf.Lerp(displayedAmount, targetAmount, amountBlend);
            if (Mathf.Abs(displayedAmount - targetAmount) < 0.35f)
            {
                displayedAmount = targetAmount;
            }

            UpdateDisplayedText(false);

            if (hitPulseTimer > 0f)
            {
                hitPulseTimer = Mathf.Max(0f, hitPulseTimer - deltaTime);
            }

            float fadeInAlpha = Mathf.Clamp01(activeTimer / FadeInDuration);
            float fadeOutAlpha = fadingOut ? 1f - EaseInQuad(Mathf.Clamp01(fadeTimer / FadeOutDuration)) : 1f;
            canvasGroup.alpha = fadeInAlpha * fadeOutAlpha;

            float pulseProgress = hitPulseTimer / HitPulseDuration;
            float pulse = Mathf.Sin(pulseProgress * Mathf.PI) * pulseProgress;
            float settle = fadingOut ? Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(fadeTimer / FadeOutDuration)) : 1f;
            rectTransform.localScale = Vector3.one * fontScale * settle * (1f + pulse * 0.22f);
            ApplyTextStyle(pulse);
        }

        public void Release()
        {
            active = false;
            fadingOut = false;
            activeTimer = 0f;
            idleTimer = 0f;
            fadeTimer = 0f;
            hitPulseTimer = 0f;
            targetAmount = 0f;
            displayedAmount = 0f;
            lastDisplayedAmount = int.MinValue;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (valueText != null)
            {
                valueText.text = string.Empty;
            }

            gameObject.SetActive(false);
        }

        private void UpdateDisplayedText(bool force)
        {
            if (valueText == null)
            {
                return;
            }

            int roundedAmount = Mathf.Max(1, Mathf.RoundToInt(displayedAmount));
            if (!force && roundedAmount == lastDisplayedAmount)
            {
                return;
            }

            lastDisplayedAmount = roundedAmount;
            valueText.SetText("累计 {0}", roundedAmount);
        }

        private void ApplyTextStyle(float shineAmount)
        {
            if (valueText == null)
            {
                return;
            }

            Color top = Color.Lerp(topColor, shineColor, shineAmount * 0.48f);
            Color bottom = Color.Lerp(bottomColor, shineColor, shineAmount * 0.28f);
            valueText.color = Color.white;
            valueText.colorGradient = new VertexGradient(top, top, bottom, bottom);
            valueText.fontSize = baseFontSize;
            valueText.fontStyle = FontStyles.Bold;
        }

        private void ApplyOutline()
        {
            if (valueText == null || valueText.font == null || valueText.fontSharedMaterial == null)
            {
                return;
            }

            Material runtimeMaterial = valueText.fontMaterial;
            if (runtimeMaterial == null)
            {
                return;
            }

            runtimeMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.92f));
            runtimeMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, DefaultOutlineWidth);
            runtimeMaterial.EnableKeyword("UNDERLAY_ON");
            runtimeMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.82f));
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.75f);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.75f);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.16f);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
            valueText.fontMaterial = runtimeMaterial;
        }

        private static float EaseInQuad(float value)
        {
            return value * value;
        }
    }
}
