"""Execute source death-visual selection with real config and mocked runtime plumbing."""
import contextlib, hashlib, io, json, runpy, struct
from pathlib import Path

with contextlib.redirect_stdout(io.StringIO()):
    fixture = runpy.run_path('Tools/VerifyDraugDeathOverrides.py')
n, effects, names = fixture['n'], fixture['effects'], fixture['names']
unicorn = fixture['unicorn']
from unicorn.x86_const import *

base = n['n']['base']
uc = unicorn.Uc(unicorn.UC_ARCH_X86, unicorn.UC_MODE_64)
uc.mem_map(base, (n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:
    uc.mem_write(base+start,n['binary'][raw:raw+length])
arena = 0x2000000
uc.mem_map(arena,0x40000)
owner, visual, entries, unity_class, owner_class, stub, stop = [arena+x for x in [0x1000,0x3000,0x4000,0x5000,0x6000,0x8000,0x9000]]
def put64(p,v): uc.mem_write(p,struct.pack('<Q',v))
def put32(p,v): uc.mem_write(p,struct.pack('<i',v))
put64(owner,owner_class);put64(owner+0xdf8,visual);put64(visual+0x10,entries)
put64(base+0x62971c8,unity_class);put32(unity_class+0xe4,1)
put64(owner_class+0x11a8,stub)
uc.mem_write(stub,b'\xc3')
objects=[]
for i,data in enumerate(effects):
    obj=arena+0x10000+i*0x200
    objects.append(obj)
    put32(obj+0x10,data['type'])
    material=obj+0x100
    if data['deathMaterial']['m_PathID']:
        put64(obj+0x40,material);put64(material+0x10,123)
    fx=obj+0x120
    put64(obj+0x28,fx) # serialized deathEffect object exists, but fx reference is null
    assert data['deathEffect']['fx']['m_PathID']==0
    uc.mem_write(obj+0x5c,bytes([data['overrideDeathType']]))
    put32(obj+0x60,data['customDeathType'])
calls=[]
cursor=0
def return_to_caller(emu):
    rsp=emu.reg_read(UC_X86_REG_RSP)
    emu.reg_write(UC_X86_REG_RIP,struct.unpack('<Q',emu.mem_read(rsp,8))[0])
    emu.reg_write(UC_X86_REG_RSP,rsp+8)
def hook(emu,address,size,user):
    global cursor
    if address==base+0x5cd560: # IL2CPP metadata resolution; used fields supplied explicitly
        return_to_caller(emu)
    elif address==base+0x2c326b0: # List<VisualEffect>.Enumerator.MoveNext
        enumerator=emu.reg_read(UC_X86_REG_RCX)
        available=cursor<len(objects)
        put64(enumerator+0x10,objects[cursor] if available else 0)
        cursor+=1
        emu.reg_write(UC_X86_REG_EAX,int(available))
        return_to_caller(emu)
    elif address==base+0xbbbda0:
        assert emu.reg_read(UC_X86_REG_RCX)==owner
        calls.append(dict(method='EnableAllVisualEffects',enabled=emu.reg_read(UC_X86_REG_EDX)))
        return_to_caller(emu)
    elif address==stub:
        assert emu.reg_read(UC_X86_REG_RCX)==owner
        calls.append(dict(method='EnableVisualEffect',effect=emu.reg_read(UC_X86_REG_EDX),
                          enabled=emu.reg_read(UC_X86_REG_R8)&255,context=emu.reg_read(UC_X86_REG_R9D)))
    elif address in [base+0x5bf6e0,base+0x5bf6d0,base+0x5c6850]:
        raise AssertionError('Unexpected runtime error/class initialization '+hex(address))
uc.hook_add(unicorn.UC_HOOK_CODE,hook)
cases=[]
for initial in range(4):
    for requested in range(11):
        cursor=0;calls.clear();put32(owner+0xa8c,initial)
        rsp=arena+0x30000
        uc.reg_write(UC_X86_REG_RSP,rsp);put64(rsp,stop)
        uc.reg_write(UC_X86_REG_RCX,owner);uc.reg_write(UC_X86_REG_EDX,requested)
        uc.emu_start(base+0xbbb930,stop,count=1500)
        assert uc.reg_read(UC_X86_REG_RIP)==stop
        data=next((d for d in effects if d['type']==requested),None)
        expected=bool(data and data['deathMaterial']['m_PathID'] and
                      (initial!=1 or data['overrideDeathType'] and data['customDeathType']==1))
        expected_calls=[dict(method='EnableAllVisualEffects',enabled=0),
                        dict(method='EnableVisualEffect',effect=requested,enabled=1,context=2)] if expected else []
        assert calls==expected_calls,(initial,requested,calls,expected_calls)
        cases.append(dict(initialDeathType=initial,requested=names.get(requested,str(requested)),enabled=expected,calls=list(calls)))
# Resolve the virtual call and context value independently from source metadata.
m,h=n['m'],n['h'];mi,ms=m['integer'],m['string']
typ=next(t for t in n['selected'] if t['name']=='PawnController' and t['namespace']=='GamesFarm')
p=h['typeDefinitions'][0]+typ['index']*m['stride'];fp=p+8+3*m['tw']+m['gw']+4
encoded=mi(h['vtableMethods'][0]+(mi(fp+24)+(0x11a8-0x138)//16)*4)
assert encoded>>29==3
q=h['methods'][0]+((encoded&0x1fffffff)>>1)*32
assert ms(mi(q))=='EnableVisualEffect'
context={}
for i in range(h['typeDefinitions'][2]):
    p=h['typeDefinitions'][0]+i*m['stride']
    if ms(mi(p))!='VisualEffectContext':continue
    fp=p+8+3*m['tw']+m['gw']+4
    context={ms(mi(h['fields'][0]+f*m['fw'])):m['compressed'](h['fieldAndParameterDefaultValueData'][0]+m['defaults'][f])
             for f in range(mi(fp),mi(fp)+mi(fp+36,2)) if f in m['defaults']}
assert context['Death']==2
default_enabled=[c['requested'] for c in cases if c['initialDeathType']==1 and c['enabled']]
assert default_enabled==['Frozen']
paths=['Tools/VerifyDraugDeathVisualEligibility.py','Logs/PawnController-_SetFirstDefinedVisualEffectDeath-0xbbb930.asm.txt',
       'Logs/PawnController-ApplyFirstDefinedDeathVisualEffects-0xba7c10.asm.txt',
       'Logs/VisualEffectStatus-SpawnVisualEffectsDeath-0xd88ad0.asm.txt','Docs/Validation/DraugDeathOverrideVerification.json']
out=dict(status='PASS_NATIVE_DEATH_VISUAL_ELIGIBILITY',cases=cases,contextEnum=context,defaultEnabled=default_enabled,
         findings=['For actual source visual configs and deathType Destroy, only requested Frozen reaches EnableVisualEffect(context Death).',
                   'Burning/Darkened deathOverlay asset presence alone does not establish that their death visual will be enabled.',
                   'VisualEffectStatus.SpawnVisualEffectsDeath separately checks applyOverlay before Actor.SetOverlay; Burning applyOverlay is false.'],
         limits=['Native _SetFirstDefinedVisualEffectDeath executed; IL2CPP metadata, List enumeration and visual activation endpoints mocked.',
                 'Caller selection, active-effect state, global FX options, actual material/texture rendering and critical-death spawning are not executed.',
                 'Non-Destroy initial types are synthetic controls; original Draug default is Destroy.',
                 'No new production implementation or Unity runtime test.'],
         evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugDeathVisualEligibilityVerification.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('DRAUG_DEATH_VISUAL_ELIGIBILITY_PASS cases=44 defaultEnabled=Frozen')
