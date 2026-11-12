using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugDeathParticleLodValidation
{
 [Serializable] class Case {public int count,selected;public float wreckPercent;public bool earlyReturn;public int[] thresholdOrder;}
 [Serializable] class Evidence {public Case[] results;}
 public static void Run()
 {
  var evidence=JsonUtility.FromJson<Evidence>(File.ReadAllText("Docs/Validation/DraugDeathLodVerification.json"));int policyCases=0;
  foreach(var row in evidence.results)
  {
   if(row.thresholdOrder[0]!=0)continue;
   int selected=DraugDeathParticleLod.Select(row.count);
   if(selected!=(row.earlyReturn?DraugDeathParticleLod.Suppressed:row.selected))throw new Exception("Native LOD policy mismatch");
   if(!row.earlyReturn && DraugDeathParticleLod.WreckPercent(selected)!=row.wreckPercent)throw new Exception("Native wreck percentage mismatch");
   policyCases++;
  }
  var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/DeathFx/DraugDeathDecodedStage"));
  int switchCases=0;
  try
  {
   var branches=new[]{root.transform.Find("lod1"),root.transform.Find("lod2"),root.transform.Find("lod3")};
   var lod=new DraugDeathParticleLod(root,branches[0].gameObject,branches[1].gameObject,branches[2].gameObject);
   foreach(int count in new[]{0,25,60,24,59,100,0})
   {
    foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))ps.Emit(3);
    int selected=lod.Begin(count);int playing=0;
    for(int b=0;b<3;b++)foreach(var ps in branches[b].GetComponentsInChildren<ParticleSystem>(true))
    {
     if(b!=selected && (ps.particleCount!=0 || ps.isPlaying))throw new Exception("Unselected LOD leaked particles");
     if(b==selected){if(!ps.isPlaying)throw new Exception("Selected LOD did not play");playing++;}
    }
    if(playing!=5)throw new Exception("Expected exactly five systems in selected source branch");
    switchCases++;
   }
   // Early return must not clear/restart an already configured instance.
   var before=root.GetComponentsInChildren<ParticleSystem>(true);var states=new bool[before.Length];
   for(int i=0;i<before.Length;i++)states[i]=before[i].isPlaying;
   if(lod.Begin(101)!=DraugDeathParticleLod.Suppressed)throw new Exception("Suppression missing");
   for(int i=0;i<before.Length;i++)if(states[i]!=before[i].isPlaying)throw new Exception("Suppression mutated particles");
  }
  finally{UnityEngine.Object.DestroyImmediate(root);}
  File.WriteAllText("Docs/Validation/DraugDeathParticleLodIntegrationVerification.json",$"{{\"status\":\"PASS_NATIVE_CORPUS_AND_PARTICLE_SELECTION\",\"policyCases\":{policyCases},\"switchCases\":{switchCases},\"suppressionCases\":1,\"limits\":\"Explicit caller count only; group registration, source renderer/loop overrides, WreckSpawner/audio and production death integration pending.\"}}");
  Debug.Log($"DRAUG_DEATH_PARTICLE_LOD_PASS policy={policyCases} switches={switchCases} suppression=1");EditorApplication.Exit(0);
 }
}
