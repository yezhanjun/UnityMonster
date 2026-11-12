"""Emulate the isolated source float-math routine on bounded finite inputs. No DLL loading or OS calls."""
import sys,pathlib,runpy,contextlib,io,struct,math,json
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceNativeTypes.py')
sys.path.insert(0,str(pathlib.Path('Logs/source-parser-packages').resolve()))
import unicorn
from unicorn.x86_const import UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_XMM0,UC_X86_REG_MXCSR
uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
base=n['base'];size=n['u32'](n['optional']+56);uc.mem_map(base,(size+4095)&~4095)
for start,length,raw in n['sections']:uc.mem_write(base+start,n['binary'][raw:raw+length])
stack=0x1000000;uc.mem_map(stack,0x20000);stop=stack+0x1000;uc.mem_write(stop,b'\xcc')
def f32(value):return struct.unpack('<f',struct.pack('<f',value))[0]
results=[]
# Covers every phase of the actual normalized flight, including exact float-pi endpoint.
pi=f32(math.pi)
for index in range(257):
 t=f32(index/256);argument=f32(t*pi)
 rsp=stack+0x1fff8;uc.reg_write(UC_X86_REG_RSP,rsp);uc.mem_write(rsp,struct.pack('<Q',stop))
 uc.reg_write(UC_X86_REG_MXCSR,0x1f80)
 uc.reg_write(UC_X86_REG_XMM0,struct.unpack('<I',struct.pack('<f',argument))[0])
 uc.emu_start(base+0x678c10,stop,timeout=1000000,count=20000)
 assert uc.reg_read(UC_X86_REG_RIP)==stop,'Math routine did not return within bound'
 actual=struct.unpack('<f',struct.pack('<I',uc.reg_read(UC_X86_REG_XMM0)&0xffffffff))[0]
 expected=f32(math.sin(argument));error=abs(actual-expected)
 assert error<=1.2e-7,(t,actual,expected,error)
 results.append({'t':t,'argument':argument,'actual':actual,'expectedFloatSin':expected,'absoluteError':error})
proof={'binarySHA256':n['result']['sha256'],'routineRVA':'0x678c10','emulator':'Unicorn '+unicorn.__version__,'inputs':257,'range':'float pi times normalized progress 0..1','maxAbsoluteError':max(x['absoluteError'] for x in results),'samples':results,'conclusion':'Routine matches float sine over tested flight domain; corroborates native branch height=H*sin(pi*t).','limits':'Bounded emulation, not original-game runtime validation or proof for NaN/infinity/other inputs. Source binary remains read-only; DLL was not loaded into the OS.'}
pathlib.Path('Docs/Validation/DraugBallisticMathVerification.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print('SOURCE_BALLISTIC_MATH_PASS',len(results),'maxError',proof['maxAbsoluteError'])
for i in [0,64,128,192,256]:print(results[i])
