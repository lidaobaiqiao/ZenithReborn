"""核对最终包的逐剑GPU颜色、世界证据和生产修改范围，避免把旧候选结果当作交付。"""
import hashlib
import json
import zipfile
from pathlib import Path

from tmod_archive import contents, read_archive

root = Path(__file__).resolve().parent.parent
out = root / 'artifacts'
source = root / 'ZenithReborn'
package = root / 'build-profile/Mods/ZenithReborn.tmod'
sha = lambda data: hashlib.sha256(data).hexdigest()
package_sha = sha(package.read_bytes())
_, name, version, entries = read_archive(package)
assert name == 'ZenithReborn' and version == '0.3.88'
packed = {entry['name']: contents(entry) for entry in entries}
actual = {p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob('*')
          if p.is_file() and not {'bin', 'obj'} & set(p.relative_to(source).parts)}
for relative, data in actual.items():
    if relative.endswith(('.cs', '.fx', '.fxc', '.json')):
        assert packed[relative] == data, relative
backup = Path(json.loads((out / 'visible-color-backup-0.3.88.json').read_text(encoding='utf-8'))['backup'])
with zipfile.ZipFile(backup / 'production-0.3.87.zip') as archive:
    previous = {n.removeprefix('ZenithReborn/'): archive.read(n) for n in archive.namelist()}
assert actual.keys() - previous.keys() == {'SwordSpritePalette.cs'}
assert not previous.keys() - actual.keys()
changed = sorted(n for n in previous if previous[n] != actual[n])
assert set(changed) == {'SwordFlowMaterial.cs', 'build.txt', 'Assets/Effects/ZRSwordFlow.fx',
                        'Assets/Effects/ZRSwordFlow.fxc', 'Assets/Effects/build-manifest.json'}, changed

def verified(relative):
    report = json.loads((out / relative).read_text(encoding='utf-8'))
    assert report['passed'] and report['testedPackageSha256'] == package_sha, relative
    return report

gpu = verified(f'soul-trail-gpu-{version}/gpu.json')
palette = json.loads((out / f'soul-trail-gpu-{version}/sword-palette.json').read_text(encoding='utf-8'))
assert palette['passed'] and palette['actualGpu'] and len(palette['items']) == 7
assert all(item['matched'] for item in palette['items'])
optics = verified(f'hybrid-optics-{version}/hybrid.json')
assert all(optics[key] == 0 for key in ('maximumNormalMaskDifference', 'maximumOpticsDifference',
                                      'maximumTransmissionDifference', 'maximumAlphaDifference'))
world = verified(f'soul-flight-world-{version}/world.json')
probe = verified(f'soul-flight-world-{version}/native-probe.json')
full_relative = f'soul-flight-world-{version}-echo-damage-full/world.json'
full = verified(full_relative)
assert full['echoDamageTested'] and set(full['echoDamageUnits']) == {1, 2, 3}
assert full['remainingEndings'] == 0 and not full['actualMouseAttack']
formal_mods = Path.home() / 'Documents/My Games/Terraria/tModLoader/Mods'
formal_names = set(json.loads((formal_mods / 'enabled.json').read_text(encoding='utf-8-sig')))
full_profile = out / f'soul-flight-world-profile-{version}-echo-damage-full'
log_bytes = (out / full_relative).parent.joinpath('stdout.log').read_bytes()
workshop = Path('E:/SteamLibrary/steamapps/workshop/content/1281930')
workshop_packages = {'HEROsMod': '2564645933/2026.7/HEROsMod.tmod',
    'ImproveGame': '2797518634/2026.3/ImproveGame.tmod',
    'SilkyUIFramework': '3403460014/2026.3/SilkyUIFramework.tmod',
    'UpgradableZenith': '3339387093/2025.7/UpgradableZenith.tmod'}
for mod in formal_names:
    # 原生控制台与日志可能混用代码页，直接核对完整加载记录的字节片段，避免宽松解码掩盖缺项。
    assert any(f'加载程序集: {mod} ('.encode(encoding) in log_bytes
               for encoding in ('utf-8', 'gb18030')), mod
    if mod != name:
        formal_package = formal_mods / f'{mod}.tmod'
        if not formal_package.exists():
            formal_package = workshop / workshop_packages[mod]
        assert sha(formal_package.read_bytes()) == sha((full_profile / 'Mods' / f'{mod}.tmod').read_bytes()), mod
for original_version, relative, expected in (
    ('0.3.79', 'backups/20261025-131530/ZenithReborn.tmod', 'c7b88428525a192e5764de000e180be73184933a41a86bf61a3832bd1fecdad1'),
    ('0.3.80', 'ZenithReborn-0.3.80.tmod', '6473635ed76bffaeb06bb404375138bdb250b3b0529cb11755419e5d7aef20dd')):
    assert sha((out / relative).read_bytes()) == expected, original_version
result = dict(passed=True, version=version, testedPackageSha256=package_sha,
    comparedWith='0.3.87', changedProductionFiles=changed, addedProductionFiles=['SwordSpritePalette.cs'],
    unchangedProductionFiles=len(previous)-len(changed), original79And80PackagesPreserved=True,
    palette=palette['items'], fullModSetWorldVerified=True, formalModNames=sorted(formal_names),
    fullModSetWorldReport=full_relative, echoDamageUnits=sorted(full['echoDamageUnits']),
    actualMouseGameplayTested=False, liveMultiplayerTested=False, artApproved=False,
    opticalComparisonScope='相同颜色输入下与80比较；实际剑色改变时透射染色也会改变')
(out / f'visible-colors-{version}.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key != 'palette'}, ensure_ascii=False, indent=2))
