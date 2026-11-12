#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugControllerResourcesValidation
 {
  [Serializable] sealed class Result
  {
   public string scope="Production resource owner loading StreamingAssets bundle and Resources manifest; EditMode explicit Animator.Update. Shared leases/state isolation, duplicate acquisition/release, last-owner unload, destroyed actor and reacquisition. Deferred PlayMode destruction/player build not checked.";
   public bool sharedController,independentStates,duplicateRejected,survivorAdvanced,doubleDisposeSafe,lastReleaseUnloaded,destroyedActorReleased,reloaded;
  }
  const string PlayKey="DraugControllerResourcesValidation.Play";
  [InitializeOnLoadMethod] static void ResumePlayMode()
  {
   if(SessionState.GetBool(PlayKey,false))EditorApplication.playModeStateChanged+=OnPlayMode;
  }
  public static void RunPlayMode()
  {
   SessionState.SetBool(PlayKey,true);EditorApplication.playModeStateChanged+=OnPlayMode;
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
  }
  static void OnPlayMode(PlayModeStateChange state)
  {
   if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey,false))return;
   SessionState.EraseBool(PlayKey);EditorApplication.playModeStateChanged-=OnPlayMode;Run();
  }
  public static void Run()
  {
   var objects=new List<GameObject>();var leases=new List<DraugControllerResources.Lease>();int exit=1;var result=new Result();RuntimeAnimatorController oldController=null,newController=null;
   try
   {
    if(!Application.isPlaying)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    if(AssetBundle.GetAllLoadedAssetBundles().Any())throw new Exception("Expected isolated process");
    var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur");
    var animators=new List<Animator>();
    for(int i=0;i<3;i++)
    {
     var model=Object.Instantiate(prefab);model.SetActive(true);objects.Add(model);
     var animator=model.GetComponent<Animator>();animators.Add(animator);leases.Add(DraugControllerResources.Acquire(animator));
     animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
     animator.SetFloat("idle_anim_speed",1);animator.SetInteger("attack_index",i);animator.Play("base.idle",0,.049f);animator.Update(0);animator.SetTrigger("attack");
    }
    result.sharedController=animators.All(a=>a.runtimeAnimatorController==animators[0].runtimeAnimatorController) && DraugControllerResources.OwnerCount==3 && AssetBundle.GetAllLoadedAssetBundles().Count()==1;
    try{DraugControllerResources.Acquire(animators[0]);}catch(InvalidOperationException){result.duplicateRejected=true;}
    for(int frame=0;frame<18;frame++)foreach(var animator in animators)animator.Update(1f/30);
    result.independentStates=Enumerable.Range(0,3).All(i=>animators[i].GetCurrentAnimatorStateInfo(0).IsName("base.attack.attack_0"+(i+1)));
    oldController=animators[0].runtimeAnimatorController;
    leases[0].Dispose();leases[0].Dispose();
    result.doubleDisposeSafe=DraugControllerResources.OwnerCount==2 && !animators[0].runtimeAnimatorController && DraugControllerResources.IsLoaded;
    float before=animators[1].GetCurrentAnimatorStateInfo(0).normalizedTime;animators[1].Update(1f/30);
    result.survivorAdvanced=animators[1].GetCurrentAnimatorStateInfo(0).normalizedTime>before;
    Object.DestroyImmediate(objects[1]);leases[1].Dispose();
    result.destroyedActorReleased=DraugControllerResources.OwnerCount==1 && DraugControllerResources.IsLoaded;
    leases[2].Dispose();
    result.lastReleaseUnloaded=DraugControllerResources.OwnerCount==0 && !DraugControllerResources.IsLoaded && !AssetBundle.GetAllLoadedAssetBundles().Any() && !animators[2].runtimeAnimatorController;
    var renewed=DraugControllerResources.Acquire(animators[0]);leases.Add(renewed);
    animators[0].Rebind();animators[0].Update(0);animators[0].Play("base.idle",0,.25f);animators[0].Update(.02f);
    result.reloaded=animators[0].GetCurrentAnimatorStateInfo(0).IsName("base.idle") && DraugControllerResources.OwnerCount==1;
    newController=animators[0].runtimeAnimatorController;
    renewed.Dispose();
    if(Application.isPlaying)result.scope="Production owner in PlayMode: shared leases and actor states checked synchronously; deferred destruction checked after next player frame in log. No actual combat or player-build validation.";
    File.WriteAllText(Application.isPlaying?"Docs/Validation/DraugControllerResourcesPlayModeVerification.json":"Docs/Validation/DraugControllerResourcesVerification.json",JsonUtility.ToJson(result,true));
    if(!result.sharedController || !result.independentStates || !result.duplicateRejected || !result.survivorAdvanced || !result.doubleDisposeSafe || !result.lastReleaseUnloaded || !result.destroyedActorReleased || !result.reloaded)throw new Exception("Resource owner assertion failed: "+JsonUtility.ToJson(result));
    Debug.Log("DRAUG_CONTROLLER_RESOURCES_PASS owners=3 sharedBundle=1 independentAttacks=True duplicateAndDestroyedSafe=True unloadReload=True");exit=0;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{foreach(var lease in leases)lease.Dispose();foreach(var model in objects)if(model)Object.DestroyImmediate(model);}
   if(Application.isPlaying && exit==0)
   {
    int frame=Time.frameCount;double deadline=EditorApplication.timeSinceStartup+10;
    EditorApplication.CallbackFunction check=null;
    check=()=>
    {
     if(Time.frameCount<=frame && EditorApplication.timeSinceStartup<deadline)return;
     EditorApplication.update-=check;
     bool passed=Time.frameCount>frame && !oldController && !newController && DraugControllerResources.OwnerCount==0 && !AssetBundle.GetAllLoadedAssetBundles().Any();
     Debug.Log("DRAUG_CONTROLLER_DEFERRED_"+(passed?"PASS":"FAIL")+" oldAndReloadedControllersDestroyed="+(!oldController && !newController));
     EditorApplication.Exit(passed?0:1);
    };
    EditorApplication.update+=check;
   }
   else EditorApplication.Exit(exit);
  }
 }
}
#endif
