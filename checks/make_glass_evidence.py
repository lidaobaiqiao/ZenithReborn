"""从隔离世界的同帧最终窗口图导出对照，记录树干轮廓的实际横向错位。"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
version = next(line.split('=', 1)[1].strip() for line in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
output = root/f'artifacts/soul-flight-world-{version}'
probe = json.loads((output/'native-probe.json').read_text(encoding='utf-8'))
world = json.loads((output/'world.json').read_text(encoding='utf-8'))
assert probe['passed'] and world['passed'] and probe['testedPackageSha256'] == world['testedPackageSha256']
images = [Image.open(output/f'native-ior-{value}.png').convert('RGB') for value in ('1.0', '1.5')]

# 这是既定隔离森林场景中穿过剑迹的树干，不向游戏添加测试条纹或改变正式世界。
row, left, right = 448, 585, 645
def brown_run(image):
    pixels = image.load()
    runs, run = [], []
    for x in range(left, right):
        red, green, blue = pixels[x, row]
        if min(red, green)-blue > 17 and red > green*.75:
            run.append(x)
        elif run:
            runs.append(run); run = []
    if run:
        runs.append(run)
    return max(runs, key=len)

runs = [brown_run(image) for image in images]
centers = [(run[0]+run[-1])*.5 for run in runs]
shift = centers[1]-centers[0]
old_region_retained = len(set(runs[0]) & set(runs[1]))/len(runs[0])

font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 19)
crop = (560, 398, 750, 520)
panels = []
for value, image in zip(('1.0', '1.5'), images):
    detail = image.crop(crop).resize((380, 244), Image.Resampling.NEAREST)
    panel = Image.new('RGB', (380, 280), (22, 25, 31))
    panel.paste(detail, (0, 36))
    ImageDraw.Draw(panel).text((10, 7), f'同帧 IOR {value} · 局部2倍', font=font, fill='white')
    panels.append(panel)
comparison = Image.new('RGB', (768, 280), (65, 68, 74))
comparison.paste(panels[0], (0, 0)); comparison.paste(panels[1], (388, 0))
comparison.save(output/'tree-refraction-comparison.png')
panels[0].save(output/'tree-refraction-comparison.gif', save_all=True, append_images=panels[1:], duration=1200, loop=0, optimize=False)
frames = [Image.open(path).convert('RGB') for path in sorted((output/'frames').glob('frame-*.png')) if 15 <= int(path.stem.split('-')[1]) <= 108]
frames[0].save(output/'flight-preview.gif', save_all=True, append_images=frames[1:], duration=50, loop=0, optimize=False)
report = dict(passed=abs(shift)>=8 and old_region_retained<.25,
    testedPackageSha256=probe['testedPackageSha256'], actualNativeFinalWindow=True,
    sameFrame=True, row=row, searchX=[left,right], crop=list(crop), previewScale=2,
    treeRanges=[[run[0],run[-1]] for run in runs], horizontalShiftPixels=shift,
    unbentFeatureRetainedFraction=old_region_retained,
    inputImages={f'native-ior-{value}.png':hashlib.sha256((output/f'native-ior-{value}.png').read_bytes()).hexdigest() for value in ('1.0','1.5')})
(output/'tree-evidence.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
assert report['passed'], report
print(json.dumps(report, ensure_ascii=True))
