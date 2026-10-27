"""制作物理移除自有特效素材的隔离包；保留原DLL，不覆盖生产包。"""
import hashlib
import json
import struct
from pathlib import Path
from tmod_archive import read_archive, contents

ROOT=Path(__file__).resolve().parent.parent
VERSION=next(line.split('=',1)[1].strip() for line in (ROOT/'ZenithReborn/build.txt').read_text(encoding='utf8').splitlines() if line.startswith('version'))
OUT=ROOT/f'artifacts/pure-code-vfx-{VERSION}'


def string(value):
    raw=value.encode('utf8');size=len(raw);prefix=bytearray()
    while size>=128:
        prefix.append((size&127)|128);size>>=7
    prefix.append(size)
    return bytes(prefix)+raw


def prepare():
    OUT.mkdir(parents=True,exist_ok=True)
    source=ROOT/'build-profile/Mods/ZenithReborn.tmod'
    loader,name,version,entries=read_archive(source)
    removed=[e['name'] for e in entries if e['name'].startswith('Assets/Vfx/') or e['name'] in ('SwordSpark.rawimg','SwordSpark.png')]
    keep=[e for e in entries if e['name'] not in removed]
    body=string(name)+string(version)+struct.pack('<i',len(keep))
    body+=b''.join(string(e['name'])+struct.pack('<ii',e['size'],e['stored_size']) for e in keep)
    body+=b''.join(e['stored'] for e in keep)
    stripped=OUT/'ZenithReborn-without-vfx-assets.tmod'
    stripped.write_bytes(b'TMOD'+string(loader)+hashlib.sha1(body).digest()+bytes(256)+struct.pack('<i',len(body))+body)
    full=OUT/'ZenithReborn-with-assets.tmod';full.write_bytes(source.read_bytes())
    _,_,_,verified=read_archive(stripped)
    actual={e['name']:contents(e) for e in verified}
    original={e['name']:contents(e) for e in entries}
    assert removed and not any(p.startswith('Assets/Vfx/') for p in actual)
    assert actual['ZenithReborn.dll']==original['ZenithReborn.dll']
    report=dict(version=version,removed=removed,removedCount=len(removed),sameDll=True,
                dllSha256=hashlib.sha256(actual['ZenithReborn.dll']).hexdigest(),
                fullPackageSha256=hashlib.sha256(full.read_bytes()).hexdigest(),
                strippedPackageSha256=hashlib.sha256(stripped.read_bytes()).hexdigest())
    (OUT/'package-independence.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(dict(version=version,removed=len(removed),sameDll=True)))


if __name__=='__main__':prepare()
