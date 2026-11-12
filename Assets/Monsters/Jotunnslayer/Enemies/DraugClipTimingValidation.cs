#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Jotunnslayer
{
 internal static class DraugLegacyClipTimingValidation
 {
  internal static IEnumerator Run(CharacterInstance owner,CharacterInstance target,Action<bool> report)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var combat=owner.GetComponent<Skeleton1HCombat>();
   var type=typeof(Skeleton1HCombat);
   var visual=(Skeleton1HVisual)type.GetField("visual",flags).GetValue(combat);
   var schedule=(DraugAttackSchedule)type.GetField("draugSchedule",flags).GetValue(combat);
   var arrows=(List<DraugArrowAttack>)type.GetField("draugArrows",flags).GetValue(combat);
   var update=type.GetMethod("Update",flags);
   var waitingField=type.GetField("gmWaiting",flags);
   void Tick(){waitingField.SetValue(combat,false);try{update.Invoke(combat,null);}finally{waitingField.SetValue(combat,true);}}
   var phase=type.GetField("phase",flags);var ends=type.GetField("phaseEnds",flags);
   var clipField=typeof(Skeleton1HVisual).GetField("currentClip",flags);
   bool waiting=(bool)type.GetField("gmWaiting",flags).GetValue(combat);
   if(!waiting){Debug.LogError("DRAUG_CLIP_TIMING_FAIL requires stopped combat");report(false);yield break;}
   Vector3 op=owner.transform.position,tp=target.transform.position;
   int oh=owner.CurrentHealth,th=target.CurrentHealth;
   bool oldBlock=owner.Block;
   bool passed=true;var random=UnityEngine.Random.state;
   float oldAttack=schedule.AttackRemaining,oldSkill=schedule.SkillRemaining;int oldIndex=schedule.ArrowIndex;
   void Clock(string name,float value)=>typeof(DraugAttackSchedule).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(schedule,value);
   void Check(bool condition,string reason){if(condition)return;passed=false;Debug.LogError("DRAUG_CLIP_TIMING_FAIL "+reason);}
   void Position()
   {
    owner.transform.position=new Vector3(400,100,400);target.transform.position=new Vector3(400,100,406);
    owner.CurrentHealth=target.CurrentHealth=1000;Physics.SyncTransforms();
   }
   try
   {
    // Disabling the component calls Stop and destroys bindings; gate automatic ticks with GM waiting instead.
    // Independent source Animator observations (1ms steps), not copied model deadlines.
    float[] idleObserved={.507f,.525f,.391f}, chaseObserved={.667f,.685f,.551f};
    for(int entry=0;entry<2 && passed;entry++)
    for(int index=0;index<3 && passed;index++)
    {
     bool chase=entry==1;
     Position();combat.SetGmCombat(true);
     visual.Play(chase?Skeleton1HVisual.Motion.Walk:Skeleton1HVisual.Motion.Idle,true);
     Clock("AttackRemaining",0);Clock("SkillRemaining",100);schedule.SetNextArrowIndex(index);
     type.GetField("nextAttack",flags).SetValue(combat,Time.time-1);
     ends.SetValue(combat,Time.time-1);
     float start=Time.time;Tick();
     Check(phase.GetValue(combat).ToString()=="Windup" && schedule.Current==DraugAttackSchedule.Action.Arrow,"arrow did not enter windup");
     if(!passed)break;
     var entered=type.GetField("draugEntered",flags);
     float maxFrame=Time.deltaTime;
     if(chase)
     {
      Check(!(bool)entered.GetValue(combat) && visual.CurrentMotion==Skeleton1HVisual.Motion.Idle,"chase skipped stand decision");
      Check(schedule.ArrowIndex==index,"next index randomized before attack entry");
      while(!(bool)entered.GetValue(combat) && passed)
      {
       Check(arrows.Count==0,"released during decision");
       yield return null;maxFrame=Mathf.Max(maxFrame,Time.deltaTime);Position();Tick();
       if(Time.time-start>1){Check(false,"decision never entered attack");break;}
      }
      Check(Time.time-start>=DraugArrowTimeline.ChaseToDecideSeconds,"attack entered before chase transition ended");
     }
     if(!passed)break;
     float actualEntry=(float)type.GetField("draugEntryAt",flags).GetValue(combat);
     Check(Mathf.Abs(schedule.AttackRemaining-4)<.0002f,"attack entry did not reset timer");
     float deadline=(float)ends.GetValue(combat);
     var playing=(AnimationClipPlayable)clipField.GetValue(visual);
     Check(playing.IsValid() && playing.GetAnimationClip()==visual.DraugArrowClip(index) && Math.Abs(playing.GetSpeed()-1)<.0001,"wrong current animation index="+index);
     var entryMixer=(AnimationMixerPlayable)typeof(Skeleton1HVisual).GetField("transitionMixer",flags).GetValue(visual);
     Check(entryMixer.IsValid(),"attack entry has no blend mixer");
     if(entryMixer.IsValid())Check(chase?entryMixer.GetInput(0).GetSpeed()!=0:entryMixer.GetInput(0).GetSpeed()==0,"incorrect interrupted idle snapshot policy");
     Check((int)type.GetField("draugArrowIndex",flags).GetValue(combat)==index,"next randomized index replaced current index");
     float offset=DraugArrowTimeline.EntryNormalizedTime(index)*playing.GetAnimationClip().length;
     Check(Math.Abs(playing.GetTime()-offset)<.0002,"attack did not start at source clip offset");
     float expectedObserved=chase?chaseObserved[index]:idleObserved[index];
     Check(Mathf.Abs(deadline-start-expectedObserved)<=(chase?maxFrame:0)+.004f,"release differs from independent source observation");
     while((Time.time<deadline || visual.CurrentClipTime<DraugArrowTimeline.ClipEventSeconds(index)) && passed)
     {
      Check(arrows.Count==0,"arrow released before event index="+index);
      yield return null;maxFrame=Mathf.Max(maxFrame,Time.deltaTime);
      if(Time.time>=deadline && visual.CurrentClipTime>=DraugArrowTimeline.ClipEventSeconds(index))break;
      Position();Tick();
     }
     if(!passed)break;
     Position();Tick();
     float releasedAt=Time.time;
     Check(arrows.Count==1 && phase.GetValue(combat).ToString()=="Recovery","release missing or duplicated index="+index);
     Check(releasedAt>=deadline && releasedAt-deadline<=maxFrame*2+.004f,"release exceeded two-frame evaluation tolerance");
     playing=(AnimationClipPlayable)clipField.GetValue(visual);
     double animationTime=playing.GetTime();
     Check(animationTime>=DraugArrowTimeline.ClipEventSeconds(index),"released before actual clip event time");
     Check(Math.Abs(animationTime-(releasedAt-actualEntry+offset))<=Time.deltaTime*2+.03,"playable clock diverged index="+index);
     Tick();Check(arrows.Count==1,"same-frame second tick spawned a duplicate");
     float returnAt=(float)type.GetField("draugReturnAt",flags).GetValue(combat);
     float[] returnObserved=chase?new[]{1.230f,1.206f,1.174f}:new[]{1.070f,1.046f,1.014f};
     Check(Mathf.Abs(returnAt-start-returnObserved[index])<=(chase?maxFrame:0)+.004f,"return differs from source observation");
     while(visual.CurrentMotion==Skeleton1HVisual.Motion.Attack && passed)
     {
      yield return null;Position();
      double beforeReturn=visual.CurrentClipTime;Tick();
      if(visual.CurrentMotion==Skeleton1HVisual.Motion.Idle)
       Check(Time.time>=returnAt && beforeReturn>=DraugArrowTimeline.ExitNormalizedTime*visual.DraugArrowClip(index).length,"returned before actual clip threshold");
      if(Time.time-start>2){Check(false,"return never started");break;}
     }
     Check(visual.CurrentMotion==Skeleton1HVisual.Motion.Idle,"missing idle blend at exit threshold");
     Check(schedule.Current==DraugAttackSchedule.Action.Arrow,"finished before idle blend");
     float finishAt=(float)ends.GetValue(combat);
     Check(Mathf.Abs(finishAt-Time.time-.1f)<.0002,"incorrect return blend duration");
     while(Time.time<finishAt && passed){yield return null;Position();Tick();}
     Check(schedule.Current==DraugAttackSchedule.Action.None,"action did not finish after return blend");
     Debug.Log($"DRAUG_CLIP_TIMING_CASE entry={(chase?"chase":"idle")} index={index} sourceObserved={expectedObserved} entryDelay={actualEntry-start} elapsed={releasedAt-start} maxFrame={maxFrame} animationTime={animationTime} returnAt={returnAt-start} arrows={arrows.Count}");
     combat.SetGmCombat(false);yield return null;
    }
    // A cancelled decision must never enter attack or consume the next index later.
    Position();combat.SetGmCombat(true);visual.Play(Skeleton1HVisual.Motion.Walk,true);
    Clock("AttackRemaining",0);Clock("SkillRemaining",100);schedule.SetNextArrowIndex(2);
    type.GetField("nextAttack",flags).SetValue(combat,Time.time-1);ends.SetValue(combat,Time.time-1);
    Tick();uint cancelled=schedule.Generation;
    Check(!(bool)type.GetField("draugEntered",flags).GetValue(combat),"cancel case skipped decision");
    combat.SetGmCombat(false);
    yield return new WaitForSeconds(.2f);
    Check(schedule.Current==DraugAttackSchedule.Action.None && schedule.ArrowIndex==2 && arrows.Count==0 && !schedule.Release(cancelled),"cancelled decision executed stale action");
    Check(type.GetField("draugPendingClip",flags).GetValue(combat)==null && visual.CurrentMotion==Skeleton1HVisual.Motion.Idle,"cancelled decision retained playback");
    // Exercise cancellation at attack entry, after release, and during the return blend.
    for(int mode=0;mode<2 && passed;mode++)
    for(int stage=0;stage<3 && passed;stage++)
    {
     owner.BlockCharacter(false);Position();combat.SetGmCombat(true);
     visual.Play(Skeleton1HVisual.Motion.Idle,true);
     Clock("AttackRemaining",0);Clock("SkillRemaining",100);schedule.SetNextArrowIndex(0);
     type.GetField("nextAttack",flags).SetValue(combat,Time.time-1);ends.SetValue(combat,Time.time-1);
     Tick();float began=Time.time;
     Check((bool)type.GetField("draugEntered",flags).GetValue(combat),"cancellation setup did not enter attack");
     while(stage>0 && arrows.Count==0 && passed)
     {
      yield return null;Position();Tick();
      if(Time.time-began>2){Check(false,"cancellation setup did not release");break;}
     }
     while(stage==2 && visual.CurrentMotion!=Skeleton1HVisual.Motion.Idle && passed)
     {
      yield return null;Position();Tick();
      if(Time.time-began>2){Check(false,"cancellation setup did not return");break;}
     }
     uint oldGeneration=schedule.Generation;int nextIndex=schedule.ArrowIndex;
     int releasedCount=arrows.Count;
     if(mode==0)combat.SetGmCombat(false);
     else {owner.BlockCharacter(true);Tick();}
     Check(schedule.Current==DraugAttackSchedule.Action.None,"cancel retained active schedule");
     Check(type.GetField("draugPendingClip",flags).GetValue(combat)==null &&
       !(bool)type.GetField("draugEntered",flags).GetValue(combat) &&
       !(bool)type.GetField("draugReturning",flags).GetValue(combat),"cancel retained animation stage");
     foreach(string timer in new[]{"draugEntryAt","draugReturnAt","draugFinishAt"})
      Check((float)type.GetField(timer,flags).GetValue(combat)==0,"cancel retained "+timer);
     Check(!schedule.Release(oldGeneration) && !schedule.EnterArrow(oldGeneration,2) && !schedule.Finish(oldGeneration),"cancel accepted stale action callback");
     var graph=(PlayableGraph)typeof(Skeleton1HVisual).GetField("graph",flags).GetValue(visual);
     Check(graph.GetPlayableCount()==1,"cancel retained blend subtree");
     float observeUntil=Time.time+1.3f;
     while(Time.time<observeUntil && passed)
     {
      yield return null;
      if(mode==1)Tick();
      Check(schedule.Current==DraugAttackSchedule.Action.None && schedule.ArrowIndex==nextIndex,"stale cancellation changed schedule/index");
      Check(arrows.Count==(mode==0?0:releasedCount),"cancel spawned a new arrow");
     }
     Check(visual.CurrentMotion==(mode==0?Skeleton1HVisual.Motion.Idle:Skeleton1HVisual.Motion.Hit),"old return transition overwrote cancellation pose");
     Debug.Log($"DRAUG_ARROW_CANCEL_CASE mode={(mode==0?"gmStop":"block")} stage={stage} releasedBeforeCancel={releasedCount} pendingCleared=True staleRejected=True blendNodes={graph.GetPlayableCount()}");
     owner.BlockCharacter(false);combat.SetGmCombat(false);yield return null;
    }
   }
   finally
   {
    owner.BlockCharacter(oldBlock);
    combat.SetGmCombat(false);visual.Play(Skeleton1HVisual.Motion.Idle,true);
    Clock("AttackRemaining",oldAttack);Clock("SkillRemaining",oldSkill);schedule.SetNextArrowIndex(oldIndex);
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.transform.position=op;target.transform.position=tp;
    UnityEngine.Random.state=random;Physics.SyncTransforms();
   }
   Debug.Log(passed?"DRAUG_CLIP_TIMING_PASS sixIdleChaseCases/sourceOffsets/idleSnapshot/entryTimer/sourceObservedRelease/actualClipThresholds/returnBlend/cancelDecision/sixStageCancellationCases":"DRAUG_CLIP_TIMING_FAILED");
   report(passed);
  }
 }
}
#endif
