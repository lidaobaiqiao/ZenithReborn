"""验证生产包只含项目自有着色器，旧材质不能被资源或运行代码暗中带回。"""
import hashlib
import json
from pathlib import Path
from tmod_archive import read_archive, contents

root=Path(__file__).resolve().parent.parent

def verify(package):
    loader,name,version,entries=read_archive(package)
    packed={entry['name']:contents(entry) for entry in entries}
    forbidden=('Assets/ThirdParty/', 'OniCrimsonSlash', 'SlashBrush01', 'NoiseSoft01')
    assert not any(any(word in path for word in forbidden) for path in packed), '生产包仍有旧材质资源'
    manifest=json.loads(packed['Assets/Effects/build-manifest.json'])
    declared=set()
    for effect in manifest['effects']:
        assert effect['origin']=='project-authored' and not effect['thirdPartySourceIncludes']
        for field,hash_field in [('source','sourceSha256'),('compiled','compiledSha256')]:
            path='Assets/Effects/'+effect[field]
            assert hashlib.sha256(packed[path]).hexdigest()==effect[hash_field],path
            declared.add(path)
    actual={path for path in packed if path.endswith(('.fx','.fxc'))}
    assert actual==declared, (actual,declared)
    for path in (root/'ZenithReborn').glob('*.cs'):
        text=path.read_text(encoding='utf-8-sig')
        assert not any(token in text for token in ('Assets/ThirdParty','MartialCrescentMaterial','OreVisualRenderer')),path
    assert not any(path.endswith('/MartialCrescentMaterial.cs') for path in packed)
    assert 'GlassMaterialConfig.cs' not in packed, '制作端材质参数不能变成玩家设置'
    return dict(passed=True,version=version,testedPackageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),
        thirdPartyAssetFiles=[],projectAuthoredEffects=[effect['source'] for effect in manifest['effects']],
        legacyRuntimeRenderersRemoved=True,legacyEffectsRestored=False,
        ownSwordBodyMaskRestored='VfxSwordMask.cs' in packed,
        playerMaterialControls=False,
        scope='包内资源、材质哈希与生产源码引用；不代替实际GPU或世界战斗验证')

if __name__=='__main__':
    result=verify(root/'build-profile/Mods/ZenithReborn.tmod')
    path=root/f"artifacts/vfx-ownership-{result['version']}.json"
    path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
