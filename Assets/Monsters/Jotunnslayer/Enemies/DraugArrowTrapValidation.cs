#if UNITY_EDITOR
using System;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
 internal static class DraugArrowTrapValidation
 {
  internal static bool Run(CharacterInstance owner,CharacterInstance target)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var tick=typeof(SkeletonBowArrow).GetMethod("FixedUpdate",flags);
   var caster=typeof(SkillBattlePreview).GetField("caster",flags);
   var op=owner.transform.position;var tp=target.transform.position;
   int oh=owner.CurrentHealth,th=target.CurrentHealth,hits=0;float dt=Time.fixedDeltaTime;
   DraugArrowAttack attack=null;GameObject wall=null;SkillBattlePreview runtime=null,previous=null;
   Health.SOnDamaged onHit=(hp,part,type,source,force)=>hits++;
   target.Server_OnDamaged+=onHit;
   foreach(var candidate in UnityEngine.Object.FindObjectsByType<SkillBattlePreview>(FindObjectsSortMode.None))
    if(caster.GetValue(candidate) as Health==target)previous=candidate;
   void Require(bool value,string reason){if(!value)throw new Exception(reason);}
   void Clear(){if(attack){attack.Cancel();UnityEngine.Object.DestroyImmediate(attack.gameObject);attack=null;}}
   SkeletonBowArrow Launch()
   {
    Physics.SyncTransforms();attack=DraugArrowAttack.Launch(owner,target,(target.CombatAimPoint-owner.CombatAimPoint).normalized,7);
    return attack.Flight;
   }
   try
   {
    var center=new Vector3(400,100,400);owner.CurrentHealth=target.CurrentHealth=1000;
    target.transform.position=center;
    runtime=new GameObject("Draug arrow Time Trap validation").AddComponent<SkillBattlePreview>();runtime.LiveMatch=true;
    runtime.Bind(target,new SkillProgress(SkillCatalog.Load(),null,20),null);runtime.SelectedRaidClass=RaidClass.Seeress;
    Require(runtime.TryChooseTrap(),"Time Trap setup failed");
    typeof(SkillBattlePreview).GetMethod("TryTimeTrap",flags).Invoke(runtime,new object[]{Time.time});
    wall=new GameObject("Draug arrow trap ordering wall");wall.layer=0;
    wall.transform.position=center+Vector3.right*5;wall.AddComponent<BoxCollider>().size=new Vector3(.1f,10,10);
    Time.fixedDeltaTime=3;
    owner.transform.position=center-Vector3.right*8;target.transform.position=center+Vector3.right*30;
    var flight=Launch();var audio=flight.GetComponent<AudioSource>();int children=attack.transform.childCount;
    tick.Invoke(flight,null);
    Require(!flight.enabled && !attack.enabled && !attack.InFlight && !audio.isPlaying && hits==0 &&
      attack.transform.childCount==children,"trap-before-wall did not cancel silently");Clear();
    owner.transform.position=center+Vector3.right*8;target.transform.position=center-Vector3.right*30;
    flight=Launch();audio=flight.GetComponent<AudioSource>();children=attack.transform.childCount;
    tick.Invoke(flight,null);
    Require(!flight.enabled && attack.enabled && !audio.isPlaying && hits==0 &&
      attack.transform.childCount>children && Mathf.Abs(flight.transform.position.x-(wall.transform.position.x+.55f))<.01f,
      "wall-before-trap lost earlier terrain end effect");Clear();
    wall.SetActive(false);owner.transform.position=center;target.transform.position=center+Vector3.right*30;
    flight=Launch();typeof(SkeletonBowArrow).GetField("speed",flags).SetValue(flight,0f);
    audio=flight.GetComponent<AudioSource>();children=attack.transform.childCount;tick.Invoke(flight,null);
    Require(!flight.enabled && !attack.enabled && !audio.isPlaying && hits==0 && attack.transform.childCount==children,
      "zero-distance arrow inside trap survived or spawned a tail");Clear();
    runtime.Bind(null,null,null);
    owner.transform.position=center-Vector3.right*8;target.transform.position=center+Vector3.right*8;
    flight=Launch();tick.Invoke(flight,null);
    Require(!flight.enabled && attack.enabled && hits==1 && target.CurrentHealth==993,
      "removing trap retained interception or changed normal arrow damage");
    Debug.Log("DRAUG_ARROW_TRAP_PASS trapBeforeWallSilent/wallBeforeTrapTail/zeroDistanceInside/removedTrapNormalHit/audioStopped");
    return true;
   }
   catch(Exception error){Debug.LogError("DRAUG_ARROW_TRAP_FAIL "+error);return false;}
   finally
   {
    Clear();if(wall)UnityEngine.Object.DestroyImmediate(wall);
    if(runtime){runtime.Bind(null,null,null);UnityEngine.Object.DestroyImmediate(runtime.gameObject);}
    if(previous)typeof(SkillBattlePreview).GetMethod("RegisterRaidStats",flags).Invoke(previous,null);
    target.Server_OnDamaged-=onHit;Time.fixedDeltaTime=dt;
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.transform.position=op;target.transform.position=tp;Physics.SyncTransforms();
   }
  }
 }
}
#endif
