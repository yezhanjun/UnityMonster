"""One-time, GUID-preserving layout migration. Run only while UnityMonster is closed."""
from pathlib import Path
import re,json,uuid,hashlib,subprocess
ROOT=Path('D:/Project/Unity/UnityMonster').resolve()
assert Path.cwd().resolve()==ROOT
GROUPS={
 'VRC':['#KURONEKO','AvatarPortability','Characters','DOLOSart','Rin','SEI10','Shinano','Siromori','SWEETMINT','Voidcat',"wako's atelier dress"],
 'Monsters':['Boss','Jotunnslayer'],
 'AssetStore':['KatanaAnimationPack','Sword and Shield Animation Pack','Paladin_Anim_Set','AaaWeapons','DamageNumbers','Demo','desNetware','KINEMATION','TirgamesAssets'],
 'Game':['Baseline','Battlefield','CharacterSelection','Localization','Maps'],
 'ThirdParty':['Mirror']}
mapping={f'Assets/{name}':f'Assets/{group}/{name}' for group,names in GROUPS.items() for name in names}
def safe(p):
 p=(ROOT/p).resolve();assert p.is_relative_to(ROOT/'Assets'),p;return p
def mapped(p):
 for old,new in mapping.items():
  if p==old or p==old+'.meta' or p.startswith(old+'/'):return new+p[len(old):]
 return p
reportdir=ROOT/'Logs/AssetLayout';reportdir.mkdir(parents=True,exist_ok=True)
# Snapshot paths, byte counts and every existing metadata GUID; retain all WIP and untracked files.
files={p.relative_to(ROOT).as_posix():p.stat().st_size for p in (ROOT/'Assets').rglob('*') if p.is_file()}
metas={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in files if p.endswith('.meta')}
for old,new in mapping.items():
 assert safe(old).is_dir(),old
 assert not safe(new).exists(),new
 assert safe(old+'.meta').is_file(),old
(reportdir/'before.json').write_text(json.dumps({'mapping':mapping,'files':files,'metas':metas},ensure_ascii=False),encoding='utf-8')
(reportdir/'git-status-before.txt').write_bytes(subprocess.check_output(['git','status','--porcelain','-z']))
for group in GROUPS:
 folder=safe('Assets/'+group)
 if not folder.exists():
  folder.mkdir();safe('Assets/'+group+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
for old,new in mapping.items():
 safe(old).rename(safe(new));safe(old+'.meta').rename(safe(new+'.meta'))
# Rewrite live project references only. Historical evidence under Docs/Validation stays immutable.
exts={'.cs','.shader','.hlsl','.cginc','.compute','.asmdef','.asmref','.json','.asset','.unity','.prefab','.mat','.controller','.overridecontroller','.py','.ps1','.cjs','.js','.md','.txt','.yaml','.yml','.uxml','.uss'}
paths=[]
for base in ['Assets','Tools','ProjectSettings','UserSettings']:
 if (ROOT/base).exists():paths.extend(p for p in (ROOT/base).rglob('*') if p.is_file() and p.suffix.lower() in exts)
paths.extend(p for p in ROOT.iterdir() if p.is_file() and (p.suffix.lower() in exts or p.name in ['.gitattributes','.gitignore']))
changed=[]
for p in paths:
 if p.resolve()==Path(__file__).resolve():continue
 data=p.read_bytes()
 if b'\0' in data[:4096]:continue
 try:text=data.decode('utf-8')
 except UnicodeDecodeError:continue
 original=text
 # Absolute paths to this target are safe to update; source-project paths are not.
 for old,new in mapping.items():
  text=text.replace(ROOT.as_posix()+'/'+old,ROOT.as_posix()+'/'+new)
  # Match whole root names, never Resources.Load paths, external-project paths or prefixes.
  text=re.sub(r'(?<![A-Za-z0-9_/\\])'+re.escape(old)+r'(?=$|[/\\\s\"\x27`*.\]\)])',lambda m:new,text)
  oldwin=old.replace('/', '\\');newwin=new.replace('/', '\\')
  text=re.sub(r'(?<![A-Za-z0-9_/\\])'+re.escape(oldwin)+r'(?=$|[/\\\s\"\x27`*.\]\)])',lambda m:newwin,text)
  oldesc=old.replace('/', '\\\\');newesc=new.replace('/', '\\\\')
  text=text.replace(str(ROOT).replace('\\','\\\\')+'\\\\'+oldesc,str(ROOT).replace('\\','\\\\')+'\\\\'+newesc)
 if text!=original:
  backup=reportdir/'original-text'/p.relative_to(ROOT);backup.parent.mkdir(parents=True,exist_ok=True);backup.write_bytes(data)
  p.write_bytes(text.encode('utf-8'));changed.append(p.relative_to(ROOT).as_posix())
for old,size in files.items():
 p=ROOT/mapped(old);assert p.is_file(),old
 if mapped(old) not in changed:assert p.stat().st_size==size,old
for old,sha in metas.items():assert hashlib.sha256((ROOT/mapped(old)).read_bytes()).hexdigest()==sha,old
report={'mapping':mapping,'originalFilesPreserved':len(files),'metadataByteIdentical':len(metas),'updatedReferences':changed}
(reportdir/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Moved folders',len(mapping),'preserved files',len(files),'identical metadata',len(metas),'updated text files',len(changed))
