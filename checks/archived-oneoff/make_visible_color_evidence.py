"""汇总原始世界抓帧和七剑GPU输出，不调整曝光、亮度、饱和度或色相。"""
import hashlib
import json
import subprocess
from pathlib import Path

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
output = root / 'artifacts/visible-colors-0.3.88'
output.mkdir(exist_ok=True)
world = root / 'artifacts/soul-flight-world-0.3.88-echo-damage-full'
report = json.loads((world / 'world.json').read_text(encoding='utf-8'))
assert report['passed'] and not report['actualMouseAttack']
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
samples = [('泰拉之刃 · 黄绿', 42), ('附魔剑 · 蓝', 153), ('星怒 · 粉', 264)]
sheet = Image.new('RGB', (2528, 540), (20, 23, 29))
source_hashes = {}
for index, (label, tick) in enumerate(samples):
    path = world / f'frames/frame-{tick:03}.png'
    image = Image.open(path).convert('RGB')
    assert image.size == (2560, 1600)
    sheet.paste(image.crop((840, 560, 1680, 1060)), (index * 844, 40))
    ImageDraw.Draw(sheet).text((index * 844 + 12, 7), label + f' · 帧{tick}', font=font, fill='white')
    source_hashes[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
sheet.save(output / 'three-swords-world.png')
palette = json.loads((root / 'artifacts/soul-trail-gpu-0.3.88/sword-palette.json').read_text(encoding='utf-8'))
names = ['泰拉之刃', '附魔剑', '星怒', '炽焰巨剑', '冰雪刃', '彩虹猫之刃', '银阔剑']
grid = Image.new('RGB', (1288, 1344), (20, 23, 29))
for index, item in enumerate(palette['items']):
    x, y = index % 2 * 648, index // 2 * 336
    image = Image.open(root / f"artifacts/soul-trail-gpu-0.3.88/sword-palette/material-{item['item']}.png").convert('RGBA')
    grid.paste(image, (x, y + 36), image)
    ImageDraw.Draw(grid).text((x + 14, y + 6), names[index] + ' · 同网格/时刻/光向的生产GPU输出', font=small, fill='white')
grid.save(output / 'seven-swords-gpu.png')
# 每3个游戏刻采样一次，视频按20fps播放，标签明确为受控世界夹具。
paths = [world / f'frames/frame-{tick:03}.png' for tick in range(15, 328, 3)]
process = subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24',
    '-s', '1280x840', '-r', '20', '-i', '-', '-an', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
    str(output / 'three-swords-original-speed.mp4')], stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
for path in paths:
    canvas = Image.new('RGB', (1280, 840), (20, 23, 29))
    canvas.paste(Image.open(path).convert('RGB').resize((1280, 800)), (0, 40))
    ImageDraw.Draw(canvas).text((12, 8), '0.3.88 · 正式五模组/画面设置 · 隔离受控攻击 · 原速', font=font, fill='white')
    process.stdin.write(canvas.tobytes())
process.stdin.close()
assert process.wait() == 0
metadata = dict(passed=True, testedPackageSha256=report['testedPackageSha256'], sourceWorld=str(world.relative_to(root)),
    actualMouseAttack=False, liveMultiplayerTested=False, capturesEveryGameTicks=3, playbackFps=20,
    colorAdjustments=False, contactSheetOriginalPixelScale=True, videoScale=.5,
    samples=samples, inputSha256=source_hashes, continuouslyRunningMaterialClock=True)
(output / 'evidence.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(metadata, ensure_ascii=False, indent=2))
