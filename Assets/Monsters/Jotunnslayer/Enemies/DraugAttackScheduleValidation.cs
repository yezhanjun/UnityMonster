#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class DraugAttackScheduleValidation
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            try
            {
                var s = new DraugAttackSchedule();
                Check(!s.CanBegin(10.01f,false) && !s.CanBegin(5,true), "range/impairment");
                uint arrow = s.Begin(10,false,.8f,2);
                Check(arrow!=0 && s.Current==DraugAttackSchedule.Action.Arrow && s.ArrowIndex==2,"strict chance boundary/index");
                s.Tick(.159f);
                Check(Mathf.Abs(s.AttackRemaining-3.841f)<.00001f && s.ArrowIndex==2,"decision retains index while request timer runs");
                Check(s.EnterArrow(arrow,1) && s.AttackRemaining==4 && s.ArrowIndex==1,"entry resets timer and advances index");
                s.Tick(.1f);
                Check(!s.EnterArrow(arrow,0) && Mathf.Abs(s.AttackRemaining-3.9f)<.00001f && s.ArrowIndex==1,"duplicate entry changes timer/index");
                Check(s.Release(arrow) && !s.Release(arrow),"single release");
                s.Finish(arrow);s.Tick(4);
                uint flask = s.Begin(0,false,.79999f,1);
                Check(s.Current==DraugAttackSchedule.Action.Flask && s.ArrowIndex==1,"flask preserves next arrow");
                Check(!s.Release(arrow) && s.Release(flask),"stale release");
                s.Tick(.5f);
                Check(Mathf.Abs(s.SkillRemaining-3.95f)<.00001f,"cooldown cap");
                Check(s.CastEnd(flask),"cast end");s.Tick(.5f);
                Check(!s.CastEnd(flask) && Mathf.Abs(s.AttackRemaining-3.5f)<.00001f,"duplicate end cannot extend timer");
                s.Finish(flask);s.Tick(3.5f);
                uint fallback=s.Begin(5,false,0,0);
                Check(s.Current==DraugAttackSchedule.Action.Arrow,"skill cooldown falls back to arrow despite chance");
                s.Cancel();Check(!s.Release(fallback) && !s.Finish(fallback) && !s.EnterArrow(fallback,2),"cancel invalidates callbacks");
                var interrupted=new DraugAttackSchedule();
                uint pending=interrupted.Begin(5,false,0,0);interrupted.Cancel();
                Check(interrupted.SkillRemaining==0 && interrupted.AttackRemaining==4 && !interrupted.Release(pending),"pre-release cancellation");
                var exitFallback=new DraugAttackSchedule();uint action=exitFallback.Begin(5,false,0,0);
                exitFallback.CastEnd(action);Check(!exitFallback.Release(action),"release after cast end");
                exitFallback.Finish(action);Check(exitFallback.AttackRemaining==4,"missing cast end exit fallback");
                Debug.Log("DRAUG_SCHEDULE_PASS chance/range/entry-reset/duplicate-entry/stale-entry/two-clocks/cooldown-fallback/single-release/stale-callback/cancel/cast-end");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
#endif
