#!/usr/bin/env python3
"""本地化键集合一致性检查（只读，不写盘、不编译）。

为什么需要它：zh-Hans / en-US 两份 hjson 是手写的，历史上出现过中文侧把
DisplayName 注释掉（于是回落成类型名）与两份键集合不一致的问题。编译和
服务端夹具都不会因此失败，只有玩家进游戏才看到英文/类型名，所以单独做成静态检查。

检查项：
  1. 两份文件的键路径集合完全一致；
  2. 中文侧不允许残留被注释的 DisplayName 占位行；
  3. 中文侧所有 DisplayName 值必须含汉字，除非在 PENDING_DISPLAY_NAMES 里显式登记；
  4. PENDING_DISPLAY_NAMES 登记的键必须真实存在（避免白名单腐烂）。

用法：python checks/check_localization_keys.py
退出码：0 = PASS，1 = FAIL，2 = 前置文件缺失。
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZH = ROOT / "ZenithReborn/Localization/zh-Hans.hjson"
EN = ROOT / "ZenithReborn/Localization/en-US.hjson"

# 允许「尚未定中文名」的 DisplayName 键路径，必须显式登记完整键路径。
# 当前为空：2026-11-18 的 B-1 已把 6 条弹幕名补齐。将来若新增未定名条目，
# 要么在这里登记（说明是待定），要么直接写中文名，不允许悄悄留英文占位。
PENDING_DISPLAY_NAMES: set[str] = set()

KEY_RE = re.compile(r"^([A-Za-z_][A-Za-z0-9_.]*)\s*:\s*(.*)$")


def parse(path: Path) -> tuple[list[str], dict[str, str], list[tuple[int, str]]]:
    """把 hjson 展平成「键路径序列 / 键路径→值 / 被注释的 DisplayName 行」。

    只认本项目实际用到的三种行：`key: {`（入栈）、`}`（出栈）、`key: value`（值键）。
    不做完整 hjson 语法解析，也不依赖第三方库；遇到不认识的行直接跳过，
    由下面的键集合对照兜底（漏读一个键会被另一份文件比出来）。
    """
    keys: list[str] = []
    values: dict[str, str] = {}
    commented: list[tuple[int, str]] = []
    stack: list[str] = []
    for lineno, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line:
            continue
        if line.startswith("//"):
            if "DisplayName" in line:
                commented.append((lineno, line))
            continue
        if line.startswith("}"):
            if stack:
                stack.pop()
            continue
        match = KEY_RE.match(line)
        if not match:
            continue
        key, value = match.group(1), match.group(2).strip()
        if value == "{":
            stack.append(key)
            continue
        full = ".".join(stack + [key])
        keys.append(full)
        values[full] = value
    return keys, values, commented


def has_han(text: str) -> bool:
    return any("\u4e00" <= ch <= "\u9fff" for ch in text)


def main() -> int:
    for path in (ZH, EN):
        if not path.is_file():
            print(f"[本地化检查] 缺文件：{path}", file=sys.stderr)
            return 2

    zh_keys, zh_values, zh_commented = parse(ZH)
    en_keys, _, _ = parse(EN)
    errors: list[str] = []
    warns: list[str] = []

    print(f"[本地化检查] 中文键 {len(zh_keys)} 条 / 英文键 {len(en_keys)} 条")

    zh_set, en_set = set(zh_keys), set(en_keys)
    missing_in_zh = sorted(en_set - zh_set)
    missing_in_en = sorted(zh_set - en_set)
    if missing_in_zh or missing_in_en:
        errors.append(f"键集合不一致：中文缺 {missing_in_zh}，英文缺 {missing_in_en}")
    else:
        print("  键集合     : 一致")
    if len(zh_keys) != len(en_keys):
        errors.append(f"键数量不同：中文 {len(zh_keys)} / 英文 {len(en_keys)}")
    duplicates = sorted({k for k in zh_keys if zh_keys.count(k) > 1})
    if duplicates:
        errors.append(f"中文侧有重复键：{duplicates}")
    if zh_keys != en_keys:
        warns.append("两份文件的键出现顺序不同（不影响加载，但建议保持同序便于对照）")

    if zh_commented:
        for lineno, line in zh_commented:
            errors.append(f"zh-Hans.hjson:{lineno} 仍有注释占位：{line}")
    else:
        print("  注释占位   : 0 条")

    display_names = [k for k in zh_keys if k.endswith(".DisplayName")]
    bad_names = [k for k in display_names
                 if k not in PENDING_DISPLAY_NAMES and not has_han(zh_values[k])]
    if bad_names:
        errors.append(f"以下 DisplayName 未写中文（或需登记到 PENDING_DISPLAY_NAMES）：{bad_names}")
    else:
        print(f"  中文名     : {len(display_names)} 条全部含汉字（待定白名单 {len(PENDING_DISPLAY_NAMES)} 条）")

    stale = sorted(PENDING_DISPLAY_NAMES - zh_set)
    if stale:
        errors.append(f"PENDING_DISPLAY_NAMES 里登记了不存在的键：{stale}")

    for warn in warns:
        print(f"  WARN {warn}")
    if errors:
        for error in errors:
            print(f"  ERROR {error}")
        print("结论：FAIL")
        return 1
    print("结论：PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
