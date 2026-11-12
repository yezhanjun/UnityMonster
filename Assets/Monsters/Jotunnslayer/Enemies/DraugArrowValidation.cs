#if UNITY_EDITOR
using System;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
 internal static class DraugArrowValidation
 {
  internal static bool Run(CharacterInstance owner,CharacterInstance target)
  {
   var tick=typeof(SkeletonBowArrow).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
   var update=typeof(DraugArrowAttack).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
   Vector3 op=owner.transform.position,tp=target.transform.position;int oh=owner.CurrentHealth,th=target.CurrentHealth;
   DraugArrowAttack attack=null;
   void Require(bool valid,string reason){if(!valid)throw new Exception(reason);}
   try
   {
    owner.transform.position=new Vector3(0,40,0);target.transform.position=new Vector3(0,40,3);target.CurrentHealth=1000;Physics.SyncTransforms();
    Vector3 direction=(target.CombatAimPoint-owner.CombatAimPoint).normalized;
    attack=DraugArrowAttack.Launch(owner,target,direction,7);var flight=attack.Flight;
    Require(flight.GetComponentsInChildren<ParticleSystem>(true).Length==4,"projectile source FX count");
    Require(!flight.GetComponent<MeshRenderer>().enabled,"placeholder mesh visible");
    var audio=flight.GetComponent<AudioSource>();Require(audio.clip && audio.clip.name=="OriginalDraugArrowFlight" && audio.maxDistance==20,"source audio binding");
    for(int i=0;i<200 && flight.enabled;i++)tick.Invoke(flight,null);
    Require(!attack.InFlight && target.CurrentHealth==993,"single target hit");
    attack.Cancel();UnityEngine.Object.DestroyImmediate(attack.gameObject);attack=null;
    target.CurrentHealth=1000;
    attack=DraugArrowAttack.Launch(owner,target,direction,7);flight=attack.Flight;audio=flight.GetComponent<AudioSource>();
    attack.Cancel();Require(!flight.enabled && !audio.isPlaying && target.CurrentHealth==1000,"cancel flight/audio");
    UnityEngine.Object.DestroyImmediate(attack.gameObject);attack=null;
    attack=DraugArrowAttack.Launch(owner,target,direction,7);flight=attack.Flight;
    owner.CurrentHealth=0;update.Invoke(attack,null);
    Require(!flight.enabled && !attack.InFlight,"original owner death cleanup");
    Debug.Log("DRAUG_ARROW_PASS sourceFX/audio/singleHit/cancel/ownerDeath");return true;
   }
   catch(Exception e){Debug.LogError("DRAUG_ARROW_FAIL "+e);return false;}
   finally
   {
    if(attack){attack.Cancel();UnityEngine.Object.DestroyImmediate(attack.gameObject);}
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.transform.position=op;target.transform.position=tp;Physics.SyncTransforms();
   }
  }
 }
}
#endif
