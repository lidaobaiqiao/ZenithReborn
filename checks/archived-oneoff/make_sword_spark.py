"""Generate the small code-defined emissive sprite. No external image assets."""
import math
import struct
import zlib
from pathlib import Path

size = 16
rows = []
for y in range(size):
    row = bytearray()
    for x in range(size):
        dx, dy = abs(x - 7.5), abs(y - 7.5)
        halo = max(0, 1 - math.hypot(dx, dy) / 7.5) ** 2 * .32
        star = max(0, 1 - min(dx, dy) / 1.5) * max(0, 1 - max(dx, dy) / 7.5) ** 1.7
        alpha = round(255 * min(1, max(halo, star)))
        row.extend((255, 255, 255, alpha))
    rows.append(b'\0' + row)

def chunk(kind, data):
    return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data))

png = b'\x89PNG\r\n\x1a\n'
png += chunk(b'IHDR', struct.pack('!2I5B', size, size, 8, 6, 0, 0, 0))
png += chunk(b'IDAT', zlib.compress(b''.join(rows))) + chunk(b'IEND', b'')
(Path(__file__).resolve().parents[1] / 'ZenithReborn/SwordSpark.png').write_bytes(png)
