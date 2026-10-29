"""将真实GPU输出原样并列，核对同姿态覆盖度；不调色、不补光、不用美术图冒充渲染。"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
folder = root / 'artifacts/clear-glass-0.3.111'
before, after = folder / 'baseline-verified', folder / 'verified'
old = json.loads((before / 'clear-glass.json').read_text())
new = json.loads((after / 'clear-glass.json').read_text())
native = json.loads((root / 'artifacts/soul-flight-world-0.3.111-full-clear-glass/native-probe.json').read_text())
package = root / 'build-profile/Mods/ZenithReborn.tmod'
sha = hashlib.sha256(package.read_bytes()).hexdigest()
assert old['passed'] and new['passed'] and native['passed']
assert old['thickness'] == 72 and new['thickness'] == 18
assert new['testedPackageSha256'] == native['testedPackageSha256'] == sha
assert all(value == 0 for value in native['materialBuffers'].values())
same_coverage = Image.open(before / 'single-1.png').tobytes() == Image.open(after / 'single-1.png').tobytes()
same_background = all(Image.open(before / f'background-{kind}.png').tobytes() ==
                      Image.open(after / f'background-{kind}.png').tobytes() for kind in ('day', 'night', 'pattern'))
assert same_coverage and same_background

font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 28)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
board = Image.new('RGB', (1944, 1246), '#11151c')
draw = ImageDraw.Draw(board)
draw.text((12, 10), '原版 0.3.110', font=font, fill='#e5e7eb')
draw.text((984, 10), '通透材质 0.3.111', font=font, fill='#bce5dc')
for row, light in enumerate(('day', 'night')):
    y = 56 + row * 574
    for column, source in enumerate((before, after)):
        board.paste(Image.open(source / f'757-{light}.png').convert('RGB'), (12 + column * 972, y))
    draw.text((12, y + 542), '白天背景' if light == 'day' else '夜间背景', font=small, fill='#aab2bf')
draw.text((12, 1214), '同一背景 / 同一姿态 / 实际 GPU · 图片未调色 · 背景重放不等于本轮自然游玩', font=small, fill='#aab2bf')
board.save(folder / 'comparison.png')

# 在完全相同的区域统计归一化表面光，避免四层扩大覆盖范围后换了一组像素。
roi = np.array(Image.open(before / 'single-1.png'))[:, :, 3] > 160
ratios = {}
for label, source in (('old', before), ('new', after)):
    energy = []
    for name in ('single', 'overlap-four'):
        normal = np.array(Image.open(source / f'{name}-1.png')).astype(float)
        surface = np.array(Image.open(source / f'{name}-2.png')).astype(float)
        energy.append((surface[:, :, :3].sum(2) / np.maximum(normal[:, :, 3], 1))[roi].mean())
    ratios[label] = energy[1] / energy[0]
assert ratios['old'] > 2 and .96 < ratios['new'] < 1.04
result = dict(passed=True, testedPackageSha256=sha, sameNormalAndCoverage=same_coverage,
              sameBackground=same_background, sameRoiOverlapGain=ratios,
              focusedEngineChecks=95, actualGpu=True, nativeFilter=True,
              actualWorldLightingPresets=['day'], replayedBackgrounds=['day', 'night'],
              originalPackageSha256=old['testedPackageSha256'],
              gpuReport='verified/clear-glass.json', baselineReport='baseline-verified/clear-glass.json',
              nativeReport='../soul-flight-world-0.3.111-full-clear-glass/native-probe.json',
              artApproved=False, naturalGameplayTested=False, liveMultiplayerTested=False)
(folder / 'validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
