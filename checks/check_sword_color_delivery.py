"""核对剑身取色、能量亮度、原版滤镜及玩法保留，不能把GPU技术通过当美术认可。"""
import hashlib
import json
import zipfile
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parent.parent
# 版本号取自 build.txt，不再写死：交付证据必须跟着当前构建走，否则每升一版就失效。
version=next(line.split('=',1)[1].strip() for line in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
out=root/'artifacts'
sha=hashlib.sha256((root/'build-profile/Mods/ZenithReborn.tmod').read_bytes()).hexdigest()
def report(path):
    value=json.loads((out/path).read_text(encoding='utf-8-sig'))
    assert value['passed'] and value['testedPackageSha256']==sha,path
    return value
gpu=report(f'soul-trail-gpu-{version}/gpu.json')
world=report(f'soul-flight-world-{version}/world.json')
native=report(f'soul-flight-world-{version}/native-probe.json')
echo=report(f'soul-flight-world-{version}-echo-damage/world.json')
assert gpu['swordPaletteVerified'] and gpu['glassResponse']['Passed'] and gpu['glassResponse']['MaximumAlphaDifference']==0
assert native['outsideChangedPixels']==0 and world['remainingEndings']==0
assert echo['echoDamageTested'] and set(echo['echoDamageUnits'])=={1,2,3}
report(f'vfx-ownership-{version}.json')
palette=json.loads((out/f'soul-trail-gpu-{version}/sword-palette.json').read_text(encoding='utf-8'))
assert palette['passed'] and len(palette['items'])==7 and all(x['matched'] for x in palette['items'])
brightness={}
for filename in ('glass-alpha.png','response-light-left.png','response-light-right.png'):
    images=[np.asarray(Image.open(out/f'soul-trail-gpu-{v}'/filename).convert('RGBA'),dtype=float) for v in ('0.3.81',version)]
    assert np.array_equal(images[0][:,:,3],images[1][:,:,3]),filename
    mask=images[0][:,:,3]>64
    means=[float((im[:,:,:3]@np.array([.2126,.7152,.0722]))[mask].mean()) for im in images]
    assert means[1]/means[0]>1.5,(filename,means)
    brightness[filename]=dict(before=means[0],after=means[1],ratio=means[1]/means[0],alphaIdentical=True)
allowed={'ZenithReborn/build.txt','ZenithReborn/SwordFlowMaterial.cs','ZenithReborn/Assets/Effects/ZRSwordFlow.fx',
    'ZenithReborn/Assets/Effects/ZRSwordFlow.fxc','ZenithReborn/Assets/Effects/build-manifest.json'}
with zipfile.ZipFile(out/'ZenithReborn-0.3.81-source.zip') as old:
    files=[n for n in old.namelist() if n.startswith('ZenithReborn/') and not n.endswith('/')]
    for name in files:
        if name not in allowed:assert old.read(name)==(root/name).read_bytes(),name
result=dict(passed=True,testedPackageSha256=sha,comparedWith='0.3.81',brightness=brightness,
    sourceTextureColors=palette['items'],preservedProductionFiles=len(files)-len(allowed),
    changedFiles=sorted(allowed),newFiles=['ZenithReborn/SwordSpritePalette.cs'],
    actualGpu=True,actualNativeWorldFixture=True,echoUnitsDamaged=echo['echoDamageUnits'],
    actualMouseAttack=False,liveMultiplayer=False,fullEnabledModCombination=False,artApproved=False)
(out/f'sword-color-delivery-{version}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
