"""用归档旧版的同输入GPU像素核对恢复结果，同时保留用户选定的逐剑色输入。"""
import hashlib
import json
import zipfile
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parent.parent
out=root/'artifacts';version='0.3.84'
sha=hashlib.sha256((root/'build-profile/Mods/ZenithReborn.tmod').read_bytes()).hexdigest()
def report(name):
    data=json.loads((out/name).read_text(encoding='utf-8-sig'))
    assert data['passed'] and data['testedPackageSha256']==sha,name
    return data
gpu=report(f'soul-trail-gpu-{version}/gpu.json')
world=report(f'soul-flight-world-{version}/world.json')
native=report(f'soul-flight-world-{version}/native-probe.json')
report(f'vfx-ownership-{version}.json')
assert gpu['swordPaletteVerified'] and gpu['glassResponse']['Passed'] and native['outsideChangedPixels']==0
comparisons={}
for name in ('response-light-left.png','response-light-right.png','response-environment-rotated.png','response-rough.png'):
    before=np.asarray(Image.open(out/'soul-trail-gpu-0.3.81'/name).convert('RGBA'),dtype=int)
    after=np.asarray(Image.open(out/f'soul-trail-gpu-{version}'/name).convert('RGBA'),dtype=int)
    difference=np.abs(before-after)
    comparisons[name]=dict(changedPixels=int(np.count_nonzero(np.any(difference,axis=2))),maximumChannelDifference=int(difference.max()))
    assert np.array_equal(before,after),(name,comparisons[name])
with zipfile.ZipFile(out/'ZenithReborn-0.3.81-source.zip') as old:
    shader=old.read('ZenithReborn/Assets/Effects/ZRSwordFlow.fx')
    assert shader==(root/'ZenithReborn/Assets/Effects/ZRSwordFlow.fx').read_bytes()
palette=json.loads((out/f'soul-trail-gpu-{version}/sword-palette.json').read_text(encoding='utf-8'))
chosen=json.loads((out/'soul-trail-gpu-0.3.82/sword-palette.json').read_text(encoding='utf-8'))
assert palette['passed']
assert {x['item']:x['sourceColor'] for x in palette['items']}=={x['item']:x['sourceColor'] for x in chosen['items']}
allowed={'ZenithReborn/build.txt','ZenithReborn/SwordFlowMaterial.cs','ZenithReborn/Assets/Effects/ZRSwordFlow.fx',
    'ZenithReborn/Assets/Effects/ZRSwordFlow.fxc','ZenithReborn/Assets/Effects/build-manifest.json'}
with zipfile.ZipFile(out/'ZenithReborn-0.3.83-source.zip') as previous:
    files=[n for n in previous.namelist() if n.startswith('ZenithReborn/') and not n.endswith('/')]
    for name in files:
        if name not in allowed:assert previous.read(name)==(root/name).read_bytes(),name
result=dict(passed=True,testedPackageSha256=sha,glassReference='0.3.81',sourceColorReference='0.3.82',
    originalShaderSourceIdentical=True,fixedInputGpuComparisons=comparisons,
    sourceColorItems=[dict(item=x['item'],rgb=x['sourceColor']) for x in palette['items']],
    preservedProductionFiles=len(files)-len(allowed),actualGpu=True,actualNativeWorldFixture=True,
    actualMouseAttack=False,liveMultiplayer=False,artApproved=False,
    scope='归档0.3.81同输入GPU逐像素对照，不是与用户截图逐像素一致或自然游玩证据')
(out/f'glass-reference-restore-{version}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
