#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Jotunnslayer
{
 internal static class DraugAnimatorRouteValidation
 {
  [Serializable] sealed class Result { public string scope="Source Animator evaluation only; original script callbacks and animation events are absent/disabled. Direct Play of sub-state-machine name is unsupported; direct decide state is tested instead."; public List<Case> cases=new List<Case>(); }
  [Serializable] sealed class Case { public string entry; public int index; public string clip; public int stateHash; public float normalizedTime; }
  public static void Run()
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new List<AssetBundle>();GameObject model=null;var result=new Result();int exit=1;
   try
   {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(source,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var original=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    foreach(string entry in new[]{"base.idle","base.chase","base.attack.decide"})
     for(int index=0;index<3;index++)
     {
      model=Object.Instantiate(original.gameObject);model.SetActive(true);
      foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
      var animator=model.GetComponent<Animator>();animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
      animator.Rebind();animator.Update(0);animator.SetInteger("attack_index",index);
      animator.Play(entry,0,0);animator.Update(0);
      if(entry!="base.attack.decide")animator.SetTrigger("attack");
      string clipName=null;AnimatorStateInfo state=default;
      for(int frame=0;frame<60;frame++)
      {
       animator.Update(1f/120f);state=animator.GetCurrentAnimatorStateInfo(0);
       var clips=animator.GetCurrentAnimatorClipInfo(0);
       var attack=clips.FirstOrDefault(c=>c.clip && c.clip.name.StartsWith("attack_"));
       if(attack.clip && !animator.IsInTransition(0)){clipName=attack.clip.name;break;}
      }
      if(clipName==null)throw new Exception($"No settled attack: {entry} index={index} state={state.fullPathHash}");
      if(clipName!="attack_0"+(index+1))throw new Exception($"Unexpected route: {entry} index={index} clip={clipName}");
      result.cases.Add(new Case{entry=entry,index=index,clip=clipName,stateHash=state.fullPathHash,normalizedTime=state.normalizedTime});
      Debug.Log($"DRAUG_ANIMATOR_ROUTE entry={entry} index={index} clip={clipName}");
      Object.DestroyImmediate(model);model=null;
     }
    File.WriteAllText("Docs/Validation/DraugAnimatorRouteVerification.json",JsonUtility.ToJson(result,true));
    Debug.Log("DRAUG_ANIMATOR_ROUTE_PASS cases="+result.cases.Count);exit=0;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{if(model)Object.DestroyImmediate(model);foreach(var bundle in bundles)if(bundle)bundle.Unload(true);}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
