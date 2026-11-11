using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DNServerList.Example
{
    public class UIPanelsManager : MonoBehaviour
    {

        public UIPanel[] panels;
       

        [System.Serializable]
        public class UIPanel
        {
            public GameObject PanelOverlay;
            public Button ShowButton;
            public Button HideButton;

            public void Initialize()
            {
                if(ShowButton) ShowButton.onClick.AddListener(ShowPanel);
                if(HideButton) HideButton.onClick.AddListener(HidePanel);
                HidePanel();
            }
            public void ShowPanel()
            {
                PanelOverlay.SetActive(true);
            }
            public void HidePanel()
            {
                PanelOverlay.SetActive(false);
            }
        }

        public void ShowPanel(int panelID) 
        {
            HideAllPanels();
            panels[panelID].ShowPanel();
        }


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