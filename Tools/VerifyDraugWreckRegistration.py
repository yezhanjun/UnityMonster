"""Execute native Wreck registration/unregistration with shared static storage."""
import contextlib, hashlib, io, json, runpy, struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    s=runpy.run_path('Tools/VerifyDraugWreckSelection.py')
from unicorn.x86_const import *
u,base=s['u'],s['base'];end=s['a']+0xc000;stack=s['stack']
# Skip metadata pointer initialization only; execute both complete method bodies.
u.mem_write(base+0xad590d+0x5a88317,b'\1')
u.mem_write(base+0xad589d+0x5a88386,b'\1')
def hook(emu,address,size,user):
    if address==end:emu.emu_stop()
u.hook_add(s['unicorn'].UC_HOOK_CODE,hook)
owners=[s['owner'],s['a']+0xa000,s['a']+0xa400]
arrays=[s['array'],s['a']+0xb000,s['a']+0xb400]
counts=[9,3,0]
for owner,array,count in zip(owners,arrays,counts):
    u.mem_write(owner+0x108,struct.pack('<Q',array));u.mem_write(array+0x18,struct.pack('<i',count))
signed=lambda x:(x+2**31)%2**32-2**31
rows=[]
for initial in [0,20,2147483645]:
    u.mem_write(s['statics'],struct.pack('<ii',initial,123))
    registered=[False]*3;expected=initial
    for owner in owners:u.mem_write(owner+0x184,b'\0')
    operations=[(0,False),(0,True),(0,True),(1,True),(2,True),(2,True),(0,False),(0,False),(1,False),(2,False),(2,False),(0,True),(0,False)]
    for index,register in operations:
        if registered[index]!=register:expected=signed(expected+(counts[index] if register else -counts[index]))
        registered[index]=register
        u.mem_write(stack-8,struct.pack('<Q',end));u.reg_write(UC_X86_REG_RSP,stack-8)
        u.reg_write(UC_X86_REG_RCX,owners[index]);u.emu_start(base+(0xad5900 if register else 0xad5890),end+1,count=100)
        assert u.reg_read(UC_X86_REG_RIP)==end
        actual,counter=struct.unpack('<ii',u.mem_read(s['statics'],8))
        flags=[bool(u.mem_read(owner+0x184,1)[0]) for owner in owners]
        assert actual==expected and flags==registered and counter==123
        rows.append(dict(initial=initial,owner=index,register=register,activeBodies=actual,flags=flags,activationCounter=counter))
paths=['Tools/VerifyDraugWreckRegistration.py','Logs/Wreck-RegisterAsActive-0xad5900.asm.txt','Logs/Wreck-UnregisterAsActive-0xad5890.asm.txt']
result=dict(status='PASS_NATIVE_SHARED_WRECK_REGISTRATION',bodyCounts=counts,cases=rows,binarySHA256=s['n']['n']['result']['sha256'],
 limits=['Metadata initialization bypassed. Array lengths used without inspecting body activity, matching native code.',
 'No claim about global reset points, source removal caller coverage, FX group count or runtime death integration.'],
 evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths])
Path('Docs/Validation/DraugWreckRegistrationVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(dict(status=result['status'],cases=len(rows))))
