"""读取实际tmod的特效目录并按明确路径提取研究样本；不安装、不执行模组。"""
import argparse
import fnmatch
import hashlib
import io
import json
import re
import struct
from pathlib import Path

from PIL import Image
from tmod_archive import contents, read_archive


def texture_info(data, suffix):
    if suffix == '.rawimg':
        version, width, height = struct.unpack('<iii', data[:12])
        if version != 1 or not 0 < width <= 8192 or not 0 < height <= 8192 or len(data) != 12 + width * height * 4:
            raise ValueError('未支持的rawimg布局')
        image = Image.frombytes('RGBA', (width, height), data[12:])
    else:
        image = Image.open(io.BytesIO(data)).convert('RGBA')
    bands = image.split()
    return dict(width=image.width, height=image.height, rgba8Level0Bytes=image.width * image.height * 4,
                rgbEqual=bands[0].tobytes() == bands[1].tobytes() == bands[2].tobytes(),
                extrema=dict(zip('RGBA', image.getextrema())), mipLevelsInRawImage=1 if suffix == '.rawimg' else None)


def inspect(path, output, patterns):
    loader, name, version, entries = read_archive(path)
    result = dict(path=str(path.resolve()), sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                  loader=loader, name=name, version=version, containerSha1Verified=True,
                  signatureAuthenticated=False, fileCount=len(entries),
                  effects={}, members=[], selected=[], limitations=[
                      'SHA1检查发现损坏，不认证作者身份或数字签名。',
                      '源码和图片是静态证据，不证明运行、GPU兼容或画面质量。',
                      '采样语句统计是词法辅助，最终语义需核对控制流和调用方。'])
    for entry in entries:
        member = entry['name']
        suffix = Path(member).suffix.lower()
        if suffix in ('.fx', '.fxc', '.xnb', '.rawimg', '.png'):
            result['members'].append(dict(name=member, size=entry['size'], storedSize=entry['stored_size']))
        if suffix in ('.fx', '.fxc'):
            result['effects'][suffix] = result['effects'].get(suffix, 0) + 1
        if not any(fnmatch.fnmatchcase(member, pattern) for pattern in patterns):
            continue
        destination = (output / name / 'samples' / member).resolve()
        if not destination.is_relative_to((output / name / 'samples').resolve()):
            raise ValueError('归档成员路径越界：' + member)
        data = contents(entry)
        record = dict(name=member, sha256=hashlib.sha256(data).hexdigest(), bytes=len(data))
        if suffix in ('.rawimg', '.png'):
            record['texture'] = texture_info(data, suffix)
        if suffix == '.fx':
            text = data.decode('utf-8-sig')
            record['textures'] = re.findall(r'^\s*texture(?:2D)?\s+(\w+)', text, re.M)
            record['sampleStatements'] = len(re.findall(r'\btex2D(?:lod|bias|grad)?\s*\(', text))
            record['anchors'] = [dict(line=i, text=line.strip()) for i, line in enumerate(text.splitlines(), 1)
                                 if re.search(r'\b(tex2D|sampler|texture|uSweep|uErode|uTailErode)\b', line)]
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
        result['selected'].append(record)
    target = output / name / 'review.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return dict(name=name, version=version, fileCount=len(entries), effects=result['effects'],
                selected=len(result['selected']), report=str(target))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mod', nargs='+', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--select', action='append', default=[], help='大小写敏感的归档内路径或通配模式')
    args = parser.parse_args()
    for path in args.mod:
        print(json.dumps(inspect(path, args.output.resolve(), args.select), ensure_ascii=False))


if __name__ == '__main__':
    main()
