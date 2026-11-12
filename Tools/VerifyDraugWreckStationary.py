"""Execute complete source IsStationary with mocked Unity body accessors."""
import contextlib,hashlib,io,json,runpy,struct,sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import *
base=n['n']['base'];u=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
u.mem_map(base,(n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:u.mem_write(base+start,n['binary'][raw:raw+length])
a=0x1000000;u.mem_map(a,0x10000);owner,array,body,klass,stack,end=[a+x for x in [0x1000,0x2000,0x3000,0x6000,0x8000,0xf000]]
u.mem_write(0x1862971c8,struct.pack('<Q',klass));u.mem_write(klass+0xe4,struct.pack('<i',1))
u.mem_write(owner+0x108,struct.pack('<Q',array));u.mem_write(body+0x10,struct.pack('<Q',body))
f=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
linear=angular=(0,0,0);active=True
def ret(emu,value=None):
    if value is not None:emu.reg_write(UC_X86_REG_RAX,value)
    sp=emu.reg_read(UC_X86_REG_RSP);emu.reg_write(UC_X86_REG_RIP,struct.unpack('<Q',emu.mem_read(sp,8))[0]);emu.reg_write(UC_X86_REG_RSP,sp+8)
def hook(emu,address,size,user):
    if address==end:emu.emu_stop()
    elif address in [base+0x5cd560,base+0x5c6850]:ret(emu)
    elif address==base+0x48e2ee0:ret(emu,body)
    elif address==base+0x48e8d30:ret(emu,int(active))
    elif address in [base+0x49f2d80,base+0x49f2ef0]:
        dest=emu.reg_read(UC_X86_REG_RCX);emu.mem_write(dest,struct.pack('<fff',*(linear if address==base+0x49f2d80 else angular)));ret(emu,dest)
u.hook_add(unicorn.UC_HOOK_CODE,hook)
def squared(v):return f(f(f(v[1]*v[1])+f(v[0]*v[0]))+f(v[2]*v[2]))
rows=[]
velocities=[(0,0,0),(.3,0,0),(.30000004,0,0),(0,-.3,0),(.2,.2,.1),(0,0,1),(1.0000001,0,0)]
for l in velocities:
 for w in velocities:
  for active in [True,False]:
   linear=tuple(map(f,l));angular=tuple(map(f,w))
   u.mem_write(array+0x18,struct.pack('<i',1));u.mem_write(array+0x20,struct.pack('<Q',body))
   u.mem_write(stack,struct.pack('<Q',end));u.reg_write(UC_X86_REG_RSP,stack);u.reg_write(UC_X86_REG_RCX,owner)
   u.emu_start(base+0xad5970,end,count=2000)
   assert u.reg_read(UC_X86_REG_RIP)==end
   actual=bool(u.reg_read(UC_X86_REG_RAX)&255)
   expected=not active or not(squared(linear)>f(.09) or squared(angular)>1)
   assert actual==expected
   rows.append(dict(linear=dict(zip('xyz',linear)),angular=dict(zip('xyz',angular)),active=active,stationary=actual))
result=dict(status='PASS_FULL_NATIVE_IS_STATIONARY_WITH_ACCESSOR_MOCKS',cases=rows,binarySHA256=n['n']['result']['sha256'],
 limits=['Unity body/gameObject/active/velocity accessors and metadata initialization mocked; native loop and arithmetic executed.',
 'One body per fixture, active/inactive vectors; mixed multiple bodies, null/destroyed objects and physics simulation not covered.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in ['Tools/VerifyDraugWreckStationary.py','Logs/Wreck-IsStationary-0xad5970.asm.txt']])
Path('Docs/Validation/DraugWreckStationaryVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'cases':len(rows),'status':result['status']}))
