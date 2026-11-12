"""Read the exact death renderer/material bindings and active Shuriken parameters."""
import hashlib, json, sys
from pathlib import Path
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import UnityPy
config=json.loads(Path('Docs/Validation/尸鬼投掷源配置证据.json').read_text(encoding='utf-8-sig'))
source=Path(config['sourceBundle']);env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
hierarchy=json.loads(Path('Docs/Validation/DraugDeathAssetEvidence.json').read_text(encoding='utf-8-sig'))['hierarchies']['visual']['records']
unity=json.loads(Path('Docs/Validation/DraugDeathUnitySourceImport.json').read_text(encoding='utf-8-sig'))
known={m['name']:m for m in unity['materials']}
materials={};renderers=[]
for node in hierarchy:
    for component in node['components']:
        if component['type'] not in ['MeshRenderer','ParticleSystemRenderer']:continue
        obj=objects[component['pathId']];data=obj.read_typetree()
        row=dict(node=node['name'],pathId=obj.path_id,type=obj.type.name,enabled=data['m_Enabled'],materials=[])
        if component['type']=='ParticleSystemRenderer':
            row['vertexStreams']=data['m_VertexStreams']
            row['useCustomVertexStreams']=data.get('m_UseCustomVertexStreams')
        for ref in data['m_Materials']:
            assert ref['m_FileID']==0
            if not ref['m_PathID']:continue
            material=objects[ref['m_PathID']].read_typetree()
            row['materials'].append(material['m_Name'])
            materials[str(ref['m_PathID'])]=dict(name=material['m_Name'],shaderReference=material['m_Shader'],
                 unityResolvedShader=known[material['m_Name']]['shader'],properties=material['m_SavedProperties'])
        renderers.append(row)
assert len(materials)==5 and len(renderers)==24
shuriken=[]
for pid,material in materials.items():
    if material['unityResolvedShader']!='Shader Graphs/S_Shuriken':continue
    floats=dict(material['properties']['m_Floats']);colors=dict(material['properties']['m_Colors'])
    assert floats['_SrcBlend']==1 and floats['_DstBlend']==1 and floats['_BlendMode']==1
    shuriken.append(dict(name=material['name'],distortionAmount=floats['_distortionAmount'],noiseScale=floats['_noiseScale'],
                         noiseSpeed=colors['_noiseSpeed'],color=colors['_color'],blendMode=floats['_BlendMode']))
assert len(shuriken)==2
assert next(x for x in shuriken if x['name']=='M_mecha_particles')['distortionAmount']>0
shader=json.loads(Path('Docs/Validation/DraugDeathShurikenShaderEvidence.json').read_text(encoding='utf-8-sig'))
bindings={x['name']:x['byteOffset'] for x in shader['shaders'][0]['fragmentBindings']}
assert {k:bindings[k] for k in ['_color','_noiseSpeed','_noiseScale','_distortionAmount']}==dict(_color=16,_noiseSpeed=32,_noiseScale=40,_distortionAmount=44)
assembly=Path('Logs/draug-death-shuriken-13.dxbc.txt').read_text(encoding='utf-8-sig')
instructions=['sample_b_indexable(texture2d)(float,float,float,float) r1.xyzw, r0.zwzz, t2.xyzw, s2, cb0[86].y',
              'mul r1.xyzw, r1.xyzw, cb1[1].xyzw','mul r1.xyzw, r1.xyzw, v3.xyzw',
              'mul r3.xyz, r1.wwww, r1.xyzx','movc r1.xyz, r0.zzzz, r3.xyzx, r1.xyzx']
for instruction in instructions:assert instruction in assembly
paths=['Tools/ReadDraugDeathMaterialEvidence.py','Tools/ReadDraugDeathShaders.py','Logs/draug-death-shuriken-13.dxbc',
       'Docs/Validation/DraugDeathUnitySourceImport.json','Docs/Validation/DraugDeathShurikenShaderEvidence.json']
result=dict(status='SOURCE_DEATH_MATERIAL_BINDINGS_IDENTIFIED',sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
            materials=materials,renderers=renderers,shuriken=shuriken,forwardBindings=bindings,
            findings=['Actual Shuriken death materials use One/One and BlendMode1; sampled RGBA is multiplied by material color and particle color, then RGB is multiplied by resulting alpha in the inspected forward program.',
                      'M_mecha_particles enables gradient-noise UV distortion (.01, scale25); M_Shaman_Glow distortion is0.',
                      'Shader metadata ForwardOnly selected explicitly; the first serialized pass is ShadowCaster and does not supply required surface bindings.'],
            limits=['Native shader extraction/static arithmetic evidence, not a compiled URP port or GPU comparison.',
                    'Noise integer hash constants, exact texture/sampler binding records, HDRP fog/exposure and particle stream layout remain to verify before integration.',
                    'Other three material shader families and actor material equivalence remain pending.'],
            evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugDeathMaterialEvidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('DRAUG_DEATH_MATERIAL_EVIDENCE_PASS materials=5 renderers=24 shuriken=2; source distortion required')
