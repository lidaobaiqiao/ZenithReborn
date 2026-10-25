"""核对生产世界的独立短尾蒙版、资源保持，以及本轮真实点击证据。"""
import argparse
import hashlib
import json
import subprocess
from collections import deque
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg
from tmod_archive import read_archive,contents

root=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser()
parser.add_argument('--baseline',type=Path,default=Path.home()/'Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod')
args=parser.parse_args()
built=root/'build-profile/Mods/ZenithReborn.tmod'
_,_,version,entries=read_archive(built)
_,_,previous,old_entries=read_archive(args.baseline)
assert previous!=version,'基线不能是当前包本身'
sha=hashlib.sha256(built.read_bytes()).hexdigest()
packed={e['name']:contents(e) for e in entries}
old={e['name']:contents(e) for e in old_entries}
output=root/f'artifacts/unit-trails-{version}';output.mkdir(exist_ok=True)
world=root/f'artifacts/soul-flight-world-{version}'
reports=[json.loads((Path(str(world)+suffix)/'world.json').read_text(encoding='utf-8')) for suffix in ('','-surface')]
assert all(r['passed'] and r['testedPackageSha256']==sha for r in reports)
assets={p:data for p,data in packed.items() if p.startswith('Assets/')}
old_assets={p:data for p,data in old.items() if p.startswith('Assets/')}
unchanged=assets==old_assets
protected=[p for p in old if p.startswith(('Martial','Ore','HardOre','VfxSword')) and p.endswith('.cs')]
protected_ok=all(packed.get(p)==old[p] for p in protected)

def components(path):
    alpha=np.asarray(Image.open(path).convert('RGBA'))[:,:,3]
    mask=alpha>8;h,w=mask.shape;seen=np.zeros_like(mask);sizes=[]
    for y,x in np.argwhere(mask):
        if seen[y,x]:continue
        seen[y,x]=True;queue=deque([(int(y),int(x))]);size=0
        while queue:
            py,px=queue.popleft();size+=1
            for ny,nx in ((py-1,px),(py+1,px),(py,px-1),(py,px+1)):
                if 0<=ny<h and 0<=nx<w and mask[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx]=True;queue.append((ny,nx))
        if size>=80:sizes.append(size)
    return sorted(sizes,reverse=True)

regions={str(i):components(world/f'refraction-mask-{i:03}.png') for i in (24,27,30,42,48,54,60)}
mouse=json.loads((root/f'artifacts/soul-input-{version}/validation.json').read_text(encoding='utf-8'))
result=dict(passed=unchanged and protected_ok and max(map(len,regions.values()))>=3 and mouse['passed'],
            testedPackageSha256=sha,baselineVersion=previous,assetsByteIdentical=unchanged,assetEntries=len(assets),
            protectedSourceFiles=protected,protectedSourceUnchanged=protected_ok,
            separatedMaskRegions=regions,actualGpuWorld=True,actualMouseInput=True,artApproved=False,multiplayerTested=False)
(output/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
assert result['passed'],result
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
crop=(300,175,950,540)
frames=[]
for i in range(3,91,3):
    image=Image.new('RGB',(650,400),(16,20,30));image.paste(Image.open(world/f'frames/frame-{i:03}.png').convert('RGB').crop(crop),(0,35))
    ImageDraw.Draw(image).text((12,5),f'单剑生产AI / GPU · 隔离世界 · 第{i}帧',font=font,fill='white');frames.append(image)
contact=Image.new('RGB',(1300,1200))
for n,i in enumerate((27,36,42,48,54,66)):
    image=frames[i//3-1];contact.paste(image,((n%2)*650,(n//2)*400))
contact.save(output/'single-sword-contact.jpg',quality=94)
proc=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s','650x400','-r','20','-i','-',
                       '-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',str(output/'single-sword-normal.mp4')],stdin=subprocess.PIPE,stderr=subprocess.DEVNULL)
for frame in frames+frames[-1:]*12:proc.stdin.write(frame.tobytes())
proc.stdin.close();assert proc.wait()==0
slow=[]
for frame in frames:
    image=frame.copy();draw=ImageDraw.Draw(image);draw.rectangle((0,0,650,34),fill=(16,20,30))
    draw.text((12,5),'单剑生产世界帧 · 两倍慢放 · 保留原素材',font=font,fill='white');slow.append(image)
slow[0].save(output/'single-sword-slow.gif',save_all=True,append_images=slow[1:]+slow[-1:]*8,duration=100,loop=0)
print(json.dumps(result,ensure_ascii=False))
