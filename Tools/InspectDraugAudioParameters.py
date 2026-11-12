"""Inspect one Draug FMOD source event without playback. Run from UnityMonster root.
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
 call('FMOD_System_SetOutput',[P,I],core,4)
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
 class Description(c.Structure):
  _fields_=[('name',S),('id',U*2),('minimum',c.c_float),('maximum',c.c_float),('default',c.c_float),('type',I),('flags',U),('guid',U*4)]
 descriptions=[]
 for index in range(params.value):
  descriptor=Description()
  call('FMOD_Studio_EventDescription_GetParameterDescriptionByIndex',[P,I,c.POINTER(Description)],event,index,c.byref(descriptor))
  descriptions.append({'name':descriptor.name.decode(),'minimum':descriptor.minimum,'maximum':descriptor.maximum,'default':getattr(descriptor,'default'),'type':descriptor.type,'flags':descriptor.flags})
 minimum=c.c_float();maximum=c.c_float()
 call('FMOD_Studio_EventDescription_GetMinMaxDistance',[P,c.POINTER(c.c_float),c.POINTER(c.c_float)],event,c.byref(minimum),c.byref(maximum))
 result['parameters']=descriptions;result['minDistance']=minimum.value;result['maxDistance']=maximum.value
 pathlib.Path(f'Docs/Validation/Draug{event_name}AudioParameters.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(result))
finally:call('FMOD_Studio_System_Release',[P],s)
