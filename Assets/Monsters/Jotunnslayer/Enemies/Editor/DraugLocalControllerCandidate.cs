#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Jotunnslayer
{
 internal static class DraugLocalControllerCandidate
 {
  [Serializable] sealed class Manifest { public string bundle,controllerName,controllerAddress;public ClipMap[] clips; }
  [Serializable] sealed class ClipMap { public string original,effective,resource; }
  public static void RunLifecycle()
  {
   try
   {
    UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
    for(int cycle=0;cycle<3;cycle++)
    {
     var owned=new List<AssetBundle>();AnimatorOverrideController candidate=null;GameObject model=null;
     try
     {
      candidate=Load(owned);model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur"));model.SetActive(true);
      var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=candidate;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
      animator.Rebind();animator.Update(0);animator.SetFloat("idle_anim_speed",1);animator.SetInteger("attack_index",cycle);
      animator.Play("base.idle",0,.049f);animator.Update(0);animator.SetTrigger("attack");
      for(int frame=0;frame<18;frame++)animator.Update(1f/30);
      if(!animator.GetCurrentAnimatorStateInfo(0).IsName("base.attack.attack_0"+(cycle+1)))throw new Exception("Reloaded controller did not reach requested attack");
     }
     finally
     {
      if(model)UnityEngine.Object.DestroyImmediate(model);if(candidate)UnityEngine.Object.DestroyImmediate(candidate);
      foreach(var bundle in owned)if(bundle)bundle.Unload(true);
     }
     if(model || candidate || AssetBundle.GetAllLoadedAssetBundles().Any())throw new Exception("Candidate ownership not released");
    }
    Debug.Log("DRAUG_LOCAL_CONTROLLER_LIFECYCLE_PASS cycles=3 destroyedDuringAttack=True remainingBundles=0");
   }
   catch(Exception e){Debug.LogException(e);UnityEditor.EditorApplication.Exit(1);return;}
   DraugAnimatorTransitionValidation.RunLocalController();
  }
  internal static AnimatorOverrideController Load(List<AssetBundle> ownedBundles)
  {
   if(AssetBundle.GetAllLoadedAssetBundles().Any())throw new Exception("Isolated candidate requires no previously loaded bundles");
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Docs/Validation/DraugLocalControllerManifest.json"));
   var bundle=AssetBundle.LoadFromFile(Path.GetFullPath(manifest.bundle));
   if(!bundle)throw new Exception("Local controller bundle failed to load");ownedBundles.Add(bundle);
   var source=bundle.LoadAsset<RuntimeAnimatorController>(manifest.controllerAddress);
   if(!source || source.name!=manifest.controllerName)throw new Exception("Named compiled controller missing from local bundle");
   var candidate=new AnimatorOverrideController(source);
   var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();candidate.GetOverrides(pairs);int count=0;
   for(int i=0;i<pairs.Count;i++)
   {
    var pair=pairs[i];var map=manifest.clips.FirstOrDefault(c=>c.original==pair.Key.name);
    if(map==null)throw new Exception("Unknown controller clip "+pair.Key.name);
    if(string.IsNullOrEmpty(map.resource))continue;
    var clip=Resources.Load<AnimationClip>(map.resource);if(!clip)throw new Exception("Missing local clip "+map.resource);
    pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pair.Key,clip);count++;
   }
   candidate.ApplyOverrides(pairs);
   Debug.Log($"DRAUG_LOCAL_CONTROLLER_LOADED bundles={AssetBundle.GetAllLoadedAssetBundles().Count()} overrides={count} sourceClips={pairs.Count}");
   return candidate;
  }
 }
}
#endif
