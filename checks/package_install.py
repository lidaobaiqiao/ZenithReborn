"""打包已验证的模组；只替换目标包，按实际声明处理依赖，不修改其他模组。"""
import hashlib
import json
import os
import shutil
import zipfile
from datetime import datetime
from pathlib import Path
from tmod_archive import read_archive, contents
from prepare_luminance import prepare as prepare_luminance
from check_vfx_ownership import verify as verify_vfx_ownership

root = Path(__file__).resolve().parent.parent
source = root / 'ZenithReborn'
out = root / 'artifacts'
built = root / 'build-profile/Mods/ZenithReborn.tmod'
report = out / 'wood-check.txt'
report_text = report.read_text(encoding='utf-8')
assert report_text.startswith('PASS: '), report_text[:80]
checks = int(report_text.split()[1])
declared = next((line.split('=',1)[1].strip() for line in (source/'build.txt').read_text(encoding='utf-8').splitlines() if line.strip().startswith('modReferences')), '')
required_mods = [value.strip().split('@')[0] for value in declared.split(',') if value.strip()]
dependencies = [prepare_luminance()] if 'Luminance' in required_mods else []
assert set(required_mods) <= {'Luminance'}, '存在尚未准备的声明依赖'
assert report.stat().st_mtime > built.stat().st_mtime
loader, name, version, entries = read_archive(built)
expected_version = next(line.split('=', 1)[1].strip() for line in (source/'build.txt').read_text(encoding='utf-8').splitlines() if line.startswith('version'))
assert name == 'ZenithReborn' and version == expected_version
packed = {e['name']: contents(e) for e in entries}
vfx_ownership = verify_vfx_ownership(built)
assert packed['ZenithReborn.dll'] == (source / 'bin/Release/net8.0/ZenithReborn.dll').read_bytes()
own_manifest=source/'Assets/Effects/build-manifest.json'
own_effects=json.loads(own_manifest.read_text(encoding='utf-8'))['effects']
for entry in own_effects:
    for field,hash_field in [('source','sourceSha256'),('compiled','compiledSha256')]:
        relative='Assets/Effects/'+entry[field]
        data=(source/relative).read_bytes()
        assert packed[relative]==data and hashlib.sha256(data).hexdigest()==entry[hash_field],relative
    assert entry['origin']=='project-authored' and not entry['thirdPartySourceIncludes']
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
    z.write(root / 'AGENTS.md', 'AGENTS.md')
    for folder in ('docs', 'checks', 'VfxLibrary','tools'):
        for path in (root/folder).rglob('*'):
            if path.is_file() and not any(s in ('bin', 'obj', '__pycache__') for s in path.relative_to(root).parts):
                z.write(path, path.relative_to(root).as_posix())
with zipfile.ZipFile(out / f'ZenithReborn-{version}-source.zip') as z:
    assert z.testzip() is None
    assert not any(path.endswith(('OniCrimsonSlash.fx','OniCrimsonSlash.fxc','NoiseSoft01.rawimg','SlashBrush01.rawimg'))
        for path in z.namelist()), '新源码包不能夹带已撤下的第三方材质'

if os.environ.get('ZENITH_PACKAGE_ONLY') == '1':
    result = dict(name=name, version=version, installed=False, enabled=False, package_only=True,
        target=str(out / 'ZenithReborn.tmod'),
        sha256=hashlib.sha256((out / 'ZenithReborn.tmod').read_bytes()).hexdigest(),
        loader=loader, focused_checks=checks,
        source_archive=f'ZenithReborn-{version}-source.zip', dependencies=dependencies)
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
def matching_report(relative):
    path=out/relative
    if not path.exists():
        return False
    data=json.loads(path.read_text(encoding='utf-8'))
    return data.get('passed') is True and data.get('testedPackageSha256') == hashlib.sha256(built.read_bytes()).hexdigest()

