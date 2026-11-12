using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random=UnityEngine.Random;

public static class DraugWreckScaleValidation
{
 [Serializable] class Row {public float[] scale,result;public float delta;}
 [Serializable] class Corpus {public Row[] scaleCases;}
 static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
 public static void Run()
 {
  var data=JsonUtility.FromJson<Corpus>(File.ReadAllText("Docs/Validation/DraugWreckSpawnTransformVerification.json"));
  var original=Random.state;var parent=new GameObject("Scale validation parent");
  var child=new GameObject("Scale validation wreck");child.transform.SetParent(parent.transform,false);
  parent.transform.localScale=new Vector3(2,3,4);
  try
  {
   foreach(var row in data.scaleCases)
   {
    child.transform.localScale=V(row.scale);DraugWreckLaunch.ApplyScaleOffset(child.transform,row.delta);
    if(!child.transform.localScale.Equals(V(row.result)))throw new Exception("Scale differs from source native arithmetic");
   }
   foreach(int seed in new[]{0,1,17,int.MaxValue})
   {
    Random.InitState(seed);float delta=Random.Range(-.05f,.05f);string expectedState=JsonUtility.ToJson(Random.state);
    Random.InitState(seed);child.transform.localScale=new Vector3(.5f,2,3);
    DraugWreckLaunch.RandomizeScale(child.transform);
    if(!child.transform.localScale.Equals(new Vector3(.5f+delta,2+delta,3+delta)) || JsonUtility.ToJson(Random.state)!=expectedState)
     throw new Exception("Root scale must consume exactly one shared float draw");
   }
   File.WriteAllText("Docs/Validation/DraugWreckScaleVerification.json","{\"status\":\"PASS_UNITY_NATIVE_SCALE_AND_SINGLE_DRAW\",\"nativeScaleCases\":"+data.scaleCases.Length+",\"randomCases\":4,\"limits\":\"Local transform scale helper only. Full activation order, FX spawn placement, actual lethal integration and source engine RNG sequence remain pending.\"}");
   Debug.Log("DRAUG_WRECK_SCALE_PASS native="+data.scaleCases.Length+" random=4");
  }
  finally{Random.state=original;UnityEngine.Object.DestroyImmediate(parent);}
  EditorApplication.Exit(0);
 }
}
