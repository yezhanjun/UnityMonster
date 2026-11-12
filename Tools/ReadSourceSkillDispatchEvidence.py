"""Persist native skill dispatch signatures and checked virtual-slot identities."""
import runpy,contextlib,io,pathlib,json
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
m=n['m'];h=m['h'];mi=m['integer'];ms=m['string']
actor=next(t for t in n['selected'] if t['name']=='AIController' and t['namespace']=='GamesFarm')
p=h['typeDefinitions'][0]+actor['index']*m['stride'];fp=p+8+3*m['tw']+m['gw']+4;start=mi(fp+24)
virtuals=[]
for displacement in [0xbf8,0x1038,0xf98,0x1188,0x918,0x8d8,0xe38,0x1078,0x1018,0xf88,0x1008,0x748,0x738,0x1098,0x1048,0x9f8,0xef8]:
 slot=(displacement-0x138)//16;encoded=mi(h['vtableMethods'][0]+(start+slot)*4)
 assert encoded>>29==3
 index=(encoded&0x1fffffff)>>1;q=h['methods'][0]+index*32
 assert mi(q+28,2)==slot,(displacement,index,mi(q+28,2),slot)
 ti=mi(q+4,2);tp=h['typeDefinitions'][0]+ti*m['stride']
 virtuals.append({'classOffset':hex(displacement),'slot':slot,'methodIndex':index,'declaringType':ms(mi(tp)),'method':ms(mi(q))})
signatures=[]
for typ in n['selected']:
 for method in typ['methods']:
  if (typ['name']=='PawnController' and method['name'] in ['ProcessSpells','ProcessSpellsCast','TriggerCooldown']) or (typ['name']=='RandomGenerator' and method['RVA']==0xa9a020):
   q=h['methods'][0]+method['index']*32;first=mi(q+14);params=[]
   for i in range(method['parameterCount']):
    p=h['parameters'][0]+(first+i)*12;params.append({'name':ms(mi(p)),'typeIndex':mi(p+8)})
   signatures.append({'type':typ['name'],'method':method['name'],'RVA':hex(method['RVA']),'parameters':params})
proof={'binarySHA256':n['n']['result']['sha256'],'virtualCallOffsetBasis':'0x138 class vtable base, 16-byte pointer/method-info entries; all selected metadata method slot numbers cross-checked','virtualCalls':virtuals,'signatures':signatures,'confirmed':[{'rva':'0xb88003..0xb88028','finding':'skillsSyncedWithAttacks=true and useType=None require virtual get_canAttack before selecting candidates.'},{'rva':'0xb88165..0xb88185','finding':'CanCastSkill gate precedes chance sampling.'},{'rva':'0xb883c2..0xb883ee','finding':'SyncedRandomGenerator.Range(0,1); success requires castChance strictly greater than sampled value and count != 0.'},{'rva':'0xb883f0..0xb8840d','finding':'Failed chance/count invokes TriggerCooldown only if autoCast AND triggerCooldown are true.'},{'rva':'0xb88446..0xb88497','finding':'When autoCast=true, useType=None calls ProcessSpellsCast; successful dispatch decreases count only if count>0. count=-1 stays unlimited.'},{'rva':'0xb4b716..0xb4b751','finding':'AI UpdateIdle passes triggerCooldown=false, autoCast=true, useType=None to ProcessSpells at this call site.'},{'rva':'0xb4b802..0xb4b8ba','finding':'After false ProcessSpells result, conditional path checks canAttack, not casting, not stunned and CanShoot (when ranged), then virtual StartAttackAnimation.'}],'unresolved':['Other UpdateIdle/UpdateChasing branches may call ProcessSpells under different flags; complete per-tick evaluation frequency not established.','Cooldown initialization/decrement and exact base animation variation remain to trace. CanCastSkill cooldown/range gates are in DraugNativeSkillEligibilityVerification.json; attack gate/reset evidence is in DraugNativeAttackTimerVerification.json; full callback chain remains unverified.'],'reference':'https://github.com/SamboyCoding/Cpp2IL/blob/development/LibCpp2IL/MetadataUsage.cs'}
pathlib.Path('Docs/Validation/DraugNativeSkillDispatchAnalysis.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
print('SKILL_DISPATCH_SLOT_CHECK_PASS',len(virtuals),'SIGNATURES',len(signatures))
print([(v['classOffset'],v['method']) for v in virtuals])
