"""Read-only source death policy evidence; no assumption that every death retains the default type."""
import contextlib, hashlib, io, json, runpy
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
m=n['m'];h=m['h'];mi=m['integer'];ms=m['string'];enum={}
for i in range(h['typeDefinitions'][2]):
    p=h['typeDefinitions'][0]+i*m['stride']
    if ms(mi(p))!='DeathType':continue
    fp=p+8+3*m['tw']+m['gw']+4
    for f in range(mi(fp),mi(fp)+mi(fp+36,2)):
        if f in m['defaults']:
            enum[ms(mi(h['fields'][0]+f*m['fw']))]=m['compressed'](h['fieldAndParameterDefaultValueData'][0]+m['defaults'][f])
assert enum==dict(RagDoll=0,Destroy=1,Animation=2,Variable=3)
config_path=Path('Docs/Validation/尸鬼投掷源配置证据.json')
config=json.loads(config_path.read_text(encoding='utf-8-sig'))['components']['121126352032497425']
assert config['deathType']==enum['Destroy']
native=n['n'];pointer=n['u64'](n['offset'](0x186254300))
assert pointer>>29==1
coroutine=native['resolve']((pointer&0x1fffffff)>>1)
assert coroutine['name']=='<RemoveFromSceneSafeImpl>d__519'
raw=n['offset'](0x185066480)
assert n['binary'][raw:].startswith(b'UnityEngine.GameObject::SetActive_Injected(System.IntPtr,System.Boolean)\0')
actor=next(t for t in n['selected'] if t['name']=='AIController' and t['namespace']=='GamesFarm')
p=h['typeDefinitions'][0]+actor['index']*m['stride'];fp=p+8+3*m['tw']+m['gw']+4;start=mi(fp+24);slots={}
for off in [0x908,0x6f8]:
    slot=(off-0x138)//16;encoded=mi(h['vtableMethods'][0]+(start+slot)*4);assert encoded>>29==3
    q=h['methods'][0]+((encoded&0x1fffffff)>>1)*32
    slots[hex(off)]=ms(mi(q))
assert slots=={'0x908':'get_isDead','0x6f8':'RemoveFromScene'}
# Resolve the yielded wait factory independently through the full containing module.
im=next(x for x in n['images'] if x['name']=='GamesFarm.Core.dll');module=n['module'](im['name']);wait=[]
for i in range(im['start'],im['start']+im['count']):
    p=h['typeDefinitions'][0]+i*m['stride'];fp=p+8+3*m['tw']+m['gw']+4
    for idx in range(mi(fp+4),mi(fp+4)+mi(fp+32,2)):
        q=h['methods'][0]+idx*32;va=n['u64'](module['table']+((mi(q+20)&0xffffff)-1)*8)
        if va-native['base']==0xf2dae0:wait.append(ms(mi(p))+'.'+ms(mi(q)))
assert wait==['WaitFor.Seconds']
wait_token=n['u64'](n['offset'](0x1862aeab0))
assert wait_token>>29==1
wait_type=native['resolve']((wait_token&0x1fffffff)>>1)
assert wait_type['namespace']=='UnityEngine' and wait_type['name']=='WaitForSeconds'
parts=[]
for path,lo,hi in [('Logs/PawnController-OnDeath-0xba7e80.asm.txt',0xba918d,0xba9305),('Logs/_RemoveFromSceneSafeImpl_d__519-MoveNext-0xc73f00.asm.txt',0xc73f2e,0xc74011),('Logs/AIController-OnDeath-0xb3f4d0.asm.txt',0xb40437,0xb4054a)]:
    lines=Path(path).read_text(encoding='utf-8-sig').splitlines()
    selected=[s for s in lines if lo<=int(s.split()[0],16)<=hi]
    assert selected
    parts.append(dict(path=path,sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),instructions=selected))
result=dict(status='SOURCE_DEFAULT_DESTROY_DEATH_POLICY_IDENTIFIED',binarySHA256=native['result']['sha256'],deathTypeEnum=enum,sourceDefault=dict(deathType=config['deathType'],validCorpse=config['validCorpse'],fadeDelay=config['_fadeoutOnDeathDelay'],fadeDuration=config['_fadeoutOnDeathDuration']),coroutineType=coroutine,virtualSlots=slots,waitFactory=wait[0],evidence=parts,findings=['Reached PawnController.OnDeath Destroy branch calls GameObject.SetActive(false), then queues RemoveFromSceneSafeImpl.', 'Coroutine first yields WaitFor.Seconds(2); on resume it checks get_isDead and calls RemoveFromScene only if still dead.', 'AIController.OnDeath Destroy branch skips the inspected DeathFade/sink branch. Serialized10+4 fade and3+6 sink timings are not automatically the active Destroy path.', 'Current UnityMonster Draug retains HumanoidDeath and RangeRobotVisual; HumanoidDeath spawns a carrier/player ragdoll instead of the source default Destroy presentation.'],limits=['Static selected branches only; death visual effects can override deathType earlier, and complete OnDeath/kill rewards/network ownership/pool behavior not executed.', 'WaitFor.Seconds factory name and argument verified; its implementation/time-scale behavior not yet examined.', 'Current carrier mismatch identified from implementation; dedicated death screenshot/runtime prefab audit still pending. No runtime fix or new Unity test this batch.'])
wait_path=Path('Logs/WaitFor-Seconds-0xf2dae0.asm.txt')
result['waitObjectType']=wait_type
result['waitFactoryEvidence']=dict(path=str(wait_path),sha256=hashlib.sha256(wait_path.read_bytes()).hexdigest())
result['findings'].append('WaitFor.Seconds caches UnityEngine.WaitForSeconds; positive duration is quantized to hundredths. The literal2-second death wait remains2 seconds.')
result['limits'][1]='Wait object type resolved as UnityEngine.WaitForSeconds. Engine scheduling under pause/time-scale and runtime removal timing still need Unity validation.'
Path('Docs/Validation/DraugDeathPolicyEvidence.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_DEATH_POLICY_EVIDENCE_PASS defaultDestroy/inactive/WaitForSeconds2/recheckDead/remove')
