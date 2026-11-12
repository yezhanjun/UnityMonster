"""Read-only targeted native disassembly. Listings are evidence, not recovered C# implementations."""
import pathlib,sys,runpy,contextlib,io,struct,json
with contextlib.redirect_stdout(io.StringIO()):n=runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0,str(pathlib.Path('Logs/source-parser-packages').resolve()))
import capstone
binary=n['binary'];native=n['n'];m=n['m'];h=n['h'];offset=n['offset'];mi=m['integer'];u64=n['u64'];u32=native['u32']
methods=n['selected'];labels={}
for typ in methods:
 for method in typ['methods']:
  if method['VA']:labels.setdefault(method['VA'],[]).append(typ['name']+'.'+method['name'])
# Windows x64 exception directory supplies function boundaries independent of neighboring metadata methods.
p=native['optional']+112+3*8;exceptionRva=u32(p);size=u32(p+4);start=offset(native['base']+exceptionRva)
ranges={u32(q):(u32(q+4),u32(q+8)) for q in range(start,start+size,12)}
fields={};offsetTable=offset(u64(native['reg']+88))
for typ in methods:
 ti=typ['index'];p=h['typeDefinitions'][0]+ti*m['stride'];fp=p+8+3*m['tw']+m['gw']+4;num=mi(fp+32+4,2)
 pointer=u64(offsetTable+ti*8)
 if not pointer:continue
 table=offset(pointer);out={}
 for j in range(num):
  fieldIndex=mi(fp)+j;name=m['string'](mi(h['fields'][0]+fieldIndex*m['fw']));out[name]=struct.unpack_from('<i',binary,table+j*4)[0]
 key=typ['namespace']+'.'+typ['name']
 if key in fields:key+='@'+typ['assembly']+'#'+str(typ['index'])
 fields[key]=out
