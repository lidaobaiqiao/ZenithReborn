"""制作平滑玻璃分层数据候选并检查原贴图，不覆盖生产素材。"""
import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
GENERATED = Path.home() / '.codex/generated_images/01a17375-107d-7712-8a45-4d83d4317a24'
SOURCES = {
    'brush': 'exec-6c7c624d-fde9-4df5-bcef-e2fc94158089.png',
    'height': 'exec-86d4eeb3-bfc1-4ea7-abbc-01a327b4a6c0.png',
    'color': 'exec-4d75c0ec-88e2-4d66-bb84-8cc9dbe9af2c.png',
    'emission': 'exec-a7904de9-88ec-42d9-8ae2-9f6797286f8e.png',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def periodic_lowpass(a, cutoff):
    # 用周期边界的频域低通去除像素级结构，所有空间变化由带限频率表达。
    fy = np.fft.fftfreq(a.shape[0])[:, None] * a.shape[0]
    fx = np.fft.fftfreq(a.shape[1])[None, :] * a.shape[1]
    gain = np.exp(-((fx / cutoff) ** 2 + (fy / cutoff) ** 2) ** 2)
    return np.fft.ifft2(np.fft.fft2(a) * gain).real


def scalar_source(name, size, cutoff, center, amplitude):
    # 生图只提供视觉标量原稿；随后按明确的数据范围、采样尺度处理。
    a = np.asarray(Image.open(GENERATED / SOURCES[name]).convert('L').resize((size, size), Image.Resampling.LANCZOS), dtype=float) / 255
    # 先去掉首尾线性偏差，再做周期低通，避免把不相接的原稿直接平铺。
    a -= np.linspace(-.5, .5, size)[None, :] * (a[:, -1] - a[:, 0])[:, None]
    a -= np.linspace(-.5, .5, size)[:, None] * (a[-1, :] - a[0, :])[None, :]
    a = periodic_lowpass(a, cutoff)
    a -= a.mean()
    return np.clip(center + amplitude * a / max(abs(a).max(), 1e-8), 0, 1)


def image_bytes(a):
    return np.uint8(np.clip(np.rint(a * 255), 0, 255))


def save_scalar(path, a):
    Image.fromarray(image_bytes(a)).save(path)


def metrics(path, normal=False):
    im = Image.open(path)
    a = np.asarray(im, dtype=float)
    report = dict(size=list(im.size), mode=im.mode, sha256=sha(path), minimum=float(a.min()), maximum=float(a.max()),
                  adjacentMeanU=float(abs(np.diff(a, axis=1)).mean()), adjacentMeanV=float(abs(np.diff(a, axis=0)).mean()),
                  wrapMeanU=float(abs(a[:, 0] - a[:, -1]).mean()), wrapMeanV=float(abs(a[0] - a[-1]).mean()))
    if normal:
        n = a[..., :3] / 255 * 2 - 1
        unit = n / np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-8)
        report.update(unitLengthMaxError=float(abs(np.linalg.norm(n, axis=2) - 1).max()),
                      adjacentAngleP99=float(np.percentile(np.degrees(np.arccos(np.clip((unit[:, 1:] * unit[:, :-1]).sum(2), -1, 1))), 99)),
                      minimumZ=float(n[..., 2].min()))
    return report


