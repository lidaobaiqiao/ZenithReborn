"""只读校验 build-manifest.json：记录集合、文本源码哈希口径与编译产物哈希。

不写盘、不编译、不启动游戏；可单独运行，也可作为回归后的生成物守护。
文本源码 .fx / .fxh 统一按 LF 归一化取哈希（见 effect_hash），
编译产物 .fxc 是二进制，按原始字节取哈希。
"""
import hashlib
import json
from pathlib import Path

from effect_hash import source_sha256

root = Path(__file__).resolve().parent.parent
folder = root / 'ZenithReborn/Assets/Effects'


def main() -> None:
    entries = json.loads((folder / 'build-manifest.json').read_text(encoding='utf-8'))['effects']
    declared_sources = {entry['source'] for entry in entries}
    actual_sources = {path.name for path in folder.glob('*.fx')}
    assert declared_sources == actual_sources, (
        f'清单与目录不一致：清单={sorted(declared_sources)}，目录={sorted(actual_sources)}')

    for entry in entries:
        source = folder / entry['source']
        assert source.exists(), f'清单指向的源码不存在：{source.name}'
        assert source_sha256(source.read_bytes()) == entry['sourceSha256'], (
            f'{source.name}: sourceSha256 不是 LF 归一化口径（工作区换行或源码已变）')
        compiled = folder / entry['compiled']
        assert compiled.exists(), f'清单指向的编译产物不存在：{compiled.name}'
        assert hashlib.sha256(compiled.read_bytes()).hexdigest() == entry['compiledSha256'], (
            f'{compiled.name}: compiledSha256 与实际字节不符')
        for dependency in entry.get('projectSourceIncludes', []):
            name = dependency['source']
            path = folder / name
            assert path.exists(), f'清单指向的依赖不存在：{name}'
            assert source_sha256(path.read_bytes()) == dependency['sourceSha256'], (
                f'{name}: 依赖哈希不是 LF 归一化口径')

    print(f'结论：PASS（{len(entries)} 条着色器记录，文本源码按 LF 归一化，编译产物按原始字节）')


if __name__ == '__main__':
    main()
