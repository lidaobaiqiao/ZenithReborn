"""Browse and validate the authoring library without loading or building the mod."""
import argparse
import json
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'VfxLibrary'


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def entries():
    return [(p, read(p)) for p in sorted(ROOT.rglob('entry.json'))]


def validate():
    catalog = read(ROOT / 'library.json')
    rows = entries()
    indexed = {}
    for path, entry in rows:
        key = entry['id']
        assert key not in indexed, f'Duplicate id: {key}'
        indexed[key] = entry
        assert entry['kind'] in ('mask', 'color'), path
        assert entry['category'] in catalog['categories'] or entry['category'] == '_Shared', path
        relative = path.relative_to(ROOT).parts
        assert relative[0] == ('Masks' if entry['kind'] == 'mask' else 'ColorFields'), path
        assert relative[1] == entry['category'] and relative[2] == entry['sword'], path
        for resource in entry.get('resources', []):
            if resource.get('repoPath'):
                target = (REPO / resource['repoPath']).resolve()
                assert target.is_relative_to(REPO) and target.is_file(), target
            if resource.get('provenance'):
                assert (REPO / resource['provenance']).is_file(), resource['provenance']
        if entry['status'] == 'awaiting-art':
            assert not entry.get('resources') and all(v is None for v in entry['clips'].values()), path
        if entry['kind'] == 'mask' and entry['category'] == 'SwordBody':
            assert entry['components'] == catalog['categories']['SwordBody']['components'], path
            assert isinstance(entry['colorField'], str), f'One shared color field required: {path}'
    for path, entry in rows:
        for name in ('reference', 'colorField'):
            target = entry.get(name)
            if target:
                assert target in indexed and indexed[target]['kind'] == 'color', f'{path}: {name} -> {target}'
        for target in entry.get('candidateFields', []):
            assert target in indexed and indexed[target]['kind'] == 'color', f'{path}: candidate -> {target}'
        seen = {entry['id']}
        target = entry.get('reference')
        while target:
            assert target not in seen, f'Color reference cycle: {path}'
            seen.add(target)
            target = indexed[target].get('reference')
    for sword in catalog['swords']:
        for category in catalog['categories']:
            for kind in ('mask', 'color'):
                assert f'{kind}.{category}.{sword["id"]}' in indexed, f'Missing {kind}/{category}/{sword["id"]}'
    print(f'PASS: {len(catalog["swords"])} swords/forms; {len(rows)} entries; categories, references and resources valid.')
    print('Authoring catalog only; no runtime integration or new artwork is implied.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('validate', 'search'))
    parser.add_argument('query', nargs='?', default='')
    parser.add_argument('--kind', choices=('mask', 'color'))
    parser.add_argument('--category', choices=('SwordBody', 'Rings', 'Particles', '_Shared'))
    args = parser.parse_args()
    if args.command == 'validate':
        validate()
        return
    for path, entry in entries():
        if args.kind and entry['kind'] != args.kind:
            continue
        if args.category and entry['category'] != args.category:
            continue
        if args.query.casefold() not in json.dumps(entry, ensure_ascii=False).casefold():
            continue
        print(f'{entry["id"]} | {entry["displayName"]} | {entry["status"]}')
        print(path.relative_to(REPO).as_posix())


if __name__ == '__main__':
    main()
