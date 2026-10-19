"""Package the checked prototype and install only its addon + enabled entry."""
import hashlib
import json
import os
import shutil
import zipfile
from datetime import datetime
from pathlib import Path
from tmod_archive import read_archive, contents

root = Path(__file__).resolve().parent.parent
source = root / 'ZenithReborn'
out = root / 'artifacts'
built = root / 'build-profile/Mods/ZenithReborn.tmod'
report = out / 'wood-check.txt'
report_text = report.read_text(encoding='utf-8')
assert report_text.startswith('PASS: '), report_text[:80]
checks = int(report_text.split()[1])
assert report.stat().st_mtime > built.stat().st_mtime
loader, name, version, entries = read_archive(built)
expected_version = next(line.split('=', 1)[1].strip() for line in (source/'build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
assert name == 'ZenithReborn' and version == expected_version
packed = {e['name']: contents(e) for e in entries}
assert packed['ZenithReborn.dll'] == (source / 'bin/Release/net8.0/ZenithReborn.dll').read_bytes()
sourcefiles = [p for p in source.rglob('*') if p.is_file() and not any(s in ('bin', 'obj') for s in p.relative_to(source).parts)]
for path in sourcefiles:
    relative = path.relative_to(source).as_posix()
    if path.suffix in ('.cs', '.hjson') or path.name == 'description.txt':
        assert packed[relative] == path.read_bytes(), relative

shutil.copy2(built, out / 'ZenithReborn.tmod')
with zipfile.ZipFile(out / f'ZenithReborn-{version}-source.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for path in sourcefiles:
        z.write(path, 'ZenithReborn/' + path.relative_to(source).as_posix())
    z.write(root / 'README.md', 'README.md')
    for folder in ('docs', 'checks'):
        for path in (root/folder).rglob('*'):
            if path.is_file() and not any(s in ('bin', 'obj', '__pycache__') for s in path.relative_to(root).parts):
                z.write(path, path.relative_to(root).as_posix())
with zipfile.ZipFile(out / f'ZenithReborn-{version}-source.zip') as z:
    assert z.testzip() is None

if os.environ.get('ZENITH_PACKAGE_ONLY') == '1':
    result = dict(name=name, version=version, installed=False, enabled=False, package_only=True,
        target=str(out / 'ZenithReborn.tmod'),
        sha256=hashlib.sha256((out / 'ZenithReborn.tmod').read_bytes()).hexdigest(),
        loader=loader, focused_checks=checks,
        source_archive=f'ZenithReborn-{version}-source.zip')
    (out / 'install-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))
    raise SystemExit(0)

profile = Path(r'C:\Users\15537\Documents\My Games\Terraria\tModLoader')
mods = profile / 'Mods'
enabled_path = mods / 'enabled.json'
enabled_before = enabled_path.read_bytes()
enabled = json.loads(enabled_before.decode('utf-8-sig'))
config_before = (profile / 'config.json').read_bytes()
previous_addon = mods / 'SusieSpecializations.tmod'
previous_addon_hash = hashlib.sha256(previous_addon.read_bytes()).hexdigest() if previous_addon.exists() else None
backup = out / 'backups' / datetime.now().strftime('%Y%m%d-%H%M%S')
backup.mkdir(parents=True)
(backup / 'enabled.json').write_bytes(enabled_before)
target = mods / 'ZenithReborn.tmod'
if target.exists():
    shutil.copy2(target, backup / target.name)
pending = target.with_suffix('.pending')
shutil.copy2(built, pending)
os.replace(pending, target)
if name not in enabled:
    enabled.append(name)
    enabled_pending = enabled_path.with_suffix('.pending')
    enabled_pending.write_text(json.dumps(enabled, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    os.replace(enabled_pending, enabled_path)
assert target.read_bytes() == built.read_bytes()
assert (profile / 'config.json').read_bytes() == config_before
if previous_addon_hash:
    assert hashlib.sha256(previous_addon.read_bytes()).hexdigest() == previous_addon_hash
assert [n for n in json.loads(enabled_path.read_text(encoding='utf-8-sig')) if n != name] == [n for n in json.loads(enabled_before.decode('utf-8-sig')) if n != name]
assert 'ZenithPrototypeCheck' not in enabled
result = dict(name=name, version=version, installed=True, enabled=True,
    target=str(target), sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
    loader=loader, focused_checks=checks, source_archive=f'ZenithReborn-{version}-source.zip',
    existing_addon_preserved=True, config_preserved=True,
    previous_enabled_entries_preserved=True, backup=str(backup),
    graphics_ui_tested=False, live_multiplayer_tested=False,
    full_existing_modpack_load_tested=False)
(out / 'install-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
