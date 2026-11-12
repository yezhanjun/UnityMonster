"""Read actual Draug override and its referenced shared AnimatorController graph."""
import hashlib, json, pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Logs/source-parser-packages'))
import UnityPy
folder = pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64')
source = next(folder.glob('enemies_assets_draug_thrower_sur_*.bundle'))
main = UnityPy.load(str(source))
objects = {o.path_id: o for o in main.objects}
animator = objects[-3003114223224357103].read_typetree()
override = objects[animator['m_Controller']['m_PathID']].read_typetree()
reference = override['m_Controller']
external = objects[animator['m_Controller']['m_PathID']].assets_file.externals[reference['m_FileID'] - 1].path
cab = external.rsplit('/', 1)[-1]
matches = []
for path in folder.glob('duplicateassetisolation*.bundle'):
    env = UnityPy.load(str(path))
    for obj in env.objects:
        if obj.path_id == reference['m_PathID'] and obj.assets_file.name == cab:
            matches.append((path, env, obj))
assert len(matches) == 1, (cab, len(matches))
shared_path, shared, controller_obj = matches[0]
controller = controller_obj.read_typetree()
shared_objects = {o.path_id: o for o in shared.objects if o.assets_file.name == cab}
names = dict(controller['m_TOS'])
overrides = {x['m_OriginalClip']['m_PathID']: x['m_OverrideClip'] for x in override['m_Clips']}

def clip(index):
    ref = controller['m_AnimationClips'][index]
    assert ref['m_FileID'] == 0
    original = shared_objects[ref['m_PathID']].read_typetree()
    replacement = overrides.get(ref['m_PathID'])
    actual = original
    if replacement and replacement['m_PathID']:
        assert replacement['m_FileID'] == 0
        actual = objects[replacement['m_PathID']].read_typetree()
    return {'index': index, 'original': ref, 'originalName': original['m_Name'], 'override': replacement,
            'effectiveName': actual['m_Name'], 'events': actual['m_Events']}

layer = controller['m_Controller']['m_StateMachineArray'][0]['data']
states = [x['data'] for x in layer['m_StateConstantArray']]
def conditions(items):
    return [dict(x['data'], parameter=names.get(x['data']['m_EventID'])) for x in items]
selected = []
for index, state in enumerate(states):
    full_name = names.get(state['m_FullPathID'], '')
    if full_name not in ['base.idle', 'base.chase', 'base.cast_tree'] and not full_name.startswith('base.attack.'):
        continue
    transitions = []
    for item in state['m_TransitionConstantArray']:
        transition = dict(item['data'])
        transition['conditions'] = conditions(transition.pop('m_ConditionConstantArray'))
        destination = transition['m_DestinationState']
        transition['destinationName'] = names.get(states[destination]['m_FullPathID']) if destination < len(states) else None
        transitions.append(transition)
    nodes = []
    for tree in state['m_BlendTreeConstantArray']:
        for node in tree['data']['m_NodeArray']:
            data = node['data']
            record = {'clipIndex': data['m_ClipID'], 'blendParameter': names.get(data['m_BlendEventID']),
                      'children': data['m_ChildIndices'], 'thresholds': data['m_Blend1dData']['data']['m_ChildThresholdArray']}
            if data['m_ClipID'] != 0xffffffff:
                record['clip'] = clip(data['m_ClipID'])
            nodes.append(record)
    selected.append({'index': index, 'name': full_name, 'speed': state['m_Speed'],
                     'speedParameter': names.get(state['m_SpeedParamID']), 'transitions': transitions, 'nodes': nodes})
any_state = []
for item in layer['m_AnyStateTransitionConstantArray']:
    transition = dict(item['data'])
    transition['conditions'] = conditions(transition.pop('m_ConditionConstantArray'))
    destination = transition['m_DestinationState']
    transition['destinationName'] = names.get(states[destination]['m_FullPathID']) if destination < len(states) else None
    any_state.append(transition)
selectors = []
for item in layer['m_SelectorStateConstantArray']:
    selector = item['data']
    if not any(c['data']['m_EventID'] == 1581244933 for t in selector['m_TransitionConstantArray'] for c in t['data']['m_ConditionConstantArray']):
        continue
    selectors.append({'raw': selector, 'transitions': [
        {'destinationIndexRaw': t['data']['m_Destination'], 'conditions': conditions(t['data']['m_ConditionConstantArray'])}
        for t in selector['m_TransitionConstantArray']]})
attack_clips = [x['nodes'][0]['clip']['effectiveName'] for x in selected if x['name'] in ['base.attack.attack_01', 'base.attack.attack_02', 'base.attack.attack_03']]
assert attack_clips == ['attack_01', 'attack_02', 'attack_03'], attack_clips
assert len(selectors) == 1
parameter = next(x for x in controller['m_Controller']['m_Values']['data']['m_ValueArray'] if x['m_ID'] == 1581244933)
assert parameter['m_Type'] == 3
attack_default = controller['m_Controller']['m_DefaultValues']['data']['m_IntValues'][parameter['m_Index']]
assert attack_default == 0
proof = {'attackIndexSerializedDefault': attack_default, 'source': str(source), 'sourceSHA256': hashlib.sha256(source.read_bytes()).hexdigest(),
         'sharedBundle': str(shared_path), 'sharedSHA256': hashlib.sha256(shared_path.read_bytes()).hexdigest(),
         'externalCAB': external, 'controllerPathId': controller_obj.path_id, 'controllerName': controller['m_Name'],
         'states': selected, 'anyStateTransitions': any_state, 'attackSelectors': selectors,
         'limits': ['Serialized graph and override mapping only; no Animator evaluation performed.',
                    'Entry selector and decide state have differing raw destination mappings. Do not flatten to a single index map until entry semantics and actual entry route are verified.',
                    'Native attack_index assignment/distribution, animation speed and callback lifecycle are not established by this graph.']}
output = ROOT / 'Docs/Validation/DraugAnimatorGraphEvidence.json'
output.write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding='utf-8')
print('DRAUG_ANIMATOR_GRAPH_PASS', len(selected), 'states', len(selectors), 'attack selector', attack_clips)
