"""Record source flask material identities; do not infer shader parity from texture names."""
import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Logs/source-parser-packages'))
import UnityPy

folder = pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64')
source = next(folder.glob('enemies_assets_draug_thrower_sur_*.bundle'))
env = UnityPy.load(str(source))
objects = {o.path_id: o for o in env.objects}
dependencies = {}

def shader_record(material_object, reference):
    if reference['m_FileID'] == 0:
        shader = objects[reference['m_PathID']]
        path = source
    else:
        cab = material_object.assets_file.externals[reference['m_FileID'] - 1].path.rsplit('/', 1)[-1]
        matches = []
        for candidate in folder.glob('duplicateassetisolation*.bundle'):
            if candidate not in dependencies:
                dependencies[candidate] = UnityPy.load(str(candidate))
            for obj in dependencies[candidate].objects:
                if obj.path_id == reference['m_PathID'] and obj.assets_file.name == cab:
                    matches.append((candidate, obj))
        assert len(matches) == 1, (cab, reference, len(matches))
        path, shader = matches[0]
    return {'name': shader.read_typetree()['m_ParsedForm']['m_Name'],
            'reference': reference, 'bundle': path.name,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

records = []
for path_id in [-2169485050493711019, 2336004704372309490, 8899847503430415608,
                5233225565518159268, 4548124323786104210]:
    obj = objects[path_id]
    material = obj.read_typetree()
    saved = material['m_SavedProperties']
    records.append({'pathID': path_id, 'name': material['m_Name'],
                    'shader': shader_record(obj, material['m_Shader']),
                    'properties': saved})

glass = records[0]['properties']
textures = dict(glass['m_TexEnvs'])
assert textures['_MainTex']['m_Texture'] == textures['_BaseColorMap']['m_Texture']
alpha = dict(glass['m_Colors'])['_BaseColor']['a']
assert 0 < alpha < .1
arrow_shaders = {'VFX_Klaus/fx_explosion_add', 'VFX_Klaus/fx_explosion_apb'}
assert all(record['shader']['name'] not in arrow_shaders for record in records)
result = {'sourceBundle': source.name, 'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
          'materials': records,
          'findings': {'glassTextureAliasesEqual': True, 'glassBaseAlpha': alpha,
                       'glassRefractionModel': dict(glass['m_Floats'])['_RefractionModel'],
                       'arrowPackedShaderReuseSupported': False},
          'limits': 'Serialized material evidence only. HDRP refraction, lighting and these particle shader programs are not ported or visually validated.'}
output = ROOT / 'Docs/Validation/DraugFlaskMaterialEvidence.json'
output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('DRAUG_FLASK_MATERIAL_EVIDENCE_PASS', json.dumps(result['findings']))
for record in records:
    print(record['name'], '=>', record['shader']['name'])
