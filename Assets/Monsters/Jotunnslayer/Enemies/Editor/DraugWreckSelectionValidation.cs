using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugWreckSelectionValidation
{
 [Serializable] class Gate {public float percent;public bool rejected;}
 [Serializable] class Row {public float percent,sample;public int counter,start,draws,finalCounter;public int[] disabled;}
 [Serializable] class Evidence {public Gate[] gateCases;public Row[] cases;}
 public static void Run()
 {
  var data=JsonUtility.FromJson<Evidence>(File.ReadAllText("Docs/Validation/DraugWreckSelectionVerification.json"));
  var samples=new float[9];var keep=new bool[9];int cases=0;
  foreach(var gate in data.gateCases)if(DraugWreckSelection.CanSpawn(gate.percent)==gate.rejected)throw new Exception("Source percent gate mismatch");
  foreach(var row in data.cases)
  {
   for(int i=0;i<9;i++)samples[i]=row.sample;
   int counter=row.counter;DraugWreckSelection.Select(row.percent,row.start,samples,ref counter,keep);
   if(counter!=row.finalCounter)throw new Exception("Source counter mismatch");
   for(int i=0;i<9;i++)if(keep[i]!=(Array.IndexOf(row.disabled,i)<0))throw new Exception("Source body selection mismatch");
   cases++;
  }
  int unchanged=37;DraugWreckSelection.Select(0,0,samples,ref unchanged,keep);
  if(unchanged!=37 || Array.IndexOf(keep,true)>=0)throw new Exception("Zero percentage spawned or consumed counter");
  File.WriteAllText("Docs/Validation/DraugWreckSelectionUnityVerification.json",$"{{\"status\":\"PASS_NATIVE_SELECTION_CORPUS\",\"selectionCases\":{cases},\"gateCases\":{data.gateCases.Length},\"limits\":\"Explicit RNG samples/start index/counter; source RNG-start helper, shared counter lifecycle, velocities and production spawn integration pending.\"}}");
  Debug.Log($"DRAUG_WRECK_SELECTION_PASS cases={cases} gates={data.gateCases.Length}");EditorApplication.Exit(0);
 }
}
