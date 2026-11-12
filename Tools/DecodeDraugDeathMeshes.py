"""Decode original streamed mesh channels, avoiding unreadable Unity mesh cloning."""
import hashlib,json,math,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Logs/source-parser-packages'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
config=json.loads((ROOT/'Docs/Validation/尸鬼投掷源配置证据.json').read_text(encoding='utf-8-sig'))
source=Path(config['sourceBundle'])
assert hashlib.sha256(source.read_bytes()).hexdigest()==config['sha256']
env=UnityPy.load(str(source))
records=json.loads((ROOT/'Docs/Validation/DraugDeathUnitySourceImport.json').read_text(encoding='utf-8-sig'))['meshes']
objects=[o for o in env.objects if o.type.name=='Mesh']
def vectors(values,keys):
    return [dict(zip(keys,v)) for v in values] if values else []
result={'sourceSHA256':config['sha256'],'meshes':[]}
for record in records:
    if record['name']=='Cube':
        continue # Builtin dependency geometry is validated separately in Unity.
    matches=[o for o in objects if o.read().m_Name==record['name']]
    assert len(matches)==1,(record['name'],len(matches))
    obj=matches[0];m=obj.read();h=MeshHandler(m);h.process()
    assert len(h.m_Vertices)==record['vertices']
    assert all(math.isfinite(x) for v in h.m_Vertices for x in v)
    submeshes=[]
    for sub in m.m_SubMeshes:
        assert sub.topology==0 and sub.indexCount%3==0
        first=sub.firstByte//(2 if h.m_Use16BitIndices else 4)
        indices=[int(x)+sub.baseVertex for x in h.m_IndexBuffer[first:first+sub.indexCount]]
        assert len(indices)==sub.indexCount and all(0<=x<len(h.m_Vertices) for x in indices)
        submeshes.append({'indices':indices})
    result['meshes'].append({'name':m.m_Name,'pathId':str(obj.path_id),'sourceReadable':m.m_IsReadable,
       'vertices':vectors(h.m_Vertices,'xyz'),'normals':vectors(h.m_Normals,'xyz'),
       'tangents':vectors(h.m_Tangents,'xyzw'),'colors':vectors(h.m_Colors,'rgba'),
       'uv':vectors(h.m_UV0,'xy'),'submeshes':submeshes})
output=ROOT/'Docs/Validation/DraugDeathDecodedMeshes.json'
output.write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
print(json.dumps({'meshes':len(result['meshes']),'vertices':sum(len(m['vertices']) for m in result['meshes']),'sha256':hashlib.sha256(output.read_bytes()).hexdigest()}))
