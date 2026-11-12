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
 [DefaultExecutionOrder(-32000)]
 internal sealed class DraugLateInterruptionProbe:MonoBehaviour
 {
  internal Action Inject;
  void LateUpdate(){var action=Inject;Inject=null;action?.Invoke();}
 }
 internal static class DraugLateCancellationValidation
 {
  internal static IEnumerator Run(CharacterInstance owner,CharacterInstance target,Action<bool> report)
  {
   const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
   var combat=owner.GetComponent<Skeleton1HCombat>();var t=typeof(Skeleton1HCombat);
   var visual=(Skeleton1HVisual)t.GetField("visual",f).GetValue(combat);
   var schedule=(DraugAttackSchedule)t.GetField("draugSchedule",f).GetValue(combat);
   var arrows=(List<DraugArrowAttack>)t.GetField("draugArrows",f).GetValue(combat);
   var flasks=(List<DraugFlaskAttack>)t.GetField("draugFlasks",f).GetValue(combat);
   if(!(bool)t.GetField("gmWaiting",f).GetValue(combat)){report(false);yield break;}
   var injection=owner.gameObject.AddComponent<DraugLateInterruptionProbe>();
   var capture=owner.gameObject.AddComponent<DraugCombatFrameProbe>();
   bool passed=true;int cases=0,observedFrame=-1;bool cleared=false,staleRejected=false;uint generation=0;Action restoreTransient=null;
   capture.Capture=()=>{observedFrame=Time.frameCount;cleared=schedule.Current==DraugAttackSchedule.Action.None && t.GetField("draugPendingClip",f).GetValue(combat)==null && !(bool)t.GetField("draugCastPending",f).GetValue(combat);staleRejected=!schedule.Release(generation) && !schedule.CastEnd(generation);restoreTransient?.Invoke();restoreTransient=null;};
   var ownedField=t.GetField("draugRandom",f);var savedOwned=ownedField.GetValue(combat);
   var random=UnityEngine.Random.state;Vector3 op=owner.transform.position,tp=target.transform.position;int oh=owner.CurrentHealth,th=target.CurrentHealth;bool block=owner.Block;
   float oldAttack=schedule.AttackRemaining,oldSkill=schedule.SkillRemaining;int oldIndex=schedule.ArrowIndex;
   void Clock(string name,float value)=>typeof(DraugAttackSchedule).GetProperty(name,f).SetValue(schedule,value);
   void Check(bool value,string why){if(!value){passed=false;Debug.LogError("DRAUG_LATE_CANCEL_FAIL "+why);}}
   try
   {
    for(int action=0;action<2 && passed;action++)
    for(int mode=0;mode<5 && passed;mode++)
    {
     owner.CurrentHealth=target.CurrentHealth=1000;owner.transform.position=new Vector3(400,100,400);target.transform.position=new Vector3(400,100,406);Physics.SyncTransforms();
     owner.BlockCharacter(false);combat.SetGmCombat(true);visual.Play(Skeleton1HVisual.Motion.Hit,true);visual.Play(Skeleton1HVisual.Motion.Idle,true);
     Clock("AttackRemaining",0);Clock("SkillRemaining",action==0?100:0);
     if(action==1)ownedField.SetValue(combat,new DraugRandomSource(0));
     t.GetField("nextAttack",f).SetValue(combat,Time.time-1);t.GetField("phaseEnds",f).SetValue(combat,Time.time-1);
     t.GetMethod("Update",f).Invoke(combat,null);generation=schedule.Generation;
     Check(schedule.Current==(action==0?DraugAttackSchedule.Action.Arrow:DraugAttackSchedule.Action.Flask),"setup wrong action");
     // Prime the live Animator to just before its release event. The real PlayerLoop evaluates next.
     var animator=visual.GetComponent<Animator>();animator.Update(0);animator.Update(0);
     float threshold=action==0?DraugArrowTimeline.ClipEventSeconds((int)t.GetField("draugArrowIndex",f).GetValue(combat)):Skeleton1HVisual.DraugCastReleaseSeconds;
     string stateName=action==0?"base.attack.attack_0"+((int)t.GetField("draugArrowIndex",f).GetValue(combat)+1):"base.cast_tree";
     float length=action==0?visual.DraugArrowClip((int)t.GetField("draugArrowIndex",f).GetValue(combat)).length:visual.AttackDuration;
     animator.Play(stateName,0,(threshold-.00001f)/length);animator.Update(0);
     int injectionFrame=-1;bool reachedRelease=false;int leaseCount=DraugControllerResources.OwnerCount;
     injection.Inject=()=>
     {
      injectionFrame=Time.frameCount;
      if(action==0)visual.SampleDraugArrow();else visual.SampleDraugCast();
      reachedRelease=visual.CurrentClipTime>=threshold;
      if(mode==0){owner.BlockCharacter(true);restoreTransient=()=>owner.BlockCharacter(false);}
      else if(mode==1)visual.enabled=false;
      else if(mode==2){owner.CurrentHealth=0;restoreTransient=()=>owner.CurrentHealth=1000;}
      else if(mode==3){target.CurrentHealth=0;restoreTransient=()=>target.CurrentHealth=1000;}
      else combat.enabled=false;
     };
     yield return null;
     Check(injectionFrame==observedFrame && observedFrame==Time.frameCount-1,"late injection/sample frame mismatch");
     Check(reachedRelease,"injection did not reach release threshold");
     Check(cleared && staleRejected,"pending action survives late interruption mode="+mode+" action="+action);
     Check(arrows.Count==0 && flasks.Count==0,"late interruption released projectile");
     if(mode==1)Check(!visual.DraugControllerActive && DraugControllerResources.OwnerCount==leaseCount-1,"disabled visual retained lease");
     if(mode==1)
     {
      float until=Time.time+4.2f;
      while(Time.time<until && passed)
      {
       yield return null;
       Check(schedule.Current==DraugAttackSchedule.Action.None && !visual.DraugControllerActive && arrows.Count==0 && flasks.Count==0,"disabled visual started a new action after cooldown");
      }
     }
     // Restore the shared fixture; this is not a production respawn/rebind assertion.
     owner.CurrentHealth=target.CurrentHealth=1000;owner.BlockCharacter(false);
     if(mode==4){t.GetField("body",f).SetValue(combat,owner);t.GetField("target",f).SetValue(combat,target);combat.enabled=true;}
     visual.enabled=true;combat.SetGmCombat(false);
     combat.ProcessDraugArrow();combat.ProcessDraugCast();
     Check(arrows.Count==0 && flasks.Count==0,"restoration revived stale action");
     Debug.Log($"DRAUG_LATE_CANCEL_CASE action={action} mode={mode} cancelledInInjectedFrame={cleared} noProjectile=True");cases++;
     yield return null;
    }
   }
   finally
   {
    restoreTransient?.Invoke();restoreTransient=null;injection.Inject=null;capture.Capture=null;UnityEngine.Object.Destroy(injection);UnityEngine.Object.Destroy(capture);
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.BlockCharacter(block);visual.enabled=true;
    if(!combat.enabled){t.GetField("body",f).SetValue(combat,owner);t.GetField("target",f).SetValue(combat,target);combat.enabled=true;}
    combat.SetGmCombat(false);Clock("AttackRemaining",oldAttack);Clock("SkillRemaining",oldSkill);schedule.SetNextArrowIndex(oldIndex);
    ownedField.SetValue(combat,savedOwned);
    owner.transform.position=op;target.transform.position=tp;UnityEngine.Random.state=random;Physics.SyncTransforms();
   }
   Check(cases==10,"incomplete late interruption cases");
   Debug.Log(passed?"DRAUG_LATE_CANCEL_PASS tenCases/block/visualDisableBeyondCooldown/ownerDeathFlag/targetDeathFlag/combatDisable/noLateRelease":"DRAUG_LATE_CANCEL_FAILED");report(passed);
  }
 }
}
#endif
