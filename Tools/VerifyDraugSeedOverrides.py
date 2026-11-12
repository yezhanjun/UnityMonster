"""Check native seed setter semantics and selected network write sites, not full RPC execution."""
import contextlib, hashlib, io, json, runpy, struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    fixture=runpy.run_path('Tools/VerifyDraugRandomSequence.py')
uc=fixture['uc'];invoke=fixture['invoke'];obj=fixture['obj'];wrapper=fixture['wrapper'];klass=fixture['klass'];Reference=fixture['Reference'];state_matches=fixture['state_matches'];n=fixture['n']
random_type=next(t for t in n['selected'] if t['name']=='RandomGenerator')
setter=next(m for m in random_type['methods'] if m['name']=='set_seed')
assert setter['RVA']==0x862f80
pairs=[(0,1),(1,0),(-1,2147483647),(-2147483648,161803398),(987654321,-123456789)]
cases=[]
for initial,replacement in pairs:
    uc.mem_write(obj,bytes(32));uc.mem_write(obj,struct.pack('<Q',klass));invoke(0x32aafe0,obj,initial)
    ref=Reference(initial)
    for _ in range(7):assert invoke(0x32ab770,obj,1000000)==ref.next(1000000)
    uc.mem_write(wrapper,bytes(32));uc.mem_write(wrapper+0x10,struct.pack('<Q',obj));uc.mem_write(wrapper+0x18,struct.pack('<i',initial))
    invoke(setter['RVA'],wrapper,replacement)
    assert struct.unpack('<i',uc.mem_read(wrapper+0x18,4))[0]==replacement
    assert struct.unpack('<Q',uc.mem_read(wrapper+0x10,8))[0]==obj
    state_matches(ref)
    for _ in range(128):
        expected=struct.unpack('<I',struct.pack('<f',ref.next(1000000)/999999.0))[0]
        invoke(0xa9a170,wrapper)
        assert uc.reg_read(fixture['UC_X86_REG_XMM0'])&0xffffffff==expected
        state_matches(ref)
    uc.mem_write(wrapper,bytes(32));invoke(setter['RVA'],wrapper,replacement)
    assert bytes(uc.mem_read(wrapper+0x10,8))==bytes(8)
    assert struct.unpack('<i',uc.mem_read(wrapper+0x18,4))[0]==replacement
    cases.append(dict(initialSeed=initial,replacementSeed=replacement,warmContinuationDraws=128,warmPointerAndStatePreserved=True,coldRemainsUninitialized=True))
checks={
 'Logs/NetAIController-Cast-0xf65260.asm.txt':['0xf653d5 mov edx, 0xf423f','0xf653da xor ecx, ecx','0xf653dc call 0x1848dc880','0xf653ea mov dword ptr [rbx + 0x18], eax','0xf6543b mov dword ptr [rcx + 0x18], eax'],
 'Logs/NetAIController-RpcCast-0xf6e3e0.asm.txt':['0xf6e663 mov rcx, qword ptr [rsi + 0x4f0]','0xf6e67b mov dword ptr [rcx + 0x18], r15d','0xf6e6a0 mov dword ptr [rcx + 0x18], r15d'],
 'Logs/NetAIController-RpcUseSkill-0xf6dba0.asm.txt':['0xf6dcf5 mov rcx, qword ptr [r14 + 0x4f0]','0xf6dd0d mov dword ptr [rcx + 0x18], r12d'],
 'Logs/AIController-GFStart-0xb33440.asm.txt':['0xb336b8 mov rbx, qword ptr [rdi + 0x4f0]','0xb33709 call rax','0xb33739 movss dword ptr [rdi + 0xfd4], xmm0'],
}
evidence=[]
for path,needles in checks.items():
    text=Path(path).read_text(encoding='utf-8-sig')
    assert all(s in text for s in needles),path
    evidence.append(dict(path=path,sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),checkedInstructions=needles))
fields=json.loads(Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text(encoding='utf-8-sig'))
start_field=next(k for k,v in fields['GamesFarm.AIController'].items() if v==0xfd4)
assert fields['GamesFarm.RPGSkill']['_randomGenerator']==0x180
result=dict(status='PASS_NATIVE_SEED_WRITE_SEMANTICS',binarySHA256=n['n']['result']['sha256'],setterRVA=hex(setter['RVA']),setterBytes=n['binary'][setter['raw']:setter['raw']+8].hex(),cases=cases,warmDraws=640,precedingNativeSequenceDraws=26496,aiStartRandomizedField=start_field,evidence=evidence,findings=['Warm RandomGenerator.set_seed changes the backing seed only; it neither replaces nor rewinds the existing System.Random sequence. Cold setter leaves_random null for later initialization.', 'Reached NetAIController.Cast branch draws Unity integer Range(0,999999), writes actor seed and copies it to skill randomGenerator seed. RpcCast writes received seed to both actor and skill; RpcUseSkill writes actor seed.', 'AIController.GFStart consumes the same actor RNG to randomize field '+start_field+' before gameplay skill draws. A skill-only new generator starting at draw0 cannot claim whole-actor sequence parity.'],limits=['Selected static network branches only; RPC dispatch, authority guards, full call graph and later Initialize calls not executed.', 'Warm continuation validates the setter operation, not a claim that a full cast can never reinitialize either generator.', 'Startup/other actor RNG consumers still need configuration-specific accounting. No runtime change, Unity smoke or new monster acceptance.'])
Path('Docs/Validation/DraugSeedOverrideVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_SEED_OVERRIDE_PASS',len(cases),'warm/cold pairs;',640,'continued native draws; startup field',start_field)
