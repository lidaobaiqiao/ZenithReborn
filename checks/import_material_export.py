"""核对实验室导出物，再按实际剑名接入材质库；默认只列出操作，--apply才写入源码。"""
import argparse
import hashlib
import json
import math
import re
import shutil
import struct
from datetime import datetime
from pathlib import Path
from PIL import Image
from prepare_material_maps import data_payload

root=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('export',type=Path)
parser.add_argument('--weapon',required=True)
parser.add_argument('--apply',action='store_true')
parser.add_argument('--source-root',type=Path,default=root/'ZenithReborn')
args=parser.parse_args()
assert re.fullmatch(r'[A-Z][A-Za-z0-9_]{1,80}',args.weapon),'请使用原版实际剑名，例如TerraBlade'
source=args.export.resolve();target=args.source_root.resolve()
material=json.loads((source/'material.json').read_text(encoding='utf-8-sig'))
manifest=json.loads((source/'manifest.json').read_text(encoding='utf-8-sig'))
controls=json.loads((root/'VfxLibrary/Materials/Surface/schema-controls.json').read_text(encoding='utf-8-sig'))
assert material['SchemaVersion']==1 and manifest['schemaVersion']==1 and manifest['scope']=='developer-authoring-only'
allowed={'SchemaVersion','Name','UseWeaponColor','Assets','DetailLayers'}|{c['Key'].split('.')[0] for c in controls}
assert set(material)==allowed and isinstance(material['Name'],str) and material['Name'].strip() and isinstance(material['UseWeaponColor'],bool),'材质字段与正式合同不一致'
for control in controls:
    parts=control['Key'].split('.')
    value=material[parts[0]] if len(parts)==1 else material[parts[0]][parts[1]]
    assert isinstance(value,(int,float)) and math.isfinite(value) and control['Min']<=value<=control['Max'],control['Key']
assets=material['Assets'];assert isinstance(assets['Frames'],int) and 1<=assets['Columns']<=assets['Frames']<=256 and assets['LoopSeconds']>0
layers=material['DetailLayers'];assert isinstance(layers,list) and len(layers)<=4
layer_ranges={'Weight':(0,1),'Blend':(0,1),'Threshold':(0,1),'ScaleU':(.01,128),'ScaleV':(.01,128),
              'ScrollU':(-8,8),'ScrollV':(-8,8),'RoughnessDelta':(-1,1),'MetallicDelta':(-1,1),'EmissionStrength':(0,8)}
for layer in layers:
    assert set(layer)==set(layer_ranges)|{'Tint'},'细节层字段不匹配'
    for key,(low,high) in layer_ranges.items():
        value=layer[key];assert isinstance(value,(float,int)) and math.isfinite(value) and low<=value<=high,key
    assert set(layer['Tint'])=={'X','Y','Z'} and all(isinstance(v,(float,int)) and math.isfinite(v) and 0<=v<=4 for v in layer['Tint'].values())
assert manifest['kind']=='ZenithReborn.SurfaceMaterial'
declared={r['slot']:r for r in manifest['resources']}
categories={'ColorField':'ColorFields','MotionMask':'Masks','NormalMap':'NormalMaps','SurfaceMap':'SurfaceMaps',
            'HeightMap':'HeightMaps','EmissionMap':'EmissionMaps','EnvironmentMap':'EnvironmentMaps',
            'Brush':'Brushes','Noise':'NoiseMaps','LayerMask':'LayerMasks'}
assert set(assets)==set(categories)|{'Frames','Columns','LoopSeconds'},'贴图字段与正式合同不一致'
material_root=Path('Assets/Vfx/Materials/Surface')/args.weapon
copies=[];library=[];payloads={}
for slot,category in categories.items():
    path=assets[slot]
    if not path:continue
    relative=Path(path)
    assert not relative.is_absolute() and '..' not in relative.parts and ':' not in path
    file=(source/relative).resolve();assert file.is_relative_to(source) and file.suffix.lower() in (('.png','.zrdata') if slot=='LayerMask' else ('.png',))
    assert slot in declared and declared[slot]['file']==path
    assert hashlib.sha256(file.read_bytes()).hexdigest()==declared[slot]['sha256'],slot
    if file.suffix.lower()=='.zrdata':
        data=file.read_bytes();assert data[:4]==b'ZRDT' and len(data)>=16
        version,width,height=struct.unpack('<III',data[4:16]);assert version==1 and 1<=width<=8192 and 1<=height<=8192 and len(data)==16+width*height*4
    else:
        with Image.open(file) as image:image.verify()
        with Image.open(file) as image:
            assert 1<=image.width<=8192 and 1<=image.height<=8192
            if slot=='MotionMask':assert image.width%assets['Columns']==0 and image.height%math.ceil(assets['Frames']/assets['Columns'])==0
            if slot=='LayerMask':data=data_payload(image)
    # RGBA区域数据直接上传字节，保证Alpha为0处的RGB不会被图像解码或构建处理。
    destination=material_root/(relative.with_suffix('.zrdata') if slot=='LayerMask' else relative)
    if slot=='LayerMask':payloads[target/destination]=data
    copies.append((file,target/destination));assets[slot]='ZenithReborn/'+(destination if slot=='LayerMask' else destination.with_suffix('')).as_posix()
    library.append((file,root/'VfxLibrary'/category/'SwordTrail'/args.weapon/relative))
binding_path=target/'Assets/Vfx/Materials/SwordFlow/bindings.json'
bindings=json.loads(binding_path.read_text(encoding='utf-8')) if binding_path.exists() else {'schemaVersion':1,'weapons':{}}
bindings['weapons'].setdefault(args.weapon,{})['SurfaceMaterial']=(material_root/'material.json').as_posix()
print(json.dumps(dict(apply=args.apply,weapon=args.weapon,sourceRoot=str(target),copies=[str(b) for a,b in copies],
    material=str(target/material_root/'material.json'),bindings=str(binding_path)),ensure_ascii=True,indent=2))
if not args.apply:raise SystemExit(0)
backup=root/'artifacts/material-import-backups'/datetime.now().strftime('%Y%m%d-%H%M%S-%f')
def save(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    if path.exists():
        previous=backup/path.relative_to(target) if path.is_relative_to(target) else backup/'library'/path.relative_to(root/'VfxLibrary')
        previous.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,previous)
    pending=path.with_suffix(path.suffix+'.pending');pending.write_bytes(data);pending.replace(path)
for a,b in copies:save(b,payloads[b] if b in payloads else a.read_bytes())
save(target/material_root/'material.json',json.dumps(material,ensure_ascii=False,indent=2).encode('utf-8'))
save(binding_path,json.dumps(bindings,ensure_ascii=False,indent=2).encode('utf-8'))
if target==root/'ZenithReborn':
    for a,b in library:save(b,a.read_bytes())
    entry=root/'VfxLibrary/Materials/Surface'/args.weapon/'entry.json'
    save(entry,json.dumps(dict(id='material.Surface.'+args.weapon,origin='author-export',
        definition=(target/material_root/'material.json').relative_to(root).as_posix(),
        inputs=[b.relative_to(root).as_posix() for a,b in library],status='awaiting-game-review'),ensure_ascii=False,indent=2).encode('utf-8'))
print('材质已接入源码；需重新构建并验证，脚本没有安装正式包。')
