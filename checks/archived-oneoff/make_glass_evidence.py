"""导出隔离世界同帧树干对照与按实际采样频率播放的预览。"""
import hashlib
import json
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import imageio_ffmpeg

root = Path(__file__).resolve().parent.parent
version = next(line.split('=', 1)[1].strip() for line in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
output = root/f'artifacts/soul-flight-world-{version}'
probe = json.loads((output/'native-probe.json').read_text(encoding='utf-8'))
world = json.loads((output/'world.json').read_text(encoding='utf-8'))
assert probe['passed'] and world['passed'] and probe['testedPackageSha256'] == world['testedPackageSha256']
images = [Image.open(output/f'native-ior-{value}.png').convert('RGB') for value in ('1.0', '1.5')]

# 短尾下旧版448行不再穿过玻璃；反射也会改变棕色阈值，不能继续拿它量树干位移。
# 保留原始像素局部对照，只统计IOR响应，不把颜色差异冒充几何位移。
tree_patch=(590,478,630,499)
delta=np.abs(np.asarray(images[0].crop(tree_patch),dtype=int)-np.asarray(images[1].crop(tree_patch),dtype=int)).sum(axis=2)
changed=int(np.count_nonzero(delta>3))

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
paths=[path for path in sorted((output/'frames').glob('frame-*.png')) if 15<=int(path.stem.split('-')[1])<=108]
ticks=[int(p.stem.split('-')[1]) for p in paths]
assert all(b-a==3 for a,b in zip(ticks,ticks[1:])),ticks
frames=[]
for path in paths:
    frame=Image.new('RGB',(960,578),(22,25,31))
    frame.paste(Image.open(path).convert('RGB').resize((960,540)),(0,38))
    ImageDraw.Draw(frame).text((12,7),'隔离世界 · 真实AI夹具 · 原速20fps采样',font=font,fill='white')
    frames.append(frame)
proc=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s','960x578','-r','20','-i','-',
    '-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(output/'flight-original-speed.mp4')],stdin=subprocess.PIPE,stderr=subprocess.DEVNULL)
for frame in frames:proc.stdin.write(frame.tobytes())
proc.stdin.close();assert proc.wait()==0
slow=[]
for frame in frames:
    frame=frame.copy();draw=ImageDraw.Draw(frame);draw.rectangle((0,0,959,37),fill=(22,25,31))
    draw.text((12,7),'隔离世界 · 真实AI夹具 · 3倍慢放',font=font,fill='white');slow.append(frame)
slow[0].save(output/'flight-preview-slow.gif',save_all=True,append_images=slow[1:],duration=150,loop=0,optimize=False)
report = dict(passed=changed>200 and probe['outsideChangedPixels']==0,
    testedPackageSha256=probe['testedPackageSha256'], actualNativeFinalWindow=True,
    sameFrame=True,crop=list(crop),previewScale=2,treePatch=list(tree_patch),iorChangedTreePixels=changed,
    geometricDisplacementMeasured=False,unbentFeatureRetainedFractionMeasured=False,
    evidenceScope='同帧完整滤镜链IOR响应与像素原样树干对照；不把反射引起的颜色变化当位移测量',
    capturedEveryGameTicks=3,originalVideoFps=20,slowGifFactor=3,
    inputImages={f'native-ior-{value}.png':hashlib.sha256((output/f'native-ior-{value}.png').read_bytes()).hexdigest() for value in ('1.0','1.5')})
(output/'tree-evidence.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
assert report['passed'], report
print(json.dumps(report, ensure_ascii=True))