def prepare(output, size=1024):
    output.mkdir(parents=True, exist_ok=True)
    inputs = output / 'Input'
    inputs.mkdir(exist_ok=True)
    raw = output / 'Raw'
    raw.mkdir(exist_ok=True)
    for name, file in SOURCES.items():
        shutil.copy2(GENERATED / file, raw / (name + '.png'))
    brush = scalar_source('brush', size, 22, .52, .15)
    height = scalar_source('height', size, 9, .5, .025)
    color = scalar_source('color', size, 8, .85, .06)
    emission = scalar_source('emission', size, 28, .055, .13)
    # 法线从同源浮点高度求导，先求导再量化，避免8位高度阶梯被放大成法线方块。
    fx = np.fft.fftfreq(size)[None, :] * size
    fy = np.fft.fftfreq(size)[:, None] * size
    spectrum = np.fft.fft2(height)
    du = np.fft.ifft2(spectrum * (2j * np.pi * fx)).real
    dv = np.fft.ifft2(spectrum * (2j * np.pi * fy)).real
    normals = np.stack((-du * .18, -dv * .18, np.ones_like(height)), axis=2)
    normals /= np.linalg.norm(normals, axis=2, keepdims=True)
    # 平滑周期噪声直接在目标分辨率求值；不放大小格子，不对比度拉满。
    rng = np.random.default_rng(20261025)
    u, v = np.meshgrid(np.arange(size) / size, np.arange(size) / size)
    noise = np.zeros((size, size))
    for _ in range(28):
        ku, kv = rng.integers(-7, 8, 2)
        if ku == kv == 0:
            continue
        noise += np.cos(2 * np.pi * (ku * u + kv * v) + rng.uniform(0, 2 * np.pi)) / (1 + (ku * ku + kv * kv) ** .7)
    noise = .5 + .22 * noise / abs(noise).max()
    roughness = .68 + (height - .5) * 1.8
    for name, a in [('brush', brush), ('height', height), ('color', color), ('emission', emission), ('noise', noise), ('roughness', roughness)]:
        save_scalar(inputs / (name + '.png'), a)
    Image.fromarray(image_bytes(normals * .5 + .5)).save(inputs / 'normal.png')
    # 通用材质合同：R金属度、G粗糙度、B环境遮蔽；默认剑流的表面图只读R，不能混用。
    surface = np.stack((np.zeros_like(height), roughness, np.ones_like(height)), axis=2)
    Image.fromarray(image_bytes(surface)).save(inputs / 'surface.png')
    # 四个权重层共享同一UV，不携带剑弧轮廓；V=0为刃，V=1为内侧。
    edge = np.exp(-(v / .09) ** 2)
    body = np.exp(-((v - .33) / .22) ** 2)
    dark = np.exp(-((v - .76) / .24) ** 2)
    soft = np.exp(-((v - .16 - .018 * np.sin(2 * np.pi * u)) / .12) ** 2)
    channels = [edge, body, dark, soft]
    for name, channel in zip(('edge', 'body', 'dark', 'glow'), channels):
        save_scalar(output / ('region-' + name + '.png'), channel)
    rgba = image_bytes(np.stack(channels, axis=2))
    Image.fromarray(rgba).save(output / 'regions-data-inspection.png')
    (inputs / 'regions.zrdata').write_bytes(b'ZRDT' + struct.pack('<III', 1, size, size) + rgba.tobytes())
    material = json.loads((ROOT / 'VfxLibrary/Materials/Surface/Glass/material.json').read_text(encoding='utf-8-sig'))
    material.update(Name='平滑玻璃分层R1·制作候选', UseWeaponColor=True, Metallic=0, Roughness=.14, Ior=1.5,
                    Transmission=.88, Thickness=72, NormalStrength=.65, BumpStrength=0, Displacement=0,
                    EnvironmentStrength=0, LightStrength=1.4, EmissionColor=dict(X=1, Y=1, Z=1), EmissionStrength=.10)
    material['Assets'].update(ColorField='color.png', MotionMask='', NormalMap='normal.png', SurfaceMap='surface.png',
                              HeightMap='height.png', EmissionMap='emission.png', EnvironmentMap='', Brush='brush.png',
                              Noise='noise.png', LayerMask='regions.zrdata', Frames=1, Columns=1)
    def layer(weight, rough=0, glow=0):
        return dict(Weight=weight, Blend=1, ScaleU=1, ScaleV=.8, ScrollU=.055, ScrollV=0,
                    Tint=dict(X=1, Y=1, Z=1), Threshold=0, RoughnessDelta=rough, MetallicDelta=0, EmissionStrength=glow)
    material['DetailLayers'] = [layer(.8, -.045, .4), layer(.28, -.018, .12), layer(.15, .025, .025), layer(.24, -.012, .12)]
    (output / 'material.json').write_text(json.dumps(material, ensure_ascii=False, indent=2), encoding='utf8')
    # 只做数据审计与联系表，不将数据检查冒充GPU验收。
    old = ROOT / 'ZenithReborn/Assets/Vfx/Materials/SwordFlow'
    report = dict(status='offline-candidate-awaiting-gpu', productionModified=False, sourceVersion=(ROOT / 'ZenithReborn/build.txt').read_text(encoding='utf8'),
                  original={name: metrics(old / name, name == 'NormalMap.png') for name in ('NoiseSeamless.png', 'NormalMap.png')},
                  candidate={p.name: metrics(p, p.name == 'normal.png') for p in sorted(inputs.glob('*.png'))},
                  channels=dict(surface='R=0金属度/G=粗糙度/B=1环境遮蔽', normal='RGB单位法线，G正向V；从浮点高度导数生成',
                                regions='RGBA=亮刃/主体/内侧/柔光；Alpha为数据，直接上传zrdata'),
                  limitations=['带限周期纹理仍有8位量化，不能承诺完全没有可见像素或所有姿态无伪影。',
                               '当前默认着色器不等于通用Surface材质；本候选仅供现有通用材质核心制作档读取。',
                               '区域沿V分工，V不循环；笔刷/噪声/高度/色场才采用周期平铺。'])
    (output / 'preparation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    names = ['brush', 'noise', 'height', 'normal', 'roughness', 'surface', 'color', 'emission']
    sheet = Image.new('RGB', (1024, 584), (22, 25, 31))
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        x, y = i % 4 * 256, i // 4 * 292
        im = Image.open(inputs / (name + '.png')).convert('RGB').resize((256, 256), Image.Resampling.LANCZOS)
        sheet.paste(im, (x, y + 32))
        draw.text((x + 8, y + 7), name, fill='white')
    sheet.save(output / 'layer-inputs.png')
    print(json.dumps(dict(output=str(output), original=report['original'], candidate={name: report['candidate'][name] for name in ('noise.png', 'normal.png')}), ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/smooth-glass-layers-r1')
    args = parser.parse_args()
    prepare(args.output)
