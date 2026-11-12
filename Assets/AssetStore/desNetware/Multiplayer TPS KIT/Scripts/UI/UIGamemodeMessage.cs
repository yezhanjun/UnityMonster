using MTPSKIT.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI
{
    public class UIGamemodeMessage : MonoBehaviour
    {
        [SerializeField] private Text _msgText;
        [SerializeField] private Image _msgBackground;
        [SerializeField] UIContentBackground _uiContentBackground;

        Coroutine _messageLiveTimeCounter;

        private void OnEnable()
        {
            _msgBackground.enabled = false;
            _msgText.enabled = false;
            ClientFrontend.GamemodeEvent_Message += GamemodeMessage;
        }
        private void OnDisable()
        {
            ClientFrontend.GamemodeEvent_Message -= GamemodeMessage;
        }

        void GamemodeMessage(string _msg, float _liveTime)
        {
            if (_messageLiveTimeCounter != null)
            {
                StopCoroutine(_messageLiveTimeCounter);
                _messageLiveTimeCounter = null;
            }
            _messageLiveTimeCounter = StartCoroutine(messageLiveTimeCounter());

            IEnumerator messageLiveTimeCounter()
            {
                _msgBackground.enabled = true;
                _msgText.enabled = true;
                _msgText.text = _msg;
                _uiContentBackground.OnSizeChanged();
                yield return new WaitForSeconds(_liveTime);
                _msgBackground.enabled = false;
                _msgText.enabled = false;
                _msgText.text = "";
            }
        }

    }
}