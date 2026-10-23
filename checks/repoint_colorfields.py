"""Point each sword's R2 authoring colorField at its new per-sword metallic field."""
import glob
import json
import os

count = 0
for d in sorted(glob.glob('VfxLibrary/Masks/SwordBody/*/R2')):
    name = os.path.basename(os.path.dirname(d))
    if name == 'TitaniumSword':
        continue  # keeps user-preferred SlowFlowRadiantR2
    path = f'{d}/authoring.json'
    recipe = json.load(open(path, encoding='utf-8'))
    field = f'color.SwordBody.{name}'
    if recipe.get('colorField') == field:
        continue
    recipe['colorField'] = field
    with open(path, 'w', encoding='utf-8') as f:
        f.write(json.dumps(recipe, ensure_ascii=False, indent=2) + '\n')
    print('colorField ->', field)
    count += 1
print(count, 'recipes repointed')
