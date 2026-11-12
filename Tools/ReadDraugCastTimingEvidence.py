"""Join checked native method/slot evidence with source animation cast events."""
import json, pathlib
folder = pathlib.Path('Docs/Validation')
methods = json.loads((folder/'SourceCombatMethodMap.json').read_text())
slots = json.loads((folder/'DraugNativeSkillDispatchAnalysis.json').read_text())
animations = json.loads((folder/'DraugAttackDispatchEvidence.json').read_text())
time_method = next(m for t in methods['types'] if t['name']=='Time' and t['namespace']=='UnityEngine' for m in t['methods'] if m['name']=='get_deltaTime')
assert time_method['RVA'] == 0x9587d0
by_slot = {v['classOffset']: v['method'] for v in slots['virtualCalls']}
assert by_slot['0xef8']=='DoCast' and by_slot['0x1038']=='TriggerCooldown'
clip = next(x for x in animations['clips'] if x['name']=='skill_throw')
events = {e['functionName']:e['time'] for e in clip['events']}
assert 0.49 < events['OnCastEvent'] < 0.50 and 1.10 < events['OnCastEndEvent'] < 1.12
def excerpt(path, begin, end):
    lines = pathlib.Path(path).read_text().splitlines()
    selected = [line for line in lines if begin <= int(line.split()[0],16) <= end]
    assert selected and int(selected[0].split()[0],16)==begin
    return selected
proof = {'binarySHA256': methods['binarySHA256'], 'sourceBundleSHA256': animations['sha256'],
    'timeMethod': time_method, 'skillThrowEvents': events,
    'chain': [
        {'stage':'OnCastEvent', 'instructions':excerpt('Logs/PawnController-OnCastEvent-0xba0910.asm.txt',0xba0a8a,0xba0ab4),
         'finding':'If ProcessAnimationEvent succeeds, invokes virtual DoCast at slot0xef8.'},
        {'stage':'AIController.DoCast', 'instructions':excerpt('Logs/AIController-DoCast-0xb55a80.asm.txt',0xb55ae1,0xb55ae6),
         'finding':'Calls PawnController.DoCast.'},
        {'stage':'PawnController.DoCast', 'instructions':excerpt('Logs/PawnController-DoCast-0xb98890.asm.txt',0xb9972a,0xb9975d),
         'finding':'At this reached cast path, ApplyModifiers precedes virtual TriggerCooldown with original skill and instance.'},
        {'stage':'AIController._DoCastEnd', 'instructions':excerpt('Logs/AIController-_DoCastEnd-0xb37a70.asm.txt',0xb37c2d,0xb37c45),
         'finding':'Separate cast-end path resets attack timer when skillsSyncedWithAttacks is true.'}],
    'limits':['Static reached-path analysis, not full successful-cast execution. DoCast has earlier gates and branches.',
              'Source animation event times are clip-local; speed and state transitions affect wall-clock timing.',
              'Skill cooldown trigger and attack timer reset are distinct events; do not combine into a single release timer.']}
(folder/'DraugCastTimingEvidence.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print('DRAUG_CAST_TIMING_EVIDENCE_PASS',len(proof['chain']),'linked stages')
