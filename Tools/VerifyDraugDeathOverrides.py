"""Check current source visual config and execute the selected native death override block.
This does not emulate effect selection, Unity components, or the complete death handler.
"""
import contextlib, hashlib, io, json, runpy, struct, sys
from pathlib import Path

with contextlib.redirect_stdout(io.StringIO()):
    n = runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0, str(Path('Logs/source-parser-packages').resolve()))
import UnityPy
import unicorn
from unicorn.x86_const import *

m, h = n['m'], n['h']
mi, ms = m['integer'], m['string']
enums = {}
for i in range(h['typeDefinitions'][2]):
    p = h['typeDefinitions'][0] + i*m['stride']
    if ms(mi(p)) != 'VisualEffectType':
        continue
    fp = p+8+3*m['tw']+m['gw']+4
    enums = {ms(mi(h['fields'][0]+f*m['fw'])): m['compressed'](h['fieldAndParameterDefaultValueData'][0]+m['defaults'][f])
             for f in range(mi(fp), mi(fp)+mi(fp+36, 2)) if f in m['defaults']}
assert enums['Frozen'] == 1 and enums['Poison'] == 7 and enums['Darkened'] == 8
names = {v:k for k,v in enums.items()}
config = json.loads(Path('Docs/Validation/尸鬼投掷源配置证据.json').read_text(encoding='utf-8-sig'))
bundle = Path(config['sourceBundle'])
assert hashlib.sha256(bundle.read_bytes()).hexdigest() == config['sha256']
env = UnityPy.load(str(bundle))
actor = next(o for o in env.objects if o.path_id == 121126352032497425).read_typetree()
reference = actor['visualEffects']
assert reference['m_FileID'] == 0
visual = next(o for o in env.objects if o.path_id == reference['m_PathID']).read_typetree()
effects = visual['effects']['list']
assert len(effects) == 8
overlays = []
for data in effects:
    ref = data['deathOverlay']
    if not ref['m_PathID']:
        continue
    assert ref['m_FileID'] == 0
    obj = next(o for o in env.objects if o.path_id == ref['m_PathID'])
    tree = obj.read_typetree()
    assert obj.type.name == 'Texture2D'
    overlays.append(dict(effect=names[data['type']], reference=ref, objectType=obj.type.name,
                         name=tree['m_Name'], width=tree['m_Width'], height=tree['m_Height']))
assert visual == json.loads(Path('Logs/draug-death-source-visual-effects.json').read_text(encoding='utf-8-sig'))
offsets = json.loads(Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text())['.VisualEffect']
assert [offsets[k] for k in ['overrideDeathType','customDeathType','forceExplosionDeath']] == [0x5c,0x60,0x64]

base = n['n']['base']
uc = unicorn.Uc(unicorn.UC_ARCH_X86, unicorn.UC_MODE_64)
uc.mem_map(base, (n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start, length, raw in n['n']['sections']:
    uc.mem_write(base+start, n['binary'][raw:raw+length])
arena = 0x1000000
uc.mem_map(arena, 0x20000)
owner, effect, empty_array, frame, stack = [arena+x for x in [0x1000,0x4000,0x5000,0x8000,0xa000]]
component_queries = 0

def hook(emu, address, size, user):
    global component_queries
    if address == base+0x184deb0:
        # Unity GetComponents helper is external to the selected policy block.
        assert emu.reg_read(UC_X86_REG_RCX) == owner
        component_queries += 1
        emu.reg_write(UC_X86_REG_RAX, empty_array)
        rsp = emu.reg_read(UC_X86_REG_RSP)
        emu.reg_write(UC_X86_REG_RIP, struct.unpack('<Q', emu.mem_read(rsp,8))[0])
        emu.reg_write(UC_X86_REG_RSP, rsp+8)

uc.hook_add(unicorn.UC_HOOK_CODE, hook)
cases = []
for data in effects:
    for initial in range(4):
        uc.mem_write(owner+0xa8c, struct.pack('<i',initial))
        uc.mem_write(effect, bytes(128))
        uc.mem_write(effect+0x5c, bytes([data['overrideDeathType']]))
        uc.mem_write(effect+0x60, struct.pack('<i',data['customDeathType']))
        uc.mem_write(effect+0x64, bytes([data['forceExplosionDeath']]))
        uc.mem_write(stack+0x50, struct.pack('<i',data['type']))
        for register,value in [(UC_X86_REG_RAX,effect),(UC_X86_REG_R15,effect),(UC_X86_REG_RBX,owner),
                               (UC_X86_REG_R12,0),(UC_X86_REG_RSI,1),(UC_X86_REG_RBP,frame),(UC_X86_REG_RSP,stack)]:
            uc.reg_write(register,value)
        before = component_queries
        uc.emu_start(base+0xb3f7fa, base+0xb3f892, count=100)
        assert uc.reg_read(UC_X86_REG_RIP) == base+0xb3f892
        actual = struct.unpack('<i',uc.mem_read(owner+0xa8c,4))[0]
        explosion = struct.unpack('<i',uc.mem_read(frame+0xd0,4))[0]
        expected = data['customDeathType'] if data['overrideDeathType'] else initial
        assert actual == expected and explosion == data['forceExplosionDeath']
        assert component_queries-before == int(bool(data['overrideDeathType']) and expected==1)
        cases.append(dict(effect=names[data['type']], initialDeathType=initial, finalDeathType=actual,
                          forceExplosionDeath=explosion, mockedComponentQueries=component_queries-before))

rows = [dict(name=names[e['type']], **e) for e in effects]
assert [r['name'] for r in rows if r['overrideDeathType']] == ['Frozen']
assert [r['name'] for r in rows if r['forceExplosionDeath']] == ['Poison','Darkened']
assert all(c['finalDeathType']==1 for c in cases if c['initialDeathType']==1)
paths = ['Tools/VerifyDraugDeathOverrides.py','Logs/draug-death-source-visual-effects.json',
         'Logs/AIController-OnDeath-0xb3f4d0.asm.txt','Logs/PawnController-GetDominantDeathVisualEffect-0xbb20d0.asm.txt',
         'Logs/PawnController-_GetDominantDeathVisualEffect_b__420_0-0xbc9fe0.asm.txt',
         'Docs/Validation/SourceCombatFieldOffsets.json']
result = dict(status='PASS_SOURCE_CONFIG_AND_SELECTED_NATIVE_OVERRIDE_BLOCK', binarySHA256=n['n']['result']['sha256'],
              bundleSHA256=config['sha256'], visualReference=reference, visualName=visual['m_Name'], enums=enums,
              sourceRows=rows, deathOverlayTextures=overlays, cases=cases, actualDefaultCases=8, syntheticInitialTypeCases=24,
              findings=['All eight configured effects preserve default Destroy when selected; only Frozen overrides type and writes Destroy again.',
                        'Poison and Darkened set forceExplosionDeath independently of deathType.',
                        'Frozen/Burning/Darkened reference non-null deathOverlay assets; these visuals are not implemented by default root deactivation.',
                        'All eight deathEffect.fx references are null; this is distinct from non-null deathOverlay assets.'],
              limits=['Selected native block entered after effect lookup; no proof which effect wins under simultaneous statuses.',
                      'GetComponents helper mocked with empty array; component hiding and actual Unity overlay spawning not executed.',
                      'Full AI/Pawn death pipeline, forceExplosion downstream behavior and source game runtime not executed.',
                      'No production visual implementation or new Unity smoke in this batch.'],
              evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugDeathOverrideVerification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('DRAUG_DEATH_OVERRIDE_PASS cases=32 sourceDefault=8 synthetic=24; overlays/explosion remain pending')
