using Mirror;
using MTPSKIT.Gameplay;
using TMPro;
using UnityEngine;

namespace MTPSKIT
{
    public sealed class HeroLoadoutHud : MonoBehaviour
    {
        Canvas canvas, editor;
        TMP_Text status;
        PlayerInstance player;
        float nextText;
        bool previousInput;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var host=new GameObject("Hero loadout HUD");DontDestroyOnLoad(host);host.AddComponent<HeroLoadoutHud>();
        }
        void Update()
        {
            var current=GameManager.myPlayerInstance;
            if(!current||!current.HasHeroLoadout||!current.MyCharacter)
            {Clear();return;}
            player=current;
            if(!canvas)
            {
                canvas=FrontEndUI.Canvas(transform,22);
                status=FrontEndUI.Text(canvas.transform,"",650,890,620,110,20,TextAlignmentOptions.TopLeft);
            }
            if(Input.GetKeyDown(KeyCode.F8) && Application.isFocused && (editor || UI.ClientFrontend.GamePlayInput()))
            {
                if(editor)CloseEditor();else OpenEditor();
            }
            if(editor && Input.GetKeyDown(KeyCode.Escape))CloseEditor();
            if(Time.unscaledTime<nextText)return;
            nextText=Time.unscaledTime+.1f;
            var hero=HeroLoadoutCatalog.Hero(player.HeroSelection);
            string Line(int slot,string key)
            {
                var action=HeroLoadoutCatalog.Action(player.HeroSlot(slot));
                if(action==null)return key+" 未配置";
                double left=System.Math.Max(0,player.HeroReady(slot)-NetworkTime.time);
                return key+" "+action.name.Replace("调试占位·","")+(left>0?" "+left.ToString("0.0")+"s":" 就绪");
            }
            status.text=Line(0,"Q")+"    "+Line(1,"E")+"    "+Line(2,"V")+
                "\n法力 "+player.HeroMana.ToString("0")+" / "+hero.maximumMana+"    F 交互 · 右键瞄准 · F8 配置\n"+
                (player.MyCharacter.GetComponent<CharacterMotor>().GodWarControl ? "战神3C" : "常规枪械3C")+" · N切换    光环："+
                HeroLoadoutCatalog.AuraName(player.HeroSelection,player.HeroAura);
        }
        internal void OpenEditor()
        {
            editor=FrontEndUI.Canvas(transform,85);
            FrontEndUI.Box(editor.transform,"Background",60,130,680,770,new Color(.03f,.03f,.05f,.99f));
            FrontEndUI.Text(editor.transform,RangeCharacterSelection.DisplayName(player.HeroSelection)+" · 战斗配置",95,160,580,60,30);
            FrontEndUI.Text(editor.transform,"点击选择 · 悬停查看效果 · F8关闭",95,230,580,45,21);
            var root=FrontEndUI.Rect(editor.transform,"Loadout rows",95,340,550,400);
            HeroLoadoutUI.Draw(root,player.HeroSelection,()=>
            {
                int hero=player.HeroSelection;
                player.CmdSetHeroLoadout(HeroLoadoutCatalog.Saved(hero,0),HeroLoadoutCatalog.Saved(hero,1),HeroLoadoutCatalog.Saved(hero,2),
                    HeroLoadoutCatalog.Saved(hero,3),HeroLoadoutCatalog.Saved(hero,4),HeroLoadoutCatalog.Saved(hero,5),
                    HeroLoadoutCatalog.SavedAura(hero),PlayerPrefs.GetString("HeroTrait."+hero,""),PlayerPrefs.GetInt("HeroWeapon."+hero,-1));
                bool input=previousInput;
                Destroy(editor.gameObject);editor=null;OpenEditor();previousInput=input;
            });
            UI.ClientFrontend.ShowCursor(true);
            previousInput = PlayerGameplayInput.Instance && PlayerGameplayInput.Instance.enabled;
            if(PlayerGameplayInput.Instance)PlayerGameplayInput.Instance.enabled=false;
        }
        void CloseEditor()
        {
            if(editor)Destroy(editor.gameObject);editor=null;
            UI.ClientFrontend.ShowCursor(false);
            if(PlayerGameplayInput.Instance)PlayerGameplayInput.Instance.enabled=previousInput;
        }
        void Clear()
        {
            if(editor)CloseEditor();
            if(canvas)Destroy(canvas.gameObject);canvas=null;player=null;
        }
        void OnDestroy()=>Clear();
    }
}
