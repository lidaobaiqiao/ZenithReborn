"""整理原始79夜间隔离世界画面，只裁切排版并按游戏帧序合成视频，不调亮或调色。"""
import hashlib
import json
import subprocess
from pathlib import Path
import imageio_ffmpeg
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parent.parent
folder=root/'artifacts/soul-flight-world-0.3.79-original-full-night'
world=json.loads((folder/'world.json').read_text(encoding='utf-8-sig'))
expected='c7b88428525a192e5764de000e180be73184933a41a86bf61a3832bd1fecdad1'
assert world['passed'] and world['testedPackageSha256']==expected and world['lightPreset']=='night'
box=(850,550,1650,1050)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
canvas=Image.new('RGB',(800*3,540),(12,15,20));draw=ImageDraw.Draw(canvas)
for column,(tick,name) in enumerate([(42,'泰拉之刃'),(153,'附魔剑'),(264,'星怒')]):
    image=Image.open(folder/f'frames/frame-{tick:03}.png').convert('RGB').crop(box)
    image.save(folder/f'night-crop-{tick:03}.png')
    canvas.paste(image,(column*800,40))
    draw.text((column*800+10,7),f'原始79 · {name} · 夜间原始像素',font=font,fill='white')
canvas.save(folder/'night-three-swords.png')
files=sorted((folder/'frames').glob('frame-*.png'))
ticks=[int(p.stem.split('-')[1]) for p in files]
assert all(b-a==3 for a,b in zip(ticks,ticks[1:])),ticks
video=folder/'night-original-speed-crop.mp4'
process=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-f','rawvideo','-pix_fmt','rgb24',
    '-s','800x500','-r','20','-i','-','-an','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(video)],stdin=subprocess.PIPE)
try:
    for path in files:process.stdin.write(Image.open(path).convert('RGB').crop(box).tobytes())
finally:process.stdin.close()
assert process.wait()==0
result=dict(passed=True,originalPackageSha256=expected,night=True,crop=list(box),scaled=False,brightnessAdjusted=False,
    colorsAdjusted=False,videoFrameRate=20,gameTicksPerSecond=60,frames=len(files),videoSha256=hashlib.sha256(video.read_bytes()).hexdigest(),
    worldFixture=True,actualMouseGameplay=False,liveMultiplayer=False,formalPackageModified=False)
(folder/'evidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
