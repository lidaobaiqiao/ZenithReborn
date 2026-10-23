"""Evolve each sword's R2 mask personality (art direction call).

Same shape language as the frozen R2 baseline - one blade-fitting film, tapered
wake, advecting contour crests. No hooks, no forks, no general brightening.
Only rhythm differs, matched to each sword's character:
  - shortswords: tight, brisk crests (fast weapon feel)
  - low-tier broadswords: the R2 baseline, weighty and calm
  - high-tier broadswords: slightly taller crests, richer blade flow
  - late-hardmode ore swords: tallest crests, brisk-but-controlled loop
TitaniumSword keeps the frozen user-preferred R2 recipe untouched.
"""
import glob
import json
import os

GROUPS = {
    'SHORT': {'height': 16, 'loop': 0.7, 'members': [
        'CopperShortsword', 'TinShortsword', 'IronShortsword', 'LeadShortsword',
        'SilverShortsword', 'TungstenShortsword', 'GoldShortsword', 'PlatinumShortsword']},
    'BROAD_LOW': {'height': 18, 'loop': 0.9, 'members': [
        'CopperBroadsword', 'TinBroadsword', 'IronBroadsword', 'LeadBroadsword']},
    'BROAD_HIGH': {'height': 20, 'loop': 0.8, 'members': [
        'SilverBroadsword', 'TungstenBroadsword', 'GoldBroadsword', 'PlatinumBroadsword']},
    'ORE_LATE': {'height': 20, 'loop': 0.75, 'members': [
        'CobaltSword', 'PalladiumSword', 'MythrilSword', 'OrichalcumSword', 'AdamantiteSword']},
}

lookup = {}
for group, spec in GROUPS.items():
    for name in spec['members']:
        lookup[name] = (group, spec['height'], spec['loop'])

for d in sorted(glob.glob('VfxLibrary/Masks/SwordBody/*/R2')):
    name = os.path.basename(os.path.dirname(d))
    if name not in lookup:
        continue
    group, height, loop = lookup[name]
    path = f'{d}/authoring.json'
    recipe = json.load(open(path, encoding='utf-8'))
    recipe['wave']['height'] = height
    recipe['loopSeconds'] = loop
    recipe['evolution'] = {'group': group, 'note': 'R2-family rhythm differentiation; shape language unchanged'}
    with open(path, 'w', encoding='utf-8') as f:
        f.write(json.dumps(recipe, ensure_ascii=False, indent=2) + '\n')
    print(f'{name:<22} {group:<10} height={height} loop={loop}')
print(len(lookup), 'swords evolved')
