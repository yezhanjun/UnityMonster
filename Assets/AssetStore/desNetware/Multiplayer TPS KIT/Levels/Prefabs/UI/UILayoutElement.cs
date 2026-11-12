using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI
{
    [RequireComponent(typeof(ContentSizeFitter))]
    public class UILayoutElement : MonoBehaviour
    {
        ContentSizeFitter _contentSizeFitter;
        [HideInInspector] public RectTransform RectTransform;

        private void Awake()
        {
            _contentSizeFitter = GetComponent<ContentSizeFitter>();
            RectTransform = GetComponent<RectTransform>();
        }

        public void AdjustElement()
        {
            if (_contentSizeFitter)
            {
                _contentSizeFitter.SetLayoutHorizontal();
                _contentSizeFitter.SetLayoutVertical();
            }
        }
    }
}