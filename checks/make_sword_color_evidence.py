"""从真实隔离世界帧制作多剑配色原速预览，另保留固定姿态材质对照。"""
import json
import argparse
import subprocess
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg

root=Path(__file__).resolve().parent.parent
version=next(line.split('=',1)[1].strip() for line in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
parser=argparse.ArgumentParser();parser.add_argument('--baseline',default='0.3.82');baseline=parser.parse_args().baseline
output=root/f'artifacts/soul-flight-world-{version}'
world=json.loads((output/'world.json').read_text(encoding='utf-8'))
assert world['passed']
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
frames=[]
for tick in range(15,328,3):
    source=Image.open(output/f'frames/frame-{tick:03}.png').convert('RGB').crop((300,160,980,540))
    frame=Image.new('RGB',(680,418),(22,25,31));frame.paste(source,(0,38))
    name='泰拉之刃' if tick<130 else '附魔剑' if tick<240 else '星怒'
    ImageDraw.Draw(frame).text((10,8),f'隔离世界AI夹具 · 原速20fps采样 · {name}',font=font,fill='white')
    frames.append(frame)
proc=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s','680x418','-r','20','-i','-',
    '-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(output/'three-swords-original-speed.mp4')],stdin=subprocess.PIPE,stderr=subprocess.DEVNULL)
for frame in frames:proc.stdin.write(frame.tobytes())
proc.stdin.close();assert proc.wait()==0
frames[0].save(output/'three-swords-original-speed.gif',save_all=True,append_images=frames[1:],duration=50,loop=0,optimize=False)
gpu=root/f'artifacts/soul-trail-gpu-{version}'
contact=Image.new('RGB',(960,930),(22,25,31))
for row,(index,name) in enumerate([(0,'泰拉之刃'),(1,'附魔剑'),(2,'星怒')]):
    for column,v in enumerate((baseline,version)):
        im=Image.open(root/f'artifacts/soul-trail-gpu-{v}/palette-{index}.png').convert('RGB').crop((180,100,800,480)).resize((480,270))
        contact.paste(im,(column*480,row*310+40))
        ImageDraw.Draw(contact).text((column*480+12,row*310+8),f'{v} · {name}',font=font,fill='white')
contact.save(gpu/'three-swords-before-after.png')
print('已导出三剑原速世界预览与同姿态材质对照；未调曝光、饱和度或游戏图像颜色。')
