"""Assemble actual offscreen GPU frames into a slowed preview; no synthetic frames."""
import argparse
import json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('folder', type=Path)
args = parser.parse_args()
paths = sorted((args.folder / 'frames').glob('slash-*.png'))
report = json.loads((args.folder / 'slash-gpu.json').read_text(encoding='utf-8'))
expected = report.get('previewFrameCount', 38)
if len(paths) != expected:
    raise ValueError(f'Expected {expected} actual GPU frames, got {len(paths)}')
frames = [Image.open(path).convert('RGB').resize((640, 480), Image.Resampling.LANCZOS) for path in paths]
# A shared palette avoids flickering colors between frames.
palette_source = Image.new('RGB', (640 * 3, 480))
for i, frame in enumerate((frames[5], frames[11], frames[16])):
    palette_source.paste(frame, (640 * i, 0))
palette = palette_source.quantize(colors=256)
indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
output = args.folder / 'slash-preview.gif'
indexed[0].save(output, save_all=True, append_images=indexed[1:], duration=[30] * (len(indexed)-1) + [400], loop=0, disposal=2, optimize=False)
print(output.resolve())
