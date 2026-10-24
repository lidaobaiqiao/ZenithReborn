"""检查制作工具的关键语义：通道保真、线性色彩、法线长度与动画帧隔离。"""
import json
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

from prepare_material_maps import bake_mips, pack, data_payload


def main():
    checks = []
    with tempfile.TemporaryDirectory(prefix='zenith-material-') as temporary:
        root = Path(temporary)
        # 独立随机通道模拟四种毫不相关的材质信息，不能把它们预乘成颜色。
        rng = np.random.default_rng(731)
        source = rng.integers(0, 256, (8, 16, 4), dtype=np.uint8)
        paths = []
        for i in range(4):
            path = root / f'scalar-{i}.png'
            Image.fromarray(source[:, :, i]).save(path)
            paths.append(path)
        report = pack({c: (paths[i], 'R') for i, c in enumerate('RGBA')}, root / 'packed.png')
        actual = np.asarray(Image.open(root / 'packed.png'))
        assert np.array_equal(source, actual)
        checks.append('四种无关数据经PNG保存重读逐像素无损')
        payload=data_payload(Image.fromarray(source))
        assert payload[:4]==b'ZRDT' and payload[16:]==source.tobytes()
        checks.append('原始数据载荷的四通道与源图逐字节一致')
        assert report['rgba8Level0Bytes'] == source.size
        checks.append('显存预算按RGBA像素数计算，不按PNG大小推算')
        Image.new('L', (4, 4)).save(root / 'wrong-size.png')
        try:
            pack({c: (paths[i] if i else root / 'wrong-size.png', 'R') for i, c in enumerate('RGBA')}, root / 'invalid.png')
            raise AssertionError('尺寸不一致没有被拒绝')
        except ValueError:
            checks.append('拒绝静默缩放不同尺寸输入')
        try:
            pack({c: (paths[i], 'R') for i, c in enumerate('RGBA')}, paths[0])
            raise AssertionError('允许覆盖输入')
        except ValueError:
            checks.append('拒绝覆盖源素材')
        # 黑白色彩平均应约188；直接在sRGB字节取均值会得到128。
        color = np.array([[[0, 0, 0, 255], [255, 255, 255, 255]]], dtype=np.uint8)
        Image.fromarray(color).save(root / 'black-white.png')
        bake_mips(root / 'black-white.png', root / 'color', 'color')
        pixel = np.asarray(Image.open(root / 'color/frame-000/mip-01.png'))[0, 0]
        assert all(187 <= int(c) <= 189 for c in pixel[:3])
        checks.append('sRGB在线性空间平均，黑白得到约188而非128')
        # 不透明红色旁的透明蓝色不应给边缘染蓝。
        Image.fromarray(np.array([[[255, 0, 0, 255], [0, 0, 255, 0]]], dtype=np.uint8)).save(root / 'alpha.png')
        bake_mips(root / 'alpha.png', root / 'alpha', 'color')
        pixel = np.asarray(Image.open(root / 'alpha/frame-000/mip-01.png'))[0, 0]
        assert pixel[0] == 255 and pixel[1] == pixel[2] == 0 and 127 <= pixel[3] <= 128
        checks.append('透明颜色边缘以预乘形式滤波，无透明像素染边')
        normals = np.array([[[255, 128, 128, 255], [128, 255, 128, 255]]], dtype=np.uint8)
        Image.fromarray(normals).save(root / 'normal.png')
        bake_mips(root / 'normal.png', root / 'normal', 'normal')
        vector = np.asarray(Image.open(root / 'normal/frame-000/mip-01.png'))[0, 0, :3].astype(float) / 255 * 2 - 1
        assert abs(np.linalg.norm(vector) - 1) < .01
        checks.append('法线低级mip重归一化，无长度塌缩')
        atlas = Image.new('RGBA', (16, 8), (255, 0, 0, 255))
        atlas.paste(Image.new('RGBA', (8, 8), (0, 255, 0, 255)), (8, 0))
        atlas.save(root / 'atlas.png')
        mips = bake_mips(root / 'atlas.png', root / 'atlas', 'scalar', 2, 1)
        for entry in mips['levels']:
            expected = (255, 0, 0, 255) if entry['frame'] == 0 else (0, 255, 0, 255)
            im = Image.open(root / 'atlas' / entry['file'])
            assert np.all(np.asarray(im) == np.array(expected))
        checks.append('两帧图集逐级到1像素仍无跨帧串色')
        assert len(mips['levels']) == 8
        checks.append('每帧8到1像素的mip链均完整')
    output = Path(__file__).resolve().parent.parent / 'artifacts/mod-vfx-research-20261024/preparation-check.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(dict(passed=True, checks=checks, count=len(checks),
                                      scope='offline-data-semantics', gpuTested=False,
                                      runtimeMipsInstalled=False), ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'PASS: {len(checks)} meaningful material preparation checks')


if __name__ == '__main__':
    main()
