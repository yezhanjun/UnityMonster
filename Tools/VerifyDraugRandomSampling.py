"""Verify source RNG wrappers with injected Next results; no source DLL execution."""
import contextlib, hashlib, io, json, runpy, struct, sys
from pathlib import Path

with contextlib.redirect_stdout(io.StringIO()):
    n = runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0, str(Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import *

base = n['n']['base']
uc = unicorn.Uc(unicorn.UC_ARCH_X86, unicorn.UC_MODE_64)
uc.mem_map(base, (n['n']['u32'](n['n']['optional'] + 56) + 4095) & ~4095)
for start, length, raw in n['n']['sections']:
    uc.mem_write(base + start, n['binary'][raw:raw + length])
arena = 0x1000000
uc.mem_map(arena, 0x20000)
generator, random, klass, stub, stop = [arena + x for x in (0x1000, 0x2000, 0x3000, 0x4000, 0x5000)]
uc.mem_write(generator + 0x10, struct.pack('<Q', random))
uc.mem_write(random, struct.pack('<Q', klass))
uc.mem_write(klass + 0x1a8, struct.pack('<Q', stub))
uc.mem_write(stub, b'\xc3')
uc.mem_write(stop, b'\xcc')
sample = 0
calls = 0

def hook(emu, address, size, user):
    global calls
    if address == stub:
        assert emu.reg_read(UC_X86_REG_RCX) == random
        assert emu.reg_read(UC_X86_REG_EDX) == 1000000
        emu.reg_write(UC_X86_REG_EAX, sample)
        calls += 1
    elif address == base + 0xa9a500:
        raise AssertionError('Unexpected initialization: fixture must use existing random instance')

uc.hook_add(unicorn.UC_HOOK_CODE, hook)
def bits(v):
    return struct.unpack('<I', struct.pack('<f', v))[0]

values = sorted(set([i * 999999 // 4096 for i in range(4097)] + list(range(799997, 800003))))
digest = hashlib.sha256()
selected = []
for sample in values:
    expected = bits(sample / 999999.0)
    for rva in (0xa9a020, 0xa9a170):
        rsp = arena + 0x18000
        uc.reg_write(UC_X86_REG_RSP, rsp)
        uc.mem_write(rsp, struct.pack('<Q', stop))
        uc.reg_write(UC_X86_REG_RCX, generator)
        uc.reg_write(UC_X86_REG_XMM1, bits(0))
        uc.reg_write(UC_X86_REG_XMM2, bits(1))
        uc.reg_write(UC_X86_REG_MXCSR, 0x1f80)
        before = calls
        uc.emu_start(base + rva, stop, count=100)
        assert uc.reg_read(UC_X86_REG_RIP) == stop
        actual = uc.reg_read(UC_X86_REG_XMM0) & 0xffffffff
        assert actual == expected and calls == before + 1
        digest.update(struct.pack('<III', sample, rva, actual))
    if sample in (0, 999999) or 799997 <= sample <= 800002:
        value = struct.unpack('<f', struct.pack('<I', expected))[0]
        chance = struct.unpack('<f', struct.pack('<I', bits(.8)))[0]
        selected.append(dict(nextResult=sample, value=value, chancePointEightSucceeds=chance > value))

# Resolve the actual System.Random constructor and virtual Next(maxValue) slot from metadata.
m, h = n['m'], n['h']
mi, ms = m['integer'], m['string']
resolved = []
for i in range(h['typeDefinitions'][2]):
    p = h['typeDefinitions'][0] + i * m['stride']
    if ms(mi(p)) != 'Random' or ms(mi(p + 4)) != 'System':
        continue
    im = next(im for im in n['images'] if im['start'] <= i < im['start'] + im['count'])
    mod = n['module'](im['name'])
    fp = p + 8 + 3*m['tw'] + m['gw'] + 4
    for index in range(mi(fp + 4), mi(fp + 4) + mi(fp + 32, 2)):
        q = h['methods'][0] + index * 32
        name, slot, count = ms(mi(q)), mi(q + 28, 2), mi(q + 30, 2)
        rva = n['u64'](mod['table'] + ((mi(q + 20) & 0xffffff) - 1) * 8) - base
        if name == 'Next' and slot == 7:
            assert count == 1 and 0x138 + slot * 16 == 0x1a8
            resolved.append(dict(method='System.Random.Next(maxValue)', rva=hex(rva), slot=slot))
        if name == '.ctor' and count == 1:
            assert rva == 0x32aafe0
            resolved.append(dict(method='System.Random..ctor(seed)', rva=hex(rva)))
assert len(resolved) == 2
assert n['binary'][n['offset'](0x185064cc0):].startswith(b'UnityEngine.Random::get_value()\0')
assert n['binary'][n['offset'](0x185064ce0):].startswith(b'UnityEngine.Random::RandomRangeInt(System.Int32,System.Int32)\0')
caller = Path('Logs/PawnController-ProcessSpells-0xb87f10.asm.txt').read_text(encoding='utf-8-sig')
assert 'mov rcx, qword ptr [r14 + 0x4f0]' in caller and 'call 0x180a9a020' in caller
assert '0xb88065 movss xmm7' in caller and 'rip=0x185c0592c f32=1.0' in caller
assert '0xb883d5 movaps xmm2, xmm7' in caller and '0xb883d8 xorps xmm1, xmm1' in caller
arrow = Path('Logs/ai_attack-OnStateEnter-0xa00410.asm.txt').read_text(encoding='utf-8-sig')
for instruction in ['0xa005ab lea rcx', 'rip=0x185064ce0', '0xa005c7 mov edx, 3', '0xa005cc xor ecx, ecx', '0xa005ce call rax', '0xa005d7 mov r15d, eax', '0xa00602 mov r8d, r15d', '0xa00616 mov edx, dword ptr [rdx + 0x28]']:
    assert instruction in arrow
fields = json.loads(Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text(encoding='utf-8-sig'))
assert fields['GamesFarm.Actor']['<SyncedRandomGenerator>k__BackingField'] == 0x4f0
assert fields['.AnimatorIds']['attack_index'] == 0x28
initialization = Path('Logs/RandomGenerator-Initialize-0xa9a500.asm.txt').read_text(encoding='utf-8-sig')
assert '0xa9a721 mov ebx, dword ptr [rbp + 0x18]' in initialization
assert '0xa9a733 mov edx, ebx' in initialization and '0xa9a73b call 0x1832aafe0' in initialization
paths = ['Logs/RandomGenerator-get_value-0xa9a170.asm.txt', 'Logs/RandomGenerator-Value-0xa9a0c0.asm.txt', 'Logs/RandomGenerator-Initialize-0xa9a500.asm.txt', 'Logs/RandomGenerator-Range-0xa9a020.asm.txt', 'Logs/PawnController-ProcessSpells-0xb87f10.asm.txt']
paths.append('Logs/ai_attack-OnStateEnter-0xa00410.asm.txt')
result = dict(status='PASS_NATIVE_RANDOM_WRAPPER_SAMPLING', binarySHA256=n['n']['result']['sha256'], emulator=unicorn.__version__, samplesPerWrapper=len(values), nativeWrapperCalls=calls, corpusSHA256=digest.hexdigest(), selectedCases=selected, metadataMethods=resolved, evidence=[dict(path=p, sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths], findings=['Non-null generator wrapper calls virtual System.Random.Next with maxValue1000000, then float32 divides by999999. Range(0,1) and get_value agree bitwise for tested inputs, including0 and1 endpoints.', 'ProcessSpells uses Actor.SyncedRandomGenerator at0x4f0 for Range; it is not the null-generator Unity random fallback.', 'RandomGenerator.Initialize reaches System.Random..ctor(seed); debug seed override and seed assignment require separate lifecycle tracing.', 'Static Value(null) resolves UnityEngine.Random::get_value(); this fallback is not exercised by the non-null skill chance path.'], limits=['Next results injected; native PRNG sequence, seed initialization, state ownership, consumption ordering and cross-actor synchronization NOT verified.', 'Sampled4097 uniform integer points plus6 chance-boundary points, not exhaustive allmillion results.', 'No runtime substitution or new Unity smoke performed. Arrow index selection still needs its call path resolved.'])
result['findings'].append('ai_attack.OnStateEnter resolves UnityEngine.Random::RandomRangeInt, passes0 and3, then assigns the result to AnimatorIds.attack_index. Arrow selection uses a different RNG stream from skill chance; runtime UnityEngine.Random.Range(0,3) should not be replaced by actor RNG.')
result['limits'][-1] = 'No runtime substitution or new Unity smoke performed. Arrow RNG API and bounds statically resolved; engine version sequence parity and native lifecycle ordering remain unverified.'
Path('Docs/Validation/DraugRandomSamplingVerification.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
print('DRAUG_RANDOM_SAMPLING_PASS', len(values), 'per wrapper;', calls, 'native wrapper calls')
