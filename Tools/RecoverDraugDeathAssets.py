"""Expose original Draug death VFX/audio roots in a local copied bundle only."""
import hashlib, json, sys
from collections import Counter
from pathlib import Path
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import UnityPy

config=json.loads(Path('Docs/Validation/尸鬼投掷源配置证据.json').read_text(encoding='utf-8-sig'))
source=Path(config['sourceBundle'])
source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
assert source_hash==config['sha256']
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
actor=objects[121126352032497425].read_typetree()
ref=actor['_actorFx'];assert ref['m_FileID']==0
actor_fx=objects[ref['m_PathID']].read_typetree()
entries={};bindings={}
for name in ['OnDeathFX','OnCriticalDeathFX','OnExplosiveDeathFX']:
    rows=actor_fx['<'+name+'>k__BackingField']
    assert len(rows)==1
    collection=rows[0]['<Collection>k__BackingField']
    bindings[name]=rows
    for kind in ['_vfx','_sfx']:
        for item in collection[kind]:
            assert item['m_FileID']==0
            component=objects[item['m_PathID']].read_typetree()
            root_id=component['m_GameObject']['m_PathID']
            root=objects[root_id].read_typetree()
            key='visual' if kind=='_vfx' else 'audio'
            if key in entries:
                assert entries[key]['gameObjectPathId']==root_id
            else:
                entries[key]=dict(componentPathId=item['m_PathID'],gameObjectPathId=root_id,
                                  sourceName=root['m_Name'],sourceActive=root['m_IsActive'],component=component)
assert set(entries)=={'visual','audio'}
assert entries['visual']['sourceName']=='fx_draug_thrower_sur_death'
hierarchies={}
for key,entry in entries.items():
    visited=set();records=[]
    def walk(gid):
        assert gid not in visited;visited.add(gid)
        go=objects[gid].read_typetree()
        row=dict(gameObjectPathId=gid,name=go['m_Name'],active=go['m_IsActive'],components=[])
        children=[]
        for ref in go['m_Component']:
            pid=ref['component']['m_PathID'];obj=objects[pid];data=obj.read_typetree()
            record=dict(pathId=pid,type=obj.type.name)
            if obj.type.name in ['Transform','RectTransform']:
                record['data']=data
                children=[r['m_PathID'] for r in data['m_Children']]
            elif obj.type.name=='ParticleSystem':
                record['settings']={k:data[k] for k in ['lengthInSec','looping','playOnAwake','startDelay','InitialModule','EmissionModule'] if k in data}
            elif obj.type.name in ['MeshRenderer','ParticleSystemRenderer','MeshFilter','MeshCollider','Rigidbody','AudioSource','MonoBehaviour']:
                record['data']=data
            row['components'].append(record)
        records.append(row)
        for tid in children:walk(objects[tid].read_typetree()['m_GameObject']['m_PathID'])
    walk(entry['gameObjectPathId'])
    hierarchies[key]=dict(nodes=len(records),componentCounts=dict(Counter(c['type'] for r in records for c in r['components'])),records=records)
for obj in env.objects:
    if obj.type.name!='AssetBundle':continue
    data=obj.read_typetree();template=data['m_Container'][0][1]
    for key,entry in entries.items():
        alias='assets/recovered/draugdeath_'+key+'.prefab'
        assert not any(k==alias for k,v in data['m_Container'])
        data['m_Container'].append((alias,dict(preloadIndex=template['preloadIndex'],preloadSize=template['preloadSize'],
                                            asset=dict(m_FileID=0,m_PathID=entry['gameObjectPathId']))))
    obj.save_typetree(data)
dest=Path('Logs/draug-death-recovered.bundle')
dest.write_bytes(next(iter(env.files.values())).save())
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
# Reopen serialized output to verify aliases really point to the source roots.
reopened=UnityPy.load(str(dest));aliases={}
for obj in reopened.objects:
    if obj.type.name=='AssetBundle':aliases.update(dict(obj.read_typetree()['m_Container']))
for key,entry in entries.items():
    assert aliases['assets/recovered/draugdeath_'+key+'.prefab']['asset']['m_PathID']==entry['gameObjectPathId']
out=dict(status='RECOVERED_SOURCE_DEATH_ROOTS_NOT_RUNTIME_INTEGRATED',sourceBundle=str(source),sourceSHA256=source_hash,
         copiedBundle=str(dest),copiedSHA256=hashlib.sha256(dest.read_bytes()).hexdigest(),bindings=bindings,entries=entries,hierarchies=hierarchies,
         limits=['Copied bundle aliases verified by reopening with UnityPy; Unity load/URP conversion/audio playback not yet tested.',
                 'Source MonoBehaviours depend on original game assemblies; importer must preserve their semantics explicitly.',
                 'Hierarchy evidence includes selected particle fields; copied bundle retains complete serialized source data.',
                 'No source installation file modified; production default death currently omits these VFX/audio.'])
Path('Docs/Validation/DraugDeathAssetEvidence.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('DRAUG_DEATH_ASSETS_RECOVERED',json.dumps({k:{'nodes':v['nodes'],'components':v['componentCounts']} for k,v in hierarchies.items()}))
