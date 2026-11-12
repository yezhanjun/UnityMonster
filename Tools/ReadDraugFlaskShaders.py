"""Read source flask VFX shaders and preserve reproducible DXBC evidence."""
import ctypes
import hashlib
import json
import pathlib
import struct
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Logs/source-parser-packages'))
import UnityPy
from UnityPy.helpers import CompressionHelper

SOURCE = next(pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64').glob('enemies_assets_draug_thrower_sur_*.bundle'))
objects = {o.path_id: o for o in UnityPy.load(str(SOURCE)).objects}
disassemble = ctypes.WinDLL('d3dcompiler_47.dll').D3DDisassemble
disassemble.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
disassemble.restype = ctypes.c_long
report = {'source': str(SOURCE), 'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(), 'shaders': [], 'limits': ['All embedded DXBC programs mapped to serialized pass/stage blob indices. Per-variant parameter blobs preserved; parameter decoding and active keyword selection still require inspection.', 'No shader port or source visual parity validation claimed.']}
for label, path_id in [('dissolve', 1524805689388971415), ('shoot-hit', 2395031570923167312), ('erosion', 5953244318182842499)]:
    shader = objects[path_id].read_typetree()
    shader_pass = shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes'][0]
    names = {index: name for name, index in shader_pass['m_NameIndices']}
    parameters = shader_pass['progFragment']['m_CommonParameters']
    bindings = []
    for buffer in parameters['m_ConstantBuffers']:
        for parameter in buffer['m_VectorParams'] + buffer.get('m_ScalarParams', []):
            bindings.append({'name': names[parameter['m_NameIndex']], 'byteOffset': parameter['m_Index'], 'dimensions': parameter.get('m_Dim', 1)})
    entries = []
    blob_records = []
    for platform_index, offsets in enumerate(shader['offsets']):
        for segment, offset in enumerate(offsets):
            size = shader['compressedLengths'][platform_index][segment]
            expected = shader['decompressedLengths'][platform_index][segment]
            if not size:
                continue
            data = CompressionHelper.decompress_lz4(bytes(shader['compressedBlob'][offset:offset + size]), expected)
            count = struct.unpack_from('<I', data)[0]
            descriptors = [struct.unpack_from('<III', data, 4 + index * 12) for index in range(count)]
            assert all(seg == segment and off + length <= len(data) for off, length, seg in descriptors)
            for blob_index, (off, length, seg) in enumerate(descriptors):
                blob_path = ROOT / f'Logs/draug-flask-{label}-blob-{blob_index}.bin'
                blob_path.write_bytes(data[off:off + length])
                blob_records.append({'index': blob_index, 'platform': platform_index, 'segment': segment,
                                     'offset': off, 'length': length, 'path': str(blob_path.relative_to(ROOT))})
            cursor = 0
            while True:
                start = data.find(b'DXBC', cursor)
                if start < 0:
                    break
                length = struct.unpack_from('<I', data, start + 24)[0]
                assert 32 <= length <= len(data) - start
                bytecode = data[start:start + length]
                blob = ctypes.c_void_p()
                storage = ctypes.create_string_buffer(bytecode)
                result = disassemble(storage, len(bytecode), 0, None, ctypes.byref(blob))
                assert result == 0, hex(result)
                table = ctypes.cast(blob, ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
                try:
                    pointer = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(table[3])(blob)
                    count = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(table[4])(blob)
                    assembly = ctypes.string_at(pointer, count).rstrip(b'\0').decode('utf-8')
                finally:
                    ctypes.WINFUNCTYPE(ctypes.c_ulong, ctypes.c_void_p)(table[2])(blob)
                output = ROOT / f'Logs/draug-flask-{label}-{len(entries)}.dxbc'
                output.write_bytes(bytecode)
                output.with_suffix('.dxbc.txt').write_text(assembly, encoding='utf-8')
                matching = [index for index, (off, size, seg) in enumerate(descriptors) if off <= start and start + length <= off + size]
                assert len(matching) == 1
                entries.append({'blobIndex': matching[0], 'platform': platform_index, 'segment': segment, 'path': str(output.relative_to(ROOT)), 'sha256': hashlib.sha256(bytecode).hexdigest()})
                cursor = start + length
    keyword_names = shader['m_ParsedForm'].get('m_KeywordNames', [])
    passes = []
    for item in shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes']:
        stages = {}
        for stage in ['progVertex', 'progFragment']:
            stages[stage] = item[stage]
            for group in item[stage]['m_PlayerSubPrograms']:
                for program in group:
                    assert any(entry['blobIndex'] == program['m_BlobIndex'] for entry in entries)
        passes.append({'name': item['m_State']['m_Name'], 'names': item['m_NameIndices'],
                       'state': item['m_State'], 'stages': stages})
    report['shaders'].append({'name': shader['m_ParsedForm']['m_Name'], 'pathId': path_id, 'passState': shader_pass['m_State'], 'fragmentBindings': bindings, 'passNames': [p['m_State']['m_Name'] for p in shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes']], 'firstPassFragmentParameters': parameters, 'nameIndices': names, 'programs': entries, 'blobs': blob_records, 'passes': passes, 'keywordNames': keyword_names})
report['materialForwardVariants'] = []
for material_id in [2336004704372309490, 8899847503430415608, 5233225565518159268, 4548124323786104210]:
    material = objects[material_id].read_typetree()
    shader = next(s for s in report['shaders'] if s['pathId'] == material['m_Shader']['m_PathID'])
    forward = next(p for p in shader['passes'] if p['name'] in ['ForwardOnly', 'Forward Unlit'])
    programs = [p for group in forward['stages']['progFragment']['m_PlayerSubPrograms'] for p in group]
    available = {shader['keywordNames'][k] for p in programs for k in p['m_KeywordIndices']}
    wanted = set(material.get('m_ValidKeywords', [])) & available
    selected = [p for p in programs if {shader['keywordNames'][k] for k in p['m_KeywordIndices']} == wanted]
    assert len(selected) == 1, (material['m_Name'], selected)
    matched = [p for p in shader['programs'] if p['blobIndex'] == selected[0]['m_BlobIndex']]
    assert len(matched) == 1
    report['materialForwardVariants'].append({'material': material['m_Name'], 'pathId': material_id,
        'materialKeywords': material.get('m_ValidKeywords', []), 'variantKeywords': sorted(wanted),
        'program': matched[0], 'limits': 'Material keywords only; runtime global keyword overrides not observed.'})
report['dissolveSurfaceAnalysis'] = {
    'evidence': 'ForwardOnly transparent+fog blob20, extracted dissolve-12.dxbc.txt; vertex TEXCOORD0/1/2 and COLOR copied to INTERP1/2/3/4.',
    'uv': 'u = uv0.xy * (uv2.y,1) + (frac(uv2.z),0); warp = sample(MainTex_UV,u).rgb; u += (warp.rg-.5)*uv2.x*warp.b',
    'samples': 'A=sample(main,u+uv2.w*(.0087,-.005)); B=sample(main,u+uv2.w*(0,.01)); C=sample(main,u+uv2.w*(-.0087,-.005))',
    'rgbBeforePipeline': 'saturate(float3(A.r,B.r,C.r)*vertexColor.rgb)*uv1.z',
    'alphaBeforeDepth': 'q=saturate(uv1.y); t=saturate((saturate(1-max(A.g,B.g,C.g))+uv1.x*(2-q)-1)/(1.01-q)); alpha=saturate(max(A.a,B.a,C.a)-t*t*(3-2*t))',
    'limits': 'Texture slot names from preserved parameter blob15; transparent blob16 still needs complete binding decoding. Later scene-depth/vertex-alpha, premultiply, exposure and HDRP fog operations not yet translated. This is an intermediate formula, not a complete shader.'}
output = ROOT / 'Docs/Validation/DraugFlaskShaderEvidence.json'
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'output': str(output), 'shaders': [(s['name'], len(s['programs']), s['passNames']) for s in report['shaders']]}))
