"""Execute native PrepareCastedSkill with synthetic Unity objects and cast-ID dictionary callback."""
import contextlib, hashlib, io, json, runpy, struct, sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    fixture=runpy.run_path('Tools/VerifyDraugRandomSequence.py')
n=fixture['n'];uc=fixture['uc'];base=fixture['base'];arena=fixture['arena'];stop=fixture['stop']
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import capstone,unicorn
from unicorn.x86_const import *
actor,pending,skill_a,skill_b,random_a,random_b,unity_class=[arena+x for x in (0x8000,0xa000,0xb000,0xc000,0xd000,0xe000,0xf000)]
uc.mem_write(base+0x62971c8,struct.pack('<Q',unity_class));uc.mem_write(unity_class+0xe4,struct.pack('<I',1))
cs=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64);cs.detail=True
raw=n['offset'](base+0xbbe420)
for ins in cs.disasm(n['binary'][raw:raw+0x220],base+0xbbe420):
    if ins.mnemonic=='cmp' and ins.operands[0].type==capstone.x86.X86_OP_MEM and ins.operands[0].size==1 and ins.operands[0].mem.base==capstone.x86.X86_REG_RIP:
        uc.mem_write(ins.address+ins.size+ins.operands[0].mem.disp,b'\x01')
calls=[]
def callback(emu,address,size,user):
    if address!=base+0xe3b400:return
    calls.append((emu.reg_read(UC_X86_REG_RCX),emu.reg_read(UC_X86_REG_RDX),emu.reg_read(UC_X86_REG_R8D)))
    rsp=emu.reg_read(UC_X86_REG_RSP);ret=struct.unpack('<Q',emu.mem_read(rsp,8))[0]
    emu.reg_write(UC_X86_REG_RSP,rsp+8);emu.reg_write(UC_X86_REG_RIP,ret)
uc.hook_add(unicorn.UC_HOOK_CODE,callback)
cases=[]
for seed in [0,1,-1,-2147483648,2147483647]:
    for a_exists,b_exists in [(False,False),(True,False),(False,True),(True,True)]:
        for p in [actor,pending,skill_a,skill_b,random_a,random_b]:uc.mem_write(p,bytes(0x1000))
        uc.mem_write(actor+0xbd0,struct.pack('<Q',pending))
        uc.mem_write(pending+0x18,struct.pack('<Q',skill_a if a_exists else 0))
        uc.mem_write(actor+0x820,struct.pack('<Q',skill_b if b_exists else 0))
        for s,r in [(skill_a,random_a),(skill_b,random_b)]:
            uc.mem_write(s+0x10,struct.pack('<Q',1));uc.mem_write(s+0x180,struct.pack('<Q',r))
            uc.mem_write(r+0x10,struct.pack('<Q',0x12345678));uc.mem_write(r+0x18,struct.pack('<i',42))
        actor_rng=arena+0x7000
        uc.mem_write(actor_rng,bytes(32));uc.mem_write(actor_rng+0x10,struct.pack('<Q',0x87654321));uc.mem_write(actor_rng+0x18,struct.pack('<i',99))
        uc.mem_write(actor+0x4f0,struct.pack('<Q',actor_rng));actor_before=bytes(uc.mem_read(actor_rng,32))
        calls.clear();rsp=arena+0x18000;uc.mem_write(rsp,struct.pack('<Q',stop))
        uc.reg_write(UC_X86_REG_RSP,rsp);uc.reg_write(UC_X86_REG_RCX,actor);uc.reg_write(UC_X86_REG_EDX,17);uc.reg_write(UC_X86_REG_R8D,seed&0xffffffff)
        uc.emu_start(base+0xbbe420,stop,count=1000);assert uc.reg_read(UC_X86_REG_RIP)==stop
        expected_calls=[(s,actor,17) for s,exists in [(skill_a,a_exists),(skill_b,b_exists)] if exists]
        assert calls==expected_calls
        for r,exists in [(random_a,a_exists),(random_b,b_exists)]:
            assert struct.unpack('<i',uc.mem_read(r+0x18,4))[0]==(seed if exists else 42)
            assert struct.unpack('<Q',uc.mem_read(r+0x10,8))[0]==0x12345678
        assert bytes(uc.mem_read(actor_rng,32))==actor_before
        cases.append(dict(seed=seed,pendingSkill=a_exists,currentSkill=b_exists,castIdCalls=len(calls),actorUnchanged=True,skillRandomPointersPreserved=True))
paths=['Logs/PawnController-PrepareCastedSkill-0xbbe420.asm.txt','Logs/ActiveRPGSkill-SetCastId-0xe3b400.asm.txt','Logs/ActiveRPGSkill-Cast-0xe3b540.asm.txt','Logs/RPGSkill-Cast-0xe4c0c0.asm.txt']
fields=json.loads(Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text(encoding='utf-8-sig'))
slot_names={hex(off):[(t,k) for t,fs in fields.items() for k,v in fs.items() if v==off and t in ['GamesFarm.PawnController','GamesFarm.RPGActor']] for off in [0xbd0,0x820]}
result=dict(status='PASS_NATIVE_CAST_RANDOM_PREPARATION',binarySHA256=n['n']['result']['sha256'],cases=cases,slotFields=slot_names,evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths],findings=['Native PrepareCastedSkill writes seed only to each present skill randomGenerator; actor random fields and existing skill_random pointers remain unchanged in all20 cases.', 'SetCastId calls occur in pending-then-current order with requested actor and castId. Dictionary mutation is mocked, not executed.', 'Direct listings of inspected PrepareCastedSkill/SetCastId/ActiveRPGSkill.Cast/RPGSkill.Cast contain no explicit RandomGenerator.Initialize call; RPGSkill.Cast can draw from skill RNG through Range, whose lazy initialization was separately verified.'],limits=['Unity object validity and metadata represented with synthetic fixtures; missing pending container and invalid/destroyed object exception branches not tested.', 'Does not execute SetCastId dictionary implementation, full Cast virtual callbacks, all derived skill behavior or RPC authority; no global claim that casting never reinitializes RNG.', 'No production change, new Unity smoke or complete monster acceptance.'])
for p in paths:assert 'call 0x180a9a500' not in Path(p).read_text(encoding='utf-8-sig')
Path('Docs/Validation/DraugCastRandomPreparationVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_CAST_RANDOM_PREPARATION_PASS',len(cases),'cases',slot_names)
