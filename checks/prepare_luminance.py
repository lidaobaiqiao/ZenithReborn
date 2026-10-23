"""Resolve the installed Luminance library; keep development binaries in ignored artifacts."""
import argparse
import hashlib
import json
import os
import shutil
from pathlib import Path
from tmod_archive import read_archive, contents

ROOT = Path(__file__).resolve().parent.parent


def prepare(archive=None, profiles=()):
    if not archive:
        archive = os.environ.get('ZENITH_LUMINANCE_ARCHIVE')
    if archive:
        source = Path(archive)
    else:
        workshop = Path('E:/SteamLibrary/steamapps/workshop/content/1281930/3222493606')
        candidates = []
        for path in workshop.glob('*/Luminance.tmod'):
            try:
                branch = tuple(map(int, path.parent.name.split('.')))
            except ValueError:
                continue
            if branch <= (2026, 8):
                candidates.append((branch, path))
        if not candidates:
            raise FileNotFoundError('Install Luminance, or set ZENITH_LUMINANCE_ARCHIVE to its .tmod path.')
        source = max(candidates)[1]
    loader, name, version, entries = read_archive(source)
    if name != 'Luminance':
        raise ValueError('Expected the official Luminance library archive.')
    output = ROOT / 'artifacts/dependencies/Luminance'
    output.mkdir(parents=True, exist_ok=True)
    for entry in entries:
        if entry['name'] in ('Luminance.dll', 'Luminance.pdb', 'Luminance.xml'):
            target = output / entry['name']
            data = contents(entry)
            if not target.exists() or target.read_bytes() != data:
                target.write_bytes(data)
    if not (output / 'Luminance.dll').exists():
        raise ValueError('Luminance.dll missing from archive.')
    for profile in profiles:
        target = Path(profile) / 'Mods/Luminance.tmod'
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_bytes() != source.read_bytes():
            shutil.copy2(source, target)
    result = dict(name=name, version=version, loader=loader, archive=str(source.resolve()),
                  sha256=hashlib.sha256(source.read_bytes()).hexdigest())
    (output / 'dependency.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive')
    parser.add_argument('--profile', action='append', default=[])
    args = parser.parse_args()
    print(json.dumps(prepare(args.archive, args.profile), ensure_ascii=False))
