"""Native source empty-attachment gate and additive local-scale arithmetic."""
import contextlib,hashlib,io,json,runpy,struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):s=runpy.run_path('Tools/VerifyDraugWreckSelection.py')
from unicorn.x86_const import *
u,base=s['u'],s['base'];owner,array,stack=s['owner'],s['array'],s['stack']
endpoints={base+x for x in [0xad78ab,0xad787f,0xad85e6,0xad50a5]}
def hook(emu,address,size,user):
    if address in endpoints:emu.emu_stop()
u.hook_add(s['unicorn'].UC_HOOK_CODE,hook)
gates=[]
for length in [0,1,9]:
    u.mem_write(owner+0xe0,struct.pack('<Q',array));u.mem_write(array+0x18,struct.pack('<i',length))
    u.reg_write(UC_X86_REG_RBX,owner);u.emu_start(base+0xad7869,base+0xad85e7,count=10)
    direct=u.reg_read(UC_X86_REG_RIP)==base+0xad78ab
    assert direct==(length==0);gates.append(dict(attachmentCount=length,directActivation=direct))
rows=[]
for scale in [(1,1,1),(.5,2,3),(0,-1,.02),(100,.01,-10)]:
    for delta in [-.05,0,.05]:
        delta=s['f'](delta);scale=tuple(map(s['f'],scale))
        u.mem_write(stack+0x40,struct.pack('<fff',*scale));u.reg_write(UC_X86_REG_RSP,stack);u.reg_write(UC_X86_REG_XMM6,s['bits'](delta))
        u.emu_start(base+0xad5087,base+0xad50a6,count=10)
        actual=[s['xf'](r) for r in [UC_X86_REG_XMM0,UC_X86_REG_XMM1,UC_X86_REG_XMM2]]
        assert actual==[s['f'](x+delta) for x in scale]
        rows.append(dict(scale=scale,delta=delta,result=actual))
# Confirm icall identities from the same source binary; not guessed from offsets.
names=[]
for rva in [0x506b840,0x506b7e0]:
    raw=s['n']['offset'](base+rva);names.append(s['n']['binary'][raw:raw+200].split(b'\0')[0].decode())
assert 'get_localScale' in names[0] and 'set_localScale' in names[1],names
paths=['Tools/VerifyDraugWreckSpawnTransform.py','Docs/Validation/DraugDeathAssetEvidence.json','Logs/WreckSpawner-SpawnWrecks-0xad7420.asm.txt','Logs/Wreck-ActivateImpl-0xad45c0.asm.txt']
result=dict(status='PASS_NATIVE_ATTACHMENT_GATE_AND_ADDITIVE_SCALE',gateCases=gates,scaleCases=rows,icalls=names,binarySHA256=s['n']['n']['result']['sha256'],
 limits=['Source serialized attachToBones list is empty. This gate does not rule out external runtime mutation.',
 'Scale sample injected after draw; complete spawn/parent transform and FX lifecycle remain separate integration work.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugWreckSpawnTransformVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(dict(status=result['status'],gateCases=len(gates),scaleCases=len(rows))))
