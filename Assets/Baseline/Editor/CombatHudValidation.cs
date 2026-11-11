#if UNITY_EDITOR
using System;
using TMPro;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class CombatHudValidation
    {
        public static void VerifyAndBuild()
        {
            var canvas=FrontEndUI.Canvas(null,100);
            try
            {
                var panel=new GameObject("Inventory validation",typeof(RectTransform)).AddComponent<CombatInventoryPanel>();
                panel.Initialize(canvas.transform);
                var steak = Resources.Load<Sprite>("RainImported/LogIcons/texSteakIcon");
                var glasses = Resources.Load<Sprite>("RainImported/LogIcons/texGlassesIcon");
                var tooth = Resources.Load<Sprite>("RainImported/LogIcons/texToothNecklaceIcon");
                if (!steak || !glasses || !tooth) throw new Exception("Rain inventory icons must be imported as sprites");
                panel.SetItem(0,steak,"生命强化","每层生命上限 +25",3);
                panel.SetOpen(true);Canvas.ForceUpdateCanvases();
                var slot=(RectTransform)panel.transform.Find("Inventory item 0");
                var point=RectTransformUtility.WorldToScreenPoint(null,slot.TransformPoint(slot.rect.center));
                if(!panel.InspectPointer(point))throw new Exception("Hover did not hit the visible inventory icon");
                var tip=panel.transform.Find("Item information");
                if(!tip.gameObject.activeSelf)throw new Exception("Tooltip did not open");
                bool title=false,effect=false;
                foreach(var text in tip.GetComponentsInChildren<TMP_Text>())
                {title|=text.text=="生命强化";effect|=text.text=="每层生命上限 +25";}
                if(!title||!effect||slot.GetComponentInChildren<TMP_Text>().text!="3")throw new Exception("Tooltip or stack count data mismatch");
                if(panel.InspectPointer(new Vector2(-100,-100))||tip.gameObject.activeSelf)throw new Exception("Tooltip stayed open outside icons");
                panel.SetItem(0,null,"","",0);
                if(panel.InspectPointer(point))throw new Exception("Removed item still has a tooltip");
                panel.SetOpen(false);
                if(panel.IsOpen||panel.transform.Find("Player inventory").gameObject.activeSelf)throw new Exception("Inventory did not close");
                Debug.Log("COMBAT_HUD_HOVER_STACK_REMOVAL_PASSED");
            }
            finally{UnityEngine.Object.DestroyImmediate(canvas.gameObject);}
            RainValidationBuild.Build();
        }
    }
}
#endif
