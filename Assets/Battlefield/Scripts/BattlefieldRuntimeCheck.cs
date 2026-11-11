using System;
using System.Collections;
using Mirror;
using MTPSKIT;
using MTPSKIT.Battlefield;
using MTPSKIT.Gameplay;
using UnityEngine;
public class BattlefieldRuntimeCheck : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-battlefieldCheck")<0)return;
        Time.timeScale=4;var g=new GameObject("BattlefieldRuntimeCheck_TEST");DontDestroyOnLoad(g);g.AddComponent<BattlefieldRuntimeCheck>();
    }
    IEnumerator Start()
    {
        float timeout=Time.realtimeSinceStartup+90;
        while(Time.realtimeSinceStartup<timeout&&(!BattlefieldGamemode.Instance||!NetworkClient.localPlayer||GameManager.Players.Count<2))yield return null;
        var mode=BattlefieldGamemode.Instance;
        if(!mode||!NetworkClient.localPlayer){Debug.LogError("BATTLEFIELD_TEST_FAIL session not ready");yield break;}
        var player=NetworkClient.localPlayer.GetComponent<PlayerInstance>();
        yield return new WaitForSecondsRealtime(3);
        Debug.Log($"BATTLEFIELD_REPLICA host={NetworkServer.active} players={GameManager.Players.Count} configuredHumanSlots={MTPSKIT.Gameplay.Gamemodes.RoomSetup.Properties.P_MaxPlayers} hp={(player.MyCharacter?player.MyCharacter.CurrentHealth:0)} maxHP={(player.MyCharacter?player.MyCharacter.MaxHealth:0)} entities={mode.Entities.Count}");
        if(!NetworkServer.active)yield break;
        float progress=0;int maxBlue=0,maxOrange=0;timeout=Time.realtimeSinceStartup+100;
        while(mode.MatchClock<155&&mode.Phase!=BattlefieldPhase.Finished&&Time.realtimeSinceStartup<timeout)
        {
            maxBlue=Mathf.Max(maxBlue,mode.CountMinions(0));maxOrange=Mathf.Max(maxOrange,mode.CountMinions(1));
            for(int i=0;i<mode.Entities.Count;i++)
            {
                var e=mode.Entities[i];if(e&&e.Kind==BattlefieldKind.Minion)foreach(var s in mode.Map.spawns)if(s.id==e.SourceId)progress=Mathf.Max(progress,Vector3.Distance(e.transform.position,s.position));
            }
            yield return new WaitForSecondsRealtime(1);
        }
        bool valid=GameManager.Players.Count==2&&DNNetworkManager.Instance.maxConnections==2&&player.MyCharacter&&player.MyCharacter.MaxHealth==500&&mode.BlueWave>=10&&mode.OrangeWave>=10&&maxBlue<=24&&maxOrange<=24&&progress>10&&mode.TowerShots>0&&mode.MinionShots>0;
        Debug.Log($"BATTLEFIELD_TEST_{(valid?"PASS":"FAIL")} players={GameManager.Players.Count} waves={mode.BlueWave}/{mode.OrangeWave} peakMinions={maxBlue}/{maxOrange} created={mode.MinionsCreated} towerShots={mode.TowerShots} minionShots={mode.MinionShots} deaths={mode.Deaths} progress={progress:F1} clock={mode.MatchClock:F1}");
        var old=player.MyCharacter;uint oldId=old.netId;
        old.Server_ChangeHealthStateRaw(9999,CharacterPart.body,AttackType.falldamage,old,0);
        yield return new WaitForSecondsRealtime(2);
        bool respawn=player.MyCharacter&&player.MyCharacter.netId!=oldId&&player.MyCharacter.CurrentHealth==500&&player.MyCharacter.MaxHealth==500;
        Debug.Log("BATTLEFIELD_PLAYER_RESPAWN_"+(respawn?"PASS":"FAIL"));
        // Controlled damage-pipeline tests. Active state is forced only in this explicit TEST PROFILE.
        var attacker=player.MyCharacter;
        var front=mode.Buildings["WaveTowerFactory4"];var middle=mode.Buildings["WaveTowerFactory3"];var rear=mode.Buildings["WaveTowerFactory6"];var baseCamp=mode.Buildings["Wave_BaseCampGR"];
        int protectedHp=baseCamp.Health.CurrentHealth;int lockedHp=middle.Health.CurrentHealth;
        baseCamp.Health.Server_ChangeHealthStateRaw(99999,CharacterPart.body,AttackType.hitscan,attacker,0);
        middle.Health.Server_ChangeHealthStateRaw(99999,CharacterPart.body,AttackType.hitscan,attacker,0);
        bool protectedOK=baseCamp.Health.CurrentHealth==protectedHp&&middle.Health.CurrentHealth==lockedHp;
        int beforeDeaths=mode.Deaths,expectedDeaths=0;
        foreach(var tower in new[]{front,middle,rear})
        {
            // Natural waves may already have destroyed a front tower before this check.
            if(tower.Alive){expectedDeaths++;tower.BuildingState=BattlefieldBuildingState.Active;}
            tower.Health.Server_ChangeHealthStateRaw(99999,CharacterPart.body,AttackType.hitscan,attacker,0);
            tower.Health.Server_ChangeHealthStateRaw(99999,CharacterPart.body,AttackType.hitscan,attacker,0);
        }
        bool chain=protectedOK&&mode.PrerequisiteMet(baseCamp)&&!front.Alive&&!middle.Alive&&!rear.Alive&&mode.Deaths-beforeDeaths==expectedDeaths;
        baseCamp.Health.Server_ChangeHealthStateRaw(99999,CharacterPart.body,AttackType.hitscan,attacker,0);
        yield return null;yield return null;
        bool result=mode.Phase==BattlefieldPhase.Finished&&mode.Winner==0;
        int waves=mode.BlueWave+mode.OrangeWave,shots=mode.TowerShots+mode.MinionShots;
        yield return new WaitForSecondsRealtime(2);
        bool frozen=waves==mode.BlueWave+mode.OrangeWave&&shots==mode.TowerShots+mode.MinionShots;
        Debug.Log($"BATTLEFIELD_LIFECYCLE_{(chain&&result&&frozen?"PASS":"FAIL")} shield={protectedOK} chain={chain} result={result} frozen={frozen}");

    }
    void OnGUI()
    {
        var m=BattlefieldGamemode.Instance;if(!m)return;GUI.Label(new Rect(Screen.width*.5f-180,95,360,25),"TEST PROFILE · 4x SIMULATION");
    }
    void OnDestroy(){Time.timeScale=1;}
}
