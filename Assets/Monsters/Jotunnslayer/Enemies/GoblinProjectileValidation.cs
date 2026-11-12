#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
namespace Jotunnslayer
{
    internal static class GoblinProjectileValidation
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly MethodInfo Tick=typeof(SkeletonBowArrow).GetMethod("FixedUpdate",Private);
        static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static bool Run(CharacterInstance caster,CharacterInstance defender)
        {
            var flights=new List<SkeletonBowArrow>();
            var ownerField=typeof(SkillBattlePreview).GetField("caster",Private);
            var register=typeof(SkillBattlePreview).GetMethod("RegisterRaidStats",Private);
            SkillBattlePreview previous=null,runtime=null;
            foreach(var candidate in UnityEngine.Object.FindObjectsByType<SkillBattlePreview>(FindObjectsSortMode.None))
                if(ownerField.GetValue(candidate) as Health==defender)previous=candidate;
            Vector3 defenderPosition=defender.transform.position;
            int casterHealth=caster.CurrentHealth,defenderHealth=defender.CurrentHealth;
            int casterHits=0,defenderHits=0;Health credited=null;
            Health.SOnDamaged onCaster=(hp,part,type,attacker,force)=>{casterHits++;credited=attacker;};
            Health.SOnDamaged onDefender=(hp,part,type,attacker,force)=>defenderHits++;
            caster.Server_OnDamaged+=onCaster;defender.Server_OnDamaged+=onDefender;
            SkeletonBowArrow NewFlight(int phase)
            {
                Vector3 direction=(defender.CombatAimPoint-caster.CombatAimPoint).normalized;
                var flight=SkeletonBowArrow.Launch(caster,defender,direction,7,true);
                flight.ConfigureGoblinReturn(null);
                flight.transform.position=defender.CombatAimPoint+direction*(phase==2?.2f:-.2f);
                Set(flight,"goblinPhase",phase);Set(flight,"goblinPhaseTime",1f);
                flights.Add(flight);return flight;
            }
            try
            {
                runtime=new GameObject("Goblin interaction validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch=true;
                runtime.Bind(defender,new SkillProgress(SkillCatalog.Load(),null,20),null);
                Set(runtime,"projectileReflectChance",100);
                foreach(int phase in new[]{0,2})
                {
                    casterHits=defenderHits=0;credited=null;
                    var flight=NewFlight(phase);
                    Tick.Invoke(flight,null);
                    Require(flight.enabled && !flight.GoblinReturning,"Reflection failed to leave homing return state");
                    for(int i=0;i<800 && flight.enabled;i++)Tick.Invoke(flight,null);
                    Require(!flight.enabled && casterHits==1 && defenderHits==0 && credited==defender,
                        $"Reflection phase={phase}: casterHits={casterHits} defenderHits={defenderHits} attribution={credited==defender}");
                    Debug.Log($"GOBLIN_REFLECTION_CHECK phase={phase} casterHits=1 defenderHits=0 attribution=True consumed=True");
                }
                casterHits=defenderHits=0;
                var cancelled=NewFlight(0);Tick.Invoke(cancelled,null);cancelled.CancelFlight();
                Require(!cancelled.enabled && casterHits==0 && defenderHits==0,"Reflected projectile cancellation failed");
                Set(runtime,"projectileReflectChance",0);
                runtime.SelectedRaidClass=RaidClass.Seeress;
                Require(runtime.TryChooseTrap(),"Time Trap setup unavailable");
                typeof(SkillBattlePreview).GetMethod("TryTimeTrap",Private).Invoke(runtime,new object[]{Time.time});
                foreach(int phase in new[]{0,1,2})
                {
                    casterHits=defenderHits=0;
                    var flight=NewFlight(phase);Tick.Invoke(flight,null);
                    Require(!flight.enabled && casterHits==0 && defenderHits==0,$"Time Trap phase {phase} allowed damage or retained projectile");
                    Debug.Log($"GOBLIN_TRAP_CHECK phase={phase} intercepted=True damageEvents=0");
                }
                var goblinTick=typeof(SkeletonBowArrow).GetMethod("TickGoblinReturn",Private);
                Vector3 axis=(defender.CombatAimPoint-caster.CombatAimPoint).normalized;
                Vector3 trapAim=defender.CombatAimPoint;
                casterHits=defenderHits=0;
                var crossing=NewFlight(0);crossing.transform.position=trapAim-axis*6f;
                goblinTick.Invoke(crossing,new object[]{1f});
                Require(!crossing.enabled && defenderHits==0,"Trap entry after a long step failed to intercept before target");
                defender.transform.position-=axis*4f;
                var targetFirst=NewFlight(0);targetFirst.transform.position=trapAim-axis*6f;Set(targetFirst,"direction",axis);
                goblinTick.Invoke(targetFirst,new object[]{1f});
                Require(targetFirst.enabled && defenderHits==1 && Vector3.Distance(targetFirst.transform.position,defender.CombatAimPoint)<.501f,
                    "Non-piercing target hit incorrectly continued to later trap contact");
                targetFirst.CancelFlight();
                Debug.Log("GOBLIN_CONTACT_ORDER_CHECK trapFirst=noDamage targetFirst=oneHitAndHold");
                Debug.Log("GOBLIN_INTERACTION_PASS outbound/return reflection attribution, reflected cancellation, outbound/hold/return trap interception");
                return true;
            }
            catch(Exception error){Debug.LogException(error);Debug.LogError("GOBLIN_INTERACTION_FAILED");return false;}
            finally
            {
                foreach(var flight in flights)if(flight)flight.CancelFlight();
                caster.Server_OnDamaged-=onCaster;defender.Server_OnDamaged-=onDefender;
                if(runtime){runtime.Bind(null,null,null);UnityEngine.Object.Destroy(runtime.gameObject);}
                if(previous)register.Invoke(previous,null);
                defender.transform.position=defenderPosition;
                caster.CurrentHealth=casterHealth;defender.CurrentHealth=defenderHealth;
            }
        }
    }
}
#endif
