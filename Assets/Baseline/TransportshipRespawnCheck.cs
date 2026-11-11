using System;
using System.Collections;
using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
using Mirror;
// Explicitly opted-in runtime regression check; never active during normal gameplay.
public class TransportshipRespawnCheck : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-transportshipRespawnCheck") < 0) return;
        var go=new GameObject("TransportshipRespawnCheck"); DontDestroyOnLoad(go);go.AddComponent<TransportshipRespawnCheck>();
    }
    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+75;
        while(Time.realtimeSinceStartup<deadline && (!NetworkServer.active || GameManager.Players.Count<2 || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="map_transportship")) yield return null;
        if(!NetworkServer.active || GameManager.Players.Count<2){Debug.LogError("TRANSPORTSHIP_RESPAWN_FAIL: two-player session not ready");yield break;}
        yield return new WaitForSecondsRealtime(18);
        var players=new List<PlayerInstance>(GameManager.Players.Values);
        var oldIds=new uint[players.Count];
        for(int i=0;i<players.Count;i++)
        {
            if(!players[i].MyCharacter){Debug.LogError("TRANSPORTSHIP_RESPAWN_FAIL: no initial character");yield break;}
            var ch=players[i].MyCharacter;oldIds[i]=ch.netId;
            Debug.Log($"TRANSPORTSHIP_INITIAL team={players[i].Team} id={ch.netId} position={ch.transform.position} grounded={ch.GetComponent<CharacterController>().isGrounded}");
            ch.Server_ChangeHealthState(9999,CharacterPart.body,AttackType.falldamage,ch,0);
        }
        yield return new WaitForSecondsRealtime(8);
        for(int i=0;i<players.Count;i++)
        {
            var ch=players[i].MyCharacter;
            bool valid=ch && ch.netId!=oldIds[i] && ch.CurrentHealth>0 && ch.transform.position.y>-17.5f && ch.transform.position.y<-15f && (players[i].Team==0?ch.transform.position.x<0:ch.transform.position.x>0);
            if(valid)Debug.Log($"TRANSPORTSHIP_RESPAWN_PASS team={players[i].Team} oldId={oldIds[i]} newId={ch.netId} position={ch.transform.position} grounded={ch.GetComponent<CharacterController>().isGrounded}");
            else Debug.LogError($"TRANSPORTSHIP_RESPAWN_FAIL team={players[i].Team}");
        }
    }
}
