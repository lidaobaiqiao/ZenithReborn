"""整理透明主体/有色高光候选的真实昼夜画面，核对包和姿态，不后期调色。"""
import hashlib
import json
import subprocess
from pathlib import Path
import imageio_ffmpeg
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parent.parent
artifacts=root/'artifacts'
output=artifacts/'clear-highlight-0.3.91';output.mkdir(exist_ok=True)
digest=hashlib.sha256((root/'build-profile/Mods/ZenithReborn.tmod').read_bytes()).hexdigest()
def report(path):
    data=json.loads((artifacts/path).read_text(encoding='utf-8-sig'))
    assert data['passed'] and data['testedPackageSha256']==digest,path
    return data
gpu=report('soul-trail-gpu-0.3.91/gpu.json')
night=report('soul-flight-world-0.3.91-full-night/world.json')
day=report('soul-flight-world-0.3.91-full/world.json')
for suffix in ['full-night','full']:
    native=report(f'soul-flight-world-0.3.91-{suffix}/native-probe.json')
    assert native['outsideChangedPixels']==0 and native['actualNativeFilter']
old=json.loads((artifacts/'soul-flight-world-0.3.79-original-full-night/world.json').read_text(encoding='utf-8-sig'))
assert old['passed'] and old['testedPackageSha256']=='c7b88428525a192e5764de000e180be73184933a41a86bf61a3832bd1fecdad1'
poses={(p['tick'],p['type']):p for p in old['poses']}
matched=0;maximum=0
for pose in night['poses']:
    key=(pose['tick'],pose['type'])
    if key in poses:
        matched+=1
        maximum=max(maximum,max(abs(pose[field]-poses[key][field]) for field in ['x','y','rotation','phase']))
assert matched>100 and maximum==0
box=(850,550,1650,1050);font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
canvas=Image.new('RGB',(1600,1064),(12,15,20));draw=ImageDraw.Draw(canvas)
for row,(folder,label) in enumerate([('soul-flight-world-0.3.79-original-full-night','原始79／夜间'),('soul-flight-world-0.3.91-full-night','候选91／透明主体＋有色高光／夜间')]):
    image=Image.open(artifacts/folder/'frames/frame-042.png').convert('RGB').crop(box)
    canvas.paste(image,(0,row*532+32));draw.text((10,row*532+4),label,font=font,fill='white')
for row,(tick,label) in enumerate([(153,'候选91／附魔剑／夜间'),(264,'候选91／星怒／夜间')]):
    image=Image.open(artifacts/'soul-flight-world-0.3.91-full-night'/f'frames/frame-{tick:03}.png').convert('RGB').crop(box)
    canvas.paste(image,(800,row*532+32));draw.text((810,row*532+4),label,font=font,fill='white')
canvas.save(output/'night-79-candidate.png')
Image.open(artifacts/'soul-flight-world-0.3.91-full/frames/frame-042.png').convert('RGB').crop(box).save(output/'day-candidate.png')
for suffix in ['full-night','full']:
    frames=sorted((artifacts/f'soul-flight-world-0.3.91-{suffix}/frames').glob('frame-*.png'))
    ticks=[int(p.stem.split('-')[1]) for p in frames]
    assert all(b-a==3 for a,b in zip(ticks,ticks[1:]))
    path=output/('night-original-speed.mp4' if suffix.endswith('night') else 'day-original-speed.mp4')
    process=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-f','rawvideo','-pix_fmt','rgb24','-s','800x500','-r','20','-i','-',
        '-an','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(path)],stdin=subprocess.PIPE)
    try:
        for frame in frames:process.stdin.write(Image.open(frame).convert('RGB').crop(box).tobytes())
    finally:process.stdin.close()
    assert process.wait()==0
result=dict(passed=True,testedPackageSha256=digest,original79PoseRecordsMatched=matched,maximumPoseDifference=maximum,
    crop=list(box),scaled=False,brightnessAdjusted=False,colorsAdjusted=False,clearHighlight=gpu['clearHighlight'],
    actualDayAndNightWorldFixture=True,originalPalette79=True,newTexturesAdded=False,
    unresolvedVisuals=['部分姿态可见扇形分段和剑体重复轮廓，尚未认定来源或修复'],
    actualMouseGameplayTested=False,liveMultiplayerTested=False,artApproved=False,formalPackageModified=False)
(output/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
