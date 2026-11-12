using System.Collections.Generic;
using Mirror;
using UnityEngine;
using MTPSKIT.RainImported;
namespace MTPSKIT.Gameplay
{
    // Host-owned training companions. Original combat models are reused where DLC visuals are absent.
    public sealed class RangeDrone : MonoBehaviour
    {
        static readonly List<RangeDrone> fleet=new List<RangeDrone>();
        DroneSpec spec;
        CharacterInstance body,owner;
        RainActorVisual visual;
        Health target;
        Health[] candidates;
        const int FleetCapacity = 21;
        const int BackupCount = 4;
        const float BackupLifetime = 25f;
        const int BackupBurstShots = 4;
        const float BackupShotInterval = .1f;
        float expiresAt, nextBackupShot, backupShotDamage;
        int backupShotsRemaining;
        Health backupBurstTarget;
        PlayerInstance backupPlayer;
        int backupLevelOffset;
        float BaseAttack => spec.damage + spec.damageGain * backupLevelOffset;
        float scanAt,ready,commandAt,beamUntil,repairAt,hasteUntil;
        int ammo=40;
        LineRenderer beam;
        Material beamMaterial;
        public string Id=>spec.id;
        internal CharacterInstance EquipmentSummoner => expiresAt > 0f ? owner : null;
        internal static DroneSpec FindSpec(string id)
        {
            foreach (var unit in DroneCatalog.Load().units)
                if (unit.id == id) return unit;
            return null;
        }

        internal static CharacterInstance PreferCloserAlly(Vector3 origin, CharacterInstance player)
        {
            if (!player) return null;
            CharacterInstance nearest = player;
            float distance = (player.transform.position - origin).sqrMagnitude;
            foreach (var drone in fleet)
            {
                if (!drone || !drone.body || drone.body.Team != player.Team || drone.body.CurrentHealth <= 0) continue;
                float candidate = (drone.body.transform.position - origin).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = drone.body;
                distance = candidate;
            }
            return nearest;
        }

        internal static void ConfigureReplica(CharacterInstance body)
        {
            var spec = FindSpec(body.RainDroneId);
            if (spec != null) ConfigureBody(body, spec);
        }

        static RainActorVisual ConfigureBody(CharacterInstance body, DroneSpec spec)
        {
            body.GetComponent<CharacterMotor>().enabled = false;
            body.GetComponent<CharacterController>().enabled = false;
            var model = ModelFor(spec.id);
            var visual = body.gameObject.AddComponent<RainActorVisual>();
            visual.Setup("mdl" + model, model == "Drone1" ? "Drone" : model, spec.id == "MegaDrone" ? 2.8f : 1.2f);
            body.ConfigureDroneHitbox(visual.GetModelBounds());
            return visual;
        }
        public static string ModelFor(string id)
        {
            switch(id)
            {
                case "Drone1":case "Drone2":case "Turret1":case "BackupDrone":case "EmergencyDrone":case "EquipmentDrone":case "FlameDrone":case "MissileDrone":case "MegaDrone":return id;
                case "DTHealDrone":case "RechargeDrone":return "Drone2";
                case "BomberDrone":case "BombardmentDrone":return "MissileDrone";
                case "DroneCommander":return "MegaDrone";
                case "HaulerDrone":case "DTHaulerDrone":case "JunkDrone":case "CleanupDrone":case "JailerDrone":return "EquipmentDrone";
                default:return "Drone1";
            }
        }
        internal static string Recruit(CharacterInstance owner,DroneSpec spec)
        {
            if(!NetworkServer.active||!owner||owner.CurrentHealth<=0)return "需要房主存活角色。";
            if(fleet.Count>=FleetCapacity)return "编队已满（21 架），请先拆卸。";
            Spawn(owner, spec, 0f);
            var model = ModelFor(spec.id);
            return "已部署："+spec.name+(model!=spec.id?"（复用 "+model+" 模型）":"")+"。F7 编队指令。";
        }

