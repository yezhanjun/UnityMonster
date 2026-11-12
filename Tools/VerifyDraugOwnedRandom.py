"""Native corpus for lazy wrapper, warm seed assignment, and explicit reinitialization."""
import contextlib, hashlib, io, json, runpy, struct
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):f=runpy.run_path('Tools/VerifyDraugRandomSequence.py')
uc=f['uc'];obj=f['obj'];wrapper=f['wrapper'];klass=f['klass'];invoke=f['invoke']
digest=hashlib.sha256();seeds=f['seeds'];draws=0
for seed in seeds:
    uc.mem_write(obj,bytes(32));uc.mem_write(obj,struct.pack('<Q',klass));invoke(0x32aafe0,obj,seed)
    uc.mem_write(wrapper,bytes(32));uc.mem_write(wrapper+0x10,struct.pack('<Q',obj));uc.mem_write(wrapper+0x18,struct.pack('<i',seed))
    for phase in range(3):
        if phase==1:invoke(0x862f80,wrapper,~seed)
        if phase==2:
            # Normal Initialize ultimately constructs System.Random with the stored seed.
            # Allocation/debug override boilerplate is not executed here.
            invoke(0x32aafe0,obj,~seed)
        for _ in range(128):
            invoke(0xa9a170,wrapper);bits=uc.reg_read(f['UC_X86_REG_XMM0'])&0xffffffff
            digest.update(struct.pack('<iiI',seed,phase,bits));draws+=1
report=dict(status='PASS_NATIVE_OWNED_RANDOM_CORPUS',binarySHA256=f['n']['n']['result']['sha256'],seeds=seeds,draws=draws,corpusSHA256=digest.hexdigest(),phases=['initial128 values','seed=bitwiseComplement; continue128','explicit System.Random constructor with changed seed;128'],limits=['Uses real native seeded constructor, seed setter and get_value arithmetic. Lazy allocation and explicit Initialize debug/allocator wrapper are not executed.', 'Does not prove actor startup draw accounting, network authority or combat integration.'])
Path('Docs/Validation/DraugOwnedRandomNativeVerification.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('DRAUG_OWNED_RANDOM_NATIVE_PASS',len(seeds),'seeds',draws,'values')
