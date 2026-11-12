using System;
using System.Collections;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
// Opt-in smoke runner. Normal launches are unaffected.
public class BaselineSmoke : MonoBehaviour
{
    bool host;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-baselineHost") < 0 && Array.IndexOf(args, "-baselineClient") < 0) return;
        Application.runInBackground = true;
        var go = new GameObject("BaselineSmoke");
        DontDestroyOnLoad(go);
        go.AddComponent<BaselineSmoke>().host = Array.IndexOf(args, "-baselineHost") >= 0;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3);
        UserSettings.UserNickname = host ? "BaselineHost" : "BaselineClient";
        var manager = DNNetworkManager.Instance;
        manager.SetAddressAndPort("127.0.0.1", 17977);
        if (host)
        {
                        bool battlefield = Array.IndexOf(Environment.GetCommandLineArgs(), "-baselineBattlefield") >= 0;
            bool ship = Array.IndexOf(Environment.GetCommandLineArgs(), "-baselineTransportship") >= 0;
            manager.onlineScene = battlefield ? "Assets/Game/Maps/WAVE_ONEWAY/Scenes/map_desert_battlefield.unity" : ship ? "Assets/Game/Maps/TRANSPORTSHIP/Scenes/map_transportship.unity" : "Assets/AssetStore/desNetware/Multiplayer TPS KIT/Levels/map_arena.unity";
            RoomSetup.Properties = new RoomProperties { P_Gamemode = battlefield ? Gamemodes.Battlefield : Gamemodes.TeamDeathmatch, P_MaxPlayers = battlefield ? 2 : 4, P_FillEmptySlotsWithBots = battlefield, P_GameDuration = 300, P_RespawnCooldown = 4 };
            manager.maxConnections = battlefield ? 2 : 4;
            manager.StartHost();
        }
        else manager.StartClient();
        var deadline = Time.realtimeSinceStartup + 60;
        while (Time.realtimeSinceStartup < deadline && (!NetworkClient.localPlayer || !GameManager.Gamemode)) yield return null;
        if (!NetworkClient.localPlayer) { Debug.LogError("BASELINE_FAIL: no local player"); yield break; }
        var player = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
        player.ClientRequestJoiningTeam(host ? 0 : 1);
        yield return new WaitForSecondsRealtime(15);
        Debug.Log($"BASELINE_STATE host={host} connected={NetworkClient.isConnected} players={GameManager.Players.Count} team={player.Team} character={player.MyCharacter != null} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} position={(player.MyCharacter ? player.MyCharacter.transform.position.ToString() : "none")}");
    }
}
