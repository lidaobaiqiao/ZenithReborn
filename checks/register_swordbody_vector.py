"""Register an exported native GPU vector mask without repainting its Alpha."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('render',type=Path)
p.add_argument('destination',type=Path)
a=p.parse_args()
report=json.loads((a.render/'gpu.json').read_text(encoding='utf-8'))
assert report['passed'] and report['actualGpu'] and not report['artApproved']
recipe=json.loads((a.destination/'authoring.json').read_text(encoding='utf-8'))
assert report['revision']==recipe['revision']
raw=(a.render/'active.png').read_bytes()
assert hashlib.sha256(raw).hexdigest()==report['maskSha256']
(a.destination/'active.png').write_bytes(raw)
atlas=Image.open(a.destination/'active.png').convert('RGBA')
alpha=atlas.getchannel('A')
Image.merge('RGB',(alpha,alpha,alpha)).save(a.destination/'contact-sheet.png')
frames=[alpha.crop((i%4*448,i//4*256,(i%4+1)*448,(i//4+1)*256)) for i in range(16)]
def values(frame):
    return list(frame.get_flattened_data())
changes=[]
for i,frame in enumerate(frames):
    x,y=values(frame),values(frames[(i+1)%16])
    changes.append(sum(abs(u-v) for u,v in zip(x,y))/len(x))
wave=recipe['wave']
def crest(i,phase):
    q=(i/16+phase)%1
    return round(wave['startX']+(wave['endX']-wave['startX'])*q,3)
clip={
    'revision':recipe['revision'],'status':'review-candidate','grid':[4,4],
    'frameSize':[448,256],'canvas':recipe['canvas'],'pivot':recipe['pivot'],'tip':recipe['tip'],
    'frames':16,'fps':20,'loop':True,'interpolation':'alpha-linear','channel':'alpha',
    'encoding':'RGBA premultiplied white, use Alpha only',
    'atlasSha256':report['maskSha256'],'authoring':'authoring.json',
    'sourceMethod':'Native SpriteBatch vector recipe; GPU Alpha output packed unchanged',
    'sourceCode':'checks/ZenithPrototypeCheck/SwordBodyVectorMask.cs',
    'sourceCodeSha256':hashlib.sha256(Path('checks/ZenithPrototypeCheck/SwordBodyVectorMask.cs').read_bytes()).hexdigest(),
    'crestPositions': [{'frame':i,'upper':crest(i,wave['topPhase']),'lower':crest(i,wave['bottomPhase'])} for i in range(16)],
    'adjacentAlphaMeanChanges':changes,
    'phases':{
        'appear':{'seconds':.3,'activeMaskContinues':True,'envelope':'tip-to-tail reveal'},
        'active':{'seconds':1.6,'activeMaskContinues':True},
        'finish':{'seconds':.5,'activeMaskContinues':True,'envelope':'tail-to-tip retreat and fade'}
    },
    'contextBlend':report['contextBlend'],'colorField':recipe['colorField'],
    'runtimeIntegrated':False,'finalArtApproved':False
}
(a.destination/'clip.json').write_text(json.dumps(clip,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'revision':clip['revision'],'frames':16,'hash':clip['atlasSha256'],'artApproved':False}))
