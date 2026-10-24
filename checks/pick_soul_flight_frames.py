"""从飞剑隔离世界抓帧中挑出拖尾形态最完整的几帧，供人工比对。

只做客观统计（非背景像素数、亮像素分布的横向跨度），不改动任何产物。
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import png_stdlib


def main():
    if len(sys.argv) < 2:
        raise SystemExit('用法: python pick_soul_flight_frames.py <帧目录> [数量]')
    folder = Path(sys.argv[1])
    top = int(sys.argv[2]) if len(sys.argv) > 2 else 8
    rows = []
    for path in sorted(folder.glob('frame-*.png')):
        image = png_stdlib.read_png(str(path))
        width, height = image.width, image.height
        data = image.convert('RGB').get_flattened_data()
        lit = 0
        min_x, max_x = width, -1
        for y in range(height):
            base = y * width * 3
            for x in range(width):
                index = base + x * 3
                r, g, b = data[index], data[index + 1], data[index + 2]
                if abs(r - 12) + abs(g - 17) + abs(b - 29) > 30:
                    lit += 1
                    if x < min_x:
                        min_x = x
                    if x > max_x:
                        max_x = x
        span = (max_x - min_x) if max_x >= 0 else 0
        rows.append((lit, span, path.name))
    rows.sort(key=lambda item: (item[0], item[1]), reverse=True)
    print(f'{"帧":<16}{"非背景像素":>12}{"横向跨度":>10}')
    for lit, span, name in rows[:top]:
        print(f'{name:<16}{lit:>12}{span:>10}')
    print(f'\n共统计 {len(rows)} 帧。')


if __name__ == '__main__':
    main()
