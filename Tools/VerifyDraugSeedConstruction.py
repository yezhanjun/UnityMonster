"""Isolated native seed construction with synthetic entropy, not OS RNG execution."""
import contextlib, hashlib, io, json, runpy, struct, sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    n = runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0, str(Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import *
base=n['n']['base'];uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
uc.mem_map(base,(n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:uc.mem_write(base+start,n['binary'][raw:raw+length])
arena=0x1000000;uc.mem_map(arena,0x20000);obj=arena+0x1000;stop=arena+0x2000
entropy=b'';requests=0
def hook(emu,address,size,user):
    global requests
    if address!=base+0x3001890:return
    assert emu.reg_read(UC_X86_REG_EDX)==16
    emu.mem_write(emu.reg_read(UC_X86_REG_RCX),entropy)
    rsp=emu.reg_read(UC_X86_REG_RSP)
    ret=struct.unpack('<Q',emu.mem_read(rsp,8))[0]
    emu.reg_write(UC_X86_REG_RSP,rsp+8);emu.reg_write(UC_X86_REG_RIP,ret)
    requests+=1
uc.hook_add(unicorn.UC_HOOK_CODE,hook)
inputs=[bytes(16),bytes([255])*16,bytes(range(16))]+[hashlib.sha256(struct.pack('<I',i)).digest()[:16] for i in range(256)]
digest=hashlib.sha256();selected=[]
for index,entropy in enumerate(inputs):
    uc.mem_write(obj,bytes(32));rsp=arena+0x18000;uc.mem_write(rsp,struct.pack('<Q',stop))
    uc.reg_write(UC_X86_REG_RSP,rsp);uc.reg_write(UC_X86_REG_RCX,obj)
    before=requests;uc.emu_start(base+0xa99fa0,stop,count=100)
    assert uc.reg_read(UC_X86_REG_RIP)==stop and requests==before+1
    transformed=bytearray(entropy);transformed[7]=(transformed[7]&15)|64;transformed[8]=(transformed[8]&63)|128
    a,b,c,d=struct.unpack('<IIII',transformed);expected=a^b^c^d
    actual=struct.unpack('<I',uc.mem_read(obj+0x18,4))[0]
    assert actual==expected and bytes(uc.mem_read(obj+0x10,8))==bytes(8)
    digest.update(entropy+struct.pack('<I',actual))
    if index<3:selected.append(dict(entropy=entropy.hex(),seedSigned=struct.unpack('<i',struct.pack('<I',actual))[0]))
# Independently resolve the external entropy call by metadata, not by its inferred behavior.
m=n['m'];h=m['h'];mi=m['integer'];ms=m['string']
im=next(i for i in n['images'] if i['name']=='mscorlib.dll');mod=n['module'](im['name']);matches=[]
for i in range(im['start'],im['start']+im['count']):
    p=h['typeDefinitions'][0]+i*m['stride'];fp=p+8+3*m['tw']+m['gw']+4
    for idx in range(mi(fp+4),mi(fp+4)+mi(fp+32,2)):
        q=h['methods'][0]+idx*32;va=n['u64'](mod['table']+((mi(q+20)&0xffffff)-1)*8)
        if va-base==0x3001890:matches.append((ms(mi(p)),ms(mi(q)),mi(q+30,2)))
assert matches==[('Interop','GetRandomBytes',2)]
paths=['Logs/Actor-.ctor-0xb0f7a0.asm.txt','Logs/RandomGenerator-.ctor-0xa99fa0.asm.txt','Logs/RandomGenerator-Initialize-0xa9a500.asm.txt','Logs/RandomGenerator-get_value-0xa9a170.asm.txt']
actor=Path(paths[0]).read_text(encoding='utf-8-sig')
assert '0xb0fff6 call 0x180a99fa0' in actor and '0xb10002 mov qword ptr [rdi + 0x4f0], rbx' in actor
observations=[]
for name in ['GFAwake','GFStart','OnSpawn','OnDespawn']:
    p=next(Path('Logs').glob('Actor-'+name+'-*.asm.txt'));text=p.read_text(encoding='utf-8-sig')
    assert '0x4f0]' not in text
    observations.append(dict(method=name,directFieldReference=False))
    paths.append(str(p).replace('\\','/'))
result=dict(status='PASS_NATIVE_DEFAULT_SEED_CONSTRUCTION',binarySHA256=n['n']['result']['sha256'],emulator=unicorn.__version__,cases=len(inputs),entropyRequests=requests,corpusSHA256=digest.hexdigest(),examples=selected,entropyCall=dict(method='Interop.GetRandomBytes',rva='0x3001890',bytes=16),actorCallbacks=observations,evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths],findings=['Actor constructor creates default RandomGenerator and stores it at SyncedRandomGenerator offset0x4f0.', 'Default RandomGenerator constructor requests16 entropy bytes, sets GUID v4 version/variant bits, XORs four little-endian32-bit words into seed at0x18. It does not initialize_random at0x10.', 'First get_value/Range lazily invokes Initialize if_random is null. Initialize may override the stored seed through debug-global-seed configuration before constructing System.Random.', 'Selected base Actor lifecycle listings contain no direct SyncedRandomGenerator field reference; this does NOT prove callbacks cannot reseed through virtual/helper calls.'],limits=['Synthetic entropy supplied by emulator; OS entropy implementation and distribution not executed.', 'Default constructor only. Later seed writes, subclass/network overrides, full pool lifecycle and debug-global override ownership remain unverified.', 'No source System.Random sequence verification or production runtime change. No new Unity run.'])
Path('Docs/Validation/DraugSeedConstructionVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_SEED_CONSTRUCTION_PASS',len(inputs),'native cases')
