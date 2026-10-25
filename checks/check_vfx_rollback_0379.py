"""逐字节核对0.3.79归档和当前生产文件，并验证包内源码及着色器。"""
import hashlib
import json
import zipfile
import argparse
from pathlib import Path

from tmod_archive import contents, read_archive

root = Path(__file__).resolve().parent.parent
source = root / 'ZenithReborn'
parser = argparse.ArgumentParser()
parser.add_argument('--version', default='0.3.85')
version = parser.parse_args().version
report_path = root / f'artifacts/vfx-rollback-{version}.json'
report = json.loads(report_path.read_text(encoding='utf-8'))
reference = root / report['reference']
with zipfile.ZipFile(reference) as archive:
    expected = {
        name.removeprefix('ZenithReborn/'): archive.read(name)
        for name in archive.namelist()
        if name.startswith('ZenithReborn/') and not name.endswith('/')
    }
actual = {
    path.relative_to(source).as_posix(): path.read_bytes()
    for path in source.rglob('*')
    if path.is_file() and not {'bin', 'obj'} & set(path.relative_to(source).parts)
}
assert actual.keys() == expected.keys(), '生产文件清单与0.3.79不一致'
for name, data in expected.items():
    if name == 'build.txt':
        data = data.replace(b'version = 0.3.79', ('version = '+version).encode('ascii'))
    assert actual[name] == data, f'生产文件偏离0.3.79：{name}'

package = root / 'build-profile/Mods/ZenithReborn.tmod'
_, name, version, entries = read_archive(package)
assert name == 'ZenithReborn' and version == report['version']
packed = {entry['name']: contents(entry) for entry in entries}
checked = []
for name, data in actual.items():
    if name.endswith(('.cs', '.fx', '.fxc', '.json')):
        assert packed[name] == data, f'包内文件与当前源码不一致：{name}'
        checked.append(name)
assert not any('GlassR3/' in name or 'SwordSpritePalette' in name for name in packed)
report.update(
    passed=True,
    referenceSha256=hashlib.sha256(reference.read_bytes()).hexdigest(),
    productionIdenticalExceptVersion=True,
    verifiedProductionFileCount=len(actual),
    verifiedPackedSourceAndEffectCount=len(checked),
    testedPackageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),
    scope='生产文件与0.3.79归档逐字节一致，仅版本号不同；包内源码和着色器一致，不代替实际游玩或多人验证',
)
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
