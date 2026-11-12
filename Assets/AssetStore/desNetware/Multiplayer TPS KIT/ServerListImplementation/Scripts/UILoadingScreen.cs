using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    public class UILoadingScreen : MonoBehaviour
    {
        [Header("MessagePanel")]
        [SerializeField] GameObject _messagePanel;
        [SerializeField] Text _messagePanelText;
        [SerializeField] Button _messagePanelCloseButton;

        [Header("LoadingPanel")]
        [SerializeField] GameObject _loadingPanel;
        [SerializeField] Text _loadingPanelText;

        [Header("Loading animation")]
        [SerializeField] GameObject _halo;
        [SerializeField] GameObject _mask;
        [SerializeField] float _haloRotationSpeed = 40f; //degrees per second
        [SerializeField] float _maskRotationSpeed = 40f; //degrees per second
        Coroutine _c_loadingScreen;
        Coroutine _c_messageScreen;

        void Awake()
        {
            _messagePanel.SetActive(false);
            _loadingPanel.SetActive(false);

            _messagePanelCloseButton.onClick.AddListener(BtnHideMessagePanel);
        }

        public void ShowMessageScreen(string message, float time)
        {
            _messagePanel.SetActive(true);

            _messagePanelText.text = message;

            if (_c_messageScreen != null)
                StopCoroutine(_c_messageScreen);

            _c_messageScreen = StartCoroutine(LiveTime());

            IEnumerator LiveTime()
            {
                yield return new WaitForSeconds(time);
                _messagePanel.SetActive(false);
            }
        }
        public void BtnHideMessagePanel()
        {
            if (_c_messageScreen != null)
                StopCoroutine(_c_messageScreen);

            _messagePanel.SetActive(false);
        }

        public void ShowLoadingScreen(string message, float time)
        {
            _loadingPanel.SetActive(true);

            _loadingPanelText.text = message;

            if (_c_loadingScreen != null)
                StopCoroutine(_c_loadingScreen);

            _c_loadingScreen = StartCoroutine(LiveTime());

            IEnumerator LiveTime()
            {
                float timer = Time.time + time;
                while (timer >= Time.time)
                {
                    _halo.transform.Rotate(0, 0, _haloRotationSpeed * Time.deltaTime);
                    _mask.transform.Rotate(0, 0, _maskRotationSpeed * Time.deltaTime);

                    yield return null;
                }
                _loadingPanel.SetActive(false);

            }
        }


        public void OnConnectionError()
        {
            ShowMessageScreen("无法连接服务器", 2.2f);
        }

        public void HideLoadingScreen()
        {
            if (_c_loadingScreen != null)
                StopCoroutine(_c_loadingScreen);

            _loadingPanel.SetActive(false);
        }
    }
}