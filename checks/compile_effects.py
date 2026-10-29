"""使用本机微软编译器编译项目自己的Effect；玩家运行只需要包内编译结果。"""
import ctypes
import hashlib
import json
import re
from pathlib import Path

root=Path(__file__).resolve().parent.parent
folder=root/'ZenithReborn/Assets/Effects'
library=ctypes.WinDLL('d3dcompiler_43.dll')
compile_effect=library.D3DCompile
compile_effect.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_char_p,ctypes.c_void_p,
    ctypes.c_void_p,ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint,ctypes.c_uint,
    ctypes.POINTER(ctypes.c_void_p),ctypes.POINTER(ctypes.c_void_p)]
compile_effect.restype=ctypes.c_long

def blob(pointer):
    if not pointer.value:
        return b''
    table=ctypes.cast(pointer,ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
    address=ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(table[3])(pointer)
    size=ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(table[4])(pointer)
    try:
        return ctypes.string_at(address,size)
    finally:
        ctypes.WINFUNCTYPE(ctypes.c_uint,ctypes.c_void_p)(table[2])(pointer)

records=[]
for source in sorted(folder.glob('*.fx')):
    data=source.read_bytes()
    # 只展开明确登记的自有模块；依赖哈希随包记录，不允许目录穿越或第三方包含。
    inputs=[]
    def include(match):
        name=match.group(1).decode('ascii')
        if name not in {'ZRSwordSpaceWarp.fxh','ZRClearGlass.fxh','ZRSwordHighlights.fxh','ZRLiquidGlass.fxh'}:
            raise RuntimeError(f'{source.name}: 未登记的着色器模块 {name}')
        part=(folder/name).read_bytes()
        if b'#include' in part:raise RuntimeError(f'{name}: 不允许间接包含')
        inputs.append({'source':name,'sourceSha256':hashlib.sha256(part).hexdigest()})
        return part
    expanded=re.sub(rb'^[ \t]*#include\s+"([^"]+)"[ \t]*\r?$',include,data,flags=re.MULTILINE)
    if b'#include' in expanded:raise RuntimeError(f'{source.name}: 无效包含指令')
    code=ctypes.c_void_p();errors=ctypes.c_void_p()
    result=compile_effect(expanded,len(expanded),source.name.encode('utf-8'),None,None,None,
        b'fx_2_0',1<<15,0,ctypes.byref(code),ctypes.byref(errors))
    diagnostics=blob(errors).decode('utf-8',errors='replace').strip('\0\r\n')
    compiled=blob(code)
    if result<0 or not compiled:
        raise RuntimeError(f'{source.name}: {diagnostics}')
    if diagnostics:
        print(diagnostics)
    target=source.with_suffix('.fxc')
    # UI 构建仍验证着色器编译结果，但不反复覆盖完全相同的生产二进制。
    if not target.exists() or target.read_bytes()!=compiled:
        target.write_bytes(compiled)
    records.append({'source':source.name,'sourceSha256':hashlib.sha256(data).hexdigest(),
        'compiled':target.name,'compiledSha256':hashlib.sha256(compiled).hexdigest(),
        'origin':'project-authored','compiler':'Microsoft D3DCompile / d3dcompiler_43 / fx_2_0',
        'thirdPartySourceIncludes':[],'projectSourceIncludes':inputs})
    print(f'{source.name} -> {target.name}: {len(compiled)} bytes')
(folder/'build-manifest.json').write_text(json.dumps({'effects':records},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
