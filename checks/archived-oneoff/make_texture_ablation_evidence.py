"""按原始像素排版环境贴图消融，量化同一GPU姿态的细碎纹理；不调亮或调色。"""
import json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont,ImageFilter

root=Path(__file__).resolve().parent.parent
version=next(s.split('=',1)[1].strip() for s in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if s.startswith('version'))
folder=root/f'artifacts/soul-trail-gpu-{version}-texture-ablation'
keys=['original80','flat-height','flat-normal','flat-height-normal','flat-surface','flat-environment','soft-environment']
reference=Image.open(folder/'texture-original80-night.png')
mask=reference.getchannel('A').point(lambda value:255 if value>160 else 0).filter(ImageFilter.MinFilter(7))
interior=np.asarray(mask)>0
values={}
for key in keys:
    image=Image.open(folder/f'texture-{key}-night.png').convert('RGB')
    delta=np.asarray(image,dtype=float)-np.asarray(image.filter(ImageFilter.GaussianBlur(2)),dtype=float)
    values[key]=float(np.sqrt((delta[interior]**2).mean()))
box=reference.getbbox();box=(box[0]-5,box[1]-5,box[2]+5,box[3]+5)
width,height=box[2]-box[0],box[3]-box[1]
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
canvas=Image.new('RGB',(width*2,(height+34)*3),(12,15,20));draw=ImageDraw.Draw(canvas)
for row,(key,label) in enumerate([('original80','80原始环境图'),('flat-height-normal','只抹平高度＋法线'),('soft-environment','只替换同源平滑环境图')]):
    for column,channel in enumerate(['night','day']):
        image=Image.open(folder/f'texture-{key}-{channel}.png').convert('RGB').crop(box)
        canvas.paste(image,(column*width,row*(height+34)+34))
        draw.text((column*width+5,row*(height+34)+5),label+('／暗背景' if column==0 else '／亮背景'),font=font,fill='white')
canvas.save(folder/'texture-source-comparison.png')
result=dict(actualGpu=True,sameShader='0.3.80',sameGeometryAndLight=True,
    interiorHighPassRmsDisplayUnits=values,smoothEnvToOriginalRatio=values['soft-environment']/values['original80'],
    measurement='Gaussian sigma2 residual; alpha>160 eroded 3px; display RGB, not physical luminance',
    userVideoExactGridReproduced=False)
(folder/'texture-analysis.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
