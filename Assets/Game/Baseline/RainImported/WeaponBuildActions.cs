using System;
using UnityEngine;
namespace MTPSKIT.RainImported {
    internal sealed partial class WeaponBuild {
        internal void Hurt(float dmg,bool projectile=true){
            if(hp<=0||time<hideUntil)return;
            if(projectile&&N("CaptainDefenseMatrix")>0&&time>=matrix){matrix=time+.5f/Rate;return;}
            if(Roll(1-1/(1+.15f*N("Bear")),false))return;
            float armor=(time<tonic?20:0)+(sprint?30*N("SprintArmor"):0)+(time<armorEquip?500:0);
            dmg=Mathf.Max(1,dmg*100/(100+armor)-5*N("ArmorPlate"));
            float b=Mathf.Min(barrier,dmg);barrier-=b;dmg-=b;b=Mathf.Min(shield,dmg);shield-=b;dmg-=b;
            hurtAt=combatAt=time;received+=Mathf.Min(hp,dmg);hp-=dmg;
            if(N("GoldOnHit")>0)gold=Mathf.Max(0,gold-(int)(gold*dmg/Max*N("GoldOnHit")));
            if(hp<=0){if(N("ExtraLife")>0){Add("ExtraLife",-1);hp=Life;hideUntil=time+3;}else{hp=0;killer=projectile?"来袭单位":"范围伤害";return;}}
            if(N("Medkit")>0)healAt=time+2;if(N("ParentEgg")>0)Heal(15*N("ParentEgg"));
            if(N("Thorns")>0){int count=3+2*N("Thorns");foreach(var e in targets)if(e.Alive&&Mathf.Abs(e.x-x)<=15+10*N("Thorns")&&count-->0)Deal(e,Power*1.6f);}
            if(hp<Max*.25f){
                if(N("Phasing")>0&&time>=hideReady){hideUntil=time+5;hideReady=time+30*Mathf.Pow(.5f,N("Phasing")-1);}
                if(N("NovaOnLowHealth")>0&&time>=novaReady){Area(x,25,60*Power);novaReady=time+30*Mathf.Pow(.5f,N("NovaOnLowHealth")-1);}
            }
        }
        float matrix;
        internal void Interact(string action){
            if(hp<=0)return;
            switch(action){
                case "传送器":if(zone)return;zone=true;BannerUp();titan=N("TitanGoldDuringTP")>0;zoneNova=0;break;
                case "宝箱":if(!cache)return;cache=false;Add("TreasureCache",-1);Add(Roll(.8f)?"Syringe":"Missile");break;
                case "打印机":
                    string scrap=null;foreach(var s in new[]{"ScrapWhite","ScrapGreen","ScrapRed","ScrapYellow"})if(N(s)>0){scrap=s;break;}
                    if(scrap==null)return;Add(scrap,-1);Add(scrap=="ScrapWhite"?"Syringe":scrap=="ScrapGreen"?"Missile":scrap=="ScrapRed"?"Behemoth":"Knurl");break;
                case "神器圣匣":if(N("ArtifactKey")==0||!keyRoom)return;Add("ArtifactKey",-1);keyRoom=false;Area(0,100,Power*10);break;
                case "方尖碑":if(N("LunarTrinket")==0)return;SpawnWave();var scav=targets[targets.Count-1];scav.hp=scav.max*=3;last="隐藏遭遇：拾荒者";return;
                case "神龛":if(N("MonstersOnShrineUse")>0)SpawnWave();break;
                default:return;
            }
            last=action;if(N("Firework")>0)for(int i=0;i<4+4*N("Firework");i++){var t=Near(x);if(t!=null)Later(t,.25f,Power*3);}
            if(N("Squid")>0)turret=time+30;
        }
        internal void NextStage(){FleetStage();stage++;SpawnWave();pending.Clear();zone=false;finished=false;charge=0;cache=N("TreasureCache")>0;keyRoom=true;
            last="进入下一关";}
        internal void UseEquipment(){
            if(equipmentCharges<=0||hp<=0||equipment=="")return;var t=Near(x);
            if(equipment=="Recycle"&&(recycleUsed||!hasReward))return;
            equipmentCharges--;if(equipRecharge<=time)equipRecharge=time+EquipCD();
            if(N("EnergizedOnEquipmentUse")>0)energized=time+4+4*N("EnergizedOnEquipmentUse");
            last=equipment;
            switch(equipment){
                case "BurnNearby":fireUntil=time+12;break;
                case "CrippleWard":if(effigies<5)effigies++;wardPositions[wardCursor]=x;wardCursor=(wardCursor+1)%5;break;
                case "Meteor":meteor=time+20;break;
                case "Tonic":tonic=tonicAt=time+20;break;
                case "BFG":Area(x,35,Power*6);if(t!=null)Area(t.x,20,Power*40);break;
                case "Blackhole":blackhole=time+10;gravityX=t!=null?t.x:x;break;
                case "Cleanse":playerBleed=playerSlow=playerStun=0;safeClean=time+1;break;
                case "CommandMissile":for(int i=0;i<12;i++){var e=Near(x);if(e!=null)Later(e,i*.06f,Power*3);}break;
                case "CritOnUse":critEquip=time+8;break;
                case "DeathProjectile":doll=time+8;break;
                case "DroneBackup":SummonBackup();break;
                case "FireBallDash":dash=time+5;dashEnd=dash;break;
                case "Fruit":Heal(.5f*Max);break;
                case "GainArmor":armorEquip=time+5;break;
                case "Gateway":tunnel=time+30;x=x<15?35:0;break;
                case "GoldGat":goldFire=time+8;break;
                case "Jetpack":flight=time+15;break;
                case "LifestealOnHit":leech=time+8;break;
                case "Lightning":if(t!=null){Deal(t,30*Power);foreach(var e in targets)if(Mathf.Abs(e.x-t.x)<10)e.stun=time+3;}break;
                case "PassiveHealing":Heal(Max*.1f);woodUntil=time+60;break;
                case "Recycle":
                    // Ground rewards in this arena use the normal tier. Select another enabled normal-tier definition.
                    if(recyclePool==null)recyclePool=Array.FindAll(new LogbookCatalog().Entries,e=>e.family=="item"&&e.tier==0);
                    if(recyclePool.Length>1){int start=rng.Next(recyclePool.Length);for(int i=0;i<recyclePool.Length;i++){
                        var candidate=recyclePool[(i+start)%recyclePool.Length];if(candidate.catalogName!=reward){reward=candidate.catalogName;break;}}}
                    recycleUsed=true;break;
                case "Saw":if(t!=null){Deal(t,12*Power);sawUntil=time+3;}break;
                case "Scanner":scan=time+10;break;
                case "TeamWarCry":rageUntil=time+7;break;
            }
        }
        readonly float[] wardPositions=new float[5];int wardCursor;float safeClean,dashEnd;bool recycleUsed;
        PickupRow[] recyclePool; bool hasReward=true;float gravityX,playerBleed,playerSlow,playerStun;
        internal string reward="Syringe";
        internal bool Revealed=>scan>time;
        internal void Pickup(){if(!hasReward)return;hasReward=false;Add(reward);last="拾取："+reward;}
        internal void Traverse(){if(tunnel>time)x=x<15?35:0;}
        float EquipCD()=>Mathf.Max(.5f,equipBase*Mathf.Pow(.85f,N("EquipmentMagazine"))*(N("AutoCastEquipment")>0?.5f*Mathf.Pow(.85f,N("AutoCastEquipment")-1):1));
        void Ally(float d){var t=Near(x);if(t!=null){minionDamage+=Mathf.Min(t.hp,d);Deal(t,d);if(!t.Alive)minionKills++;}}
        internal void Step(float dt){
            if(hp<=0||finished)return;time+=dt;
            foreach(var e in targets){if(e.ruptureEnd<=time)e.rupture=0;if(!e.Alive)continue;if(blackhole>time&&Mathf.Abs(e.x-gravityX)<30)e.x=Mathf.MoveTowards(e.x,gravityX,dt*4);else if(e.stun<=time&&e.rootUntil<=time&&Mathf.Abs(e.x-x)>2)e.x=Mathf.MoveTowards(e.x,x,dt*(e.slow>time?.3f:1.2f));}
            for(int i=pending.Count-1;i>=0;i--)if(pending[i].at<=time){var p=pending[i];pending.RemoveAt(i);Deal(p.t,p.dmg);}
            if(time>critUntil)critBuff=0;if(healAt>0&&time>=healAt){healAt=0;Heal(20+Max*.05f*N("Medkit"));}
            float amount=Mathf.Min(healQueue,Max*.1f*Mathf.Pow(.5f,Mathf.Max(0,N("RepeatHeal")-1))*dt);
            healQueue-=amount;if(amount>0)ApplyHeal(amount);
            barrier=Mathf.Max(0,barrier-Max*.033f*dt);if(time-hurtAt>7)shield=Mathf.Min(MaxShield,shield+dt*MaxShield*.5f);
            if(secondary<1+N("SecondarySkillMagazine")&&time>=secondRecharge){secondary++;secondRecharge=time+CD(3);}
            if(utility<1+2*N("UtilitySkillMagazine")&&time>=utilityAt){utility++;utilityAt=time+CD(4)*.67f;}
            if(equipmentCharges<1+N("EquipmentMagazine")&&time>=equipRecharge){equipmentCharges++;equipRecharge=time+EquipCD();}
            if(N("AutoCastEquipment")>0&&equipmentCharges>0)UseEquipment();
            hp=Mathf.Min(hp,Life);if(tonicAt>0&&time>=tonicAt){tonicAt=0;if(Roll(.2f))affliction++;}
            if(dashEnd>0&&time>=dashEnd){dashEnd=0;Area(x,15,Power*8);}
            if(N("FallBoots")>0&&airUntil>0&&time>=airUntil&&fallReady<=time){Area(x,10*jumps,10*Power*jumps);fallReady=time+10*Mathf.Pow(.5f,N("FallBoots")-1);airUntil=0;}
            if(zone){charge+=dt*(1+.3f*N("FocusConvergence"))/20*(Mathf.Abs(x)<16*Mathf.Pow(.5f,N("FocusConvergence"))?1:0);
                if(N("TPHealingNova")>0&&zoneNova<N("TPHealingNova")&&charge>=(zoneNova+.5f)/N("TPHealingNova")){zoneNova++;Heal(Max*.5f);}
                if(charge>=1&&Near(x)==null){finished=true;last="关卡完成";}}
            FleetStep(dt);if(time>=secondTick){secondTick=time+1;Pulse();}
            if(time>=threatAt){threatAt=time+3;var t=Near(x);if(t!=null && Mathf.Abs(t.x-x)<18 && t.stun<=time && dash<=time && flight<=time && airUntil<=time && safeClean<=time)
                {bool hitPlayer=FleetThreat(t,(8+stage)*incomingScale*(N("RandomDamageZone")>0&&Mathf.Abs(t.x-fieldX)<16?1.5f:1));if(hitPlayer&&t.elite){playerSlow=time+2;playerBleed=time+3;}if(hitPlayer&&t.boss)playerStun=time+.4f;}}
        }
        void Pulse(){
            if(playerBleed>time)Hurt(2,false);
            var target=Near(x);if(target!=null && soul>=Max*.1f){Deal(target,soul*2.5f);soul=0;}
            float regen=1+(time<medAura?Max*.02f:0)+N("Knurl")*1.6f+(time-combatAt>5?3*N("HealWhileSafe"):0);
            if(time<tonic)regen*=4;
            if(time-movedAt>1&&N("Mushroom")>0)regen+=Max*(.0225f+.0225f*N("Mushroom"));
            if(plantStart<=time&&time<plantStart+10&&Mathf.Abs(x-plantX)<=5*N("Plant"))regen+=.1f*Max;
            if(woodUntil>time||equipment=="PassiveHealing")regen+=.015f*Max;
            if(hp<Life)Heal(regen);
            foreach(var e in targets){if(!e.Alive)continue;
                for(int n=e.bleeds.Count-1;n>=0;n--)if(e.bleeds[n]<time)e.bleeds.RemoveAt(n);
                if(e.bleeds.Count>0)Deal(e,Power*.8f*e.bleeds.Count);
                if(e.burn>time)Deal(e,Power*(.5f+.25f*Mathf.Max(0,N("IgniteOnKill")-1)));
                for(int i=0;i<effigies;i++)if(Mathf.Abs(e.x-wardPositions[i])<12){e.slow=time+2;e.shred=time+2;break;}
            }
            if(ghost>time)Ally(15*Power);if(turret>time)Ally(Power*N("Squid"));
            if(N("BeetleGland")>0&&time>=nextBeetle){beetles=Mathf.Min(beetles+1,N("BeetleGland"));nextBeetle=time+30;}
            if(N("RoboBallBuddy")>0&&time>=nextOrb){probes=2;nextOrb=time+30;}
            if(beetles>0&&N("BeetleGland")>0)Ally(beetles*Power*4);if(probes>0&&N("RoboBallBuddy")>0)Ally(probes*Power*N("RoboBallBuddy"));
            if(titan&&zone)Ally(Power*(1+N("TitanGoldDuringTP")));
            if(eliteUntil>time)Area(x,12,Power);
            if(N("ShockNearby")>0&&(int)(time/10)%2==0){int n=1+2*N("ShockNearby");foreach(var e in targets)if(e.Alive&&Mathf.Abs(x-e.x)<25&&n-->0)Deal(e,Power*4);}
                        while(iceKills.Count>0&&time-iceKills.Peek()>5)iceKills.Dequeue();
            if(icicleUntil>time){float radius=Mathf.Min(6+iceKills.Count*2,6+12*N("Icicle"));foreach(var e in targets)if(e.Alive&&Mathf.Abs(e.x-x)<=radius){e.slow=time+1.5f;Deal(e,12*Power);}}
            if(vortexEnd>=time&&vortexDps>0)Area(vortexX,8,vortexDps);
                        if(target!=null&&time-combatAt<=5&&N("SiphonOnLowHealth")>0){
                int selected=0;
                for(int i=0;i<N("SiphonOnLowHealth");i++){
                    int best=-1;float nearest=13;
                    for(int j=0;j<targets.Count;j++)if((selected&(1<<j))==0&&targets[j].Alive&&Mathf.Abs(targets[j].x-x)<=nearest){best=j;nearest=Mathf.Abs(targets[j].x-x);}
                    if(best<0)break;selected|=1<<best;var e=targets[best];float v=Mathf.Min(Power,e.hp);Deal(e,v);Heal(v);e.slow=time+2;
                }
            }
            if(sprint&&N("SprintWisp")>0&&time>=sprintShot){sprintShot=time+1.6f/Speed;Deal(target,Power*3*N("SprintWisp"));}
            if(time<meteor){Area((float)rng.NextDouble()*30,9,Power*6);if(Roll(.2f))Hurt(Power*6,false);}
            if(time<fireUntil){Area(x,15,Max*.05f*24);Hurt(Max*.05f,false);}
            if(time<doll&&target!=null)Hit(target,Power,1);
            if(time<dash)Area(x,8,Power*5);
            if(time<goldFire&&gold>0){gold--;Deal(target,Power);}
            if(time<sawUntil&&target!=null&&target.bleed>time)Deal(target,3*Power);
            if(N("RandomDamageZone")>0&&(int)time%8==0)fieldX=(float)rng.NextDouble()*30;
        }
    }
}









