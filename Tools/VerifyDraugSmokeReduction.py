"""Evaluate actual source surface instructions against the translated surface formula (CPU only)."""
import hashlib,json,pathlib,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Logs/source-parser-packages'))
import numpy as np
source=ROOT/'Logs/draug-flask-erosion-5.dxbc.txt'
text=source.read_text(encoding='utf-8-sig')
start=text.index('mul r0.x, cb0[80].x')
end=text.index('div r1.xyz, v5.xyzx',start)
lines=text[start:end].strip().splitlines()
def texture(slot,uv):
    if slot!=3:return np.ones(4,dtype=np.float32)
    x,y=uv[:2]
    # Smooth, distinct channels exercise sampled coordinates and channel selection.
    offsets=np.array([.17,.39,.63,.91],dtype=np.float32)
    return np.asarray((np.sin(x*(slot+1)+y*(slot+2)+offsets*5)+1)*.5,dtype=np.float32)
def check(v2,v3,v4,v5):
    regs={'v2':v2,'v3':v3,'v4':v4,'v5':v5,'cb0[80]':np.array([3.21,0,0,0],dtype=np.float32)}
    evidence=json.loads((ROOT/'Docs/Validation/DraugSmokeSourceEvidence.json').read_text(encoding='utf-8-sig'))
    floats=dict(evidence['material']['m_Floats']);colors=dict(evidence['material']['m_Colors'])
    for binding in evidence['constantBindings']:
        if binding['buffer']!='UnityPerMaterial':continue
        name=binding['name'];offset=binding['offset'];size=binding['dimensions']
        values=[floats[name]] if size==1 else [colors[name][k] for k in 'rgba'[:size]]
        for i,value in enumerate(values):
            index=offset//4+i
            regs.setdefault('cb1['+str(index//4)+']',np.zeros(4,dtype=np.float32))[index%4]=value
    def read(arg):
        arg=arg.strip()
        if arg.startswith('l('):return np.array([float(x) for x in arg[2:-1].split(',')],dtype=np.float32).repeat(4)[:4] if ',' not in arg else np.array([float(x) for x in arg[2:-1].split(',')],dtype=np.float32)
        if arg=='cb0[86].y':return np.zeros(4,dtype=np.float32)
        negative=arg.startswith('-');arg=arg.lstrip('-')
        name,swizzle=arg.split('.') if '.' in arg else (arg,'xyzw')
        swizzle=swizzle.ljust(4,swizzle[-1]);value=regs.get(name,np.zeros(4,dtype=np.float32))[["xyzw".index(c) for c in swizzle]]
        return -value if negative else value
    for line in lines:
        op,args=line.split(' ',1);args=re.split(r',\s*(?![^()]*\))',args.strip())
        dest=args[0];parts=args[1:]
        if op.startswith('sample_'):
            uv=read(parts[0]);slot,swizzle=parts[1].split('.');value=texture(int(slot[1:]),uv)[['xyzw'.index(c) for c in swizzle]]
        else:
            values=[read(x) for x in parts];base=op.removesuffix('_sat')
            if base=='mov':value=values[0]
            elif base=='frc':value=values[0]-np.floor(values[0])
            elif base=='add':value=values[0]+values[1]
            elif base=='mul':value=values[0]*values[1]
            elif base=='mad':value=values[0]*values[1]+values[2]
            elif base=='max':value=np.maximum(values[0],values[1])
            elif base=='div':value=values[0]/values[1]
            elif base=='log':value=np.log2(values[0])
            elif base=='exp':value=np.exp2(values[0])
            else:raise AssertionError(op)
            if op.endswith('_sat'):value=np.clip(value,0,1)
        name,mask=dest.split('.');target=regs.setdefault(name,np.zeros(4,dtype=np.float32))
        for axis in mask:target['xyzw'.index(axis)]=value['xyzw'.index(axis)]
    density=texture(3,v3[:2])[0]
    rgb=density*v2[:3]
    return max(float(np.max(np.abs(rgb-regs['r0'][1:4]))),abs(float(density)-float(regs['r0'][0])))
rng=np.random.default_rng(20261112)
errors=[check(*[rng.uniform(-1,3,4).astype(np.float32) for _ in range(4)]) for _ in range(2048)]
maximum=max(errors);assert maximum<.0001,maximum
result={'samples':len(errors),'maxAbsoluteError':maximum,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'instructionCount':len(lines),'limits':'CPU reduction against actual source surface instructions for the asserted current material, four white auxiliary textures and synthetic red mask. Does not test GPU, depth, alpha/fog, or real texture import.'}
(ROOT/'Docs/Validation/DraugSmokeReductionVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_SMOKE_REDUCTION_PASS',result)
