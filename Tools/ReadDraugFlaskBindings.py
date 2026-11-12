"""Verify selected transparent parameter records against preserved source blob16."""
import hashlib,json,pathlib,struct
ROOT=pathlib.Path(__file__).resolve().parents[1]
blob=ROOT/'Logs/draug-flask-dissolve-blob-16.bin'
data=blob.read_bytes()
def record(name,count):
    raw=name.encode()
    start=data.index(raw)
    assert name=='UnityPerMaterial' or data.find(raw,start+1)<0
    assert struct.unpack_from('<I',data,start-4)[0]==len(raw)
    end=(start+len(raw)+3)&~3
    return {'name':name,'recordOffset':start-4,'values':list(struct.unpack_from('<'+'i'*count,data,end))}
records=[record('Boolean_2FAC1FE4',6),record('_BlendMode',6),record('Texture2D_F593E37E',4),record('_MainTex_UV',4),record('UnityPerMaterial',3)]
assert records[0]['values']==[0,1,1,0,0,16]
assert records[1]['values']==[0,1,1,0,0,160]
assert records[2]['values']==[0,3,2,4]
assert records[3]['values']==[0,4,3,4]
assert records[4]['values']==[176,2,16] # material size, field count, first name length
# The second occurrence is the resource-binding record, not the declaration.
name=b'UnityPerMaterial'; first=data.index(name); second=data.index(name,first+1)
end=(second+len(name)+3)&~3
binding=list(struct.unpack_from('<iii',data,end));assert binding==[1,1,0]
result={'blob':str(blob.relative_to(ROOT)),'sha256':hashlib.sha256(data).hexdigest(),'records':records,'materialBufferBinding':binding,
 'bindings':{'Boolean_2FAC1FE4':'cb1[1].x','_BlendMode':'cb1[10].x','Texture2D_F593E37E':'t3/s2','_MainTex_UV':'t4/s3'},
 'limits':'Targeted source-record verification, not a general Unity parameter parser. HDRP atmospheric fog and exposure not ported.'}
(ROOT/'Docs/Validation/DraugFlaskBindingEvidence.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DRAUG_FLASK_BINDINGS_PASS',result['bindings'])
