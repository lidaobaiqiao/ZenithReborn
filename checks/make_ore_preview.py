"""Assemble real GPU ore frames; no generated/interpolated frames."""
import argparse
import json
import subprocess
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('folder', type=Path)
parser.add_argument('--ffmpeg', type=Path, help='Optional existing encoder for a true 60-fps MP4 preview')
args = parser.parse_args()
report = json.loads((args.folder/'ore-gpu.json').read_text(encoding='utf-8'))
assert report['passed']
for name, count_key in [('ore-volley','volleyFrames'), ('ore-skyfall','skyfallFrames'), ('ore-burning','burningFrames')]:
    if count_key not in report:
        continue
    paths = sorted((args.folder/name).glob('ore-*.png'))
    assert len(paths) == report[count_key]
    frames = [Image.open(p).convert('RGB').resize((640,480),Image.Resampling.LANCZOS) for p in paths]
    palette = frames[len(frames)//2].quantize(colors=256)
    indexed = [f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    indexed[0].save(args.folder/(name+'.gif'),save_all=True,append_images=indexed[1:],
        duration=[10 if i%3==0 else 20 for i in range(len(indexed)-1)]+[300],loop=0,disposal=2,optimize=False)
    print(name+'.gif')
    if args.ffmpeg:
        subprocess.run([str(args.ffmpeg),'-y','-framerate','60','-start_number','0',
            '-i',str(args.folder/name/'ore-%03d.png'),'-an','-c:v','libx264','-preset','fast',
            '-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(args.folder/(name+'.mp4'))],
            check=True,capture_output=True)
        print(name+'.mp4 (60 fps, original GPU frames)')
