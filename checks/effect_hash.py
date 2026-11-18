"""着色器文本源码的哈希口径：统一按 LF 归一化后计算。

仓库中的 .fx / .fxh 以 LF 入库；Windows 检出受 core.autocrlf 影响可能是 CRLF。
若直接对磁盘原始字节取哈希，同一份源码在不同换行下会得到不同的值，
导致 build-manifest.json 每次回归后漂移，也会让打包校验误判。
编译产物 .fxc 是二进制，仍按原始字节取哈希，不使用本模块。
"""
import hashlib


def normalized_source(data: bytes) -> bytes:
    """把文本着色器源码的换行统一为 LF。"""
    return data.replace(b'\r\n', b'\n')


def source_sha256(data: bytes) -> str:
    """返回 .fx / .fxh 文本源码在 LF 归一化口径下的 sha256 十六进制串。"""
    return hashlib.sha256(normalized_source(data)).hexdigest()
