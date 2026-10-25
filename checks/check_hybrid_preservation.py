"""核对融合版只改变材质光效，旧版本收藏与其他生产实现保持完整。"""
import hashlib
import json
import zipfile
from pathlib import Path

from tmod_archive import contents, read_archive

root = Path(__file__).resolve().parent.parent
source = root / 'ZenithReborn'
allowed = {'build.txt', 'Assets/Effects/ZRSwordFlow.fx', 'Assets/Effects/ZRSwordFlow.fxc', 'Assets/Effects/build-manifest.json'}
with zipfile.ZipFile(root / 'artifacts/ZenithReborn-0.3.80-source.zip') as z:
    old = {n.removeprefix('ZenithReborn/'): z.read(n) for n in z.namelist() if n.startswith('ZenithReborn/') and not n.endswith('/')}
actual = {p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob('*') if p.is_file() and not {'bin', 'obj'} & set(p.relative_to(source).parts)}
assert old.keys() == actual.keys()
changed = sorted(name for name in old if old[name] != actual[name])
assert set(changed) == allowed, changed
assert actual['build.txt'] == old['build.txt'].replace(b'version = 0.3.80', b'version = 0.3.86')
gameplay = []
with zipfile.ZipFile(root / 'artifacts/ZenithReborn-0.3.79-source.zip') as z:
    for name in actual:
        if name.endswith('.cs') and name not in {'SwordFlowMaterial.cs', 'SoulSceneRefraction.cs'}:
            assert actual[name] == z.read('ZenithReborn/' + name), name
            gameplay.append(name)
for version in ('0.3.79', '0.3.80'):
    package = root / ('artifacts/backups/20261025-131530/ZenithReborn.tmod' if version == '0.3.79' else 'artifacts/ZenithReborn-0.3.80.tmod')
    expected = 'c7b88428525a192e5764de000e180be73184933a41a86bf61a3832bd1fecdad1' if version == '0.3.79' else '6473635ed76bffaeb06bb404375138bdb250b3b0529cb11755419e5d7aef20dd'
    assert hashlib.sha256(package.read_bytes()).hexdigest() == expected
package = root / 'build-profile/Mods/ZenithReborn.tmod'
_, name, version, entries = read_archive(package)
assert name == 'ZenithReborn' and version == '0.3.86'
packed = {entry['name']: contents(entry) for entry in entries}
verified = []
for name, data in actual.items():
    if name.endswith(('.cs', '.fx', '.fxc', '.json')):
        assert packed[name] == data, name
        verified.append(name)
report = dict(passed=True, testedPackageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),
    comparedWith='0.3.80', changedProductionFiles=changed, unchangedProductionFiles=len(actual) - len(changed),
    unchangedGameplaySourceFiles=len(gameplay), packageFilesVerified=len(verified), original79And80PackagesPreserved=True)
(root / 'artifacts/hybrid-preservation-0.3.86.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
