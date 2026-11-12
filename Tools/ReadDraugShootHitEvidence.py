"""Record shoot-hit bindings and the actual source particle singular-boundary input."""
import hashlib,json,pathlib,struct,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Logs/source-parser-packages'))
import UnityPy
source=next(pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64').glob('enemies_assets_draug_thrower_sur_*.bundle'))
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
material=objects[5233225565518159268].read_typetree()
shader=objects[material['m_Shader']['m_PathID']].read_typetree()
p=next(p for p in shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes'] if p['m_State']['m_Name']=='ForwardOnly')
names={index:name for name,index in p['m_NameIndices']}
common=p['progFragment']['m_CommonParameters']
soft=[v for cb in common['m_ConstantBuffers'] for v in cb['m_VectorParams'] if names[v['m_NameIndex']]=='Boolean_2FAC1FE4'];assert len(soft)==1 and soft[0]['m_Index']==16
blob=ROOT/'Logs/draug-flask-shoot-hit-blob-22.bin';data=blob.read_bytes()
def record(name,count):
 raw=name.encode();start=data.index(raw);assert struct.unpack_from('<I',data,start-4)[0]==len(raw)
 end=(start+len(raw)+3)&~3
 return list(struct.unpack_from('<'+'i'*count,data,end))
texture=record('Texture2D_F593E37E',4);blend=record('_BlendMode',6)
assert texture==[0,3,2,4] and blend==[0,1,1,0,0,144]
particles=[]
for obj in env.objects:
 if obj.type.name!='ParticleSystemRenderer':continue
 d=obj.read_typetree()
 if not any(m['m_PathID']==5233225565518159268 for m in d['m_Materials']):continue
 go=objects[d['m_GameObject']['m_PathID']].read_typetree()
 for c in go['m_Component']:
  component=objects[c['component']['m_PathID']]
  if component.type.name=='ParticleSystem':
   module=component.read_typetree()['CustomDataModule']
   assert module['vector0_1']['minMaxState']==0 and module['vector0_1']['scalar']==.5
   particles.append({'name':go['m_Name'],'rendererPathId':obj.path_id,'streams':d['m_VertexStreams'],'customData':module})
assert particles
result={'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'parameterBlobSha256':hashlib.sha256(data).hexdigest(),'portedShaderSha256':hashlib.sha256((ROOT/'Assets/Monsters/Jotunnslayer/Enemies/SourceFlaskShootHit.shader').read_bytes()).hexdigest(),
 'bindings':{'softCommon':soft[0],'textureRecord':texture,'blendRecord':blend},'particles':particles,
 'formula':'blue=sample(uv).b; packed=sample(uv+float2(blue*uv.x*uv1.z,0)); q=clamp(uv1.y,0,.5); t=saturate((uv1.x*(2-2*q)-packed.g)/(1-2*q)); mask=saturate(packed.a-t*t*(3-2*t)); mask*=saturate((sceneEyeDepth-particleEyeDepth)*uv2.x) if soft; rgb=packed.r*color.rgb*mask*uv1.w*color.a; additive One/One, output alpha1.',
 'limits':'Formula is explanatory; shader preserves source arithmetic ordering at q=.5. HDRP fog/exposure not reproduced. GPU checks cover constant-texture singular boundary, not full visual parity.'}
(ROOT/'Docs/Validation/DraugShootHitSourceEvidence.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_SHOOT_HIT_SOURCE_PASS',len(particles),'source particle q=.5')
