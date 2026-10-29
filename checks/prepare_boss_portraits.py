"""把官方 Wiki 下载的 GIF 保持原帧时序转换为游戏可读的透明图集。"""
from pathlib import Path
import hashlib
import json
import math
import shutil
from PIL import Image, ImageSequence, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'ZenithReborn/Assets/UI/BossPortraits'
EVIDENCE = ROOT / 'artifacts/boss-unlocks-0.3.109'
DOWNLOADS = Path.home() / 'Documents/Downloads'
SOURCES = [
    ('Eye', '克苏鲁之眼', 'Eye_of_Cthulhu.gif', 'Eye_of_Cthulhu.gif'),
    ('Brain', '克苏鲁之脑', 'Brain_of_Cthulhu.gif', 'Brain_of_Cthulhu.gif'),
    ('Eater', '世界吞噬怪', 'Emote_Boss_EoW.gif', 'Emote_Boss_EoW.gif'),
    ('Skeleton', '骷髅王', 'Emote_Boss_Skeletron.gif', 'Emote_Boss_Skeletron.gif'),
    ('Cultist', '拜月教邪教徒', 'Lunatic_Cultist.gif', 'Lunatic_Cultist (1).gif'),
    ('Moon', '月亮领主', 'Moon_Lord.gif', '255px-Moon_Lord.gif'),
    ('Bee', '蜂王', 'Queen_Bee.gif', 'Queen_Bee.gif'),
    ('Flower', '世纪之花', 'Plantera.gif', 'Plantera.gif'),
    ('Temple', '石巨人', 'Golem.gif', '199px-Golem.gif'),
    ('Destroyer', '毁灭者', 'Emote_Boss_Destroyer.gif', 'Emote_Boss_Destroyer.gif'),
    ('Twins', '双子魔眼', 'The_Twins.gif', '183px-The_Twins.gif'),
    ('Prime', '机械骷髅王', 'Skeletron_Prime.gif', '185px-Skeletron_Prime.gif'),
    ('Flesh', '血肉墙', 'Wall_of_Flesh.gif', '150px-Wall_of_Flesh.gif'),
    ('Queen', '史莱姆皇后', 'Emote_Boss_Queen_Slime.gif', 'Emote_Boss_Queen_Slime.gif'),
    ('Empress', '光之女皇', 'Empress_of_Light.gif', '255px-Empress_of_Light.gif'),
]
STILLS = {'Eater': ('Eater_of_Worlds.png', '170px-Eater_of_Worlds.png'),
          'Skeleton': ('Skeletron.png', '255px-Skeletron.png'),
          'Destroyer': ('The_Destroyer.png', '255px-The_Destroyer.png'),
          'Queen': ('Queen_Slime.png', 'Queen_Slime.png')}

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    originals = EVIDENCE / 'wiki-originals'
    originals.mkdir(exist_ok=True)
    records, declarations = [], []
    sheet = Image.new('RGB', (5 * 150, 3 * 140), (16, 22, 32))
    for n, (key, name, wiki_file, local) in enumerate(SOURCES):
        source = DOWNLOADS / local
        shutil.copy2(source, originals / wiki_file)
        gif = Image.open(source)
        frames, durations = [], []
        for frame in ImageSequence.Iterator(gif):
            frames.append(frame.convert('RGBA').copy())
            durations.append(max(20, frame.info.get('duration', 100)))
        assert len(frames) > 1, key
        # 对所有帧共用包围盒和缩放比例，避免逐帧紧裁造成抖动。
        boxes = [f.getbbox() for f in frames if f.getbbox()]
        crop = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
        scale = min(88 / (crop[2] - crop[0]), 88 / (crop[3] - crop[1]))
        size = (max(1, round((crop[2] - crop[0]) * scale)), max(1, round((crop[3] - crop[1]) * scale)))
        columns = min(10, math.ceil(math.sqrt(len(frames))))
        atlas = Image.new('RGBA', (columns * 96, math.ceil(len(frames) / columns) * 96))
        for i, f in enumerate(frames):
            resized = f.crop(crop).resize(size, Image.Resampling.NEAREST)
            atlas.paste(resized, (i % columns * 96 + (96 - size[0]) // 2, i // columns * 96 + (96 - size[1]) // 2))
        atlas.save(DEST / f'{key}.png')
        cell = atlas.crop((0, 0, 96, 96))
        still = None
        if key in STILLS:
            original_name, local_name = STILLS[key]
            original = DOWNLOADS / local_name
            shutil.copy2(original, originals / original_name)
            body = Image.open(original).convert('RGBA')
            body = body.crop(body.getbbox())
            body.thumbnail((88, 88), Image.Resampling.NEAREST)
            clear = Image.new('RGBA', (96, 96))
            clear.paste(body, ((96-body.width)//2, (96-body.height)//2))
            clear.save(DEST / f'{key}Body.png')
            cell = clear
            still = dict(wikiFile=original_name, downloadedFile=local_name,
                originalSha256=hashlib.sha256(original.read_bytes()).hexdigest(),
                filePage='https://terraria.wiki.gg/wiki/File:' + original_name)
        sheet.paste(cell, (n % 5 * 150 + 27, n // 5 * 140 + 10), cell)
        ImageDraw.Draw(sheet).text((n % 5 * 150 + 20, n // 5 * 140 + 112), f'{key}: {len(frames)} frames', fill=(225, 230, 240))
        records.append(dict(key=key, name=name, wikiFile=wiki_file,
            page='https://terraria.wiki.gg/zh/wiki/' + name,
            filePage='https://terraria.wiki.gg/wiki/File:' + wiki_file,
            downloadedFile=local, originalSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            atlasSha256=hashlib.sha256((DEST / f'{key}.png').read_bytes()).hexdigest(),
            frames=len(frames), durationsMs=durations, columns=columns,
            body=still, mediaKind='static boss body with animated emote' if still else 'animated boss sprite'))
        declarations.append(f'        ["{key}"]=new("{key}",{columns},[{",".join(map(str,durations))}]),')
    manifest = dict(source='https://terraria.wiki.gg/zh/wiki/Boss',
        credit='Terraria / Re-Logic; animations hosted by the Official Terraria Wiki and its contributors.',
        rights='Game artwork remains copyrighted by Re-Logic. Wiki file pages identify game media as In Copyright; wiki prose licensing is not an artwork license.',
        conversion='Transparent 96px tiles, common crop across the loop, nearest-neighbor resizing; original GIF frame durations retained.', portraits=records)
    (DEST / 'sources.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    (ROOT / 'ZenithReborn/SoulBossPortraitCatalog.cs').write_text('''namespace ZenithReborn;

// 由 checks/prepare_boss_portraits.py 生成；只登记 Wiki 动画图集与原始帧时序。
public static partial class SoulBossPortraits
{
    public static readonly IReadOnlyDictionary<string,SoulBossAnimation> Catalog=new Dictionary<string,SoulBossAnimation>
    {
''' + '\n'.join(declarations) + '\n    };\n}\n', encoding='utf-8')
    sheet.save(EVIDENCE / 'portrait-contact-sheet.png')
    print(json.dumps([dict(key=r['key'], frames=r['frames']) for r in records]))

if __name__ == '__main__':
    main()
