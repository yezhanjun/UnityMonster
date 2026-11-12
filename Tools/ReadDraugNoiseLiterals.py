"""Read exact immediate literals hidden by decimal DXBC disassembly formatting."""
import hashlib,json,struct
from pathlib import Path
path=Path('Logs/draug-death-shuriken-13.dxbc');data=path.read_bytes()
assert data[:4]==b'DXBC'
chunks=struct.unpack_from('<I',data,28)[0];records=[]
for i in range(chunks):
    p=struct.unpack_from('<I',data,32+i*4)[0]
    if data[p:p+4] not in [b'SHDR',b'SHEX']:continue
    size=struct.unpack_from('<I',data,p+4)[0]
    words=struct.unpack_from('<'+'I'*(size//4),data,p+8)
    j=2
    while j<len(words):
        token=words[j];length=(token>>24)&127
        assert length>0 and j+length<=len(words)
        instruction=words[j:j+length]
        # Check only the six MAD instructions from the gradient hash normalization,
        # with known immediate32 scalar/vector operand tokens (0x4001/0x4002).
        if j in [216,236,355,375,521,541]:
            assert token&2047==50
            literals=[]
            for k,w in enumerate(instruction):
                if w not in [0x4001,0x4002]:continue
                count=1 if w==0x4001 else 4
                values=instruction[k+1:k+1+count]
                if 0x33800001 in values:literals.extend(v for v in values if v==0x33800001)
            assert literals
            records.append(dict(wordOffset=j,opcode='mad',normalizerBits=hex(literals[0]),occurrences=len(literals),instruction=[hex(x) for x in instruction]))
        j+=length
assert len(records)==6 and sum(r['occurrences'] for r in records)==12
value=struct.unpack('<f',struct.pack('<I',0x33800001))[0]
assert value==struct.unpack('<f',struct.pack('<f',1/16777215))[0]
out=dict(status='EXACT_SOURCE_NOISE_NORMALIZER_VERIFIED',normalizerBits='0x33800001',normalizerFloat=value,
         sourceSHA256=hashlib.sha256(data).hexdigest(),records=records,
         limits='Targeted bytecode literal extraction; not full instruction emulation or shader GPU parity.')
Path('Docs/Validation/DraugNoiseLiteralEvidence.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print('DRAUG_NOISE_LITERALS_PASS sixMADs twelveImmediates normalizer=0x33800001')
