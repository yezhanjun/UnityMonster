"""Read original Draug death Legacy/AnimatedAlpha bytecode and shader defaults."""
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
report = {'source': str(SOURCE), 'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(), 'shaders': [], 'limits': ['Non-soft particle formula only; soft depth fading not restored.', 'Other arrow materials remain URP approximations.', 'Resource preview is not battle or source visual parity validation.']}
dependencies = {}
for label, material_id in [('legacy', -6099456773788670808), ('animatedalpha', 736159808234345581)]:
    material_object=objects[material_id]
    reference=material_object.read_typetree()['m_Shader']
    path_id=reference['m_PathID']
    cab=material_object.assets_file.externals[reference['m_FileID']-1].path.rsplit('/',1)[-1]
    matches=[]
    for pattern in ['duplicateassetisolation*.bundle','*unitybuiltinassets*.bundle']:
        for candidate in SOURCE.parent.glob(pattern):
            if candidate not in dependencies: dependencies[candidate]=UnityPy.load(str(candidate))
            for obj in dependencies[candidate].objects:
                if obj.path_id==path_id and obj.assets_file.name==cab: matches.append((candidate,obj))
    assert len(matches)==1,(label,cab,len(matches))
    dependency,shader_object=matches[0]
    shader=shader_object.read_typetree()
    shader_pass = next((p for p in shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes'] if p['m_State']['m_Name']=='ForwardOnly'),shader['m_ParsedForm']['m_SubShaders'][0]['m_Passes'][0])
    names = {index: name for name, index in shader_pass['m_NameIndices']}
    parameters = shader_pass['progFragment']['m_CommonParameters']
    bindings = []
    for buffer in parameters['m_ConstantBuffers']:
        for parameter in buffer['m_VectorParams']:
            bindings.append({'name': names[parameter['m_NameIndex']], 'byteOffset': parameter['m_Index'], 'dimensions': parameter['m_Dim']})
    entries = []
    for platform_index, offsets in enumerate(shader['offsets']):
        for segment, offset in enumerate(offsets):
            size = shader['compressedLengths'][platform_index][segment]
            expected = shader['decompressedLengths'][platform_index][segment]
            if not size:
                continue
            data = CompressionHelper.decompress_lz4(bytes(shader['compressedBlob'][offset:offset + size]), expected)
            (ROOT / f'Logs/draug-death-{label}-blob-{platform_index}-{segment}.bin').write_bytes(data)
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
                output = ROOT / f'Logs/draug-death-{label}-{len(entries)}.dxbc'
                output.write_bytes(bytecode)
                output.with_suffix('.dxbc.txt').write_text(assembly, encoding='utf-8')
                entries.append({'path': str(output.relative_to(ROOT)), 'sha256': hashlib.sha256(bytecode).hexdigest()})
                cursor = start + length
    report['shaders'].append({'name': shader['m_Name'], 'pathId': path_id, 'dependencyBundle': str(dependency), 'dependencySHA256': hashlib.sha256(dependency.read_bytes()).hexdigest(), 'properties': shader['m_ParsedForm']['m_PropInfo'], 'materialSavedProperties': material_object.read_typetree()['m_SavedProperties'], 'fragmentTextureBindings': parameters['m_TextureParams'], 'passNameIndices': shader_pass['m_NameIndices'], 'passState': shader_pass['m_State'], 'fragmentBindings': bindings, 'programs': entries})
report['limits'] = ['Source shader bytecode and bindings extracted; shader formula and URP equivalence require separate analysis. No runtime material port or visual pass.']
output = ROOT / 'Docs/Validation/DraugDeathRemainingShaderEvidence.json'
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'output': str(output), 'programs': [len(s['programs']) for s in report['shaders']], 'bindings': [s['fragmentBindings'] for s in report['shaders']]}))
