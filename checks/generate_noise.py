"""生成可平铺的无缝噪声，供 ZRSwordFlow 的域扭曲使用。

纯 Python（无 numpy）：先造周期性值噪声网格，再做 4 倍频叠加（fBm）。
因为网格坐标对格数取模，所以左右、上下都能无缝接上。
"""
import math
import random
import struct
import zlib
from pathlib import Path

SIZE = 512
OCTAVES = [(4, 0.50), (8, 0.26), (16, 0.15), (32, 0.09)]
SEED = 20261024
OUT = Path(__file__).resolve().parent.parent / 'ZenithReborn/Assets/Vfx/Materials/SwordFlow/NoiseSeamless.png'


def make_octave(cells, rng):
    grid = [[rng.random() for _ in range(cells)] for _ in range(cells)]
    # 预计算每个像素的两组插值索引与权重，避免内层重复算
    axis = []
    for i in range(SIZE):
        f = i / SIZE * cells
        i0 = int(f) % cells
        i1 = (i0 + 1) % cells
        t = f - int(f)
        t = t * t * (3 - 2 * t)          # smoothstep，避免网格线
        axis.append((i0, i1, t))
    layer = [[0.0] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        y0, y1, ty = axis[y]
        row0, row1 = grid[y0], grid[y1]
        out = layer[y]
        for x in range(SIZE):
            x0, x1, tx = axis[x]
            top = row0[x0] + (row0[x1] - row0[x0]) * tx
            bottom = row1[x0] + (row1[x1] - row1[x0]) * tx
            out[x] = top + (bottom - top) * ty
    return layer


def write_gray_png(path, size, values):
    raw = bytearray()
    for y in range(size):
        raw.append(0)                     # 每行的 filter 字节
        row = values[y]
        for x in range(size):
            v = row[x]
            raw += bytes((v, v, v, 255))  # 灰度写进 RGB，alpha 满
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 6))
           + chunk(b'IEND', b''))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def main():
    rng = random.Random(SEED)
    total = [[0.0] * SIZE for _ in range(SIZE)]
    for cells, weight in OCTAVES:
        layer = make_octave(cells, rng)
        for y in range(SIZE):
            trow, lrow = total[y], layer[y]
            for x in range(SIZE):
                trow[x] += lrow[x] * weight
        print(f'octave {cells} done')

    # 归一化到 0..255，并用直方图拉伸避免整张偏灰
    flat = [v for row in total for v in row]
    lo, hi = min(flat), max(flat)
    scale = 255.0 / max(hi - lo, 1e-6)
    pixels = [[max(0, min(255, int((v - lo) * scale))) for v in row] for row in total]
    write_gray_png(OUT, SIZE, pixels)
    print('written', OUT, OUT.stat().st_size, 'bytes')


if __name__ == '__main__':
    main()
