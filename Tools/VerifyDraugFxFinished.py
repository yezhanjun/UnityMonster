"""Execute source IsFinishedInternal for the death prefab's wreck+particle subset."""
import contextlib,hashlib,io,json,runpy,struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):s=runpy.run_path('Tools/VerifyDraugWreckSelection.py')
from unicorn.x86_const import *
u,base,a=s['u'],s['base'],s['a'];owner,stack=s['owner'],s['stack']
spawner,infos,array,info,particle,stub,end=[a+x for x in [0xa000,0xa400,0xa800,0xac00,0xb000,0xb400,0xb800]]
u.mem_write(owner,bytes(0x400));u.mem_write(owner+0x249,b'\1');u.mem_write(owner+0x24d,b'\1')
u.mem_write(owner+0x1e0,struct.pack('<Q',spawner));u.mem_write(owner+0x208,struct.pack('<Q',infos))
u.mem_write(infos+0x18,struct.pack('<i',1));u.mem_write(infos+0x10,struct.pack('<Q',array));u.mem_write(array+0x18,struct.pack('<i',1));u.mem_write(array+0x20,struct.pack('<Q',info));u.mem_write(info+0x10,struct.pack('<Q',particle));u.mem_write(particle+0x10,struct.pack('<Q',1))
u.mem_write(base+0xbdbfdf+0x59820af,b'\1');u.mem_write(0x1862971c8,struct.pack('<Q',s['klass']));u.mem_write(s['klass']+0xe4,struct.pack('<i',1));u.mem_write(base+0xbdc65d+0x599baf3,struct.pack('<Q',stub))
current={}
def hook(emu,address,size,user):
    if address==end:emu.emu_stop()
    elif address==base+0x5cd560:s['ret'](emu)
    elif address==base+0xadc7d0:s['ret'](emu,current['remainingWrecks'])
    elif address==base+0xbdc770:s['ret'](emu,particle)
    elif address==base+0x48f1ea0:s['ret'](emu,1)
    elif address==base+0x49b75d0:s['ret'](emu,current['particleCount'])
    elif address==base+0xbdc8d0:s['ret'](emu,int(current['loop']))
    elif address==base+0x2f93ea0:s['ret'](emu,particle)
    elif address==stub:emu.reg_write(UC_X86_REG_XMM0,s['bits'](current['duration']));s['ret'](emu)
u.hook_add(s['unicorn'].UC_HOOK_CODE,hook)
rows=[]
for state,initialized in [(0,True),(1,True),(0,False)]:
 for spawned in [False,True]:
  for wrecks in [0,1]:
   for count,loop in [(0,False),(1,False),(0,True)]:
    for life in [.34999997,.35,.35000002]:
     current=dict(state=state,initialized=initialized,spawned=spawned,remainingWrecks=wrecks,particleCount=count,loop=loop,lifeTime=s['f'](life),duration=s['f'](.35))
     u.mem_write(owner+0x254,struct.pack('<i',state));u.mem_write(owner+0x28e,bytes([initialized]));u.mem_write(owner+0x294,struct.pack('<f',life));u.mem_write(spawner+0xe9,bytes([spawned]))
     u.mem_write(stack-8,struct.pack('<Q',end));u.reg_write(UC_X86_REG_RSP,stack-8);u.reg_write(UC_X86_REG_RCX,owner)
     u.emu_start(base+0xbdbfd0,end+1,count=3000);assert u.reg_read(UC_X86_REG_RIP)==end
     finished=bool(u.reg_read(UC_X86_REG_RAX)&255)
     expected=state==0 and initialized and not(spawned and wrecks>0) and count==0 and not loop and current['duration']<=current['lifeTime']
     assert finished==expected,current
     rows.append(dict(**current,finished=finished))
paths=['Tools/VerifyDraugFxFinished.py','Logs/FX-_IsFinished_g__IsFinishedInternal_170_0-0xbdbfd0.asm.txt','Logs/WreckSpawner-get_ActiveWreckCount-0xadc7d0.asm.txt']
result=dict(status='PASS_NATIVE_DEATH_FX_FINISHED_SUBSET',cases=rows,binarySHA256=s['n']['n']['result']['sha256'],
 limits=['Single valid particle, wreck/particle subset. Engine component/property reads and wreck count mocked; native branching/comparisons executed.',
 'No animation/trail/VFX/decal branches, source delayed coroutine or actual lethal integration validated here.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugFxFinishedVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(dict(status=result['status'],cases=len(rows))))
