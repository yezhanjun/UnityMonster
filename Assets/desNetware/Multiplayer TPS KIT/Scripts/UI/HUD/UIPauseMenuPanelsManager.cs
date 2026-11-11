using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT.UI
{
    public class UIPauseMenuPanelsManager : MonoBehaviour
    {
        [System.Serializable]
        public class Panel
        {
            public GameObject PanelOverlay;
            public Button ShowButton;
            public Button HideButton;

            public void Initialize()
            {
                ShowButton.onClick.AddListener(ShowPanel);
                HideButton.onClick.AddListener(HidePanel);
                HidePanel();
            }
            void ShowPanel()
            {

                PanelOverlay.SetActive(true);
            }
            public void HidePanel()
            {
                PanelOverlay.SetActive(false);
            }
        }

        public Panel[] panels;

        private void Awake()
        {
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].ShowButton.onClick.AddListener(HideAllPanels);
                panels[i].Initialize();
            }
        }
        void HideAllPanels()
        {
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].HidePanel();
            }
        }
    }
}