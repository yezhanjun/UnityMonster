"""Render one Draug FMOD source event offline. Run from UnityMonster root.
Requires Windows, Python 3, and the installed source game's FMOD 2.02.07 DLL/banks.
Writes only Logs/ and the Draug audio resources; never modifies source game files.
API signatures checked against https://github.com/fmod/fmod-for-unity/tree/2.02
This is one sampled event outcome, not an FMOD runtime integration.
"""
import ctypes as c,pathlib,json,argparse
parser=argparse.ArgumentParser()
parser.add_argument('event',choices=['Flight','Impact','ArrowFlight'])
args=parser.parse_args()
event_name=args.event
source_guid={'ArrowFlight':(1536503598,1273046226,110216364,-335371009),'Flight':(815710961,1089414169,1175418756,-116487206),
             'Impact':(-592975813,1326911593,-2035202163,-1097832446)}[event_name]
root=pathlib.Path('E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data')
f=c.CDLL(str(root/'Plugins/x86_64/fmodstudio.dll'))
P=c.c_void_p;I=c.c_int;U=c.c_uint;S=c.c_char_p

def call(name,types,*args):
 fn=getattr(f,name);fn.argtypes=types;fn.restype=I;r=fn(*args)
 if r:raise RuntimeError(name+' FMOD_RESULT='+str(r))
 return r
pathlib.Path('Logs').mkdir(exist_ok=True)
s=P();call('FMOD_Studio_System_Create',[c.POINTER(P),U],c.byref(s),0x20207)
try:
 core=P();call('FMOD_Studio_System_GetCoreSystem',[P,c.POINTER(P)],s,c.byref(core))
 call('FMOD_System_SetOutput',[P,I],core,5)
 call('FMOD_Studio_System_Initialize',[P,I,U,U,P],s,128,4,0,c.cast(c.c_char_p(str(pathlib.Path(f'Logs/draug-source-{event_name}.wav').resolve()).encode()),P))
 for name in ['Master.bank','Master.strings.bank']:
  bank=P();call('FMOD_Studio_System_LoadBankFile',[P,S,U,c.POINTER(P)],s,str(root/'StreamingAssets'/name).encode(),0,c.byref(bank))
 guid=(U*4)(*[value & 0xffffffff for value in source_guid])
 event=P();call('FMOD_Studio_System_GetEventByID',[P,P,c.POINTER(P)],s,c.byref(guid),c.byref(event))
 buf=c.create_string_buffer(1024);n=I();call('FMOD_Studio_EventDescription_GetPath',[P,P,I,c.POINTER(I)],event,buf,1024,c.byref(n))
 length=I();call('FMOD_Studio_EventDescription_GetLength',[P,c.POINTER(I)],event,c.byref(length))
 params=I();call('FMOD_Studio_EventDescription_GetParameterDescriptionCount',[P,c.POINTER(I)],event,c.byref(params))
 oneshot=I();call('FMOD_Studio_EventDescription_IsOneshot',[P,c.POINTER(I)],event,c.byref(oneshot))
 result={'path':buf.value.decode(),'lengthMs':length.value,'parameterCount':params.value,'oneshot':oneshot.value,'version':'2.02.07'}
 call('FMOD_Studio_EventDescription_LoadSampleData',[P],event)
 call('FMOD_Studio_System_FlushSampleLoading',[P],s)
 instance=P();call('FMOD_Studio_EventDescription_CreateInstance',[P,c.POINTER(P)],event,c.byref(instance))
 call('FMOD_Studio_EventInstance_Start',[P],instance)
 for frame in range(600):
  call('FMOD_Studio_System_Update',[P],s)
 call('FMOD_Studio_EventInstance_Stop',[P,I],instance,1)
 call('FMOD_Studio_EventInstance_Release',[P],instance)
 print(json.dumps(result));pathlib.Path(f'Logs/draug-fmod-{event_name}.json').write_text(json.dumps(result,indent=2))
finally:call('FMOD_Studio_System_Release',[P],s)

import wave,array,hashlib
with wave.open(f'Logs/draug-source-{event_name}.wav','rb') as audio:
 channels=audio.getnchannels();rate=audio.getframerate();width=audio.getsampwidth();pcm=audio.readframes(audio.getnframes())
if width!=2:raise RuntimeError('Expected PCM16 render')
samples=array.array('h',pcm)
active=[i//channels for i,value in enumerate(samples) if abs(value)>3]
if not active:raise RuntimeError('Source event rendered silence')
first=max(0,active[0]-int(rate*.01));last=min(len(samples)//channels,active[-1]+int(rate*.04))
output=pathlib.Path(f'Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/OriginalDraug{event_name}.wav')
output.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(output),'wb') as audio:
 audio.setnchannels(channels);audio.setsampwidth(width);audio.setframerate(rate);audio.writeframes(pcm[first*channels*width:last*channels*width])
evidence={'event':result,'sourceGuid':source_guid,'sourceBank':'Master.bank','sourceBankSHA256':hashlib.sha256((root/'StreamingAssets/Master.bank').read_bytes()).hexdigest(),'fmodVersion':'2.02.07','render':'Single offline source event outcome; no parameter variation or full FMOD runtime logic','trimFrames':[first,last],'sampleRate':rate,'channels':channels,'peak':max(abs(x) for x in samples)/32768,'duration':(last-first)/rate,'manualListening':False}
pathlib.Path(f'Docs/Validation/Draug{event_name}AudioSource.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORTED',output,evidence['duration'])
