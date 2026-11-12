using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugDeathFxRegistryValidation
{
 [Serializable] class Row {public string operation;public int group,identity,before,after,selected;}
 [Serializable] class Corpus {public Row[] cases;}
 public static void Run()
 {
  var corpus=JsonUtility.FromJson<Corpus>(File.ReadAllText("Docs/Validation/DraugFxRegistryVerification.json"));
  var registry=new DraugDeathFxRegistry();int steps=0;
  foreach(var row in corpus.cases)
  {
   if(registry.Count(row.group)!=row.before)throw new Exception("Group count differs before operation "+steps);
   if(row.operation=="spawn")
   {
    int selected=DraugDeathParticleLod.Select(registry.Count(row.group));
    if(selected!=row.selected)throw new Exception("LOD differs at registry boundary "+steps);
    if(selected!=DraugDeathParticleLod.Suppressed)registry.Register(row.group,row.identity);
   }
   else registry.Remove(row.group,row.identity);
   if(registry.Count(row.group)!=row.after)throw new Exception("Group count differs after operation "+steps);
   steps++;
  }
  if(registry.Count(2)!=21 || registry.Count(3)!=1 || registry.Count(4)!=0)throw new Exception("Groups leaked across identities");
  File.WriteAllText("Docs/Validation/DraugDeathFxRegistryUnityVerification.json","{\"status\":\"PASS_UNITY_FX_REGISTRY_AND_LOD_SEQUENCE\",\"steps\":"+steps+",\"limits\":\"Native corpus models dictionary/unique collection operations. Tests registry and LOD decisions, not real particles, production despawn/destroy or full lethal lifecycle.\"}");
  Debug.Log("DRAUG_DEATH_FX_REGISTRY_PASS steps="+steps);
  EditorApplication.Exit(0);
 }
}
