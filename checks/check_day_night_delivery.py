"""核对89昼夜纹理修订、原始80保留边界以及实际测试包，不能只凭静态参数宣布美术通过。"""
import hashlib
import json
import zipfile
from pathlib import Path
from tmod_archive import read_archive,contents

root=Path(__file__).resolve().parent.parent
package=root/'build-profile/Mods/ZenithReborn.tmod'
digest=hashlib.sha256(package.read_bytes()).hexdigest()
def report(relative):
    path=root/'artifacts'/relative
    value=json.loads(path.read_text(encoding='utf-8-sig'))
    assert value.get('passed',value.get('Passed',False)),relative
    if 'testedPackageSha256' in value:assert value['testedPackageSha256']==digest,relative
    assert path.stat().st_mtime>package.stat().st_mtime,relative
    return value
gpu=report('soul-trail-gpu-0.3.89/gpu.json')
day_night=report('soul-trail-gpu-0.3.89/day-night.json')
assert day_night['DayMaximumDifference']==day_night['MaximumOpticalDifference']==0
worlds={}
for suffix in ['full','full-night','full-dusk','full-torch']:
    world=report(f'soul-flight-world-0.3.89-{suffix}/world.json')
    native=report(f'soul-flight-world-0.3.89-{suffix}/native-probe.json')
    assert native['outsideChangedPixels']==0 and native['actualNativeFilter']
    assert len(world['damagedItems'])==len(world['completedReturnItems'])==3
    worlds[suffix]=dict(passed=True,lightPreset=world['lightPreset'],outsideChangedPixels=0)
allowed={'build.txt','SwordFlowMaterial.cs','Assets/Effects/build-manifest.json','Assets/Effects/ZRSwordFlow.fx','Assets/Effects/ZRSwordFlow.fxc'}
unchanged=0;changed=[]
with zipfile.ZipFile(root/'artifacts/vfx-presets/0.3.80/ZenithReborn-0.3.80-source.zip') as archive:
    for name in archive.namelist():
        if not name.startswith('ZenithReborn/') or name.endswith('/'):continue
        path=root/name
        assert path.exists(),name
        if path.read_bytes()==archive.read(name):unchanged+=1
        else:
            relative=name.removeprefix('ZenithReborn/')
            assert relative in allowed,relative
            changed.append(relative)
_,name,version,entries=read_archive(package)
assert name=='ZenithReborn' and version=='0.3.89'
packed={entry['name']:contents(entry) for entry in entries}
for relative in changed:
    if relative.endswith('.cs') or relative.endswith('.fxc'):
        assert packed[relative]==(root/'ZenithReborn'/relative).read_bytes(),relative
metadata=json.loads((root/'ZenithReborn/Assets/Vfx/Materials/SwordFlow/GlassR3/EnvironmentSoftMap.json').read_text(encoding='utf-8'))
assert metadata['adjacentDifferenceRatio']<.2 and not metadata['originalOverwritten']
evidence=json.loads((root/'artifacts/soul-trail-gpu-0.3.89-texture-ablation/texture-analysis.json').read_text(encoding='utf-8'))
assert evidence['smoothEnvToOriginalRatio']<.7
result=dict(passed=True,testedPackageSha256=digest,baseline='0.3.80',unchangedBaselineFiles=unchanged,
    changedBaselineFiles=changed,dayMaximumDifference=0,opticalMaximumDifference=0,worlds=worlds,
    textureHighPassRatio=evidence['smoothEnvToOriginalRatio'],originalTexturesKept=True,
    exactUserVideoGridReproduced=False,actualMouseGameplayTested=False,liveMultiplayerTested=False,artApproved=False)
(root/'artifacts/day-night-delivery-0.3.89.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
