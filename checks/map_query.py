#!/usr/bin/env python3
"""ZenithReborn 文件索引只读检索（纯标准库，不写文件、不编译、不启游戏）。

一、用途
    `docs/FILE_INDEX.md` 是脚本生成的六列文件索引（文件路径 / 类型 / 所属模块 /
    文件职责 / 关键关联 / 分析状态）。改代码前想"这个功能在哪个文件"时，
    直接 grep 全库既慢又会被素材与归档噪音淹没。本脚本把索引读进内存，
    按关键词或模块给出"最可能相关的几个文件"，只读不写。

二、数据源
    只读 `docs/FILE_INDEX.md`。六张表列序一致，表头行与 `|---|` 分隔行跳过；
    路径列去掉反引号、统一正斜杠。索引缺失时退出码 2，并提示重新生成命令。

三、排序口径（关键词模式）
    多关键词取并集，按命中位置加权，同一位置只计一次：
        路径命中 3 分 / 模块命中 2 分 / 职责或关联命中 1 分
    例：某文件的路径与模块都命中，得 5 分；只在职责里提到，得 1 分。
    同分按路径字典序。`--module` 与 `--file` 是过滤模式，命中项固定 2 分 / 3 分。

四、退出码
    0  有命中，或无命中（无命中只打印提示，不算错误）
    2  仓库根或 docs/FILE_INDEX.md 缺失

五、用法
    python checks/map_query.py 飞刃
    python checks/map_query.py 木质 羁绊 --limit 20
    python checks/map_query.py --module 木质族
    python checks/map_query.py --file FlyingBlade
    python checks/map_query.py 诅咒 --limit 5 --json

    注：`--json` 的条目数同样受 --limit 约束（默认 12）；需要全量请显式给大 --limit。
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# 索引文件相对仓库根的位置（与 checks/map_audit.py 的 INDEX_REL 保持一致）
INDEX_REL = "docs/FILE_INDEX.md"

# 表头第一列的写法；两种都要跳过，避免把表头当数据行
HEADER_NAMES = ("文件路径", "路径")

# 模块列为空时的展示名（tools/ 下有一批未归模块的条目）
UNGROUPED = "未分组"

# 职责列截断宽度（约 50 个汉字，超出补省略号）
DUTY_MAX = 50

# 关键词模式下三类命中的权重
W_PATH = 3
W_MODULE = 2
W_BODY = 1

# 生成命令，写进提示里，保证提示可直接照抄
BUILD_CMD = "python checks/build_file_index.py"


def split_row(line: str) -> list[str]:
    """把一行 Markdown 表格拆成单元格（去掉首尾竖线）。"""
    s = line.strip()
    if s.startswith("|"):
        s = s[1:]
    if s.endswith("|"):
        s = s[:-1]
    return [c.strip() for c in s.split("|")]


def clean_path(cell: str) -> str:
    """路径单元格归一化：去反引号、反斜杠改正斜杠、去掉前导 ./。"""
    s = cell.strip().strip("`").strip().replace("\\", "/")
    while s.startswith("./"):
        s = s[2:]
    return s


def is_separator(cells: list[str]) -> bool:
    """判断是否为 `|---|---|` 这类分隔行（每格只由 -、: 和空白组成）。"""
    return bool(cells) and all(c and not c.strip("-: ") for c in cells)


def load_index(root: Path) -> list[dict]:
    """读取索引，返回条目列表（已按路径去重，保持文件内顺序）。"""
    index_path = root / INDEX_REL
    entries: list[dict] = []
    seen: set[str] = set()
    for raw in index_path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line.startswith("|"):
            continue
        cells = split_row(line)
        # 六列固定；列数不符的行（说明文字里的零散竖线）直接跳过
        if len(cells) != 6:
            continue
        first = cells[0].strip().strip("`").strip()
        if not first or first in HEADER_NAMES or is_separator(cells):
            continue
        # 以 ( 开头的是统计行（如 "(合计)"）与分组索引行，不是可检索的文件
        if first.startswith("("):
            continue
        path = clean_path(cells[0])
        if not path or path in seen:
            continue
        seen.add(path)
        entries.append(
            {
                "path": path,
                "type": cells[1],
                "module": cells[2],
                "duty": cells[3],
                "rel": cells[4],
                "status": cells[5],
            }
        )
    return entries


def keyword_score(entry: dict, keywords: list[str]) -> int:
    """按命中位置加权计分；同一位置多个关键词命中只算一次。0 表示未命中。"""
    path = entry["path"].casefold()
    module = entry["module"].casefold()
    body = (entry["duty"] + " " + entry["rel"]).casefold()
    score = 0
    if any(k in path for k in keywords):
        score += W_PATH
    if any(k in module for k in keywords):
        score += W_MODULE
    if any(k in body for k in keywords):
        score += W_BODY
    return score


def dir_tier(path: str) -> int:
    """目录优先级：源码 > 夹具 > 其他 > 文档。

    同分时源码排在文档前面——查一个功能时先要看到实现文件，而不是历史设计稿。
    """
    p = path.casefold()
    if p.startswith("zenithreborn/assets/") or p.startswith("vfxlibrary/"):
        return 1
    if p.startswith("zenithreborn/"):
        return 3
    if p.startswith("checks/"):
        return 2
    if p.startswith("docs/"):
        return 0
    return 1


def select(
    entries: list[dict], keywords: list[str], module: str, file_pat: str
) -> list[dict]:
    """按模式筛选并计分，返回按 (分数降序, 目录优先级降序, 路径升序) 排好的结果。"""
    picked: list[dict] = []
    for entry in entries:
        if module and module not in entry["module"].casefold():
            continue
        if file_pat and file_pat not in entry["path"].casefold():
            continue
        if keywords:
            score = keyword_score(entry, keywords)
            if score <= 0:
                continue
        elif module:
            score = W_MODULE
        elif file_pat:
            score = W_PATH
        else:
            continue
        item = dict(entry)
        item["score"] = score
        picked.append(item)
    # 中文关键词能命中 docs 的路径与模块名，却命不中英文源码文件名；
    # 因此排序把目录优先级折算成加权分，避免查功能时文档把源码压到后面。
    picked.sort(key=lambda e: (-(e["score"] + 2 * dir_tier(e["path"])), e["path"]))
    return picked


def short_duty(duty: str) -> str:
    """职责截断到 DUTY_MAX 个字符，超出补省略号；空值给占位符。"""
    text = duty.strip()
    if not text:
        return "（索引未写职责）"
    return text if len(text) <= DUTY_MAX else text[:DUTY_MAX] + "…"


def module_total(entries: list[dict], name: str) -> int:
    """某模块（精确名）在整份索引里的文件总数。"""
    return sum(1 for e in entries if e["module"] == name)


def print_report(hits: list[dict], entries: list[dict], limit: int) -> None:
    """人类可读输出：每行 模块 | 文件 | 职责，末行给命中模块与模块总量。"""
    for entry in hits[:limit]:
        module = entry["module"] or UNGROUPED
        print("%s | %s | %s" % (module, entry["path"], short_duty(entry["duty"])))

    # 命中模块按命中数降序，最多列 6 个，避免末行被长尾模块撑爆
    counts: dict[str, int] = {}
    for entry in hits:
        name = entry["module"] or UNGROUPED
        counts[name] = counts.get(name, 0) + 1
    order = sorted(counts.items(), key=lambda kv: (-kv[1], kv[0]))
    parts = [
        "%s 共 %d 个" % (name, module_total(entries, "" if name == UNGROUPED else name))
        for name, _ in order[:6]
    ]
    if len(order) > 6:
        parts.append("另 %d 个模块" % (len(order) - 6))
    footer = "命中 %d 个文件（%s）" % (len(hits), "，".join(parts))
    if len(hits) > limit:
        footer += "，仅显示前 %d 条" % limit
    print(footer)


def print_miss(subject: str, stream=None) -> None:
    """无命中：按约定文案提示，退出码仍是 0；--json 时改走 stderr 以保证 stdout 是纯 JSON。"""
    print(
        "未命中：%s。可能该功能尚未入索引——改完源码请跑 %s 重生成。"
        % (subject, BUILD_CMD),
        file=stream or sys.stdout,
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="ZenithReborn 文件索引只读检索（不写文件、不启游戏）",
        epilog="示例：python checks/map_query.py 飞刃 --limit 5",
    )
    parser.add_argument("keywords", nargs="*", help="关键词，可多个，取并集后加权排序")
    parser.add_argument("--module", default="", help="按所属模块子串过滤，列出该模块全部文件")
    parser.add_argument("--file", dest="file_pat", default="", help="只按文件路径子串匹配")
    parser.add_argument("--limit", type=int, default=12, help="最多显示多少条（默认 12）")
    parser.add_argument("--json", action="store_true", help="输出 JSON 数组，便于其他脚本消费")
    parser.add_argument(
        "--root", default=str(Path(__file__).resolve().parent.parent),
        help="仓库根目录（默认：脚本上级目录）",
    )
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    index_path = root / INDEX_REL
    if not index_path.is_file():
        print("[地图检索] 找不到索引文件：%s" % index_path, file=sys.stderr)
        print("[地图检索] 请先运行 %s 生成索引。" % BUILD_CMD, file=sys.stderr)
        return 2

    keywords = [k.casefold() for k in args.keywords if k.strip()]
    module = args.module.strip().casefold()
    file_pat = args.file_pat.strip().casefold()

    if not keywords and not module and not file_pat:
        parser.print_usage(sys.stderr)
        print("[地图检索] 至少给一个关键词，或 --module / --file。", file=sys.stderr)
        return 0

    entries = load_index(root)
    hits = select(entries, keywords, module, file_pat)

    if not hits:
        subject = (
            " ".join(args.keywords) if keywords
            else ("模块 %s" % args.module if module else "路径 %s" % args.file_pat)
        )
        if args.json:
            print("[]")
            print_miss(subject, sys.stderr)
        else:
            print_miss(subject)
        return 0

    limit = max(1, args.limit)
    if args.json:
        payload = [
            {
                "path": e["path"],
                "type": e["type"],
                "module": e["module"],
                "duty": e["duty"],
                "rel": e["rel"],
                "status": e["status"],
                "score": e["score"],
            }
            for e in hits[:limit]
        ]
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return 0

    print_report(hits, entries, limit)
    return 0


if __name__ == "__main__":
    sys.exit(main())
