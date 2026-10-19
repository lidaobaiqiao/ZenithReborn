"""CPU preview of the actual C# vertex mesh, not a game/GPU capture."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
vertices = np.asarray(json.loads((root / 'artifacts/soul-ribbon-preview-mesh.json').read_text()), dtype=float)
scale = 2
width, height = 960, 500
canvas = np.empty((height * scale, width * scale, 3), dtype=float)
canvas[:] = (14, 21, 25)

for tri in vertices.reshape(-1, 3, 6):
    positions = tri[:, :2] * scale
    low = np.maximum(np.floor(positions.min(axis=0)).astype(int), (0, 0))
    high = np.minimum(np.ceil(positions.max(axis=0)).astype(int), (width * scale - 1, height * scale - 1))
    if np.any(high < low):
        continue
    (ax, ay), (bx, by), (cx, cy) = positions
    denominator = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(denominator) < 1e-8:
        continue
    yy, xx = np.mgrid[low[1]:high[1] + 1, low[0]:high[0] + 1]
    xx, yy = xx + .5, yy + .5
    a = ((by - cy) * (xx - cx) + (cx - bx) * (yy - cy)) / denominator
    b = ((cy - ay) * (xx - cx) + (ax - cx) * (yy - cy)) / denominator
    c = 1 - a - b
    inside = (a >= 0) & (b >= 0) & (c >= 0)
    # C# Color values are already premultiplied for AlphaBlend.
    rgba = a[..., None] * tri[0, 2:] + b[..., None] * tri[1, 2:] + c[..., None] * tri[2, 2:]
    target = canvas[low[1]:high[1] + 1, low[0]:high[0] + 1]
    blended = rgba[..., :3] + target * (1 - rgba[..., 3:4] / 255)
    target[inside] = blended[inside]

image = Image.fromarray(np.clip(canvas, 0, 255).astype('uint8')).resize((width, height), Image.Resampling.LANCZOS)
draw = ImageDraw.Draw(image)
font_path = Path('C:/Windows/Fonts/msyh.ttc')
font = ImageFont.truetype(str(font_path), 20)
small = ImageFont.truetype(str(font_path), 15)
draw.text((35, 27), '弧面拖尾 · 实际网格离线预览', font=font, fill=(215, 226, 229))
draw.text((35, 62), '仅展示形状与渐变，不是游戏截图；未展示武器贴图、残影和粒子。', font=small, fill=(140, 158, 165))
draw.line((800, 128, 800, 181), fill=(225, 238, 224), width=3)
draw.line((791, 177, 809, 177), fill=(185, 160, 100), width=2)
draw.line((800, 178, 800, 190), fill=(185, 160, 100), width=3)
draw.text((815, 153), '剑身位置', font=small, fill=(177, 194, 184))
draw.text((35, 450), '亮边贴着剑尖轨迹，向内变柔和；尾端同步收细、淡出。', font=small, fill=(140, 158, 165))
out = root / 'artifacts/soul-ribbon-preview.png'
image.save(out)
print(out)
