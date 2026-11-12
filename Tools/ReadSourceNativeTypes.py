"""Read-only PE/IL2CPP native type-table resolution. Based on Cpp2IL BinaryStructures."""
import pathlib,struct,runpy,json,hashlib
m=runpy.run_path('Tools/ReadSourceMetadataEnums.py');h=m['h'];count=h['typeDefinitions'][2]
path=pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/GameAssembly.dll');binary=path.read_bytes()
def u32(p):return struct.unpack_from('<I',binary,p)[0]
def u64(p):return struct.unpack_from('<Q',binary,p)[0]
pe=u32(0x3c);assert binary[pe:pe+4]==b'PE\0\0';optional=pe+24;assert struct.unpack_from('<H',binary,optional)[0]==0x20b
base=u64(optional+24);n=struct.unpack_from('<H',binary,pe+6)[0];sectionStart=optional+struct.unpack_from('<H',binary,pe+20)[0]
sections=[]
for i in range(n):
 p=sectionStart+i*40;sections.append((u32(p+12),u32(p+16),u32(p+20)))
def offset(va):
 rva=va-base
 for start,size,raw in sections:
  if start<=rva<start+size:return raw+rva-start
 raise ValueError('Unmapped VA '+hex(va))
pattern=struct.pack('<Q',count);candidates=[];pos=0
while True:
 pos=binary.find(pattern,pos)
 if pos<0:break
 if pos>=80 and pos+48<len(binary) and u64(pos+16)==count:
  try:
   reg=pos-80;num=u64(reg+48);table=offset(u64(reg+56))
   if num>65535 and num<1000000 and table+num*8<=len(binary):
    samples=[0,1,30216,39735,47556,64509,64491,num-1]
    valid=True
    for idx in samples:
     loc=offset(u64(table+idx*8));tag=(u32(loc+8)>>16)&255
     if tag not in range(1,31):valid=False
    if valid:candidates.append((reg,num,table))
  except (ValueError,struct.error):pass
 pos+=1
assert len(candidates)==1,candidates
reg,num,table=candidates[0];print('NATIVE_TYPES',hex(reg),num,hex(table))
def resolve(idx):
 assert 0<=idx<num
 loc=offset(u64(table+idx*8));data=u64(loc);bits=u32(loc+8);tag=(bits>>16)&255
 out={'typeIndex':idx,'tag':tag,'attributes':bits&65535,'data':data}
 if tag in [0x11,0x12]:
  assert data<count
  p=h['typeDefinitions'][0]+data*m['stride'];out['name']=m['string'](m['integer'](p));out['namespace']=m['string'](m['integer'](p+4))
  fp=p+8+3*m['tw']+m['gw']+4;cp=fp+32
  if m['integer'](cp+16)&2:
   out['enumValues']={}
   for f in range(m['integer'](fp),m['integer'](fp)+m['integer'](cp+4,2)):
    if f not in m['defaults']:continue
    name=m['string'](m['integer'](h['fields'][0]+f*m['fw']))
    out['enumValues'][name]=m['compressed'](h['fieldAndParameterDefaultValueData'][0]+m['defaults'][f])
 else:out['name']={2:'Boolean',8:'Int32',12:'Single',14:'String'}.get(tag,'native-tag-'+str(tag))
 return out
# Validate all pointer entries, then cross-check metadata byval definitions without decoding unrelated enums.
tagCounts={}
for idx in range(num):
 loc=offset(u64(table+idx*8));tag=(u32(loc+8)>>16)&255
 assert 1<=tag<=30,(idx,tag)
 tagCounts[tag]=tagCounts.get(tag,0)+1
primitiveCount=0
for definition in range(count):
 p=h['typeDefinitions'][0]+definition*m['stride'];idx=m['integer'](p+8,m['tw'])
 assert idx<num
 loc=offset(u64(table+idx*8));tag=(u32(loc+8)>>16)&255
 assert u64(loc)==definition,(definition,idx,tag,u64(loc))
 if tag not in [17,18]:primitiveCount+=1
print('NATIVE_TYPE_VALIDATION',num,'pointers',count,'definition links',primitiveCount,'primitive definitions')
proof=json.loads(pathlib.Path('Docs/Validation/DraugAttackDispatchEvidence.json').read_text(encoding='utf-8'))
fields={f['name']:resolve(f['typeIndex']) for t in proof['metadataSkillTypes'] for f in t['fields'] if f['name'] in ['castChance','castTurn','useType','animationType','castType','automaticCast','skillGroup','slotPosition','count']}
result={'binary':str(path),'sha256':hashlib.sha256(binary).hexdigest(),'registrationRawOffset':reg,'typeTableRawOffset':table,'typeCount':num,'typeDefinitionCount':count,'validation':{'validTypePointers':num,'matchingByvalDefinitions':count,'primitiveDefinitions':primitiveCount,'tagCounts':tagCounts},'fields':fields,'references':['https://github.com/SamboyCoding/Cpp2IL/blob/development/LibCpp2IL/BinaryStructures/Il2CppMetadataRegistration.cs','https://github.com/SamboyCoding/Cpp2IL/blob/development/LibCpp2IL/BinaryStructures/Il2CppType.cs'],'limits':'Resolves types and enum constants only; does not prove native control flow.'}
pathlib.Path('Docs/Validation/DraugNativeSkillTypes.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for typ in proof['metadataSkillTypes']:
 for field in typ['fields']:
  resolved=fields.get(field['name'])
  if resolved:
   field['resolvedType']=resolved['name']
   if 'enumValues' in resolved:field['enumValues']=resolved['enumValues']
proof['nativeTypeEvidence']='DraugNativeSkillTypes.json'
proof['limits']='Serialized fields, native field types, and distinct OnFireEvent/OnCastEvent are proven. Native chance evaluation, cooldown, fallback and scheduling control flow remain unproven.'
pathlib.Path('Docs/Validation/DraugAttackDispatchEvidence.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(fields,ensure_ascii=False))
