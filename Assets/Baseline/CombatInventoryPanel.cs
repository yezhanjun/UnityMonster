using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    // Reusable inventory strip and hold-Tab inspection. Data comes from the active mode.
    public sealed class CombatInventoryPanel : MonoBehaviour
    {
        sealed class Slot { public Image image; public TMP_Text count; public string title, description; public bool inspectionOnly; }
        readonly List<Slot> slots = new List<Slot>();
        GameObject row, shade, tooltip;
        RectTransform tip;
        TMP_Text heading, detail;
        bool inspecting;
        public bool IsOpen => inspecting;
        public void Initialize(Transform root)
        {
            transform.SetParent(root, false);
            var rect=(RectTransform)transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            shade=FrontEndUI.Box(transform,"Inventory dim",0,0,1920,1080,new Color(0,0,0,.24f)).gameObject;
            shade.GetComponent<Image>().raycastTarget=false;
            row=FrontEndUI.Box(transform,"Player inventory",430,300,1040,76,new Color(.22f,.27f,.32f,.94f),"texDetailPanel").gameObject;
            FrontEndUI.Text(row.transform,"玩家",0,-40,260,35,23);
            FrontEndUI.Text(row.transform,"关键物品",280,-40,140,35,23);
            FrontEndUI.Text(row.transform,"物品 / 武器与装备",480,-40,500,35,23);
            FrontEndUI.Text(row.transform,UserSettings.UserNickname,65,0,210,76,25);
            var portrait=FrontEndUI.Box(row.transform,"Portrait",6,8,55,55,Color.white);
            portrait.sprite=Resources.Load<Sprite>("RainImported/LogIcons/texCommandoIcon");portrait.preserveAspect=true;
            FrontEndUI.Text(row.transform,"—",300,0,80,76,24);
            for(int i=0;i<54;i++)
            {
                var im=FrontEndUI.Box(transform,"Inventory item "+i,400+i*61,75,56,56,new Color(.7f,.7f,.7f,.8f));
                im.preserveAspect=true;
                slots.Add(new Slot{image=im,count=FrontEndUI.Text(im.transform,"",20,33,36,23,17,TextAlignmentOptions.MidlineRight)});
                im.gameObject.SetActive(false);
            }
            tooltip=FrontEndUI.Box(transform,"Item information",0,0,440,170,new Color(.30f,.28f,.29f,.98f)).gameObject;
            tip=(RectTransform)tooltip.transform;
            FrontEndUI.Box(tip,"Header",0,0,440,49,new Color(.62f,.63f,.65f));
            heading=FrontEndUI.Text(tip,"",14,0,412,49,24);
            detail=FrontEndUI.Text(tip,"",14,55,412,110,20);
            SetOpen(false);
        }
        public void SetItem(int index, Sprite icon, string title, string description, int count, bool inspectionOnly = false)
        {
            if(index>=slots.Count)return;
            var s=slots[index];s.title=title;s.description=description;
            s.inspectionOnly=inspectionOnly;
            bool changed=s.image.gameObject.activeSelf!=(count>0);
            s.image.gameObject.SetActive(count>0);s.image.sprite=icon;
            s.image.color=icon?Color.white:new Color(.35f,.4f,.45f);
            s.count.text=count>1?count.ToString():"";
            if(changed)LayoutItems();
        }
        public void SetOpen(bool value)
        {
            inspecting=value;shade.SetActive(value);row.SetActive(value);tooltip.SetActive(false);
            LayoutItems();
        }
        void LayoutItems()
        {
            int index=0;
            int visible = 0;
            foreach (var slot in slots)
                if (slot.image.gameObject.activeSelf && (inspecting || !slot.inspectionOnly)) visible++;
            float spacing = inspecting ? Mathf.Min(39f, 635f / Mathf.Max(1, visible)) : 61f;
            foreach(var slot in slots)
            {
                var r=slot.image.rectTransform;
                float size=inspecting?spacing-3:56;
                r.sizeDelta=new Vector2(size,size);
                r.anchoredPosition=new Vector2((inspecting?835:400)+index*spacing,inspecting?-315:-75);
                slot.count.rectTransform.anchoredPosition=new Vector2(size-36,-size+23);
                slot.image.enabled=inspecting||!slot.inspectionOnly;
                slot.count.enabled=slot.image.enabled;
                if(slot.image.gameObject.activeSelf&&slot.image.enabled)index++;
            }
        }
        void Update()
        {
            InspectPointer(Input.mousePosition);
        }
        public bool InspectPointer(Vector2 pointer)
        {
            if(!inspecting)return false;
            Slot hovered=null;
            foreach(var s in slots)if(s.image.gameObject.activeSelf&&RectTransformUtility.RectangleContainsScreenPoint(s.image.rectTransform,pointer,null)){hovered=s;break;}
            tooltip.SetActive(hovered!=null);
            if(hovered==null)return false;
            heading.text=hovered.title;detail.text=hovered.description;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,pointer,null,out var local);
            var canvasRect=(RectTransform)transform.parent;
            float x=local.x+canvasRect.rect.width*.5f+20;
            float y=canvasRect.rect.height*.5f-local.y+20;
            tip.anchoredPosition=new Vector2(Mathf.Clamp(x,0,canvasRect.rect.width-440),-Mathf.Clamp(y,0,canvasRect.rect.height-170));
            return true;
        }
    }
}
