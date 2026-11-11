using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MTPSKIT.Gameplay;

namespace MTPSKIT
{
    public sealed class HeroLoadoutHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action enter, exit;
        public void OnPointerEnter(PointerEventData data) => enter?.Invoke();
        public void OnPointerExit(PointerEventData data) => exit?.Invoke();
        void OnDisable() => exit?.Invoke();
    }

    public static class HeroLoadoutUI
    {
        static readonly Color Purple = new Color(.38f, .20f, .42f);
        static string TraitDescription(string id,string fallback)
        {
            foreach(var node in Jotunnslayer.SkillCatalog.Load().skills)
                if(node.id==id)return "神祇临时特性 · 基础1阶\n\n"+node.desc;
            return fallback;
        }
        public static void Draw(Transform root, int hero, Action changed)
        {
            var definition = HeroLoadoutCatalog.Hero(hero);
            if (definition == null)
            {
                FrontEndUI.Text(root, "选择温蒂、诗乃、萨菲或米尔缇娜配置角色技能。", 0, 0, 540, 110, 24);
                return;
            }
            Transform tooltip = null, picker = null;
            void ClearTooltip() { if (tooltip) { UnityEngine.Object.Destroy(tooltip.gameObject); tooltip = null; } }
            void Tip(string title, string text)
            {
                root.SetAsLastSibling();
                ClearTooltip();
                tooltip = FrontEndUI.Box(root, "Skill tooltip", 610, 0, 490, 265, new Color(.07f,.055f,.09f,.98f)).transform;
                tooltip.GetComponent<Image>().raycastTarget = false;
                FrontEndUI.Box(tooltip,"Title background",0,0,490,48,Purple).raycastTarget=false;
                FrontEndUI.Text(tooltip,title,16,0,458,48,24);
                FrontEndUI.Text(tooltip,text,16,60,458,190,21,TextAlignmentOptions.TopLeft);
            }
            void Hover(Button button, string title, string text)
            {
                var h=button.gameObject.AddComponent<HeroLoadoutHover>();
                h.enter=()=>Tip(title,text); h.exit=ClearTooltip;
            }
            void Image(Button b, string resource)
            {
                var icon = Resources.Load<Texture2D>(resource);
                if (!icon) return;
                var im=FrontEndUI.Rect(b.transform,"Icon",8,5,64,49).gameObject.AddComponent<RawImage>();
                im.texture=icon; im.raycastTarget=false;
            }
            Button Tile(string label, float x, float y, Action click, bool selected = false)
            {
                var b=FrontEndUI.Button(root,"",x,y,80,78,click,selected?FrontEndUI.Gold:new Color(.16f,.14f,.19f));
                var caption=FrontEndUI.Text(b.transform,label,3,49,74,26,15,TextAlignmentOptions.Center);
                caption.enableAutoSizing=true; caption.fontSizeMin=10;
                return b;
            }
            void Open(string title)
            {
                root.SetAsLastSibling();
                ClearTooltip();
                if(picker)UnityEngine.Object.Destroy(picker.gameObject);
                picker=FrontEndUI.Box(root,"Loadout choices",600,-50,660,475,new Color(.065f,.075f,.105f,.99f)).transform;
                FrontEndUI.Text(picker,title,18,10,550,45,26);
                FrontEndUI.Button(picker,"关闭",550,12,90,40,()=>{ClearTooltip();UnityEngine.Object.Destroy(picker.gameObject);});
            }
            void Finish() { ClearTooltip(); changed?.Invoke(); }
            string[] labels={"Q / E / V","被动","光环","武器"};
            for(int row=0;row<4;row++)
            {
                FrontEndUI.Box(root,"Row",0,row*95,550,86,new Color(.055f,.045f,.065f,.94f));
                FrontEndUI.Text(root,labels[row],8,row*95,90,80,19,TextAlignmentOptions.Center);
            }
            for(int slot=0;slot<6;slot++)
            {
                int index=slot;
                string id=HeroLoadoutCatalog.Saved(hero,slot);
                var action=slot<3?HeroLoadoutCatalog.Action(id):null;
                var passive=slot>=3?HeroLoadoutCatalog.Passive(id):null;
                string name=action?.name??passive?.name??"未配置";
                string text=action?.description??passive?.description??"点击选择。最终角色技能效果等待设计；可手动选择调试占位验证链路。";
                var button=Tile(slot<3?new[]{"Q","E","V"}[slot]:"被动"+(slot-2),
                    110+slot%3*110,slot/3*95,()=>
                {
                    Open(index<3?"选择主动技能（当前为调试占位）":"选择被动（同一被动不可重复）");
                    var options=HeroLoadoutCatalog.Options(hero,index);
                    FrontEndUI.Button(picker,"不装备",20,65,600,44,()=>{HeroLoadoutCatalog.Save(hero,index,"");Finish();});
                    for(int n=0;n<options.Length;n++)
                    {
                        string value=options[n];
                        var a=HeroLoadoutCatalog.Action(value);var p=HeroLoadoutCatalog.Passive(value);
                        var b=FrontEndUI.Button(picker,a?.name??p?.name??value,20,120+n*82,600,68,()=>
                        {
                            if(index>=3) for(int other=3;other<6;other++)
                                if(other!=index&&HeroLoadoutCatalog.Saved(hero,other)==value)HeroLoadoutCatalog.Save(hero,other,"");
                            HeroLoadoutCatalog.Save(hero,index,value);Finish();
                        });
                        Hover(b,a?.name??p?.name??value,a?.description??p?.description??"");
                    }
                },!string.IsNullOrEmpty(id));
                button.name="Hero slot "+slot;
                Image(button,"RainImported/LogIcons/"+(action?.icon??passive?.icon??"texUnlockIconSmall"));
                Hover(button,name,text);
            }
            for(int option=0;option<2;option++)
            {
                int selected=option;
                var b=Tile("光环 "+(option==0?"A":"B"),110+option*110,190,()=>
                {PlayerPrefs.SetInt("HeroAura."+hero,selected);PlayerPrefs.Save();Finish();},HeroLoadoutCatalog.SavedAura(hero)==option);
                var portrait=RangeCharacterSelection.Portrait(hero);
                if(portrait){var im=FrontEndUI.Rect(b.transform,"Portrait",8,5,64,49).gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=portrait;im.preserveAspect=true;im.raycastTarget=false;}
                Hover(b,HeroLoadoutCatalog.AuraName(hero,option),HeroLoadoutCatalog.AuraDescription(hero,option)+"\n自己与同队存活玩家生效；相同光环不叠加。点击切换。 ");
            }
            string trait=PlayerPrefs.GetString("HeroTrait."+hero,"");
            var traitButton=Tile("特性",330,190,()=>
            {
                Open("特性技能 · 神祇基础效果临时占位");
                FrontEndUI.Button(picker,"不装备",20,60,600,36,()=>{PlayerPrefs.SetString("HeroTrait."+hero,"");PlayerPrefs.Save();Finish();});
                int n=0;
                foreach(var entry in Jotunnslayer.GodSkillGm.Catalog)
                {
                    var value=entry;
                    var b=FrontEndUI.Button(picker,"",20+n%7*89,110+n/7*66,82,60,()=>
                    {PlayerPrefs.SetString("HeroTrait."+hero,value.id);PlayerPrefs.Save();Finish();});
                    var im=FrontEndUI.Rect(b.transform,"Icon",15,3,48,48).gameObject.AddComponent<RawImage>();im.texture=value.icon;im.raycastTarget=false;
                    Hover(b,value.name,TraitDescription(value.id,value.description));n++;
                }
            },!string.IsNullOrEmpty(trait));
            string traitName="未配置",traitDescription="选择巨人杀手神祇基础效果，作为可替换特性。";
            foreach(var entry in Jotunnslayer.GodSkillGm.Catalog)if(entry.id==trait)
            {
                traitName=entry.name;traitDescription=TraitDescription(entry.id,entry.description);
                var im=FrontEndUI.Rect(traitButton.transform,"Trait icon",8,5,64,49).gameObject.AddComponent<RawImage>();im.texture=entry.icon;im.raycastTarget=false;break;
            }
            Hover(traitButton,traitName,traitDescription);
            int weapon=PlayerPrefs.GetInt("HeroWeapon."+hero,-1);
            var weaponPrefab=PlayerInstance.HeroWeaponPrefab(weapon);
            var weaponButton=Tile(weaponPrefab?weaponPrefab.name:"默认武器",110,285,()=>
            {
                Open("选择主武器");
                FrontEndUI.Button(picker,"默认武器",20,60,600,38,()=>{PlayerPrefs.SetInt("HeroWeapon."+hero,-1);PlayerPrefs.Save();Finish();});
                if(!ItemManager.Instance||ItemManager.Instance.SlotsLodout.Length==0)
                {FrontEndUI.Text(picker,"武器目录尚未加载，进入训练场后按F8配置。",20,115,600,70,22);return;}
                var options=ItemManager.Instance.SlotsLodout[0].availableItemsForSlot;
                for(int n=0;n<Mathf.Min(options.Length,24);n++)
                {
                    int value=n;if(!options[n])continue;
                    var item=options[n].GetComponent<Item>();
                    var b=FrontEndUI.Button(picker,item?item.ItemName:options[n].name,20+n%3*207,110+n/3*40,200,36,()=>
                    {PlayerPrefs.SetInt("HeroWeapon."+hero,value);PlayerPrefs.Save();Finish();});
                    var caption=b.GetComponentInChildren<TMP_Text>();caption.fontSize=15;
                    Hover(b,item?item.ItemName:options[n].name,"装备项目现有主武器；使用原有枪械与动作逻辑。");
                }
            },weaponPrefab);
            var selectedWeapon=weaponPrefab?weaponPrefab.GetComponent<Item>():null;
            var local=GameManager.myPlayerInstance;
            if(!selectedWeapon && local && local.HeroSelection==hero && local.MyCharacter && local.MyCharacter.CharacterItemManager && local.MyCharacter.CharacterItemManager.Slots.Count>0)
                selectedWeapon=local.MyCharacter.CharacterItemManager.Slots[0].Item;
            if(selectedWeapon && selectedWeapon.ItemIcon)
            {
                var icon=FrontEndUI.Rect(weaponButton.transform,"Weapon icon",8,5,64,49).gameObject.AddComponent<UnityEngine.UI.Image>();
                icon.sprite=selectedWeapon.ItemIcon;icon.preserveAspect=true;icon.raycastTarget=false;
            }
            Hover(weaponButton,weaponPrefab?weaponPrefab.name:"默认主武器","配置实际主武器；1～5保留局内武器槽切换。");
        }
    }
}
