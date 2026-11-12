"""Execute RandomFloat.Randomize and Unity Range wrapper, injecting engine returns."""
import contextlib, hashlib, io, json, runpy, struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    s = runpy.run_path('Tools/VerifyDraugWreckSelection.py')
from unicorn.x86_const import *
u, base, n = s['u'], s['base'], s['n']
obj, stub, stack, end = s['a']+0xa000, s['a']+0xb000, s['stack'], s['a']+0xc000
method = next(m for t in n['selected'] if t['name']=='RandomFloat' for m in t['methods'] if m['RVA']==0xf26c80)
assert method['name']=='Randomize'
u.mem_write(base+0x48dc81b+0x1c98cb5, struct.pack('<Q',stub))
calls=[]
returned=0
def hook(emu, address, size, user):
    if address==end: emu.emu_stop()
    elif address==stub:
        calls.append([s['xf'](UC_X86_REG_XMM0),s['xf'](UC_X86_REG_XMM1)])
        emu.reg_write(UC_X86_REG_XMM0,s['bits'](returned));s['ret'](emu)
u.hook_add(s['unicorn'].UC_HOOK_CODE,hook)
rows=[]
for lo,hi in [(3,6),(-2,2),(-8,8),(-.05,.05),(1,1),(6,3)]:
    lo,hi=s['f'](lo),s['f'](hi)
    for value in [lo,s['f']((lo+hi)/2),hi,lo]:
        returned=value;calls.clear()
        u.mem_write(obj+0x10,struct.pack('<fff',lo,hi,12345))
        u.mem_write(stack-8,struct.pack('<Q',end))
        u.reg_write(UC_X86_REG_RSP,stack-8);u.reg_write(UC_X86_REG_RCX,obj)
        u.emu_start(base+0xf26c80,end+1,count=200)
        assert u.reg_read(UC_X86_REG_RIP)==end
        cached=struct.unpack('<f',u.mem_read(obj+0x18,4))[0]
        assert calls==[[lo,hi]] and cached==value and s['xf'](UC_X86_REG_XMM0)==value
        rows.append(dict(min=lo,max=hi,injectedEngineResult=value,cached=cached,engineCalls=len(calls)))
paths=['Tools/VerifyDraugWreckRandomFloat.py','Tools/ReadSourceMethodMap.py']
result=dict(status='PASS_NATIVE_RANDOMFLOAT_AND_UNITY_RANGE_WRAPPER',cases=rows,
    method='RandomFloat.Randomize',rva='0xf26c80',binarySHA256=n['n']['result']['sha256'],
    limits=['Engine float RNG results injected; no source PRNG sequence equivalence claim.',
            'Wrapper always calls engine once, including equal/reversed bounds. Engine handling of those bounds is not emulated.',
            'Global RNG ownership, activation ordering and full lethal integration remain separate gates.'],
    evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugWreckRandomFloatVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(dict(status=result['status'],cases=len(rows))))
