"""Inspect actual smoke material, defaults and selected erosion shader bindings."""
import hashlib,json,pathlib,struct,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Logs/source-parser-packages'))
import UnityPy
source=next(pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64').glob('enemies_assets_draug_thrower_sur_*.bundle'))
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
material=objects[4548124323786104210].read_typetree()
shader=objects[material['m_Shader']['m_PathID']].read_typetree()
p=next(p for p in shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes'] if p['m_State']['m_Name']=='Forward Unlit')
names={i:n for n,i in p['m_NameIndices']}
bindings=[]
for cb in p['progFragment']['m_CommonParameters']['m_ConstantBuffers']:
 for v in cb['m_VectorParams']:bindings.append({'buffer':names[cb['m_NameIndex']],'name':names[v['m_NameIndex']],'offset':v['m_Index'],'dimensions':v['m_Dim']})
blob=ROOT/'Logs/draug-flask-erosion-blob-6.bin';data=blob.read_bytes()
def record(name,count):
 raw=name.encode();start=data.index(raw);assert struct.unpack_from('<I',data,start-4)[0]==len(raw)
 end=(start+len(raw)+3)&~3
 return list(struct.unpack_from('<'+'i'*count,data,end))
records={name:record(name,4) for name in ['_NoiseDistortionTexture','_Noise_01_Texture','_Noise_02_Texture','_MaskTexture','_MaskMoveTexture']}
assert list(records.values())==[[0,0,3,4],[0,1,5,4],[0,2,6,4],[0,3,2,4],[0,4,4,4]]
for name,offset in [('_OpacityBoost',88),('_Dissolve',112),('_DepthFade',144)]:
 value=record(name,6);assert value==[0,1,1,0,0,offset]
 bindings.append({'buffer':'UnityPerMaterial','name':name,'offset':offset,'dimensions':1})
props={p['m_Name']:p for p in shader['m_ParsedForm']['m_PropInfo']['m_Props']}
saved=material['m_SavedProperties'];textures=dict(saved['m_TexEnvs']);floats=dict(saved['m_Floats']);colors=dict(saved['m_Colors'])
defaults=[]
for name in records:
 if name=='_MaskTexture':continue
 assert textures[name]['m_Texture']['m_PathID']==0
 assert props[name]['m_DefTexture']['m_DefaultName']=='white'
 defaults.append(name)
assert colors['_Color']=={'r':1.,'g':1.,'b':1.,'a':0.}
for name,value in {'_Distortion':0,'_DistortionIntensity':0,'_MaskSpeed':0,'_MaskPower':1,'_MaskMultiply':1,'_MaskMovePower':1,'_MaskMoveMultiply':1,'_NoisesPower':1,'_NoisesMultiply':2,'_EmissionIntensity':0,'_OpacityBoost':1,'_Dissolve':0,'_DepthFade':1,'_CameraOffset':0}.items():assert floats[name]==value,(name,floats[name])
assert colors['_MaskScale']['r']==1 and colors['_MaskScale']['g']==1
assert colors['_MaskOffset']['r']==0 and colors['_MaskOffset']['g']==0
renderers=[]
for obj in env.objects:
 if obj.type.name!='ParticleSystemRenderer':continue
 d=obj.read_typetree()
 if any(m['m_PathID']==4548124323786104210 for m in d['m_Materials']):
  go=objects[d['m_GameObject']['m_PathID']].read_typetree()
  renderers.append({'id':obj.path_id,'name':go['m_Name'],'streams':d['m_VertexStreams']})
assembly=ROOT/'Logs/draug-flask-erosion-5.dxbc.txt';asm=assembly.read_text(encoding='utf-8-sig')
assert 'mul r0.yzw, v2.xxyz, cb1[0].xxyz' in asm
assert 'cb1[0].w' not in asm
assert 'div r1.x, r1.x, cb1[9].x' in asm and 'min r1.x, |r1.x|, l(1.000000)' in asm
related=[]
for obj in env.objects:
 if obj.type.name!='Material':continue
 d=obj.read_typetree()
 if d['m_Shader']['m_PathID']!=5953244318182842499:continue
 related.append({'pathId':obj.path_id,'name':d['m_Name'],'properties':d['m_SavedProperties']})
result={'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'shaderName':shader['m_ParsedForm']['m_Name'],
 'parameterBlobSha256':hashlib.sha256(data).hexdigest(),'assemblySha256':hashlib.sha256(assembly.read_bytes()).hexdigest(),
 'relatedSourceMaterials':related,'textureRecords':records,'constantBindings':bindings,'whiteDefaults':defaults,'material':saved,'renderers':renderers,
 'currentMaterialReduction':{'density':'d=sample(_MaskTexture,uv0.xy).r; four unassigned auxiliary textures use source white defaults.',
 'rgbBeforeFog':'d*vertexColor.rgb; source material RGB is white and emission is zero; material color alpha is unused.',
 'alpha':'saturate(saturate((saturate(d)-uv1.x)*min(abs((sceneEyeDepth-particleEyeDepth)/1),1))*vertexColor.a)',
 'blend':'One/OneMinusSrcAlpha; RGB is premultiplied by final alpha before fog.',
 'vertex':'Source camera offset combines TEXCOORD3.y and _CameraOffset, scaled .01; renderer has no TEXCOORD3 stream and material offset is zero.'},
 'limits':'Serialized properties and static DXBC data flow; no original-game draw capture or GPU parity test. Reduction is valid only for asserted current material configuration. Erosion surface shader has been ported separately; this report is source evidence only. HDRP fog/exposure and camera-offset vertex path remain unported. Related fire material has emission60 and additive blending; smoke reduction must not be applied to it.'}
(ROOT/'Docs/Validation/DraugSmokeSourceEvidence.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_SMOKE_SOURCE_PASS',len(renderers),'renderers',len(defaults),'white defaults',len(bindings),'constant bindings')
