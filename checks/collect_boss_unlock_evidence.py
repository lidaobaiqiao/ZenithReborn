"""汇总本轮已完成的原生构建、只读检查、GPU 样本和素材来源，不替代运行测试。"""
from pathlib import Path
import hashlib
import json
import shutil
import difflib
import zipfile
from PIL import Image, ImageChops, ImageStat, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/boss-unlocks-0.3.109'
GPU = ROOT / 'artifacts/information-ui-0.3.109/boss-release'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    baseline = json.loads((OUT / 'before-manifest.json').read_text(encoding='utf-8'))
    changed = [name for name, old in baseline.items() if sha(ROOT / name) != old]
    allowed = {'build.txt', 'description.txt', 'SoulCurses.cs', 'SoulUI.cs', 'SoulUIArchive.cs', 'SoulUIInformation.cs', 'SoulUIMemories.cs'}
    assert all(Path(p).name in allowed for p in changed), changed
    assets = [p for p in baseline if '/Assets/' in p]
    assert all(sha(ROOT / p) == baseline[p] for p in assets)
    sources = [p for p in baseline if p.endswith('.cs') and p not in changed]
    fresh = [p.relative_to(ROOT).as_posix() for p in (ROOT/'ZenithReborn').rglob('*')
             if p.is_file() and not any(x in p.parts for x in ('bin','obj')) and p.relative_to(ROOT).as_posix() not in baseline]
    audit = dict(passed=True, baseline='0.3.108', changedExistingFiles=changed, newFiles=fresh,
                 unchangedExistingSources=len(sources), unchangedExistingAssets=len(assets))
    (OUT/'source-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
    diff=[]
    with zipfile.ZipFile(ROOT/'artifacts/ZenithReborn-0.3.108-source.zip') as old:
        for name in changed + [n for n in fresh if n.endswith('.cs')]:
            before=old.read(name).decode('utf-8-sig').splitlines(True) if name in old.namelist() else []
            diff.extend(difflib.unified_diff(before,(ROOT/name).read_text(encoding='utf-8-sig').splitlines(True),fromfile='0.3.108/'+name,tofile='0.3.109/'+name))
    (OUT/'production-change.patch').write_text(''.join(diff),encoding='utf-8')
    gpu=json.loads((GPU/'gpu.json').read_text(encoding='utf-8'))
    native=json.loads((ROOT/'artifacts/native-build-check.json').read_text(encoding='utf-8-sig'))
    package=sha(ROOT/'build-profile/Mods/ZenithReborn.tmod')
    assert gpu['passed'] and gpu['testedPackageSha256']==package and native['passed']
    assert native['packages']['ZenithReborn']['sha256']==package
    assert (ROOT/'artifacts/wood-check.txt').read_text().startswith('PASS: 1182 ')
    for name in gpu['images']:
        with Image.open(GPU/name) as frame: frame.verify()
    # 同为一道铭刻，先打谁只改变左侧头像，右侧累计效果应完全一致。
    brain=Image.open(GPU/'boss-switching-brain-first.png').convert('RGB')
    cultist=Image.open(GPU/'boss-switching-cultist-first.png').convert('RGB')
    reward_box=(815,440,1380,745)
    order_diff=ImageChops.difference(brain.crop(reward_box),cultist.crop(reward_box))
    assert order_diff.getbbox() is None, '相同累计数量的奖励列不应随 Boss 顺序改变'
    # 只测头像不透明内部；把周围暗底和边框混入平均值不能代表头像自身的亮暗。
    atlas=Image.open(ROOT/'ZenithReborn/Assets/UI/BossPortraits/Brain.png').convert('RGBA')
    mask=atlas.crop((0,0,96,96)).getchannel('A').resize((73,73),Image.Resampling.NEAREST)
    mask=mask.point(lambda a:255 if a>=250 else 0).filter(ImageFilter.MinFilter(5))
    lit=ImageStat.Stat(brain.crop((551,462,624,535)),mask).mean
    dark=ImageStat.Stat(cultist.crop((551,462,624,535)),mask).mean
    assert sum(lit)>sum(dark)*1.8, '已击败头像需要可见的明亮差异'
    frames=[Image.open(GPU/f'animation/boss-switching-{i:02}.png').convert('RGB') for i in range(8)]
    portrait_hashes={hashlib.sha256(f.crop((550,465,625,535)).tobytes()).hexdigest() for f in frames}
    assert len(portrait_hashes)>1, '实际 GPU 中头像必须出现不同动画帧'
    frames[0].save(OUT/'boss-unlock-preview.gif',save_all=True,append_images=frames[1:],duration=130,loop=0)
    shutil.copy2(GPU/'boss-switching-brain-first.png',OUT/'boss-unlock-preview.png')
    pixels=dict(reverseOrderRewardsIdentical=True,litPortraitRgbMean=lit,darkPortraitRgbMean=dark,
                animationDistinctPortraitFrames=len(portrait_hashes))
    summary=dict(passed=True,tested_package_sha256=package,actualGpu=True,actualProductionUi=True,
        report='information-ui-0.3.109/boss-release/gpu.json',images=len(gpu['images']),bosses=gpu['bosses'],pixelChecks=pixels,
        readOnlySoulSnapshotUnchanged=gpu['readOnlySoulSnapshotUnchanged'],physicalMouseTested=False,multiplayerTested=False,worldCombatTested=False)
    (ROOT/'artifacts/ui-validation-0.3.109.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'gpu-validation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    for name in ['wood-check.txt','native-build-check.json']:shutil.copy2(ROOT/'artifacts'/name,OUT/name)
    print(json.dumps(dict(audit=audit,gpu=summary),ensure_ascii=False,indent=2))

if __name__=='__main__':main()
