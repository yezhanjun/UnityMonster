using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DNServerList
{
    public class RegionSelector : MonoBehaviour
    {
        [SerializeField] GameObject _overlay;

        [SerializeField] Button _showChangeRegionPanelButton;

        [SerializeField] Region[] _regions;
        [SerializeField] GameObject _regionButtonPrefab;
        [SerializeField] Transform _gridParent;

        [SerializeField] Text _errorMessage;
        [SerializeField] Text _regionIndicator;

        [SerializeField] WebRequestManager _webRequestManager;

        string _pp_region = "_pp_region";

        int _optionID = -1;
        void Start()
        {

            //if server dont initialize this script
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string[] command = args[i].Split('>');

                if (command[0] == "connect")
                    return;
            }

            _regionIndicator.text = string.Empty;
            _errorMessage.text = string.Empty;

            for (int i = 0; i < _regions.Length; i++)
            {
                Button region = Instantiate(_regionButtonPrefab, _gridParent).GetComponent<Button>();

                region.GetComponentInChildren<Text>().text = _regions[i].Name;

                region.gameObject.name = $"{i} Region";

                int option = i;

                region.onClick.AddListener(() => TryConnectToRegion(option));
            }

            _errorMessage.transform.SetAsLastSibling();

            _regionButtonPrefab.SetActive(false);

            _showChangeRegionPanelButton.onClick.AddListener(ShowPanel);

            //if (connected)
            //    _overlay.SetActive(false);

            _optionID = PlayerPrefs.GetInt(_pp_region);

            _optionID = Mathf.Clamp(_optionID, 0, _regions.Length);

            if (_optionID != -1)
                TryConnectToRegion(_optionID);
        }

        void ShowPanel()
        {
            _overlay.SetActive(true);
            _errorMessage.text = string.Empty;
        }

        public void TryConnectToRegion(int optionID)
        {
            if (!_overlay.activeInHierarchy)
                _overlay.SetActive(true);

            if (_regions.Length <= 0)
            {
                Debug.LogWarning("MultiFPS: There are no regions set");
                return;
            }

            _optionID = Mathf.Clamp(optionID, 0, _regions.Length-1);



            _webRequestManager.SetDomain(_regions[_optionID].Domain);
            _webRequestManager.Get("/ping", OnRegionSelectedSuccessfully, OnRegionSelectedError);

            _errorMessage.text = "正在连接…";
        }

        void OnRegionSelectedSuccessfully(string downloadHandler = "", int responseCode = 0)
        {
            if (responseCode == 200)
            {
                _overlay.SetActive(false);
            }
            else
                OnRegionSelectedError("", 0);

            PlayerPrefs.SetInt(_pp_region, _optionID);
            _regionIndicator.text = $"区域：{_regions[_optionID].Name}";
        }
        void OnRegionSelectedError(string downloadHandler = "", int responseCode = 0)
        {
            _errorMessage.text = "无法连接该区域，请选择其他区域";
        }

        [System.Serializable]
        public class Region
        {
            public string Name;
            public string Domain;
        }
    }
}