"""Isolated native cooldown time cap and decrement verification; no native DLL loading."""
import contextlib, io, json, pathlib, runpy, struct, sys
with contextlib.redirect_stdout(io.StringIO()):
    n = runpy.run_path('Tools/ReadSourceNativeTypes.py')
sys.path.insert(0, str(pathlib.Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import *
uc = unicorn.Uc(unicorn.UC_ARCH_X86, unicorn.UC_MODE_64)
base = n['base']
uc.mem_map(base, (n['u32'](n['optional'] + 56) + 4095) & ~4095)
for start, length, raw in n['sections']:
    uc.mem_write(base + start, n['binary'][raw:raw + length])
memory = 0x1000000
uc.mem_map(memory, 0x20000)
stack, stop, stub, status = memory + 0x18000, memory + 0x1000, memory + 0x1100, memory + 0x4000
f32 = lambda v: struct.unpack('<f', struct.pack('<f', v))[0]
bits = lambda v: struct.unpack('<I', struct.pack('<f', v))[0]
raw = n['offset'](base + 0x5069e18)
icall = n['binary'][raw:n['binary'].index(b'\0', raw)].decode()
assert icall == 'UnityEngine.Time::get_deltaTime()'
uc.mem_write(base + 0x6575db8, struct.pack('<Q', stub))
cases = []
uncapped_cases = []
for dt in [0, 1/240, 1/60, .049999, .05, .050001, .1, .5]:
    dt = f32(dt)
    uc.mem_write(stack, struct.pack('<Q', stop)); uc.reg_write(UC_X86_REG_RSP, stack)
    calls = []
    def supply_time(emu, address, size, _):
        if address == stub:
            calls.append(address)
            emu.reg_write(UC_X86_REG_XMM0, bits(dt))
            rsp = emu.reg_read(UC_X86_REG_RSP)
            target = struct.unpack('<Q', emu.mem_read(rsp, 8))[0]
            emu.reg_write(UC_X86_REG_RSP, rsp + 8)
            emu.reg_write(UC_X86_REG_RIP, target)
    hook = uc.hook_add(unicorn.UC_HOOK_CODE, supply_time)
    uc.emu_start(base + 0xaa7d90, stop, count=80)
    uc.hook_del(hook)
    assert uc.reg_read(UC_X86_REG_RIP) == stop
    result = struct.unpack('<f', struct.pack('<I', uc.reg_read(UC_X86_REG_XMM0) & 0xffffffff))[0]
    assert result == min(dt, f32(.05))
    cases.append({'deltaTimeSupplied': dt, 'cappedDeltaTime': result, 'timeGetterCalls': len(calls)})
    uc.mem_write(stack, struct.pack('<Q', stop)); uc.reg_write(UC_X86_REG_RSP, stack)
    hook = uc.hook_add(unicorn.UC_HOOK_CODE, supply_time)
    uc.emu_start(base + 0x9587d0, stop, count=80)
    uc.hook_del(hook)
    assert uc.reg_read(UC_X86_REG_RIP) == stop
    result = struct.unpack('<f', struct.pack('<I', uc.reg_read(UC_X86_REG_XMM0) & 0xffffffff))[0]
    assert result == dt
    uncapped_cases.append({'deltaTimeSupplied': dt, 'attackAndBallisticDeltaTime': result})
decrements = []
for remaining, delta in [(4, .05), (.02, .05), (0, .05), (1, 0)]:
    uc.reg_write(UC_X86_REG_RBP, status)
    uc.reg_write(UC_X86_REG_XMM6, bits(remaining)); uc.reg_write(UC_X86_REG_XMM0, bits(delta))
    uc.reg_write(UC_X86_REG_R14, 0)
    uc.emu_start(base + 0xba6188, base + 0xba6197, count=10)
    actual = struct.unpack('<f', uc.mem_read(status + 0x20, 4))[0]
    assert actual == f32(f32(remaining) - f32(delta))
    decrements.append({'remaining': remaining, 'deltaSupplied': delta, 'after': actual})
proof = {'binarySHA256': n['result']['sha256'], 'timeInternalCall': icall, 'capCases': cases,
         'decrementCases': decrements, 'uncappedTimeCases': uncapped_cases,
         'staticEvidence': [
             {'rva': '0xba408b..0xba4090', 'finding': 'TriggerCooldown writes supplied duration to both cooldownLeft and cooldown.'},
             {'rva': '0xba5f18..0xba5f63', 'finding': 'For nonconstant skills, nonzero maxCooldown*multiplier gives factor=maxCooldown/(maxCooldown*multiplier); otherwise factor remains1. Returns capped scaled deltaTime*factor.'},
             {'rva': '0xba60a9..0xba6174', 'finding': 'UpdateSkillCooldowns checks expiration before decrement; expired statuses are queued for removal and may restore passive effects.'},
             {'rva': '0xba6188..0xba6192', 'finding': 'Subtracts supplied cooldown delta without zero clamp.'}],
         'limits': ['Time getter is stubbed with stable finite values; no Unity game execution.', 'Collection iteration, multiplier lookup, status creation timing and removal callbacks were inspected only where stated, not executed.', 'RVA0x9587d0 is mapped to UnityEngine.Time.get_deltaTime and verified to forward supplied scaled deltaTime; this does not execute a full attack or projectile.']}
pathlib.Path('Docs/Validation/DraugCooldownTimeVerification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print('SOURCE_COOLDOWN_TIME_CAP_PASS', len(cases), 'DECREMENT_PASS', len(decrements), 'UNCAPPED_TIME_PASS', len(uncapped_cases))
