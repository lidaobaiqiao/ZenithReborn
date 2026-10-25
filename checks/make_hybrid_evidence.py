"""生成79、80与融合版的原速对照，保留原始抓帧亮度和色彩。"""
import hashlib
import json
import subprocess
from pathlib import Path

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
output = root / 'artifacts/hybrid-0.3.86'
output.mkdir(exist_ok=True)
archived = root / 'artifacts/vfx-comparison-20261025-originals'
folders = [archived / '0.3.79-world', archived / '0.3.80-world', root / 'artifacts/soul-flight-world-0.3.86']
reports = [json.loads((folder / 'world.json').read_text(encoding='utf-8')) for folder in folders]
assert all(report['passed'] for report in reports)
poses = [{(p['tick'], p['type']): p for p in report['poses']} for report in reports]
assert all(p.keys() == poses[0].keys() for p in poses)
max_position = max(float(np.hypot(p[k]['x'] - poses[0][k]['x'], p[k]['y'] - poses[0][k]['y'])) for p in poses[1:] for k in p)
max_rotation = max(abs(p[k]['rotation'] - poses[0][k]['rotation']) for p in poses[1:] for k in p)
assert max_position == 0 and max_rotation == 0
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
names = ['0.3.79 原始光效', '0.3.80 原始质感', '0.3.86 融合版']
crop = (250, 180, 1050, 600)
w, h = 800, 420

def frame(tick, indices=(0, 1, 2)):
    result = Image.new('RGB', (w * len(indices) + 8 * (len(indices) - 1), h + 40), (22, 25, 31))
    for column, index in enumerate(indices):
        image = Image.open(folders[index] / f'frames/frame-{tick:03}.png').convert('RGB')
        result.paste(image.crop(crop), (column * (w + 8), 40))
        ImageDraw.Draw(result).text((column * (w + 8) + 12, 8), f'{names[index]} · tick {tick} · 局部原尺寸', font=font, fill='white')
    return result

for tick in (42, 54, 153, 264):
    frame(tick).save(output / f'three-version-{tick:03}.png')
    frame(tick, (1, 2)).save(output / f'glass80-vs-hybrid-{tick:03}.png')
ticks = sorted(int(path.stem.split('-')[1]) for path in (folders[2] / 'frames').glob('frame-*.png'))
assert all(b - a == 3 for a, b in zip(ticks, ticks[1:]))
video = output / 'three-version-original-speed.mp4'
proc = subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24',
    '-s', '2416x460', '-r', '20', '-i', '-', '-an', '-c:v', 'libx264', '-crf', '17', '-pix_fmt', 'yuv420p', str(video)],
    stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
for tick in ticks:
    proc.stdin.write(frame(tick).tobytes())
proc.stdin.close()
assert proc.wait() == 0
gpu = json.loads((root / 'artifacts/soul-trail-gpu-0.3.86/gpu.json').read_text(encoding='utf-8'))
optics = json.loads((root / 'artifacts/hybrid-optics-0.3.86/hybrid.json').read_text(encoding='utf-8'))
native = json.loads((folders[2] / 'native-probe.json').read_text(encoding='utf-8'))
assert gpu['passed'] and optics['passed'] and native['passed']
sha = hashlib.sha256((root / 'build-profile/Mods/ZenithReborn.tmod').read_bytes()).hexdigest()
assert sha == reports[2]['testedPackageSha256'] == gpu['testedPackageSha256'] == optics['testedPackageSha256'] == native['testedPackageSha256']
report = dict(passed=True, testedPackageSha256=sha, actualGpu=True, actualWorld=True,
    normalMaskDifference=optics['maximumNormalMaskDifference'], opticsDifference=optics['maximumOpticsDifference'],
    transmissionDifference=optics['maximumTransmissionDifference'], alphaDifference=optics['maximumAlphaDifference'],
    minimumUnchangedInteriorFraction=min(case['unchangedInteriorFraction'] for case in optics['cases']),
    maxPositionDifferencePixels=max_position, maxRotationDifferenceRadians=max_rotation,
    worldMaterialClockLocked=False, fixedOpticsProbeClockLocked=True,
    nativeIorChangedPixels=native['changedPixels'], nativeIorOutsideChangedPixels=native['outsideChangedPixels'],
    crop=list(crop), cropScale=1, capturedEveryGameTicks=3, videoFps=20,
    brightnessOrColorPostprocessing=False, actualMouseAttack=False, liveMultiplayerTested=False,
    video=str(video.relative_to(root)))
(output / 'evidence.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
