"""Dependency-free PNG reader/writer for the mask registration pipeline.

Drop-in subset of the Pillow calls register_swordbody_vector.py needs:
open/convert/getchannel/merge/crop/get_flattened_data/save. Supports 8-bit
RGB and RGBA, non-interlaced PNGs with all five scanline filters - which is
exactly what the native SpriteBatch exporter (Texture2D.SaveAsPng) writes.
"""
import builtins
import struct
import zlib

_SIGNATURE = b'\x89PNG\r\n\x1a\n'


def _chunk(tag, data):
    return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)


class _Channel:
    def __init__(self, width, height, data):
        self.width, self.height, self.data = width, height, data

    def get_flattened_data(self):
        return list(self.data)

    def crop(self, box):
        left, upper, right, lower = box
        out = bytearray()
        for y in range(upper, lower):
            out += self.data[y * self.width + left:y * self.width + right]
        return _Channel(right - left, lower - upper, bytes(out))


class Image:
    def __init__(self, width, height, data, mode):
        self.width, self.height, self.data, self.mode = width, height, data, mode

    def convert(self, mode):
        if mode == self.mode:
            return self
        if self.mode == 'RGB' and mode == 'RGBA':
            out = bytearray()
            for i in range(0, len(self.data), 3):
                out += self.data[i:i + 3] + b'\xff'
            return Image(self.width, self.height, bytes(out), mode)
        if self.mode == 'RGBA' and mode == 'RGB':
            out = bytearray()
            for i in range(0, len(self.data), 4):
                out += self.data[i:i + 3]
            return Image(self.width, self.height, bytes(out), mode)
        raise ValueError('unsupported conversion ' + self.mode + ' -> ' + mode)

    def getchannel(self, name):
        step = 4 if self.mode == 'RGBA' else 3
        index = {'R': 0, 'G': 1, 'B': 2, 'A': 3}[name]
        return _Channel(self.width, self.height, self.data[index::step])

    def crop(self, box):
        left, upper, right, lower = box
        step = 4 if self.mode == 'RGBA' else 3
        out = bytearray()
        for y in range(upper, lower):
            row = y * self.width * step
            out += self.data[row + left * step:row + right * step]
        return Image(right - left, lower - upper, bytes(out), self.mode)

    def get_flattened_data(self):
        return list(self.data)

    def save(self, path):
        step = 4 if self.mode == 'RGBA' else 3
        raw = bytearray()
        for y in range(self.height):
            raw.append(0)  # filter: None
            raw += self.data[y * self.width * step:(y + 1) * self.width * step]
        color_type = 6 if self.mode == 'RGBA' else 2
        ihdr = struct.pack('>IIBBBBB', self.width, self.height, 8, color_type, 0, 0, 0)
        with open(path, 'wb') as f:
            f.write(_SIGNATURE)
            f.write(_chunk(b'IHDR', ihdr))
            f.write(_chunk(b'IDAT', zlib.compress(bytes(raw), 9)))
            f.write(_chunk(b'IEND', b''))


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    return a if pa <= pb and pa <= pc else (b if pb <= pc else c)


def read_png(path):
    with builtins.open(path, 'rb') as f:
        blob = f.read()
    assert blob[:8] == _SIGNATURE, 'not a PNG'
    pos, width, height, depth, color_type, idat = 8, 0, 0, 8, 6, bytearray()
    while pos < len(blob):
        length, = struct.unpack('>I', blob[pos:pos + 4])
        tag = blob[pos + 4:pos + 8]
        body = blob[pos + 8:pos + 8 + length]
        if tag == b'IHDR':
            width, height, depth, color_type = struct.unpack('>IIBB', body[:10])
            assert depth == 8 and body[12] == 0, 'only 8-bit non-interlaced PNG supported'
        elif tag == b'IDAT':
            idat += body
        elif tag == b'IEND':
            break
        pos += 12 + length
    assert color_type in (0, 2, 6), 'only grayscale/RGB/RGBA PNG supported'
    # 灰度单通道也要认：美术出的色场／磨损图默认是 8 位灰度，不是彩色 PNG
    channels = {0: 1, 2: 3, 6: 4}[color_type]
    stride = width * channels
    raw = zlib.decompress(bytes(idat))
    assert len(raw) == (stride + 1) * height, 'unexpected PNG payload size'
    out = bytearray(stride * height)
    prev = bytearray(stride)
    for y in range(height):
        line = raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)]
        ftype = raw[y * (stride + 1)]
        if ftype == 0:
            out[y * stride:(y + 1) * stride] = line
        elif ftype == 1:
            for i in range(stride):
                a = out[y * stride + i - channels] if i >= channels else 0
                out[y * stride + i] = (line[i] + a) & 0xff
        elif ftype == 2:
            for i in range(stride):
                out[y * stride + i] = (line[i] + prev[i]) & 0xff
        elif ftype == 3:
            for i in range(stride):
                a = out[y * stride + i - channels] if i >= channels else 0
                out[y * stride + i] = (line[i] + ((a + prev[i]) >> 1)) & 0xff
        elif ftype == 4:
            for i in range(stride):
                a = out[y * stride + i - channels] if i >= channels else 0
                c = prev[i - channels] if i >= channels else 0
                out[y * stride + i] = (line[i] + _paeth(a, prev[i], c)) & 0xff
        else:
            raise ValueError('unknown PNG filter %d' % ftype)
        prev = out[y * stride:(y + 1) * stride]
    if color_type == 0:
        # 灰度图统一扩成 RGB，调用方不用为单通道单独写分支
        rgb = bytearray(width * height * 3)
        for i, v in enumerate(out):
            rgb[i * 3] = rgb[i * 3 + 1] = rgb[i * 3 + 2] = v
        return Image(width, height, bytes(rgb), 'RGB')
    return Image(width, height, bytes(out), 'RGBA' if channels == 4 else 'RGB')


def merge(mode, channels):
    assert mode == 'RGB' and len(channels) == 3
    width, height = channels[0].width, channels[0].height
    data = bytearray(width * height * 3)
    for c in range(3):
        src = channels[c].data
        for i, v in enumerate(src):
            data[i * 3 + c] = v
    return Image(width, height, bytes(data), 'RGB')


# Mirror the Pillow call sites: Image.open(path) and Image.merge(mode, bands).
Image.open = staticmethod(read_png)
Image.merge = staticmethod(merge)
