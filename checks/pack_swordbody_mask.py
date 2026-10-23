"""Extract alpha cells and register their tip; never paint or synthesize artwork."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('source', type=Path)
p.add_argument('destination', type=Path)
p.add_argument('--revision', default='R1')
args = p.parse_args()
source = Image.open(args.source).convert('RGBA')
assert source.width % 4 == 0 and source.height % 4 == 0
cw, ch = source.width // 4, source.height // 4
assert (cw, ch) == (384, 256), 'Review any different grid before packing'
out = args.destination
out.mkdir(parents=True, exist_ok=True)
(out/'source.png').write_bytes(args.source.read_bytes())
atlas = Image.new('RGBA', (448*4, 256*4))
contacts = Image.new('RGB', (448*4, 256*4), 'black')
frames, registrations = [], []
for i in range(16):
    alpha = source.getchannel('A').crop((i%4*cw, i//4*ch, (i%4+1)*cw, (i//4+1)*ch))
    solid = alpha.point(lambda x: 255 if x >= 160 else 0)
    box = solid.getbbox()
    assert box, f'No readable core in frame {i}'
    right = box[2]-1
    tip_ys = [y for y in range(ch) if any(solid.getpixel((x,y)) for x in range(max(0,right-2),right+1))]
    tip_y = round(sum(tip_ys)/len(tip_ys))
    dx, dy = 408-right, 128-tip_y
    registered = Image.new('L', (448, 256))
    registered.paste(alpha, (dx,dy))
    frame = Image.new('RGBA', registered.size, (255,255,255,0))
    frame.putalpha(registered)
    atlas.paste(frame, (i%4*448, i//4*256))
    contacts.paste(Image.merge('RGB', (registered,)*3), (i%4*448, i//4*256))
    frames.append(registered)
    registrations.append({'frame':i,'sourceTip':[right,tip_y],'translation':[dx,dy]})
atlas.save(out/'active.png')
contacts.save(out/'contact-sheet.png')
differences = []
coverage = []
for i, frame in enumerate(frames):
    a, b = list(frame.getdata()), list(frames[(i+1)%16].getdata())
    differences.append(sum(abs(x-y) for x,y in zip(a,b))/len(a))
    coverage.append(sum((x>64)!=(y>64) for x,y in zip(a,b)))
manifest = {
    'revision':args.revision,'status':'review-candidate','grid':[4,4],'frameSize':[448,256],
    'canvas':[448,256],'pivot':[305,128],'tip':[408,128],
    'frames':16,'fps':20,'loop':True,'interpolation':'alpha-linear',
    'sourceSha256':hashlib.sha256(args.source.read_bytes()).hexdigest(),
    'atlasSha256':hashlib.sha256((out/'active.png').read_bytes()).hexdigest(),
    'sourceMethod':'built-in image_gen; alpha extraction and integer registration only',
    'registrations':registrations,
    'adjacentAlphaMeanChanges':differences,'adjacentCoverageChanges':coverage,
    'runtimeIntegrated':False,'finalArtApproved':False,
    'phases':{
        'appear':{'seconds':0.3,'activeMaskContinues':True,'envelope':'reveal from blade tip toward tail'},
        'active':{'seconds':1.6,'activeMaskContinues':True},
        'finish':{'seconds':0.5,'activeMaskContinues':True,'envelope':'tail-to-tip soft retreat and fade'}},
}
(out/'clip.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert min(differences)>0.5 and min(coverage)>50
print(json.dumps({'frames':16,'atlas':str(out/'active.png'),'alphaMotionMin':min(differences),
                  'coverageMotionMin':min(coverage),'loopAlphaChange':differences[-1]}))
