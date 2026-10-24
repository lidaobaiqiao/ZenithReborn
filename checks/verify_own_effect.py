"""核对资源移除试验：同一生产DLL在没有第三方素材的包内得到相同GPU画面。"""
import hashlib
import json
from pathlib import Path
from tmod_archive import read_archive,contents

root=Path(__file__).resolve().parent.parent
version=next(s.split('=',1)[1].strip() for s in (root/'ZenithReborn/build.txt').read_text(encoding='utf-8').splitlines() if s.startswith('version'))
out=root/'artifacts'
normal=out/f'soul-trail-gpu-{version}'
removed=out/f'soul-trail-gpu-{version}-no-thirdparty'
a=json.loads((normal/'gpu.json').read_text(encoding='utf-8'))
b=json.loads((removed/'gpu.json').read_text(encoding='utf-8'))
production=root/'build-profile/Mods/ZenithReborn.tmod'
isolated=out/'soul-trail-render-profile-no-thirdparty/Mods/ZenithReborn.tmod'
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert a['passed'] and b['passed'] and a['actualGpu'] and b['actualGpu']
assert a['testedPackageSha256']==digest(production) and b['testedPackageSha256']==digest(isolated)
assert a['materialEffect']==b['materialEffect']=='ZenithReborn/Assets/Effects/ZRSwordFlow'
assert not a['artworkInputs'] and not b['artworkInputs'] and not b['thirdPartyAssetsPresent']
_,name,actual_version,entries=read_archive(isolated)
assert name=='ZenithReborn' and actual_version==version
assert not any(e['name'].startswith('Assets/ThirdParty/') for e in entries)
binary=next(contents(e) for e in entries if e['name']=='ZenithReborn.dll')
assert binary==(root/'ZenithReborn/bin/Release/net8.0/ZenithReborn.dll').read_bytes()
frames=[p.relative_to(normal) for p in normal.rglob('*.png')]
assert len(frames)>100 and all(digest(normal/p)==digest(removed/p) for p in frames)
result={'passed':True,'testedPackageSha256':digest(production),'isolatedPackageSha256':digest(isolated),
    'sameProductionDll':True,'thirdPartyAssetFilesRemoved':True,'loadedArtworkInputs':[],
    'identicalGpuImages':len(frames),'effect':a['materialEffect'],
    'scope':'仅主飞剑材质；巨剑与矿石其他路径尚未迁移，不是全模组无第三方资源证明',
    'actualMouseGameplayTested':False,'liveMultiplayerTested':False}
(normal/'independence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
