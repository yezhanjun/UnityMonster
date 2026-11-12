"""Evaluate actual source surface instructions against the translated surface formula (CPU only)."""
import hashlib,json,pathlib,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Logs/source-parser-packages'))
import numpy as np
source=ROOT/'Logs/draug-flask-dissolve-12.dxbc.txt'
text=source.read_text(encoding='utf-8-sig')
start=text.index('mov r2.x, v4.y')
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
            elif base=='div':value=values[0]/values[1]
            else:raise AssertionError(op)
            if op.endswith('_sat'):value=np.clip(value,0,1)
        name,mask=dest.split('.');target=regs.setdefault(name,np.zeros(4,dtype=np.float32))
        for axis in mask:target['xyzw'.index(axis)]=value['xyzw'.index(axis)]
    uv=v2[:2]*np.array([v4[1],1],dtype=np.float32)+np.array([v4[2]-np.floor(v4[2]),0],dtype=np.float32)
    warp=texture(4,uv);uv=uv+(warp[:2]-.5)*v4[0]*warp[2]
    a=texture(3,uv+v4[3]*np.array([.0087,-.005],dtype=np.float32))
    b=texture(3,uv+v4[3]*np.array([0,.01],dtype=np.float32))
    c=texture(3,uv+v4[3]*np.array([-.0087,-.005],dtype=np.float32))
    rgb=np.clip(np.array([a[0],b[0],c[0]])*v5[:3],0,1)*v3[2]
    q=np.clip(v3[1],0,1)
    t=np.clip((np.clip(1-max(a[1],b[1],c[1]),0,1)+v3[0]*(2-q)-1)/(1.01-q),0,1)
    alpha=np.clip(max(a[3],b[3],c[3])-t*t*(3-2*t),0,1)
    return max(float(np.max(np.abs(rgb-regs['r5'][:3]))),abs(float(alpha)-float(regs['r0'][2])))
rng=np.random.default_rng(20261112)
errors=[check(*[rng.uniform(-1,3,4).astype(np.float32) for _ in range(4)]) for _ in range(2048)]
maximum=max(errors);assert maximum<.0001,maximum
result={'samples':len(errors),'maxAbsoluteError':maximum,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'instructionCount':len(lines),'limits':'CPU surface math only; deterministic synthetic texture sampler. Does not test GPU execution, real textures, particle streams, depth, exposure or fog.'}
(ROOT/'Docs/Validation/DraugDissolveMathVerification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_DISSOLVE_MATH_PASS',result)
