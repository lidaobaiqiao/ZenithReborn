"""从 CalamityOverhaul.tmod 提取刀光特效的分层素材并转成 PNG。

只取特效层，不含武器图标/手持贴図：
  1) 斩击主体帧   Assets/Projectiles/Melee/MuraBreakerSlash.rawimg（602x4172 = 7 帧 x 602x596）
  2) 遮罩层       Assets/Masking/*.rawimg（形状、噪声、拉丝、气流、法线矩阵等）
  3) 颜色渐变条   Assets/ColorBar/*.rawimg（每把武器一条 64x5，是本模组"特效颜色跟随武器"的做法）

rawimg 格式：12 字节头（标记 1、宽、高，int32 小端）+ 宽*高*4 字节 RGBA 裸数据。
已用 MuraItem 验证通道为 RGBA（不交换 R/B）。
"""
import shutil
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import png_stdlib
from tmod_archive import read_archive, contents

OUT = Path(r"C:\Users\15537\Desktop\鬼切-特效分层贴图")
SLASH = "Assets/Projectiles/Melee/MuraBreakerSlash.rawimg"

def decode(data):
    _, w, h = struct.unpack("<3i", data[:12])
    return w, h, data[12:12 + w * h * 4]

def save(w, h, rgba, path):
    png_stdlib.Image(w, h, rgba, "RGBA").save(str(path))

def main():
    tmod = r"C:\Users\15537\Documents\My Games\Terraria\tModLoader\Mods\CalamityOverhaul.tmod"
    # 上一版混进了武器图标，整个重建，保持目录干净
    if OUT.exists():
        shutil.rmtree(OUT)
    (OUT / "1-斩击主体帧").mkdir(parents=True)
    (OUT / "2-遮罩层").mkdir(parents=True)
    (OUT / "3-颜色渐变条").mkdir(parents=True)

    loader, name, version, entries = read_archive(tmod)
    index = {e["name"]: e for e in entries}
    print(f"模组 {name} {version}")

    # 1) 斩击主体：切 7 帧，另存整条图集
    w, h, rgba = decode(contents(index[SLASH]))
    frame_h, frames = 596, h // 596
    for i in range(frames):
        s = i * frame_h * w * 4
        save(w, frame_h, rgba[s:s + frame_h * w * 4], OUT / "1-斩击主体帧" / f"帧{i + 1}_{w}x{frame_h}.png")
    print(f"斩击主体 {w}x{h} -> {frames} 帧 x {w}x{frame_h}")

    # 2) 遮罩层
    masks = [e for e in entries if e["name"].startswith("Assets/Masking/") and e["name"].endswith(".rawimg")]
    for e in sorted(masks, key=lambda x: x["name"]):
        w, h, rgba = decode(contents(e))
        save(w, h, rgba, OUT / "2-遮罩层" / f"{Path(e['name']).stem}_{w}x{h}.png")
    print(f"遮罩层 {len(masks)} 张")

    # 3) 颜色渐变条：每条 64x5，全部竖排拼成一张对照图并放大 8 倍
    bars = [e for e in entries if "/ColorBar/" in e["name"] and e["name"].endswith(".rawimg")]
    bars.sort(key=lambda x: x["name"])
    bw, bh = 64, 5
    scale = 8
    out_w, out_h = bw * scale, bh * len(bars) * scale
    rows = []
    names = []
    for e in bars:
        w, h, rgba = decode(contents(e))
        names.append(Path(e["name"]).stem)
        rows.append(rgba)
    # 逐行放大后拼接
    buf = bytearray()
    for rgba in rows:
        for _ in range(bh):                      # 原图每一像素行
            line = bytearray()
            for x in range(bw):
                px = rgba[(0 * bw + x) * 4: (0 * bw + x) * 4 + 4]
                line += px * scale               # 横向放大
            for _ in range(scale):               # 纵向放大
                buf += line
            # 注意：这里逐条取的是该条的第 0 行，64x5 的色带各行一致
    # 上面的逐条实现只画了一条，改为逐条完整展开
    buf = bytearray()
    for rgba in rows:
        for y in range(bh):
            line = bytearray()
            for x in range(bw):
                line += rgba[(y * bw + x) * 4: (y * bw + x) * 4 + 4] * scale
            for _ in range(scale):
                buf += line
    save(out_w, out_h, bytes(buf), OUT / "3-颜色渐变条" / f"_全部{len(bars)}条对照_{out_w}x{out_h}.png")
    (OUT / "3-颜色渐变条" / "_条名顺序.txt").write_text("\n".join(names), encoding="utf-8")
    print(f"颜色渐变条 {len(bars)} 条，已拼成 {out_w}x{out_h} 对照图")

    print("输出:", OUT)

if __name__ == "__main__":
    main()
