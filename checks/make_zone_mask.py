"""按带面真实几何重画天顶剑四区遮罩，并导出原始 RGBA 数据。

为什么不用美术直接出的图：
1. 原图四通道中间调占比为 0，只有 0/255，四区之间必然是硬边；
2. 原图按满幅 0~1 画，但带面在 depth≈0.82 之后就被 shader 的软边界吃掉了；
3. tML 的 ImageIO 会清空 Alpha=0 处的 RGB，四通道数据必须绕开常规贴图加载。

这里用坐标直接构造，x 轴 = 沿剑迹的路程 s，y 轴 = 亮刃到暗侧的宽度 depth，
和 ZRSwordFlow 里的 tex2D(zoneSampler, float2(s, depth)) 完全对齐。
"""
import math
import struct
import sys
from pathlib import Path

# 同目录的纯标准库 PNG 读写，从任意工作目录运行都能导入
sys.path.insert(0, str(Path(__file__).resolve().parent))
import png_stdlib

SIZE = 1024
OUT_PNG = Path(__file__).resolve().parent.parent / 'ZenithReborn/Assets/Vfx/Materials/SwordFlow/ZoneMask.png'
OUT_DATA = OUT_PNG.with_suffix('.zrdata')

# 带面在 depth 方向的实际覆盖：软边界从 0.72 开始消失，0.90 之后彻底没有。
# 四区必须落在这一段里，超出部分画了也看不见。
VISIBLE_END = 0.88
# A 通道基底：给每个像素留一点非零值，避免任何常规贴图加载把 RGB 清成 0 的隐患
ALPHA_FLOOR = 6


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def smoothstep(e0, e1, x):
    if e0 == e1:
        return 0.0 if x < e0 else 1.0
    t = clamp((x - e0) / (e1 - e0))
    return t * t * (3.0 - 2.0 * t)


def _hash(ix, iy):
    n = (ix * 374761393 + iy * 668265263) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFFFFFF) / 0xFFFFFFFF


def value_noise(x, y):
    ix, iy = math.floor(x), math.floor(y)
    fx, fy = x - ix, y - iy
    fx = fx * fx * (3.0 - 2.0 * fx)
    fy = fy * fy * (3.0 - 2.0 * fy)
    a = _hash(ix, iy)
    b = _hash(ix + 1, iy)
    c = _hash(ix, iy + 1)
    d = _hash(ix + 1, iy + 1)
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy


def band_noise(s, cells):
    """沿 s 方向首尾相接的噪声：把 s 映射到圆周上取样，平铺时不会出接缝。"""
    angle = s * 2.0 * math.pi
    radius = cells / (2.0 * math.pi)
    return value_noise(math.cos(angle) * radius + 64.0, math.sin(angle) * radius + 64.0)


def build_channels():
    """返回四个通道的浮点权重，顺序为 刃口 R / 脊线 G / 腹部 B / 磨损 A。"""
    zones = [[0.0] * (SIZE * SIZE) for _ in range(4)]
    for y in range(SIZE):
        # depth：0 在亮刃线上，1 在最内侧
        depth = y / (SIZE - 1)
        for x in range(SIZE):
            s = x / (SIZE - 1)
            n_edge = band_noise(s, 7.0)
            n_ridge = band_noise(s + 0.37, 5.0)
            n_belly = band_noise(s + 0.71, 4.0)

            # 刃口：贴着亮刃的窄带。根部收窄、尖端略宽，形状跟着低频起伏走。
            edge_width = 0.118 + 0.026 * math.sin(s * 5.1 + 0.7) + 0.013 * math.sin(s * 13.3 + 2.1)
            edge_width += (n_edge - 0.5) * 0.030
            if s < 0.18:
                edge_width *= 0.72 + 0.28 * (s / 0.18)
            razor = smoothstep(edge_width + 0.028, edge_width - 0.028, depth)
            # 强度沿迹起伏：整条刃口一个常数会读成贴上去的红色色块
            razor *= 0.80 + 0.20 * band_noise(s + 0.11, 6.0)

            # 脊线：中段一条有厚度的带，边缘不规则，是金属的脊而不是一条直线
            ridge_center = 0.285 + 0.020 * math.sin(s * 3.7 + 1.9) + (n_ridge - 0.5) * 0.026
            ridge_half = 0.098 + 0.016 * math.sin(s * 6.3 + 0.4)
            ridge = smoothstep(ridge_half + 0.046, ridge_half - 0.046, abs(depth - ridge_center))
            ridge *= 0.86 + 0.14 * band_noise(s + 0.44, 5.0)

            # 腹部：内侧大块，上缘软，到带面消失处自然收
            belly_top = 0.405 + 0.022 * math.sin(s * 4.4 + 2.6) + (n_belly - 0.5) * 0.030
            belly = smoothstep(belly_top - 0.052, belly_top + 0.052, depth)
            belly *= smoothstep(VISIBLE_END + 0.07, VISIBLE_END - 0.08, depth)

            # 磨损：刃口最外一小条 + 剑尖附近的斑块。必须是断续的崩口，
            # 一整条连续的亮带会被读成"画了一条线"，不是使用痕迹。
            wear_cell = band_noise(s + 0.61, 31.0)
            wear_edge = 1.0 - smoothstep(0.006, 0.038 + wear_cell * 0.026, depth)
            wear_edge *= 0.35 + 0.65 * smoothstep(0.30, 0.75, wear_cell)
            patch_noise = band_noise(s + 0.53, 17.0)
            # 尖端崩口：用二维噪声取块，只压一个方向会变成一条竖直细线
            patch_u = math.cos(s * 2.0 * math.pi) * (13.0 / (2.0 * math.pi))
            patch_v = math.sin(s * 2.0 * math.pi) * (13.0 / (2.0 * math.pi))
            patch_value = value_noise(patch_u + 64.0, patch_v + depth * 11.0 + 64.0)
            tip_patch = smoothstep(0.64, 0.90, s) * smoothstep(0.42, 0.18, depth)
            tip_patch *= smoothstep(0.56, 0.74, patch_value)
            tip_patch *= 0.55 + 0.45 * patch_noise
            wear = max(wear_edge * 0.85, tip_patch * 0.75)
            index = y * SIZE + x
            zones[0][index] = razor
            zones[1][index] = ridge
            zones[2][index] = belly
            zones[3][index] = wear
    return zones


def main():
    zones = build_channels()
    data = bytearray(SIZE * SIZE * 4)
    mid_count = [0] * 4
    for channel in range(4):
        src = zones[channel]
        for i, v in enumerate(src):
            byte = int(round(clamp(v) * 255.0))
            if channel == 3:
                byte = min(255, byte + ALPHA_FLOOR)
            data[i * 4 + channel] = byte
            if 24 < byte < 232:
                mid_count[channel] += 1

    image = png_stdlib.Image(SIZE, SIZE, bytes(data), 'RGBA')
    image.save(OUT_PNG)

    # .zrdata：绕开 tML 的图像解码，原始 RGBA 原样上传
    payload = struct.pack('<IIii', 0x5444525A, 1, SIZE, SIZE) + bytes(data)
    OUT_DATA.write_bytes(payload)

    names = 'RGBA'
    for channel in range(4):
        ratio = mid_count[channel] * 100.0 / (SIZE * SIZE)
        print(f'{names[channel]} 通道 中间调占比 = {ratio:5.2f}%')
    print('已写出', OUT_PNG.name, '和', OUT_DATA.name, f'({len(payload)} 字节)')


if __name__ == '__main__':
    main()