pathlib.Path('Docs/Validation/SourceCombatFieldOffsets.json').write_text(json.dumps(fields,indent=2),encoding='utf-8')
engine=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64);engine.detail=True;engine.skipdata=True
chosen={'<_RemoveFromSceneDelayed>d__211':['MoveNext'],'FX':['<IsFinished>g__IsFinishedInternal|170_0','RemoveFromSceneDelayed','GFUpdate','ClearInstanceFromLODDict','OnDespawn','OnDestroy','Awake','Spawn','ApplyPerformanceBudget','ApplyQualityPresetFromSettings','InitializedBasedOnVideoSettings'],'Wreck':['RegisterAsActive','UnregisterAsActive','RemoveFromScene','SetBodiesKinematic','SetCollidersEnabled','IsStationary','IsFallenFarUnderSpawn','Activate','ResetWreck','Initialize','SetInitialState','ActivateImpl','GFUpdate'],'WreckSpawner':['get_ActiveWreckCount','Initialize','SpawnWrecks','AlignWrecksToSkeletonSetVelocity'],'VisualEffectStatus':['SpawnVisualEffectsDeath','Hide'],'WaitFor':['Seconds'],'<RemoveFromSceneSafeImpl>d__519':['MoveNext'],'RPGSkill':['Cast'],'ActiveRPGSkill':['SetCastId','Cast'],'NetAIController':['UseSkill','Cast','RpcUseSkill','RpcCast'],'EnemyFight':['SpawnActor'],'SpawnConfig':['ApplyConfig'],'Random':['.ctor','InternalSample','Sample','Next'],'Actor':['SetOverlay','.ctor','GFAwake','GFStart','OnSpawn','OnDespawn'],'RandomGenerator':['.ctor','Range','Value','get_value','Initialize'],'Time':['get_deltaTime'],'Utils':['get_deltaTimeCap','get_unscaledDeltaTimeCap'],'ai_attack':['OnStateEnter','OnStateExit','OnStateUpdate'],'ai_cast':['OnStateEnter','OnStateExit'],'ai_idle_simple':['OnStateEnter'],'AIController':['SpawnCriticalDeathFX','OnDeath','SetDeathState','DeathFade','UpdateInvisibleDeathRemoval','GFStart','GFAwake','SpawnedByEnemyFight','DoAttack','UpdateAttacking','StartAttackAnimation','UpdateChasing','SyncSkillCooldown','GFUpdate','UpdateIdle','ProcessSpells','get_canAttack','ResetAttackTimer','DoCast','_DoCastEnd','OnAttackStart','InitializeAnimatorState'],'PawnController':['get_OnExplosiveDeathFX','get_OnCriticalDeathFX','ApplyFirstDefinedDeathVisualEffects','_SetFirstDefinedVisualEffectDeath','_ProcessCriticalDeathSpawns','<GetDominantDeathVisualEffect>b__420_0','GetDominantDeathVisualEffect','FindVisualEffect','OnDeath','PrepareCastedSkill','GFStart','CanCastSkill','HasAnySkillReadyToCast','UseSkill','CastEnqueued','ProcessSpells','ProcessSpellsCast','GFUpdate','TriggerCooldown','IsOnCooldown','CooldownFor','GetSkillCooldownDeltaTime','UpdateSkillCooldowns','Cast','DoCast','_DoCastEnd','InstantCast','OnCastEvent','OnCastEndEvent'],'BallisticProjectile':['Launch','UpdateMovement']}
reports=[]
for typ in methods:
 if typ['namespace']!='GamesFarm' and typ['name'] not in ['<_RemoveFromSceneDelayed>d__211','FX','Wreck','WreckSpawner','VisualEffectStatus','WaitFor','<RemoveFromSceneSafeImpl>d__519','NetAIController','EnemyFight','SpawnConfig','Random','RandomGenerator','Time','Utils','ai_attack','ai_cast','ai_idle_simple','ai_chase_simple']:continue
 for method in typ['methods']:
  if method['name'] not in chosen.get(typ['name'],[]) or not method['VA']:continue
  if typ['name']=='RandomGenerator' and method['name']=='Range' and method['parameterCount']!=2:continue # Static tail-call wrappers require separate boundary validation.
  if typ['name']=='RandomGenerator' and method['name']=='.ctor' and method['parameterCount']!=0:continue # Shared setter/constructor body needs a cross-module envelope.
  if typ['name']=='Random' and (typ['namespace']!='System' or method['name']=='.ctor' and method['parameterCount']!=1):continue
  if typ['name']=='Random' and method['name']=='Next' and method['parameterCount']!=1:continue
  firstEnd=ranges.get(method['RVA'],(None,None))[0]
  mod=n['modules'][typ['assembly']]
  later=[u64(mod['table']+i*8) for i in range(mod['count']) if u64(mod['table']+i*8)>method['VA']]
  end=min(later)-native['base'];length=end-method['RVA'];assert 0<length<100000
  pieces=[rva for rva in ranges if method['RVA']<=rva<end]
  code=binary[method['raw']:method['raw']+length];lines=[];consumed=0
  for inst in engine.disasm(code,method['VA']):
   notes=[];consumed+=inst.size
   for operand in (inst.operands if inst.id else []):
    if operand.type==capstone.x86.X86_OP_IMM and operand.imm in labels:notes.append('|'.join(labels[operand.imm]))
    if operand.type==capstone.x86.X86_OP_MEM and operand.mem.base==capstone.x86.X86_REG_RIP:
     address=inst.address+inst.size+operand.mem.disp
     try:
      raw=offset(address);notes.append('rip='+hex(address)+' f32='+str(struct.unpack_from('<f',binary,raw)[0]))
     except (ValueError,struct.error):pass
   lines.append(hex(inst.address-native['base'])+' '+inst.mnemonic+' '+inst.op_str+(' ; '+', '.join(notes) if notes else ''))
  if firstEnd is None:
   assert length<=256 and any(' ret ' in line for line in lines) and not any(' call ' in line for line in lines), ('Unverified leaf method boundary',method)
  assert consumed==length,(typ['name'],method['name'],consumed,length)
  dest=pathlib.Path('Logs')/(typ['name'].replace('<','_').replace('>','_')+'-'+method['name'].replace('<','_').replace('>','_').replace('|','_')+'-'+hex(method['RVA'])+'.asm.txt');dest.write_text('\n'.join(lines),encoding='utf-8')
  reports.append({'type':typ['name'],'method':method['name'],'rva':method['RVA'],'bytes':length,'instructions':len(lines),'boundaryKind':'unwind' if firstEnd is not None else 'short call-free leaf ending before next method','firstUnwindEndRVA':firstEnd,'unwindSegmentsInEnvelope':len(pieces),'listing':str(dest)})
pathlib.Path('Docs/Validation/SourceCombatDisassemblyIndex.json').write_text(json.dumps({'binarySHA256':native['result']['sha256'],'capstone':capstone.__version__,'boundary':'PE unwind start or checked short call-free leaf; envelope ends at next greater same-module method pointer and includes split unwind segments. Envelope may include compiler helpers/padding/data.','methods':reports,'limits':'Linear decoding can include embedded jump-table data; skipdata retains undecodable bytes. Control-flow interpretation remains separate, and listings alone are not behavior proof.'},indent=2),encoding='utf-8')
print(json.dumps(reports))
