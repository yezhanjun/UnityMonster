using UnityEngine;

namespace MTPSKIT.UI
{
    public class UIElementsLayoutGrid : MonoBehaviour
    {
        UILayoutElement[] elements;

        [SerializeField] Align _elementsAlignment = Align.Center;
        
        enum Align
        {
            Right,
            Left,
            Center,
        }

        private void Awake()
        {
            elements = GetComponentsInChildren<UILayoutElement>(false);
        }

        sbyte _multiplier = 1;

        public float SetupElements()
        {

            float width = 0;
            float currentWidth = 0;


            for (int i = 0; i < elements.Length; i++)
            {
                if (!elements[i].gameObject.activeSelf) continue;

                elements[i].AdjustElement();
                width += elements[i].RectTransform.rect.width * elements[i].RectTransform.localScale.x;
            }

            if (_elementsAlignment == Align.Center)
            {
                currentWidth = -width / 2;

                _multiplier = 1;
            }
            else if (_elementsAlignment == Align.Right) 
            {
                currentWidth = -width;
            }

            for (int i = 0; i < elements.Length; i++)
            {
                if (!elements[i].gameObject.activeSelf) continue;
                currentWidth += elements[i].RectTransform.rect.width * elements[i].RectTransform.pivot.x*_multiplier* elements[i].RectTransform.localScale.x;

                elements[i].RectTransform.localPosition = new Vector2(currentWidth, 0);

                currentWidth += elements[i].RectTransform.rect.width * (1f - elements[i].RectTransform.pivot.x)*_multiplier* elements[i].RectTransform.localScale.x;
            }

            return width;
        }
    }
}