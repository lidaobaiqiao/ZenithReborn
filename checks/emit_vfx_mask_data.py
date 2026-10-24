"""把已通过 GPU 校验的 H4 蒙版复制入模组，并生成运行时几何与底色表。"""
import hashlib
import glob
import json
import os
import shutil

root = 'ZenithReborn/Assets/Vfx/SwordBody'
os.makedirs(root, exist_ok=True)

assert len(glob.glob('VfxLibrary/Masks/SwordBody/*/H4/clip.json')) == 21, '必须先完成21把H4的导出登记'
rows = []
for clip_path in sorted(glob.glob('VfxLibrary/Masks/SwordBody/*/H4/clip.json')):
    name = os.path.basename(os.path.dirname(os.path.dirname(clip_path)))
    if name == 'TitaniumSword':
        continue  # 钛金剑维持用户已选的基准，不纳入本轮批量替换
    clip = json.load(open(clip_path, encoding='utf-8'))
    src = os.path.join(os.path.dirname(clip_path), 'active.png')
    dst = os.path.join(root, name + '.png')
    assert hashlib.sha256(open(src,'rb').read()).hexdigest() == clip['atlasSha256'], name
    assert not clip.get('finalArtApproved'), name
    shutil.copyfile(src, dst)
    field_entry = json.load(open(f'VfxLibrary/ColorFields/SwordBody/{name}/entry.json', encoding='utf-8'))
    field = field_entry['variants']['H4']['recipe']
    clear = field['clear']
    p = field['layers'][0]
    s = field['layers'][1]
    rows.append(dict(
        name=name,
        tipx=round(clip['tip'][0] - clip['pivot'][0], 3),
        pivotx=clip['pivot'][0],
        clear=clear, primary=p['tint'], secondary=s['tint'],
        po=p['opacity'], so=s['opacity'],
        ps=p['scrollPixelsPerSecond'][0], ss=s['scrollPixelsPerSecond'][0],
        loop=json.load(open(os.path.join(os.path.dirname(clip_path),"authoring.json"),encoding="utf-8"))["loopSeconds"],
    ))

lines = []
lines.append('using Microsoft.Xna.Framework;')
lines.append('using Terraria.ID;')
lines.append('')
lines.append('namespace ZenithReborn;')
lines.append('')
lines.append('// 本文件由 checks/emit_vfx_mask_data.py 依据 VfxLibrary 登记自动生成。')
lines.append('// 每行对应一把矿石剑：图集名、实测剑身几何、金属色场配方。')
lines.append('// 不要手工改这个文件，改配方请改 VfxLibrary 后重新生成。')
lines.append('public static class VfxMaskData')
lines.append('{')
lines.append('    // ItemId：物品ID；TipPx：剑尖到剑柄的画布像素长；PivotX：剑柄在画布上的X坐标；')
lines.append('    // Clear：色场底色；Primary/Secondary：主色与补色；')
lines.append('    // PrimaryOpacity/SecondaryOpacity：两层不透明度；PrimaryScroll/SecondaryScroll：滚动速度。')
lines.append('    public readonly record struct Row(int ItemId,float TipPx,float PivotX,Color Clear,Color Primary,Color Secondary,float PrimaryOpacity,float SecondaryOpacity,float PrimaryScroll,float SecondaryScroll,float LoopSeconds);')
lines.append('    public static readonly Row[] Rows =')
lines.append('    {')
for r in rows:
    lines.append(f'        new(ItemID.{r["name"]},{r["tipx"]}f,{r["pivotx"]}f,'
                 f'new Color({r["clear"][0]},{r["clear"][1]},{r["clear"][2]}),'
                 f'new Color({r["primary"][0]},{r["primary"][1]},{r["primary"][2]}),'
                 f'new Color({r["secondary"][0]},{r["secondary"][1]},{r["secondary"][2]}),'
                 f'{r["po"]}f,{r["so"]}f,{r["ps"]}f,{r["ss"]}f,{r["loop"]}f),')
lines.append('    };')
lines.append('}')
open('ZenithReborn/VfxMaskData.cs', 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
print(len(rows), 'rows; atlases copied to', root)
