"""Isolated source cooldown/range gates; not a full AI or skill lifecycle test."""
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
actor, skill, stack, stop, vtable = [memory + x for x in [0x4000, 0x8000, 0x18000, 0x1000, 0xb000]]
f32 = lambda value: struct.unpack('<f', struct.pack('<f', value))[0]
bits = lambda value: struct.unpack('<I', struct.pack('<f', value))[0]
source = json.loads(pathlib.Path('Docs/Validation/尸鬼投掷源配置证据.json').read_text(encoding='utf-8-sig'))
config = source['components']['1687912784046678327']
minimum, maximum = config['_castRange']['x'], config['_castRange']['y']
assert config['_castCondition'] == 0
uc.mem_write(skill + 0x200, struct.pack('<ffi', minimum, maximum, 0))
fields = json.loads(pathlib.Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text())
assert fields['GamesFarm.ActiveRPGSkill']['_castRange'] == 0x200
assert fields['GamesFarm.ActiveRPGSkill']['_castCondition'] == 0x208
assert fields['GamesFarm.RPGSkillStatus']['cooldownLeft'] == 0x20
assert fields['GamesFarm.RPGActor']['m_skillStatuses'] == 0x988

# Execute IsOnCooldown including its return; stub only the virtual CooldownFor result.
uc.mem_write(actor, struct.pack('<Q', vtable))
uc.mem_write(vtable + 0x1008, struct.pack('<Q', stop + 0x100))
cooldown_cases = []
for remaining in [-4, -.000001, 0, .000001, .001, 4]:
    remaining = f32(remaining)
    uc.reg_write(UC_X86_REG_RCX, actor)
    uc.reg_write(UC_X86_REG_RDX, skill)
    uc.reg_write(UC_X86_REG_RSP, stack)
    uc.mem_write(stack, struct.pack('<Q', stop))
    def supply_cooldown(emu, address, size, _):
        if address == stop + 0x100:
            emu.reg_write(UC_X86_REG_XMM0, bits(remaining))
            rsp = emu.reg_read(UC_X86_REG_RSP)
            target = struct.unpack('<Q', emu.mem_read(rsp, 8))[0]
            emu.reg_write(UC_X86_REG_RSP, rsp + 8)
            emu.reg_write(UC_X86_REG_RIP, target)
    hook = uc.hook_add(unicorn.UC_HOOK_CODE, supply_cooldown)
    uc.emu_start(base + 0xba3e70, stop, count=40)
    uc.hook_del(hook)
    assert uc.reg_read(UC_X86_REG_RIP) == stop
    actual = bool(uc.reg_read(UC_X86_REG_RAX) & 255)
    assert actual == (remaining > 0)
    cooldown_cases.append({'cooldownLeftSupplied': remaining, 'isOnCooldown': actual})

# Begin just after the IsOnCooldown call in CanCastSkill. Execute actual range comparisons and jump table.
range_cases = []
for cooling in [False, True]:
    for distance in [-.00001, 0, .00001, 5, 9.99999, 10, 10.00001]:
        distance = f32(distance)
        uc.reg_write(UC_X86_REG_RAX, int(cooling))
        uc.reg_write(UC_X86_REG_RSI, skill)
        uc.reg_write(UC_X86_REG_XMM6, bits(distance))
        uc.reg_write(UC_X86_REG_MXCSR, 0x1f80)
        destinations = []
        def stop_at_result(emu, address, size, _):
            if address in [base + 0xb879b7, base + 0xb87ecd]:
                destinations.append(address)
                emu.emu_stop()
        hook = uc.hook_add(unicorn.UC_HOOK_CODE, stop_at_result)
        uc.emu_start(base + 0xb878c2, base + 0xb87ed4, count=60)
        uc.hook_del(hook)
        assert len(destinations) == 1
        eligible = destinations[0] == base + 0xb87ecd
        assert eligible == (not cooling and minimum <= distance <= maximum)
        range_cases.append({'isOnCooldownSupplied': cooling, 'distance': distance, 'eligibleAtThisGate': eligible})

proof = {'binarySHA256': n['result']['sha256'], 'sourceBundleSHA256': source['sha256'],
    'skillPathId': 1687912784046678327, 'sourceCooldown': config['_cooldown'], 'sourceCastRange': config['_castRange'],
    'cooldownCases': cooldown_cases, 'rangeCases': range_cases,
    'staticEvidence': [
        {'rva': '0xba379e..0xba37a6', 'finding': 'CooldownFor returns zero for absent m_skillStatuses.'},
        {'rva': '0xba3841..0xba3945', 'finding': 'CooldownFor starts at zero and reduces the returned status collection by maximum cooldownLeft at offset0x20.'},
        {'rva': '0xb878a9..0xb878eb', 'finding': 'CanCastSkill calls virtual IsOnCooldown, rejects true, then rejects distance greater than max or less than min.'}],
    'limits': ['CooldownFor collection retrieval and enumeration were inspected statically, not executed.', 'Tests supply CooldownFor results and isolate downstream eligibility; other actor/cast gates remain outside scope.', 'Cooldown initialization/decrement, full cast timing and base animation selection remain unresolved.']}
pathlib.Path('Docs/Validation/DraugNativeSkillEligibilityVerification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print('SOURCE_COOLDOWN_GATE_PASS', len(cooldown_cases), 'SOURCE_SKILL_RANGE_PASS', len(range_cases))
