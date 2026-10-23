"""Write an R2-family autoFit authoring recipe for one sword directory."""
import json
import sys
from pathlib import Path

destination = Path(sys.argv[1])
recipe = {
    'revision': 'R2',
    'family': 'R2',
    'autoFit': True,
    'method': 'native SpriteBatch vector-mask authoring; no production renderer changes',
    'canvas': [448, 256],
    'frames': 16,
    'fps': 20,
    'loopSeconds': 0.8,
    'centerAlpha': 0.16,
    'edgeAlpha': 0.64,
    'leadingExtensionPixels': 6,
    'wave': {
        'topPhase': 0,
        'bottomPhase': 0.45,
        'height': 18,
        'leadingWidth': 10,
        'trailingWidth': 35,
    },
    'shape': 'continuous translucent blade-fitting film, tapered wake, advecting contour crests',
    'colorField': 'color._Shared.SlowFlowRadiantR2',
    'artApproved': False,
    'runtimeIntegrated': False,
}
destination.mkdir(parents=True, exist_ok=True)
(destination / 'authoring.json').write_text(json.dumps(recipe, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('recipe written:', destination / 'authoring.json')
