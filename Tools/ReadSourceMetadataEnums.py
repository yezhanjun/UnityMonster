"""Read only IL2CPP metadata v39 enum definitions from the installed source game.
Based on structure/variable index documentation in SamboyCoding/Cpp2IL (development).
Validates the header and per-record layout; writes evidence under Docs/Validation.
Does not modify the game or execute its native code.
"""
import pathlib,struct,json
path=pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/il2cpp_data/Metadata/global-metadata.dat');b=path.read_bytes();assert struct.unpack_from('<II',b)==(0xfab11baf,39)
names='stringLiteral stringLiteralData string events properties methods parameterDefaultValues fieldDefaultValues fieldAndParameterDefaultValueData fieldMarshaledSizes parameters fields genericParameters genericParameterConstraints genericContainers nestedTypes interfaces vtableMethods interfaceOffsets typeDefinitions images assemblies fieldRefs referencedAssemblies attributeData attributeDataRange unresolvedVirtualCallParameterTypes unresolvedVirtualCallParameterRanges windowsRuntimeTypeNames windowsRuntimeStrings exportedTypeDefinitions'.split()
h={name:struct.unpack_from('<III',b,8+i*12) for i,name in enumerate(names)}
def width(n):return 1 if n<255 else 2 if n<65535 else 4
tw=h['interfaceOffsets'][1]//h['interfaceOffsets'][2]-4;gw=width(h['genericContainers'][2]);stride=h['typeDefinitions'][1]//h['typeDefinitions'][2];fw=8+tw
print('LAYOUT',tw,gw,stride,'fields',fw,h['fields'][1]//h['fields'][2])
def string(i):
 a=h['string'][0]+i;return b[a:b.index(0,a)].decode('utf-8')
def integer(pos,size=4):return int.from_bytes(b[pos:pos+size],'little')
assert stride==8+3*tw+gw+4+32+16+8
assert fw==h['fields'][1]//h['fields'][2]
pathlib.Path('Logs').mkdir(exist_ok=True)
types=[]
for i in range(h['typeDefinitions'][2]):
 p=h['typeDefinitions'][0]+i*stride;name=string(integer(p));ns=string(integer(p+4));fieldPos=p+8+3*tw+gw+4;fieldStart=integer(fieldPos);countsPos=fieldPos+32;fieldCount=integer(countsPos+4,2);flags=integer(countsPos+16)
 if any(k in name.lower() for k in ['attribute','targeting','projectile','effecttype']):
  fields=[]
  for f in range(fieldStart,fieldStart+fieldCount):
   if f>=h['fields'][2]:raise ValueError((name,f))
   fp=h['fields'][0]+f*fw;fields.append({'index':f,'name':string(integer(fp)),'typeIndex':integer(fp+4,tw)})
  types.append({'index':i,'name':name,'namespace':ns,'enum':bool(flags&2),'fields':fields})
pathlib.Path('Logs/source-metadata-types.json').write_text(json.dumps(types,ensure_ascii=False,indent=2),encoding='utf-8')

defaults={}
for offset in range(h['fieldDefaultValues'][0],sum(h['fieldDefaultValues'][:2]),12):
 f,t,data=struct.unpack_from('<III',b,offset);defaults[f]=data

def compressed(pos):
 first=b[pos]
 if first<128:u=first
 elif first<192:u=((first&63)<<8)|b[pos+1]
 elif first<224:u=((first&31)<<24)|(b[pos+1]<<16)|(b[pos+2]<<8)|b[pos+3]
 elif first==240:u=int.from_bytes(b[pos+1:pos+5],'little')
 else:raise ValueError(hex(first))
 return -(u>>1)-1 if u&1 else u>>1
results={}
for typ in types:
 if typ['name'] in ['RPGAttributeType','ETargetingType']:
  entries={}
  for field in typ['fields']:
   if field['index'] not in defaults:continue
   off=h['fieldAndParameterDefaultValueData'][0]+defaults[field['index']];entries[field['name']]=compressed(off)
  results[typ['name']]=entries
  print(typ['name'],{n:v for n,v in entries.items() if typ['name']=='ETargetingType' or 390<=v<=420})
pathlib.Path('Docs/Validation/源RPG与投射物枚举.json').write_text(json.dumps({'metadataVersion':39,'source':str(path),'reference':'https://github.com/SamboyCoding/Cpp2IL/tree/development/LibCpp2IL/Metadata','enums':results},ensure_ascii=False,indent=2),encoding='utf-8')