        internal static bool TryDeployBackup(CharacterInstance owner, PlayerInstance player)
        {
            if (!NetworkServer.active || !owner || !owner.isServer || owner.CurrentHealth <= 0 ||
                fleet.Count > FleetCapacity - BackupCount) return false;
            var spec = FindSpec("BackupDrone");
            if (spec == null) return false;
            float expiry = Time.time + BackupLifetime;
            int levelOffset = player ? player.RainLevel - 1 : 0;
            for (int i = 0; i < BackupCount; i++) Spawn(owner, spec, expiry, player, levelOffset);
            return true;
        }

        static void Spawn(CharacterInstance owner, DroneSpec spec, float expiry, PlayerInstance player = null, int levelOffset = 0)
        {
            var position=owner.transform.position+Vector3.up*3;
            if(spec.id=="Turret1")
            {
                position=owner.transform.position+owner.transform.right*3;
                if(Physics.Raycast(position+Vector3.up*4,Vector3.down,out var ground,12,GameManager.environmentLayer))position=ground.point;
            }
            var go=Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),position,Quaternion.identity);
            var body=go.GetComponent<CharacterInstance>();
            foreach(var slot in body.CharacterItemManager.Slots)slot.ItemOnSpawn=null;
            body.Team = owner.Team;
            body.MaxHealth = body.CurrentHealth = Mathf.CeilToInt(spec.hp + spec.hpGain * levelOffset);
            if (expiry > 0f) body.RainHealthRegen = 7.5f + 1.5f * levelOffset;
            body.RainDroneOwner=owner.netId;
            body.RainDroneId=spec.id;
            NetworkServer.Spawn(go);body.SetAsBOT(true);
            var ai=go.GetComponent<BotAI>();if(ai){ai.enabled=false;ai.StopAllCoroutines();}
            // Flight owns movement; the character remains a network damage receiver.
            var drone=go.AddComponent<RangeDrone>();drone.body=body;drone.owner=owner;drone.spec=spec;
            drone.expiresAt = expiry;
            drone.backupPlayer = player;
            drone.backupLevelOffset = levelOffset;
            drone.visual=ConfigureBody(body,spec);
            drone.beam=go.AddComponent<LineRenderer>();drone.beam.positionCount=2;drone.beam.startWidth=.045f;drone.beam.endWidth=.025f;
            drone.beamMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default"));
            drone.beamMaterial.color=Color.cyan;drone.beam.sharedMaterial=drone.beamMaterial;drone.beam.enabled=false;
            fleet.Add(drone);
        }
        public static string Recall(string id)
        {
            for(int i=fleet.Count-1;i>=0;i--)if(fleet[i]&&fleet[i].Id==id){NetworkServer.Destroy(fleet[i].gameObject);return "已拆卸该无人机。";}
            return "当前没有该无人机。";
        }
        public static void CommandAll(){for(int i=0;i<fleet.Count;i++)if(fleet[i])fleet[i].Command();}
        public static void ClearAll(){for(int i=fleet.Count-1;i>=0;i--)if(fleet[i])NetworkServer.Destroy(fleet[i].gameObject);}
        void Command(){if(body.CurrentHealth>0&&Time.time>=commandAt){commandAt=Time.time+8;Act(true);}}
        void Update()
        {
            if(!NetworkServer.active)return;
            if (!owner || (expiresAt > 0f && Time.time >= expiresAt))
            {
                NetworkServer.Destroy(gameObject);
                return;
            }
            if(beam)beam.enabled=Time.time<beamUntil;
            if(visual&&Time.time>=beamUntil)visual.SetState(0);
            if(body.CurrentHealth<=0)
            {
                if(!Id.StartsWith("DT")){NetworkServer.Destroy(gameObject);return;}
                if(repairAt==0)repairAt=Time.time+8;
                if(Time.time<repairAt)return;
                // Replace the dead health state and hitboxes using a fresh instance.
                var saved=spec;var player=owner;fleet.Remove(this);NetworkServer.Destroy(gameObject);Recruit(player,saved);return;
            }
            if (owner.CurrentHealth <= 0)
            {
                backupShotsRemaining = 0;
                backupBurstTarget = null;
                return;
            }
            if(Time.time>=scanAt)
            {
                UpdateBackupLevel();
                scanAt=Time.time+.5f;candidates=FindObjectsByType<Health>(FindObjectsSortMode.None);
                target=null;float best=40*40;
                foreach(var h in candidates)
                {
                    if(!h||h.CurrentHealth<=0||h.Team==body.Team)continue;
                    float distance=(h.transform.position-transform.position).sqrMagnitude;
                    if(distance<best&&!Physics.Linecast(transform.position,h.transform.position+Vector3.up,GameManager.environmentLayer)){best=distance;target=h;}
                }
            }
            int index=fleet.IndexOf(this);float angle=index*2.39996f;
            var desired=owner.transform.position+new Vector3(Mathf.Cos(angle)*3,2.5f+index%3*.5f,Mathf.Sin(angle)*3);
            if(Id=="Turret1")desired=transform.position;
            else if(Id=="FlameDrone"&&target)desired=target.transform.position+Vector3.up*2+(owner.transform.position-target.transform.position).normalized*3;
            var next=Vector3.MoveTowards(transform.position,desired,Mathf.Max(1,spec.speed)*Time.deltaTime);
            if(!Physics.Linecast(transform.position,next,GameManager.environmentLayer))transform.position=next;
            if(target){var direction=target.transform.position+Vector3.up-transform.position;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);}
            if (backupShotsRemaining > 0) FireBackupBurst();
            if(Time.time<ready)return;
            ready=Time.time+(Time.time<hasteUntil?.75f:1);Act(false);
        }
        void UpdateBackupLevel()
        {
            if (!backupPlayer) return;
            int levelOffset = backupPlayer.RainLevel - 1;
            if (levelOffset == backupLevelOffset) return;
            backupLevelOffset = levelOffset;
            int maximum = Mathf.CeilToInt(spec.hp + spec.hpGain * levelOffset);
            int increase = maximum - body.MaxHealth;
            body.RainHealthRegen = 7.5f + 1.5f * levelOffset;
            body.ServerSetMaximumHealth(maximum);
            if (increase > 0) body.ServerHeal(increase, body.netId, allowOverhealBarrier: false);
        }

        void FireBackupBurst()
        {
            if (Time.time < nextBackupShot) return;
            var victim = backupBurstTarget;
            if (!victim || victim.CurrentHealth <= 0 || victim.Team == body.Team ||
                Physics.Linecast(transform.position, victim.transform.position + Vector3.up, GameManager.environmentLayer))
            {
                backupShotsRemaining = 0;
                backupBurstTarget = null;
                return;
            }

            // Distribute integer damage across the burst while preserving its 200% total.
            int damage = Mathf.RoundToInt(backupShotDamage * backupShotsRemaining) -
                Mathf.RoundToInt(backupShotDamage * (backupShotsRemaining - 1));
            backupShotsRemaining--;
            if (backupShotsRemaining == 0) backupBurstTarget = null;
            nextBackupShot = Time.time + BackupShotInterval;
            Trace(victim.transform.position + Vector3.up, Color.yellow);
            victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, body, 0, 1f);
        }

        void Trace(Vector3 end,Color color){beamMaterial.color=color;beam.SetPosition(0,transform.position);beam.SetPosition(1,end);beamUntil=Time.time+.18f;beam.enabled=true;visual.SetState(2);}
        void Hit(Health h,float multiplier)
        {
            if(!h||h.CurrentHealth<=0)return;
            Trace(h.transform.position+Vector3.up,Color.yellow);
            var source = expiresAt > 0f ? body : owner;
            h.Server_ChangeHealthStateRaw(Mathf.CeilToInt(BaseAttack * multiplier), CharacterPart.body, AttackType.melee, source, 0);
        }
        void Heal(Health h,float amount){if(h&&h.CurrentHealth>0&&h.CurrentHealth<h.MaxHealth){h.ServerHeal(Mathf.CeilToInt(amount),body.netId);Trace(h.transform.position+Vector3.up,Color.green);}}
        void Area(float multiplier,float radius,bool root=false)
        {
            if(candidates==null)return;var center=target?target.transform.position:transform.position;
            foreach(var h in candidates)if(h&&h.CurrentHealth>0&&h.Team!=body.Team&&(h.transform.position-center).sqrMagnitude<radius*radius)
            {if(multiplier>0)Hit(h,multiplier);if(root)RangeDroneStatus.Control(h,2);}
        }
        void Act(bool command)
        {
            switch(Id)
            {
                case "BackupDrone" when expiresAt > 0f && !command:
                    if (!target || backupShotsRemaining > 0) break;
                    backupBurstTarget = target;
                    backupShotDamage = BaseAttack * .5f;
                    backupShotsRemaining = BackupBurstShots;
                    nextBackupShot = Time.time;
                    FireBackupBurst();
                    break;
                case "Drone1":case "BackupDrone":case "DTGunnerDrone":Hit(target,command?3.4f:2);break;
                case "Turret1":Hit(target,command?4.5f:.3f);ready=Time.time+.16f;if(--ammo<=0){ammo=40;ready=Time.time+2;}break;
                case "Drone2":case "DTHealDrone":
                    Health lowest=owner;foreach(var d in fleet)if(d&&d.body.CurrentHealth>0&&(float)d.body.CurrentHealth/d.body.MaxHealth<(float)lowest.CurrentHealth/lowest.MaxHealth)lowest=d.body;
                    Heal(lowest,spec.damage*2);if(command)foreach(var d in fleet)if(d)d.hasteUntil=Time.time+5;break;
                case "EmergencyDrone":Heal(owner,spec.damage*(command?2:.5f));int count=1;foreach(var d in fleet)if(d&&count++<8)Heal(d.body,spec.damage*(command?2:.5f));break;
                case "MissileDrone":Hit(target,command?8:4);ready=Time.time+3;break;
                case "FlameDrone":Hit(target,command?20:20f/3);break;
                case "MegaDrone":Hit(target,33);Area(8,8);ready=Time.time+4;break;
                case "DroneCommander":foreach(var d in fleet)if(d)d.hasteUntil=Time.time+3;Trace(owner.transform.position+Vector3.up,Color.blue);break;
                case "EquipmentDrone":Hit(target,4.5f);if(target)RangeDroneStatus.Control(target,1);ready=Time.time+4;break;
                case "HaulerDrone":case "DTHaulerDrone":Hit(target,command?3:2);if(target)RangeDroneStatus.Control(target,1.5f);ready=Time.time+3;break;
                case "CleanupDrone":Area(command?2:0,18,true);Heal(owner,spec.damage*.5f);break;
                case "JunkDrone":
                    if(command)Area(3,8,true);else RangeDroneStatus.Shield(owner,spec.damage*2);ready=Time.time+12;break;
                case "BombardmentDrone":Area(2.5f,command?20:10,true);ready=Time.time+2;break;
                case "CopycatDrone":
                    int remaining=7;if(candidates!=null)foreach(var h in candidates)if(h&&h.CurrentHealth>0&&h.Team!=body.Team&&(h.transform.position-transform.position).sqrMagnitude<1225&&remaining-->0)
                    {Hit(h,2);RangeDroneStatus.Control(h,1);if(h.CurrentHealth>0&&h.CurrentHealth<h.MaxHealth*.3f)Hit(h,h.CurrentHealth/Mathf.Max(1,spec.damage)+1);}ready=Time.time+3;break;
                case "RechargeDrone":RangeDroneStatus.Shield(owner,8*spec.damage);Trace(owner.transform.position+Vector3.up,Color.cyan);ready=Time.time+4;break;
                case "BomberDrone":Area(2.4f,9,true);ready=Time.time+10;break;
                case "JailerDrone":Area(command?3:0,10);if(target)RangeDroneStatus.Control(target,5);ready=Time.time+7;break;
                default:Debug.LogError("Missing training drone action: "+Id);break;
            }
        }
        void OnDestroy(){fleet.Remove(this);if(beamMaterial)Destroy(beamMaterial);}
    }
}
