"""Execute source FX.Spawn LOD selection and wreck-percentage blocks only."""
import contextlib,hashlib,io,json,runpy,struct,sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    n=runpy.run_path('Tools/ReadSourceMethodMap.py')
sys.path.insert(0,str(Path('Logs/source-parser-packages').resolve()))
import UnityPy,unicorn
from unicorn.x86_const import *
config=json.loads(Path('Docs/Validation/DraugDeathAssetEvidence.json').read_text(encoding='utf-8-sig'))
source=Path(config['sourceBundle'])
assert hashlib.sha256(source.read_bytes()).hexdigest()==config['sourceSHA256']
objects={o.path_id:o for o in UnityPy.load(str(source)).objects}
fx=objects[-4030375497284763322].read_typetree()
rows=[fx['_lod'+str(i)] for i in range(3)]
assert [r['count'] for r in rows]==[0,25,60]
assert [r['wreckSpawnPercent'] for r in rows]==[100,50,0]
base=n['n']['base'];uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64)
uc.mem_map(base,(n['n']['u32'](n['n']['optional']+56)+4095)&~4095)
for start,length,raw in n['n']['sections']:uc.mem_write(base+start,n['binary'][raw:raw+length])
arena=0x1000000;uc.mem_map(arena,0x10000)
owner,array=arena+0x1000,arena+0x2000
endpoints={base+0xbd0770,base+0xbd1191,base+0xbd0bcc}
def stop(emu,address,size,user):
    if address in endpoints:emu.emu_stop()
uc.hook_add(unicorn.UC_HOOK_CODE,stop)
cases=[]
for ordered in [rows,[rows[2],rows[0],rows[1]]]:
    uc.mem_write(owner+0x240,struct.pack('<Q',array))
    uc.mem_write(array+0x18,struct.pack('<i',len(ordered)))
    for i,row in enumerate(ordered):
        ptr=arena+0x3000+i*0x100
        uc.mem_write(array+0x20+8*i,struct.pack('<Q',ptr))
        uc.mem_write(ptr+0x18,struct.pack('<ii',row['wreckSpawnPercent'],row['count']))
    for count in range(-1,103):
        for register,value in [(UC_X86_REG_RDI,owner),(UC_X86_REG_R13,0),(UC_X86_REG_R10,count&0xffffffff)]:uc.reg_write(register,value)
        uc.emu_start(base+0xbd06eb,base+0xbd123c,count=1000)
        early=uc.reg_read(UC_X86_REG_RIP)==base+0xbd1191
        assert early==(count>100)
        selected=uc.reg_read(UC_X86_REG_R15)&0xffffffff
        if selected==0xffffffff:selected=-1
        expected=max((i for i,r in enumerate(ordered) if r['count']<=count),key=lambda i:ordered[i]['count'],default=-1)
        if early:
            cases.append({'thresholdOrder':[r['count'] for r in ordered],'count':count,'earlyReturn':True});continue
        assert selected==expected,(count,selected,expected)
        uc.emu_start(base+0xbd0b89,base+0xbd0bcc,count=50)
        assert uc.reg_read(UC_X86_REG_RIP)==base+0xbd0bcc
        percent=struct.unpack('<f',struct.pack('<I',uc.reg_read(UC_X86_REG_XMM3)&0xffffffff))[0]
        assert percent==(ordered[selected]['wreckSpawnPercent'] if selected>=0 else 100)
        cases.append({'thresholdOrder':[r['count'] for r in ordered],'count':count,'selected':selected,'wreckPercent':percent,'earlyReturn':False})
paths=['Tools/VerifyDraugDeathLod.py','Logs/FX-Spawn-0xbcf940.asm.txt','Docs/Validation/SourceCombatFieldOffsets.json']
result={'status':'PASS_SELECTED_NATIVE_LOD_AND_WRECK_PERCENT_BLOCKS','cases':len(cases),'sourceSHA256':config['sourceSHA256'],
 'binarySHA256':n['n']['result']['sha256'],'lodGroup':fx['_lodGroup'],'sourceLods':rows,'results':cases,
 'limits':['Synthetic group-count input after source dictionary lookup; full group registration/timing not emulated.',
 'Particle stop/play and WreckSpawner calls not executed. More than100 exits only with non-null LOD array in this selected block.',
 'Source WreckSpawner discovery/activation, audio, full lifecycle and production implementation remain pending.'],
 'evidence':[{'path':p,'sha256':hashlib.sha256(Path(p).read_bytes()).hexdigest()} for p in paths]}
Path('Docs/Validation/DraugDeathLodVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'status':result['status'],'cases':len(cases),'thresholds':[r['count'] for r in rows]}))
