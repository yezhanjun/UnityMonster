#if UNITY_EDITOR
using System;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
 internal static class DraugArrowContactValidation
 {
  internal static bool Run(CharacterInstance owner,CharacterInstance target)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var tick=typeof(SkeletonBowArrow).GetMethod("FixedUpdate",flags);
   var sourceField=typeof(SkeletonBowArrow).GetField("source",flags);
   var casterField=typeof(SkillBattlePreview).GetField("caster",flags);
   var update=typeof(DraugArrowAttack).GetMethod("Update",flags);
   Vector3 op=owner.transform.position,tp=target.transform.position;
   int oh=owner.CurrentHealth,th=target.CurrentHealth;float dt=Time.fixedDeltaTime;
   DraugArrowAttack attack=null;GameObject wall=null;SkillBattlePreview runtime=null,previous=null;
   Health credited=null;int hits=0;
   Health.SOnDamaged onHit=(hp,part,type,source,force)=>{hits++;credited=source;};
   owner.Server_OnDamaged+=onHit;
   foreach(var candidate in UnityEngine.Object.FindObjectsByType<SkillBattlePreview>(FindObjectsSortMode.None))
    if(casterField.GetValue(candidate) as Health==target)previous=candidate;
   void Require(bool value,string reason){if(!value)throw new Exception(reason);}
   void Clear(){if(attack){attack.Cancel();UnityEngine.Object.DestroyImmediate(attack.gameObject);attack=null;}}
   SkeletonBowArrow Launch()
   {
    Physics.SyncTransforms();
    attack=DraugArrowAttack.Launch(owner,target,(target.CombatAimPoint-owner.CombatAimPoint).normalized,7);
    return attack.Flight;
   }
   void Reflect(SkeletonBowArrow flight)
   {
    for(int i=0;i<100 && flight.enabled && sourceField.GetValue(flight) as Health==owner;i++)tick.Invoke(flight,null);
    Require(flight.enabled && sourceField.GetValue(flight) as Health==target,"reflection did not transfer source");
   }
   try
   {
    owner.transform.position=new Vector3(400,100,400);target.transform.position=owner.transform.position+Vector3.forward*30;
    owner.CurrentHealth=target.CurrentHealth=1000;
    var flight=Launch();Vector3 origin=flight.transform.position;
    for(int i=0;i<200 && flight.enabled;i++)tick.Invoke(flight,null);
    Require(!flight.enabled && Mathf.Abs(Vector3.Distance(origin,flight.transform.position)-18)<.002f && target.CurrentHealth==1000,"lifetime/distance mismatch");Clear();
    target.transform.position=owner.transform.position+Vector3.forward*8;
    wall=new GameObject("Draug arrow thin wall");wall.layer=0;wall.transform.position=owner.CombatAimPoint+Vector3.forward*3;
    wall.AddComponent<BoxCollider>().size=new Vector3(10,10,.1f);
    Time.fixedDeltaTime=1f;
    flight=Launch();tick.Invoke(flight,null);
    Require(!flight.enabled && target.CurrentHealth==1000 && Mathf.Abs(flight.transform.position.z-(wall.transform.position.z-.55f))<.01f,"long sweep through wall");Clear();
    wall.SetActive(false);Time.fixedDeltaTime=dt;
    target.transform.position=owner.transform.position+Vector3.forward*3;
    runtime=new GameObject("Draug arrow reflection validation").AddComponent<SkillBattlePreview>();runtime.LiveMatch=true;
    runtime.Bind(target,new SkillProgress(SkillCatalog.Load(),null,20),null);
    typeof(SkillBattlePreview).GetField("projectileReflectChance",flags).SetValue(runtime,100);
    flight=Launch();Reflect(flight);
    for(int i=0;i<100 && flight.enabled;i++)tick.Invoke(flight,null);
    Require(!flight.enabled && hits==1 && credited==target && owner.CurrentHealth==993 && target.CurrentHealth==1000,"reflected damage/attribution");Clear();
    owner.CurrentHealth=1000;hits=0;
    flight=Launch();Reflect(flight);owner.CurrentHealth=0;update.Invoke(attack,null);
    Require(!flight.enabled && hits==0,"original owner death after reflection");Clear();owner.CurrentHealth=1000;
    flight=Launch();Reflect(flight);target.CurrentHealth=0;tick.Invoke(flight,null);
    Require(!flight.enabled && hits==0,"reflected damage source death");Clear();target.CurrentHealth=1000;
    flight=Launch();Reflect(flight);attack.Cancel();
    Require(!flight.enabled && hits==0,"cancel after reflection");
    Debug.Log("DRAUG_ARROW_CONTACT_PASS lifetime18m/thinWall/reflectionAttribution/originalOwnerDeath/reflectedSourceDeath/cancel");return true;
   }
   catch(Exception e){Debug.LogError("DRAUG_ARROW_CONTACT_FAIL "+e);return false;}
   finally
   {
    Clear();if(wall){wall.SetActive(false);UnityEngine.Object.Destroy(wall);}
    if(runtime){runtime.Bind(null,null,null);UnityEngine.Object.Destroy(runtime.gameObject);}
    if(previous)typeof(SkillBattlePreview).GetMethod("RegisterRaidStats",flags).Invoke(previous,null);
    owner.Server_OnDamaged-=onHit;Time.fixedDeltaTime=dt;
    owner.CurrentHealth=oh;target.CurrentHealth=th;owner.transform.position=op;target.transform.position=tp;Physics.SyncTransforms();
   }
  }
 }
}
#endif
