#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
namespace Jotunnslayer
{
 internal static class DraugFlaskValidation
 {
  [Serializable] sealed class NativeMathSample {public float t,actual;}
  [Serializable] sealed class NativeMathEvidence {public NativeMathSample[] samples;}
  static void ValidateNativeTrajectory()
  {
   var evidence=JsonUtility.FromJson<NativeMathEvidence>(System.IO.File.ReadAllText("Docs/Validation/DraugBallisticMathVerification.json"));
   Require(evidence.samples!=null && evidence.samples.Length==257,"Missing native trajectory samples");
   var start=new Vector3(-3,2,1);var end=new Vector3(7,-1,5);float maxError=0;
   foreach(var sample in evidence.samples)
   {
    var actual=DraugFlaskAttack.EvaluateFlightPosition(start,end,sample.t);
    var expected=start+(end-start)*sample.t+Vector3.up*(5*sample.actual);
    maxError=Mathf.Max(maxError,Vector3.Distance(actual,expected));
   }
   Require(maxError<.00001f,$"Native trajectory mismatch {maxError}");
   Debug.Log($"DRAUG_NATIVE_TRAJECTORY_PASS samples=257 unequalEndpointHeights=True maxError={maxError}");
  }
  internal static System.Collections.IEnumerator RunAudio(CharacterInstance owner,CharacterInstance target,Action<bool> complete)
  {
   float oldScale=Time.timeScale;int oldHealth=target.CurrentHealth;
   Vector3 oldPosition=target.transform.position;DraugFlaskAttack attack=null;bool passed=true;
   void Check(bool result,string reason){if(!result){passed=false;Debug.LogError("DRAUG_AUDIO_LIFECYCLE_CHECK_FAILED "+reason);}}
   try
   {
    Time.timeScale=1;target.CurrentHealth=1000;
    attack=DraugFlaskAttack.Launch(owner,target,7);
    float deadline=Time.realtimeSinceStartup+4;
    while(attack && !attack.AreaActive && Time.realtimeSinceStartup<deadline)yield return null;
    Check(attack && attack.AreaActive,"area never began");
    if(!attack || !attack.AreaActive)yield break;
    target.transform.position=attack.LandingPoint+Vector3.right*4;
    var source=attack.CurrentAudio;
    yield return new WaitForSecondsRealtime(.1f);
    Check(source && source.isPlaying,"impact audio not playing");
    Time.timeScale=.5f;yield return new WaitForSecondsRealtime(.15f);
    Check(source && Mathf.Abs(source.pitch-.5f)<.001f,"half speed pitch mismatch");
    Time.timeScale=0;yield return new WaitForSecondsRealtime(.1f);
    int pausedSample=source?source.timeSamples:0;
    int pausedDamage=attack.DamageEvents;
    yield return new WaitForSecondsRealtime(.2f);
    Check(source && !source.isPlaying && Mathf.Abs(source.timeSamples-pausedSample)<64 && attack.DamageEvents==pausedDamage,
      "paused audio or damage advanced");
    Time.timeScale=1;yield return new WaitForSecondsRealtime(.1f);
    Check(source && source.isPlaying && Mathf.Abs(source.pitch-1)<.001f,"audio failed to resume");
    deadline=Time.realtimeSinceStartup+4;
    while(attack && attack.AreaActive && Time.realtimeSinceStartup<deadline)yield return null;
    Check(attack && attack.AudioTailActive && source && source.isPlaying,"impact tail truncated at area end");
    int tailDamage=attack?attack.DamageEvents:0;
    deadline=Time.realtimeSinceStartup+4;
    while(attack && Time.realtimeSinceStartup<deadline)
    {
     Check(attack.DamageEvents==tailDamage,"tail continued damage");yield return null;
    }
    Check(!attack,"finished tail retained attack");
    // Drive a second real attack into its tail and prove explicit cancellation stops it.
    attack=DraugFlaskAttack.Launch(owner,target,7);
    for(int i=0;i<400 && !attack.AudioTailActive && attack.enabled;i++)Tick.Invoke(attack,null);
    source=attack.CurrentAudio;Check(attack.AudioTailActive,"tail cancellation setup failed");
    attack.Cancel();Check(!attack.enabled && !attack.gameObject.activeSelf && (!source || !source.isPlaying),"tail cancellation retained sound");
    Debug.Log($"DRAUG_AUDIO_LIFECYCLE_{(passed?"PASS":"FAIL")} halfSpeed/pause/resume/naturalTail/noTailDamage/cancelTail");
   }
   finally
   {
    if(attack)attack.Cancel();Time.timeScale=oldScale;
    if(target){target.CurrentHealth=oldHealth;target.transform.position=oldPosition;}
    complete(passed);
   }
  }
  static readonly MethodInfo Tick=typeof(DraugFlaskAttack).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
  static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
  internal static bool Run(CharacterInstance owner,CharacterInstance target)
  {
   var attacks=new List<DraugFlaskAttack>();
   int health=target.CurrentHealth,sourceHealth=owner.CurrentHealth,team=target.Team,hits=0;
   Vector3 position=target.transform.position,ownerPosition=owner.transform.position;
   int reflectedHits=0;Health credited=null;
   Health.SOnDamaged onOwnerHit=(hp,part,type,source,force)=>{reflectedHits++;credited=source;};
   owner.Server_OnDamaged+=onOwnerHit;
   GameObject wall=null;SkillBattlePreview runtime=null,previous=null;
   const BindingFlags privateFields=BindingFlags.Instance|BindingFlags.NonPublic;
   var casterField=typeof(SkillBattlePreview).GetField("caster",privateFields);
   foreach(var candidate in UnityEngine.Object.FindObjectsByType<SkillBattlePreview>(FindObjectsSortMode.None))
    if(casterField.GetValue(candidate) as Health==target)previous=candidate;
   Health.SOnDamaged onHit=(hp,part,type,source,force)=>{if(source==owner)hits++;};target.Server_OnDamaged+=onHit;
   DraugFlaskAttack Launch(){var a=DraugFlaskAttack.Launch(owner,target,7);attacks.Add(a);return a;}
   void Advance(DraugFlaskAttack a,int count){for(int i=0;i<count && a.enabled;i++)Tick.Invoke(a,null);}
   void Land(DraugFlaskAttack a){for(int i=0;i<150 && !a.AreaActive && a.enabled;i++)Tick.Invoke(a,null);Require(a.AreaActive,"Flask failed to land");}
   try
   {
    ValidateNativeTrajectory();
    target.CurrentHealth=1000;hits=0;var inside=Launch();Land(inside);Require(hits==0,"Damage occurred before area tick");
    // Test area semantics at the actual impact, independently of intervening arena geometry.
    target.transform.position=inside.LandingPoint;Advance(inside,180);target.transform.position=position;
    Require(!inside.AreaActive && hits==12,$"Area lifetime expected12 pulses, actual={hits}");inside.Cancel();
    Debug.Log("DRAUG_AREA_INSIDE_CHECK hits=12 ended=True noPreLandingDamage=True");
    target.CurrentHealth=1000;hits=0;var outside=Launch();Land(outside);target.transform.position=outside.LandingPoint+Vector3.right*3;Advance(outside,180);
    Require(hits==0,"Outside actor damaged");target.transform.position=position;
    hits=0;var ally=Launch();target.Team=owner.Team;Land(ally);Advance(ally,180);Require(hits==0,"Friendly actor damaged");target.Team=team;
    foreach(int phase in new[]{0,1,2})
    {
     hits=0;var cancelled=Launch();
     if(phase==1)Advance(cancelled,8);else if(phase==2)Land(cancelled);
     cancelled.Cancel();Require(!cancelled.enabled && !cancelled.gameObject.activeSelf && hits==0,$"Cancel phase{phase} failed");
    }
    hits=0;var dead=Launch();Land(dead);owner.CurrentHealth=0;Advance(dead,1);Require(!dead.enabled && hits==0,"Dead caster retained area");
    owner.CurrentHealth=sourceHealth;
    Debug.Log("DRAUG_AREA_LIFECYCLE_PASS outside=0 friendly=0 cancelDelayFlightArea=True deathCleanup=True");
    // Isolated geometry exercises a long sweep without depending on the whitebox arena layout.
    Vector3 center=new Vector3(400,100,400);
    wall=new GameObject("Draug sweep validation wall");wall.layer=0;wall.transform.position=center;
    wall.AddComponent<BoxCollider>().size=new Vector3(.2f,10,10);Physics.SyncTransforms();
    hits=0;var blocked=Launch();
    Require(blocked.ResolveFlightSegment(center-Vector3.right*8,center+Vector3.right*8) && blocked.AreaActive &&
      Mathf.Abs(blocked.LandingPoint.x-(center.x-.1f))<.01f && hits==0,"Swept wall contact failed");blocked.Cancel();
    runtime=new GameObject("Draug trap validation").AddComponent<SkillBattlePreview>();runtime.LiveMatch=true;
    target.transform.position=center-Vector3.right*4;
    runtime.Bind(target,new SkillProgress(SkillCatalog.Load(),null,20),null);runtime.SelectedRaidClass=RaidClass.Seeress;
    Require(runtime.TryChooseTrap(),"Draug Time Trap setup failed");
    typeof(SkillBattlePreview).GetMethod("TryTimeTrap",privateFields).Invoke(runtime,new object[]{Time.time});
    var trapped=Launch();
    Require(trapped.ResolveFlightSegment(center-Vector3.right*10,center+Vector3.right*8) &&
      !trapped.enabled && !trapped.AreaActive && !trapped.gameObject.activeSelf && hits==0,"Trap before wall retained area");
    var wallFirst=Launch();
    Require(wallFirst.ResolveFlightSegment(center+Vector3.right*8,center-Vector3.right*10) && wallFirst.AreaActive &&
      Mathf.Abs(wallFirst.LandingPoint.x-(center.x+.1f))<.01f,"Later trap incorrectly consumed earlier wall impact");wallFirst.Cancel();
    var stationary=Launch();
    Require(stationary.ResolveFlightSegment(target.transform.position,target.transform.position) && !stationary.enabled,
      "Stationary flask inside trap survived");
    Debug.Log("DRAUG_FLASK_COLLISION_PASS sweptWall=True trapBeforeWall=cancel wallBeforeTrap=impact stationaryTrap=cancel damageEvents=0");
    wall.SetActive(false);runtime.Bind(null,null,null);
    owner.transform.position=center+Vector3.right*8;target.transform.position=center-Vector3.right*8;
    runtime.Bind(target,new SkillProgress(SkillCatalog.Load(),null,20),null);
    typeof(SkillBattlePreview).GetField("projectileReflectChance",privateFields).SetValue(runtime,100);
    Physics.SyncTransforms();owner.CurrentHealth=1000;target.CurrentHealth=1000;hits=reflectedHits=0;
    DraugFlaskAttack ReflectFlight()
    {
     var a=Launch();Advance(a,8);
     Vector3 aim=target.CombatAimPoint;
     Require(a.ResolveFlightSegment(aim-Vector3.right*2,aim+Vector3.right*2) && a.Reflected && a.InFlight,
       "Draug reflection did not restart flight");
     Require((a.LandingPoint-owner.transform.position).sqrMagnitude<.001f,"Reflected warning retained old destination");
     return a;
    }
    var reflected=ReflectFlight();Land(reflected);owner.transform.position=reflected.LandingPoint;Advance(reflected,180);
    Require(!reflected.AreaActive && reflectedHits==12 && hits==0 && credited==target,
      $"Reflected area attribution failed casterHits={reflectedHits} defenderHits={hits} source={credited==target}");
    reflected.Cancel();
    hits=reflectedHits=0;
    var stopReflected=ReflectFlight();stopReflected.Cancel();
    Require(!stopReflected.enabled && !stopReflected.gameObject.activeSelf && reflectedHits==0 && hits==0,"Reflected stop retained attack");
    var deadReflected=ReflectFlight();owner.CurrentHealth=0;Advance(deadReflected,1);
    Require(!deadReflected.enabled && reflectedHits==0 && hits==0,"Original caster death retained reflected attack");
    owner.CurrentHealth=1000;
    var deadDefender=ReflectFlight();target.CurrentHealth=0;Advance(deadDefender,1);
    Require(!deadDefender.enabled && reflectedHits==0 && hits==0,"Reflected damage source death retained attack");
    Debug.Log("DRAUG_REFLECTION_PASS casterPulses=12 defenderHits=0 attribution=True cancel=True casterDeath=True defenderDeath=True");return true;
   }
   catch(Exception e){Debug.LogException(e);Debug.LogError("DRAUG_AREA_LIFECYCLE_FAILED");return false;}
   finally
   {
    if(wall){wall.SetActive(false);UnityEngine.Object.Destroy(wall);}
    if(runtime){runtime.Bind(null,null,null);UnityEngine.Object.Destroy(runtime.gameObject);}
    if(previous)typeof(SkillBattlePreview).GetMethod("RegisterRaidStats",privateFields).Invoke(previous,null);
    target.Server_OnDamaged-=onHit;owner.Server_OnDamaged-=onOwnerHit;
    target.CurrentHealth=health;owner.CurrentHealth=sourceHealth;target.Team=team;target.transform.position=position;owner.transform.position=ownerPosition;
    foreach(var a in attacks)if(a)a.Cancel();
   }
  }
 }
}
#endif
