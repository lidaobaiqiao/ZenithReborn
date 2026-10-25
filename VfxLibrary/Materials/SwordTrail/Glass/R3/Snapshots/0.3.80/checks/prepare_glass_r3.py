"""将本轮内置生图原稿转换为可复用的环境、同源高度/法线/粗糙度数据，不改旧素材。"""
import hashlib
import json
import shutil
from pathlib import Path
import numpy as np
from PIL import Image,ImageFilter

root=Path(__file__).resolve().parent.parent
generated=Path.home()/'.codex/generated_images/01a163e3-ea52-7e03-8c19-aaf79091acce'
sources={'environment':generated/'exec-031aded8-024d-47e2-a15b-61aa6b8c99e9.png',
         'height':generated/'exec-ed1f30f9-12c6-48b9-b357-fe38a6dfb42b.png'}
production=root/'ZenithReborn/Assets/Vfx/Materials/SwordFlow/GlassR3'
production.mkdir(parents=True,exist_ok=True)
authoring=root/'artifacts/vfx-textures'

def periodic(data,axis):
    data=data.copy();first=np.take(data,0,axis=axis);last=np.take(data,-1,axis=axis)
    delta=last-first
    shape=[1]*data.ndim;shape[axis]=data.shape[axis]
    ramp=np.linspace(-.5,.5,data.shape[axis]).reshape(shape)
    return data-ramp*np.expand_dims(delta,axis)

def save(name,data):
    pixels=np.clip(data*255+.5,0,255).astype(np.uint8)
    if pixels.ndim==2:pixels=np.repeat(pixels[:,:,None],3,axis=2)
    Image.fromarray(pixels).save(production/name)

env=np.asarray(Image.open(sources['environment']).convert('RGB').resize((1024,512),Image.Resampling.LANCZOS),dtype=float)/255
env=periodic(env,1)
gray=env@np.array([.2126,.7152,.0722]);env=env*.35+gray[:,:,None]*.65
save('EnvironmentMap.png',env)
height=np.asarray(Image.open(sources['height']).convert('L').resize((512,512),Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(1)),dtype=float)/255
height=periodic(periodic(height,1),0)
height=np.clip(.5+(height-height.mean())*.20,.45,.55)
dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.5
dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.5
normal=np.stack([-dx*24,-dy*24,np.ones_like(height)],axis=2)
normal/=np.linalg.norm(normal,axis=2,keepdims=True)
roughness=np.clip(.30+np.sqrt(dx*dx+dy*dy)*120,.26,.62)
save('HeightMap.png',height);save('NormalMap.png',normal*.5+.5);save('SurfaceMap.png',roughness)
for kind,source,key in [('EnvironmentMap',sources['environment'],'EnvironmentMap.png'),('HeightMap',sources['height'],'HeightMap.png')]:
    folder=authoring/kind/'GlassR3';folder.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,folder/'01-raw.png');shutil.copy2(production/key,folder/f'02-{key}')
for kind in ('NormalMap','SurfaceMap'):
    folder=authoring/kind/'GlassR3';folder.mkdir(parents=True,exist_ok=True)
    shutil.copy2(production/f'{kind}.png',folder/f'02-{kind}.png')
manifest=dict(id='material.SwordTrail.Glass.R3',origin='builtin-imagegen-with-project-authored-data-derivation',
    sourceImages={k:dict(name=v.name,sha256=hashlib.sha256(v.read_bytes()).hexdigest()) for k,v in sources.items()},
    images={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(production.glob('*.png'))},
    channels=dict(EnvironmentMap='RGB环境反射；U环绕',HeightMap='RGB同值；中灰基准，低振幅高度',
                  NormalMap='RGB切线空间单位法线；由同一高度中心差分得到',SurfaceMap='RGB同值；粗糙度调制'),
    seamlessHeightError=float(max(np.max(np.abs(height[:,0]-height[:,-1])),np.max(np.abs(height[0]-height[-1])))),
    seamlessEnvironmentError=float(np.max(np.abs(env[:,0]-env[:,-1]))),normalUnitError=float(np.max(np.abs(np.linalg.norm(normal,axis=2)-1))),
    heightRange=[float(height.min()),float(height.max())],roughnessRange=[float(roughness.min()),float(roughness.max())],
    thirdPartyImages=False,oldInputsOverwritten=False)
(production/'provenance.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False))
