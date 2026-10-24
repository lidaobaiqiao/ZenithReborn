"""比较结束修订前后的活动期实际GPU图与随包美术，防止扩大改动范围。"""
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image
from tmod_archive import read_archive,contents

root=Path(__file__).resolve().parent.parent
version=next(s.split('=',1)[1].strip() for s in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if s.startswith('version'))
current=root/'build-profile/Mods/ZenithReborn.tmod'
sha=hashlib.sha256(current.read_bytes()).hexdigest()
images=[]
for name in ['fixed-a.png','fixed-b.png']+[f'palette-{i}.png' for i in range(4)]:
    a=np.asarray(Image.open(root/f'artifacts/soul-trail-gpu-0.3.70/{name}')).astype(np.int16)
    b=np.asarray(Image.open(root/f'artifacts/soul-trail-gpu-{version}/{name}')).astype(np.int16)
    images.append(dict(file=name,maxDifference=int(np.abs(a-b).max()),changedPixels=int(np.any(a!=b,axis=2).sum())))
def assets(path):
    return {e['name']:contents(e) for e in read_archive(path)[3] if e['name'].startswith('Assets/Vfx/')}
before=assets(Path.home()/'Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod')
after=assets(current)
report=dict(passed=all(i['changedPixels']==0 for i in images) and before==after,testedPackageSha256=sha,
            images=images,vfxAssetEntries=len(after),artworkBytesUnchanged=before==after)
(root/f'artifacts/ending-preservation-{version}.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report));assert report['passed'],report
