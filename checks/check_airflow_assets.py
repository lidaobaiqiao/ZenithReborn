"""核对整组导出、运行时素材、逐矿材颜色与拖尾长度，不以此代替画面审核。"""
import hashlib
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent
recipes = sorted((root / 'VfxLibrary/Masks/SwordBody').glob('*/H4/authoring.json'))
assert len(recipes) == 21
colors = set()
results = []
for path in recipes:
    name = path.parent.parent.name
    recipe = json.loads(path.read_text(encoding='utf-8'))
    clip = json.loads((path.parent / 'clip.json').read_text(encoding='utf-8'))
    report = json.loads((root / f'artifacts/airflow-H4-export/{name}/gpu.json').read_text(encoding='utf-8'))
    assert report['passed'] and report['actualGpu'] and not report['artApproved']
    atlas = path.parent / 'active.png'
    raw = atlas.read_bytes()
    assert hashlib.sha256(raw).hexdigest() == clip['atlasSha256'] == report['maskSha256']
    assert raw == (root / f'ZenithReborn/Assets/Vfx/SwordBody/{name}.png').read_bytes()
    for source, expected in clip['sourceDependencies'].items():
        assert hashlib.sha256((root / source).read_bytes()).hexdigest() == expected
    field = json.loads((root / f'VfxLibrary/ColorFields/SwordBody/{name}/entry.json').read_text(encoding='utf-8'))
    colors.add(tuple(field['variants']['H4']['recipe']['clear']))
    def reach(revision, width, pivot):
        with Image.open(path.parent.parent / revision / 'active.png') as image:
            alpha = image.getchannel('A').crop((0, 0, width, 256))
            bounds = alpha.point(lambda value: 255 if value > 32 else 0).getbbox()
            assert bounds is not None
            return pivot - bounds[0]
    before = reach('H3', 448, 305)
    after = reach('H4', 640, 497)
    assert after > before * 1.5, (name, before, after)
    results.append(dict(sword=name,oldReach=before,newReach=after,ratio=round(after / before, 2)))
assert len(colors) == 13, '应保留十三种矿材的独立底色，不能全部变成同一种颜色'
summary = dict(passed=True,swords=21,distinctMaterialColors=len(colors),
               alphaExportAndRuntimeIdentical=True,tailReachResults=results,artApproved=False)
out = root / 'artifacts/airflow-H4-assets.json'
out.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS: 21把图集与运行时素材一致；13种矿材底色；可见尾迹长度逐剑增加超过50%。')
