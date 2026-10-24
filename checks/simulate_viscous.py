"""离线模拟粘性空腔挥砍：完全复刻 ViscousSlash 的算法（累积 + 每帧回填）。

不依赖 numpy/PIL：读 png_stdlib 的软轮廓贴图，自己编码 PNG，最后交给 ffmpeg 合成动图。
坐标约定与游戏一致：武器柄在画布中心偏下，剑身朝外，角度按挥动扫过。
"""
import math
import os
import struct
import sys
import zlib

sys.path.insert(0, 'checks')
import png_stdlib

SRC = 'artifacts/swordbody-CopperShortsword-H2-emissive-local/softblade.png'
OUT = 'artifacts/viscous-sim'
W, H = 360, 240
DECAY = 0.955         # 每帧回填比例（帧数变多，单帧衰减相应放缓）
SLASH_ALPHA = 0.16    # 单帧轮廓强度：20 帧叠加也不饱和，才不会在柄部烧成一团
SWING = 56            # 挥动帧数：帧数太少会让剑尖的扫掠边缘出现齿轮状台阶
TOTAL = 96            # 总帧数（含之后的回填）
SCALE = 4.2           # 武器显示倍率

os.makedirs(OUT, exist_ok=True)

img = png_stdlib.read_png(SRC)
SW, SH, src = img.width, img.height, img.data


def src_alpha(sx, sy):
    # 双线性采样：放大后边缘才不会出现像素台阶
    x0, y0 = int(math.floor(sx)), int(math.floor(sy))
    fx, fy = sx - x0, sy - y0
    total = 0.0
    for dy in (0, 1):
        for dx in (0, 1):
            xx, yy = x0 + dx, y0 + dy
            if 0 <= xx < SW and 0 <= yy < SH:
                total += src[(yy * SW + xx) * 4 + 3] * (fx if dx else 1 - fx) * (fy if dy else 1 - fy)
    return total


def write_png(path, w, h, rgba):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw += rgba[y * w * 4:(y + 1) * w * 4]
    def chunk(tag, data):
        c = struct.pack('>I', len(data)) + tag + data
        return c + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) + chunk(b'IDAT', zlib.compress(bytes(raw), 6)) + chunk(b'IEND', b'')
    open(path, 'wb').write(png)


# 空腔缓冲：只存 alpha（形状），颜色在输出时统一上淡青
cavity = bytearray(W * H)

cx, cy = W * 0.50, H * 0.66      # 武器柄（画布中心偏下）

for frame in range(TOTAL):
    # ① 回填：所有已有空腔按比例变淡（数值上等价于 ViscousSlash 的 alpha 衰减）
    for i in range(len(cavity)):
        a = cavity[i]
        if a:
            cavity[i] = int(a * DECAY)

    # ② 仍在挥动：把当前武器轮廓累加进去 = 扫掠体积
    if frame < SWING:
        t = frame / (SWING - 1)
        # 从右下扫到左上，约 170 度，像一次有力的横斩
        angle = math.radians(-100 + 150 * t)
        # 手部沿一条小弧移动（模拟手臂摆动），不固定在一点
        hand_x = cx + 26 * math.cos(angle * 0.55 + 1.1)
        hand_y = cy + 18 * math.sin(angle * 0.55 + 1.1)
        ca, sa = math.cos(-angle), math.sin(-angle)
        reach = int(SH * SCALE) + 8
        x0, x1 = max(0, int(hand_x) - reach), min(W, int(hand_x) + reach)
        y0, y1 = max(0, int(hand_y) - reach), min(H, int(hand_y) + reach)
        for y in range(y0, y1):
            dy = (y - hand_y) / SCALE
            for x in range(x0, x1):
                dx = (x - hand_x) / SCALE
                # 反向旋转到贴图坐标系
                # 旋转中心放在剑柄（贴图左下角附近），武器是握在柄上挥的；
                # 用贴图中心当枢轴会扫出蝴蝶形，那是错的
                sx = dx * ca - dy * sa + SW * 0.22
                sy = dx * sa + dy * ca + SH * 0.78
                # 2x2 超采样：源贴图只有 32×32，放大后不加这个就会出现明显台阶
                a = 0.0
                for sy2 in (-0.25, 0.25):
                    for sx2 in (-0.25, 0.25):
                        a += src_alpha(sx + sx2 / SCALE, sy + sy2 / SCALE)
                a *= 0.25
                if a > 0.05:
                    # 局部排开量 ∝ 局部速度 v = ω·r：柄部几乎不动所以几乎不留痕，
                    # 剑尖速度最快、留下的空腔最明显
                    r = math.hypot(dx, dy) / (SH * 0.5)
                    weight = 0.02 + 0.98 * min(1.0, r)
                    i = y * W + x
                    v = cavity[i] + int(a * SLASH_ALPHA * weight)
                    cavity[i] = 255 if v > 255 else v

    # ③ 输出：深色背景 + 淡青胶体
    out = bytearray(W * H * 4)
    for i in range(W * H):
        a = cavity[i]
        if a == 0:
            # 夜空底色
            out[i * 4] = 13
            out[i * 4 + 1] = 18
            out[i * 4 + 2] = 32
            out[i * 4 + 3] = 255
            continue
        f = a / 255.0
        # 胶体颜色：淡青白，alpha 随空腔浓度
        out[i * 4] = int(13 + (196 - 13) * f)
        out[i * 4 + 1] = int(18 + (236 - 18) * f)
        out[i * 4 + 2] = int(32 + (245 - 32) * f)
        out[i * 4 + 3] = 255
    write_png(f'{OUT}/f-{frame:03}.png', W, H, bytes(out))

print('frames written', TOTAL)
