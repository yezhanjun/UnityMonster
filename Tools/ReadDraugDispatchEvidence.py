"""Read source Draug animation events and metadata field/enum relationships. No native-code inference."""
import runpy,pathlib,sys,json,hashlib
m=runpy.run_path('Tools/ReadSourceMetadataEnums.py')
b,h,integer,string,stride,tw,gw,fw=[m[k] for k in ['b','h','integer','string','stride','tw','gw','fw']]
types=[]
for i in range(h['typeDefinitions'][2]):
 p=h['typeDefinitions'][0]+i*stride;fp=p+8+3*tw+gw+4;cp=fp+32
 fields=[]
 for f in range(integer(fp),integer(fp)+integer(cp+4,2)):
  q=h['fields'][0]+f*fw
  fields.append({'index':f,'name':string(integer(q)),'typeIndex':integer(q+4,tw)})
 types.append({'name':string(integer(p)),'namespace':string(integer(p+4)),'byval':integer(p+8,tw),'enum':bool(integer(cp+16)&2),'fields':fields})
byval={t['byval']:t for t in types}
selected=[]
for t in types:
 if not any(f['name']=='castChance' for f in t['fields']):continue
 entry={'type':t['name'],'fields':[]}
 for f in t['fields']:
  resolved=byval.get(f['typeIndex']);item=dict(f)
  if resolved:
   item['resolvedType']=resolved['name']
   if resolved['enum']:
    item['enumValues']={ef['name']:m['compressed'](h['fieldAndParameterDefaultValueData'][0]+m['defaults'][ef['index']]) for ef in resolved['fields'] if ef['index'] in m['defaults']}
  entry['fields'].append(item)
 selected.append(entry)
sys.path.insert(0,str(pathlib.Path('Logs/source-parser-packages').resolve()))
import UnityPy
source=next(pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64').glob('enemies_assets_draug_thrower_sur_*.bundle'))
env=UnityPy.load(str(source));clips=[];controllers=[];skills=None
for obj in env.objects:
 if obj.type.name not in ['AnimationClip','AnimatorOverrideController','MonoBehaviour']:continue
 data=obj.read_typetree()
 if obj.type.name=='AnimationClip':clips.append({'pathId':obj.path_id,'name':data['m_Name'],'events':data['m_Events']})
 elif obj.type.name=='AnimatorOverrideController':controllers.append(data)
 elif obj.path_id==2413814501833703185:skills=data
result={'bundle':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'clips':clips,'controllers':controllers,'skills':skills,'metadataSkillTypes':selected,'limits':'Serialized fields and separate OnFireEvent/OnCastEvent are proven. Field type indices without resolvedType require native type-table resolution. Native scheduling, chance evaluation frequency and fallback behavior are not proven.'}
pathlib.Path('Docs/Validation/DraugAttackDispatchEvidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for t in selected:
 print(t['type'],[(f['name'],f.get('resolvedType'),f.get('enumValues')) for f in t['fields'] if f['name'] in ['useType','animationType','castType','automaticCast','castChance']])
print('DRAUG_DISPATCH_EVIDENCE',len(clips),'clips',len(selected),'skill types')
