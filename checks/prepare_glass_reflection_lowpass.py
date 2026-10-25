"""从原始80环境图的线性光能生成暗处反射输入，只去掉细碎云纹和水面纹，不覆盖原图。"""
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image

folder=Path(__file__).resolve().parent.parent/'ZenithReborn/Assets/Vfx/Materials/SwordFlow/GlassR3'
source=folder/'EnvironmentMap.png'
display=np.asarray(Image.open(source).convert('RGB'),dtype=np.float64)/255
linear=display**2.2
# 横向按全景环绕，纵向镜像延拓，避免把天顶和水底混成一条接缝。
height,width=linear.shape[:2]
extended=np.concatenate((linear,linear[::-1]),axis=0)
fy=np.fft.fftfreq(height*2)[:,None]
fx=np.fft.fftfreq(width)[None,:]
kernel=np.exp(-2*np.pi**2*((fx*32)**2+(fy*16)**2))
soft=np.fft.ifft2(np.fft.fft2(extended,axes=(0,1))*kernel[:,:,None],axes=(0,1)).real[:height]
encoded=np.clip(soft,0,1)**(1/2.2)
target=folder/'EnvironmentSoftMap.png'
Image.fromarray(np.uint8(encoded*255+.5)).save(target)
decoded=(np.asarray(Image.open(target),dtype=np.float64)/255)**2.2
def differences(data):
    return float(np.mean(np.abs(np.diff(data,axis=0)))+np.mean(np.abs(np.diff(data,axis=1))))
result=dict(source='EnvironmentMap.png',sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
    derived='EnvironmentSoftMap.png',derivedSha256=hashlib.sha256(target.read_bytes()).hexdigest(),
    method='linear-energy gaussian lowpass; horizontal periodic; vertical mirrored',sigmaPixels=[32,16],
    sourceMeanLinear=linear.mean(axis=(0,1)).tolist(),derivedMeanLinear=decoded.mean(axis=(0,1)).tolist(),
    adjacentDifferenceRatio=differences(decoded)/differences(linear),originalOverwritten=False)
(folder/'EnvironmentSoftMap.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
