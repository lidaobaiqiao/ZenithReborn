#!/usr/bin/env python3
"""地图新鲜度校验：判断 docs/ 下的代码地图是否仍跟得上源码。

为什么需要它：地图文档里的版本号、源码文件数、`文件:行号` 都会随代码变动悄悄过期。
这些过期不会让任何测试失败，只会让后来的人按错的行号读到错的代码，所以单独做成一道提交前闸门。

检查项：
  A 索引新鲜度  docs/FILE_INDEX.md 与本次重新生成的结果是否一致（复用 build_file_index.py --check）
  B 行号引用    文档里 `文件:行号` 的文件是否存在、行号是否越界
  C 版本号      docs/CODEBASE_MAP.md 声称的版本是否等于 ZenithReborn/build.txt
  D 源码计数    文档里声称的 `.cs` 文件数是否等于磁盘实际数量
  E 回归项数    文档里写的 `PASS: N focused checks` 是否与 artifacts/wood-check.txt 一致（只警告）

用法：
  python checks/map_freshness.py             # 全量校验，有 ERROR 退出码 1
  python checks/map_freshness.py --no-index-check   # 跳过 A（pre-commit 之外的快速场景）

退出码：0 = PASS，1 = 有 ERROR，2 = 前置文件缺失。
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

INDEX_REL = "docs/FILE_INDEX.md"
BUILD_TXT_REL = "ZenithReborn/build.txt"
MAP_REL = "docs/CODEBASE_MAP.md"
GUIDE_REL = "docs/AI接入指南.md"
REPORT_REL = "artifacts/wood-check.txt"

# 参与「文件:行号」校验的文档
REF_DOCS = [
    "docs/CODEBASE_MAP.md",
    "docs/HOTSPOTS.md",
    "docs/PROBLEM_INDEX.md",
    "docs/ARCHITECTURE.md",
    "docs/FILE_INDEX.md",
]

REF_RE = re.compile(r"`([^`\s]+\.(?:cs|py|ps1|hjson|json|md|fx|fxh)):(\d+)`")
VERSION_RE = re.compile(r"version\s*=\s*([0-9][0-9A-Za-z.\-]*)")
COUNT_RES = [
    re.compile(r"\*\*(\d+)\*\*\s*个"),          # = **121** 个
    re.compile(r"(\d+)\s*个\s*`\.cs`"),          # 121 个 `.cs`
    re.compile(r"(\d+)\s*`\.cs`"),               # 121 `.cs`
]
CHECK_RE = re.compile(r"PASS:\s*(\d+)\s+focused checks")

SKIP_DIRS = {".git", "artifacts", "bin", "obj", "__pycache__", ".vs", "node_modules"}


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def resolve_ref(root: Path, raw: str) -> Path | None:
    """把文档里的引用解析成真实文件。只写文件名的先按 ZenithReborn/ 猜，再全仓库按名找。"""
    direct = root / raw
    if direct.is_file():
        return direct
    name = raw.rsplit("/", 1)[-1]
    guess = root / "ZenithReborn" / name
    if guess.is_file():
        return guess
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and not d.endswith(("-profile", "-playtest"))]
        if name in files:
            return Path(base) / name
    return None


def check_index_freshness(root: Path) -> tuple[bool, str]:
    script = root / "checks" / "build_file_index.py"
    if not script.is_file():
        return False, "找不到 checks/build_file_index.py"
    proc = subprocess.run(
        [sys.executable, str(script), "--check"],
        cwd=str(root),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    detail = (proc.stdout or "").strip().splitlines()
    summary = detail[0] if detail else "(无输出)"
    return proc.returncode == 0, summary


def check_refs(root: Path, errors: list[str]) -> tuple[int, int]:
    """返回 (检查过的引用数, 失败数)。失败明细追加进 errors。"""
    total = failed = 0
    for rel in REF_DOCS:
        path = root / rel
        if not path.is_file():
            continue
        for lineno, line in enumerate(read(path).splitlines(), 1):
            for raw, num in REF_RE.findall(line):
                total += 1
                target = resolve_ref(root, raw)
                if target is None:
                    failed += 1
                    errors.append("%s:%d 文件缺失 `%s`" % (rel, lineno, raw))
                    continue
                try:
                    limit = sum(1 for _ in target.open("r", encoding="utf-8", errors="replace"))
                except OSError as exc:
                    failed += 1
                    errors.append("%s:%d 无法读取 `%s`（%s）" % (rel, lineno, raw, exc))
                    continue
                if int(num) > limit:
                    failed += 1
                    errors.append(
                        "%s:%d 行号越界 `%s:%s`（该文件仅 %d 行）" % (rel, lineno, raw, num, limit)
                    )
    return total, failed


def check_version(root: Path, errors: list[str]) -> tuple[str, str]:
    build = root / BUILD_TXT_REL
    if not build.is_file():
        errors.append("找不到 %s" % BUILD_TXT_REL)
        return "?", "?"
    actual = (VERSION_RE.search(read(build)) or [None, "?"])[1]
    claimed = "?"
    map_file = root / MAP_REL
    if map_file.is_file():
        for line in read(map_file).splitlines():
            m = VERSION_RE.search(line)
            if m:
                claimed = m.group(1)
                break
        if claimed == "?":
            errors.append("%s 里找不到基线版本声明" % MAP_REL)
        elif claimed != actual:
            errors.append("%s 声称基线版本 %s，build.txt 实际为 %s" % (MAP_REL, claimed, actual))
    return claimed, actual


def count_files(directory: Path, pattern: str) -> int:
    return len(list(directory.glob(pattern)))


def count_changelog_entries(root: Path) -> int:
    """CHANGELOG 的版本行数，用于核对「版本索引（N 条）」。"""
    path = root / "docs" / "CHANGELOG.md"
    if not path.is_file():
        return 0
    return sum(1 for line in read(path).splitlines() if line.startswith("| 0.3."))


def check_counts(root: Path, errors: list[str]) -> tuple[int, int, int]:
    """返回 (核对过的声明数, 失败数, 真实源码数)。

    只核对**明确指名目录**的计数：一行里同时出现目录名和 `.cs` 才会被当成该目录的文件数，
    否则会把 `checks/ZenithPrototypeCheck`（夹具）的计数误判成主工程源码数。
    """
    real_main = count_files(root / "ZenithReborn", "*.cs")
    # 目录关键字 → (真实数量, 人类可读名)。夹具名先判，避免 "Zenith" 字样串到主工程。
    scopes = {
        "ZenithPrototypeCheck": (count_files(root / "checks" / "ZenithPrototypeCheck", "*.cs"), "夹具 .cs"),
        "ZenithReborn": (real_main, "源码 .cs"),
    }
    # 目录职责表里写成 `checks/*.py` 54 个 这类形式的计数
    extras = [
        (re.compile(r"`checks/\*\.py`\s*(\d+)\s*个"), "checks 下 .py", count_files(root / "checks", "*.py")),
        (re.compile(r"`checks/\*\.ps1`\s*(\d+)\s*个"), "checks 下 .ps1", count_files(root / "checks", "*.ps1")),
        (re.compile(r"(\d+) 顶层 `\.md`"), "docs 顶层 .md", count_files(root / "docs", "*.md")),
        (re.compile(r"`docs/archive/`\s*(\d+)\s*`\.md`"), "docs/archive .md",
         count_files(root / "docs" / "archive", "**/*.md")),
        (re.compile(r"版本索引（(\d+) 条"), "CHANGELOG 条目", count_changelog_entries(root)),
    ]
    checked = failed = 0
    for rel in (MAP_REL, GUIDE_REL, "CODEBUDDY.md"):
        path = root / rel
        if not path.is_file():
            continue
        for lineno, line in enumerate(read(path).splitlines(), 1):
            if ".cs" in line:
                scope = next((k for k in scopes if k in line), None)
                if scope:
                    actual, label = scopes[scope]
                    for pattern in COUNT_RES:
                        for num in pattern.findall(line):
                            if not num.isdigit():
                                continue
                            checked += 1
                            if int(num) != actual:
                                failed += 1
                                errors.append(
                                    "%s:%d 声称 %s %s 个，实际 %d 个" % (rel, lineno, label, num, actual)
                                )
            for pattern, label, actual in extras:
                for num in pattern.findall(line):
                    checked += 1
                    if int(num) != actual:
                        failed += 1
                        errors.append(
                            "%s:%d 声称 %s %s 个，实际 %d 个" % (rel, lineno, label, num, actual)
                        )
    return checked, failed, real_main


def check_report_count(root: Path, warns: list[str]) -> str:
    report = root / REPORT_REL
    if not report.is_file():
        return "跳过（无回归报告）"
    m = CHECK_RE.search(read(report).splitlines()[0] if read(report) else "")
    if not m:
        return "跳过（报告首行无项数）"
    actual = m.group(1)
    for rel in (MAP_REL, GUIDE_REL, "CODEBUDDY.md"):
        path = root / rel
        if not path.is_file():
            continue
        for lineno, line in enumerate(read(path).splitlines(), 1):
            if "focused checks" not in line:
                continue
            for num in CHECK_RE.findall(line):
                if num != actual:
                    warns.append("%s:%d 写 %s，当前回归为 %s" % (rel, lineno, num, actual))
    return "%s 项" % actual


def main(argv: list[str] | None = None) -> int:
    default_root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description="校验代码地图是否仍跟得上源码（不启游戏、不编译）")
    parser.add_argument("--root", default=str(default_root), help="仓库根目录（默认：脚本上级目录）")
    parser.add_argument("--no-index-check", action="store_true", help="跳过 A 项（索引新鲜度）")
    parser.add_argument("--limit", type=int, default=20, help="最多打印多少条明细（默认 20）")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    if not (root / "ZenithReborn").is_dir():
        print("[地图校验] 仓库根不对，缺 ZenithReborn/：%s" % root, file=sys.stderr)
        return 2

    errors: list[str] = []
    warns: list[str] = []
    print("[地图校验] 仓库根：%s" % root)

    if args.no_index_check:
        print("A 索引新鲜度   : 跳过（--no-index-check）")
    else:
        ok, summary = check_index_freshness(root)
        print("A 索引新鲜度   : %s  %s" % ("OK" if ok else "过期", summary))
        if not ok:
            errors.append("FILE_INDEX 已过期：跑 python checks/build_file_index.py 重生成")

    total, failed = check_refs(root, errors)
    print("B 行号引用     : 检查 %d 处，失败 %d 处" % (total, failed))

    claimed, actual = check_version(root, errors)
    print("C 版本号       : %s = %s  %s" % (claimed, actual, "OK" if claimed == actual else "不一致"))

    checked, count_failed, real = check_counts(root, errors)
    print("D 源码计数     : 核对 %d 处声明，实际 %d 个 .cs  %s"
          % (checked, real, "OK" if count_failed == 0 else "不一致"))

    print("E 回归项数     : %s%s" % (check_report_count(root, warns), "  WARN" if warns else "  OK"))

    for w in warns[: args.limit]:
        print("     WARN %s" % w)
    if len(warns) > args.limit:
        print("     ……还有 %d 条警告" % (len(warns) - args.limit))

    if errors:
        print("")
        print("明细（%d 条，最多显示 %d 条）：" % (len(errors), args.limit))
        for e in errors[: args.limit]:
            print("  ERROR %s" % e)
        if len(errors) > args.limit:
            print("  ……还有 %d 条" % (len(errors) - args.limit))
        print("结论：FAIL")
        return 1

    print("结论：PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
