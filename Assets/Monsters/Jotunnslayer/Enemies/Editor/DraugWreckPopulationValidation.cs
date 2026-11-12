using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugWreckPopulationValidation
{
 [Serializable] class Row {public int initial,owner,activeBodies,activationCounter;public bool register;public bool[] flags;}
 [Serializable] class Corpus {public int[] bodyCounts;public Row[] cases;}
 public static void Run()
 {
  var corpus=JsonUtility.FromJson<Corpus>(File.ReadAllText("Docs/Validation/DraugWreckRegistrationVerification.json"));
  DraugWreckPopulation population=null;bool[] flags=null;int initial=0;
  foreach(var row in corpus.cases)
  {
   if(population==null || initial!=row.initial)
   {
    population=new DraugWreckPopulation();initial=row.initial;bool baseline=false;
    population.Register(initial,ref baseline);population.ActivationCounter=123;
    flags=new bool[corpus.bodyCounts.Length];
   }
   if(row.register)population.Register(corpus.bodyCounts[row.owner],ref flags[row.owner]);
   else population.Unregister(corpus.bodyCounts[row.owner],ref flags[row.owner]);
   if(population.ActiveBodies!=row.activeBodies || population.ActivationCounter!=row.activationCounter)throw new Exception("Population differs from native sequence");
   for(int i=0;i<flags.Length;i++)if(flags[i]!=row.flags[i])throw new Exception("Registration flag differs from native sequence");
  }
  File.WriteAllText("Docs/Validation/DraugWreckPopulationVerification.json","{\"status\":\"PASS_NATIVE_REGISTRATION_CORPUS_IN_UNITY\",\"cases\":"+corpus.cases.Length+",\"limits\":\"Shared population helper only; production lifecycle ownership, reset points, FX LOD registry and lethal integration remain pending.\"}");
  Debug.Log("DRAUG_WRECK_POPULATION_PASS cases="+corpus.cases.Length);
  EditorApplication.Exit(0);
 }
}
