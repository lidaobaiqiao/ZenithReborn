"""核对本轮保留用户选定的剑色输入，恢复玻璃表现且不改变玩法、形态或素材。"""
import hashlib
import json
import zipfile
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parent.parent
version='0.3.83'
out=root/'artifacts'
sha=hashlib.sha256((root/'build-profile/Mods/ZenithReborn.tmod').read_bytes()).hexdigest()
def report(name):
    value=json.loads((out/name).read_text(encoding='utf-8-sig'))
    assert value['passed'] and value['testedPackageSha256']==sha,name
    return value
gpu=report(f'soul-trail-gpu-{version}/gpu.json')
world=report(f'soul-flight-world-{version}/world.json')
native=report(f'soul-flight-world-{version}/native-probe.json')
report(f'vfx-ownership-{version}.json')
assert gpu['glassResponse']['Passed'] and gpu['glassResponse']['MaximumAlphaDifference']==0
assert gpu['swordPaletteVerified'] and gpu['endingFinalPixels']==0 and world['remainingEndings']==0
assert native['outsideChangedPixels']==0
palette=json.loads((out/f'soul-trail-gpu-{version}/sword-palette.json').read_text(encoding='utf-8'))
old_palette=json.loads((out/'soul-trail-gpu-0.3.82/sword-palette.json').read_text(encoding='utf-8'))
assert palette['passed'] and len(palette['items'])==7
assert {x['item']:x['sourceColor'] for x in palette['items']}=={x['item']:x['sourceColor'] for x in old_palette['items']}
images=[np.asarray(Image.open(out/f'soul-trail-gpu-{v}/glass-alpha.png').convert('RGBA')) for v in ('0.3.82',version)]
assert np.array_equal(images[0][:,:,3],images[1][:,:,3])
allowed={'ZenithReborn/build.txt','ZenithReborn/Assets/Effects/ZRSwordFlow.fx',
    'ZenithReborn/Assets/Effects/ZRSwordFlow.fxc','ZenithReborn/Assets/Effects/build-manifest.json'}
with zipfile.ZipFile(out/'ZenithReborn-0.3.82-source.zip') as old:
    files=[n for n in old.namelist() if n.startswith('ZenithReborn/') and not n.endswith('/')]
    for name in files:
        if name not in allowed:assert old.read(name)==(root/name).read_bytes(),name
result=dict(passed=True,testedPackageSha256=sha,sourceColorsIdenticalTo='0.3.82',
    sourceColorItems=[dict(item=x['item'],sourceColor=x['sourceColor']) for x in palette['items']],
    alphaIdenticalTo='0.3.82',preservedProductionFiles=len(files)-len(allowed),changedFiles=sorted(allowed),
    actualGpu=True,actualNativeWorldFixture=True,actualMouseAttack=False,liveMultiplayer=False,artApproved=False,
    scope='保留已选颜色输入与所有形态/素材/战斗文件；GPU和世界结果不能代替用户对玻璃观感的判断')
(out/f'color-glass-restoration-{version}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
