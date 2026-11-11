using System;
using System.Collections.Generic;
using UnityEngine;
namespace MTPSKIT.RainImported{
    [Serializable] internal sealed class DroneSpec{
        public string id,name,skill,command,source,bodyToken;
        public float hp,damage,speed,hpGain,damageGain; public int tier; public bool canDrop,canCombine,canScrap;
    }
    [Serializable] internal sealed class DroneCatalog{
        public DroneSpec[] units;
        static DroneCatalog cached;
        internal static DroneCatalog Load()=>cached??(cached=JsonUtility.FromJson<DroneCatalog>(Resources.Load<TextAsset>("RainImported/DroneFleet").text));
    }
    internal sealed partial class WeaponBuild{
        internal sealed class Companion{
            internal DroneSpec def; internal float x,hp,max,ready,commandReady,repairAt,expires;internal int attacks,ammo=40;
            internal string gear;internal float gearCD,creditedDamage; internal int creditedKills,commands,repairs;
            internal bool Down=>hp<=0;
        }
        internal readonly List<Companion> fleet=new List<Companion>();
        internal float fleetHaste,medAura;float junkEnd,bombReady;
        int junkStacks;
        internal Companion Recruit(DroneSpec def,float lifetime=0){
            if(fleet.Count>=32)return null;
            var c=new Companion{def=def,x=x,hp=def.hp+def.hpGain*(level-1),max=def.hp+def.hpGain*(level-1),expires=lifetime>0?time+lifetime:0,gear=equipment,gearCD=equipBase};
            fleet.Add(c);return c;
        }
                readonly List<DroneRunStat> departed=new List<DroneRunStat>();
        static DroneRunStat Summary(Companion c,string state)=>new DroneRunStat{id=c.def.id,name=c.def.name,state=state,hp=c.hp,
            damage=c.creditedDamage,kills=c.creditedKills,commands=c.commands,repairs=c.repairs};
        internal void RecordFleet(RunLedger report){
            report.drones.AddRange(departed);
            foreach(var c in fleet)report.drones.Add(Summary(c,c.Down?"损毁":"在役"));
        }
        internal void Recall(string id){int i=fleet.FindLastIndex(c=>c.def.id==id&&c.expires==0);if(i>=0){departed.Add(Summary(fleet[i],"撤编"));fleet.RemoveAt(i);}}
        internal void RepairFleet(){
            foreach(var c in fleet)if(c.Down && c.expires==0 && gold>=12){gold-=12;Restore(c);last="维修："+c.def.name;}
        }
        void Restore(Companion c){c.repairs++;c.hp=c.max;c.x=x;c.ready=time+1;c.ammo=40;}
        void CopyFleet(WeaponBuild fresh){
            foreach(var c in fleet)if(c.expires==0){var n=fresh.Recruit(c.def);n.gear=c.gear;n.gearCD=c.gearCD;}
        }
        void FleetStage(){foreach(var c in fleet)c.x=x;for(int i=fleet.Count-1;i>=0;i--)if(fleet[i].expires>0){departed.Add(Summary(fleet[i],"跨关离队"));fleet.RemoveAt(i);}}
        internal void SummonBackup(){
            var def=Array.Find(DroneCatalog.Load().units,s=>s.id=="BackupDrone");
            for(int i=0;i<4;i++)Recruit(def,25);
        }
        void FleetStep(float dt){
            if(junkStacks>0&&time>=junkEnd)junkStacks=0;
            for(int i=fleet.Count-1;i>=0;i--){
                var c=fleet[i];if(c.expires>0&&time>=c.expires){departed.Add(Summary(c,"支援到期"));fleet.RemoveAt(i);continue;}
                c.max=c.def.hp+c.def.hpGain*(level-1);
                if(c.Down){if(c.def.id.StartsWith("DT",StringComparison.Ordinal)&&time>=c.repairAt)Restore(c);continue;}
                var target=Near(c.x);
                bool follow=Mathf.Abs(c.x-x)>75;
                bool stay=c.def.id=="Turret1";float desired=x+2;
                if(!follow&&target!=null&&Mathf.Abs(target.x-x)<40){
                    float range=c.def.id=="FlameDrone"?4:12;
                    desired=target.x+Mathf.Sign(c.x-target.x)*range;
                }
                if(!stay)c.x=Mathf.MoveTowards(c.x,desired,dt*c.def.speed*(time<fleetHaste?1.25f:1));
                if(follow||time<c.ready)continue;
                c.ready=time+1/(time<fleetHaste?1.25f:1);
                DroneAction(c,target,false);
            }
        }
        bool FleetThreat(Target attacker,float amount){
            Companion closest=null;float best=Mathf.Abs(attacker.x-x);
            foreach(var c in fleet)if(!c.Down&&Mathf.Abs(attacker.x-c.x)<=best){best=Mathf.Abs(attacker.x-c.x);closest=c;}
            if(closest==null){Hurt(amount);return true;}
            closest.hp=Mathf.Max(0,closest.hp-amount);if(closest.Down){closest.repairAt=time+8;last=closest.def.name+" 被击落";}return false;
        }
        internal void CommandFleet(){
            foreach(var c in fleet.ToArray())if(!c.Down&&time>=c.commandReady){
                c.commands++;c.commandReady=time+8;float normalReady=c.ready;DroneAction(c,Near(c.x),true);c.ready=normalReady;
            }
        }
        void DroneHeal(float value,bool group){
            if(group){Heal(value);int healed=1;foreach(var c in fleet)if(healed<8&&!c.Down&&c.hp<c.max){c.hp=Mathf.Min(c.max,c.hp+value);healed++;}return;}
            Companion lowest=null;float ratio=hp/Life;foreach(var c in fleet)if(!c.Down&&c.hp/c.max<ratio){ratio=c.hp/c.max;lowest=c;}
            if(lowest==null)Heal(value);else lowest.hp=Mathf.Min(lowest.max,lowest.hp+value);
        }
        void DroneDamage(Companion owner,Target t,float damage){
            if(t==null||!t.Alive)return;float before=t.hp;Deal(t,damage);float actual=before-Mathf.Max(0,t.hp);owner.creditedDamage+=actual;minionDamage+=actual;if(!t.Alive){minionKills++;owner.creditedKills++;}
        }
        void DroneAction(Companion c,Target t,bool cmd){
            var id=c.def.id;float d=c.def.damage+c.def.damageGain*(level-1);c.attacks++;
            if(cmd)last=c.def.name+"：指令";
            void Hit(float k){if(t!=null&&Mathf.Abs(t.x-c.x)<=40)DroneDamage(c,t,d*k);}
            void Splash(float k,float radius){if(t!=null)foreach(var e in targets)if(e.Alive&&Mathf.Abs(e.x-t.x)<=radius)DroneDamage(c,e,d*k);}
            switch(id){
                case "Drone1":case "BackupDrone":case "DTGunnerDrone":
                    for(int n=0;n<4;n++)Hit(cmd?.85f:.5f);
                    if(cmd&&id!="Drone1"&&t!=null)foreach(var e in targets)if(e!=t&&e.Alive&&Mathf.Abs(e.x-t.x)<10){DroneDamage(c,e,.5f*d);break;}
                    break;
                case "Turret1":
                    if(cmd){Hit(4.5f);break;}c.ready=time+.16f;Hit(.3f);if(--c.ammo==0){c.ammo=40;c.ready=time+2;}break;
                case "Drone2":case "DTHealDrone":DroneHeal(2*d,false);if(cmd){fleetHaste=time+5;if(id=="DTHealDrone")barrier=Mathf.Min(Max,barrier+d);}break;
                case "EmergencyDrone":DroneHeal(cmd?2*d:.5f*d,true);if(cmd)medAura=time+5;break;
                case "MissileDrone":for(int n=0;n<4;n++)Hit(cmd?2:1);c.ready=time+3;break;
                case "FlameDrone":if(t!=null&&Mathf.Abs(c.x-t.x)<=12){Hit(cmd?20:20f/3);t.burn=time+3;}break;
                case "MegaDrone":for(int n=0;n<(cmd?6:15);n++)Hit(cmd?1:2.2f);Splash(cmd?3:8,8);c.ready=time+4;break;
                case "DroneCommander":fleetHaste=time+3;if(cmd)rageUntil=time+7;break;
                case "EquipmentDrone":
                    if(cmd){Hit(4.5f);if(t!=null)t.stun=time+1;}
                    else if(!string.IsNullOrEmpty(c.gear)){
                        string old=equipment;int charges=equipmentCharges;float cd=equipBase,re=equipRecharge;
                        try{equipment=c.gear;equipmentCharges=1;equipBase=c.gearCD;UseEquipment();}
                        finally{equipment=old;equipmentCharges=charges;equipBase=cd;equipRecharge=re;}
                        c.ready=time+Mathf.Max(.5f,c.gearCD*.2f);
                    }break;
                case "HaulerDrone":case "DTHaulerDrone":
                    if(t!=null){t.stun=time+1.5f;t.x+=4;Hit(cmd?3:2);if(cmd)Splash(1,6);}c.ready=time+3;break;
                case "CleanupDrone":foreach(var e in targets)if(e.Alive&&Mathf.Abs(e.x-c.x)<=18){e.slow=time+2;if(cmd){e.x=Mathf.MoveTowards(e.x,c.x,4);DroneDamage(c,e,2*d);}}break;
                case "JunkDrone":
                    if(cmd){Splash(3,8);if(t!=null)t.stun=time+1;}else{
                        junkStacks=1+level/3;junkEnd=time+8;}
                    c.ready=time+12;break;
                case "BombardmentDrone":Splash(2.5f,cmd?20:10);if(t!=null)t.stun=time+2;c.ready=time+2;break;
                case "CopycatDrone":
                    int remaining=cmd?3:7;foreach(var e in targets)if(e.Alive&&Mathf.Abs(e.x-c.x)<35&&remaining-->0){
                        e.stun=time+1;DroneDamage(c,e,d*(cmd?1:2));if(e.Alive&&e.hp<e.max*.3f)DroneDamage(c,e,e.hp);}
                    c.ready=time+3;break;
                case "RechargeDrone":barrier=Mathf.Min(Max,barrier+8*d);
                    if(cmd){secondary=1+N("SecondarySkillMagazine");utility=1+2*N("UtilitySkillMagazine");specialAt=time;}c.ready=time+4;break;
                case "BomberDrone":if(cmd||time>=bombReady){Splash(2.4f,9);if(t!=null)t.stun=time+1;bombReady=time+10;}break;
                case "JailerDrone":if(t!=null)foreach(var e in targets)if(e.Alive&&Mathf.Abs(t.x-e.x)<10){if(cmd){e.x=Mathf.MoveTowards(e.x,t.x,5);DroneDamage(c,e,3*d);}e.rootUntil=time+5;}c.ready=time+7;break;
                default:throw new InvalidOperationException("无人机无行为："+id);
            }
        }
        void SalvageDeath(Target t){
            foreach(var c in fleet)if(!c.Down&&c.def.id=="CleanupDrone"&&Mathf.Abs(c.x-t.x)<18){
                if(Roll(.5f)){gold+=12;earnedGold+=12;}else Heal(Max*.15f);break;
            }
        }
    }
}



