"""把原始 GPU 帧整理成可观看的动图，保持原始画面尺寸。"""
import argparse
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('frames', type=Path)
parser.add_argument('output', type=Path)
parser.add_argument('--step', type=int, default=3)
parser.add_argument('--fps', type=int, default=60)
args = parser.parse_args()
paths = sorted(args.frames.glob('frame-*.png'))[::args.step]
assert paths, '没有可用的原始帧'
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert('RGB').quantize(colors=256))
args.output.parent.mkdir(parents=True, exist_ok=True)
frames[0].save(args.output, save_all=True, append_images=frames[1:],
               duration=round(1000 * args.step / args.fps), loop=0, disposal=2)
print(f'{args.output}: {len(frames)}帧，{frames[0].size}，未经放大')
