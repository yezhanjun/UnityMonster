"""Execute source percentage gate and optimized nine-body selection arithmetic."""
import contextlib,hashlib,io,json,math,runpy,struct,sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import *
base=n['n']['base'];u=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
u.mem_map(base,(n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:u.mem_write(base+start,n['binary'][raw:raw+length])
a=0x1000000;u.mem_map(a,0x20000)
owner,array,klass,statics,stub,stack=[a+x for x in [0x1000,0x2000,0x3000,0x4000,0x5000,0x18000]]
u.mem_write(0x186246918,struct.pack('<Q',klass));u.mem_write(klass+0xb8,struct.pack('<Q',statics))
u.mem_write(owner+0x108,struct.pack('<Q',array));u.mem_write(array+0x18,struct.pack('<i',9))
for i in range(9):u.mem_write(array+0x20+i*8,struct.pack('<Q',a+0x6000+i*0x100))
u.mem_write(base+0xad4b0d+0x5aa09d3,struct.pack('<Q',stub))
f=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
bits=lambda x:struct.unpack('<I',struct.pack('<f',x))[0]
xf=lambda reg:struct.unpack('<f',struct.pack('<I',u.reg_read(reg)&0xffffffff))[0]
endpoints={base+x for x in [0xad4a98,0xad46d6,0xad46ae,0xad7605,0xad857c]}
disabled=[];sample=.5;draws=0
def ret(emu,value=None):
    if value is not None:emu.reg_write(UC_X86_REG_RAX,value)
    sp=emu.reg_read(UC_X86_REG_RSP);emu.reg_write(UC_X86_REG_RIP,struct.unpack('<Q',emu.mem_read(sp,8))[0]);emu.reg_write(UC_X86_REG_RSP,sp+8)
def hook(emu,address,size,user):
    global draws
    if address in endpoints:emu.emu_stop()
    elif address==stub:
        draws+=1;emu.reg_write(UC_X86_REG_XMM0,bits(sample));ret(emu)
    elif address==base+0x48e2ee0:ret(emu,emu.reg_read(UC_X86_REG_RCX))
    elif address==base+0x48e8be0:
        assert emu.reg_read(UC_X86_REG_RDX)==0
        disabled.append((emu.reg_read(UC_X86_REG_RCX)-(a+0x6000))//0x100);ret(emu)
u.hook_add(unicorn.UC_HOOK_CODE,hook)
gate=[]
for percent in [-1,0,50,100]:
    u.reg_write(UC_X86_REG_XMM0,0);u.reg_write(UC_X86_REG_XMM6,bits(percent))
    u.emu_start(base+0xad75fc,base+0xad857d,count=10)
    rejected=u.reg_read(UC_X86_REG_RIP)==base+0xad857c
    assert rejected==(percent<=0);gate.append(dict(percent=percent,rejected=rejected))
rows=[]
for percent in [50,100]:
 for counter in [0,1,8,9,10,100]:
  for start in [0,4,8]:
   for sample in [0,.5,1]:
    u.mem_write(owner+0x19c,struct.pack('<f',percent));u.mem_write(statics+4,struct.pack('<i',counter))
    u.reg_write(UC_X86_REG_RBX,owner);u.reg_write(UC_X86_REG_RSP,stack)
    u.emu_start(base+0xad4a40,base+0xad4a99,count=5000)
    assert u.reg_read(UC_X86_REG_RIP)==base+0xad4a98
    period,phase=xf(UC_X86_REG_XMM7),xf(UC_X86_REG_XMM8)
    expected_period=2 if percent==50 else math.inf
    expected_phase=f(math.fmod(counter,expected_period)-1)
    assert period==expected_period and phase==expected_phase
    u.reg_write(UC_X86_REG_RDI,8);u.reg_write(UC_X86_REG_RSI,8+start)
    disabled=[];draws=0
    u.emu_start(base+0xad4af0,base+0xad4bbc,count=50000)
    assert u.reg_read(UC_X86_REG_RIP)==base+0xad46d6
    expected=[(8+start-i)%9 for i in range(9) if sample>=f(math.fmod(f(counter+i+1-phase),period))]
    assert disabled==expected and draws==9
    final=struct.unpack('<i',u.mem_read(statics+4,4))[0];assert final==counter+9
    rows.append(dict(percent=percent,counter=counter,start=start,sample=sample,disabled=disabled,draws=draws,finalCounter=final))
paths=['Tools/VerifyDraugWreckSelection.py','Logs/Wreck-ActivateImpl-0xad45c0.asm.txt','Logs/WreckSpawner-SpawnWrecks-0xad7420.asm.txt']
result=dict(status='PASS_NATIVE_PERCENT_GATE_AND_OPTIMIZATION_LOOP',gateCases=gate,cases=rows,binarySHA256=n['n']['result']['sha256'],
 limits=['Native fmod implementation executed. Unity random callback/gameObject/SetActive mocked.',
 'Start offset supplied after source rounding/random-start helper; that helper and its RNG consumption are not verified here.',
 'Only actual50/100percent,9bodies and bounded counters tested; global counter lifecycle, activation velocities and full spawn remain pending.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugWreckSelectionVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'gateCases':len(gate),'selectionCases':len(rows),'status':result['status']}))
