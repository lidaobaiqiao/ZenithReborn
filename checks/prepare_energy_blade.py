"""核验用户交付的八张图，制作法线、表面图和四区数据；只生成制作候选。"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image
from prepare_material_maps import data_payload

ROOT=Path(__file__).resolve().parent.parent
NAMES=['brush','noise','height','roughness','region_edge','region_body','region_dark','region_filament']


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(source,output):
    source=source.resolve();output=output.resolve()
    if source==output or source.is_relative_to(output):raise ValueError('输出目录不能覆盖原始交付')
    inputs=output/'Input';inputs.mkdir(parents=True,exist_ok=True)
    images={};records=[]
    for name in NAMES:
        path=source/(name+'.png')
        with Image.open(path) as im:
            if im.size!=(512,512) or im.mode!='L':raise ValueError('需要512×512灰度原图：'+name)
            images[name]=im.copy()
        a=np.asarray(images[name],dtype=np.float32)
        records.append(dict(file=path.name,sha256=sha(path),size=[512,512],mode='L',minimum=int(a.min()),maximum=int(a.max()),
                            seamU=float(abs(a[:,0]-a[:,-1]).mean()),seamV=float(abs(a[0]-a[-1]).mean())))
    for name in ['brush','noise','height','roughness']:shutil.copy2(source/(name+'.png'),inputs/(name+'.png'))
    # 按UV尺度求导：U环绕，V边界保持；绿色分量正向沿V。
    h=np.asarray(images['height'],dtype=np.float32)/255
    du=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*.5*512
    dv=np.gradient(h,axis=0)*512
    n=np.stack((-du*.035,-dv*.035,np.ones_like(h)),axis=2)
    n/=np.linalg.norm(n,axis=2,keepdims=True)
    normal=np.uint8(np.clip(np.rint((n*.5+.5)*255),0,255))
    Image.fromarray(normal).save(inputs/'normal.png')
    # 通用材质现有合同：R金属=0、G粗糙度、B AO=1；不把玻璃误设成金属。
    Image.merge('RGB',(Image.new('L',(512,512),0),images['roughness'],Image.new('L',(512,512),255))).save(inputs/'surface.png')
    regions=Image.merge('RGBA',tuple(images[name] for name in ['region_edge','region_body','region_dark','region_filament']))
    regions.save(output/'regions-inspection.png');(inputs/'regions.zrdata').write_bytes(data_payload(regions))
    material=json.loads((ROOT/'VfxLibrary/Materials/Surface/Glass/material.json').read_text(encoding='utf-8-sig'))
    material.update(Name='能量薄刃A1·泰拉之刃审核',UseWeaponColor=True,Metallic=0,Roughness=.24,Ior=1.5,
                    Transmission=.55,Thickness=72,NormalStrength=.65,BumpStrength=0,Displacement=0)
    material['Assets'].update(ColorField='',MotionMask='',NormalMap='normal.png',SurfaceMap='surface.png',HeightMap='height.png',
                              EmissionMap='',EnvironmentMap='environment.png',Brush='brush.png',Noise='noise.png',LayerMask='regions.zrdata',Frames=1,Columns=1)
    # 区域只修改本片表面的属性；底色沿用武器配色，四层不重复绘制形状。
    def layer(**kw):
        d=dict(Weight=0,Blend=1,ScaleU=1,ScaleV=.8,ScrollU=.06,ScrollV=0,Tint=dict(X=1,Y=1,Z=1),
               Threshold=0,RoughnessDelta=0,MetallicDelta=0,EmissionStrength=0);d.update(kw);return d
    material['DetailLayers']=[layer(Weight=.22,RoughnessDelta=-.035),layer(Weight=.25),
        layer(Weight=.28,RoughnessDelta=.08,Tint=dict(X=.85,Y=.9,Z=.95)),
        layer(Weight=.4,Blend=0,EmissionStrength=.65)]
    (output/'material.json').write_text(json.dumps(material,ensure_ascii=False,indent=2),encoding='utf8')
    report=dict(schemaVersion=1,status='review-candidate',origin='user-supplied-via-Doubao',source=str(source),inputs=records,
                derivedNormal=dict(method='UV finite difference; U wrap, V clamped',heightScale=.035,minimumZ=float(n[:,:,2].min())),
                roughnessRange=[.24*int(np.asarray(images['roughness']).min())/255,.24*int(np.asarray(images['roughness']).max())/255],
                originalFilesModified=False,productionBindingsChanged=False,mipsUploaded=False,
                notes=['交付中的_brush-raw.png不在本次八张验收范围。','区域图用于属性，不代替动态形状。',
                       '笔刷只沿U滚动，V取样限制在0～0.8，避免纵向重复接缝。',
                       '薄刃仍使用现有程序慢底色与动态形状；本轮不替换轮廓。'])
    (output/'preparation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    return report


def register(source,output):
    evidence=json.loads((output/'gpu/artwork.json').read_text(encoding='utf8'))
    if not evidence['passed']:raise ValueError('先完成实际GPU素材检查再登记')
    library=ROOT/'VfxLibrary';refs={}
    files=[('Brush',source/'brush.png','Brushes/SwordTrail/_Shared/EnergyBladeA1'),
           ('Noise',source/'noise.png','NoiseMaps/SwordTrail/_Shared/EnergyBladeA1'),
           ('HeightMap',source/'height.png','HeightMaps/SwordTrail/_Shared/EnergyBladeA1'),
           ('RoughnessSource',source/'roughness.png','SurfaceMaps/SwordTrail/TerraBlade/EnergyBladeA1'),
           ('NormalMap',output/'Input/normal.png','NormalMaps/SwordTrail/_Shared/EnergyBladeA1'),
           ('SurfaceMap',output/'Input/surface.png','SurfaceMaps/SwordTrail/TerraBlade/EnergyBladeA1'),
           ('LayerMask',output/'Input/regions.zrdata','LayerMasks/SwordTrail/TerraBlade/EnergyBladeA1')]
    files += [(name,source/(name+'.png'),'LayerMasks/SwordTrail/TerraBlade/EnergyBladeA1')
              for name in NAMES if name.startswith('region_')]
    for slot,path,directory in files:
        target=library/directory/path.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
        refs[slot]=dict(file=target.relative_to(ROOT).as_posix(),sha256=sha(target))
    target=library/'Materials/Surface/TerraBlade/EnergyBladeA1';target.mkdir(parents=True,exist_ok=True)
    shutil.copy2(output/'material.json',target/'authoring.material.json')
    (target/'entry.json').write_text(json.dumps(dict(id='material.Surface.TerraBlade.EnergyBladeA1',
        status='review-candidate-gpu-validated',origin='project-recipe-with-user-supplied-Doubao-artwork',
        assetReferences=refs,productionBound=False,usesWeaponTint=True,
        evidence='artifacts/energy-blade-a1/gpu/artwork.json',testedPackageSha256=evidence['testedPackageSha256'],
        authoringPaths='authoring.material.json的素材名对应独立项目Input目录，不是正式模组资源路径'),ensure_ascii=False,indent=2),encoding='utf8')
    return target/'entry.json'


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--source',type=Path,default=ROOT/'artifacts/vfx-textures/energy-blade')
    p.add_argument('--output',type=Path,default=ROOT/'artifacts/energy-blade-a1');p.add_argument('--register-library',action='store_true');a=p.parse_args();r=prepare(a.source,a.output)
    if a.register_library:print(register(a.source,a.output))
    print(json.dumps(dict(status=r['status'],files=len(r['inputs']),normalMinimumZ=r['derivedNormal']['minimumZ'],roughnessRange=r['roughnessRange']),ensure_ascii=False))