material_verified=matching_report(f'airflow-material-{version}/gpu.json')
world_verified=matching_report(f'airflow-world-{version}/world.json')
soul_trail_verified=matching_report(f'soul-trail-gpu-{version}/gpu.json')
own_shader_independent=matching_report(f'soul-trail-gpu-{version}/independence.json')
soul_flight_world_verified=matching_report(f'soul-flight-world-{version}/world.json')
refraction_gpu=out/f'soul-trail-gpu-{version}/gpu.json'
refraction_report=json.loads(refraction_gpu.read_text(encoding='utf-8')) if soul_trail_verified else {}
native_refraction_verified=matching_report(f'soul-flight-world-{version}/native-probe.json')
tree_refraction_verified=matching_report(f'soul-flight-world-{version}/tree-evidence.json')
refraction_verified=soul_trail_verified and refraction_report.get('refraction',{}).get('Passed') is True and soul_flight_world_verified and native_refraction_verified and tree_refraction_verified
lab_package=root/'build-profile/Mods/ZenithMaterialLab.tmod'
lab_path=out/f'material-lab-{version}/gpu.json'
lab_report=json.loads(lab_path.read_text(encoding='utf-8')) if lab_path.exists() else {}
lab_sha=hashlib.sha256(lab_package.read_bytes()).hexdigest() if lab_package.exists() else None
material_lab_verified=matching_report(f'material-lab-{version}/gpu.json') and lab_report.get('labPackageSha256')==lab_sha
lab_artifact=None
if lab_package.exists():
    _,lab_name,lab_version,_=read_archive(lab_package)
    assert lab_name=='ZenithMaterialLab'
    lab_artifact=out/f'ZenithMaterialLab-{lab_version}.tmod'
    shutil.copy2(lab_package,lab_artifact)
lab_input_path=out/f'material-lab-interaction-{version}.json'
lab_input=json.loads(lab_input_path.read_text(encoding='utf-8')) if lab_input_path.exists() else {}
material_lab_input_verified=(lab_input.get('passed') is True and lab_input.get('actualMouseWorkbenchInput') is True
    and lab_input.get('testedPackageSha256')==hashlib.sha256(built.read_bytes()).hexdigest()
    and lab_input.get('testedLabPackageSha256')==lab_sha)
advanced_world_path=out/f'soul-flight-world-{version}-surface/world.json'
advanced_world=json.loads(advanced_world_path.read_text(encoding='utf-8')) if advanced_world_path.exists() else {}
advanced_surface_world_verified=(matching_report(f'soul-flight-world-{version}-surface/world.json')
    and advanced_world.get('advancedSurfaceMaterial') is True and advanced_world.get('materialLightCalls',0)>0)
manual_path=out/f'manual-{version}/validation.json'
manual_report=json.loads(manual_path.read_text(encoding='utf-8')) if manual_path.exists() else {}
manual_attack_verified=(manual_report.get('testedPackageSha256')==hashlib.sha256(built.read_bytes()).hexdigest()
    and manual_report.get('actualAttackObserved') is True and manual_report.get('fixtureModEnabled') is False)
dummy_path=out/f'superdummy-{version}/world.json'
dummy_verified=matching_report(f'superdummy-{version}/world.json')
dummy_report=json.loads(dummy_path.read_text(encoding='utf-8')) if dummy_verified else {}
dummy_verified=dummy_verified and dummy_report.get('modDummyId',-1)>=0 and dummy_report.get('dummyFrames',0)>0
loaded_names={m['Name'] for m in dummy_report.get('loadedMods',[])}
full_pack_verified=dummy_verified and set(enabled_original)<=loaded_names
input_path=out/f'soul-input-{version}/validation.json'
input_report=json.loads(input_path.read_text(encoding='utf-8')) if input_path.exists() else {}
input_verified=(input_report.get('passed') is True and input_report.get('actualMouseAttack') is True
    and input_report.get('testedPackageSha256')==hashlib.sha256(built.read_bytes()).hexdigest())
