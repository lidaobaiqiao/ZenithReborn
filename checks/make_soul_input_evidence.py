"""核对真实鼠标输入记录，并将实际世界帧导出为原速视频与标明慢放的预览。"""
import hashlib
import argparse
import json
import re
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

root=Path(__file__).resolve().parent.parent
version=next(s.split('=',1)[1].strip() for s in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if s.startswith('version'))
output=root/f'artifacts/soul-input-{version}'
parser=argparse.ArgumentParser();parser.add_argument('--full',action='store_true');args=parser.parse_args()
if args.full:output=output.with_name(output.name+'-full')
record=json.loads((output/'input.json').read_text(encoding='utf-8'))
trace=record['trace']
sha=hashlib.sha256((root/'build-profile/Mods/ZenithReborn.tmod').read_bytes()).hexdigest()
mods=sorted(set(re.findall(r'(ZenithReborn|HEROsMod|ImproveGame|SilkyUIFramework|UpgradableZenith|ZenithPrototypeCheck|ModLoader) \(',
                          (output/'stdout.log').read_text(encoding='utf-8',errors='replace'))))
released=any(t['mouseLeft'] is False and t['itemAnimation']>0 for t in trace)
after_parent=any(not t['blades'] and t['endings']>0 for t in trace)
passed=(record['testedPackageSha256']==sha and trace[0]['mouseLeft'] is True and released and after_parent
        and trace[-1]['endings']==0 and not trace[-1]['blades'] and trace[-1]['created']>=2)
result=dict(passed=passed,testedPackageSha256=sha,actualMouseAttack=True,mouseReleasedDuringAttack=released,
            endingOutlivedParent=after_parent,remainingEndings=trace[-1]['endings'],endingsCreated=trace[-1]['created'],
            loadedMods=mods,isolatedSaves=True,fixtureModEnabled=True,naturalGameplay=False,liveMultiplayer=False,
            video='actual-click-60fps.mp4',slowPreview='actual-click-slow.gif')
result['playbackScope']='逐游戏帧读取后按60次更新/秒重放，采集会拖慢客户端，不是实时帧率证明'
formal_mods=json.loads((Path.home()/'Documents/My Games/Terraria/tModLoader/Mods/enabled.json').read_text(encoding='utf-8-sig'))
result['fullFormalModSet']=set(formal_mods)<=set(mods)
(output/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
assert passed,result
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
crop=(300,175,950,540)
if args.full:
    width,height=Image.open(output/'frames/frame-000.png').size
    crop=(width//2-620,height//2-350,width//2+460,height//2+300)
last=78
frames=[]
for i in range(last):
    src=Image.open(output/f'frames/frame-{i:03}.png').convert('RGB').crop(crop).resize((650,365),Image.Resampling.LANCZOS)
    frame=Image.new('RGB',(650,400),(16,20,30));frame.paste(src,(0,35))
    ImageDraw.Draw(frame).text((12,5),f'实际鼠标触发 · 隔离档逐游戏帧重放 · {i}',font=font,fill='white')
    frames.append(frame)
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
proc=subprocess.Popen([ffmpeg,'-y','-f','rawvideo','-pix_fmt','rgb24','-s','650x400','-r','60','-i','-',
                       '-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(output/'actual-click-60fps.mp4')],stdin=subprocess.PIPE,stderr=subprocess.DEVNULL)
for frame in frames+frames[-1:]*30:proc.stdin.write(frame.tobytes())
proc.stdin.close();assert proc.wait()==0
slow=[]
for i,frame in enumerate(frames):
    frame=frame.copy();draw=ImageDraw.Draw(frame);draw.rectangle((0,0,650,34),fill=(16,20,30))
    draw.text((12,5),f'实际世界帧 · 约2.4倍慢放 · 第{i}帧',font=font,fill='white');slow.append(frame)
slow[0].save(output/'actual-click-slow.gif',save_all=True,append_images=slow[1:]+slow[-1:]*12,duration=40,loop=0)
contact=Image.new('RGB',(650*3,400*2))
for k,i in enumerate([24,36,48,54,60,66]):contact.paste(frames[i],((k%3)*650,(k//3)*400))
contact.save(output/'ending-contact.jpg',quality=94)
print(json.dumps(result,ensure_ascii=False))
