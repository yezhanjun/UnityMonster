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
 [DefaultExecutionOrder(32000)]
 internal sealed class DraugCombatFrameProbe : MonoBehaviour
 {
  internal Action Capture;
  void LateUpdate()=>Capture?.Invoke();
 }
 internal static class DraugClipTimingValidation
 {
  internal static IEnumerator Run(CharacterInstance owner,CharacterInstance target,Action<bool> report)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var combat=owner.GetComponent<Skeleton1HCombat>();var type=typeof(Skeleton1HCombat);
   var visual=(Skeleton1HVisual)type.GetField("visual",flags).GetValue(combat);
   var schedule=(DraugAttackSchedule)type.GetField("draugSchedule",flags).GetValue(combat);
   var arrows=(List<DraugArrowAttack>)type.GetField("draugArrows",flags).GetValue(combat);
   var waiting=type.GetField("gmWaiting",flags);var phase=type.GetField("phase",flags);var ends=type.GetField("phaseEnds",flags);
   var update=type.GetMethod("Update",flags);var entered=type.GetField("draugEntered",flags);
   bool passed=true;int cases=0,cancels=0;
   bool sampledStarted=false,sampledReturning=false,sampledFinished=false,sampledEntered=false;
   int sampledArrows=0,sampledIndex=0,sampledFrame=-1;double sampledClip=0;float sampledTimer=0;
   DraugAttackSchedule.Action sampledAction=DraugAttackSchedule.Action.None;
   if(!(bool)waiting.GetValue(combat)){report(false);yield break;}
   var probe=owner.gameObject.AddComponent<DraugCombatFrameProbe>();
   probe.Capture=()=>
   {
    sampledFrame=Time.frameCount;sampledStarted=visual.DraugArrowStarted;sampledReturning=visual.DraugArrowReturning;sampledFinished=visual.DraugArrowFinished;
    sampledEntered=(bool)entered.GetValue(combat);sampledArrows=arrows.Count;sampledIndex=(int)type.GetField("draugArrowIndex",flags).GetValue(combat);
    sampledClip=visual.CurrentClipTime;sampledTimer=schedule.AttackRemaining;sampledAction=schedule.Current;
   };
   void Check(bool value,string reason){if(!value){passed=false;Debug.LogError("DRAUG_CONTROLLER_COMBAT_FAIL "+reason);}}
   Vector3 op=owner.transform.position,tp=target.transform.position;int oh=owner.CurrentHealth,th=target.CurrentHealth;bool block=owner.Block;
   var random=UnityEngine.Random.state;float oldAttack=schedule.AttackRemaining,oldSkill=schedule.SkillRemaining;int oldIndex=schedule.ArrowIndex;
   void Clock(string name,float value)=>typeof(DraugAttackSchedule).GetProperty(name,flags).SetValue(schedule,value);
   void Position(){owner.transform.position=new Vector3(400,100,400);target.transform.position=new Vector3(400,100,406);owner.CurrentHealth=target.CurrentHealth=1000;Physics.SyncTransforms();}
   void Begin(bool chase,int index)
   {
    Position();owner.BlockCharacter(false);combat.SetGmCombat(true);
    visual.Play(Skeleton1HVisual.Motion.Hit,true);
    visual.Play(chase?Skeleton1HVisual.Motion.Walk:Skeleton1HVisual.Motion.Idle,true,startSeconds:index==1?.049f*(chase?Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/move").length:visual.IdleDuration):0);
    Clock("AttackRemaining",0);Clock("SkillRemaining",100);schedule.SetNextArrowIndex(index);
    type.GetField("nextAttack",flags).SetValue(combat,Time.time-1);ends.SetValue(combat,Time.time-1);
    string beforeRandom=JsonUtility.ToJson(UnityEngine.Random.state);
    update.Invoke(combat,null);
    Check(beforeRandom==JsonUtility.ToJson(UnityEngine.Random.state),"cooling skill consumed random before Animator entry");
    Check(schedule.Current==DraugAttackSchedule.Action.Arrow && phase.GetValue(combat).ToString()=="Windup","request did not create arrow windup");
    Check(!(bool)entered.GetValue(combat) && arrows.Count==0 && schedule.ArrowIndex==index,"request consumed entry or release before Animator evaluation");
   }

   try
   {
    for(int entry=0;entry<2 && passed;entry++)
    for(int index=0;index<3 && passed;index++)
    {
     Begin(entry==1,index);float began=Time.time;bool sawEntry=false,sawRelease=false,sawReturn=false,finished=false;
     while(passed && Time.time-began<3)
     {
      yield return null;
      Check(sampledFrame==Time.frameCount-1,"missing previous evaluated frame sample");
      if(sampledStarted && !sawEntry)
      {
       sawEntry=true;Check(sampledEntered,"entry not consumed in same evaluated frame");
       Check(Mathf.Abs(sampledTimer-4)<.0002f,"entry timer not reset to4");
       Check(sampledIndex==index,"current attack index overwritten by next index");
      }
      if(!sawRelease)
      {
       bool eventReached=sampledStarted && sampledClip>=DraugArrowTimeline.ClipEventSeconds(index);
       Check(eventReached?sampledArrows==1:sampledArrows==0,"release did not match event threshold in evaluated frame");
       if(eventReached){sawRelease=true;combat.ProcessDraugArrow();Check(arrows.Count==1,"repeated sample released twice");}
      }
      if(sampledReturning)sawReturn=true;
      if(sampledFinished)
      {
       finished=true;Check(sampledAction==DraugAttackSchedule.Action.None,"finish not consumed in evaluated frame");break;
      }
      Check(sampledAction==DraugAttackSchedule.Action.Arrow,"schedule ended before source idle settlement");Position();
     }
     Check(sawEntry && sawRelease && sawReturn && finished,"incomplete native controller attack chain");cases++;
     Debug.Log($"DRAUG_CONTROLLER_COMBAT_CASE entry={entry} index={index} automaticLateUpdate=True elapsed={Time.time-began}");
     combat.SetGmCombat(false);yield return null;
    }
    for(int mode=0;mode<2 && passed;mode++)
    for(int stage=0;stage<3 && passed;stage++)
    {
     Begin(true,1);float began=Time.time;
     if(stage>0)
     {
      while(passed && (stage==1?arrows.Count==0:!visual.DraugArrowReturning))
      {yield return null;Position();if(Time.time-began>3){Check(false,"cancel stage timeout");break;}}
     }
     uint oldGeneration=schedule.Generation;int next=schedule.ArrowIndex;int released=arrows.Count;
     if(mode==0)combat.SetGmCombat(false);
     else{owner.BlockCharacter(true);update.Invoke(combat,null);}
     combat.ProcessDraugArrow();
     Check(schedule.Current==DraugAttackSchedule.Action.None && type.GetField("draugPendingClip",flags).GetValue(combat)==null,"cancel retained pending action");
     Check(!schedule.Release(oldGeneration) && !schedule.EnterArrow(oldGeneration,2) && !schedule.Finish(oldGeneration),"stale generation accepted");
     float until=Time.time+1.3f;
     while(Time.time<until && passed)
     {
      yield return null;combat.ProcessDraugArrow();Position();
      Check(schedule.Current==DraugAttackSchedule.Action.None && schedule.ArrowIndex==next,"cancelled entry changed timer/index");
      Check(arrows.Count==(mode==0?0:released),"stale callback spawned arrow");
     }
     Check(visual.CurrentMotion==(mode==0?Skeleton1HVisual.Motion.Idle:Skeleton1HVisual.Motion.Hit),"stale return overwrote cancellation pose");
     owner.BlockCharacter(false);combat.SetGmCombat(false);cancels++;yield return null;
    }
   }
   finally
   {
    probe.Capture=null;UnityEngine.Object.Destroy(probe);
    owner.BlockCharacter(block);combat.SetGmCombat(false);visual.Play(Skeleton1HVisual.Motion.Idle,true);
    Clock("AttackRemaining",oldAttack);Clock("SkillRemaining",oldSkill);schedule.SetNextArrowIndex(oldIndex);
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.transform.position=op;target.transform.position=tp;UnityEngine.Random.state=random;Physics.SyncTransforms();
   }
   Check(cases==6 && cancels==6,"incomplete case count");
   Debug.Log(passed?"DRAUG_CONTROLLER_COMBAT_PASS sixAutomaticLateUpdateCases/entryTimer/sameFrameEventThreshold/noDuplicate/sourceIdleFinish/sixStageCancellationCases":"DRAUG_CONTROLLER_COMBAT_FAILED");
   report(passed);
  }
 }
}
#endif
