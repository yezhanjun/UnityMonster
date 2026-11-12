using System;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.AI;
namespace MTPSKIT.Battlefield
{
    public class BattlefieldEntity : NetworkBehaviour
    {
        [SyncVar] public BattlefieldKind Kind;
        [SyncVar] public string SourceId,Prerequisite;
        [SyncVar(hook=nameof(BuildingStateChanged))] public BattlefieldBuildingState BuildingState;
        [SyncVar] float turretYaw;
        [SyncVar] public BattlefieldBrainState BrainState;
        public float Range;
        public string NextNode;
        public int Seed;
        public Transform VisualRoot,Turret;
        public Health Health {get;private set;}
        public bool Alive=>Health&&Health.CurrentHealth>0;
        public string Label {get;private set;}
        public Vector3 LabelPosition=>transform.position+Vector3.up*labelHeight;
        float labelHeight=2.5f;
        BattlefieldGamemode mode;CharacterInstance character;NavMeshAgent agent;BattlefieldEntity target,pending;
        System.Random random;float nextThink,nextAttack,hitAt=-1,recoverUntil,deathAt=-1,labelAt,traceUntil,lastMotionTime,chaseUntil,guardUntil;Vector3 lastMotion,routeAnchor,destination;
        bool hasDestination,replanned;LineRenderer trace;string currentNode;
        void Awake()
        {
            Health=GetComponent<Health>();character=GetComponent<CharacterInstance>();agent=GetComponent<NavMeshAgent>();
            if(Kind>=BattlefieldKind.Tower&&VisualRoot)
                foreach(var renderer in VisualRoot.GetComponentsInChildren<Renderer>())labelHeight=Mathf.Max(labelHeight,renderer.bounds.max.y-transform.position.y+.6f);
        }
        public override void OnStartServer()
        {
            mode=BattlefieldGamemode.Instance;mode.Register(this);random=new System.Random(Seed);
            Health.ServerDamageAllowed=AllowDamage;Health.Server_OnHealthDepleted+=Died;
            if(Kind==BattlefieldKind.Minion)
            {
                character.SetAsBOT(true);var old=GetComponent<BotAI>();old.StopAllCoroutines();old.enabled=false;
                GetComponent<CharacterMotor>().enabled=false;
                agent.enabled=true;agent.speed=mode.Settings.MinionSpeed;agent.angularSpeed=360;agent.acceleration=24;agent.stoppingDistance=.45f;
                currentNode=NextNode;BrainState=BattlefieldBrainState.Move;lastMotion=transform.position;lastMotionTime=Time.time;
            }
        }
        public override void OnStartClient(){if(!mode)mode=BattlefieldGamemode.Instance;if(mode)mode.Register(this);if(BuildingState==BattlefieldBuildingState.Destroyed)DisableBuilding();}
        void BuildingStateChanged(BattlefieldBuildingState before,BattlefieldBuildingState after){if(after==BattlefieldBuildingState.Destroyed)DisableBuilding();}
        public override void OnStopServer(){Release();}
        public override void OnStopClient(){Release();}
        void Release(){if(mode)mode.Unregister(this);if(Health){Health.Server_OnHealthDepleted-=Died;Health.ServerDamageAllowed=null;}target=null;pending=null;if(agent&&agent.enabled)agent.enabled=false;}
        bool AllowDamage(Health attacker)
        {
            if(!mode||mode.Phase!=BattlefieldPhase.Playing||!attacker)return false;
            if(attacker==Health && Kind==BattlefieldKind.Player)return true;
            if(attacker.Team==Health.Team)return false;
            if(Kind>=BattlefieldKind.Tower && (!mode.PrerequisiteMet(this)||(Kind==BattlefieldKind.Tower&&BuildingState!=BattlefieldBuildingState.Active)))return false;
            return true;
        }
        void Died(CharacterPart part,AttackType type,Health killer,int force)
        {
            pending=null;target=null;hitAt=-1;BrainState=BattlefieldBrainState.Dead;deathAt=Time.time+5;mode.Deaths++;
            if(agent&&agent.enabled)agent.enabled=false;
            if(character){character.SetMovementInput(Vector2.zero);character.BlockCharacter(true);}
            if(Kind>=BattlefieldKind.Tower){BuildingState=BattlefieldBuildingState.Destroyed;DisableBuilding();RpcDestroyed();Debug.Log("BATTLEFIELD_BUILDING_DESTROYED "+SourceId);}
        }
        [ClientRpc] void RpcDestroyed() { DisableBuilding(); }
        void DisableBuilding()
        {
            if(VisualRoot)VisualRoot.gameObject.SetActive(false);
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
            var obstacle=GetComponent<NavMeshObstacle>();if(obstacle)obstacle.enabled=false;
        }
        void Update()
        {
            if(!mode)mode=BattlefieldGamemode.Instance;
            if(!isServer&&Turret)Turret.rotation=Quaternion.RotateTowards(Turret.rotation,Quaternion.Euler(0,turretYaw,0),180*Time.deltaTime);
            if(trace&&Time.time>=traceUntil)trace.enabled=false;
            if(Time.unscaledTime>=labelAt){labelAt=Time.unscaledTime+.25f;Label=(Health.Team==0?"蓝":"橙")+" · "+(Kind==BattlefieldKind.Minion?"小兵":Kind==BattlefieldKind.Tower?"防御塔":Kind==BattlefieldKind.Base?"基地":"玩家")+"  "+Health.CurrentHealth+" / "+Health.MaxHealth;}
            if(!isServer||!mode)return;
            if(!Alive){if(Kind==BattlefieldKind.Minion&&Time.time>=deathAt)NetworkServer.Destroy(gameObject);return;}
            if(mode.Phase!=BattlefieldPhase.Playing){if(agent&&agent.enabled)agent.isStopped=true;if(character&&Kind==BattlefieldKind.Minion)character.SetMovementInput(Vector2.zero);pending=null;target=null;hitAt=-1;if(mode.Phase==BattlefieldPhase.Finished)BrainState=BattlefieldBrainState.Finished;return;}
            if(Kind==BattlefieldKind.Player||Kind==BattlefieldKind.Base)return;
            if(Kind==BattlefieldKind.Tower){BuildingTick();if(Turret)turretYaw=Turret.eulerAngles.y;}else MinionTick();
        }
        bool Visible(BattlefieldEntity e)
        {
            return e&&e.Alive&&!Physics.Linecast(Health.GetPositionToAttack(),e.Health.GetPositionToAttack(),1,QueryTriggerInteraction.Ignore);
        }
        bool TargetValid(BattlefieldEntity e,float range)
        {
            return e&&e.Alive&&e.Health.Team!=Health.Team&&(e.transform.position-transform.position).sqrMagnitude<=range*range&&Visible(e);
        }
        BattlefieldEntity Acquire(float range,bool tower)
        {
            BattlefieldEntity best=null;int priority=int.MaxValue;float distance=float.MaxValue;
            for(int i=0;i<mode.Entities.Count;i++)
            {
                var e=mode.Entities[i];if(!e||e==this||e.Health.Team==Health.Team||!e.Alive)continue;
                if(tower&&e.Kind>=BattlefieldKind.Tower)continue;
                if(e.Kind>=BattlefieldKind.Tower)continue; // Buildings are selected by explicit route dependencies, never nearest tower.
                float d=(e.transform.position-transform.position).sqrMagnitude;if(d>range*range||!Visible(e))continue;
                int p=e.Kind==BattlefieldKind.Minion?0:1;
                if(p<priority||(p==priority&&(d<distance||(Mathf.Approximately(d,distance)&&best&&e.netId<best.netId)))){priority=p;distance=d;best=e;}
            }
            return best;
        }
        void BuildingTick()
        {
            if(!mode.PrerequisiteMet(this)){BuildingState=BattlefieldBuildingState.Locked;target=null;pending=null;hitAt=-1;return;}
            if(Time.time>=nextThink)
            {
                nextThink=Time.time+.5f;if(!TargetValid(target,Range+2))target=Acquire(Range,true);
                if(target)guardUntil=Time.time+2;
                BuildingState=Time.time<guardUntil?BattlefieldBuildingState.Active:BattlefieldBuildingState.Guarded;
            }
            AttackTick(Range,mode.Settings.TowerAttackPeriod,true);
        }
        void MinionTick()
        {
            if(BrainState==BattlefieldBrainState.Blocked)return;
            if(!agent.enabled||!agent.isOnNavMesh){BrainState=BattlefieldBrainState.Blocked;return;}
            if(Time.time>=nextThink)
            {
                nextThink=Time.time+.5f;
                if(!TargetValid(target,mode.Settings.MinionRange+2))target=null;
                if(!target&&BrainState!=BattlefieldBrainState.Return)
                {
                    target=Acquire(mode.Settings.MinionRange+5,false);
                    if(target){routeAnchor=transform.position;chaseUntil=Time.time+5;}
                }
                if(!target)
                {
                    var building=mode.RouteBuilding(Health.Team);
                    if(building&&TargetValid(building,mode.Settings.MinionRange))target=building;
                }
                if(target && target.Kind<BattlefieldKind.Tower && (Time.time>chaseUntil || (transform.position-routeAnchor).sqrMagnitude>64))
                {target=null;pending=null;hitAt=-1;BrainState=BattlefieldBrainState.Return;}
            }
            if(target)
            {
                float d=(target.transform.position-transform.position).magnitude;
                if(d<=mode.Settings.MinionRange && Visible(target)){agent.isStopped=true;lastMotionTime=Time.time;lastMotion=transform.position;replanned=false;character.SetMovementInput(Vector2.zero);AttackTick(mode.Settings.MinionRange,mode.Settings.MinionAttackPeriod,false);}
                else{BrainState=BattlefieldBrainState.Chase;Travel(target.transform.position);}
            }
            else
            {
                pending=null;hitAt=-1;
                if(BrainState==BattlefieldBrainState.Return)
                {
                    Travel(routeAnchor);if(Vector3.Distance(transform.position,routeAnchor)<1.2f)BrainState=BattlefieldBrainState.Move;
                }
                else FollowRoute();
            }
            if(agent.enabled&&!agent.isStopped)
            {
                Vector3 v=agent.velocity;v.y=0;character.SetMovementInput(transform.InverseTransformDirection(v).x/Mathf.Max(.1f,agent.speed),transform.InverseTransformDirection(v).z/Mathf.Max(.1f,agent.speed));
                if(v.sqrMagnitude>.02f)character.lookInput.y=Quaternion.LookRotation(v).eulerAngles.y;
                if((transform.position-lastMotion).sqrMagnitude>.09f){lastMotion=transform.position;lastMotionTime=Time.time;replanned=false;}
                else if(Time.time-lastMotionTime>10){agent.isStopped=true;BrainState=BattlefieldBrainState.Blocked;Debug.LogWarning("BATTLEFIELD_MINION_BLOCKED node="+currentNode+" id="+netId);}
                else if(Time.time-lastMotionTime>3&&!replanned){agent.SetDestination(destination);replanned=true;}
            }
        }
        void FollowRoute()
        {
            BrainState=BattlefieldBrainState.Move;
            if(!string.IsNullOrEmpty(currentNode)&&mode.Nodes.TryGetValue(currentNode,out var node))
            {
                var delta=node.position-transform.position;
                if(new Vector2(delta.x,delta.z).sqrMagnitude<=1 && Mathf.Abs(delta.y)<=1 && !agent.pathPending && (!agent.hasPath||agent.remainingDistance<=1.2f))
                {
                    bool a=!string.IsNullOrEmpty(node.next1),b=!string.IsNullOrEmpty(node.next2);
                    if(a&&!b)currentNode=node.next1;else if(b&&!a)currentNode=node.next2;
                    else if(a&&b){float sum=node.weight1+node.weight2;currentNode=random.NextDouble()<(sum>0?node.weight1/sum:.5f)?node.next1:node.next2;}
                    else currentNode=null;
                    NextNode=currentNode;hasDestination=false;
                }
                else Travel(node.position);
            }
            else
            {
                var b=mode.RouteBuilding(Health.Team);if(!b){agent.isStopped=true;return;}
                Vector3 toward=transform.position-b.transform.position;toward.y=0;
                Vector3 attackPosition=b.transform.position+toward.normalized*(mode.Settings.MinionRange*.7f);
                if(NavMesh.SamplePosition(attackPosition,out var hit,3,NavMesh.AllAreas))Travel(hit.position);
            }
        }
        void Travel(Vector3 position)
        {
            agent.isStopped=false;
            if(!hasDestination||(position-destination).sqrMagnitude>1||agent.pathStatus==NavMeshPathStatus.PathInvalid)
            {destination=position;agent.SetDestination(position);hasDestination=true;}
        }
        void AttackTick(float range,float period,bool tower)
        {
            if(hitAt>=0)
            {
                if(Time.time<hitAt)return;
                var victim=pending;pending=null;hitAt=-1;
                if(TargetValid(victim,range)&&victim.AllowDamage(Health))
                {
                    int amount=tower?(victim.Kind==BattlefieldKind.Player?mode.Settings.TowerPlayerDamage:mode.Settings.TowerMinionDamage):mode.Settings.MinionDamage;
                    victim.Health.Server_ChangeHealthStateRaw(amount,CharacterPart.body,AttackType.hitscan,Health,0);
                    RpcAttack(victim.Health.GetPositionToAttack());if(tower)mode.TowerShots++;else mode.MinionShots++;
                }
                recoverUntil=nextAttack;BrainState=BattlefieldBrainState.Recover;return;
            }
            if(!TargetValid(target,range)||Time.time<nextAttack)return;
            Vector3 delta=target.Health.GetPositionToAttack()-Health.GetPositionToAttack();
            float desired=Quaternion.LookRotation(delta).eulerAngles.y;
            if(character)character.lookInput.y=Mathf.MoveTowardsAngle(character.lookInput.y,desired,360*Time.deltaTime);
            if(Turret)Turret.rotation=Quaternion.RotateTowards(Turret.rotation,Quaternion.Euler(0,desired,0),180*Time.deltaTime);
            float current=Turret?Turret.eulerAngles.y:character?character.lookInput.y:desired;
            if(Mathf.Abs(Mathf.DeltaAngle(current,desired))>10)return;
            pending=target;hitAt=Time.time+.2f;nextAttack=Time.time+period;BrainState=BattlefieldBrainState.Windup;
        }
        [ClientRpc] void RpcAttack(Vector3 end)
        {
            if(!trace)
            {
                trace=gameObject.AddComponent<LineRenderer>();trace.positionCount=2;trace.startWidth=.045f;trace.endWidth=.018f;
                trace.sharedMaterial=mode.GetTraceMaterial();trace.startColor=trace.endColor=Health.Team==0?Color.cyan:new Color(1,.4f,.1f);
            }
            trace.enabled=true;trace.SetPosition(0,Health.GetPositionToAttack());trace.SetPosition(1,end);traceUntil=Time.time+.1f;
            if(character){character.CharacterAnimator.DoRecoil(.7f);character.CharacterEvent_OnItemUsed?.Invoke(.25f);}
        }
    }
}
