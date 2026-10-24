"""制作端通道打包与语义mip烘焙；输出候选文件，不改变游戏材质绑定。"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image


def save_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def data_payload(image):
    image=image.convert('RGBA')
    return b'ZRDT'+struct.pack('<III',1,image.width,image.height)+image.tobytes()


def pack(channels, output):
    if set(channels) != set('RGBA'):
        raise ValueError('必须明确指定RGBA四个通道的来源，不自动推断材质含义')
    arrays, records, size = [], {}, None
    sources = set()
    for channel in 'RGBA':
        path, source_channel = channels[channel]
        if source_channel not in 'RGBA' or len(source_channel) != 1:
            raise ValueError('来源通道必须是R/G/B/A之一')
        with Image.open(path) as source:
            image = source.convert('RGBA')
            if size is not None and size != image.size:
                raise ValueError('尺寸不同：不静默缩放会改变采样含义的贴图')
            size = image.size
            arrays.append(image.getchannel(source_channel))
        sources.add(path.resolve())
        records[channel] = dict(source=str(path.resolve()), channel=source_channel, sha256=digest(path))
    if output.resolve() in sources:
        raise ValueError('输出不能覆盖输入素材')
    output.parent.mkdir(parents=True, exist_ok=True)
    Image.merge('RGBA', arrays).save(output)
    # 重新读取结果，检查每个通道逐像素无损。
    with Image.open(output) as actual:
        if any(a.tobytes() != b.tobytes() for a, b in zip(arrays, actual.split())):
            raise ValueError('通道无损校验失败')
    report = dict(schemaVersion=1, status='offline-candidate-not-runtime-bound', colorSpace='linear-data',
                  width=size[0], height=size[1], channels=records, sha256=digest(output),
                  pngBytes=output.stat().st_size, rgba8Level0Bytes=size[0] * size[1] * 4,
                  sourcesPngBytes=sum(p.stat().st_size for p in sources),
                  note='通道意义由调用者命名；已有shader未必读取这些通道。彩色底色/法线/环境图不可当独立灰度通道无损合并。')
    save_json(output.with_suffix('.channels.json'), report)
    return report


def resize_float(values, size):
    return np.stack([np.asarray(Image.fromarray(values[:, :, i]).resize(size, Image.Resampling.BOX))
                     for i in range(values.shape[2])], axis=2)


def srgb_decode(v):
    return np.where(v <= .04045, v / 12.92, ((v + .055) / 1.055) ** 2.4)


def srgb_encode(v):
    return np.where(v <= .0031308, v * 12.92, 1.055 * np.maximum(v, 0) ** (1 / 2.4) - .055)


def downsample(values, size, kind):
    if kind == 'normal':
        n = values[:, :, :3] * 2 - 1
        n = resize_float(n, size)
        length = np.linalg.norm(n, axis=2, keepdims=True)
        n = np.where(length > 1e-6, n / np.maximum(length, 1e-6), np.array([0, 0, 1], dtype=np.float32))
        return np.concatenate((n * .5 + .5, resize_float(values[:, :, 3:], size)), axis=2)
    if kind == 'color':
        # 颜色在线性空间预乘后滤波，避免透明边缘出现黑边；输出仍为非预乘PNG。
        alpha = resize_float(values[:, :, 3:], size)
        rgb = resize_float(srgb_decode(values[:, :, :3]) * values[:, :, 3:], size)
        rgb = np.where(alpha > 1e-6, rgb / np.maximum(alpha, 1e-6), 0)
        return np.concatenate((srgb_encode(rgb), alpha), axis=2)
    return resize_float(values, size)


def bake_mips(source, output, kind, columns=1, rows=1):
    output = output.resolve()
    source = source.resolve()
    # 图集按帧分别烘焙，禁止跨帧滤波；运行端仍需按manifest逐级上传。
    with Image.open(source) as image:
        image = image.convert('RGBA')
        if not 1 <= columns <= 256 or not 1 <= rows <= 256 or image.width % columns or image.height % rows:
            raise ValueError('图集行列必须整除图片尺寸')
        fw, fh = image.width // columns, image.height // rows
        records = []
        for row in range(rows):
            for column in range(columns):
                frame = row * columns + column
                values = np.asarray(image.crop((column * fw, row * fh, (column + 1) * fw, (row + 1) * fh)), dtype=np.float32) / 255
                width, height, level = fw, fh, 0
                while True:
                    relative = Path(f'frame-{frame:03d}') / f'mip-{level:02d}.png'
                    target = output / relative
                    if target == source:
                        raise ValueError('输出不能覆盖输入素材')
                    target.parent.mkdir(parents=True, exist_ok=True)
                    Image.fromarray(np.uint8(np.clip(np.rint(values * 255), 0, 255))).save(target)
                    records.append(dict(frame=frame, level=level, file=relative.as_posix(), width=width, height=height,
                                        rgba8Bytes=width * height * 4, sha256=digest(target)))
                    if width == height == 1:
                        break
                    width, height = max(1, width // 2), max(1, height // 2)
                    values = downsample(values, (width, height), kind)
                    level += 1
    report = dict(schemaVersion=1, status='offline-candidate-not-runtime-uploaded', kind=kind,
                  source=str(source), sourceSha256=digest(source), columns=columns, rows=rows, levels=records,
                  totalRgba8Bytes=sum(r['rgba8Bytes'] for r in records),
                  limitations=['PNG文件并不自动成为GPU mip链，仍需创建LevelCount并逐级SetData。',
                               '图集按独立帧输出，生产上传和采样还需显式适配。',
                               '法线重归一化未补偿高光方差；标量均值不自动保持阈值裁剪后的覆盖率。'])
    save_json(output / 'mips.json', report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('pack')
    p.add_argument('--channel', action='append', required=True, help='例如R=surface.png:R；需明确指定四个通道')
    p.add_argument('--output', required=True, type=Path)
    p = sub.add_parser('data', help='将四通道数据图转换为直接上传的原始载荷，保留Alpha为0处的RGB')
    p.add_argument('source', type=Path)
    p.add_argument('--output', required=True, type=Path)
    p = sub.add_parser('mips')
    p.add_argument('source', type=Path)
    p.add_argument('--kind', choices=['scalar', 'normal', 'color'], required=True)
    p.add_argument('--columns', type=int, default=1)
    p.add_argument('--rows', type=int, default=1)
    p.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if args.command == 'pack':
        channels = {}
        for entry in args.channel:
            key, value = entry.split('=', 1)
            path, channel = value.rsplit(':', 1)
            if key not in 'RGBA' or len(key) != 1 or key in channels:
                raise ValueError('输出通道重复或无效')
            channels[key] = (Path(path), channel)
        result = pack(channels, args.output)
        print(json.dumps(result, ensure_ascii=False))
    elif args.command == 'mips':
        result = bake_mips(args.source, args.output, args.kind, args.columns, args.rows)
        print(json.dumps(dict(status=result['status'], frames=args.columns * args.rows,
                              levels=len(result['levels']), bytes=result['totalRgba8Bytes']), ensure_ascii=False))
    else:
        if args.source.resolve()==args.output.resolve():raise ValueError('输出不能覆盖输入')
        with Image.open(args.source) as image:payload=data_payload(image)
        args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_bytes(payload)
        print(json.dumps(dict(bytes=len(payload),sha256=digest(args.output),status='raw-rgba-ready-for-explicit-loader')))


if __name__ == '__main__':
    main()
