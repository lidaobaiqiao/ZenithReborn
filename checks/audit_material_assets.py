"""审计工作区两条材质路径、源码/安装包身份与贴图预算，不启动或修改游戏。"""
import argparse
import hashlib
import json
import re
import zipfile
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return path.read_text(encoding='utf-8-sig')


def strip_comments(text):
    return re.sub(r'/\*.*?\*/|//[^\n]*', '', text, flags=re.S)


def parameter_links(root, renderer, shader):
    code = strip_comments(read(root / renderer))
    effect = strip_comments(read(root / shader))
    # 这是声明/提交的静态比较；优化器是否移除参数仍需检查编译Effect。
    declared = set(re.findall(r'^\s*(?:float\d?(?:x\d)?|texture(?:2D)?|matrix)\s+(\w+)', effect, re.M))
    submitted = set(re.findall(r'(?:parameters|p)\["([^"]+)"\]\??\.SetValue', code))
    return dict(renderer=renderer, shader=shader, rendererSha256=sha(root / renderer), shaderSha256=sha(root / shader),
                submittedButNotDeclared=sorted(submitted - declared),
                detailUniformsDeclared=all(name in declared for name in ['uDetailA', 'uDetailB', 'uDetailColor']),
                detailUniformsSubmitted=all(name in submitted for name in ['uDetailA', 'uDetailB', 'uDetailColor']),
                note='未解析辅助函数、反射、继承或动态参数名；此列表是待核对线索，不等于GPU执行结果。')


def audit(root):
    folder = root / 'ZenithReborn/Assets/Vfx/Materials/SwordFlow'
    images = []
    for path in sorted(folder.glob('*.png')):
        with Image.open(path) as im:
            width, height = im.size
            mode = im.mode
            rgba = im.convert('RGBA')
            levels = []
            w, h = width, height
            while True:
                levels.append(dict(width=w, height=h, rgba8Bytes=w * h * 4))
                if w == h == 1:
                    break
                w, h = max(1, w // 2), max(1, h // 2)
            images.append(dict(file=path.relative_to(root).as_posix(), width=width, height=height, mode=mode,
                               pngBytes=path.stat().st_size, sha256=sha(path), rgba8Level0Bytes=width * height * 4,
                               rgba8MipChainBytes=sum(x['rgba8Bytes'] for x in levels), mipLevelCount=len(levels),
                               channelExtrema=dict(zip('RGBA', rgba.getextrema()))))
    install_path = root / 'artifacts/install-result.json'
    install = json.loads(read(install_path)) if install_path.exists() else {}
    archive = root / 'artifacts' / install.get('source_archive', 'missing.zip')
    comparisons = []
    if archive.exists():
        with zipfile.ZipFile(archive) as z:
            for name in ['SurfaceMaterialDefinition.cs', 'SurfaceMaterialRenderer.cs', 'SwordFlowMaterial.cs',
                         'Assets/Effects/ZRSwordFlow.fx', 'Assets/Effects/ZRMaterialSurface.fx']:
                local = root / 'ZenithReborn' / name
                key = 'ZenithReborn/' + name
                if key not in z.namelist():
                    key = name
                old = z.read(key)
                comparisons.append(dict(file=local.relative_to(root).as_posix(), currentSha256=sha(local),
                                        installedSourceSha256=hashlib.sha256(old).hexdigest(), same=old == local.read_bytes()))
    target = Path(install.get('target', 'missing.tmod'))
    bindings = json.loads(read(folder / 'bindings.json'))
    pipeline = [parameter_links(root, 'ZenithReborn/SwordFlowMaterial.cs', 'ZenithReborn/Assets/Effects/ZRSwordFlow.fx'),
                parameter_links(root, 'ZenithReborn/SurfaceMaterialRenderer.cs', 'ZenithReborn/Assets/Effects/ZRMaterialSurface.fx')]
    ore = list((root / 'ZenithReborn/Assets/Vfx/SwordBody').glob('*.png'))
    return dict(schemaVersion=1, observedAtUtc=datetime.now(timezone.utc).isoformat(), scope='static-authoring-audit', images=images,
                pngBytes=sum(x['pngBytes'] for x in images),
                rgba8Level0Bytes=sum(x['rgba8Level0Bytes'] for x in images),
                rgba8MipChainBytes=sum(x['rgba8MipChainBytes'] for x in images),
                budgetNote='RGBA8为假定每张图均加载一次的预算，不是运行时显存测量；PNG大小不等于显存。',
                advancedWeaponBindingCount=len(bindings['weapons']),
                oreBodyMaskFiles=len(ore), oreBodyMaskUniqueHashes=len({sha(p) for p in ore}),
                pipeline=pipeline, sourceComparedToInstall=comparisons,
                installedVersion=install.get('version'), installedPackageSha256=sha(target) if target.exists() else None,
                installedMatchesReceipt=target.exists() and sha(target) == install.get('sha256'),
                findings=[
                    '通用ZRMaterialSurface的SurfaceMap使用R金属/G粗糙度/B AO；默认ZRSwordFlow的SurfaceMap仅R调粗糙度，不能交换解释。',
                    '细节层合同看pipeline字段；声明/提交匹配仍需结合实际GPU区域对照，不能只看参数名称。',
                    'MotionMask决定整体形状，材质区域遮罩决定局部属性；不能用后者多画几遍轮廓。',
                    'LabResources.FromStream未生成mip。创建mip资源、填全各级和启用采样过滤是三个环节。',
                    'PNG灰度模式不是运行时单通道格式的证据。mip会增加约三分之一显存，不是省显存手段。',
                    '新增未绑定素材和默认分支硬编码加载应分开计数；21套矿石蒙版不属于同一张通用表面图。'],
                limitations=['未启动GPU或世界，未认定新源码具有已安装0.3.65的验收结果。',
                             '词法审计不能替代编译、参数反射、调用链和实际A/B。'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    report = audit(args.root.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: report[k] for k in ['scope', 'pngBytes', 'rgba8Level0Bytes', 'rgba8MipChainBytes',
                                           'advancedWeaponBindingCount', 'oreBodyMaskFiles', 'oreBodyMaskUniqueHashes',
                                           'installedVersion', 'installedMatchesReceipt']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
