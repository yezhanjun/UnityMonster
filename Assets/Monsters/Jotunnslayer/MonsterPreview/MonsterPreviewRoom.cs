using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Jotunnslayer.Preview
{
 public sealed class MonsterPreviewRoom:MonoBehaviour
 {
  public MonsterPreviewGallery gallery;
  public Camera previewCamera;
  public Material lineMaterial;
  public int SelectedMonster {get;private set;}
  public int SelectedAction {get;private set;}
  public GameObject Model {get;private set;}
  public GameObject Effect {get;private set;}
  public float Clock {get;private set;}
  public bool paused,loop=true,showRange=true,autoEffect;
  public float speed=1,range=1.5f,targetDistance=4,effectTrigger=.5f;
  PlayableGraph graph;AnimationClipPlayable playable;Animator animator;
  ParticleSystem[] effectParticles=Array.Empty<ParticleSystem>();
  LineRenderer ring,axis;Font font;
  float yaw=150,pitch=20,distance=7,modelHeight=2,effectAge;
  int effectIndex,rangeShape;bool effectTriggered;
  string search="",rangeNote="自定义测量范围（非伤害判定）";
  Vector2 monsterScroll,actionScroll,fxScroll,controlsScroll;
  GUIStyle title,label,button;Rect viewport;
  MonsterPreviewGallery.Entry Entry=>gallery.entries[SelectedMonster];
  public AnimationClip Clip=>Entry.clips.Length==0?null:Entry.clips[SelectedAction];

  void Start()
  {
   font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",16);
   ring=CreateLine("Range outline",new Color(1,.65f,.18f));axis=CreateLine("Target distance",new Color(.25f,.8f,1));
   int index=Array.FindIndex(gallery.entries,e=>e.id=="draug_thrower_sur");SelectMonster(Mathf.Max(0,index));
  }
  LineRenderer CreateLine(string name,Color color)
  {
   var go=new GameObject(name);go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();
   line.sharedMaterial=lineMaterial;line.startColor=line.endColor=color;line.widthMultiplier=.035f;line.useWorldSpace=true;return line;
  }
  public void SelectMonster(int index)
  {
   ClearEffect();if(graph.IsValid())graph.Destroy();if(Model){Model.SetActive(false);Destroy(Model);}
   SelectedMonster=index;SelectedAction=0;effectIndex=0;
   Model=Instantiate(Entry.model);Model.name="Preview - "+Entry.id;Model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
   foreach(var behaviour in Model.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
   foreach(var collider in Model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
   foreach(var body in Model.GetComponentsInChildren<Rigidbody>(true))body.isKinematic=true;
   animator=Model.GetComponent<Animator>();if(!animator)animator=Model.AddComponent<Animator>();
   if(Entry.avatar)animator.avatar=Entry.avatar;
   animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   int idle=Array.FindIndex(Entry.clips,c=>c.name.IndexOf("stand",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);
   SelectAction(Mathf.Max(0,idle));FrameModel();
  }
  public void SelectAction(int index)
  {
   if(graph.IsValid())graph.Destroy();SelectedAction=index;Clock=0;effectTriggered=false;
   if(Clip)
   {
    animator.Rebind();graph=PlayableGraph.Create("Monster preview animation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
    playable=AnimationClipPlayable.Create(graph,Clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
    var output=AnimationPlayableOutput.Create(graph,"Preview",animator);output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
   }
   rangeNote="自定义测量范围（非伤害判定）";
   if(Entry.id=="draug_thrower_sur" && Clip && Clip.name.Contains("skill_throw"))
   {range=1.5f;rangeShape=0;rangeNote="投瓶爆炸半径 1.5 m（当前战斗实现）";}
  }
  public void Scrub(float seconds)
  {Clock=seconds;if(graph.IsValid()){playable.SetTime(Clock);graph.Evaluate(0);}}
  public void FrameModel()
  {
   var renderers=Model.GetComponentsInChildren<SkinnedMeshRenderer>();if(renderers.Length==0)return;
   var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
   modelHeight=Mathf.Clamp(bounds.size.y,.5f,30);distance=Mathf.Clamp(Mathf.Max(bounds.size.magnitude*1.6f,targetDistance*1.5f+range*2),3,80);
  }
  public void ClearEffect()
  {if(Effect){Effect.SetActive(false);Destroy(Effect);}Effect=null;effectParticles=Array.Empty<ParticleSystem>();}
  public void PlayEffect(int index)
  {
   ClearEffect();if(Entry.effects.Length==0)return;effectIndex=index;effectAge=0;
   Effect=Instantiate(Entry.effects[index],new Vector3(0,0,targetDistance),Quaternion.identity);
   foreach(var behaviour in Effect.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
   foreach(var body in Effect.GetComponentsInChildren<Rigidbody>(true))body.isKinematic=true;
   effectParticles=Effect.GetComponentsInChildren<ParticleSystem>(true);
   bool death=Effect.name.Contains("DraugDeathDecodedStage");
   if(death)new DraugDeathParticleLod(Effect,Effect.transform.Find("lod1").gameObject,Effect.transform.Find("lod2").gameObject,Effect.transform.Find("lod3").gameObject).Begin(0);
   foreach(var particle in effectParticles)
   {
    if(death && !particle.isPlaying)continue;
    particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particle.Play(false);particle.Pause(false);
   }
  }
  void Update()
  {
   if(!Model)return;
   if(!paused)
   {
    float dt=Time.unscaledDeltaTime*speed;
    if(Clip)
    {
     float next=Clock+dt;
     if(autoEffect && !effectTriggered && next>=effectTrigger*Clip.length){PlayEffect(effectIndex);effectTriggered=true;}
     if(next>Clip.length){if(loop){next%=Clip.length;effectTriggered=false;}else next=Clip.length;}
     Scrub(next);
    }
    effectAge+=dt;
    foreach(var particle in effectParticles)if(particle && !particle.isStopped)particle.Simulate(dt,false,false,true);
    if(Effect && effectAge>20)ClearEffect();
   }
   
   float side=Mathf.Min(.26f,300f/Mathf.Max(1,Screen.width));previewCamera.rect=new Rect(side,0,1-side*2,1);
   var focus=new Vector3(0,modelHeight*.5f,targetDistance*.5f);
   previewCamera.transform.position=focus+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);
   previewCamera.transform.LookAt(focus);
   DrawRange();
  }
  void DrawRange()
  {
   ring.enabled=axis.enabled=showRange;if(!showRange)return;
   var center=Vector3.forward*targetDistance+Vector3.up*.035f;
   if(rangeShape==0){ring.positionCount=97;for(int i=0;i<=96;i++){float a=i*Mathf.PI*2/96;ring.SetPosition(i,center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*range);}}
   else if(rangeShape==1){ring.positionCount=51;ring.SetPosition(0,Vector3.up*.035f);for(int i=0;i<=48;i++){float a=Mathf.Lerp(-45,45,i/48f)*Mathf.Deg2Rad;ring.SetPosition(i+1,new Vector3(Mathf.Sin(a)*range,.035f,Mathf.Cos(a)*range));}ring.SetPosition(50,Vector3.up*.035f);}
   else{ring.positionCount=5;ring.SetPositions(new[]{new Vector3(-range,.035f,0),new Vector3(-range,.035f,targetDistance),new Vector3(range,.035f,targetDistance),new Vector3(range,.035f,0),new Vector3(-range,.035f,0)});}
   axis.positionCount=2;axis.SetPosition(0,Vector3.up*.04f);axis.SetPosition(1,center);
  }
  void OnGUI()
  {
   if(!gallery || gallery.entries.Length==0)return;
   if(title==null){title=new GUIStyle(GUI.skin.label){font=font,fontSize=20,fontStyle=FontStyle.Bold};label=new GUIStyle(GUI.skin.label){font=font,fontSize=14,wordWrap=true};button=new GUIStyle(GUI.skin.button){font=font,fontSize=14};}
   GUI.skin.font=font;float panel=Mathf.Min(Screen.width*.26f,300);viewport=new Rect(panel,0,Screen.width-panel*2,Screen.height);
   GUI.Box(new Rect(0,0,panel,Screen.height),"");GUI.Box(new Rect(Screen.width-panel,0,panel,Screen.height),"");
   GUILayout.BeginArea(new Rect(12,12,panel-24,Screen.height-24));GUILayout.Label("怪物资源预览",title);GUILayout.Label("模型 · 动作 · 特效",label);
   search=GUILayout.TextField(search);GUILayout.Label(gallery.entries.Length+" 个已提取模型",label);
   monsterScroll=GUILayout.BeginScrollView(monsterScroll);
   for(int i=0;i<gallery.entries.Length;i++)if(string.IsNullOrEmpty(search)||gallery.entries[i].id.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
    if(GUILayout.Button((i==SelectedMonster?"● ":"")+gallery.entries[i].id,button))SelectMonster(i);
   GUILayout.EndScrollView();GUILayout.Label("资源浏览不代表完整复刻验收通过。",label);GUILayout.EndArea();
   GUILayout.BeginArea(new Rect(Screen.width-panel+12,12,panel-24,Screen.height-24));controlsScroll=GUILayout.BeginScrollView(controlsScroll);GUILayout.Label("动作与播放",title);
   GUILayout.BeginHorizontal();if(GUILayout.Button(paused?"继续":"暂停",button))paused=!paused;if(GUILayout.Button("重播",button)){Scrub(0);effectTriggered=false;}GUILayout.EndHorizontal();
   loop=GUILayout.Toggle(loop,"循环播放");GUILayout.Label("速度 "+speed.ToString("0.00")+"×",label);speed=GUILayout.HorizontalSlider(speed,.1f,2);
   if(Clip){GUILayout.Label(Clip.name+"\n"+Clock.ToString("0.00")+" / "+Clip.length.ToString("0.00")+" 秒",label);float t=GUILayout.HorizontalSlider(Clock,0,Clip.length);if(Mathf.Abs(t-Clock)>.001f){paused=true;Scrub(t);}}
   actionScroll=GUILayout.BeginScrollView(actionScroll,GUILayout.Height(Mathf.Max(90,Screen.height*.16f)));
   for(int i=0;i<Entry.clips.Length;i++)if(GUILayout.Button((i==SelectedAction?"● ":"")+Entry.clips[i].name,button))SelectAction(i);
   GUILayout.EndScrollView();GUILayout.Label("特效",title);
   fxScroll=GUILayout.BeginScrollView(fxScroll,GUILayout.Height(Mathf.Max(65,Screen.height*.12f)));
   for(int i=0;i<Entry.effects.Length;i++)if(GUILayout.Button(Entry.effects[i].name,button))PlayEffect(i);
   if(Entry.effects.Length==0)GUILayout.Label("此模型尚无关联特效预制体",label);GUILayout.EndScrollView();
   if(GUILayout.Button("清除特效",button))ClearEffect();autoEffect=GUILayout.Toggle(autoEffect,"随动作触发所选特效（预览）");
   if(autoEffect){GUILayout.Label("触发进度 "+effectTrigger.ToString("P0"),label);effectTrigger=GUILayout.HorizontalSlider(effectTrigger,0,1);}
   showRange=GUILayout.Toggle(showRange,"显示范围和目标距离");GUILayout.Label(rangeNote,label);
   int shape=GUILayout.SelectionGrid(rangeShape,new[]{"圆","扇形","直线"},3,button);if(shape!=rangeShape){rangeShape=shape;rangeNote="自定义测量范围（非伤害判定）";}
   GUILayout.Label("半径 / 半宽 "+range.ToString("0.0")+" m",label);float r=GUILayout.HorizontalSlider(range,.1f,15);if(Mathf.Abs(r-range)>.001f){range=r;rangeNote="自定义测量范围（非伤害判定）";}
   GUILayout.Label("目标距离 "+targetDistance.ToString("0.0")+" m",label);targetDistance=GUILayout.HorizontalSlider(targetDistance,0,25);
   if(GUILayout.Button("镜头适配模型与范围",button))FrameModel();GUILayout.EndScrollView();GUILayout.EndArea();
   GUI.Label(new Rect(panel+15,15,viewport.width-30,60),Entry.id+"\n右键拖动旋转 · 滚轮缩放 · 地面网格 1 m",label);
   var e=Event.current;if(viewport.Contains(e.mousePosition)){if(e.type==EventType.MouseDrag&&e.button==1){yaw+=e.delta.x*.5f;pitch=Mathf.Clamp(pitch+e.delta.y*.4f,-5,80);e.Use();}if(e.type==EventType.ScrollWheel){distance=Mathf.Clamp(distance*(1+e.delta.y*.07f),1,100);e.Use();}}
  }
  void OnDestroy(){if(graph.IsValid())graph.Destroy();ClearEffect();if(Model)Destroy(Model);if(font)Destroy(font);}
 }
}
