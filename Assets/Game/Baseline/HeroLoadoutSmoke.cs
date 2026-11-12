using System;
using System.Collections;
using System.IO;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace MTPSKIT
{
    // Opt-in graphical Player checks. Never runs for normal launches.
    public sealed class HeroLoadoutSmoke : MonoBehaviour
    {
        string output;
        bool runtimeError;
        readonly string[] prefKeys = {"HeroLoadout.12.0","HeroLoadout.12.1","HeroLoadout.12.2","HeroLoadout.12.3","HeroLoadout.12.4","HeroLoadout.12.5","HeroTrait.12"};
        string[] oldPrefs;
        bool[] hadPrefs;
        int oldAura; bool hadAura;
        void RestorePrefs()
        {
            if(oldPrefs==null)return;
            for(int i=0;i<prefKeys.Length;i++) if(hadPrefs[i])PlayerPrefs.SetString(prefKeys[i],oldPrefs[i]);else PlayerPrefs.DeleteKey(prefKeys[i]);
            if(hadAura)PlayerPrefs.SetInt("HeroAura.12",oldAura);else PlayerPrefs.DeleteKey("HeroAura.12");
            PlayerPrefs.Save();oldPrefs=null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(),"--hero-loadout-smoke")<0)return;
            var host=new GameObject("Hero loadout acceptance");DontDestroyOnLoad(host);host.AddComponent<HeroLoadoutSmoke>();
        }
        void Awake()
        {
            output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"HeroLoadoutAcceptance");Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;
        }
        void OnDestroy(){Application.logMessageReceived-=Log;RestorePrefs();}
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=true;}
        static void Check(bool pass,string message)
        {if(!pass)throw new InvalidOperationException("HERO_LOADOUT_FAILED "+message);}
        static PlayerInstance Peer(PlayerInstance owner)
        {
            foreach(var pair in GameManager.Players)
                if(pair.Value&&pair.Value!=owner&&!pair.Value.BOT&&pair.Value.MyCharacter)return pair.Value;
            return null;
        }
        IEnumerator Start()
        {
            float deadline=Time.realtimeSinceStartup+90;
            while((!GameManager.myPlayerInstance||!GameManager.myPlayerInstance.MyCharacter)&&Time.realtimeSinceStartup<deadline)yield return null;
            var player=GameManager.myPlayerInstance;Check(player&&player.MyCharacter,"owner not ready");
            if(!NetworkServer.active)
            {
                // Read actual replicated values on the remote client throughout the host's aura sequence.
                int seen=0;
                while(Time.realtimeSinceStartup<deadline+90)
                {
                    var b=player.MyCharacter?player.MyCharacter.HeroBonuses:default;
                    if(b.regeneration>0)seen|=1;if(b.cooldown>0)seen|=2;if(b.goldPerSecond>0)seen|=4;
                    if(b.speed>0)seen|=8;if(b.damage>0)seen|=16;if(b.critical>0)seen|=32;
                    if(b.armor>0)seen|=64;if(b.health>0)seen|=128;
                    if(seen==255){Debug.Log("HERO_LOADOUT_REMOTE_AURAS_PASSED 8/8");File.WriteAllText(Path.Combine(output,"remote-result.txt"),"AURA_SYNC=8/8");yield break;}
                    yield return null;
                }
                Check(false,"remote did not observe all eight aura values");yield break;
            }
            while(!Peer(player)&&Time.realtimeSinceStartup<deadline)yield return null;
            var peer=Peer(player);Check(peer&&peer.MyCharacter,"second real client missing");
            // Character spawning precedes the initial client identity command. Let both clients finish joining.
            yield return new WaitForSeconds(3f);
            player.MyCharacter.RangeInvulnerable=true;peer.MyCharacter.RangeInvulnerable=true;
            foreach(int hero in new[]{12,13,14,15})
            {
                Check(player.ServerSetRangeHero(hero),"hero selection "+hero);
                yield return new WaitForSeconds(.5f);
                for(int aura=0;aura<2;aura++)
                {
                    player.CmdSetHeroLoadout("","","","","","",aura,"",-1);
                    yield return new WaitForSeconds(.5f);
                    var self=player.MyCharacter.HeroBonuses;var ally=peer.MyCharacter.HeroBonuses;
                    Debug.Log("HERO_AURA_VALUES hero="+player.HeroSelection+" option="+player.HeroAura+" self="+JsonUtility.ToJson(self)+" ally="+JsonUtility.ToJson(ally)+" teams="+player.Team+"/"+peer.Team+" ffa="+GameManager.Gamemode.FFA);
                    Check(self.Same(ally),"teammate stats mismatch "+hero+"/"+aura);
                    bool correct=hero switch
                    {
                        12=>aura==0?ally.regeneration==1&&ally.manaRegeneration==1:ally.cooldown==.1f&&ally.regeneration==0,
                        13=>aura==0?ally.goldPerSecond==1:ally.speed==.01f&&ally.goldPerSecond==0,
                        14=>aura==0?ally.damage==.1f:ally.critical==.1f&&ally.damage==0,
                        _=>aura==0?ally.armor==.1f:ally.health==.1f&&peer.MyCharacter.MaxHealth>100
                    };
                    Check(correct,"aura values "+hero+"/"+aura);
                    if(hero==13&&aura==0)
                    {
                        int gold=peer.RainGold;yield return new WaitForSeconds(1.2f);
                        Check(peer.RainGold>gold,"team gold not issued");
                    }
                    if(hero==12&&aura==0)
                    {
                        player.MyCharacter.CurrentHealth-=5;player.HeroMana=50;
                        int health=player.MyCharacter.CurrentHealth;yield return new WaitForSeconds(1.2f);
                        Check(player.MyCharacter.CurrentHealth>health&&player.HeroMana>=52,"health/mana regeneration missing");
                    }
                    Debug.Log("HERO_LOADOUT_AURA_PASSED hero="+hero+" option="+aura+" peer="+peer.netId);
                }
            }
            // Health contribution must be reversible and must not multiply itself on refresh.
            int boosted=peer.MyCharacter.MaxHealth;
            peer.MyCharacter.ServerSetMaximumHealth(boosted);
            Check(peer.MyCharacter.MaxHealth==boosted,"health bonus compounded");
            player.CmdSetHeroLoadout("test_blast","test_heal","test_guard","test_regen","test_damage","test_speed",0,"",-1);
            yield return new WaitForSeconds(.4f);
            Check(peer.MyCharacter.HeroAppliedMaximum==0&&peer.MyCharacter.MaxHealth< boosted,"health bonus not removed");
            Check(player.MyCharacter.HeroBonuses.regeneration==1&&player.MyCharacter.HeroBonuses.damage==.05f&&player.MyCharacter.HeroBonuses.speed==.05f,"passive stats");
            Health target=null;
            foreach(var pair in GameManager.HealthInstances)
                if(pair.Value && pair.Value.CurrentHealth>0 && pair.Value.Team!=player.Team) { target=pair.Value; break; }
            Check(target,"training enemy missing");
            target.transform.position=player.MyCharacter.transform.position+player.MyCharacter.transform.forward*3;
            int before=target.CurrentHealth;float mana=player.HeroMana;
            player.CmdUseHeroSkill(0);yield return new WaitForSeconds(.1f);
            Debug.Log("HERO_SKILL_VALUES hp="+before+"->"+target.CurrentHealth+" mana="+mana+"->"+player.HeroMana+" q="+player.HeroQ);
            Check(target.CurrentHealth<before&&player.HeroMana<mana,"Q damage/mana missing");
            mana=player.HeroMana;player.CmdUseHeroSkill(0);yield return new WaitForSeconds(.1f);Check(player.HeroMana>=mana&&player.HeroMana<mana+1,"cooldown allowed repeated cast");
            player.MyCharacter.CurrentHealth=50;player.CmdUseHeroSkill(1);yield return new WaitForSeconds(.1f);Check(player.MyCharacter.CurrentHealth>50,"E healing missing");
            player.CmdUseHeroSkill(2);yield return new WaitForSeconds(.1f);Check(player.MyCharacter.HeroFlatArmor==50,"V armor missing");
            player.CmdSetHeroLoadout("unknown","","","test_regen","test_regen","test_regen",0,"unknown",-1);yield return new WaitForSeconds(.1f);
            Check(player.HeroQ==""&&player.HeroPassive2==""&&player.HeroPassive3==""&&player.HeroTrait=="","invalid config/duplicate passive accepted");
            Debug.Log("HERO_LOADOUT_QEV_PASSIVES_PASSED cooldown=True mana=True damage=True heal=True armor=True validation=True");
            player.ServerSetRangeHero(12);yield return new WaitForSeconds(.5f);
            player.CmdSetHeroLoadout("test_blast","test_heal","test_guard","test_regen","test_damage","test_speed",1,"active_thor_D",-1);
            yield return new WaitForSeconds(.5f);
            Check(player.MyCharacter.GetComponentInChildren<Jotunnslayer.SkillBattlePreview>(),"trait not running");
            oldPrefs=new string[prefKeys.Length];hadPrefs=new bool[prefKeys.Length];
            for(int i=0;i<prefKeys.Length;i++){hadPrefs[i]=PlayerPrefs.HasKey(prefKeys[i]);oldPrefs[i]=PlayerPrefs.GetString(prefKeys[i]);}
            hadAura=PlayerPrefs.HasKey("HeroAura.12");oldAura=PlayerPrefs.GetInt("HeroAura.12");
            foreach(int i in new[]{0,1,2}){HeroLoadoutCatalog.Save(12,i,new[]{"test_blast","test_heal","test_guard"}[i]);HeroLoadoutCatalog.Save(12,i+3,new[]{"test_regen","test_damage","test_speed"}[i]);}
            PlayerPrefs.SetInt("HeroAura.12",1);PlayerPrefs.SetString("HeroTrait.12","active_thor_D");
            FindFirstObjectByType<HeroLoadoutHud>().OpenEditor();yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"配置四行.png"));yield return new WaitForEndOfFrame();
            UnityEngine.UI.Button qButton=null;
            foreach(var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if(button.name=="Hero slot 0"){qButton=button;break;}
            Check(qButton,"Q configuration tile missing");
            qButton.GetComponent<HeroLoadoutHover>().OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"技能悬停.png"));yield return new WaitForEndOfFrame();
            qButton.onClick.Invoke();yield return null;
            UnityEngine.UI.Button healChoice=null;
            foreach(var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if(button.name=="调试占位·治疗"){healChoice=button;break;}
            Check(healChoice,"skill choices not opened");
            healChoice.onClick.Invoke();yield return new WaitForSeconds(.3f);
            Check(HeroLoadoutCatalog.Saved(12,0)=="test_heal"&&player.HeroQ=="test_heal","UI selection did not apply to server");
            Debug.Log("HERO_LOADOUT_UI_SELECTION_PASSED");
            RestorePrefs();
            Check(!runtimeError,"unexpected runtime error");
            File.WriteAllText(Path.Combine(output,"host-result.txt"),"AURAS=8/8\nREAL_LOCAL_CLIENTS=2\nQEV=3/3\nPASSIVE_SLOTS=3\nTRAIT_RUNNING=True\nUI_SELECTION=True\nRUNTIME_ERRORS=0\n");
            Debug.Log("HERO_LOADOUT_ACCEPTANCE_PASSED");
        }
    }
}
