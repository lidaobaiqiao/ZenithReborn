"""Package the checked addon; replace only it and enable its declared graphics dependency."""
import hashlib
import json
import os
import shutil
import zipfile
from datetime import datetime
from pathlib import Path
from tmod_archive import read_archive, contents
from prepare_luminance import prepare as prepare_luminance

root = Path(__file__).resolve().parent.parent
source = root / 'ZenithReborn'
out = root / 'artifacts'
built = root / 'build-profile/Mods/ZenithReborn.tmod'
report = out / 'wood-check.txt'
report_text = report.read_text(encoding='utf-8')
assert report_text.startswith('PASS: '), report_text[:80]
checks = int(report_text.split()[1])
dependency = prepare_luminance()
required_mods = [dependency['name']]
assert report.stat().st_mtime > built.stat().st_mtime
loader, name, version, entries = read_archive(built)
expected_version = next(line.split('=', 1)[1].strip() for line in (source/'build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
assert name == 'ZenithReborn' and version == expected_version
packed = {e['name']: contents(e) for e in entries}
assert packed['ZenithReborn.dll'] == (source / 'bin/Release/net8.0/ZenithReborn.dll').read_bytes()
sourcefiles = [p for p in source.rglob('*') if p.is_file() and not any(s in ('bin', 'obj') for s in p.relative_to(source).parts)]
for manifest_path in (source / 'Assets/ThirdParty').glob('*/provenance.json'):
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    for filename in ['LICENSE.txt', 'provenance.json'] + [r['file'] for r in manifest['resources']]:
        path = manifest_path.parent / filename
        assert packed[path.relative_to(source).as_posix()] == path.read_bytes(), filename
    for resource in manifest['resources']:
        assert hashlib.sha256((manifest_path.parent / resource['file']).read_bytes()).hexdigest() == resource['sha256']
for path in sourcefiles:
    relative = path.relative_to(source).as_posix()
    if path.suffix in ('.cs', '.hjson') or path.name == 'description.txt':
        assert packed[relative] == path.read_bytes(), relative

shutil.copy2(built, out / 'ZenithReborn.tmod')
with zipfile.ZipFile(out / f'ZenithReborn-{version}-source.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for path in sourcefiles:
        z.write(path, 'ZenithReborn/' + path.relative_to(source).as_posix())
    z.write(root / 'README.md', 'README.md')
    for folder in ('docs', 'checks', 'VfxLibrary'):
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
        source_archive=f'ZenithReborn-{version}-source.zip', dependencies=[dependency])
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
if any(mod not in enabled for mod in required_mods + [name]):
    for mod in required_mods + [name]:
        if mod not in enabled:
            enabled.append(mod)
    enabled_pending = enabled_path.with_suffix('.pending')
    enabled_pending.write_text(json.dumps(enabled, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    os.replace(enabled_pending, enabled_path)
assert target.read_bytes() == built.read_bytes()
assert (profile / 'config.json').read_bytes() == config_before
if previous_addon_hash:
    assert hashlib.sha256(previous_addon.read_bytes()).hexdigest() == previous_addon_hash
enabled_original = json.loads(enabled_before.decode('utf-8-sig'))
assert [n for n in json.loads(enabled_path.read_text(encoding='utf-8-sig')) if n in enabled_original] == enabled_original
assert 'ZenithPrototypeCheck' not in enabled
gui_report_path = out / f'ui-validation-{version}.json'
graphics_verified = False
if gui_report_path.exists():
    gui_report = json.loads(gui_report_path.read_text(encoding='utf-8'))
    graphics_verified = gui_report.get('tested_package_sha256') == hashlib.sha256(built.read_bytes()).hexdigest()
combat_report_path = out / f'ore-tier-combat-validation-{version}.json'
combat_verified = False
if combat_report_path.exists():
    combat_report = json.loads(combat_report_path.read_text(encoding='utf-8-sig'))
    combat_verified = combat_report.get('tested_package_sha256') == hashlib.sha256(built.read_bytes()).hexdigest()
slash_report_path = out / f'slash-gpu-{version}/slash-gpu.json'
slash_verified = False
if slash_report_path.exists():
    slash_report = json.loads(slash_report_path.read_text(encoding='utf-8'))
    slash_verified = (slash_report.get('passed') is True and slash_report.get('actualGpu') is True and
        slash_report.get('tested_package_sha256') == hashlib.sha256(built.read_bytes()).hexdigest())
ore_report_path = out / f'slash-gpu-{version}/ore-gpu.json'
ore_verified = False
if ore_report_path.exists():
    ore_report = json.loads(ore_report_path.read_text(encoding='utf-8'))
    ore_verified = (ore_report.get('passed') is True and ore_report.get('actualGpu') is True and
        ore_report.get('tested_package_sha256') == hashlib.sha256(built.read_bytes()).hexdigest())
result = dict(name=name, version=version, installed=True, enabled=True,
    target=str(target), sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
    loader=loader, focused_checks=checks, source_archive=f'ZenithReborn-{version}-source.zip',
    existing_addon_preserved=True, config_preserved=True,
    previous_enabled_entries_preserved=True, backup=str(backup),
    dependencies=[dependency],
    graphics_slash_preview_tested=slash_verified,
    graphics_slash_preview_report=str(slash_report_path.relative_to(out)) if slash_verified else None,
    graphics_ore_preview_tested=ore_verified,
    graphics_ore_preview_report=str(ore_report_path.relative_to(out)) if ore_verified else None,
    graphics_ui_tested=graphics_verified, graphics_ui_report=gui_report_path.name if graphics_verified else None,
    graphics_combat_tested=combat_verified, graphics_combat_report=combat_report_path.name if combat_verified else None,
    live_multiplayer_tested=False,
    full_existing_modpack_load_tested=False)
(out / 'install-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
