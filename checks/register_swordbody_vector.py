"""登记原生 GPU 导出的蒙版，不重绘其 Alpha。"""
import argparse
import hashlib
import json
from pathlib import Path
try:
    from PIL import Image
except ImportError:
    from png_stdlib import Image

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
# 自动配准的真实几何由 GPU 导出回填，保证登记资源可独立复现。
if recipe.get('autoFit'):
    profile=json.loads((a.render/'profile.json').read_text(encoding='utf-8'))
    recipe['pivot']=profile['pivot']
    recipe['tip']=profile['tip']
    recipe['bladeCanvasPx']=profile['bladeCanvasPx']
    wave=recipe.setdefault('wave',{})
    wave.setdefault('startX',round(recipe['tip'][0]-18,3))
    wave.setdefault('endX',round(recipe['pivot'][0]-257,3))
    (a.destination/'authoring.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
atlas=Image.open(a.destination/'active.png').convert('RGBA')
alpha=atlas.getchannel('A')
Image.merge('RGB',(alpha,alpha,alpha)).save(a.destination/'contact-sheet.png')
frame_w,frame_h=recipe['canvas']
frames=[alpha.crop((i%4*frame_w,i//4*frame_h,(i%4+1)*frame_w,(i//4+1)*frame_h)) for i in range(16)]
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
    'frameSize':recipe['canvas'],'canvas':recipe['canvas'],'pivot':recipe['pivot'],'tip':recipe['tip'],
    'frames':16,'fps':16/recipe['loopSeconds'] if recipe.get('shape')=='airflow' else 20,'loop':True,
    'loopSeconds':recipe['loopSeconds'],'interpolation':'alpha-linear','channel':'alpha',
    'encoding':'RGBA premultiplied white, use Alpha only',
    'atlasSha256':report['maskSha256'],'authoring':'authoring.json',
    'sourceMethod':'Native SpriteBatch vector recipe; GPU Alpha output packed unchanged',
    'sourceCode':'checks/ZenithPrototypeCheck/SwordBodyVectorMask.cs',
    'sourceCodeSha256':hashlib.sha256(Path('checks/ZenithPrototypeCheck/SwordBodyVectorMask.cs').read_bytes()).hexdigest(),
    'sourceDependencies':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [Path('checks/ZenithPrototypeCheck/SwordAirflowMask.cs')]} if recipe.get('shape')=='airflow' else {},
    'crestPositions': [] if recipe.get('shape')=='airflow' else [{'frame':i,'upper':crest(i,wave['topPhase']),'lower':crest(i,wave['bottomPhase'])} for i in range(16)],
    'motionModel':'native silhouette distance field, downstream edge waves, coherent fading wake' if recipe.get('shape')=='airflow' else 'R2 traveling crest',
    'adjacentAlphaMeanChanges':changes,
    'phases':{
        'appear':{'seconds':.3,'activeMaskContinues':True,'envelope':'tip-to-tail reveal'},
        'active':{'seconds':1.6,'activeMaskContinues':True},
        'finish':{'seconds':.5,'activeMaskContinues':True,'envelope':'tail-to-tip retreat and fade'}
    },
    'contextBlend':report['contextBlend'],'colorField':recipe['colorField'],
    'colorFieldVariant':recipe.get('colorFieldVariant','default'),
    'runtimeIntegrated':False,'finalArtApproved':False
}
(a.destination/'clip.json').write_text(json.dumps(clip,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'revision':clip['revision'],'frames':16,'hash':clip['atlasSha256'],'artApproved':False}))
