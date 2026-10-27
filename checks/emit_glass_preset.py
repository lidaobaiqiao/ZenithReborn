"""把制作端材质预设编入常量；游戏不加载制作工具或显示调参面板。"""
import json
import math
from pathlib import Path

root=Path(__file__).resolve().parent.parent
source=root/'VfxLibrary/Materials/SwordTrail/Glass/preset.json'
data=json.loads(source.read_text(encoding='utf-8'))
values=data['parameters']
bounds={'Ior':(1,2.5),'Roughness':(0,1),'Metallic':(0,1),'Transmission':(0,1),'Opacity':(0,1),'Thickness':(0,128),
        'Dispersion':(0,1),'SsrStrength':(0,2),'EmissionStrength':(0,2),'StripeStrength':(0,2),
        'SpaceWarpStrength':(0,24),'SpaceWarpRadius':(8,64),'SpaceWarpSpeed':(0,4)}
assert data['scope']=='developer-authoring-only' and set(values)==set(bounds)
lines=['namespace ZenithReborn;','','// 由 checks/emit_glass_preset.py 从制作端预设生成，不提供玩家调参入口。',
       'public static class GlassMaterialPreset','{']
for key,(low,high) in bounds.items():
    value=float(values[key])
    assert math.isfinite(value) and low<=value<=high,(key,value)
    lines.append(f'    public const float {key}={value:g}f;')
lines.append('}')
(root/'ZenithReborn/GlassMaterialPreset.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('制作端玻璃预设已生成；没有玩家配置项。')
