#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using Mirror;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugActorLifetimeValidation
 {
  internal static IEnumerator Run(CharacterInstance target,Action<bool> report)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var type=typeof(Skeleton1HCombat);bool passed=true;int cases=0;CharacterInstance actor=null;
   int baseline=DraugControllerResources.OwnerCount;int health=target.CurrentHealth;Vector3 position=target.transform.position;var random=UnityEngine.Random.state;
   void Check(bool value,string why){if(!value){passed=false;Debug.LogError("DRAUG_ACTOR_LIFETIME_FAIL "+why);}}
   void Clock(DraugAttackSchedule s,string name,float value)=>typeof(DraugAttackSchedule).GetProperty(name,flags).SetValue(s,value);
   void StartAction(Skeleton1HCombat combat,Skeleton1HVisual visual,DraugAttackSchedule schedule,int action)
   {
    target.transform.position=combat.transform.position+Vector3.forward*6;Physics.SyncTransforms();
    combat.SetGmCombat(true);visual.Play(Skeleton1HVisual.Motion.Idle,true);Clock(schedule,"AttackRemaining",0);Clock(schedule,"SkillRemaining",action==0?100:0);
    if(action==1)type.GetField("draugRandom",flags).SetValue(combat,new DraugRandomSource(0));
    type.GetField("nextAttack",flags).SetValue(combat,Time.time-1);type.GetField("phaseEnds",flags).SetValue(combat,Time.time-1);type.GetMethod("Update",flags).Invoke(combat,null);
    Check(schedule.Current==(action==0?DraugAttackSchedule.Action.Arrow:DraugAttackSchedule.Action.Flask), $"wrong action setup current={schedule.Current} distance={Vector3.Distance(combat.transform.position,target.transform.position)} phase={type.GetField("phase",flags).GetValue(combat)} health={combat.GetComponent<CharacterInstance>().CurrentHealth} targetHealth={target.CurrentHealth} enabled={visual.isActiveAndEnabled}");
   }
   try
   {
    for(int action=0;action<2 && passed;action++)
    for(int mode=0;mode<3 && passed;mode++)
    for(int stage=0;stage<2 && passed;stage++)
    {
     target.CurrentHealth=1000;target.transform.position=new Vector3(500,100,506);Physics.SyncTransforms();
     actor=Skeleton1HSpawn.TrySpawn(target,new Vector3(500,100,500),Quaternion.identity,Skeleton1HSpawn.Kind.DraugThrower);
     Check(actor,"spawn failed: "+Skeleton1HSpawn.LastSpawnError);if(!actor)break;
     var combat=actor.GetComponent<Skeleton1HCombat>();var visual=(Skeleton1HVisual)type.GetField("visual",flags).GetValue(combat);
     var schedule=(DraugAttackSchedule)type.GetField("draugSchedule",flags).GetValue(combat);
     var arrows=(List<DraugArrowAttack>)type.GetField("draugArrows",flags).GetValue(combat);var flasks=(List<DraugFlaskAttack>)type.GetField("draugFlasks",flags).GetValue(combat);
     var startupRandom=(DraugRandomSource)type.GetField("draugRandom",flags).GetValue(combat);
     var startupReference=new System.Random(startupRandom.Seed);
     float startupSample=startupReference.Next(1000000)/999999f;
     Check(Mathf.Abs(combat.DraugStartupChaseDistance-17f*(.95f+startupSample*(1.05f-.95f)))<.00001f,"startup chase sample mismatch");
     Check(startupRandom.IsInitialized,"startup did not consume actor sample");
     combat.SetGmCombat(false);yield return null; // Let ordinary Start/death subscriptions initialize.
     StartAction(combat,visual,schedule,action);uint generation=schedule.Generation;uint netId=actor.netId;
     Check(DraugControllerResources.OwnerCount==baseline+1,"spawn did not acquire exactly one controller lease");
     if(stage==1)
     {
      float deadline=Time.time+3;
      while(arrows.Count+flasks.Count==0 && Time.time<deadline){yield return null;target.CurrentHealth=1000;target.transform.position=actor.transform.position+Vector3.forward*6;Physics.SyncTransforms();}
      Check(arrows.Count+flasks.Count==1,"release stage not reached");
     }
     var launched=new List<Object>();foreach(var arrow in arrows)if(arrow)launched.Add(arrow);foreach(var flask in flasks)if(flask)launched.Add(flask);
     bool deathCallback=false;
     if(mode==0)
     {
      actor.Server_OnHealthDepleted+=(part,attack,killer,force)=>deathCallback=true;
      actor.ServerApplyRangeItemBlast(100000,target);
      Check(deathCallback && actor.CurrentHealth<=0,"lethal damage did not execute death callback");
      yield return null;yield return null;
      Check(schedule.Current==DraugAttackSchedule.Action.None && arrows.Count==0 && flasks.Count==0,"death retained combat actions");
      // Death and despawn are distinct carrier paths. Explicitly exercise the latter too.
      if(actor)NetworkServer.Destroy(actor.gameObject);
     }
     else if(mode==1)NetworkServer.Destroy(actor.gameObject);
     else
     {
      actor.gameObject.SetActive(false);
      Check(schedule.Current==DraugAttackSchedule.Action.None && arrows.Count==0 && flasks.Count==0,"root disable retained actions");
     }
     yield return null;yield return null;
     Check(DraugControllerResources.OwnerCount==baseline,"lifetime exit retained controller lease");
     foreach(var projectile in launched)Check(!projectile,"launched projectile survived actor exit");
     Check(!schedule.Release(generation) && !schedule.CastEnd(generation) && !schedule.EnterArrow(generation,2),"stale generation accepted after lifetime exit");
     if(mode==2 && actor)
     {
      var beforeRebind=type.GetField("draugRandom",flags).GetValue(combat);
      float beforeChase=combat.DraugStartupChaseDistance;
      actor.gameObject.SetActive(true);
      Check(combat.Begin(target,visual,Skeleton1HSpawn.Kind.DraugThrower),"production Begin rejected rebind");
      Check(ReferenceEquals(beforeRebind,type.GetField("draugRandom",flags).GetValue(combat)) && beforeChase==combat.DraugStartupChaseDistance,"rebind replaced random stream or repeated startup");
      StartAction(combat,visual,schedule,action);
      Check(schedule.Generation!=generation && DraugControllerResources.OwnerCount==baseline+1,"fresh bind did not start fresh owned action");
      NetworkServer.Destroy(actor.gameObject);yield return null;yield return null;
      Check(DraugControllerResources.OwnerCount==baseline,"rebound actor removal retained lease");
     }
     Check(!actor && !NetworkServer.spawned.ContainsKey(netId),"network actor remained after destroy");
     Debug.Log($"DRAUG_ACTOR_LIFETIME_CASE action={action} mode={mode} stage={stage} deathCallback={deathCallback} leaseBaseline={baseline} cleanup={passed}");
     actor=null;cases++;
    }
   }
   finally
   {
    if(actor)NetworkServer.Destroy(actor.gameObject);
    target.CurrentHealth=health;target.transform.position=position;UnityEngine.Random.state=random;Physics.SyncTransforms();
   }
   Check(cases==12,"incomplete actor lifetime cases");
   Debug.Log(passed?"DRAUG_ACTOR_LIFETIME_PASS twelveCases/lethalDamageThenDespawn/networkDestroy/rootDisableBeginRebind/pendingAndReleased/leasesAndProjectiles":"DRAUG_ACTOR_LIFETIME_FAILED");report(passed);
  }
 }
}
#endif
