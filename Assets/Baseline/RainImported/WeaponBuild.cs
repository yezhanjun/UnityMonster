using System;
using System.Collections.Generic;
using UnityEngine;
namespace MTPSKIT.RainImported {
    // Fixed-step, one-dimensional range. Distances are metres, clocks are simulation seconds.
    internal sealed partial class WeaponBuild {
        internal sealed class Target {
            internal readonly List<float> bleeds=new List<float>();
            internal float rootUntil; internal float x,hp,max,bleed,burn,wound,slow,stun,shred,mark,rupture,ruptureEnd;
            internal int hits; internal bool elite,boss;
            internal bool Alive=>hp>0;
        }
        readonly Dictionary<string,int> bag=new Dictionary<string,int>();
        readonly System.Random rng;
        internal readonly List<Target> targets=new List<Target>();
        internal string equipment="",last=""; internal int kills,gold,level=1,stage=1,earnedGold,minionKills;
        internal float peakHit,received; internal string killer=""; internal float roleHP=110,roleGain=33,incomingScale=1;
        internal float time,hp,shield,barrier,x,distance,charge,damageDone,minionDamage;
        internal bool sprint,zone,finished; internal int equipmentCharges=1,secondary=1,utility=1;
        float equipRecharge,equipBase,shotAt,heavyAt,specialAt,utilityAt,secondRecharge,jumpAt;
        float hurtAt=-100,combatAt=-100,movedAt=-100,secondTick,threatAt=3,healAt,healQueue,soul;
        float infusion,critBuff,critUntil,bannerX,bannerUntil,rageUntil,freeUntil,eliteUntil,critEquip,tonic,armorEquip,leech,hideUntil,hideReady,novaReady,ringFire,ringIce;
        float turret,ghost,drone,sprintShot,tesla,zoneNova,nextBeetle,nextOrb,icicleUntil,plantStart=-100,flight,dash,tunnel,scan,meteor,effigy,blackhole,doll,fireUntil,sawUntil,goldFire,woodUntil,disc,fallReady,airUntil,fieldX=5;
        float tonicAt;int affliction,beetles,probes,effigies,jumps;bool titan,keyRoom,cache;
        readonly Queue<float> recentKills=new Queue<float>();
        internal int N(string id)=>(bag.TryGetValue(id,out int n)?n:0)+(id=="Syringe"?junkStacks:0);
        internal float Max=>Mathf.Max(1,(roleHP+roleGain*(level-1)+25*N("FlatHealth")+40*N("Knurl")+infusion)*(1+.1f*(N("Pearl")+N("ShinyPearl")))*(time<tonic?1.5f:1)*Mathf.Pow(.5f,N("LunarDagger"))*Penalty);
        float Penalty=>Mathf.Max(.1f,1-.05f*affliction);
        float Power=>(12+2.4f*(level-1))*(1+N("LunarDagger"))*(1+.1f*N("ShinyPearl"))*(time<tonic?2:1)*Penalty;
        float Speed=>(1+.14f*N("Hoof")+(sprint?.25f*N("SprintBonus"):0)+(time<fleetHaste?.25f:0)+(time-combatAt>5?.3f*N("SprintOutOfCombat"):0)+(time<rageUntil?.5f:0)+(time<hideUntil?.4f:0)+(time<flight?.2f:0)+Banner*.3f+(time<tonic?.3f:0))*(1+.1f*N("ShinyPearl"))*Penalty;
        float Banner=>time<bannerUntil && Mathf.Abs(x-bannerX)<16+8*(N("WardOnLevel")-1)?1:0;
        float Rate=>(1+(time<fleetHaste?.25f:0)+.15f*N("Syringe")+(time<critUntil?critBuff:0)+(time<rageUntil?1:0)+(time<tonic?.7f:0)+(time<energized?.7f:0)+Banner*.3f)*(1+.1f*N("ShinyPearl"))*Penalty;
        float energized,vortexEnd,vortexX,vortexDps,plantX;readonly Queue<float> iceKills=new Queue<float>();
        float MaxShield=>Max*(.08f*N("PersonalShield")+(N("ShieldOnly")>0?1.5f+.25f*(N("ShieldOnly")-1):0));
        float Life=>N("ShieldOnly")>0?1:Max;
                internal WeaponBuild Deploy(float roleBaseHP,float gain,float difficulty){
            var fresh=new WeaponBuild(){roleHP=roleBaseHP,roleGain=gain,incomingScale=difficulty,weapon=weapon};
            foreach(var pair in bag)if(pair.Value>0)fresh.Add(pair.Key,pair.Value);
            if(equipment!="")fresh.Equip(equipment,equipBase);
            CopyFleet(fresh);fresh.hp=fresh.Life;return fresh;
        }
        internal IEnumerable<KeyValuePair<string,int>> Inventory=>bag;
        internal WeaponPattern weapon=WeaponPatterns.Load().weapons[0];
        internal WeaponBuild(int seed=1){rng=new System.Random(seed);hp=Max;keyRoom=true;SpawnWave();}
        internal void Add(string id,int delta=1){
            float before=Life;bag.TryGetValue(id,out int old);bag[id]=Mathf.Clamp(old+delta,0,99);
            hp=Mathf.Clamp(hp+Mathf.Max(0,Life-before),0,Life);shield=Mathf.Min(shield,MaxShield);
            if(id=="PersonalShield"||id=="ShieldOnly")shield=MaxShield;
            if(id=="SecondarySkillMagazine")secondary=Mathf.Clamp(secondary+delta,0,1+N(id));
            if(id=="UtilitySkillMagazine")utility=Mathf.Clamp(utility+2*delta,0,1+2*N(id));
            if(id=="EquipmentMagazine")equipmentCharges=Mathf.Clamp(equipmentCharges+delta,0,1+N(id));
            if(id=="TreasureCache"&&delta>0)cache=true;
        }
        internal void Equip(string id,float cooldown){equipment=id;equipBase=cooldown;equipmentCharges=1+N("EquipmentMagazine");equipRecharge=0;}
        bool Roll(float chance,bool luck=true){
            int l=luck?N("Clover")-N("LunarBadLuck"):0;
            bool ok=rng.NextDouble()<chance;
            for(int i=0;i<Math.Abs(l);i++){bool b=rng.NextDouble()<chance;ok=l>0?ok||b:ok&&b;}
            return ok;
        }
        void SpawnWave(){targets.Clear();for(int i=0;i<5;i++){float h=(70+stage*15)*(i==4?4:i==3?2:1);targets.Add(new Target{x=5+i*5,hp=h,max=h,boss=i==4,elite=i==3});}}
        internal void Move(float dir,float dt){if(dir==0||hp<=0||time<playerStun)return;float old=x;x=Mathf.Clamp(x+dir*dt*7*Speed*(sprint?1.45f:1)*(playerSlow>time?.5f:1),-25,45);distance+=Mathf.Abs(x-old);movedAt=time;}
        Target Near(float at){Target best=null;float d=float.MaxValue;foreach(var t in targets)if(t.Alive&&Mathf.Abs(t.x-at)<d){best=t;d=Mathf.Abs(t.x-at);}return best;}
        float CD(float seconds)=>time<freeUntil?0:Mathf.Max(.15f,seconds*Mathf.Pow(.75f,N("AlienHead"))-(N("LunarBadLuck")>0?1+N("LunarBadLuck"):0));
        internal void Shoot(int slot=0){
            Target t=Near(x);if(t==null||hp<=0||time<playerStun)return;
            float k=1;
            if(slot==0){if(time<shotAt)return;if(Mathf.Abs(t.x-x)>weapon.reach)return;shotAt=time+weapon.interval/Rate;k=weapon.coefficient;if(N("LunarPrimaryReplacement")>0){k=1.2f;if(++shards>=12*N("LunarPrimaryReplacement")){shards=0;shotAt=time+2*N("LunarPrimaryReplacement");}Later(t,.6f,Power*k);return;}}
            if(slot==1){if(secondary<=0)return;secondary--;if(secondRecharge<=time)secondRecharge=time+CD(N("LunarSecondaryReplacement")>0?5*N("LunarSecondaryReplacement"):3);k=2;if(N("LunarSecondaryReplacement")>0){for(int n=1;n<=3;n++)Later(t,n,Power*8.75f);Later(t,3,Power*7);t.stun=time+3*N("LunarSecondaryReplacement");return;}}
            if(slot==2){if(time<specialAt)return;specialAt=time+CD(8);k=4.5f;if(N("LunarSpecialReplacement")>0){specialAt=time+CD(8*N("LunarSpecialReplacement"));foreach(var e in targets)if(e.Alive&&e.rupture>0){Deal(e,Power*(3+1.2f*e.rupture));e.rupture=0;}return;}}
            int shots=slot==0&&N("LunarPrimaryReplacement")==0?weapon.pellets:1;
            for(int i=0;i<shots&&t.Alive;i++)Hit(t,Power*k,slot==0?weapon.procCoefficient:1);
        }
        int shards;
        internal void Dodge(){if(utility<=0||hp<=0)return;utility--;if(utilityAt<=time)utilityAt=time+CD(4)*(N("UtilitySkillMagazine")>0?.67f:1);
            hideUntil=time+Mathf.Max(1,3*N("LunarUtilityReplacement"));if(N("LunarUtilityReplacement")>0)Heal(Max*.25f);Move(1,.7f);}
        internal void Jump(){
            if(time>airUntil)jumps=0;if(jumps>=1+N("Feather"))return;
            jumps++;airUntil=time+1+jumps*.3f;Move(1,.45f+(sprint?N("JumpBoost")*1.4f:0));
        }
        void Heal(float value){if(value<=0||hp<=0)return;value*=1+N("IncreaseHealing");if(N("RepeatHeal")>0){healQueue+=value*(1+N("RepeatHeal"));return;}ApplyHeal(value);}
        void ApplyHeal(float value){float over=Mathf.Max(0,hp+value-Life);hp=Mathf.Min(Life,hp+value);barrier=Mathf.Min(Max,barrier+over*.5f*N("BarrierOnOverHeal"));soul+=value*N("NovaOnHeal");}
        void Area(float at,float radius,float value,Target skip=null){foreach(var e in targets)if(e!=skip&&e.Alive&&Mathf.Abs(e.x-at)<=radius)Deal(e,value);}
        int statuses(Target t)=>(t.bleed>time?1:0)+(t.slow>time?1:0)+(t.burn>time?1:0)+(t.shred>time?1:0)+(t.stun>time?1:0)+(t.rupture>0?1:0);
        void Hit(Target t,float baseHit,float proc){
            if(!t.Alive)return;combatAt=time;bool crit=time<critEquip||Roll(.01f+.1f*N("CritGlasses")+(N("HealOnCrit")>0?.05f:0)+(N("AttackSpeedOnCrit")>0?.05f:0));
            float dmg=baseHit*(crit?2:1)*(t.hp>.9f*t.max?1+.75f*N("Crowbar"):1)*(t.boss?1+.2f*N("BossDamageBonus"):1)*(Mathf.Abs(x-t.x)<13?1+.2f*N("NearbyDamageBonus"):1);
            if(N("RandomDamageZone")>0&&Mathf.Abs(x-fieldX)<16+8*(N("RandomDamageZone")-1))dmg*=1.5f;
            if(crit){if(N("HealOnCrit")>0)Heal(4+4*N("HealOnCrit"));if(N("AttackSpeedOnCrit")>0){critUntil=time+3;critBuff=Mathf.Min(.12f+N("AttackSpeedOnCrit")*.24f,critBuff+.12f);}}
            if(Roll(.1f*N("BleedOnHit")*proc)||(crit&&N("BleedOnHitAndExplode")>0)){t.bleed=time+3;t.bleeds.Add(t.bleed);}
            if(N("SlowOnHit")>0)t.slow=time+2*N("SlowOnHit");
            if(Roll(1-1/(1+.05f*N("StunChanceOnHit")*proc)))t.stun=time+2;
            if(++t.hits%5==0&&N("ArmorReductionOnHit")>0)t.shred=time+8*N("ArmorReductionOnHit");
            if(N("LunarSpecialReplacement")>0){t.rupture++;t.ruptureEnd=time+10*N("LunarSpecialReplacement");}
            if(N("DeathMark")>0&&statuses(t)>=4)t.mark=time+7*N("DeathMark");
            Deal(t,dmg);
            if(N("Seed")>0)Heal(N("Seed")*proc);if(time<leech)Heal(dmg*.2f);
            if(N("GoldOnHit")>0&&Roll(.3f*proc)){gold+=2*N("GoldOnHit");earnedGold+=2*N("GoldOnHit");}
            ProcImpact(t,dmg,proc,Ancestor.None);
            if(N("StickyBomb")>0&&Roll(.05f*N("StickyBomb")*proc))Later(t,1,dmg*1.8f);

            if(N("BounceNearby")>0&&Roll(.2f*N("BounceNearby")*proc))Chain(t,5+5*N("BounceNearby"),100,dmg);
            if(N("Behemoth")>0)Area(t.x,2.5f+1.5f*N("Behemoth"),dmg*.6f);
            if(N("FireballsOnHit")>0&&Roll(.1f*proc)){Area(t.x,8,dmg*9*N("FireballsOnHit"));t.burn=time+3;}
            if(N("LightningStrikeOnHit")>0&&Roll(.1f*proc))Deal(t,dmg*5*N("LightningStrikeOnHit"));
            if(dmg>=Power*4&&N("FireRing")>0&&time>=ringFire){ringFire=time+10;vortexX=t.x;vortexEnd=time+3;vortexDps=dmg*N("FireRing");}
            if(dmg>=Power*4&&N("IceRing")>0&&time>=ringIce){ringIce=time+10;t.slow=time+3*N("IceRing");Area(t.x,8,dmg*2.5f*N("IceRing"));}
        }
        struct Pending{public Target t;public float at,dmg;}
        readonly List<Pending> pending=new List<Pending>();
        void Later(Target t,float delay,float dmg)=>pending.Add(new Pending{t=t,at=time+delay,dmg=dmg});
        void Chain(Target t,int count,float range,float dmg){foreach(var e in targets)if(e!=t&&e.Alive&&Mathf.Abs(t.x-e.x)<=range&&count-->0)Deal(e,dmg);}
        void Deal(Target t,float dmg){if(t==null||!t.Alive||dmg<=0)return;dmg*= (t.shred>time?1.375f:1)*(t.mark>time?1.5f:1);
            if(t.elite&&t.hp-dmg<t.max*(1-1/(1+.13f*N("ExecuteLowHealthElite"))))dmg=t.hp;
            peakHit=Mathf.Max(peakHit,Mathf.Min(dmg,t.hp));damageDone+=Mathf.Min(dmg,t.hp);t.hp-=dmg;if(t.hp<=0)Killed(t);}
        void Killed(Target t){
            SalvageDeath(t);kills++;gold+=8;earnedGold+=8;if(kills%3==0&&!hasReward){hasReward=true;reward="Syringe";recycleUsed=false;}barrier=Mathf.Min(Max,barrier+15*N("BarrierOnKill"));
            if(N("Tooth")>0)Heal(8+Max*.02f*N("Tooth"));infusion=Mathf.Min(100*N("Infusion"),infusion+N("Infusion"));
            if(Roll(.04f*N("BonusGoldPackOnKill"))){gold+=25*stage;earnedGold+=25*stage;}
            if(N("Bandolier")>0&&Roll(.08f+.1f*N("Bandolier"))){specialAt=utilityAt=secondRecharge=time;secondary=1+N("SecondarySkillMagazine");utility=1+2*N("UtilitySkillMagazine");}
            if(N("Talisman")>0)equipRecharge-=2+2*N("Talisman");
            if(N("IgniteOnKill")>0){foreach(var e in targets)if(Mathf.Abs(e.x-t.x)<=8+4*N("IgniteOnKill"))e.burn=time+3;Area(t.x,8+4*N("IgniteOnKill"),Power*1.5f);}
            if(N("ExplodeOnDeath")>0)Area(t.x,9.6f+2.4f*N("ExplodeOnDeath"),Power*(.7f+2.8f*N("ExplodeOnDeath")));
            if(t.bleed>time&&N("BleedOnHitAndExplode")>0)Area(t.x,12,(Power*4+t.max*.15f)*N("BleedOnHitAndExplode"));
            if(N("Dagger")>0)for(int i=0;i<3;i++){var v=Near(t.x);if(v!=null)Later(v,.25f,1.5f*Power*N("Dagger"));}
            if(N("GhostOnKill")>0&&Roll(.07f))ghost=time+30*N("GhostOnKill");
            if(t.elite&&N("HeadHunter")>0)eliteUntil=time+3+5*N("HeadHunter");
            if(t.elite&&N("KillEliteFrenzy")>0)freeUntil=time+4*N("KillEliteFrenzy");
            if(N("Icicle")>0){icicleUntil=time+5;iceKills.Enqueue(time);}if(N("Plant")>0){plantStart=time+5;plantX=t.x;}
            recentKills.Enqueue(time);while(recentKills.Count>0&&recentKills.Peek()<time-7)recentKills.Dequeue();
            if(recentKills.Count>=4){if(N("LaserTurbine")>0&&disc<=time){disc=time+7;foreach(var other in targets)if(other.Alive){Later(other,.2f,Power*3*N("LaserTurbine"));if(Mathf.Abs(other.x-t.x)<=10)Later(other,.5f,Power*10*N("LaserTurbine"));Later(other,.8f,Power*3*N("LaserTurbine"));}}
                int count=0;foreach(float k in recentKills)if(time-k<=1)count++;if(count>=4&&N("WarCryOnMultiKill")>0)rageUntil=time+2+4*N("WarCryOnMultiKill");}
            if(kills%3==0){level++;BannerUp();}
        }
        void BannerUp(){if(N("WardOnLevel")>0){bannerX=x;bannerUntil=time+60;}}
    }
}









