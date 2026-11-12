"""Join source Animator behaviour bindings to checked native callback evidence."""
import contextlib, hashlib, io, json, pathlib, runpy, struct, sys
with contextlib.redirect_stdout(io.StringIO()):
    n = runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0, str(pathlib.Path('Logs/source-parser-packages').resolve()))
import UnityPy
graph = json.loads(pathlib.Path('Docs/Validation/DraugAnimatorGraphEvidence.json').read_text())
shared = UnityPy.load(graph['sharedBundle'])
objects = {o.path_id: o for o in shared.objects}
controller = objects[graph['controllerPathId']].read_typetree()
names = dict(controller['m_TOS'])
folder = pathlib.Path(graph['sharedBundle']).parent
script_path = next(folder.glob('*monoscripts*.bundle'))
scripts = {o.path_id: o for o in UnityPy.load(str(script_path)).objects}
description = controller['m_StateMachineBehaviourVectorDescription']
bindings = []
for key, span in description['m_StateMachineBehaviourRanges']:
    state = names.get(key['m_StateID'], str(key['m_StateID']))
    if state not in ['base.attack.attack_01', 'base.attack.attack_02', 'base.attack.attack_03', 'base.cast_tree']:
        continue
    for index in range(span['m_StartIndex'], span['m_StartIndex'] + span['m_Count']):
        behaviour_index = description['m_StateMachineBehaviourIndices'][index]
        reference = controller['m_StateMachineBehaviours'][behaviour_index]
        assert reference['m_FileID'] == 0
        behaviour = objects[reference['m_PathID']].read_typetree()
        script = scripts[behaviour['m_Script']['m_PathID']].read_typetree()
        bindings.append({'state': state, 'behaviourPathId': reference['m_PathID'], 'scriptPathId': behaviour['m_Script']['m_PathID'],
                         'class': script['m_ClassName'], 'assembly': script['m_AssemblyName']})
assert len(bindings) == 4
assert all(x['class'] == 'ai_attack' for x in bindings if '.attack.' in x['state'])
fields = json.loads(pathlib.Path('Docs/Validation/SourceCombatFieldOffsets.json').read_text())
assert fields['.AnimatorIds']['attack_index'] == 0x28
assert fields['GamesFarm.RPGActor']['lastAttackAnimation'] == 0x798
virtuals = json.loads(pathlib.Path('Docs/Validation/DraugNativeSkillDispatchAnalysis.json').read_text())['virtualCalls']
slots = {x['classOffset']: x['method'] for x in virtuals}
assert [slots[x] for x in ['0x748', '0x738', '0x1098']] == ['GetAnimatorInteger', 'SetAnimatorInteger', 'OnAttackStart']
binary, offset, base = n['binary'], n['offset'], n['n']['base']
usage = struct.unpack_from('<Q', binary, offset(base + 0x628a2b8))[0]
assert usage >> 29 == 1
type_info = n['n']['resolve']((usage & 0x1fffffff) >> 1)
assert type_info['name'] == 'AnimatorIds' and type_info['data'] == 180
start = offset(base + 0x5064ce0)
icall = binary[start:binary.index(b'\0', start)].decode()
assert icall == 'UnityEngine.Random::RandomRangeInt(System.Int32,System.Int32)'
listing = pathlib.Path('Logs/ai_attack-OnStateEnter-0xa00410.asm.txt').read_text()
snippets = [line for line in listing.splitlines() if 0xa004f2 <= int(line.split()[0], 16) <= 0xa00619]
proof = {'binarySHA256': n['n']['result']['sha256'], 'sharedBundleSHA256': graph['sharedSHA256'],
         'scriptBundle': str(script_path), 'scriptBundleSHA256': hashlib.sha256(script_path.read_bytes()).hexdigest(),
         'bindings': bindings, 'animatorIdsTypeUsage': hex(usage), 'resolvedType': type_info,
         'attackIndexStaticOffset': 40, 'randomInternalCall': icall, 'nativeInstructions': snippets,
         'findings': ['ai_attack.OnStateEnter reads AnimatorIds.attack_index through GetAnimatorInteger.',
                      'Input indices 0,1,2 map to lastAttackAnimation values 1,2,4; other indices map to 0.',
                      'Then invokes virtual OnAttackStart, followed by RandomRangeInt with arguments 0 and 3.',
                      'Writes the returned integer to AnimatorIds.attack_index through SetAnimatorInteger after the current state has entered.'],
         'limits': ['Static control-flow evidence only; callbacks and RNG were not executed.',
                    'Distribution, initial index assignment, actual state-machine entry route and full callback lifecycle still require verification.',
                    'Do not reinterpret the new index as selecting the already-entered attack state.']}
pathlib.Path('Docs/Validation/DraugAttackCallbackEvidence.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print('DRAUG_ATTACK_CALLBACK_BINDING_PASS', len(bindings), 'VIRTUAL_SLOTS_PASS 3', icall)
