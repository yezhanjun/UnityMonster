#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
namespace Jotunnslayer
{
 internal static class DraugCastCombatValidation
 {
  internal static IEnumerator Run(CharacterInstance owner,CharacterInstance target,Action<bool> report)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var combat=owner.GetComponent<Skeleton1HCombat>();var type=typeof(Skeleton1HCombat);
   var visual=(Skeleton1HVisual)type.GetField("visual",flags).GetValue(combat);
   var schedule=(DraugAttackSchedule)type.GetField("draugSchedule",flags).GetValue(combat);
   var flasks=(List<DraugFlaskAttack>)type.GetField("draugFlasks",flags).GetValue(combat);
   var waiting=type.GetField("gmWaiting",flags);var ends=type.GetField("phaseEnds",flags);var update=type.GetMethod("Update",flags);
   if(!(bool)waiting.GetValue(combat)){report(false);yield break;}
   bool passed=true;int cases=0,cancels=0;
   void Check(bool value,string reason){if(!value){passed=false;Debug.LogError("DRAUG_CAST_COMBAT_FAIL "+reason);}}
   bool started=false,released=false,ended=false,finished=false;int frame=-1,count=0,index=0;float attackTimer=0,skillTimer=0;
   DraugAttackSchedule.Action action=DraugAttackSchedule.Action.None;
   var probe=owner.gameObject.AddComponent<DraugCombatFrameProbe>();
   probe.Capture=()=>{frame=Time.frameCount;started=visual.DraugCastStarted;released=visual.DraugCastReleased;ended=visual.DraugCastEnded;finished=visual.DraugCastFinished;count=flasks.Count;index=schedule.ArrowIndex;attackTimer=schedule.AttackRemaining;skillTimer=schedule.SkillRemaining;action=schedule.Current;};
   Vector3 op=owner.transform.position,tp=target.transform.position;int oh=owner.CurrentHealth,th=target.CurrentHealth;bool block=owner.Block;
   var ownedField=type.GetField("draugRandom",flags);var savedOwned=ownedField.GetValue(combat);
   var random=UnityEngine.Random.state;float oldAttack=schedule.AttackRemaining,oldSkill=schedule.SkillRemaining;int oldIndex=schedule.ArrowIndex;
   void Clock(string name,float value)=>typeof(DraugAttackSchedule).GetProperty(name,flags).SetValue(schedule,value);
   void Position(){owner.transform.position=new Vector3(400,100,400);target.transform.position=new Vector3(400,100,406);owner.CurrentHealth=target.CurrentHealth=1000;Physics.SyncTransforms();}
   void Begin(bool chase)
   {
    Position();owner.BlockCharacter(false);combat.SetGmCombat(true);visual.Play(Skeleton1HVisual.Motion.Hit,true);
    visual.Play(chase?Skeleton1HVisual.Motion.Walk:Skeleton1HVisual.Motion.Idle,true);
    Clock("AttackRemaining",0);Clock("SkillRemaining",0);schedule.SetNextArrowIndex(2);
    // Seed0 has a verified first chance sample below .8; restore the real actor stream in finally.
    ownedField.SetValue(combat,new DraugRandomSource(0));
    type.GetField("nextAttack",flags).SetValue(combat,Time.time-1);ends.SetValue(combat,Time.time-1);update.Invoke(combat,null);
    Check(schedule.Current==DraugAttackSchedule.Action.Flask && flasks.Count==0,"flask request failed or released before evaluation");
    Check(schedule.SkillRemaining<=0 && schedule.AttackRemaining<=0,"cooldown reset at request");
   }
   try
   {
    for(int entry=0;entry<2 && passed;entry++)
    {
     Begin(entry==1);float began=Time.time;bool sawEntry=false,sawRelease=false,sawEnd=false,complete=false;
     while(passed && Time.time-began<3)
     {
      yield return null;Check(frame==Time.frameCount-1,"missing evaluated frame snapshot");
      if(started && !sawEntry){sawEntry=true;Check(attackTimer<=0 && skillTimer<=0,"entry prematurely reset cooldown");}
      if(!sawRelease)
      {
       Check(released?count==1:count==0,"flask release differs from evaluated event threshold");
       if(released){sawRelease=true;Check(Mathf.Abs(skillTimer-4)<.0002f,"skill cooldown not reset on release");Check(attackTimer<=0,"attack cooldown reset at release");combat.ProcessDraugCast();Check(flasks.Count==1,"duplicate release");}
      }
      if(ended && !sawEnd)
      {
       sawEnd=true;Check(Mathf.Abs(attackTimer-4)<.0002f,"attack cooldown not reset on cast end");
       float before=schedule.AttackRemaining;combat.ProcessDraugCast();Check(schedule.AttackRemaining==before,"duplicate cast end reset timer");
      }
      Check(index==2,"flask consumed arrow variation");
      if(finished){complete=true;Check(action==DraugAttackSchedule.Action.None,"did not finish in evaluated idle frame");break;}
      Check(action==DraugAttackSchedule.Action.Flask,"cast ended before idle blend settled");Position();
     }
     Check(sawEntry && sawRelease && sawEnd && complete,"incomplete cast chain");cases++;
     Debug.Log($"DRAUG_CAST_COMBAT_CASE entry={entry} automaticLateUpdate=True separateCooldowns=True elapsed={Time.time-began}");
     combat.SetGmCombat(false);yield return null;
    }
    for(int mode=0;mode<2 && passed;mode++)
    for(int stage=0;stage<3 && passed;stage++)
    {
     Begin(true);float began=Time.time;
     if(stage>0)while(passed && (stage==1?flasks.Count==0:!visual.DraugCastReturning))
     {yield return null;Position();if(Time.time-began>3){Check(false,"cast cancel stage timeout");break;}}
     uint generation=schedule.Generation;int oldCount=flasks.Count;
     if(mode==0)combat.SetGmCombat(false);else{owner.BlockCharacter(true);update.Invoke(combat,null);}
     combat.ProcessDraugCast();
     Check(schedule.Current==DraugAttackSchedule.Action.None && !(bool)type.GetField("draugCastPending",flags).GetValue(combat),"cast cancel retained pending state");
     Check(!schedule.Release(generation) && !schedule.CastEnd(generation) && !schedule.Finish(generation),"stale cast generation accepted");
     Check(Mathf.Abs(schedule.AttackRemaining-4)<.0002f,"cancel before end missed exit timer fallback");
     float until=Time.time+1.3f;
     while(Time.time<until && passed)
     {yield return null;combat.ProcessDraugCast();Position();Check(schedule.Current==DraugAttackSchedule.Action.None && flasks.Count==(mode==0?0:oldCount),"cancelled cast resumed");}
     Check(visual.CurrentMotion==(mode==0?Skeleton1HVisual.Motion.Idle:Skeleton1HVisual.Motion.Hit),"old cast return overwrote cancellation pose");
     owner.BlockCharacter(false);combat.SetGmCombat(false);cancels++;yield return null;
    }
   }
   finally
   {
    ownedField.SetValue(combat,savedOwned);
    probe.Capture=null;UnityEngine.Object.Destroy(probe);owner.BlockCharacter(block);combat.SetGmCombat(false);visual.Play(Skeleton1HVisual.Motion.Idle,true);
    Clock("AttackRemaining",oldAttack);Clock("SkillRemaining",oldSkill);schedule.SetNextArrowIndex(oldIndex);
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.transform.position=op;target.transform.position=tp;UnityEngine.Random.state=random;Physics.SyncTransforms();
   }
   Check(cases==2 && cancels==6,"incomplete cast case count");
   Debug.Log(passed?"DRAUG_CAST_COMBAT_PASS twoAutomaticCases/separateReleaseEndCooldowns/noDuplicate/idleSettlement/sixStageCancellationCases":"DRAUG_CAST_COMBAT_FAILED");report(passed);
  }
 }
}
#endif
