"""Execute the source AI startup chase-distance block, including real PRNG consumption."""
import contextlib, hashlib, io, json, runpy, struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):f=runpy.run_path('Tools/VerifyDraugRandomSequence.py')
uc=f['uc'];invoke=f['invoke'];base=f['base'];obj=f['obj'];wrapper=f['wrapper'];klass=f['klass'];actor=f['arena']+0x8000
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def bits(v):return struct.unpack('<I',struct.pack('<f',v))[0]
cases=[]
for seed in f['seeds']:
    for distance,follow in [(17.,25.),(17.,16.),(0.,25.),(17.,0.)]:
        uc.mem_write(obj,bytes(32));uc.mem_write(obj,struct.pack('<Q',klass));invoke(0x32aafe0,obj,seed)
        ref=f['Reference'](seed)
        uc.mem_write(wrapper+0x10,struct.pack('<Q',obj));uc.mem_write(actor+0x4f0,struct.pack('<Q',wrapper))
        uc.mem_write(actor+0xfd4,struct.pack('<f',distance));uc.mem_write(actor+0xfdc,struct.pack('<f',follow))
        uc.reg_write(f['UC_X86_REG_RDI'],actor);uc.reg_write(f['UC_X86_REG_RSP'],f['arena']+0x18000)
        uc.reg_write(f['UC_X86_REG_MXCSR'],0x1f80)
        uc.emu_start(base+0xb336b8,base+0xb33757,count=200)
        assert uc.reg_read(f['UC_X86_REG_RIP'])==base+0xb33757
        sample=f32(ref.next(1000000)/999999.0)
        expected=f32(f32(f32(sample*f32(.09999996423721313))+f32(.95))*distance)
        if expected>=follow:expected=f32(follow*f32(.95))
        actual=struct.unpack('<f',uc.mem_read(actor+0xfd4,4))[0]
        assert bits(actual)==bits(expected),(seed,distance,follow,actual,expected)
        f['state_matches'](ref) # proves exactly one sample, including clamped and zero-distance cases
        invoke(0xa9a170,wrapper);next_bits=uc.reg_read(f['UC_X86_REG_XMM0'])&0xffffffff
        assert next_bits==bits(ref.next(1000000)/999999.0);f['state_matches'](ref)
        cases.append(dict(seed=seed,chaseDistance=distance,chaseFollowRadius=follow,result=actual,nextValueBits=next_bits,drawsConsumed=1))
paths=['Logs/AIController-GFStart-0xb33440.asm.txt','Logs/SpawnConfig-ApplyConfig-0x154a170.asm.txt','Docs/Validation/尸鬼投掷源配置证据.json']
config=json.loads(Path(paths[2]).read_text(encoding='utf-8-sig'))['components']['121126352032497425']
assert config['chaseDistance']==17 and config['chaseFollowRadius']==25
listing=Path(paths[1]).read_text(encoding='utf-8-sig')
for line in ['0x154b553 mov rbx, qword ptr [rbx + 0x4f0]','0x154b55a movsd xmm0, qword ptr [rdi + 0x40]','0x154b5ab call rax','0x154b5db mulss xmm0, xmm4','0x154b5e5 mulss xmm1, xmm4','0x154b5ee mulss xmm2, xmm4']:assert line in listing
result=dict(status='PASS_NATIVE_STARTUP_CHASE_RANDOM',binarySHA256=f['n']['n']['result']['sha256'],cases=cases,sourceDraugCaseCount=23,syntheticClampCaseCount=69,evidence=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest()) for p in paths],findings=['Reached AIController.GFStart chase block consumes exactly one actor random sample even when output clamps or configured distance is0.', 'Source arithmetic: float32 chaseDistance*(.95f + sample*(1.05f-.95f)); if result>=chaseFollowRadius replace with .95f*chaseFollowRadius. Real Draug source configuration17/25 does not clamp for tested seeds.', 'The inspected SpawnConfig block multiplies three existing transform components by one sampled scalar from config0x40; this is a config-driven scale consumer, not a universal per-actor fixed number of draws.'],limits=['Entered the selected block with initialized generator. Entire startup method, preceding helper side effects, encounter configuration invocation/order and cold initialization not executed.', 'NextValueBits proves next stream position after this block only; not the first complete-game skill chance sample.', 'No runtime integration or new Unity smoke.'])
Path('Docs/Validation/DraugStartupRandomVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_STARTUP_RANDOM_PASS',len(cases),'cases; one real draw and following state verified')
