using System;
using UnityEngine;
namespace MTPSKIT.Battlefield
{
    public enum BattlefieldKind { Player, Minion, Tower, Base }
    public enum BattlefieldPhase { Preparing, Playing, Finished }
    public enum BattlefieldBuildingState { Locked, Guarded, Active, Destroyed }
    public enum BattlefieldBrainState { Move, Chase, Windup, Recover, Return, Dead, Finished, Blocked }
    [Serializable] public class BattlefieldNode { public string id,next1,next2; public Vector3 position; public float weight1,weight2; }
    [Serializable] public class BattlefieldSpawn { public string id,startNode; public int team,count; public Vector3 position; public float period; }
    [Serializable] public class BattlefieldBuilding { public string id,prerequisite; public int team,kind,sourceHealth; public Vector3 position; public float yaw,range; }
    [Serializable] public class BattlefieldPlayerSpawn { public string id;public int team;public Vector3 position;public float yaw; }
    [Serializable] public class BattlefieldMapData { public string mapId;public BattlefieldNode[] nodes;public BattlefieldSpawn[] spawns;public BattlefieldBuilding[] buildings;public BattlefieldPlayerSpawn[] playerSpawns; }
    [CreateAssetMenu(menuName="TPSBattle/Battlefield Settings")]
    public class BattlefieldSettings : ScriptableObject
    {
        public int PlayerHealth=500,MinionHealth=100,TowerHealth=600,BaseHealth=1200;
        public int MinionDamage=8,TowerPlayerDamage=30,TowerMinionDamage=25,CapPerTeam=24;
        public float MinionSpeed=5,MinionRange=20,MinionAttackPeriod=1.5f,TowerAttackPeriod=1.5f,PreparingSeconds=3;
        public int MatchSeed=20261104;
        [Header("头顶血条")]
        public float MinionHealthBarDistance=70,BuildingHealthBarDistance=180;
        public bool HealthBarsRespectWalls=true;
        public Font UIFont;
        public Material TracerMaterial;
        public GameObject PlayerPrefab,MinionPrefab,TowerPrefab,BasePrefab;
        public TextAsset MapJson;
    }
}
