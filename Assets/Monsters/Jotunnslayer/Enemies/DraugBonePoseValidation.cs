#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugBonePoseValidation
 {
  [Serializable] sealed class Result
  {
   public string scope="Source Animator direct attack state versus imported Generic-avatar Skeleton1HVisual at identical clip time; excludes transitions and original scripts.";
   public bool sourceHuman,runtimeHuman;
   public int boneCount,failed,metadataFailed;
   public List<ClipInfo> clips=new List<ClipInfo>();
   public List<Case> cases=new List<Case>();
  }
  [Serializable] sealed class IdleEvidence { public string status;public float idleAnimationSpeed; }
  [Serializable] sealed class ClipInfo
  {
   public string name;public float sourceLength,importedLength;public bool sourceLoop,importedLoop;
  }
  [Serializable] sealed class Case
  {
   public string clip;public int index;public float sourceNormalizedTime,sourceSpeed,sourceSpeedMultiplier;public float normalizedTime,maxPosition,maxRotationDegrees,maxScale,maxRootPosition;
   public string worstPositionBone,worstRotationBone;public bool passed;
  }
  static bool nativeCopies, directSample, sourceAvatarSample, denseBake, alignedBake, reducedBake, compactBake, compactAudit, otherClips, continuousLoops, configuredLoops;
  public static void RunConfiguredContinuousLoops(){otherClips=compactBake=continuousLoops=configuredLoops=true;Run();}
  public static void RunContinuousLoops(){otherClips=compactBake=continuousLoops=true;Run();}
  public static void RunOtherClipBake(){otherClips=denseBake=alignedBake=reducedBake=compactBake=true;Run();}
  public static void RunOtherClipReload(){otherClips=denseBake=alignedBake=reducedBake=compactBake=compactAudit=true;Run();}
  public static void RunOtherClips(){otherClips=true;Run();}
  public static void RunCompactAudit()
  {
   const string archive="Assets/Monsters/Jotunnslayer/Enemies/Editor/DraugPoseCandidates";
   Directory.CreateDirectory(archive);AssetDatabase.Refresh();
   foreach(string name in new[]{"SourceClips","Dense240","Aligned240","Reduced960"})
   {
    string from="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/"+name;
    if(!AssetDatabase.IsValidFolder(from))continue;
    string error=AssetDatabase.MoveAsset(from,archive+"/"+name);
    if(!string.IsNullOrEmpty(error))throw new Exception(error);
   }
   denseBake=alignedBake=reducedBake=compactBake=compactAudit=true;Run();
  }
  public static void RunCompactBake(){denseBake=alignedBake=reducedBake=compactBake=true;Run();}
  public static void RunReducedBake(){denseBake=alignedBake=reducedBake=true;Run();}
  public static void RunAlignedBake(){denseBake=alignedBake=true;Run();}
  public static void RunDenseBake(){denseBake=true;Run();}
  public static void RunSourceAvatarSampling(){directSample=sourceAvatarSample=true;Run();}
  public static void RunSourceSampling(){directSample=true;Run();}
  public static void RunNativeCopies(){nativeCopies=true;Run();}
  // Retain endpoints and recursively split where linear reconstruction exceeds the component bound.
  static Keyframe[] Reduce(Keyframe[] source,float tolerance)
  {
   if(source.Length<=2)return source;
   var keep=new bool[source.Length];keep[0]=keep[source.Length-1]=true;
   void Split(int first,int last)
   {
    float maximum=tolerance;int selected=-1;
    for(int i=first+1;i<last;i++)
    {
     float t=(source[i].time-source[first].time)/(source[last].time-source[first].time);
     float error=Mathf.Abs(source[i].value-Mathf.LerpUnclamped(source[first].value,source[last].value,t));
     if(error>maximum){maximum=error;selected=i;}
    }
    if(selected<0)return;
    keep[selected]=true;Split(first,selected);Split(selected,last);
   }
   Split(0,source.Length-1);
   var result=new List<Keyframe>();
   for(int i=0;i<source.Length;i++)if(keep[i])result.Add(source[i]);
   return result.ToArray();
  }
  static void BakeDense(GameObject model, AnimationClip original)
  {
   string folder=compactBake?"Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/Compact960":"Assets/Monsters/Jotunnslayer/Enemies/Editor/DraugPoseCandidates/"+(reducedBake?"Reduced960":alignedBake?"Aligned240":"Dense240");
   Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   string path=folder+"/"+original.name+".anim";
   if(AssetDatabase.LoadAssetAtPath<AnimationClip>(path))return;
   var transforms=model.GetComponentsInChildren<Transform>(true);
   int rate=reducedBake?960:240;
   int frames=alignedBake?Mathf.RoundToInt(original.length*rate):Mathf.CeilToInt(original.length*rate);
   var keys=new Keyframe[transforms.Length*10][];
   for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe[frames+1];
   var previous=new Quaternion[transforms.Length];
   for(int frame=0;frame<=frames;frame++)
   {
    float time=alignedBake?(frame==frames?original.length:frame/(float)rate):original.length*frame/frames;original.SampleAnimation(model,time);
    for(int i=1;i<transforms.Length;i++)
    {
     var t=transforms[i];var p=t.localPosition;var q=t.localRotation;var scale=t.localScale;
     if(frame>0 && Quaternion.Dot(previous[i],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
     previous[i]=q;
     float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w,scale.x,scale.y,scale.z};
     for(int axis=0;axis<10;axis++)keys[i*10+axis][frame]=new Keyframe(time,values[axis]);
    }
   }
   string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
   var clip=new AnimationClip{name=original.name,frameRate=rate};
   for(int i=1;i<transforms.Length;i++)for(int axis=0;axis<10;axis++)
   {
    var samples=keys[i*10+axis];
    if(alignedBake)
    {
     float min=samples[0].value,max=min;
     foreach(var key in samples){min=Mathf.Min(min,key.value);max=Mathf.Max(max,key.value);}
     if(max-min<=.0000001f)samples=new[]{samples[0],samples[samples.Length-1]};
    }
    if(reducedBake)samples=Reduce(samples,compactBake?(axis>=3 && axis<=6?.00005f:axis<3?.00001f:.000001f):.000001f);
    var curve=new AnimationCurve(samples);
    for(int key=0;key<curve.length;key++)
    {
     AnimationUtility.SetKeyLeftTangentMode(curve,key,AnimationUtility.TangentMode.Linear);
     AnimationUtility.SetKeyRightTangentMode(curve,key,AnimationUtility.TangentMode.Linear);
    }
    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(transforms[i],model.transform),typeof(Transform),properties[axis]),curve);
   }
   AnimationUtility.SetAnimationClipSettings(clip,AnimationUtility.GetAnimationClipSettings(original));
   AnimationUtility.SetAnimationEvents(clip,AnimationUtility.GetAnimationEvents(original));
   AssetDatabase.CreateAsset(clip,path);AssetDatabase.SaveAssets();
   Debug.Log($"DRAUG_DENSE_BAKE clip={original.name} frames={frames+1} transforms={transforms.Length} fps={rate} linear=True reduced={reducedBake}");
  }
  public static void Run()
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new List<AssetBundle>();GameObject sourceModel=null,runtimeModel=null;
   var result=new Result();int exit=1;
   if(directSample)result.scope="Source Animator direct state versus original source clip SampleAnimation; sourceAvatar="+sourceAvatarSample+"; excludes baked curves and transitions.";
   try
   {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(source,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var original=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    if(nativeCopies)
    {
     const string folder="Assets/Monsters/Jotunnslayer/Enemies/Editor/DraugPoseCandidates/SourceClips";
     Directory.CreateDirectory(folder);AssetDatabase.Refresh();
     for(int index=0;index<3;index++)
     {
      string name="attack_0"+(index+1),path=folder+"/"+name+".anim";
      if(!AssetDatabase.LoadAssetAtPath<AnimationClip>(path))
      {
       var sourceClip=original.runtimeAnimatorController.animationClips.First(c=>c && c.name==name);
       var copy=Object.Instantiate(sourceClip);copy.name=name;AssetDatabase.CreateAsset(copy,path);
      }
     }
     AssetDatabase.SaveAssets();
    }
    sourceModel=Object.Instantiate(original.gameObject);sourceModel.SetActive(true);
    foreach(var component in sourceModel.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
    var sourceAnimator=sourceModel.GetComponent<Animator>();
    sourceAnimator.fireEvents=false;sourceAnimator.applyRootMotion=false;sourceAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    runtimeModel=Object.Instantiate(sourceAvatarSample?original.gameObject:Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur"));runtimeModel.SetActive(true);
    if(sourceAvatarSample)foreach(var component in runtimeModel.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
    var runtimeAnimator=runtimeModel.GetComponent<Animator>();runtimeAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var visual=runtimeModel.AddComponent<Skeleton1HVisual>();
    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    typeof(Skeleton1HVisual).GetMethod("Awake",flags).Invoke(visual,null);
    AnimationClip Clip(string name)=>Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/"+name);
    visual.Bind(Clip("summon_01"),Clip("stand"),Clip("move"),Clip("skill_throw"),Clip("stand"),null);
    result.sourceHuman=sourceAnimator.avatar && sourceAnimator.avatar.isHuman;
    result.runtimeHuman=runtimeAnimator.avatar && runtimeAnimator.avatar.isHuman;
    var bones=sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(b=>b).Distinct().ToArray();
    if(bones.Length<10)throw new Exception("Insufficient source skin bones");
    result.boneCount=bones.Length;
    var pairs=bones.Select(b=>new{source=b,path=AnimationUtility.CalculateTransformPath(b,sourceAnimator.transform)}).ToArray();
    var runtimeBones=runtimeModel.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,runtimeAnimator.transform));
    foreach(var pair in pairs)if(!runtimeBones.ContainsKey(pair.path))throw new Exception("Missing imported bone "+pair.path);
    string[] names=continuousLoops?new[]{"stand","move"}:otherClips?new[]{"stand","move","skill_throw","summon_01"}:new[]{"attack_01","attack_02","attack_03"};
    if(otherClips)result.scope="Source Animator idle/chase for looping motions; original clip SampleAnimation on source Avatar for throw/spawn; versus imported Generic-avatar playable, no transitions.";
    for(int index=0;index<names.Length;index++)
    {
     var sourceClip=original.runtimeAnimatorController.animationClips.First(c=>c && c.name==names[index]);
     if(denseBake){sourceAnimator.Rebind();sourceAnimator.Update(0);BakeDense(sourceAnimator.gameObject,sourceClip);}
     var clip=otherClips?Clip((compactBake?"Compact960/":"")+names[index]):compactBake?Clip("Compact960/attack_0"+(index+1)):denseBake||nativeCopies?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Monsters/Jotunnslayer/Enemies/Editor/DraugPoseCandidates/"+(nativeCopies?"SourceClips":reducedBake?"Reduced960":alignedBake?"Aligned240":"Dense240")+"/attack_0"+(index+1)+".anim"):visual.DraugArrowClip(index);
     if(compactAudit)Debug.Log($"DRAUG_CLIP_MEMORY index={index} originalBytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(Clip(names[index]))} candidateBytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip)}");
     var info=new ClipInfo{name=names[index],sourceLength=sourceClip.length,importedLength=clip.length,sourceLoop=AnimationUtility.GetAnimationClipSettings(sourceClip).loopTime,importedLoop=AnimationUtility.GetAnimationClipSettings(clip).loopTime};
     result.clips.Add(info);
     if(Mathf.Abs(info.sourceLength-info.importedLength)>.00001f || info.sourceLoop!=info.importedLoop)result.metadataFailed++;
     Debug.Log($"DRAUG_POSE_CLIP_METADATA name={info.name} sourceLength={info.sourceLength} importedLength={info.importedLength} sourceLoop={info.sourceLoop} importedLoop={info.importedLoop}");
     if(continuousLoops)
     {
      result.scope="Source Animator and imported playable advanced continuously with identical variable steps; zero-step pauses; local and model-root-relative skin bone poses.";
      string loopState=names[index]=="stand"?"base.idle":"base.chase";
      sourceAnimator.Rebind();sourceAnimator.Update(0);sourceAnimator.SetBool("chase",names[index]=="move");
      if(configuredLoops)
      {
       var evidence=JsonUtility.FromJson<IdleEvidence>(File.ReadAllText("Docs/Validation/DraugIdleSpeedEvidence.json"));
       if(evidence.status!="STATIC_NATIVE_ASSIGNMENT_VERIFIED" || evidence.idleAnimationSpeed<=0)throw new Exception("Missing checked idle speed evidence");
       sourceAnimator.SetFloat("idle_anim_speed",evidence.idleAnimationSpeed);
       result.scope+=" Source idle parameter initialized from checked native assignment/serialized configuration; native script itself not executed.";
      }
      sourceAnimator.Play(loopState,0,0);sourceAnimator.Update(0);
      visual.Play(Skeleton1HVisual.Motion.Attack,true,1,clip);
      var loopGraph=(PlayableGraph)typeof(Skeleton1HVisual).GetField("graph",flags).GetValue(visual);
      loopGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);loopGraph.Evaluate(0);
      float[] steps={1f/60,1f/120,1f/30,.007f,.025f,0f,.05f,.004f};
      for(int frame=0;frame<720;frame++)
      {
       float step=steps[frame%steps.Length];
       sourceAnimator.Update(step);loopGraph.Evaluate(step);
       if(sourceAnimator.IsInTransition(0) || sourceAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash!=Animator.StringToHash(loopState))throw new Exception("Continuous reference left loop state");
       var stateInfo=sourceAnimator.GetCurrentAnimatorStateInfo(0);
       var item=new Case{clip=names[index],index=index,normalizedTime=(float)(visual.CurrentClipTime/clip.length),sourceNormalizedTime=stateInfo.normalizedTime,sourceSpeed=stateInfo.speed,sourceSpeedMultiplier=stateInfo.speedMultiplier};
       if(frame<2)Debug.Log($"DRAUG_LOOP_CLOCK clip={names[index]} frame={frame} sourceTime={stateInfo.normalizedTime} runtimeTime={item.normalizedTime} stateSpeed={stateInfo.speed} multiplier={stateInfo.speedMultiplier} animatorSpeed={sourceAnimator.speed} sourceClips={string.Join(",",sourceAnimator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name))}");
       foreach(var pair in pairs)
       {
        var a=pair.source;var b=runtimeBones[pair.path];
        float position=Vector3.Distance(a.localPosition,b.localPosition),rotation=Quaternion.Angle(a.localRotation,b.localRotation);
        if(position>item.maxPosition){item.maxPosition=position;item.worstPositionBone=pair.path;}
        if(rotation>item.maxRotationDegrees){item.maxRotationDegrees=rotation;item.worstRotationBone=pair.path;}
        item.maxScale=Mathf.Max(item.maxScale,Vector3.Distance(a.localScale,b.localScale));
        item.maxRootPosition=Mathf.Max(item.maxRootPosition,Vector3.Distance(sourceAnimator.transform.InverseTransformPoint(a.position),runtimeAnimator.transform.InverseTransformPoint(b.position)));
       }
       item.passed=item.maxPosition<=.0001f && item.maxRotationDegrees<=.05f && item.maxScale<=.0001f && item.maxRootPosition<=.001f;
       if(!item.passed)result.failed++;
       result.cases.Add(item);
      }
      Debug.Log($"DRAUG_CONTINUOUS_LOOP clip={names[index]} frames=720 cycles={visual.CurrentClipTime/clip.length}");
      continue;
     }
     foreach(float sample in (otherClips?(info.sourceLoop?new[]{0f,.25f,.5f,.75f,1f,1.125f,2.375f}:new[]{0f,.25f,.5f,.75f,1f}):new[]{0f,DraugArrowTimeline.EntryNormalizedTime(index),.1f,.25f,DraugArrowTimeline.ClipEventSeconds(index)/clip.length,.5f,.75f,.9f}).Concat(alignedBake||otherClips?Enumerable.Range(0,compactAudit?2053:257).Select(i=>(i+(compactAudit?.718281f:.314159f))/(compactAudit?2053f:257f)*(compactAudit||otherClips?1f:.94f)):Enumerable.Empty<float>()))
     {
      sourceAnimator.Rebind();sourceAnimator.Update(0);
      string state="base.attack.attack_0"+(index+1);
      if(!otherClips){sourceAnimator.SetInteger("attack_index",index);sourceAnimator.Play(state,0,sample);sourceAnimator.Update(0);}
      if(otherClips && info.sourceLoop)
      {
       string loopState=names[index]=="stand"?"base.idle":"base.chase";
       sourceAnimator.SetBool("chase",names[index]=="move");
       sourceAnimator.Play(loopState,0,sample);sourceAnimator.Update(0);
       if(sourceAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash!=Animator.StringToHash(loopState) || sourceAnimator.IsInTransition(0))throw new Exception("Loop reference left requested source state");
      }
      else if(otherClips || compactAudit && sample>=.94f)sourceClip.SampleAnimation(sourceModel,sample*sourceClip.length);
      else if(sourceAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash!=Animator.StringToHash(state) || sourceAnimator.IsInTransition(0))throw new Exception("Source sample not in isolated attack state");
      if(directSample)sourceClip.SampleAnimation(runtimeModel,sample*sourceClip.length);
      else
      {
       visual.Play(Skeleton1HVisual.Motion.Attack,true,0,clip,sample*clip.length);
       var graph=(PlayableGraph)typeof(Skeleton1HVisual).GetField("graph",flags).GetValue(visual);
       graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Evaluate(0);
      }
      var item=new Case{clip=names[index],index=index,normalizedTime=sample};
      foreach(var pair in pairs)
      {
       var a=pair.source;var b=runtimeBones[pair.path];
       float position=Vector3.Distance(a.localPosition,b.localPosition),rotation=Quaternion.Angle(a.localRotation,b.localRotation),scale=Vector3.Distance(a.localScale,b.localScale);
       if(position>item.maxPosition){item.maxPosition=position;item.worstPositionBone=pair.path;}
       if(rotation>item.maxRotationDegrees){item.maxRotationDegrees=rotation;item.worstRotationBone=pair.path;}
       item.maxScale=Mathf.Max(item.maxScale,scale);
      }
      item.passed=item.maxPosition<=.0001f && item.maxRotationDegrees<=.05f && item.maxScale<=.0001f;
      if(!item.passed)result.failed++;
      result.cases.Add(item);
      if(!otherClips && (!alignedBake || !item.passed))Debug.Log($"DRAUG_BONE_POSE_CASE index={index} normalized={sample} bones={bones.Length} position={item.maxPosition} rotation={item.maxRotationDegrees} scale={item.maxScale} passed={item.passed}");
     }
    }
    File.WriteAllText(configuredLoops?"Docs/Validation/DraugConfiguredContinuousLoopVerification.json":continuousLoops?"Docs/Validation/DraugContinuousLoopVerification.json":otherClips?(compactAudit?"Docs/Validation/DraugOtherClipReloadPoseVerification.json":compactBake?"Docs/Validation/DraugOtherClipFixedPoseVerification.json":"Docs/Validation/DraugOtherClipPoseVerification.json"):compactAudit?"Docs/Validation/DraugCompactReloadPoseVerification.json":compactBake?"Docs/Validation/DraugCompactPoseVerification.json":reducedBake?"Docs/Validation/DraugReducedPoseVerification.json":alignedBake?"Docs/Validation/DraugAlignedPoseVerification.json":denseBake?"Docs/Validation/DraugDensePoseVerification.json":sourceAvatarSample?"Docs/Validation/DraugSourceAvatarSamplingVerification.json":directSample?"Docs/Validation/DraugSourceSamplingVerification.json":nativeCopies?"Docs/Validation/DraugNativeClipPoseVerification.json":"Docs/Validation/DraugBonePoseVerification.json",JsonUtility.ToJson(result,true));
    Debug.Log($"DRAUG_BONE_POSE_{(result.failed==0 && result.metadataFailed==0?"PASS":"FAIL")} cases={result.cases.Count} failed={result.failed} metadataFailed={result.metadataFailed} bones={result.boneCount} sourceHuman={result.sourceHuman} runtimeHuman={result.runtimeHuman}");
    exit=result.failed==0 && result.metadataFailed==0?0:1;
   }
   catch(Exception error){Debug.LogException(error);}
   finally
   {
    if(runtimeModel)
    {
     var visual=runtimeModel.GetComponent<Skeleton1HVisual>();
     if(visual)typeof(Skeleton1HVisual).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(visual,null);
     Object.DestroyImmediate(runtimeModel);
    }
    if(sourceModel)Object.DestroyImmediate(sourceModel);
    foreach(var bundle in bundles)if(bundle)bundle.Unload(true);
   }
   EditorApplication.Exit(exit);
  }
 }
}
#endif
