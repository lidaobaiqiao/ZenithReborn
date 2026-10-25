"""将两版原始客户端抓帧并排呈现；不改变画面曝光、色彩或对比度。"""
import hashlib
import json
import subprocess
from pathlib import Path

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
folder = root / 'artifacts/vfx-comparison-20261025-originals'
versions = ('0.3.79', '0.3.80')
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
small_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
world = [json.loads((folder / f'{v}-world/world.json').read_text(encoding='utf-8')) for v in versions]
gpu = [json.loads((folder / f'{v}-gpu/gpu.json').read_text(encoding='utf-8')) for v in versions]
for version, report, fixed in zip(versions, world, gpu):
    assert report['passed'] and report['actualWorld'] and report['productionRenderer']
    session = json.loads((folder / f'{version}-world/capture-session.json').read_text(encoding='utf-8-sig'))
    assert report['testedPackageSha256'] == fixed['testedPackageSha256'] == session['packageSha256']

# 核对真实AI姿态，不能仅凭截图看起来相似就声称轨迹相同。
def poses(report):
    return {(p['tick'], p['type']): p for p in report['poses']}

left, right = map(poses, world)
assert left.keys() == right.keys()
max_position_error = max(float(np.hypot(left[k]['x'] - right[k]['x'], left[k]['y'] - right[k]['y'])) for k in left)
max_rotation_error = max(abs(left[k]['rotation'] - right[k]['rotation']) for k in left)
assert max_position_error < .01 and max_rotation_error < .0001

crop = (250, 180, 1050, 600)
width, height = crop[2] - crop[0], crop[3] - crop[1]

def paired(tick):
    panel = Image.new('RGB', (width * 2 + 8, height + 40), (22, 25, 31))
    draw = ImageDraw.Draw(panel)
    for index, version in enumerate(versions):
        path = folder / f'{version}-world/frames/frame-{tick:03}.png'
        image = Image.open(path).convert('RGB')
        assert image.size == (1280, 720)
        x = index * (width + 8)
        panel.paste(image.crop(crop), (x, 40))
        draw.text((x + 12, 7), f'{version} 原始画面 · tick {tick} · 局部原尺寸', font=font, fill='white')
    return panel

selected = (42, 54, 153, 264)
sheet = Image.new('RGB', (width * 2 + 8, (height + 40) * len(selected)), (22, 25, 31))
for i, tick in enumerate(selected):
    panel = paired(tick)
    panel.save(folder / f'world-pair-{tick:03}.png')
    sheet.paste(panel, (0, i * (height + 40)))
sheet.save(folder / 'world-contact-sheet.jpg', quality=95)

ticks = sorted(int(p.stem.split('-')[1]) for p in (folder / '0.3.79-world/frames').glob('frame-*.png'))
assert all(b - a == 3 for a, b in zip(ticks, ticks[1:]))
process = subprocess.Popen([
    imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24',
    '-s', f'{width * 2 + 8}x{height + 40}', '-r', '20', '-i', '-', '-an',
    '-c:v', 'libx264', '-crf', '17', '-pix_fmt', 'yuv420p',
    str(folder / 'world-original-speed-side-by-side.mp4'),
], stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
for tick in ticks:
    process.stdin.write(paired(tick).tobytes())
process.stdin.close()
assert process.wait() == 0

fixed_images = [Image.open(folder / f'{v}-gpu/fixed-a.png').convert('RGB') for v in versions]
fixed_sheet = Image.new('RGB', (1928, 582), (22, 25, 31))
for i, (version, image) in enumerate(zip(versions, fixed_images)):
    fixed_sheet.paste(image, (i * 968, 42))
    ImageDraw.Draw(fixed_sheet).text((i * 968 + 12, 9), f'{version} · 固定几何 / 材质时钟0 / 原尺寸', font=font, fill='white')
fixed_sheet.save(folder / 'fixed-material-pair.png')

# 此统计只描述固定GPU面板的相对光输出，不代替世界画面的美术判断。
arrays = [np.asarray(image, dtype=np.float64) for image in fixed_images]
background = np.array([12., 17., 29.])
support = np.maximum(np.abs(arrays[0] - background).max(axis=2), np.abs(arrays[1] - background).max(axis=2)) > 8
weights = np.array([.2126, .7152, .0722])
light_output = [np.maximum((a - background) @ weights, 0)[support] for a in arrays]
measurements = {v: {'meanAboveBackground': float(a.mean()), 'p95AboveBackground': float(np.percentile(a, 95))} for v, a in zip(versions, light_output)}
report = {
    'captureCompleted': True, 'versions': list(versions),
    'originalPackages': {v: w['testedPackageSha256'] for v, w in zip(versions, world)},
    'actualGpu': True, 'actualWorld': True, 'worldFixturesPassed': all(w['passed'] for w in world),
    'maxPositionDifferencePixels': max_position_error, 'maxRotationDifferenceRadians': max_rotation_error,
    'worldMaterialClockLocked': False,
    'fixedMaterialClock': 0, 'sameCameraAndDaylightSetup': True,
    'fixedPanelLuminance': measurements,
    'fixedPanelMeanRatio80To79': float(light_output[1].mean() / light_output[0].mean()),
    'legacyGpuSuitePassed': {v: g['passed'] for v, g in zip(versions, gpu)},
    'legacyGpuGlassSurface': {v: g['glassSurface'] for v, g in zip(versions, gpu)},
    'captureScope': '两版原始包独立客户端真实世界夹具；世界材质连续时钟未锁定，固定GPU面板使用同一时钟。未改曝光、亮度、色彩或对比度。旧GPU总套件结果单列，不把抓帧成功写成全部检查通过。',
    'worldCrop': list(crop), 'worldCropScale': 1, 'capturedEveryGameTicks': 3, 'videoFps': 20,
    'actualMouseAttack': False, 'liveMultiplayerTested': False,
    'formalInstalledSha256': sha(Path(r'C:/Users/15537/Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod')),
}
assert report['formalInstalledSha256'] == '733da93f4493db9c82d5cd8bd86a42dfebf7edc6ac2b915e2e0a74e9a4ed0b30'
(folder / 'comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
