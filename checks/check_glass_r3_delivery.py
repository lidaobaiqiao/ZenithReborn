"""核对R3交付证据与实际包一致，并保留0.3.79玩法和已认可的短尾布局。"""
import hashlib
import json
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parent.parent
version='0.3.80'
package=root/'build-profile/Mods/ZenithReborn.tmod'
sha=hashlib.sha256(package.read_bytes()).hexdigest()
def report(name):
    value=json.loads((root/'artifacts'/name).read_text(encoding='utf-8-sig'))
    assert value.get('passed') and value.get('testedPackageSha256')==sha,name
    return value

gpu=report(f'soul-trail-gpu-{version}/gpu.json')
assert gpu['glassResponse']['Passed'] and gpu['glassResponse']['MaximumAlphaDifference']==0
assert all(gpu['glassResponse'][k]>400 for k in ('LightingChangedPixels','EnvironmentChangedPixels','RoughnessChangedPixels'))
assert gpu['expiredPixels']==0 and gpu['endingFinalPixels']==0
for name in ('NormalMap','SurfaceMap','HeightMap','EnvironmentMap'):
    assert '/GlassR3/' in gpu['defaultMaterialMaps'][name],name
world=report(f'soul-flight-world-{version}/world.json')
native=report(f'soul-flight-world-{version}/native-probe.json')
assert native['outsideChangedPixels']==0 and native['actualNativeFilter']
tree=report(f'soul-flight-world-{version}/tree-evidence.json')
echo=report(f'soul-flight-world-{version}-echo-damage/world.json')
assert echo['echoDamageTested'] and set(echo['echoDamageUnits'])=={1,2,3}
assert all(not hit['bodyCanDamage'] and hit['damage']>0 for hit in echo['echoHitRecords'])
surface=report(f'soul-flight-world-{version}-surface/world.json')
assert surface['advancedSurfaceMaterial'] and surface['materialLightCalls']>0
lab=report(f'material-lab-{version}/gpu.json')
assert len(lab['cases'])==45 and all(case['passed'] for case in lab['cases'])
assert lab['labPackageSha256']==hashlib.sha256((root/'build-profile/Mods/ZenithMaterialLab.tmod').read_bytes()).hexdigest()
report(f'vfx-ownership-{version}.json')

keep=['SoulBlade.cs','SoulTrailUnits.cs','SoulMaterialTrail.cs','SoulTrailEnding.cs','MartialBlades.cs','VfxSwordMask.cs']
with zipfile.ZipFile(root/'artifacts/ZenithReborn-0.3.79-source.zip') as old:
    for name in keep:
        assert old.read('ZenithReborn/'+name)==(root/'ZenithReborn'/name).read_bytes(),name
    old_assets=[name for name in old.namelist() if name.startswith('ZenithReborn/Assets/Vfx/') and not name.endswith('/')]
    assert all(old.read(name)==(root/name).read_bytes() for name in old_assets)
result=dict(passed=True,testedPackageSha256=sha,unchangedGameplayAndLayoutFiles=keep,
    oldVfxAssetsKept=len(old_assets),actualGpu=True,actualNativeWorldFixture=True,
    echoUnitsDamaged=echo['echoDamageUnits'],materialLabCases=45,
    actualMouseAttack=False,fullEnabledModCombination=False,liveMultiplayer=False,artApproved=False,
    preview='soul-flight-world-0.3.80/flight-original-speed.mp4',
    slowPreview='soul-flight-world-0.3.80/flight-preview-slow.gif')
(root/f'artifacts/glass-r3-delivery-{version}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
