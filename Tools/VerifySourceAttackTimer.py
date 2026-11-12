"""Execute isolated native attack gate and cast-end timer branch in mapped PE bytes."""
import contextlib, io, json, pathlib, runpy, struct, sys
with contextlib.redirect_stdout(io.StringIO()):
    n = runpy.run_path('Tools/ReadSourceNativeTypes.py')
sys.path.insert(0, str(pathlib.Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import UC_X86_REG_RSP, UC_X86_REG_RCX, UC_X86_REG_RBX, UC_X86_REG_RAX, UC_X86_REG_RIP, UC_X86_REG_XMM0, UC_X86_REG_MXCSR

uc = unicorn.Uc(unicorn.UC_ARCH_X86, unicorn.UC_MODE_64)
base = n['base']
uc.mem_map(base, (n['u32'](n['optional'] + 56) + 4095) & ~4095)
for start, length, raw in n['sections']:
    uc.mem_write(base + start, n['binary'][raw:raw + length])
memory = 0x1000000
uc.mem_map(memory, 0x20000)
actor, stack, stop = memory + 0x4000, memory + 0x18000, memory + 0x1000
f32 = lambda x: struct.unpack('<f', struct.pack('<f', x))[0]
cases = []
for impairment in [-1, 0, 1]:
    for timer in [-1, 0, .0009999, .001, .0010001, 1, 4]:
        timer = f32(timer)
        uc.mem_write(actor + 0x7f0, struct.pack('<i', impairment))
        uc.mem_write(actor + 0x1168, struct.pack('<f', timer))
        uc.mem_write(stack, struct.pack('<Q', stop))
        uc.reg_write(UC_X86_REG_RSP, stack)
        uc.reg_write(UC_X86_REG_RCX, actor)
        uc.reg_write(UC_X86_REG_MXCSR, 0x1f80)
        uc.emu_start(base + 0xb564a0, stop, count=30)
        assert uc.reg_read(UC_X86_REG_RIP) == stop
        actual = bool(uc.reg_read(UC_X86_REG_RAX) & 255)
        assert actual == (impairment == 0 and timer <= f32(.001))
        cases.append({'impairmentStack': impairment, 'timer': timer, 'canAttack': actual})

cast_cases = []
for synced in [False, True]:
    for interval in [1.5, 4]:
        uc.mem_write(actor + 0x1168, struct.pack('<f', -2))
        uc.reg_write(UC_X86_REG_RBX, actor)
        uc.reg_write(UC_X86_REG_RAX, int(synced))
        uc.reg_write(UC_X86_REG_RSP, stack)
        calls = []
        def stub_interval(emu, address, size, _):
            if address == base + 0xb572a0:
                calls.append('get_attackInterval')
                emu.reg_write(UC_X86_REG_XMM0, struct.unpack('<I', struct.pack('<f', interval))[0])
                rsp = emu.reg_read(UC_X86_REG_RSP)
                return_address = struct.unpack('<Q', emu.mem_read(rsp, 8))[0]
                emu.reg_write(UC_X86_REG_RSP, rsp + 8)
                emu.reg_write(UC_X86_REG_RIP, return_address)
        hook = uc.hook_add(unicorn.UC_HOOK_CODE, stub_interval)
        uc.emu_start(base + 0xb37c37, base + 0xb37c4d, count=30)
        uc.hook_del(hook)
        assert uc.reg_read(UC_X86_REG_RIP) == base + 0xb37c4d
        result = struct.unpack('<f', uc.mem_read(actor + 0x1168, 4))[0]
        assert result == (interval if synced else -2)
        assert len(calls) == int(synced)
        cast_cases.append({'skillsSyncedWithAttacks': synced, 'suppliedAttackInterval': interval, 'timerAfter': result})

proof = {'binarySHA256': n['result']['sha256'], 'emulator': unicorn.__version__, 'gateCases': cases, 'castEndCases': cast_cases,
    'staticEvidence': [
        {'rva': '0xb49755..0xb49760', 'finding': 'StartAttackAnimation assigns get_attackInterval to m_attackTimer.'},
        {'rva': '0xb433c1..0xb433c6', 'finding': 'OnAttackStart also assigns get_attackInterval to m_attackTimer.'},
        {'rva': '0xb3716e..0xb37181', 'finding': 'GFUpdate subtracts result of RVA0x9587d0 from m_attackTimer.'},
        {'rva': '0xb37c32..0xb37c45', 'finding': '_DoCastEnd checks get_skillsSyncedWithAttacks; when true assigns get_attackInterval to m_attackTimer.'}],
    'limits': ['Finite gate values tested; full AI not executed.', 'Cast-end test supplies synchronization flag and stubs attackInterval getter; full animation callback chain not executed.', 'Time-provider semantics, skill cooldown and animation variation remain unresolved.']}
pathlib.Path('Docs/Validation/DraugNativeAttackTimerVerification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print('SOURCE_ATTACK_GATE_PASS', len(cases), 'CAST_END_TIMER_PASS', len(cast_cases))
