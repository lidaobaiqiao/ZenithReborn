"""核对逐剑配色和亮刃混色沿用79，生产变化限于本轮颜色修订。"""
import hashlib
import json
import re
import zipfile
from pathlib import Path

from tmod_archive import contents, read_archive

root = Path(__file__).resolve().parent.parent
source = root / 'ZenithReborn'
with zipfile.ZipFile(root / 'artifacts/ZenithReborn-0.3.79-source.zip') as old:
    assert (source / 'SoulVisuals.cs').read_bytes() == old.read('ZenithReborn/SoulVisuals.cs')
    material79 = old.read('ZenithReborn/SwordFlowMaterial.cs').decode('utf-8-sig')
    shader79 = old.read('ZenithReborn/Assets/Effects/ZRSwordFlow.fx').decode('utf-8-sig')
material = (source / 'SwordFlowMaterial.cs').read_text(encoding='utf-8-sig')
shader = (source / 'Assets/Effects/ZRSwordFlow.fx').read_text(encoding='utf-8-sig')
for parameter in ('uBodyColor', 'uEdgeColor'):
    pattern = rf'parameters\["{parameter}"\]\.SetValue\([^;]+;'
    assert re.search(pattern, material).group() == re.search(pattern, material79).group()
reference = re.search(r'float3 neutralHighlight=([^;]+);', shader79).group(1)
assert re.search(r'float3 edgeLight=([^;]+);', shader).group(1) == reference
assert 'reflected+=edgeLight*(sweep*0.16+fineSweep*0.035);' in shader
assert 'SwordSpritePalette' not in material
backup = json.loads((root / 'artifacts/color79-backup-0.3.87.json').read_text(encoding='utf-8'))
with zipfile.ZipFile(Path(backup['backup']) / 'production-0.3.86.zip') as z:
    previous = {n.removeprefix('ZenithReborn/'): z.read(n) for n in z.namelist()}
actual = {p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob('*') if p.is_file() and not {'bin', 'obj'} & set(p.relative_to(source).parts)}
assert previous.keys() == actual.keys()
changed = sorted(n for n in actual if actual[n] != previous[n])
assert set(changed) == {'build.txt', 'Assets/Effects/ZRSwordFlow.fx', 'Assets/Effects/ZRSwordFlow.fxc', 'Assets/Effects/build-manifest.json'}
package = root / 'build-profile/Mods/ZenithReborn.tmod'
_, name, version, entries = read_archive(package)
assert name == 'ZenithReborn' and version == '0.3.87'
packed = {e['name']: contents(e) for e in entries}
for n in actual:
    if n.endswith(('.cs', '.fx', '.fxc', '.json')):
        assert packed[n] == actual[n], n
report = dict(passed=True, version=version, paletteSource='0.3.79',
    swordPaletteIdentical=True, bodyAndEdgeColorInputsIdentical=True, highlightColorExpressionIdentical=True,
    changedProductionFiles=changed, unchangedProductionFiles=len(actual)-len(changed),
    testedPackageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),
    scope='核对配色输入与混色配方；玻璃受光和背景合成不同，不声称最终RGB画面与79逐像素相同')
(root / 'artifacts/color79-0.3.87.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
