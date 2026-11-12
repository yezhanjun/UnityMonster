"""Expose source base-arrow VFX in a copied bundle. Never modifies the installed game."""
import pathlib,sys,json,hashlib
sys.path.insert(0,str(pathlib.Path('Logs/source-parser-packages').resolve()))
import UnityPy
source=next(pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64').glob('enemies_assets_draug_thrower_sur_*.bundle'))
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
projectile=objects[5835990943423446068].read_typetree()
entries={};evidence={}
for kind,key in [('muzzle','_onStartFX'),('projectile','_onStartToEndFX'),('hit','_onHitFX'),('end','_onEndFX')]:
 ref=projectile[key][0]['<Collection>k__BackingField']['_vfx'][0]['m_PathID']
 component=objects[ref].read_typetree();gid=component['m_GameObject']['m_PathID'];go=objects[gid].read_typetree()
 entries[kind]=gid;evidence[kind]={'componentPathId':ref,'gameObjectPathId':gid,'sourceName':go['m_Name']}
for obj in env.objects:
 if obj.type.name!='AssetBundle':continue
 data=obj.read_typetree();template=data['m_Container'][0][1]
 for kind,pid in entries.items():data['m_Container'].append(('assets/recovered/draugarrow_'+kind+'.prefab',{'preloadIndex':template['preloadIndex'],'preloadSize':template['preloadSize'],'asset':{'m_FileID':0,'m_PathID':pid}}))
 obj.save_typetree(data)
pathlib.Path('Logs/draug-arrow-recovered.bundle').write_bytes(next(iter(env.files.values())).save())
pathlib.Path('Docs/Validation/DraugBaseArrowAssets.json').write_text(json.dumps({'sourceBundle':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'projectile':projectile,'effects':evidence,'limits':'Hidden source VFX exposed in copied bundle; URP material conversion and runtime dispatch require separate verification.'},indent=2),encoding='utf-8')
print('DRAUG_BASE_ARROW_RECOVERED',evidence)
