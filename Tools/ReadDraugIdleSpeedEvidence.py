"""Read-only native idle speed assignment evidence; original game code is not executed."""
import contextlib,io,runpy,json,pathlib,sys,hashlib,bisect,struct
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
m=n['m'];h=m['h'];mi=m['integer'];ms=m['string']
sys.path.insert(0,str(pathlib.Path('Logs/source-parser-packages').resolve()))
import capstone
fields=json.loads(pathlib.Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text(encoding='utf-8-sig'))
assert fields['GamesFarm.AIController']['idleAnimationSpeed']==0x108c
assert fields['.AnimatorIds']['idle_anim_speed']==0x9c
actor=next(t for t in n['selected'] if t['name']=='AIController' and t['namespace']=='GamesFarm')
p=h['typeDefinitions'][0]+actor['index']*m['stride'];fp=p+8+3*m['tw']+m['gw']+4;start=mi(fp+24)
slot=(0x718-0x138)//16;encoded=mi(h['vtableMethods'][0]+(start+slot)*4)
assert encoded>>29==3
index=(encoded&0x1fffffff)>>1;q=h['methods'][0]+index*32
assert mi(q+28,2)==slot and ms(mi(q))=='SetAnimatorFloat'
method=next(x for x in actor['methods'] if x['name']=='SetAiState')
starts=sorted({x['raw'] for t in n['selected'] for x in t['methods'] if x['raw']})
end=starts[bisect.bisect_right(starts,method['raw'])]
engine=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64)
lines=[f'{hex(i.address-n["n"]["base"])} {i.mnemonic} {i.op_str}' for i in engine.disasm(n['binary'][method['raw']:end],method['VA'])]
listing=pathlib.Path('Logs/AIController-SetAiState-idle-speed.asm.txt');listing.write_text('\n'.join(lines),encoding='utf-8')
segment=[x for x in lines if 0xb38af9<=int(x.split()[0],16)<=0xb38b39]
assert any('test esi, esi' in x for x in lines) and any('je 0x180b38af9' in x for x in lines)
usage=struct.unpack_from('<Q',n['binary'],n['offset'](n['n']['base']+0x628a2b8))[0]
assert usage>>29==1
resolved=n['n']['resolve']((usage&0x1fffffff)>>1);assert resolved['name']=='AnimatorIds'
assert any('xmm2, dword ptr [rbx + 0x108c]' in x for x in segment)
assert any('edx, dword ptr [rdx + 0x9c]' in x for x in segment)
assert any('call qword ptr [rax + 0x718]' in x for x in segment)
configPath=pathlib.Path('Docs/Validation/尸鬼投掷源配置证据.json');config=json.loads(configPath.read_text(encoding='utf-8-sig'))
def values(obj):
 if isinstance(obj,dict):
  if 'idleAnimationSpeed' in obj:yield obj['idleAnimationSpeed']
  for v in obj.values():yield from values(v)
 elif isinstance(obj,list):
  for v in obj:yield from values(v)
speeds=list(values(config));assert speeds==[1.0],speeds
callback=pathlib.Path('Logs/ai_idle_simple-OnStateEnter-0xa0c570.asm.txt').read_text(encoding='utf-8-sig')
assert 'xor edx, edx' in callback and 'call 0x180b383e0' in callback
out={'status':'STATIC_NATIVE_ASSIGNMENT_VERIFIED','binarySHA256':n['n']['result']['sha256'],'configSource':str(configPath),'configSha256':hashlib.sha256(configPath.read_bytes()).hexdigest(),'idleAnimationSpeed':speeds[0],'animatorDefaultMeasured':.5,'virtualSlot':{'classOffset':'0x718','slot':slot,'methodIndex':index,'method':'SetAnimatorFloat'},'resolvedParameterType':resolved,'stateZeroBranch':[x for x in lines if 0xb3880e<=int(x.split()[0],16)<=0xb38810],'instructions':segment,'listing':str(listing),'findings':['ai_idle_simple.OnStateEnter invokes SetAiState with state0.','SetAiState contains assignment from AIController.idleAnimationSpeed to AnimatorIds.idle_anim_speed via SetAnimatorFloat.','Serialized Draug speed is1; isolated Animator default .5 omits original script initialization.'],'limits':['Static native evidence plus serialized data; original native callbacks not executed.','SetAiState has early returns for unchanged state, inactive actor and a global gate; assignment evidence describes the successful state-change path.','Does not prove every initialization or interruption branch, nor source script scheduling.']}
pathlib.Path('Docs/Validation/DraugIdleSpeedEvidence.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
print('DRAUG_IDLE_SPEED_ASSIGNMENT_PASS field=0x108c parameter=0x9c setter=SetAnimatorFloat value=1')
