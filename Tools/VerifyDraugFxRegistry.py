"""Execute source FX count/selection/register/remove control flow; mock containers."""
import contextlib,hashlib,io,json,runpy,struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):s=runpy.run_path('Tools/VerifyDraugWreckSelection.py')
from unicorn.x86_const import *
u,base,a=s['u'],s['base'],s['a'];owner=s['owner'];stack=s['stack']
klass,statics,dictionary,method,ctx,ctx2=[a+x for x in [0xc000,0xc200,0xc400,0xc600,0xc800,0xca00]]
u.mem_write(0x18628ba08,struct.pack('<Q',klass));u.mem_write(klass+0xe4,struct.pack('<i',1));u.mem_write(klass+0xb8,struct.pack('<Q',statics));u.mem_write(statics+0x10,struct.pack('<Q',dictionary))
for address in [base+0xbd0636+0x598da3c,base+0xbcf87d+0x598e7da]:u.mem_write(address,b'\1')
u.mem_write(0x186257a58,struct.pack('<Q',method));u.mem_write(method+0x20,struct.pack('<Q',ctx));u.mem_write(ctx+0xc0,struct.pack('<Q',ctx2))
lods=a+0xd000;u.mem_write(owner+0x240,struct.pack('<Q',lods));u.mem_write(lods+0x18,struct.pack('<i',3))
for i,threshold in enumerate([0,25,60]):
    p=a+0xd100+i*0x100;u.mem_write(lods+0x20+i*8,struct.pack('<Q',p));u.mem_write(p+0x1c,struct.pack('<i',threshold))
groups={};sets={};nextset=a+0xe000;identity=0
end=a+0x17000
def allocate():
    global nextset
    p=nextset;nextset+=0x100;sets[p]=set();u.mem_write(p+0x10,struct.pack('<Q',p+0x40));return p
def sync(p):u.mem_write(p+0x40+0x18,struct.pack('<i',len(sets[p])))
def hook(emu,address,size,user):
    if address in {base+0xbd0770,base+0xbd1191,base+0xbd1155,end}:emu.emu_stop()
    elif address==base+0x2a683e0:
        group=emu.reg_read(UC_X86_REG_RDX)&0xffffffff;p=groups.get(group,0)
        emu.mem_write(emu.reg_read(UC_X86_REG_R8),struct.pack('<Q',p));s['ret'](emu,int(bool(p)))
    elif address==base+0x60b310:s['ret'](emu,allocate())
    elif address==base+0x26349e0:s['ret'](emu)
    elif address==base+0x2634300:
        p=emu.reg_read(UC_X86_REG_RCX);sets[p].add(identity);sync(p);s['ret'](emu)
    elif address==base+0x2a673b0:
        groups[emu.reg_read(UC_X86_REG_RDX)&0xffffffff]=emu.reg_read(UC_X86_REG_R8);s['ret'](emu)
    elif address==base+0x2634430:
        p=emu.reg_read(UC_X86_REG_RCX);sets[p].discard(identity);sync(p);s['ret'](emu)
u.hook_add(s['unicorn'].UC_HOOK_CODE,hook)
def count(group):return len(sets[groups[group]]) if group in groups else 0
operations=[('spawn',2,1),('spawn',2,1)]+[('spawn',2,i) for i in range(2,104)]+[('spawn',3,1)]+[('remove',2,i) for i in range(1,82)]+[('remove',2,1),('spawn',2,200),('remove',4,1)]
rows=[]
for op,group,identity in operations:
    before=count(group);u.mem_write(owner+0x80,struct.pack('<i',group));selected=None
    u.reg_write(UC_X86_REG_RSP,stack);u.reg_write(UC_X86_REG_RDI,owner);u.reg_write(UC_X86_REG_R13,0)
    if op=='spawn':
        u.emu_start(base+0xbd0614,base+0xbd123c,count=1000)
        suppressed=u.reg_read(UC_X86_REG_RIP)==base+0xbd1191
        assert suppressed==(before>100)
        selected=-2 if suppressed else (u.reg_read(UC_X86_REG_R15)&0xffffffff)
        if not suppressed:
            assert selected==(2 if before>=60 else 1 if before>=25 else 0)
            u.emu_start(base+0xbd103f,base+0xbd1156,count=1000)
            assert u.reg_read(UC_X86_REG_RIP)==base+0xbd1155
    else:
        u.mem_write(stack-8,struct.pack('<Q',end));u.reg_write(UC_X86_REG_RSP,stack-8);u.reg_write(UC_X86_REG_RCX,owner)
        u.emu_start(base+0xbcf870,end+1,count=1000);assert u.reg_read(UC_X86_REG_RIP)==end
    rows.append(dict(operation=op,group=group,identity=identity,before=before,after=count(group),selected=selected))
paths=['Tools/VerifyDraugFxRegistry.py','Logs/FX-Spawn-0xbcf940.asm.txt','Logs/FX-ClearInstanceFromLODDict-0xbcf870.asm.txt']
result=dict(status='PASS_NATIVE_FX_REGISTRY_CONTROL_FLOW',cases=rows,binarySHA256=s['n']['n']['result']['sha256'],
 limits=['Dictionary and unique collection operations/allocation mocked; this verifies source call order/selection/registration/removal, not collection internals.',
 'Particle/wreck operations between selection and registration skipped. Caller must register after successful spawn.',
 'Source despawn/destroy call sites statically identified; actual runtime lifecycle integration remains pending.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugFxRegistryVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(dict(status=result['status'],steps=len(rows),finalCounts={k:count(k) for k in groups})))
