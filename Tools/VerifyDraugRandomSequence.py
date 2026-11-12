"""Native seeded System.Random sequence comparison; runtime services are synthetic."""
import contextlib, hashlib, io, json, runpy, struct, sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    n=runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import *
base=n['n']['base'];uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
uc.mem_map(base,(n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:uc.mem_write(base+start,n['binary'][raw:raw+length])
arena=0x1000000;uc.mem_map(arena,0x20000)
obj,arr,klass,mathklass,stop,wrapper=[arena+x for x in (0x1000,0x2000,0x3000,0x4000,0x5000,0x6000)]
uc.mem_write(base+0x6295ab0,struct.pack('<Q',mathklass))
uc.mem_write(mathklass+0xe4,struct.pack('<I',1))
uc.mem_write(base+0x32aaff6+0x32bc921,b'\x01') # metadata initialized
uc.mem_write(base+0x32ab03e+0x32ae4c2,bytes(4)) # GC write-barrier disabled in synthetic heap
uc.mem_write(klass+0x178,struct.pack('<Q',base+0x32ab210)) # virtual Sample
uc.mem_write(klass+0x1a8,struct.pack('<Q',base+0x32ab770)) # virtual Next(max)
allocations=0
def hook(emu,address,size,user):
    global allocations
    if address==base+0x60a2b0:
        assert emu.reg_read(UC_X86_REG_EDX)==56
        emu.mem_write(arr,bytes(256));emu.mem_write(arr+0x18,struct.pack('<Q',56))
        emu.reg_write(UC_X86_REG_RAX,arr);allocations+=1
        rsp=emu.reg_read(UC_X86_REG_RSP);ret=struct.unpack('<Q',emu.mem_read(rsp,8))[0]
        emu.reg_write(UC_X86_REG_RSP,rsp+8);emu.reg_write(UC_X86_REG_RIP,ret)
    elif address in [base+x for x in (0x5cd560,0x5c6850,0x5bf6e0,0x5bf6f0)]:
        raise AssertionError('Unexpected metadata/exception path '+hex(address-base))
uc.hook_add(unicorn.UC_HOOK_CODE,hook)
def invoke(rva,receiver,arg=0):
    rsp=arena+0x18000;uc.mem_write(rsp,struct.pack('<Q',stop))
    uc.reg_write(UC_X86_REG_RSP,rsp);uc.reg_write(UC_X86_REG_RCX,receiver)
    uc.reg_write(UC_X86_REG_EDX,arg&0xffffffff);uc.reg_write(UC_X86_REG_MXCSR,0x1f80)
    uc.emu_start(base+rva,stop,count=20000)
    assert uc.reg_read(UC_X86_REG_RIP)==stop
    return uc.reg_read(UC_X86_REG_EAX)
def i32(x):return (x+2**31)%2**32-2**31
class Reference:
    def __init__(self,seed):
        self.a=[0]*56;self.i=0;self.j=21
        mj=161803398-(2147483647 if seed==-2147483648 else abs(seed))
        self.a[55]=mj;mk=1
        for i in range(1,55):
            k=21*i%55;self.a[k]=mk;mk=i32(mj-mk)
            if mk<0:mk=i32(mk+2147483647)
            mj=self.a[k]
        for _ in range(4):
            for i in range(1,56):
                self.a[i]=i32(self.a[i]-self.a[1+(i+30)%55])
                if self.a[i]<0:self.a[i]=i32(self.a[i]+2147483647)
    def next(self,maximum):
        self.i=self.i%55+1;self.j=self.j%55+1
        value=i32(self.a[self.i]-self.a[self.j])
        if value==2147483647:value-=1
        if value<0:value+=2147483647
        self.a[self.i]=value
        return int(value*(1.0/2147483647)*maximum)
def state_matches(ref):
    assert list(struct.unpack('<56i',uc.mem_read(arr+0x20,224)))==ref.a
    assert struct.unpack('<ii',uc.mem_read(obj+0x10,8))==(ref.i,ref.j)
seeds=[0,1,-1,161803398,-161803398,2147483647,-2147483648]+[i32(int.from_bytes(hashlib.sha256(struct.pack('<I',i)).digest()[:4],'little')) for i in range(16)]
digest=hashlib.sha256();examples=[];draws=0
for seed in seeds:
    uc.mem_write(obj,bytes(32));uc.mem_write(obj,struct.pack('<Q',klass))
    invoke(0x32aafe0,obj,seed);ref=Reference(seed);state_matches(ref)
    uc.mem_write(wrapper+0x10,struct.pack('<Q',obj));first=[]
    for index in range(1024):
        maximum=[1000000,3,1,0,2147483647][index%5]
        expected=ref.next(maximum);actual=invoke(0x32ab770,obj,maximum)
        assert actual==expected,(seed,index,actual,expected)
        state_matches(ref);digest.update(struct.pack('<iiI',seed,maximum,actual));draws+=1
        if index<5:first.append(actual)
    # Continue the SAME stream through source RandomGenerator.get_value -> native Next -> Sample.
    for index in range(128):
        integer=ref.next(1000000);invoke(0xa9a170,wrapper)
        actual=uc.reg_read(UC_X86_REG_XMM0)&0xffffffff
        expected=struct.unpack('<I',struct.pack('<f',integer/999999.0))[0]
        assert actual==expected;state_matches(ref);draws+=1
        digest.update(struct.pack('<iI',seed,actual))
    examples.append(dict(seed=seed,firstMixedNextResults=first))
assert allocations==len(seeds)
paths=['Logs/Random-.ctor-0x32aafe0.asm.txt','Logs/Random-Next-0x32ab770.asm.txt','Logs/Random-Sample-0x32ab210.asm.txt','Logs/Random-InternalSample-0x32ab230.asm.txt','Logs/RandomGenerator-get_value-0xa9a170.asm.txt']
result=dict(status='PASS_NATIVE_SEEDED_RANDOM_SEQUENCE',binarySHA256=n['n']['result']['sha256'],emulator=unicorn.__version__,seeds=len(seeds),draws=draws,perSeed=dict(mixedNext=1024,continuedWrapperValue=128),maxima=[1000000,3,1,0,2147483647],stateCheck='All56 array entries and both cursors after constructor and EVERY draw',corpusSHA256=digest.hexdigest(),examples=examples,evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths],limits=['Reference is a Python subtractive-generator model. Unity installed System.Random implementation has not yet been compared.', 'Synthetic array allocation, metadata class objects and virtual dispatch table; native constructor and all PRNG arithmetic executed, no DLL loaded or OS calls.', 'Selected nonnegative Next(max) domain only; negative-bound exceptions, Next(min,max), NextBytes and concurrent access not tested.', 'Actor seed overrides, random-consumption order, full synchronization and gameplay integration remain pending.'])
Path('Docs/Validation/DraugRandomSequenceVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_RANDOM_SEQUENCE_PASS',len(seeds),'seeds',draws,'draws with full state checks')
