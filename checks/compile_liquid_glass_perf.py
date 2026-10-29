"""只编译独立液态玻璃性能基线，不修改生产材质或生产构建清单。"""
import ctypes
from pathlib import Path

source = Path(__file__).resolve().parent / 'ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassPerfBase.fx'
data = source.read_bytes()
compiler = ctypes.WinDLL('d3dcompiler_43.dll').D3DCompile
compiler.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_char_p, ctypes.c_void_p,
                     ctypes.c_void_p, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint,
                     ctypes.c_uint, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_void_p)]
compiler.restype = ctypes.c_long

def read_blob(pointer):
    if not pointer.value:
        return b''
    table = ctypes.cast(pointer, ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
    address = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(table[3])(pointer)
    size = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(table[4])(pointer)
    try:
        return ctypes.string_at(address, size)
    finally:
        ctypes.WINFUNCTYPE(ctypes.c_uint, ctypes.c_void_p)(table[2])(pointer)

code, errors = ctypes.c_void_p(), ctypes.c_void_p()
# 本机对照显示包含预着色计算的版本无法正确选择输出模式；
# 禁用预着色计算后同一源码在隔离 GPU 夹具中可正常输出。
result = compiler(data, len(data), b'LiquidGlassPerfBase.fx', None, None, None, b'fx_2_0',
                  (1 << 15) | (1 << 8), 0, ctypes.byref(code), ctypes.byref(errors))
diagnostics = read_blob(errors).decode('utf-8', errors='replace').strip('\0\r\n')
compiled = read_blob(code)
if result < 0 or not compiled:
    raise RuntimeError(diagnostics or '液态玻璃性能基线编译失败')
if diagnostics:
    print(diagnostics)
source.with_suffix('.fxc').write_bytes(compiled)
print(f'{source.name} -> {source.with_suffix(".fxc").name}: {len(compiled)} bytes')

