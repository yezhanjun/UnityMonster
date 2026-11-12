using Mirror;
using MTPSKIT.UI;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
namespace MTPSKIT.Battlefield
{
    public class BattlefieldUI : UIGamemode
    {
        BattlefieldGamemode mode;PlayerInstance player;bool selecting=true;GUIStyle text,small,title;string headline,detail;float refresh;
        public override void SetupUI(Gamemode gamemode,NetworkIdentity identity)
        {
            mode=(BattlefieldGamemode)gamemode;player=identity.GetComponent<PlayerInstance>();
            gameObject.AddComponent<BattlefieldHealthBars>().Initialize(mode);
            GameManager.myPlayerInstance=player;ClientFrontend.GamemodeUI=this;
            player.PlayerEvent_OnReceivedTeamResponse+=OnReceivedTeamResponse;ClientFrontend.ShowCursor(true);
        }
        public override void Btn_ShowTeamSelector(){if(!selecting){selecting=true;ClientFrontend.ShowCursor(true);}}
        protected override void OnReceivedTeamResponse(int team,int result){if(result==0&&selecting){selecting=false;ClientFrontend.ShowCursor(false);}}
        void OnDestroy(){if(player)player.PlayerEvent_OnReceivedTeamResponse-=OnReceivedTeamResponse;if(selecting)ClientFrontend.ShowCursor(false);if(ClientFrontend.GamemodeUI==this)ClientFrontend.GamemodeUI=null;}
        void OnGUI()
        {
            if(!mode)return;
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=mode.Settings.UIFont,fontSize=18,alignment=TextAnchor.MiddleCenter};text.normal.textColor=Color.white;
                small=new GUIStyle(text){fontSize=14};title=new GUIStyle(text){fontSize=24};
            }
            if(Time.unscaledTime>=refresh)
            {
                refresh=Time.unscaledTime+.25f;
                headline="沙漠风暴 · 战场模式   "+(mode.Phase==BattlefieldPhase.Preparing?"准备中":mode.Phase==BattlefieldPhase.Finished?(mode.Winner==2?"平局":(mode.Winner==0?"蓝队":"橙队")+"获胜"):"推进中");
                detail=$"小兵 蓝 {mode.BlueMinions}/{mode.Settings.CapPerTeam} · 橙 {mode.OrangeMinions}/{mode.Settings.CapPerTeam}    波次 {mode.BlueWave}/{mode.OrangeWave}    {(int)mode.MatchClock/60:00}:{(int)mode.MatchClock%60:00}";
            }
            float w=Screen.width;
            Panel(new Rect(w*.5f-360,12,720,78));GUI.Label(new Rect(w*.5f-350,15,700,35),headline,title);GUI.Label(new Rect(w*.5f-350,50,700,30),detail,small);
            if(selecting)
            {
                var r=new Rect(w*.5f-260,Screen.height*.5f-100,520,200);Panel(r);GUI.Label(new Rect(r.x+10,r.y+15,500,40),"选择阵营 · 小兵不占玩家名额",text);
                var button=new GUIStyle(GUI.skin.button){font=mode.Settings.UIFont,fontSize=20};
                if(GUI.Button(new Rect(r.x+30,r.y+85,210,60),"加入蓝队",button))SelectTeam(0);
                if(GUI.Button(new Rect(r.x+280,r.y+85,210,60),"加入橙队",button))SelectTeam(1);
            }
            int row=0;
            for(int i=0;i<mode.Entities.Count;i++)
            {
                var e=mode.Entities[i];if(!e||e.Kind!=BattlefieldKind.Base)continue;
                Panel(new Rect(15,15+row*48,260,43));GUI.Label(new Rect(18,17+row*48,254,38),e.Label,small);row++;
            }
        }
        static void Panel(Rect r){Color c=GUI.color;GUI.color=new Color(.025f,.04f,.06f,.9f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=c;}
    }
}
