"""UnityMonster local workaround for Unity UUM-122608 on 6000.2.10f1.
Run from project root with Unity closed. Idempotent. Regenerate lilToon shaders
before reapplying when shared pass source or lilToon settings change.
Keeps original pass bodies, common HLSL, both shader targets, UV retention pass,
properties, tags and fallback. No material conversion and no error suppression.
Remove by regenerating these four wrappers after moving to a fixed Unity version.
"""
from pathlib import Path
import re,json
root=Path('Packages/jp.lilxyzw.liltoon-2.3.2/Shader')
names=['lts.shader','lts_o.shader','lts_trans.shader','lts_trans_o.shader']
def block(text,opening):
 depth=0
 for end in range(opening,len(text)):
  if text[end]=='{':depth+=1
  elif text[end]=='}':
   depth-=1
   if depth==0:return text[opening:end+1],end+1
 raise ValueError('Unbalanced ShaderLab')
records=[]
pending=[]
for name in names:
 p=root/name;parent=p.read_text(encoding='utf-8-sig')
 if 'UUM-122608: source pass code is inlined' in parent:
  print('ALREADY_INLINED',name);continue
 marker=re.search(r'^    SubShader\s*\{',parent,re.M);opening=parent.index('{',marker.start());body,end=block(parent,opening)
 refs=re.findall(r'UsePass "(Hidden/ltspass_[^/]+)/([^"]+)"',body)
 assert refs and len(set(x for x,y in refs))==1
 sourceName=refs[0][0].split('/')[-1]+'.shader';source=(root/sourceName).read_text(encoding='utf-8-sig')
 firstSub=source.index('    SubShader');common='\n'.join(re.findall(r'HLSLINCLUDE(.*?)ENDHLSL',source[:firstSub],re.S))
 expanded=[]
 for sub in re.finditer(r'^    SubShader\s*\{',source,re.M):
  sourceBody,_=block(source,source.index('{',sub.start()))
  config='\n'.join(re.findall(r'HLSLINCLUDE(.*?)ENDHLSL',sourceBody,re.S))
  passes={}
  for match in re.finditer(r'^        Pass\s*\{',sourceBody,re.M):
   originalPass,_=block(sourceBody,sourceBody.index('{',match.start()))
   passName=re.search(r'Name "([^"]+)"',originalPass)
   if passName:passes[passName[1]]='Pass\n        '+originalPass
  result=body
  for shader,passName in refs:
   assert passName in passes,(name,passName)
   result=result.replace('UsePass "'+shader+'/'+passName+'"',passes[passName])
  result=result.replace('{','{\n        // UUM-122608: source pass code is inlined, not a replacement shading model.\n        HLSLINCLUDE\n'+common+config+'\n        ENDHLSL',1)
  expanded.append('    SubShader\n    '+result)
 assert len(expanded)==2,(name,len(expanded))
 pending.append((p,parent[:marker.start()]+'\n\n'.join(expanded)+parent[end:]))
 records.append({'wrapper':name,'source':sourceName,'subshaders':len(expanded),'passesPerSubshader':[n for _,n in refs]})
 print('INLINED',name,len(expanded),len(refs))
for path,text in pending:path.write_text(text,encoding='utf-8')
if records:
 Path('Logs').mkdir(exist_ok=True)
 Path('Logs/liltoon-inline-manifest.json').write_text(json.dumps(records,indent=2))

