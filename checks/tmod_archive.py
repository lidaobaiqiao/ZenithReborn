import hashlib
import io
import json
import struct
import sys
import zlib
from pathlib import Path

def read_string(f):
    n = shift = 0
    while True:
        b = f.read(1)[0]
        n |= (b & 127) << shift
        if b < 128:
            break
        shift += 7
    return f.read(n).decode('utf-8')

def read_archive(path):
    raw = Path(path).read_bytes()
    f = io.BytesIO(raw)
    assert f.read(4) == b'TMOD'
    loader = read_string(f)
    digest = f.read(20)
    signature = f.read(256)
    length = struct.unpack('<i', f.read(4))[0]
    start = f.tell()
    assert length == len(raw) - start
    assert hashlib.sha1(raw[start:]).digest() == digest
    name, version = read_string(f), read_string(f)
    count = struct.unpack('<i', f.read(4))[0]
    entries = []
    for _ in range(count):
        filename = read_string(f)
        size, stored_size = struct.unpack('<ii', f.read(8))
        entries.append(dict(name=filename, size=size, stored_size=stored_size))
    for e in entries:
        e['offset'] = f.tell()
        e['stored'] = f.read(e['stored_size'])
        assert len(e['stored']) == e['stored_size']
    assert f.tell() == len(raw)
    return loader, name, version, entries

def contents(e):
    data = zlib.decompress(e['stored'], -15) if e['size'] != e['stored_size'] else e['stored']
    assert len(data) == e['size']
    return data

if __name__ == '__main__':
    loader, name, version, entries = read_archive(sys.argv[1])
    out = Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    info = dict(loader=loader, name=name, version=version, entries=[{k:v for k,v in e.items() if k != 'stored'} for e in entries])
    (out/'archive-index.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
    selected = [e for e in entries if e['name'].endswith(('.dll','.hjson','.cs','.json')) or e['name'] == 'Info']
    for e in selected:
        target = out/e['name']
        assert target.resolve().is_relative_to(out.resolve())
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(contents(e))
    print(json.dumps(dict(loader=loader, name=name, version=version, files=len(entries), extracted=len(selected))))
