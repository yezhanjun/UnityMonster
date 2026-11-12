"""Run original Wreck finished-timer and stop-damping arithmetic blocks."""
import contextlib,hashlib,io,json,runpy,struct,sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import UnityPy,unicorn
from unicorn.x86_const import *
config=json.loads(Path('Docs/Validation/DraugDeathAssetEvidence.json').read_text(encoding='utf-8-sig'))
source=Path(config['sourceBundle']);assert hashlib.sha256(source.read_bytes()).hexdigest()==config['sourceSHA256']
env=UnityPy.load(str(source));wreck=next(o for o in env.objects if o.path_id==-5069360116416429754).read_typetree()
assert wreck['duration']==.5 and wreck['fadeSpeed']==2 and wreck['stopTime']==1.5 and wreck['stopAfterTime']==1
base=n['n']['base'];uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
uc.mem_map(base,(n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:uc.mem_write(base+start,n['binary'][raw:raw+length])
arena=0x1000000;uc.mem_map(arena,0x10000);owner=arena+0x1000;stub=arena+0x2000;stack=arena+0x8000
f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
bits=lambda x:struct.unpack('<I',struct.pack('<f',x))[0]
put=lambda offset,x:uc.mem_write(owner+offset,struct.pack('<f',x))
endpoints={base+x for x in [0xad3375,0xad3178,0xad3091,0xad3361,0xad3401]}
delta=0
def hook(emu,address,size,user):
    if address in endpoints:emu.emu_stop()
    elif address==stub:
        emu.reg_write(UC_X86_REG_XMM0,bits(delta));rsp=emu.reg_read(UC_X86_REG_RSP)
        emu.reg_write(UC_X86_REG_RIP,struct.unpack('<Q',emu.mem_read(rsp,8))[0]);emu.reg_write(UC_X86_REG_RSP,rsp+8)
uc.hook_add(unicorn.UC_HOOK_CODE,hook)
# Cached native Time.deltaTime icall target at instruction ad3024, nextIP ad302b.
uc.mem_write(base+0xad302b+0x5aa2d8d,struct.pack('<Q',stub))
timer_cases=[]
for duration,fade in [(wreck['duration'],wreck['fadeDuration']),(.5,0),(.5,.001),(.5,.75)]:
    for timer in [0,.09999998,.1,.49,.5,.6]:
        for dt in [0,.016,.2]:
            timer,delta,duration,fade=map(f32,[timer,dt,duration,fade])
            put(0x118,timer);put(0x70,duration);put(0x74,fade)
            uc.reg_write(UC_X86_REG_RDI,owner);uc.reg_write(UC_X86_REG_RSP,stack)
            uc.emu_start(base+0xad3024,base+0xad3376,count=100)
            actual=struct.unpack('<f',uc.mem_read(owner+0x118,4))[0]
            assert actual==f32(timer+delta)
            outcome={base+0xad3375:'keep',base+0xad3178:'remove',base+0xad3091:'sink'}[uc.reg_read(UC_X86_REG_RIP)]
            expected='remove' if actual>=duration else 'sink' if actual>=f32(duration-fade) and fade>f32(.001) else 'keep'
            assert outcome==expected
            timer_cases.append(dict(timer=timer,delta=delta,duration=duration,fadeDuration=fade,nextTimer=actual,outcome=outcome))
damping=[]
for duration in [0,.05,.1,.4]:
    for active in [1.50001,1.52,1.55,1.59999,1.6,1.9,2]:
        active,duration=map(f32,[active,duration]);put(0xc0,duration);put(0x124,active)
        uc.reg_write(UC_X86_REG_RDI,owner);uc.reg_write(UC_X86_REG_XMM7,bits(1.5))
        uc.emu_start(base+0xad33c1,base+0xad3402,count=50)
        value=struct.unpack('<f',struct.pack('<I',uc.reg_read(UC_X86_REG_XMM6)&0xffffffff))[0]
        expected=f32(1-f32(f32(active-1.5)/max(f32(.1),duration)))
        assert value==expected
        finished=uc.reg_read(UC_X86_REG_RIP)==base+0xad3361
        assert finished==(value<=0)
        damping.append(dict(activeTime=active,stopTime=1.5,stopDuration=duration,multiplier=value,finish=finished))
paths=['Tools/VerifyDraugWreckTiming.py','Logs/Wreck-GFUpdate-0xad2f10.asm.txt','Logs/Wreck-ActivateImpl-0xad45c0.asm.txt']
result=dict(status='PASS_SELECTED_NATIVE_WRECK_TIMING',timerCases=timer_cases,dampingCases=damping,
 sourceSHA256=config['sourceSHA256'],binarySHA256=n['n']['result']['sha256'],
 findings=['Finished timer uses UnityEngine.Time::get_deltaTime(), confirmed by original icall name.',
 'duration .5 is compared only on m_finished path; sink starts at float32(duration-fadeDuration), remove precedes sink at duration.',
 'Sink branch changes world position Y by -deltaTime*fadeSpeed, not alpha.',
 'After strict activeTime>stopTime, damping coefficient1-(activeTime-stopTime)/max(.1,stopDuration) multiplies current linear/angular velocities; <=0 marks finished/kinematic.'],
 limits=['Only arithmetic/branch fragments executed; clock icall mocked with supplied delta.',
 'Sink transform mutation, velocity writes, stationary/fallen checks, initialization/activation and physics not emulated.',
 'No production lifetime implementation or final death acceptance in this gate.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugWreckTimingVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'timerCases':len(timer_cases),'dampingCases':len(damping),'status':result['status']}))
