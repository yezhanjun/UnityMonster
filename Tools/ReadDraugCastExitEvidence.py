"""Read-only static cast-exit fallback evidence; native handlers are not executed."""
import contextlib,io,runpy,json,hashlib
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()): n=runpy.run_path('Tools/ReadSourceMethodMap.py')
m=n['m'];h=m['h'];mi=m['integer'];ms=m['string']
actor=next(t for t in n['selected'] if t['name']=='AIController' and t['namespace']=='GamesFarm')
p=h['typeDefinitions'][0]+actor['index']*m['stride'];fp=p+8+3*m['tw']+m['gw']+4;start=mi(fp+24)
slots=[]
for displacement in [0xb98,0xc58]:
 slot=(displacement-0x138)//16;encoded=mi(h['vtableMethods'][0]+(start+slot)*4);assert encoded>>29==3
 index=(encoded&0x1fffffff)>>1;q=h['methods'][0]+index*32;assert mi(q+28,2)==slot
 ti=mi(q+4,2);tp=h['typeDefinitions'][0]+ti*m['stride']
 slots.append(dict(classOffset=hex(displacement),slot=slot,method=ms(mi(q)),declaringType=ms(mi(tp))))
fields=json.loads(Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text(encoding='utf-8-sig'))
assert fields['GamesFarm.RPGActor']['m_castingState']==0x7e4
parts=[]
for filename,begin,end in [('Logs/ai_cast-OnStateExit-0xa05d00.asm.txt',0xa05e15,0xa05e2f),('Logs/PawnController-OnCastEndEvent-0xba1050.asm.txt',0xba12fd,0xba133d)]:
 lines=Path(filename).read_text(encoding='utf-8-sig').splitlines();selected=[s for s in lines if begin<=int(s.split()[0],16)<=end]
 assert selected and int(selected[0].split()[0],16)==begin
 parts.append(dict(path=filename,sha256=hashlib.sha256(Path(filename).read_bytes()).hexdigest(),instructions=selected))
result={'status':'STATIC_CAST_EXIT_FALLBACK_VERIFIED','binarySHA256':n['n']['result']['sha256'],'castingStateOffset':'0x7e4','virtualSlots':slots,'evidence':parts,'findings':['ai_cast.OnStateExit checks m_castingState==2 and, after prior object-validity gates, calls OnCastEndEvent with null event.','Reached normal end branch invokes virtual slot0xb98 and slot0xc58 before ProcessAnimationEvent; this call order cannot be modeled as an event-weight filter before all end effects.'],'limits':['Static selected branches only; cast-repeat/other casting-state paths and all earlier gates are not simulated.','Original native callbacks are not executed by Unity comparison fixture; do not equate test event delivery with complete native cast-end behavior.']}
Path('Docs/Validation/DraugCastExitEvidence.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_CAST_EXIT_EVIDENCE_PASS',slots)
