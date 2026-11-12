using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.DamageNumbers
{
    public enum DamageFloatingTextMode
    {
        Standard = 0,
        NightSoul = 1
    }

    public class DamageFloatingTextItem : MonoBehaviour
    {
        private const float DefaultOutlineWidth = 0.2f;
        private const float PopInDuration = 0.1f;
        private const float HoldDuration = 0.14f;
        private const float CharacterRevealDuration = 0.22f;
        private const float CharacterPopDuration = 0.24f;
        private const float ShineDelay = 0.02f;
        private const float ShineDuration = 0.42f;
        private const float ShineBandWidth = 0.18f;
        private const float NormalShineStrength = 1.05f;
        private const float EmphasizedShineStrength = 1.15f;
        private const float NightSoulShineStrength = 1.28f;
        private const float NormalShineOverlayAlpha = 1f;
        private const float EmphasizedShineOverlayAlpha = 1f;
        private const float NormalShineBandPixelWidth = 72f;
        private const float EmphasizedShineBandPixelWidth = 86f;
        private const float NightSoulShineBandPixelWidth = 110f;
        private const float CriticalIconNormalSizeMultiplier = 1.30f;
        private const float CriticalIconEmphasizedSizeMultiplier = 1.36f;
        private const float CriticalIconShineScale = 0.08f;
        private const float CriticalTextGrowDuration = 0.32f;
        private const float CriticalTextGrowStartScale = 0.42f;
        private const float CriticalTextGrowPeakScale = 1.48f;
        private const float CriticalTextGrowPeakTime = 0.46f;
        private const float CriticalTextLaunchOffset = -10f;
        private const float CriticalTextPeakOffset = 7f;
        private const string CriticalIconResourcePath = "DamageNumbers/CriticalDamageIcon";
        private static readonly Vector2 CriticalIconOutlineDistance = new Vector2(2.2f, -2.2f);

        private static Sprite criticalIconSprite;
        private static bool criticalIconResolved;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI valueText;
        private RectTransform shineMaskTransform;
        private RectTransform shineTextTransform;
        private RectTransform criticalIconTransform;
        private CanvasGroup shineCanvasGroup;
        private TextMeshProUGUI shineText;
        private Image criticalIcon;
        private Outline criticalIconOutline;
        private Material shineRuntimeMaterial;
        private TMP_FontAsset shineRuntimeFontAsset;
        private Texture shineRuntimeAtlasTexture;
        private float baseFontSize = 28f;

        private float lifeTime;
        private float elapsed;
        private Vector2 startPosition;
        private Vector2 endPosition;
        private bool activeAnimation;
        private int totalVisibleCharacters;
        private bool revealCharacters;
        private bool emphasizedText;
        private bool criticalIconVisible;
        private DamageFloatingTextMode presentationMode;
        private float presentationScale = 1f;
        private Color baseTextColor = Color.white;
        private Color animatedTextColor = Color.white;
        private Color gradientTopColor = Color.white;
        private Color gradientBottomColor = Color.white;
        private Color shineTintColor = Color.white;
        private Vector3[][] baseVertices;

        public RectTransform RectTransform => rectTransform;
        public bool IsFinished => activeAnimation && elapsed >= lifeTime;

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
            rectTransform.sizeDelta = new Vector2(240f, 72f);

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
            EnsureShineOverlay();
            EnsureCriticalIcon();
            gameObject.SetActive(false);
        }

        public void ConfigureStyle(TMP_FontAsset fontAsset, float fontSize)
        {
            if (valueText == null)
            {
                return;
            }

            if (fontAsset != null)
            {
                valueText.font = fontAsset;
                if (shineText != null)
                {
                    shineText.font = fontAsset;
                }
            }

            if (fontSize > 0f)
            {
                baseFontSize = fontSize;
                valueText.fontSize = fontSize;
                if (shineText != null)
                {
                    shineText.fontSize = fontSize;
                }
            }

            ApplyDefaultOutline();
            ApplyShineStyle();
        }

        public void Play(string text, Color color, Vector2 screenPosition, float upwardDistance, float duration, float fontScale = 1f, bool showCriticalIcon = false)
        {
            Vector2 randomOffset = new Vector2(Random.Range(-26f, 26f), Random.Range(-6f, 12f));
            Vector2 travelOffset = new Vector2(Random.Range(-12f, 12f), upwardDistance);
            Play(text, color, screenPosition, randomOffset, travelOffset, duration, fontScale, false, true, showCriticalIcon);
        }

        public void Play(
            string text,
            Color color,
            Vector2 anchorPosition,
            Vector2 startOffset,
            Vector2 travelOffset,
            float duration,
            float fontScale = 1f,
            bool emphasized = false,
            bool revealTextCharacters = true,
            bool showCriticalIcon = false,
            DamageFloatingTextMode mode = DamageFloatingTextMode.Standard,
            float visualScale = 1f)
        {
            Color topColor = Color.Lerp(color, Color.white, 0.22f);
            Color bottomColor = emphasized ? DeepenColor(color) : Color.Lerp(color, Color.black, 0.12f);
            Color shineColor = Color.Lerp(topColor, Color.white, 0.72f);
            Play(
                text,
                topColor,
                bottomColor,
                shineColor,
                anchorPosition,
                startOffset,
                travelOffset,
                duration,
                fontScale,
                emphasized,
                revealTextCharacters,
                showCriticalIcon,
                mode,
                visualScale);
        }

        public void Play(
            string text,
            Color topColor,
            Color bottomColor,
            Color shineColor,
            Vector2 anchorPosition,
            Vector2 startOffset,
            Vector2 travelOffset,
            float duration,
            float fontScale = 1f,
            bool emphasized = false,
            bool revealTextCharacters = true,
            bool showCriticalIcon = false,
            DamageFloatingTextMode mode = DamageFloatingTextMode.Standard,
            float visualScale = 1f)
        {
            gameObject.SetActive(true);
            ResetTextForReuse(valueText);
            ResetTextForReuse(shineText);

            string safeText = text ?? string.Empty;
            valueText.text = safeText;
            shineText.text = safeText;
            gradientTopColor = emphasized ? DeepenColor(topColor) : topColor;
            gradientBottomColor = emphasized ? DeepenColor(bottomColor) : bottomColor;
            shineTintColor = shineColor;
            baseTextColor = Color.Lerp(gradientBottomColor, gradientTopColor, 0.5f);
            animatedTextColor = baseTextColor;
            emphasizedText = emphasized;
            criticalIconVisible = showCriticalIcon;
            presentationMode = mode;
            presentationScale = Mathf.Max(0.1f, visualScale);
            valueText.color = animatedTextColor;
            shineText.color = ResolveShineOverlayColor(
                shineTintColor,
                emphasized || presentationMode == DamageFloatingTextMode.NightSoul);
            valueText.fontSize = baseFontSize * Mathf.Max(0.1f, fontScale);
            shineText.fontSize = valueText.fontSize;
            valueText.fontStyle = FontStyles.Bold;
            shineText.fontStyle = FontStyles.Bold;
            lifeTime = Mathf.Max(0.1f, duration);
            elapsed = 0f;
            activeAnimation = true;
            revealCharacters = revealTextCharacters;
            canvasGroup.alpha = 0f;

            startPosition = anchorPosition + startOffset;
            endPosition = startPosition + travelOffset;
            rectTransform.anchoredPosition = startPosition;
            float initialScale = presentationMode == DamageFloatingTextMode.NightSoul ? 0.46f : 0.58f;
            rectTransform.localScale = Vector3.one * (initialScale * presentationScale);

            valueText.ForceMeshUpdate();
            shineText.ForceMeshUpdate();
            totalVisibleCharacters = Mathf.Max(0, valueText.textInfo.characterCount);
            valueText.maxVisibleCharacters = revealCharacters ? 0 : totalVisibleCharacters;
            shineText.maxVisibleCharacters = valueText.maxVisibleCharacters;
            CacheBaseVertices();
            UpdateCriticalIconLayout();
            SetOutlineWidth(presentationMode == DamageFloatingTextMode.NightSoul
                ? DefaultOutlineWidth * 1.2f
                : DefaultOutlineWidth);
            ApplyShineStyle();
            UpdateTextMeshAnimation();
            UpdateShineOverlay();
        }

        public void Tick(float deltaTime)
        {
            if (!activeAnimation)
            {
                return;
            }

            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifeTime);
            float popProgress = Mathf.Clamp01(elapsed / PopInDuration);
            float holdEnd = Mathf.Min(lifeTime, PopInDuration + HoldDuration);
            float floatProgress = elapsed <= holdEnd
                ? 0f
                : Mathf.Clamp01((elapsed - holdEnd) / Mathf.Max(0.01f, lifeTime - holdEnd));

            Vector2 holdPosition = Vector2.Lerp(startPosition, endPosition, 0.08f);
            if (elapsed <= holdEnd)
            {
                float holdMove = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, holdEnd));
                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, holdPosition, EaseOutQuad(holdMove));
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.Lerp(holdPosition, endPosition, EaseOutQuad(floatProgress));
            }

            float scale;
            bool isNightSoul = presentationMode == DamageFloatingTextMode.NightSoul;
            float popStartScale = isNightSoul ? 0.46f : 0.58f;
            float popPeakScale = isNightSoul ? 1.38f : 1.26f;
            float floatEndScale = isNightSoul ? 0.88f : 0.92f;
            if (elapsed <= PopInDuration)
            {
                scale = Mathf.LerpUnclamped(popStartScale, popPeakScale, EaseOutBack(popProgress));
            }
            else if (elapsed <= holdEnd)
            {
                float settleProgress = Mathf.Clamp01((elapsed - PopInDuration) / Mathf.Max(0.01f, HoldDuration));
                scale = Mathf.Lerp(popPeakScale, 1f, EaseOutQuad(settleProgress));
            }
            else
            {
                scale = Mathf.Lerp(1f, floatEndScale, floatProgress);
            }

            rectTransform.localScale = Vector3.one * (scale * presentationScale);
            canvasGroup.alpha = ResolveAlpha(progress, popProgress, floatProgress);
            UpdateVisibleCharacters();
            UpdateTextMeshAnimation();
            UpdateCriticalIconLayout();
            UpdateShineOverlay();
            UpdateCriticalIconColor();
        }

        public void Release()
        {
            activeAnimation = false;
            elapsed = 0f;
            presentationMode = DamageFloatingTextMode.Standard;
            presentationScale = 1f;
            canvasGroup.alpha = 0f;
            if (valueText != null)
            {
                ResetTextForReuse(valueText);
            }

            if (shineText != null)
            {
                ResetTextForReuse(shineText);
            }

            if (shineCanvasGroup != null)
            {
                shineCanvasGroup.alpha = 0f;
            }

            if (criticalIcon != null)
            {
                criticalIcon.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        private void EnsureShineOverlay()
        {
            if (shineText != null)
            {
                return;
            }

            GameObject maskObject = new GameObject("DamageFloatingTextShineMask", typeof(RectTransform), typeof(CanvasGroup), typeof(RectMask2D));
            shineMaskTransform = maskObject.GetComponent<RectTransform>();
            shineMaskTransform.SetParent(rectTransform, false);
            shineMaskTransform.anchorMin = new Vector2(0.5f, 0.5f);
            shineMaskTransform.anchorMax = new Vector2(0.5f, 0.5f);
            shineMaskTransform.pivot = new Vector2(0.5f, 0.5f);
            shineMaskTransform.sizeDelta = new Vector2(NormalShineBandPixelWidth, rectTransform.sizeDelta.y * 1.7f);
            shineCanvasGroup = maskObject.GetComponent<CanvasGroup>();
            shineCanvasGroup.alpha = 0f;

            GameObject textObject = new GameObject("DamageFloatingTextShine", typeof(RectTransform), typeof(TextMeshProUGUI));
            shineTextTransform = textObject.GetComponent<RectTransform>();
            shineTextTransform.SetParent(shineMaskTransform, false);
            shineTextTransform.anchorMin = new Vector2(0.5f, 0.5f);
            shineTextTransform.anchorMax = new Vector2(0.5f, 0.5f);
            shineTextTransform.pivot = new Vector2(0.5f, 0.5f);
            shineTextTransform.sizeDelta = rectTransform.sizeDelta;

            shineText = textObject.GetComponent<TextMeshProUGUI>();
            shineText.alignment = TextAlignmentOptions.Center;
            shineText.fontSize = baseFontSize;
            shineText.fontStyle = FontStyles.Bold;
            shineText.enableWordWrapping = false;
            shineText.overflowMode = TextOverflowModes.Overflow;
            shineText.raycastTarget = false;
            shineText.richText = false;
            shineText.color = Color.white;
        }

        private void EnsureCriticalIcon()
        {
            if (criticalIcon != null)
            {
                EnsureCriticalIconOutline();
                return;
            }

            GameObject iconObject = new GameObject("CriticalDamageIcon", typeof(RectTransform), typeof(Image));
            criticalIconTransform = iconObject.GetComponent<RectTransform>();
            criticalIconTransform.SetParent(rectTransform, false);
            criticalIconTransform.anchorMin = new Vector2(0.5f, 0.5f);
            criticalIconTransform.anchorMax = new Vector2(0.5f, 0.5f);
            criticalIconTransform.pivot = new Vector2(0.5f, 0.5f);
            criticalIconTransform.sizeDelta = new Vector2(24f, 24f);

            criticalIcon = iconObject.GetComponent<Image>();
            criticalIcon.sprite = ResolveCriticalIconSprite();
            criticalIcon.type = Image.Type.Simple;
            criticalIcon.preserveAspect = true;
            criticalIcon.raycastTarget = false;
            criticalIcon.color = Color.white;
            EnsureCriticalIconOutline();
            criticalIcon.gameObject.SetActive(false);
        }

        private void EnsureCriticalIconOutline()
        {
            if (criticalIcon == null)
            {
                return;
            }

            criticalIconOutline = criticalIcon.GetComponent<Outline>();
            if (criticalIconOutline == null)
            {
                criticalIconOutline = criticalIcon.gameObject.AddComponent<Outline>();
            }

            criticalIconOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            criticalIconOutline.effectDistance = CriticalIconOutlineDistance;
            criticalIconOutline.useGraphicAlpha = true;
        }

        private static Sprite ResolveCriticalIconSprite()
        {
            if (criticalIconSprite != null)
            {
                return criticalIconSprite;
            }

            if (criticalIconResolved) return null;
            criticalIconResolved = true;
            Texture2D texture = Resources.Load<Texture2D>(CriticalIconResourcePath);
            if (texture == null)
            {
                return null;
            }

            criticalIconSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            criticalIconSprite.name = "CriticalDamageIcon_RuntimeSprite";
            return criticalIconSprite;
        }

        private void ApplyDefaultOutline()
        {
            SetOutlineWidth(DefaultOutlineWidth);
        }

        private void SetOutlineWidth(float width)
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

            runtimeMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            runtimeMaterial.EnableKeyword("UNDERLAY_ON");
            runtimeMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.88f));
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.62f);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.62f);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.18f);
            runtimeMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.04f);
            valueText.fontMaterial = runtimeMaterial;
        }

        private void ApplyShineStyle()
        {
            if (shineText == null || shineText.font == null)
            {
                return;
            }

            Material sourceMaterial = shineText.fontSharedMaterial;
            if (sourceMaterial == null)
            {
                return;
            }

            Texture sourceAtlasTexture = sourceMaterial.GetTexture(ShaderUtilities.ID_MainTex);
            if (shineRuntimeMaterial == null ||
                shineRuntimeFontAsset != shineText.font ||
                shineRuntimeAtlasTexture != sourceAtlasTexture)
            {
                if (shineRuntimeMaterial != null)
                {
                    Destroy(shineRuntimeMaterial);
                }

                shineRuntimeMaterial = new Material(sourceMaterial)
                {
                    name = "DamageFloatingTextShineMaterial"
                };
                shineRuntimeFontAsset = shineText.font;
                shineRuntimeAtlasTexture = sourceAtlasTexture;
            }

            shineRuntimeMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.clear);
            shineRuntimeMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            shineText.fontMaterial = shineRuntimeMaterial;
        }

        private static void ResetTextForReuse(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.maxVisibleCharacters = int.MaxValue;
            text.text = string.Empty;
            text.ForceMeshUpdate(true, true);
            text.ClearMesh(true);
        }

        private void UpdateVisibleCharacters()
        {
            if (valueText == null || !revealCharacters)
            {
                return;
            }

            if (totalVisibleCharacters <= 0)
            {
                valueText.maxVisibleCharacters = int.MaxValue;
                return;
            }

            float revealProgress = Mathf.Clamp01(elapsed / CharacterRevealDuration);
            valueText.maxVisibleCharacters = Mathf.Clamp(
                Mathf.CeilToInt(totalVisibleCharacters * EaseOutQuad(revealProgress)),
                1,
                totalVisibleCharacters);

            if (shineText != null)
            {
                shineText.maxVisibleCharacters = valueText.maxVisibleCharacters;
            }
        }

        private void CacheBaseVertices()
        {
            if (valueText == null)
            {
                return;
            }

            TMP_TextInfo textInfo = valueText.textInfo;
            int meshCount = textInfo.meshInfo.Length;
            if (baseVertices == null || baseVertices.Length < meshCount)
            {
                baseVertices = new Vector3[meshCount][];
            }

            for (int i = 0; i < meshCount; i++)
            {
                Vector3[] sourceVertices = textInfo.meshInfo[i].vertices;
                if (sourceVertices == null)
                {
                    continue;
                }

                if (baseVertices[i] == null || baseVertices[i].Length < sourceVertices.Length)
                {
                    baseVertices[i] = new Vector3[sourceVertices.Length];
                }

                System.Array.Copy(sourceVertices, baseVertices[i], sourceVertices.Length);
            }
        }

        private void UpdateTextMeshAnimation()
        {
            if (valueText == null || baseVertices == null)
            {
                return;
            }

            TMP_TextInfo textInfo = valueText.textInfo;
            int visibleLimit = revealCharacters ? valueText.maxVisibleCharacters : totalVisibleCharacters;
            float shineProgress = Mathf.Clamp01((elapsed - ShineDelay) / ShineDuration);
            float shineCenter = Mathf.Lerp(-ShineBandWidth, 1f + ShineBandWidth, EaseOutQuad(shineProgress));
            bool shineActive = elapsed >= ShineDelay && shineProgress < 1f;
            int visibleOrder = 0;

            for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
            {
                Vector3[] vertices = textInfo.meshInfo[meshIndex].vertices;
                if (vertices == null || baseVertices.Length <= meshIndex || baseVertices[meshIndex] == null)
                {
                    continue;
                }

                System.Array.Copy(baseVertices[meshIndex], vertices, Mathf.Min(baseVertices[meshIndex].Length, vertices.Length));
            }

            for (int i = 0; i < totalVisibleCharacters; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                int currentVisibleOrder = visibleOrder;
                visibleOrder++;
                if (currentVisibleOrder >= visibleLimit)
                {
                    continue;
                }

                int materialIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                {
                    continue;
                }

                TMP_MeshInfo meshInfo = textInfo.meshInfo[materialIndex];
                if (meshInfo.vertices == null || meshInfo.colors32 == null || vertexIndex + 3 >= meshInfo.vertices.Length)
                {
                    continue;
                }

                float characterScale = ResolveCharacterScale(currentVisibleOrder, Mathf.Max(1, totalVisibleCharacters));
                ApplyCharacterScale(meshInfo.vertices, vertexIndex, characterScale);

                float normalizedIndex = totalVisibleCharacters <= 1
                    ? 0.5f
                    : (float)currentVisibleOrder / (totalVisibleCharacters - 1);
                ApplyCharacterGradient(meshInfo.vertices, meshInfo.colors32, vertexIndex, normalizedIndex, shineCenter, shineActive);
            }

            ApplyCriticalTextPopTransform(
                textInfo,
                visibleLimit,
                ResolveCriticalTextGrowScale(),
                ResolveCriticalTextPopOffset());

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
                if (meshInfo.mesh == null)
                {
                    continue;
                }

                meshInfo.mesh.vertices = meshInfo.vertices;
                meshInfo.mesh.colors32 = meshInfo.colors32;
                valueText.UpdateGeometry(meshInfo.mesh, i);
            }
        }

        private void UpdateShineOverlay()
        {
            if (shineCanvasGroup == null || shineMaskTransform == null || shineTextTransform == null || shineText == null)
            {
                return;
            }

            float shineProgress = (elapsed - ShineDelay) / ShineDuration;
            if (shineProgress <= 0f || shineProgress >= 1f)
            {
                shineCanvasGroup.alpha = 0f;
                return;
            }

            float easedProgress = EaseOutQuad(Mathf.Clamp01(shineProgress));
            float bandWidth = presentationMode == DamageFloatingTextMode.NightSoul
                ? NightSoulShineBandPixelWidth
                : emphasizedText ? EmphasizedShineBandPixelWidth : NormalShineBandPixelWidth;
            float halfTravelWidth = rectTransform.sizeDelta.x * 0.62f + bandWidth;
            float x = Mathf.Lerp(-halfTravelWidth, halfTravelWidth, easedProgress);

            shineMaskTransform.sizeDelta = new Vector2(bandWidth, rectTransform.sizeDelta.y * 1.75f);
            shineMaskTransform.anchoredPosition = new Vector2(x, ResolveCriticalTextPopOffset());
            shineTextTransform.sizeDelta = rectTransform.sizeDelta;
            shineTextTransform.anchoredPosition = new Vector2(-x, 0f);
            shineMaskTransform.localScale = Vector3.one * ResolveCriticalTextGrowScale();
            shineText.maxVisibleCharacters = valueText != null ? valueText.maxVisibleCharacters : int.MaxValue;

            float pulse = Mathf.Sin(Mathf.Clamp01(shineProgress) * Mathf.PI);
            float alpha = emphasizedText ? EmphasizedShineOverlayAlpha : NormalShineOverlayAlpha;
            shineCanvasGroup.alpha = Mathf.Clamp01(canvasGroup.alpha * pulse * alpha);
        }

        private void UpdateCriticalIconLayout()
        {
            if (criticalIcon == null || criticalIconTransform == null)
            {
                return;
            }

            criticalIcon.gameObject.SetActive(criticalIconVisible);
            if (!criticalIconVisible || valueText == null || rectTransform == null)
            {
                return;
            }

            float fontSize = Mathf.Max(1f, valueText.fontSize);
            bool useLargeIcon = emphasizedText || presentationMode == DamageFloatingTextMode.NightSoul;
            float iconSize = fontSize * (useLargeIcon
                ? CriticalIconEmphasizedSizeMultiplier
                : CriticalIconNormalSizeMultiplier);
            Vector2 preferredSize = valueText.GetPreferredValues(valueText.text);
            float textWidth = Mathf.Max(1f, preferredSize.x);
            float currentCriticalScale = ResolveCriticalTextGrowScale();
            float criticalGrowPadding = criticalIconVisible ? textWidth * Mathf.Max(0f, currentCriticalScale - 1f) * 0.5f : 0f;
            float gap = iconSize * 0.03f;

            criticalIconTransform.sizeDelta = new Vector2(iconSize, iconSize);
            criticalIconTransform.anchoredPosition = new Vector2(
                -textWidth * 0.5f - criticalGrowPadding - gap - iconSize * 0.5f,
                -fontSize * 0.02f);
            criticalIconTransform.localScale = Vector3.one;
            UpdateCriticalIconColor();
        }

        private void UpdateCriticalIconColor()
        {
            if (criticalIcon == null || !criticalIconVisible)
            {
                return;
            }

            Color iconColor = Color.Lerp(gradientBottomColor, gradientTopColor, 0.58f);
            float shineProgress = Mathf.Clamp01((elapsed - ShineDelay) / ShineDuration);
            bool shineActive = elapsed >= ShineDelay && shineProgress < 1f;
            if (shineActive)
            {
                float pulse = Mathf.Sin(shineProgress * Mathf.PI);
                Color flashColor = Color.Lerp(shineTintColor, Color.white, 0.82f);
                float strength = pulse * (presentationMode == DamageFloatingTextMode.NightSoul
                    ? NightSoulShineStrength
                    : emphasizedText ? EmphasizedShineStrength : NormalShineStrength);
                iconColor = Color.LerpUnclamped(iconColor, flashColor, strength);

                if (criticalIconTransform != null)
                {
                    criticalIconTransform.localScale = Vector3.one * (1f + pulse * CriticalIconShineScale);
                }
            }
            else if (criticalIconTransform != null)
            {
                criticalIconTransform.localScale = Vector3.one;
            }

            iconColor.a = baseTextColor.a;
            criticalIcon.color = iconColor;

            if (criticalIconOutline != null)
            {
                criticalIconOutline.effectColor = new Color(0f, 0f, 0f, Mathf.Lerp(0.82f, 0.98f, shineActive ? shineProgress : 0f));
            }
        }

        private float ResolveCriticalTextGrowScale()
        {
            if (!criticalIconVisible)
            {
                return 1f;
            }

            bool isNightSoul = presentationMode == DamageFloatingTextMode.NightSoul;
            float startScale = isNightSoul ? 0.36f : CriticalTextGrowStartScale;
            float peakScale = isNightSoul ? 1.62f : CriticalTextGrowPeakScale;
            float progress = Mathf.Clamp01(elapsed / CriticalTextGrowDuration);
            if (progress < CriticalTextGrowPeakTime)
            {
                float growProgress = progress / CriticalTextGrowPeakTime;
                return Mathf.LerpUnclamped(startScale, peakScale, EaseOutBack(growProgress));
            }

            float settleProgress = (progress - CriticalTextGrowPeakTime) / (1f - CriticalTextGrowPeakTime);
            return Mathf.Lerp(peakScale, 1f, EaseOutQuad(settleProgress));
        }

        private float ResolveCriticalTextPopOffset()
        {
            if (!criticalIconVisible)
            {
                return 0f;
            }

            bool isNightSoul = presentationMode == DamageFloatingTextMode.NightSoul;
            float launchOffset = isNightSoul ? -14f : CriticalTextLaunchOffset;
            float peakOffset = isNightSoul ? 11f : CriticalTextPeakOffset;
            float progress = Mathf.Clamp01(elapsed / CriticalTextGrowDuration);
            if (progress < CriticalTextGrowPeakTime)
            {
                float growProgress = progress / CriticalTextGrowPeakTime;
                return Mathf.Lerp(launchOffset, peakOffset, EaseOutBack(growProgress));
            }

            float settleProgress = (progress - CriticalTextGrowPeakTime) / (1f - CriticalTextGrowPeakTime);
            return Mathf.Lerp(peakOffset, 0f, EaseOutQuad(settleProgress));
        }

        private float ResolveCharacterScale(int characterOrder, int characterCount)
        {
            if (!revealCharacters)
            {
                return 1f;
            }

            bool isNightSoul = presentationMode == DamageFloatingTextMode.NightSoul;
            float startScale = isNightSoul ? 0.38f : 0.48f;
            float peakScale = isNightSoul ? 1.58f : 1.42f;
            float stagger = CharacterRevealDuration / characterCount;
            float localElapsed = elapsed - characterOrder * stagger * 0.82f;
            if (localElapsed <= 0f)
            {
                return startScale;
            }

            float progress = Mathf.Clamp01(localElapsed / CharacterPopDuration);
            if (progress < 0.45f)
            {
                return Mathf.LerpUnclamped(startScale, peakScale, EaseOutBack(progress / 0.45f));
            }

            return Mathf.Lerp(peakScale, 1f, EaseOutQuad((progress - 0.45f) / 0.55f));
        }

        private void ApplyCriticalTextPopTransform(TMP_TextInfo textInfo, int visibleLimit, float scale, float yOffset)
        {
            if (Mathf.Approximately(scale, 1f) && Mathf.Approximately(yOffset, 0f))
            {
                return;
            }

            bool hasBounds = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            int visibleOrder = 0;
            for (int i = 0; i < totalVisibleCharacters; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                int currentVisibleOrder = visibleOrder;
                visibleOrder++;
                if (currentVisibleOrder >= visibleLimit)
                {
                    continue;
                }

                int materialIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                {
                    continue;
                }

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                if (vertices == null || vertexIndex + 3 >= vertices.Length)
                {
                    continue;
                }

                for (int vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    Vector3 vertex = vertices[vertexIndex + vertexOffset];
                    if (!hasBounds)
                    {
                        min = vertex;
                        max = vertex;
                        hasBounds = true;
                        continue;
                    }

                    min = Vector3.Min(min, vertex);
                    max = Vector3.Max(max, vertex);
                }
            }

            if (!hasBounds)
            {
                return;
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 offset = new Vector3(0f, yOffset, 0f);
            visibleOrder = 0;
            for (int i = 0; i < totalVisibleCharacters; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                int currentVisibleOrder = visibleOrder;
                visibleOrder++;
                if (currentVisibleOrder >= visibleLimit)
                {
                    continue;
                }

                int materialIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                {
                    continue;
                }

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                if (vertices == null || vertexIndex + 3 >= vertices.Length)
                {
                    continue;
                }

                for (int vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    vertices[vertexIndex + vertexOffset] = center +
                        (vertices[vertexIndex + vertexOffset] - center) * scale +
                        offset;
                }
            }
        }

        private static void ApplyCharacterScale(Vector3[] vertices, int vertexIndex, float scale)
        {
            if (Mathf.Approximately(scale, 1f))
            {
                return;
            }

            Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
            vertices[vertexIndex] = center + (vertices[vertexIndex] - center) * scale;
            vertices[vertexIndex + 1] = center + (vertices[vertexIndex + 1] - center) * scale;
            vertices[vertexIndex + 2] = center + (vertices[vertexIndex + 2] - center) * scale;
            vertices[vertexIndex + 3] = center + (vertices[vertexIndex + 3] - center) * scale;
        }

        private void ApplyCharacterGradient(
            Vector3[] vertices,
            Color32[] colors,
            int vertexIndex,
            float normalizedIndex,
            float shineCenter,
            bool shineActive)
        {
            float bottomY = vertices[vertexIndex].y;
            float topY = vertices[vertexIndex].y;
            for (int i = 1; i < 4; i++)
            {
                float y = vertices[vertexIndex + i].y;
                if (y < bottomY)
                {
                    bottomY = y;
                }
                else if (y > topY)
                {
                    topY = y;
                }
            }

            float height = Mathf.Max(0.01f, topY - bottomY);
            for (int i = 0; i < 4; i++)
            {
                float vertical = Mathf.Clamp01((vertices[vertexIndex + i].y - bottomY) / height);
                colors[vertexIndex + i] = ResolveVertexColor(normalizedIndex, vertical, shineCenter, shineActive);
            }
        }

        private Color32 ResolveVertexColor(float normalizedIndex, float vertical, float shineCenter, bool shineActive)
        {
            float gradient = Mathf.SmoothStep(0f, 1f, vertical);
            Color color = Color.Lerp(gradientBottomColor, gradientTopColor, gradient);
            if (shineActive)
            {
                float diagonalIndex = normalizedIndex + (vertical - 0.5f) * 0.18f;
                float shineDistance = Mathf.Abs(diagonalIndex - shineCenter);
                float shinePower = Mathf.Clamp01(1f - shineDistance / ShineBandWidth);
                shinePower = shinePower * shinePower * (3f - 2f * shinePower);
                float strength = presentationMode == DamageFloatingTextMode.NightSoul
                    ? NightSoulShineStrength
                    : emphasizedText ? EmphasizedShineStrength : NormalShineStrength;
                color = Color.LerpUnclamped(color, shineTintColor, shinePower * strength);
            }

            color.a = baseTextColor.a;
            return color;
        }

        private static Color ResolveShineOverlayColor(Color color, bool emphasized)
        {
            Color shineColor = Color.Lerp(color, Color.white, emphasized ? 0.82f : 0.68f);
            shineColor.a = 1f;
            return shineColor;
        }

        private static Color DeepenColor(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            Color deepColor = Color.HSVToRGB(
                hue,
                Mathf.Clamp01(saturation * 1.22f + 0.08f),
                Mathf.Clamp01(value * 0.82f));
            deepColor.a = color.a;
            return deepColor;
        }

        private static float ResolveAlpha(float progress, float popProgress, float floatProgress)
        {
            if (popProgress < 1f)
            {
                return EaseOutQuad(popProgress);
            }

            if (progress < 0.58f)
            {
                return 1f;
            }

            float fadeProgress = Mathf.Clamp01((progress - 0.58f) / 0.42f);
            return 1f - EaseInQuad(fadeProgress);
        }

        private static float EaseOutQuad(float value)
        {
            return 1f - (1f - value) * (1f - value);
        }

        private static float EaseInQuad(float value)
        {
            return value * value;
        }

        private void OnDestroy()
        {
            if (shineRuntimeMaterial != null) Destroy(shineRuntimeMaterial);
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float t = value - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }
    }

}
