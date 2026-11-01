"""Design per-sword metallic color fields (my art direction call).

Each ore sword gets its own slow-flow field built on the R2 family language:
dark clear base + two counter-scrolling noise tints. Tints come from the metal's
in-game identity; scroll speed leans slow for warm/heavy metals, brisk for
cold/sharp ones. TitaniumSword keeps the user-preferred SlowFlowRadiantR2 and
is not touched.
"""
import json
from pathlib import Path

# name: (clear, primary tint, secondary tint, primary opacity, secondary opacity, primary scroll x, secondary scroll x)
FIELDS = {
    'CopperShortsword':  ([26, 19, 14], [255, 138, 62],  [176, 82, 38],  .46, .40, 7, -4),
    'CopperBroadsword':  ([26, 19, 14], [255, 138, 62],  [176, 82, 38],  .46, .40, 7, -4),
    'TinShortsword':     ([15, 19, 25], [172, 192, 214], [104, 126, 158], .44, .38, 8, -5),
    'TinBroadsword':     ([15, 19, 25], [172, 192, 214], [104, 126, 158], .44, .38, 8, -5),
    'IronShortsword':    ([18, 18, 22], [162, 172, 188], [96, 106, 128], .44, .38, 8, -5),
    'IronBroadsword':    ([18, 18, 22], [162, 172, 188], [96, 106, 128], .44, .38, 8, -5),
    'LeadShortsword':    ([13, 15, 23], [122, 142, 172], [66, 82, 108], .44, .38, 8, -5),
    'LeadBroadsword':    ([13, 15, 23], [122, 142, 172], [66, 82, 108], .44, .38, 8, -5),
    'SilverShortsword':  ([19, 21, 27], [222, 231, 245], [138, 153, 178], .48, .40, 9, -5),
    'SilverBroadsword':  ([19, 21, 27], [222, 231, 245], [138, 153, 178], .48, .40, 9, -5),
    'TungstenShortsword':([22, 20, 14], [232, 202, 132], [148, 118, 58], .46, .40, 7, -4),
    'TungstenBroadsword':([22, 20, 14], [232, 202, 132], [148, 118, 58], .46, .40, 7, -4),
    'GoldShortsword':    ([24, 20, 10], [255, 212, 82],  [168, 118, 30], .48, .42, 6, -4),
    'GoldBroadsword':    ([24, 20, 10], [255, 212, 82],  [168, 118, 30], .48, .42, 6, -4),
    'PlatinumShortsword':([18, 20, 26], [236, 241, 250], [148, 163, 190], .48, .40, 9, -5),
    'PlatinumBroadsword':([18, 20, 26], [236, 241, 250], [148, 163, 190], .48, .40, 9, -5),
    'CobaltSword':       ([9, 15, 36],  [72, 132, 255],  [38, 68, 182],  .46, .40, 9, -6),
    'PalladiumSword':    ([26, 14, 12], [255, 130, 70],  [170, 60, 38],  .46, .40, 7, -4),
    'MythrilSword':      ([9, 24, 22],  [82, 230, 190],  [38, 148, 128], .46, .40, 9, -6),
    'OrichalcumSword':   ([26, 12, 22], [240, 112, 160], [158, 50, 100], .46, .40, 8, -5),
    'AdamantiteSword':   ([26, 10, 10], [240, 84, 60],   [148, 40, 28],  .46, .40, 8, -5),
}

for name, (clear, primary, secondary, po, so, ps, ss) in FIELDS.items():
    directory = Path(f'VfxLibrary/ColorFields/SwordBody/{name}')
    directory.mkdir(parents=True, exist_ok=True)
    entry = {
        'id': f'color.SwordBody.{name}',
        'kind': 'color',
        'category': 'SwordBody',
        'sword': name,
        'displayName': f'{name} / 剑身一体组底色',
        'status': 'recipe-candidate',
        'recipe': {
            'clear': clear,
            'composition': 'additive',
            'layers': [
                {'tint': primary, 'opacity': po, 'scrollPixelsPerSecond': [ps, 0]},
                {'tint': secondary, 'opacity': so, 'scrollPixelsPerSecond': [ss, 0], 'initialYOffset': 'textureHeight/3'},
            ],
        },
        'resources': [],
        'previewNoise': 'checks/ZenithPrototypeCheck/PreviewColorNoise.cs',
        'currentRuntime': {'version': '0.3.60', 'enabled': False,
                           'reason': '旧材质外观已撤下，等待自有材质接入。'},
        'notes': '按矿石金属性定稿的逐剑底色；配方语言沿用R2家族（暗底+双向噪声流动），运行时未接入。',
        'reference': None,
        'candidateFields': [f'color.SwordBody.{name}'],
        'palette': {'clear': clear, 'primary': primary, 'secondary': secondary},
    }
    (directory / 'entry.json').write_text(json.dumps(entry, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('field', name)
print(len(FIELDS), 'metallic color fields written')
