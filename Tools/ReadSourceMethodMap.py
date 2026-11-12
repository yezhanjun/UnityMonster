"""Map source IL2CPP method tokens to native code pointers without executing the source binary."""
import runpy,struct,pathlib,json
n=runpy.run_path('Tools/ReadSourceNativeTypes.py');m=n['m'];h=m['h'];mi=m['integer'];ms=m['string'];binary=n['binary'];u64=n['u64'];offset=n['offset']
tdw=m['width'](h['typeDefinitions'][2]);methodWidth=h['methods'][1]//h['methods'][2];imageWidth=h['images'][1]//h['images'][2]
assert methodWidth==32 and imageWidth==36,(methodWidth,imageWidth)
images=[]
for i in range(h['images'][2]):
 p=h['images'][0]+i*imageWidth
 images.append({'name':ms(mi(p)),'start':mi(p+8,tdw),'count':mi(p+8+tdw)})
def va(raw):
 for start,size,begin in n['sections']:
  if begin<=raw<begin+size:return n['base']+start+raw-begin
 raise ValueError(raw)
def findAll(data,pattern):
 start=0
 while True:
  start=data.find(pattern,start)
  if start<0:return
  yield start;start+=1
modules={}
def module(name):
 if name in modules:return modules[name]
 matches=[]
 for raw in findAll(binary,name.encode()+b'\0'):
  for ptr in findAll(binary,struct.pack('<Q',va(raw))):
   try:
    count=u64(ptr+8);table=offset(u64(ptr+16))
    if 0<count<300000 and table+count*8<=len(binary):
     for idx in [0,count//2,count-1]:
      addr=u64(table+idx*8)
      if addr:offset(addr)
     matches.append({'raw':ptr,'count':count,'table':table})
   except (ValueError,struct.error):pass
 assert len(matches)==1,(name,matches)
 modules[name]=matches[0];return matches[0]
selected=[]
for i in range(h['typeDefinitions'][2]):
 p=h['typeDefinitions'][0]+i*m['stride'];name=ms(mi(p));namespace=ms(mi(p+4))
 if name not in ['<_RemoveFromSceneDelayed>d__211','RandomFloat','Vector3','Quaternion','Rigidbody','ParticleSystem','Transform','FX','Wreck','WreckSpawner','GuidComponent','SFX','VisualEffect','VisualEffectList','VisualEffectStatus','VisualEffectActorStatus','WaitFor','<RemoveFromSceneSafeImpl>d__519','NetAIController','EnemyFight','SpawnConfig','Random','Time','Utils','AnimatorIds','ai_attack','ai_cast','ai_idle_simple','ai_chase_simple','RandomGenerator','math','Mathf','MathF','Actor','PawnController','RPGActor','AIController','EnemyAIController','ActorRPGSkills','RPGSkillHelper','RPGSkill','ActiveRPGSkill','RPGSkillStatus','BallisticProjectile','GenericAreaProjectileSpawner','Projectile']:continue
 image=next(im for im in images if im['start']<=i<im['start']+im['count']);mod=module(image['name'])
 fp=p+8+3*m['tw']+m['gw']+4;methodStart=mi(fp+4);methodCount=mi(fp+32,2);methods=[]
 for index in range(methodStart,methodStart+methodCount):
  q=h['methods'][0]+index*methodWidth;assert mi(q+4,tdw)==i
  token=mi(q+20);assert token>>24==6
  slot=(token&0xffffff)-1;assert 0<=slot<mod['count']
  pointer=u64(mod['table']+slot*8)
  methods.append({'index':index,'name':ms(mi(q)),'token':token,'VA':pointer,'RVA':pointer-n['base'] if pointer else 0,'raw':offset(pointer) if pointer else 0,'parameterCount':mi(q+30,2)})
 selected.append({'index':i,'name':name,'namespace':namespace,'assembly':image['name'],'methods':methods})
 print('MAPPED',namespace,name,image['name'],len(methods))
result={'binarySHA256':n['result']['sha256'],'metadataVersion':39,'methodWidth':methodWidth,'imageWidth':imageWidth,'modules':modules,'types':selected,'limits':'Pointer mapping alone does not establish behavior. Null/generic methods may need separate registration mapping.'}
pathlib.Path('Docs/Validation/SourceCombatMethodMap.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for t in selected:
 print(t['name'],[(x['name'],hex(x['RVA'])) for x in t['methods'] if any(k in x['name'].lower() for k in ['cast','attack','update','launch','ballistic','skill'])][:55])
