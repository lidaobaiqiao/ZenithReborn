"""重做天顶专属色场与磨损图。

两张原图的硬伤：
- ZenithField 是纯一维竖直渐变，沿剑迹以外的方向没有任何起伏，读起来像贴了一条色带；
- ZenithWear 整体偏暗（中位 130），崩口和底色对比不足，当磨损系数用时整片都会读成半磨损。

色场按"暗—亮—中"三段沿剑迹铺开，叠低频起伏；磨损用接近无磨损的亮底，
只在上面压纵向拉丝与零星崩口，方向顺着刃走（沿 x，也就是沿剑迹）。
"""
import math
import random
import sys
from pathlib import Path

# 同目录的纯标准库 PNG 读写
sys.path.insert(0, str(Path(__file__).resolve().parent))
import png_stdlib

SIZE = 1024
MATERIALS = Path(__file__).resolve().parent.parent / 'ZenithReborn/Assets/Vfx/Materials/SwordFlow'
SEED = 20261024


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


def write_gray(values, path):
    """按灰度写 RGB PNG：三通道同值，alpha 满，避免任何预乘清色。"""
    data = bytearray(SIZE * SIZE * 3)
    for i, v in enumerate(values):
        byte = int(round(clamp(v) * 255.0))
        data[i * 3] = data[i * 3 + 1] = data[i * 3 + 2] = byte
    png_stdlib.Image(SIZE, SIZE, bytes(data), 'RGB').save(path)


def build_field():
    """暗（冷）— 亮（能量核心）— 中（暖）三段，沿剑迹方向铺开。"""
    values = [0.0] * (SIZE * SIZE)
    for y in range(SIZE):
        depth = y / (SIZE - 1)
        for x in range(SIZE):
            s = x / (SIZE - 1)
            low = value_noise(s * 3.0 + 11.0, depth * 2.0 + 7.0)
            mid = value_noise(s * 7.0 + 41.0, depth * 4.0 + 23.0)
            # 段边界跟着低频噪声漂，不然三段会像三根柱子
            t = clamp(s + (low - 0.5) * 0.055)
            # 亮段只占中间一段，峰值压到 0.76：再高会把带面中段直接烧白
            base = 0.30
            base += (0.76 - base) * smoothstep(0.08, 0.42, t)
            base -= (0.76 - 0.52) * smoothstep(0.58, 0.88, t)
            base += (low - 0.5) * 0.11 + (mid - 0.5) * 0.05
            values[y * SIZE + x] = base
    return values


def build_wear():
    """亮底 + 顺刃拉丝 + 零星崩口。亮底表示"没有磨损"。"""
    values = [0.86] * (SIZE * SIZE)
    rng = random.Random(SEED)

    # 纵向拉丝：沿 x 方向的长条，横向只占 1~3 像素；
    # 带面在 depth 方向只有约 30 像素，条数太多会挤成一片灰。
    for _ in range(70):
        y0 = rng.randrange(SIZE)
        x0 = rng.randrange(SIZE)
        length = rng.randint(80, 620)
        half_width = rng.randint(1, 3)
        strength = rng.uniform(0.05, 0.15)
        drift = rng.uniform(-0.004, 0.004)
        for k in range(length):
            x = (x0 + k) % SIZE
            centre = y0 + drift * k
            y_base = int(round(centre)) % SIZE
            for w in range(-half_width, half_width + 1):
                y = (y_base + w) % SIZE
                falloff = 1.0 - abs(w) / (half_width + 1.0)
                index = y * SIZE + x
                candidate = values[index] - strength * falloff
                if candidate < values[index]:
                    values[index] = candidate

    # 崩口：零星的软边暗斑，横向略长，像刃上的小缺口
    for _ in range(45):
        cx = rng.randrange(SIZE)
        cy = rng.randrange(SIZE)
        radius_x = rng.randint(4, 11)
        radius_y = rng.randint(2, 4)
        depth = rng.uniform(0.30, 0.52)
        for dy in range(-radius_y * 2, radius_y * 2 + 1):
            for dx in range(-radius_x * 2, radius_x * 2 + 1):
                fx = dx / (radius_x * 2.0)
                fy = dy / (radius_y * 2.0)
                distance = math.sqrt(fx * fx + fy * fy)
                if distance >= 1.0:
                    continue
                weight = (1.0 - distance) ** 2
                index = ((cy + dy) % SIZE) * SIZE + ((cx + dx) % SIZE)
                candidate = values[index] - depth * weight
                if candidate < values[index]:
                    values[index] = candidate

    return values


def main():
    field = build_field()
    write_gray(field, MATERIALS / 'ZenithField.png')
    spread = (min(field), sum(field) / len(field), max(field))
    print(f'ZenithField  最小={spread[0]:.2f} 均值={spread[1]:.2f} 最大={spread[2]:.2f}')

    wear = build_wear()
    write_gray(wear, MATERIALS / 'ZenithWear.png')
    values = sorted(wear)
    print(f'ZenithWear   最小={values[0]:.2f} 中位={values[len(values) // 2]:.2f} 最大={values[-1]:.2f}')


if __name__ == '__main__':
    main()
