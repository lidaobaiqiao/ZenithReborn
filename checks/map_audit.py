#!/usr/bin/env python3
"""ZenithReborn 仓库文件索引覆盖率审计（纯标准库，不启游戏、不编译、不写文件）。

一、用途
    `docs/FILE_INDEX.md` 是仓库唯一的人工文件索引。本脚本回答两个问题：
      1. 磁盘上"应该被索引的自有文件"，有多少真的在索引里（覆盖率）；
      2. 索引里写的路径，有多少在磁盘上已经不存在（失效路径，通常是文件被删除或改名后索引没跟上）。

二、分母口径（应被索引的自有文件）
    从仓库根递归扫描所有文件，剔除下列目录后剩下的就是分母。
    判定只看"相对仓库根的 posix 路径"，不看文件名内容。

三、为什么这些目录不进分母
    - artifacts/            构建、安装、备份、调查报告产物；每次跑都会变，索引它没有意义。
    - */bin/、*/obj/        编译中间与输出；由源码单方面决定，可随时重建。
    - *-profile/            tModLoader 隔离运行目录（7 个），内含运行时生成的配置与日志。
    - *-playtest/           同上，用于 playtest 存档隔离。
    - .git/                 版本库元数据。
    - .workbuddy/、.codebuddy/  编辑器/代理工具配置，不是项目资产。
    - .smoke-user-data/     冒烟测试生成的 Chromium 用户数据（GPUCache/Code Cache 等），纯运行时残留。
    - VfxLibrary/           11756 个原版素材文件，改由 docs/archive/codebase-map-survey-2026-11-03/
                            vfx-library-manifest.txt 单独核对（见第五部分），不并入主分母，
                            否则一个目录就淹没其余全部文件。
    - tools/ilspy/          第三方 ILSpy 二进制发行包（68 个），按"第三方-未深入"整体记录。
    - tools/xnbcli/         第三方 xnbcli 源码（60 个），同上。
    - tools/xnbcli-binary/  第三方 xnbcli 编译版 + 上游 git 元数据（36 个），同上。

四、判定规则
    1. 索引行 = `docs/FILE_INDEX.md` 里所有以 `|` 开头的表格行，取第一列、去掉反引号与首尾空白。
    2. 第一列以 `(` 开头的是纯统计行（如 `(合计)`）与分组索引行（如 `(分组) checks/archived-oneoff/`），忽略。
    3. 第一列当前是目录（索引允许为目录写汇总行）时忽略，不算失效。
    4. 其余第一列作为"已索引文件路径"，必须能命中磁盘文件，否则记为失效路径。
    5. 覆盖率 = |分母 ∩ 已索引| / |分母|。交叉索引（一行只算一次）不影响分母。
    6. 分组索引：GROUP_MANIFESTS 里登记的清单 txt 会被展开成成员路径，一并算作已索引
       （分母不变）。只用于"同类批量、逐行列会淹没索引"的目录，清单里出现不属于
       登记范围的路径会告警。

五、VfxLibrary 的单独核对
    清单文件 vfx-library-manifest.txt 是 VfxLibrary 的逐文件快照
    （11756 行，每行一个相对路径）。本脚本比对"清单行数 / 去重行数 / 磁盘实际文件数"，
    不一致只警告，不影响退出码（该目录本来就不进主分母）。
    清单与分组清单都优先读 docs/archive/codebase-map-survey-2026-11-03/（入库的只读副本），
    该目录不存在时才回退 artifacts/codebase-map/（被 .gitignore 排除的历史产物位置）。

六、退出码
    0  覆盖率 100% 且无失效路径
    1  覆盖率 < 100% 或存在失效路径
    2  仓库根或 docs/FILE_INDEX.md 缺失、无法解析

七、用法
    python checks/map_audit.py                # 全量体检
    python checks/map_audit.py --limit 200    # 未索引清单最多列 200 条（默认 50）
    python checks/map_audit.py --strict-vfx   # VfxLibrary 清单不一致也返回 1
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# 排除规则（改口径只能改这里，输出里会原样打印，保证口径可审计）
# ---------------------------------------------------------------------------

# 相对仓库根的一级目录：整目录排除
EXCLUDE_ROOT_DIRS = {
    "artifacts": "构建 / 安装 / 备份 / 调查报告产物",
    ".git": "版本库元数据",
    ".workbuddy": "编辑器与代理工具配置",
    ".codebuddy": "编辑器与代理工具配置",
    ".smoke-user-data": "冒烟测试生成的 Chromium 用户数据（运行时残留）",
    "VfxLibrary": "原版素材库，改由 vfx-library-manifest.txt 单独核对",
}

# 任意层级出现即整棵排除的目录名
EXCLUDE_ANY_DIRS = {
    "bin": "编译输出",
    "obj": "编译中间产物",
}

# 目录名后缀：tModLoader 隔离运行目录
EXCLUDE_DIR_SUFFIXES = {
    "-profile": "tModLoader 隔离运行目录（运行时生成配置与日志）",
    "-playtest": "playtest 隔离存档目录",
}

# 相对仓库根的路径前缀：第三方工具，按整体记录，不逐个进分母
EXCLUDE_PREFIXES = {
    "tools/ilspy": "第三方 ILSpy 发行包（记录为“第三方-未深入”）",
    "tools/xnbcli": "第三方 xnbcli 源码（记录为“第三方-未深入”）",
    "tools/xnbcli-binary": "第三方 xnbcli 编译版 + 上游 git 元数据（记录为“第三方-未深入”）",
}

# 索引文件与素材清单的固定位置
INDEX_REL = "docs/FILE_INDEX.md"
VFX_DIR_REL = "VfxLibrary"

# 建图调查产物（清单 txt）的入库位置与历史回退位置。
# 优先 docs/archive/ 的只读副本：artifacts/ 被 .gitignore 排除，克隆仓库后读不到，
# 那样分组索引的 20 个成员会被判成"未索引"，覆盖率假性下跌。
SURVEY_DIR_REL = "docs/archive/codebase-map-survey-2026-11-03"
ARTIFACTS_DIR_REL = "artifacts/codebase-map"


def survey_dir(root: Path) -> Path:
    """清单文件的所在目录：优先入库副本，缺失时回退 artifacts/codebase-map/。"""
    archived = root / SURVEY_DIR_REL
    return archived if archived.is_dir() else root / ARTIFACTS_DIR_REL


VFX_MANIFEST_NAME = "vfx-library-manifest.txt"

# 分组索引清单：索引里不逐文件列，只写一行"分组索引"，成员写进清单 txt。
# 用在"同类批量、逐行列会淹没索引"的目录上，每个成员仍算已索引（分母不变）。
# 加清单的唯一理由就是这一类；不要用它把散落的真实源文件藏进去。
GROUP_MANIFESTS = {
    "archived-oneoff-manifest.txt": "checks/archived-oneoff/",
}


# ---------------------------------------------------------------------------
# 分母：扫描磁盘
# ---------------------------------------------------------------------------

def is_excluded_path(rel: str) -> str:
    """判断相对路径是否被排除；返回排除原因，未排除返回空串。"""
    parts = rel.split("/")
    head = parts[0]
    if head in EXCLUDE_ROOT_DIRS:
        return "%s/ —— %s" % (head, EXCLUDE_ROOT_DIRS[head])
    for name in parts[:-1]:
        if name in EXCLUDE_ANY_DIRS:
            return "%s/ —— %s" % (name, EXCLUDE_ANY_DIRS[name])
        for suffix, why in EXCLUDE_DIR_SUFFIXES.items():
            if name.endswith(suffix):
                return "%s/ —— %s" % (name, why)
    for prefix, why in EXCLUDE_PREFIXES.items():
        if rel == prefix or rel.startswith(prefix + "/"):
            return "%s/ —— %s" % (prefix, why)
    return ""


def collect_targets(root: Path) -> list[str]:
    """返回应被索引的自有文件（相对根、posix 分隔、已排序）。"""
    out: list[str] = []
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(root).as_posix()
        if is_excluded_path(rel):
            continue
        out.append(rel)
    out.sort()
    return out


def count_raw_files(base: Path) -> int:
    """不做任何排除，统计目录下真实文件数。"""
    if not base.is_dir():
        return 0
    return sum(1 for p in base.rglob("*") if p.is_file())


# ---------------------------------------------------------------------------
# 索引解析
# ---------------------------------------------------------------------------

def index_cells(line: str) -> list[str]:
    s = line.strip()
    if s.startswith("|"):
        s = s[1:]
    if s.endswith("|"):
        s = s[:-1]
    return [c.strip() for c in s.split("|")]


def parse_index(root: Path) -> tuple[list[str], int, int]:
    """返回 (已索引路径列表(保序去重), 忽略的统计行数, 忽略的目录行数)。"""
    index_path = root / INDEX_REL
    paths: list[str] = []
    seen: set[str] = set()
    stat_rows = 0
    dir_rows = 0
    for raw in index_path.read_text(encoding="utf-8").splitlines():
        if not raw.strip().startswith("|"):
            continue
        cells = index_cells(raw)
        if not cells:
            continue
        first = cells[0].strip().strip("`").strip()
        if not first or set(first) <= set("-: "):
            continue
        if first in ("文件路径", "路径", "目录", "分组"):
            continue
        if first.startswith("("):          # 纯统计行 / 分组索引行
            stat_rows += 1
            continue
        normalized = first.replace("\\", "/")
        while normalized.startswith("./"):
            normalized = normalized[2:]
        if (root / normalized).is_dir():   # 汇总行，允许指向目录
            dir_rows += 1
            continue
        if normalized not in seen:
            seen.add(normalized)
            paths.append(normalized)
    return paths, stat_rows, dir_rows


def load_group_manifests(root: Path) -> tuple[set[str], list[str]]:
    """展开分组索引清单，返回 (清单里的路径集合, 说明行)。"""
    members: set[str] = set()
    notes: list[str] = []
    for name, scope in GROUP_MANIFESTS.items():
        manifest = survey_dir(root) / name
        rel = manifest.relative_to(root).as_posix()
        if not manifest.is_file():
            notes.append("清单缺失：%s（应覆盖 %s）" % (rel, scope))
            continue
        rows = [ln.strip() for ln in manifest.read_text(encoding="utf-8").splitlines() if ln.strip()]
        bad = [r for r in rows if not r.startswith(scope)]
        if bad:
            notes.append("清单越界：%s 含 %d 条不属于 %s 的路径" % (rel, len(bad), scope))
        members.update(rows)
        notes.append("清单展开：%s → %s %d 条" % (rel, scope, len(rows)))
    return members, notes


# ---------------------------------------------------------------------------
# VfxLibrary 清单核对
# ---------------------------------------------------------------------------

def check_vfx_manifest(root: Path) -> tuple[int, int, int, str]:
    """返回 (清单总行数, 清单去重行数, 磁盘文件数, 说明)。"""
    manifest = survey_dir(root) / VFX_MANIFEST_NAME
    disk = count_raw_files(root / VFX_DIR_REL)
    if not manifest.is_file():
        return 0, 0, disk, "清单文件缺失：%s" % manifest.relative_to(root).as_posix()
    lines = [ln.strip() for ln in manifest.read_text(encoding="utf-8").splitlines() if ln.strip()]
    return len(lines), len(set(lines)), disk, ""


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------

def main(argv: list[str] | None = None) -> int:
    default_root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description="ZenithReborn 文件索引覆盖率审计（不启游戏、不编译）")
    parser.add_argument("--root", default=str(default_root), help="仓库根目录（默认：脚本上级目录）")
    parser.add_argument("--limit", type=int, default=50, help="未索引清单最多打印多少条（默认 50）")
    parser.add_argument("--strict-vfx", action="store_true", help="VfxLibrary 清单不一致也返回 1")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    if not (root / INDEX_REL).is_file():
        print("[索引审计] 找不到索引文件：%s" % (root / INDEX_REL), file=sys.stderr)
        return 2

    print("[索引审计] 仓库根：%s" % root)
    print()

    # --- 一、口径 ---
    print("一、分母口径（应被索引的自有文件）")
    print("    从仓库根递归扫描，剔除下列目录后剩下的全部文件：")
    for name, why in EXCLUDE_ROOT_DIRS.items():
        print("      排除  %-20s %s" % (name + "/", why))
    for name, why in EXCLUDE_ANY_DIRS.items():
        print("      排除  %-20s %s" % ("*/" + name + "/", why))
    for suffix, why in EXCLUDE_DIR_SUFFIXES.items():
        print("      排除  %-20s %s" % ("*" + suffix + "/", why))
    for prefix, why in EXCLUDE_PREFIXES.items():
        print("      排除  %-20s %s" % (prefix + "/", why))
    print("    分母之外单独核对：%s/（见第五部分）" % VFX_DIR_REL)
    print()

    # --- 二、扫描 ---
    targets = collect_targets(root)
    indexed, stat_rows, dir_rows = parse_index(root)
    group_members, group_notes = load_group_manifests(root)
    target_set = set(targets)
    indexed_set = set(indexed) | group_members

    covered = target_set & indexed_set
    unindexed = [p for p in targets if p not in indexed_set]
    invalid = [p for p in indexed if not (root / p).is_file()]

    total = len(targets)
    found = len(covered)
    coverage = (found * 100.0 / total) if total else 100.0

    print("二、覆盖统计")
    print("    应索引总数 : %d" % total)
    print("    已索引     : %d" % found)
    print("    覆盖率     : %.2f%%" % coverage)
    print("    索引行合计 : %d（含目录汇总行 %d、统计行 %d）" % (len(indexed) + dir_rows + stat_rows, dir_rows, stat_rows))
    if group_members:
        print("    分组清单   : %d 条（逐行列从略，成员在 %s/ 的清单里）" % (len(group_members), survey_dir(root).relative_to(root).as_posix()))
        for note in group_notes:
            print("      %s" % note)
    print()

    # --- 三、未索引 ---
    print("三、未索引文件（前 %d 条 / 共 %d 条）" % (args.limit, len(unindexed)))
    if not unindexed:
        print("    无，分母全部被索引覆盖。")
    else:
        for rel in unindexed[: args.limit]:
            print("    缺失  %s" % rel)
        if len(unindexed) > args.limit:
            print("    …… 其余 %d 条从略（--limit 可调）" % (len(unindexed) - args.limit))
    print()

    # --- 四、失效路径 ---
    print("四、失效路径（索引里有、磁盘上没有）：%d 条" % len(invalid))
    if not invalid:
        print("    无。")
    else:
        for rel in invalid:
            print("    失效  %s" % rel)
    print()

    # --- 五、VfxLibrary ---
    m_total, m_uniq, m_disk, note = check_vfx_manifest(root)
    print("五、%s 清单核对（不进主分母）" % VFX_DIR_REL)
    if note:
        print("    %s" % note)
    print("    清单行数   : %d" % m_total)
    print("    清单去重   : %d" % m_uniq)
    print("    磁盘实际   : %d" % m_disk)
    vfx_ok = (m_total == m_uniq == m_disk) and bool(m_total)
    print("    结论       : %s" % ("一致" if vfx_ok else "不一致，需重建清单"))
    print()

    # --- 六、结论 ---
    # 退出码必须与打印的结论一致：否则 CI / 脚本调用无法感知失败（曾出现"结果：FAIL"却退 0）。
    ok = total > 0 and not unindexed and not invalid
    exit_code = 0 if ok else 1
    print("六、结论")
    print("    覆盖率 %.2f%%（%d/%d），未索引 %d，失效路径 %d" % (coverage, found, total, len(unindexed), len(invalid)))
    print("    结果：%s" % ("PASS" if ok else "FAIL"))
    if ok and not vfx_ok and args.strict_vfx:
        print("    --strict-vfx 生效：清单不一致视为失败")
        exit_code = 1
    print("    退出码：%d" % exit_code)
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
