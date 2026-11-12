"""Emulate isolated source skill chance/count and cooldown-flag branches. No DLL loading or OS calls."""
import sys,pathlib,runpy,contextlib,io,struct,math,json
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceNativeTypes.py')
sys.path.insert(0,str(pathlib.Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_XMM0,UC_X86_REG_MXCSR,UC_X86_REG_RBP,UC_X86_REG_R12,UC_X86_REG_R14
uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
base=n['base'];size=n['u32'](n['optional']+56);uc.mem_map(base,(size+4095)&~4095)
for start,length,raw in n['sections']:uc.mem_write(base+start,n['binary'][raw:raw+length])
stack=0x1000000;uc.mem_map(stack,0x20000);stop=stack+0x1000;uc.mem_write(stop,b'\xcc')
def f32(value):return struct.unpack('<f',struct.pack('<f',value))[0]
results=[]
helper=stack+0x4000;uc.reg_write(UC_X86_REG_RBP,helper)
for count in [-1,0,1]:
 for sample in [0,.79999,.8,.80001,1]:
  chance=f32(.8);sample=f32(sample)
  uc.mem_write(helper+0x40,struct.pack('<f',chance));uc.mem_write(helper+0x48,struct.pack('<i',count))
  uc.reg_write(UC_X86_REG_XMM0,struct.unpack('<I',struct.pack('<f',sample))[0]);uc.reg_write(UC_X86_REG_MXCSR,0x1f80)
  hit=[]
  def stopAtBranch(emu,address,size,user):
   if address in [base+0xb88446,base+0xb883f0]:hit.append(address);emu.emu_stop()
  hook=uc.hook_add(unicorn.UC_HOOK_CODE,stopAtBranch)
  uc.emu_start(base+0xb883e0,base+0xb88500,count=30);uc.hook_del(hook)
  assert len(hit)==1
  success=hit[0]==base+0xb88446;expected=chance>sample and count!=0
  assert success==expected
  results.append({'chance':chance,'randomValue':sample,'remainingCount':count,'success':success})
flags=[]
for auto in [0,1]:
 for trigger in [0,1]:
  rsp=stack+0x18000;uc.reg_write(UC_X86_REG_RSP,rsp);uc.mem_write(rsp+0xd0,bytes([auto]));uc.reg_write(UC_X86_REG_R12,trigger)
  hit=[]
  def stopAtCooldown(emu,address,size,user):
   if address in [base+0xb883fa,base+0xb88413]:hit.append(address);emu.emu_stop()
  hook=uc.hook_add(unicorn.UC_HOOK_CODE,stopAtCooldown);uc.emu_start(base+0xb883f0,base+0xb88446,count=10);uc.hook_del(hook)
  assert len(hit)==1
  cooldown=hit[0]==base+0xb883fa;assert cooldown==bool(auto and trigger)
  flags.append({'autoCast':auto,'triggerCooldown':trigger,'invokesCooldown':cooldown})
proof={'binarySHA256':n['result']['sha256'],'emulator':'Unicorn '+unicorn.__version__,'scope':'Isolated native chance/count branch and failed-attempt cooldown flag branch; external random generator and casting are not executed.','chanceCases':results,'cooldownCases':flags,'limits':'Does not establish caller frequency, cooldown implementation or full source-game behavior.'}
pathlib.Path('Docs/Validation/DraugNativeChanceVerification.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print('SOURCE_SKILL_CHANCE_PASS',len(results),'COOLDOWN_FLAGS_PASS',len(flags))
