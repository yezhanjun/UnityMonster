"""Run source float multiply and actual native round-to-even helper."""
import contextlib,hashlib,io,json,runpy,struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):s=runpy.run_path('Tools/VerifyDraugWreckSelection.py')
u,base,n=s['u'],s['base'],s['n'];unicorn=s['unicorn']
from unicorn.x86_const import *
f=s['f'];bits=s['bits'];stack=s['stack'];array=s['array'];klass=s['klass']
u.mem_write(base+0x6e5cbb+0x5e719f5,b'\1')
u.mem_write(base+0x6e5ce4+0x5bafdcc,struct.pack('<Q',klass));u.mem_write(klass+0xe4,struct.pack('<i',1))
def stop(emu,address,size,user):
    if address==base+0xad4ac5:emu.emu_stop()
u.hook_add(unicorn.UC_HOOK_CODE,stop)
rows=[]
for count in [1,2,9,16]:
    samples={0.,1.}
    for k in range(count-1):
        half=f((k+.5)/(count-1));raw=bits(half)
        for value in [raw-1,raw,raw+1]:samples.add(struct.unpack('<f',struct.pack('<I',value))[0])
    for sample in sorted(samples):
        u.mem_write(array+0x18,struct.pack('<i',count));u.reg_write(UC_X86_REG_RAX,array)
        u.reg_write(UC_X86_REG_XMM1,bits(sample));u.reg_write(UC_X86_REG_RSP,stack)
        u.emu_start(base+0xad4ab0,base+0xad4ac6,count=20000)
        assert u.reg_read(UC_X86_REG_RIP)==base+0xad4ac5
        actual=u.reg_read(UC_X86_REG_RAX)&0xffffffff
        expected=round(f((count-1)*sample))
        assert actual==expected,(count,sample,actual,expected)
        rows.append(dict(bodyCount=count,sample=sample,index=actual))
paths=['Tools/VerifyDraugWreckStartIndex.py','Logs/Wreck-ActivateImpl-0xad45c0.asm.txt']
result=dict(status='PASS_NATIVE_START_MULTIPLY_AND_ROUND_HELPER',cases=rows,binarySHA256=n['n']['result']['sha256'],
 limits=['Random sample injected immediately before native multiplication. Native round/helper arithmetic executed; metadata class initialization bypassed.',
 'No claim about source global Unity RNG sequence or shared-counter lifetime.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugWreckStartIndexVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'cases':len(rows),'status':result['status']}))
