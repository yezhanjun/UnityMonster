using System;
using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.AI;
namespace MTPSKIT.Battlefield
{
    public class BattlefieldGamemode : Gamemode
    {
        public static BattlefieldGamemode Instance;
        public BattlefieldSettings Settings;
        public SpawnpointsContainer[] PlayerSpawns;
        [SyncVar] public BattlefieldPhase Phase;
        [SyncVar] public float MatchClock;
        [SyncVar] public int Winner=-1,BlueMinions,OrangeMinions,BlueWave,OrangeWave;
        public readonly List<BattlefieldEntity> Entities=new List<BattlefieldEntity>(80);
        public readonly Dictionary<string,BattlefieldNode> Nodes=new Dictionary<string,BattlefieldNode>();
        public readonly Dictionary<string,BattlefieldEntity> Buildings=new Dictionary<string,BattlefieldEntity>();
        public BattlefieldMapData Map {get;private set;}
        float preparation,nextStats; float[] nextWaves;
        int sequence; public int MinionsCreated,TowerShots,MinionShots,Deaths;
        public Material TraceMaterial {get;private set;}
        public BattlefieldGamemode(){Indicator=Gamemodes.Battlefield;FriendyFire=false;LetPlayersSpawnOnTheirOwn=true;}
        protected override void Awake(){base.Awake();Instance=this;}
        public override void SetupGamemode(RoomProperties properties)
        {
            properties.P_FillEmptySlotsWithBots=false;
            base.SetupGamemode(properties);
            MaxTeamSize=Mathf.Max(1,properties.P_MaxPlayers/2);
            if(Map==null)
            {
                Map=JsonUtility.FromJson<BattlefieldMapData>(Settings.MapJson.text);
                foreach(var n in Map.nodes)Nodes.Add(n.id,n);
            }
            if(isServer && nextWaves==null)
            {
                Phase=BattlefieldPhase.Preparing;preparation=Settings.PreparingSeconds;nextWaves=new float[Map.spawns.Length];
                foreach(var b in Map.buildings)
                {
                    var go=Instantiate(b.kind==3?Settings.BasePrefab:Settings.TowerPrefab,b.position,Quaternion.Euler(0,b.yaw,0));
                    var e=go.GetComponent<BattlefieldEntity>();e.Kind=(BattlefieldKind)b.kind;e.SourceId=b.id;e.Prerequisite=b.prerequisite;e.Range=b.range;
                    e.Health.Team=b.team;e.Health.CharacterName=(b.team==0?"蓝队":"橙队")+(b.kind==3?"基地":"防御塔")+b.id;
                    e.Health.MaxHealth=e.Health.CurrentHealth=b.kind==3?Settings.BaseHealth:Settings.TowerHealth;
                    Buildings.Add(b.id,e);NetworkServer.Spawn(go);
                }
                Debug.Log($"BATTLEFIELD_READY map={Map.mapId} nodes={Nodes.Count} spawners={Map.spawns.Length} towers=6 bases=2 playerHP={Settings.PlayerHealth} towerHP={Settings.TowerHealth}");
            }
        }
        public override void Relay_NewClientJoined(NetworkConnection conn,NetworkIdentity player)
        {
            var p=RoomSetup.Properties;p.P_FillEmptySlotsWithBots=false;RoomSetup.Properties=p;
            base.Relay_NewClientJoined(conn,player);
        }
        public override GameObject GetPlayerCharacterPrefab(){return Settings.PlayerPrefab;}
        public override void ConfigurePlayerCharacter(CharacterInstance character)
        {
            character.MaxHealth=character.CurrentHealth=Settings.PlayerHealth;
        }
        protected override int PlayerRequestToJoinTeamPermission(PlayerInstance player,int team)
        {
            if(team<0||team>1||Phase==BattlefieldPhase.Finished)return -2;
            if(player.Team==team)return -2;
            return _teams[team].PlayerInstances.Count>=MaxTeamSize?-1:0;
        }
        protected override void OnPlayerAddedToTeam(PlayerInstance player,int team){PlayerSpawnCharacterRequest(player);}
        public override void PlayerSpawnCharacterRequest(PlayerInstance player)
        {
            if(Phase==BattlefieldPhase.Finished||player.Team<0||player.Team>1)return;
            player.SpawnCharacter(PlayerSpawns[player.Team].GetNextSpawnPoint());
            if(Phase!=BattlefieldPhase.Playing)player.MyCharacter.BlockCharacter(true);
        }
        public override void Server_OnPlayerKilled(Health victim,Health killer){}
        void Update()
        {
            if(!isServer||nextWaves==null||Phase==BattlefieldPhase.Finished)return;
            if(Phase==BattlefieldPhase.Preparing)
            {
                preparation-=Time.deltaTime;if(preparation>0)return;
                Phase=BattlefieldPhase.Playing;MatchClock=0;BlockAllPlayers(false);GamemodeMessage("战场开始：跟随小兵，逐步摧毁敌方防御塔与基地！",4);
            }
            MatchClock+=Time.deltaTime;
            for(int i=0;i<Map.spawns.Length;i++)
            {
                var s=Map.spawns[i];if(MatchClock<nextWaves[i])continue;
                nextWaves[i]=MatchClock+s.period;
                if(s.team==0)BlueWave++;else OrangeWave++;
                int alive=CountMinions(s.team),made=0;
                for(int n=0;n<s.count && alive+made<Settings.CapPerTeam;n++)
                {
                    Vector3 candidate=s.position+new Vector3((n%2-.5f)*1.05f,0,(n/2-.5f)*1.05f);
                    if(!NavMesh.SamplePosition(candidate,out var hit,2,NavMesh.AllAreas) || Mathf.Abs(hit.position.y-s.position.y)>1.5f){Debug.LogWarning("BATTLEFIELD_SPAWN_SKIPPED "+s.id);continue;}
                    var go=Instantiate(Settings.MinionPrefab,hit.position,Quaternion.Euler(0,s.team==0?0:180,0));
                    var e=go.GetComponent<BattlefieldEntity>();e.Health.Team=s.team;e.Health.MaxHealth=e.Health.CurrentHealth=Settings.MinionHealth;
                    e.Health.CharacterName=(s.team==0?"蓝队":"橙队")+"小兵"+(++sequence);e.SourceId=s.id;e.NextNode=s.startNode;e.Seed=Settings.MatchSeed^StableHash(s.id)^sequence;
                    NetworkServer.Spawn(go);made++;MinionsCreated++;
                }
                Debug.Log($"BATTLEFIELD_WAVE point={s.id} team={s.team} wave={(s.team==0?BlueWave:OrangeWave)} count={made} clock={MatchClock:F2} humanPlayers={GameManager.Players.Count}");
            }
            if(MatchClock>=nextStats){nextStats=MatchClock+.5f;BlueMinions=CountMinions(0);OrangeMinions=CountMinions(1);}
        }
        public int CountMinions(int team){int n=0;for(int i=0;i<Entities.Count;i++){var e=Entities[i];if(e&&e.Kind==BattlefieldKind.Minion&&e.Alive&&e.Health.Team==team)n++;}return n;}
        public bool PrerequisiteMet(BattlefieldEntity e){return string.IsNullOrEmpty(e.Prerequisite)||(Buildings.TryGetValue(e.Prerequisite,out var before)&&!before.Alive);}
        public BattlefieldEntity RouteBuilding(int attackingTeam)
        {
            BattlefieldEntity best=null;
            for(int i=0;i<Entities.Count;i++){var e=Entities[i];if(!e||!e.Alive||e.Health.Team==attackingTeam||e.Kind<BattlefieldKind.Tower||!PrerequisiteMet(e))continue;if(best==null||e.Kind<best.Kind)best=e;}
            return best;
        }
        void LateUpdate()
        {
            if(!isServer||Phase!=BattlefieldPhase.Playing)return;
            bool blue=false,orange=false;
            foreach(var pair in Buildings)if(pair.Value.Kind==BattlefieldKind.Base&&!pair.Value.Alive){if(pair.Value.Health.Team==0)blue=true;else orange=true;}
            if(!blue&&!orange)return;
            Winner=blue&&orange?2:(blue?1:0);Phase=BattlefieldPhase.Finished;State=GamemodeState.Finish;LetPlayersSpawnOnTheirOwn=false;BlockAllPlayers(true);
            GamemodeMessage(Winner==2?"双方基地同时摧毁：平局":(Winner==0?"蓝队":"橙队")+"获胜！敌方基地已摧毁",999);
            Debug.Log("BATTLEFIELD_RESULT winner="+Winner);
        }
        public void Register(BattlefieldEntity e){if(!Entities.Contains(e))Entities.Add(e);}
        public void Unregister(BattlefieldEntity e){Entities.Remove(e);}
        public Material GetTraceMaterial(){if(!TraceMaterial)TraceMaterial=new Material(Settings.TracerMaterial);return TraceMaterial;}
        public static int StableHash(string value){unchecked{int h=17;foreach(char c in value)h=h*31+c;return h;}}
        void OnDestroy(){if(TraceMaterial)Destroy(TraceMaterial);Entities.Clear();Buildings.Clear();Nodes.Clear();if(Instance==this)Instance=null;}
    }
}
