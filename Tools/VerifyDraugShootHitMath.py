"""Evaluate actual source surface instructions against the translated surface formula (CPU only)."""
import hashlib,json,pathlib,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Logs/source-parser-packages'))
import numpy as np
np.seterr(divide="ignore",invalid="ignore")
source=ROOT/'Logs/draug-flask-shoot-hit-11.dxbc.txt'
text=source.read_text(encoding='utf-8-sig')
start=text.index('sample_b_indexable(texture2d)(float,float,float,float) r0.z, v2.xyxx')
end=text.index('eq r2.x, cb0[60].w',start)
lines=text[start:end].strip().splitlines()
def texture(slot,uv):
    x,y=uv[:2]
    # Smooth, distinct channels exercise sampled coordinates and channel selection.
    offsets=np.array([.17,.39,.63,.91],dtype=np.float32)
    return np.asarray((np.sin(x*(slot+1)+y*(slot+2)+offsets*5)+1)*.5,dtype=np.float32)
def check(v2,v3,v4,v5):
    regs={'v2':v2,'v3':v3,'v4':v4,'v5':v5}
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
        if op.startswith('sample_b_'):
            uv=read(parts[0]);slot,swizzle=parts[1].split('.');value=texture(int(slot[1:]),uv)[['xyzw'.index(c) for c in swizzle]]
        else:
            values=[read(x) for x in parts];base=op.removesuffix('_sat')
            if base=='mov':value=values[0]
            elif base=='frc':value=values[0]-np.floor(values[0])
            elif base=='add':value=values[0]+values[1]
            elif base=='mul':value=values[0]*values[1]
            elif base=='mad':value=values[0]*values[1]+values[2]
            elif base=='max':value=np.maximum(values[0],values[1])
            elif base=='min':value=np.minimum(values[0],values[1])
            elif base=='div':value=values[0]/values[1]
            else:raise AssertionError(op)
            if op.endswith('_sat'):value=np.nan_to_num(np.clip(value,0,1),nan=0.0)
        name,mask=dest.split('.');target=regs.setdefault(name,np.zeros(4,dtype=np.float32))
        for axis in mask:target['xyzw'.index(axis)]=value['xyzw'.index(axis)]
    blue=texture(3,v2[:2])[2]
    uv=v2[:2]+np.array([blue*v2[0]*v3[2],0],dtype=np.float32)
    packed=texture(3,uv)
    rgb=packed[0]*v5[:3]
    q=np.clip(v3[1],0,.5)
    lower=np.float32(q-1); upper=np.float32(1-q)
    span=np.float32(upper-lower)
    threshold=np.float32(v3[0]*span+lower)
    value=np.float32(threshold+np.float32(1-packed[1]))
    numerator=np.float32(value-q)
    inv=np.float32(1)/np.float32(upper-q)
    t=np.nan_to_num(np.clip(numerator*inv,0,1),nan=0.0)
    alpha=np.clip(packed[3]-t*t*(3-2*t),0,1)
    return max(float(np.max(np.abs(rgb-regs['r3'][:3]))),abs(float(alpha)-float(regs['r0'][2])))
rng=np.random.default_rng(20261112)
errors=[]
for _ in range(2048):
    values=[rng.uniform(-1,3,4).astype(np.float32) for _ in range(4)]
    values[1][1]=.5 if _>=1024 else rng.uniform(-1,.49)
    errors.append(check(*values))
maximum=max(errors);assert maximum<.0001,maximum
result={'samples':len(errors),'maxAbsoluteError':maximum,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'instructionCount':len(lines),'limits':'CPU surface math only, including 1024 q=.5 cases with DXBC saturate NaN-to-zero semantics; GPU singular behavior unverified; deterministic synthetic texture sampler. Does not test GPU execution, real textures, particle streams, depth, exposure or fog.'}
(ROOT/'Docs/Validation/DraugShootHitMathVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_SHOOT_HIT_MATH_PASS',result)