result = dict(name=name, version=version, installed=True, enabled=True,
    target=str(target), sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
    loader=loader, focused_checks=checks, source_archive=f'ZenithReborn-{version}-source.zip',
    existing_addon_preserved=True, config_preserved=True,
    previous_enabled_entries_preserved=True, backup=str(backup),
    dependencies=dependencies,
    third_party_vfx_assets_removed=vfx_ownership['passed'],
    legacy_vfx_layers_removed=True,
    legacy_vfx_layers_rebuilt=False,
    own_swordbody_mask_restored=vfx_ownership['ownSwordBodyMaskRestored'],
    player_material_controls=False,
    material_parameters_developer_only=True,
    screen_space_refraction_tested=refraction_verified,
    native_same_frame_ior_comparison_tested=native_refraction_verified,
    native_tree_refraction_tested=tree_refraction_verified,
    material_lab_gpu_tested=material_lab_verified,
    material_lab_actual_mouse_input_tested=material_lab_input_verified,
    material_lab_package_sha256=lab_sha,
    material_lab_package=str(lab_artifact) if lab_artifact else None,
    material_lab_in_formal_profile='ZenithMaterialLab' in enabled,
    material_regions_gpu_tested=material_lab_verified and lab_report.get('regions',{}).get('Passed') is True,
    material_region_data_channels_preserved=material_lab_verified and lab_report.get('regions',{}).get('DataChannelsPreserved') is True,
    material_regions_world_fixture_tested=advanced_surface_world_verified and advanced_world.get('materialRegionsActive') is True,
    runtime_texture_mips_tested=False,
    packed_texture_layout_applied=False,
    advanced_surface_world_fixture_tested=advanced_surface_world_verified,
    graphics_slash_preview_tested=slash_verified,
    graphics_slash_preview_report=str(slash_report_path.relative_to(out)) if slash_verified else None,
    graphics_ore_preview_tested=ore_verified or material_verified,
    graphics_airflow_material_tested=material_verified,
    graphics_airflow_material_report=f'airflow-material-{version}/gpu.json' if material_verified else None,
    graphics_airflow_world_fixture_tested=world_verified,
    graphics_airflow_world_fixture_report=f'airflow-world-{version}/world.json' if world_verified else None,
    graphics_soul_trail_preview_tested=soul_trail_verified,
    own_shader_independence_gpu_tested=own_shader_independent,
    own_shader_independence_report=f'soul-trail-gpu-{version}/independence.json' if own_shader_independent else None,
    graphics_soul_trail_preview_report=f'soul-trail-gpu-{version}/gpu.json' if soul_trail_verified else None,
    graphics_soul_flight_world_fixture_tested=soul_flight_world_verified,
    independent_trail_ending_tested=soul_flight_world_verified and advanced_surface_world_verified
        and advanced_world.get('endingAfterParentKill') is True and advanced_world.get('remainingEndings')==0,
    graphics_soul_flight_world_fixture_report=f'soul-flight-world-{version}/world.json' if soul_flight_world_verified else None,
    isolated_manual_attack_observed=manual_attack_verified,
    isolated_mouse_soul_flight_tested=input_verified,
    isolated_mouse_soul_flight_report=str(input_path.relative_to(out)) if input_verified else None,
    isolated_mouse_soul_flight_full_mod_set=input_verified and set(enabled_original)<=set(input_report.get('loadedMods',[])),
    isolated_manual_attack_report=f'manual-{version}/validation.json' if manual_attack_verified else None,
    actual_mouse_superdummy_placement_tested=manual_report.get('superDummyManualPlacementCompleted') is True,
    graphics_superdummy_fixture_tested=dummy_verified,
    graphics_superdummy_fixture_report=str(dummy_path.relative_to(out)) if dummy_verified else None,
    reported_placement_black_screen_reproduced=False,
    reported_placement_black_screen_resolved=False,
    graphics_ore_preview_report=str(ore_report_path.relative_to(out)) if ore_verified else (f'airflow-material-{version}/gpu.json' if material_verified else None),
    graphics_ui_tested=graphics_verified, graphics_ui_report=gui_report_path.name if graphics_verified else None,
    graphics_combat_tested=combat_verified, graphics_combat_report=combat_report_path.name if combat_verified else None,
    live_multiplayer_tested=False,
    full_existing_modpack_load_tested=full_pack_verified)
(out / 'install-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
