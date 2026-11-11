using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT.UI
{
    public class UIContentBackground : MonoBehaviour
    {
        public RectTransform _rectTransform;
        [SerializeField] float _margin = 5f;

        RectTransform _myRectTransform;

        private void Awake()
        {
            _myRectTransform = GetComponent<RectTransform>();
        }

        public void OnSizeChanged()
        {
            StopAllCoroutines();
            StartCoroutine(SetRect());

            IEnumerator SetRect()
            {
                yield return new WaitForEndOfFrame();
                _myRectTransform.sizeDelta = new Vector2(_rectTransform.sizeDelta.x + _margin * 2f, _rectTransform.sizeDelta.y + _margin * 2f);
                _myRectTransform.position = _rectTransform.position;
            }
        }
    }
}