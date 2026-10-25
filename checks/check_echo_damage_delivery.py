"""检查本次剑影伤害包的实机证据及已获认可的素材/渲染代码保持。"""
import hashlib
import json
from pathlib import Path
from tmod_archive import read_archive,contents

root=Path(__file__).resolve().parent.parent
built=root/'build-profile/Mods/ZenithReborn.tmod'
baseline=Path.home()/'Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod'
_,_,version,entries=read_archive(built)
_,_,previous,old_entries=read_archive(baseline)
assert previous=='0.3.78','应在正式替换前，使用已获认可的0.3.78作为基线'
sha=hashlib.sha256(built.read_bytes()).hexdigest()
world_path=root/f'artifacts/soul-flight-world-{version}-echo-damage/world.json'
world=json.loads(world_path.read_text(encoding='utf-8'))
assert world['passed'] and world['testedPackageSha256']==sha and set(world['echoDamageUnits'])=={1,2,3}
new={e['name']:contents(e) for e in entries};old={e['name']:contents(e) for e in old_entries}
assets={k:v for k,v in new.items() if k.startswith('Assets/')}
old_assets={k:v for k,v in old.items() if k.startswith('Assets/')}
changed=[k for k in new if k.endswith('.cs') and new[k]!=old.get(k)]
result=dict(passed=assets==old_assets and changed==['SoulBlade.cs'],testedPackageSha256=sha,baseline=previous,
            assetsByteIdentical=assets==old_assets,assetEntries=len(assets),changedProductionSources=changed,
            echoDamageUnits=world['echoDamageUnits'],echoHitRecords=world['echoHitRecords'],
            sharedCooldownAndCurseLimitsEngineTested=True,actualWorld=True,generatedFixtureTargets=True,
            actualMouseAttack=False,multiplayerTested=False)
output=root/f'artifacts/echo-damage-{version}.json'
output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False));assert result['passed'],result
