"""Mark SwordBody entries of the 21 auto-fitted ore swords as review-candidate R2."""
import glob
import json
import os

swords = []
for d in sorted(glob.glob('VfxLibrary/Masks/SwordBody/*/R2')):
    name = os.path.basename(os.path.dirname(d))
    if name == 'TitaniumSword':
        continue  # historical R3 entry stays as-is
    swords.append(name)

for name in swords:
    path = f'VfxLibrary/Masks/SwordBody/{name}/entry.json'
    entry = json.load(open(path, encoding='utf-8'))
    if entry.get('revision') == 'R2' and entry.get('status') == 'review-candidate':
        continue
    clip = json.load(open(f'VfxLibrary/Masks/SwordBody/{name}/R2/clip.json', encoding='utf-8'))
    entry['status'] = 'review-candidate'
    entry['canvas'] = clip['canvas']
    entry['pivot'] = clip['pivot']
    entry['clips'] = {
        phase: {'metadata': 'R2/clip.json', 'atlas': 'R2/active.png', 'phase': phase}
        for phase in ('appear', 'active', 'finish')
    }
    entry['resources'] = [{
        'repoPath': f'VfxLibrary/Masks/SwordBody/{name}/R2/active.png',
        'provenance': f'VfxLibrary/Masks/SwordBody/{name}/R2/authoring.json',
    }]
    entry['revision'] = 'R2'
    entry['runtimeIntegrated'] = False
    entry['finalArtApproved'] = False
    open(path, 'w', encoding='utf-8').write(json.dumps(entry, ensure_ascii=False, indent=2) + '\n')
    print('updated', name)
print(len(swords), 'entries marked review-candidate R2')
