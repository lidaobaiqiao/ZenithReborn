#!/usr/bin/env python3
"""把建图调查报告合并成 docs/FILE_INDEX.md。

输入目录优先 `docs/archive/codebase-map-survey-2026-11-03/`（入库的只读副本），
该目录不存在时回退 `artifacts/codebase-map/`（历史产物位置，被 .gitignore 排除）。
两者内容相同；用入库副本是为了让"克隆后重建索引"这件事可复现。

一、为什么要脚本
    9 份调查报告（A~I）各自带一张六列索引表，合计约 690 行。手抄会漂移、会漏、
    会与报告不一致。本脚本每次从报告原文重新抽取，索引永远是报告的投影。

二、抽取规则（对格式差异容错）
    1. 只在"表头第一列是 `文件路径` / `路径`"的表格里取行；表格以外的一切表格
       （开关表、分组统计表、覆盖矩阵）一律不碰。
    2. 列名按表头映射，不按位置硬编码：
         文件路径/路径 → 路径   类型 → 类型   所属模块 → 所属模块
         文件职责/职责 → 职责   关键关联 → 关联   分析状态/状态 → 状态
       报告只给 3 列（文件路径/类型/职责）时，其余三列留空。
    3. 首列清洗：去掉反引号；`a` / `b` 这类多文件行只取第一个代码段，
       其后（`+ .Abstractions`、`（3 个）`）视为说明文字丢弃。
    4. 路径解析顺序：已有根前缀（ZenithReborn/ checks/ docs/ tools/ VfxLibrary/）
       直接采信；否则套该报告的默认前缀（F 组用 checks/ZenithPrototypeCheck/）；
       含 `*` 的按 glob 展开成逐个成员行（成员各自成行，职责沿用分组行）；
       含 `...` 的省略写法（I 组第三方包）不猜路径，改为继承上一行的目录出一行
       "目录分组"；解析不到磁盘实体的路径不写进索引，只在运行时告警。

三、合并规则
    - 同一个路径在多个报告里出现：保留"职责"最长的一条，状态列追加 `多代理交叉`。
    - 分组展开出的成员若与独立行同名，按同上规则合并。

四、分节（6 节，按路径前缀）
    ① ZenithReborn/*.cs
    ② ZenithReborn 的 Asset / Localization / build.txt / csproj + 仓库根文件
    ③ checks/**
    ④ docs/**
    ⑤ tools/**
    ⑥ VfxLibrary（不逐文件，只给分组统计并指向输入目录下的 vfx-library-manifest.txt）

五、覆盖口径与排除规则
    与 checks/map_audit.py 完全一致——本脚本不定义排除规则，只按磁盘现实收录。
    VfxLibrary/、artifacts/、*-profile/、*-playtest/、bin/、obj/、tools/{ilspy,xnbcli,xnbcli-binary}
    不进审计分母；本索引仍然照实收录（这些目录的行不影响覆盖率，只影响可读性）。

六、如何更新
    改完源码 / 改完调查报告后：
        python checks/build_file_index.py && python checks/map_audit.py

七、用法
    python checks/build_file_index.py             # 重建 docs/FILE_INDEX.md
    python checks/build_file_index.py --check     # 只报告差异，不写文件（CI 用）
"""

from __future__ import annotations

import argparse
import datetime
import re
import sys
from pathlib import Path

REPORT_DIR_REL = "docs/archive/codebase-map-survey-2026-11-03"
FALLBACK_REPORT_DIR_REL = "artifacts/codebase-map"
OUT_REL = "docs/FILE_INDEX.md"
VFX_MANIFEST_NAME = "vfx-library-manifest.txt"

# 归档目录里的 README.md 是目录说明，不是调查报告，不参与抽取
REPORT_SKIP = {"README.md"}


def report_dir(root: Path) -> Path:
    """调查报告（及同目录清单）的输入目录：优先入库副本，缺失时回退 artifacts/。"""
    archived = root / REPORT_DIR_REL
    return archived if archived.is_dir() else root / FALLBACK_REPORT_DIR_REL

# 报告里出现、但只写相对文件名的分组：给一个默认前缀
DEFAULT_PREFIX = {
    "F-": "checks/ZenithPrototypeCheck/",   # 夹具体系 94 行全部只写文件名
}

ROOT_PREFIXES = ("ZenithReborn/", "checks/", "docs/", "tools/", "VfxLibrary/")

HEADER_ALIASES = {
    "文件路径": "path",
    "路径": "path",
    "类型": "type",
    "所属模块": "module",
    "文件职责": "duty",
    "职责": "duty",
    "关键关联": "rel",
    "关联": "rel",
    "分析状态": "status",
    "状态": "status",
}

STATUS_JOIN = "；"
CROSS_MARK = "多代理交叉"

# 报告无法产出、只能在这里登记的文件（索引自身与本脚本）
STATIC_ROWS = [
    {
        "path": "docs/FILE_INDEX.md",
        "type": "自动生成的索引入口",
        "module": "索引",
        "duty": "全仓文件的唯一人工索引：6 节表格，由 checks/build_file_index.py 从 docs/archive/codebase-map-survey-2026-11-03/ 的 9 份调查报告抽取合并，覆盖率由 checks/map_audit.py 审计",
        "rel": "checks/build_file_index.py、checks/map_audit.py、docs/archive/codebase-map-survey-2026-11-03/",
        "status": "生成物",
    },
    {
        "path": "checks/build_file_index.py",
        "type": "构建脚本（Python 标准库）",
        "module": "索引",
        "duty": "抽取 9 份报告的六列索引表 → 去重合并 → 按前缀分 6 节 → 写 docs/FILE_INDEX.md；输入目录优先 docs/archive/codebase-map-survey-2026-11-03/，缺失时回退 artifacts/codebase-map/；支持 --check 只报差异",
        "rel": "docs/archive/codebase-map-survey-2026-11-03/*.md、docs/FILE_INDEX.md",
        "status": "已验证",
    },
    {
        "path": "checks/map_audit.py",
        "type": "审计脚本（Python 标准库）",
        "module": "索引",
        "duty": "索引覆盖率审计：磁盘分母 vs docs/FILE_INDEX.md 已索引集合，报覆盖率、未索引清单、失效路径；并核对 VfxLibrary 清单",
        "rel": "docs/FILE_INDEX.md、docs/archive/codebase-map-survey-2026-11-03/vfx-library-manifest.txt",
        "status": "已验证",
    },
]

# 2026-11-03 首次建图的调查原始记录（9 份报告 + 2 份清单 + 目录说明），入库为只读副本。
# 这些文件不在任何报告的索引表里（报告索引的是源码，不是自己），只能在这里登记。
# 少了它们索引仍能生成，但会缺 12 行、覆盖率掉到 98.2%，所以必须逐条列。
SURVEY_ARCHIVE_ROWS = [
    {
        "path": REPORT_DIR_REL + "/README.md",
        "type": "Markdown 目录说明",
        "module": "建图调查归档",
        "duty": "本目录的说明与纪律：2026-11-03 首次建图的原始调查报告为什么从 artifacts/ 复制入库、为什么只读不得修改、以及克隆仓库后如何复现 docs/FILE_INDEX.md",
        "rel": "checks/build_file_index.py、checks/map_audit.py、docs/FILE_INDEX.md",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/A-soul-core.md",
        "type": "调查报告（Markdown，488 行）",
        "module": "建图调查归档",
        "duty": "A 组：剑魂核心（soul-core）32 个 `.cs` 的模块级说明 + 逐文件六列索引；开篇声明按建图当天的磁盘状态描述，不把未提交改动当异常",
        "rel": "docs/FILE_INDEX.md 第一节、docs/CODEBASE_MAP.md",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/B-families.md",
        "type": "调查报告（Markdown，515 行）",
        "module": "建图调查归档",
        "duty": "B 组：族系统（木质 / 矿石 / 飞刃 / 神圣天堂 / 剑灵 / 环境属性 / 归属标签）39 个 `.cs`；给出「族计数只有一个真源 SoulSupport.FamilyItems」等 4 条阅读前提",
        "rel": "docs/FILE_INDEX.md 第一节、SoulSupport.cs、docs/环境族E1-21环境与武器映射.md",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/C-ui.md",
        "type": "调查报告（Markdown，267 行）",
        "module": "建图调查归档",
        "duty": "C：UI 层 22 个文件 22/22 通读；说明 `SoulUI` 这个 `ModSystem` 按「界面区域 + 职责层」拆成 18 个 partial 的切割逻辑",
        "rel": "docs/FILE_INDEX.md 第一节、SoulUI.cs、docs/ARCHITECTURE.md",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/D-render-vfx.md",
        "type": "调查报告（Markdown，509 行）",
        "module": "建图调查归档",
        "duty": "D：渲染 / 特效 / 着色器 / 资产——32 个渲染相关 `.cs` + `Assets/**` + `Localization/*.hjson` + `build.txt` + csproj，按特效总闸等组划分",
        "rel": "docs/FILE_INDEX.md 第一、二节、docs/武器分类表-常规与非常规.md",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/E-checks-scripts.md",
        "type": "调查报告（Markdown，566 行）",
        "module": "建图调查归档",
        "duty": "E：`checks/` 编排脚本与静态检查 93 项（37 `.ps1` + 54 `.py` + 2 个项目文件）；含覆盖统计、逐条索引、主线与 5 个核心脚本的调用/断言顺序、9 条坑",
        "rel": "docs/FILE_INDEX.md 第三节、checks/、AGENTS.md",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/F-checks-fixtures.md",
        "type": "调查报告（Markdown，451 行）",
        "module": "建图调查归档",
        "duty": "F：`checks/ZenithPrototypeCheck/` 夹具体系 94 个 `.cs` 的逐条索引，条目取自各检查的 `check(...)` 描述串；开篇纠正「100 个 `.cs`」的误计",
        "rel": "docs/FILE_INDEX.md 第三节、checks/ZenithPrototypeCheck/",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/G-docs-current.md",
        "type": "调查报告（Markdown，319 行）",
        "module": "建图调查归档",
        "duty": "G：`docs/` 顶层 64 篇现行文档的现状建图（不含 `docs/archive/` 的 116 篇），源码基线 `build.txt` = 0.3.130",
        "rel": "docs/FILE_INDEX.md 第四节、docs/",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/H-docs-archive.md",
        "type": "调查报告（Markdown，312 行）",
        "module": "建图调查归档",
        "duty": "H：`docs/archive/` 116 篇历史归档的索引 + 仓库根 5 个文件（`AGENTS.md`/`README.md`/`CODEBUDDY.md`/`.gitignore`/`.gitattributes`）；归档类型统一记「历史归档」",
        "rel": "docs/FILE_INDEX.md 第二节第四小节、docs/archive/",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/I-tools-vfx.md",
        "type": "调查报告（Markdown，632 行）",
        "module": "建图调查归档",
        "duty": "I：`tools/`（222 文件、5 个子目录）与 `VfxLibrary/`（11756 文件）；文件数与分组统计全部由 shell 命令生成，同目录 vfx-library-manifest.txt 是它的产物",
        "rel": "docs/FILE_INDEX.md 第五、六节、vfx-library-manifest.txt",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/vfx-library-manifest.txt",
        "type": "逐文件清单（纯文本，11756 行）",
        "module": "建图调查归档",
        "duty": "`VfxLibrary/` 的 11756 条相对路径快照；审计第五部分比对「清单行数 = 去重行数 = 磁盘文件数」，不一致默认只告警，`--strict-vfx` 可升级为失败",
        "rel": "I-tools-vfx.md、checks/map_audit.py 第五部分",
        "status": "归档副本·只读",
    },
    {
        "path": REPORT_DIR_REL + "/archived-oneoff-manifest.txt",
        "type": "分组成员清单（纯文本，20 行）",
        "module": "建图调查归档",
        "duty": "`checks/archived-oneoff/` 20 个一次性脚本的成员清单；索引里只占一行分组行，map_audit.py 把它展开成成员后才算已索引（分母不变）",
        "rel": "F-checks-fixtures.md 5.1~5.2、checks/map_audit.py 的 GROUP_MANIFESTS",
        "status": "归档副本·只读",
    },
]


# 报告没有覆盖、由人工核对补写的条目（每一条都按源码/正文核过，不是占位符）。
# 补写条目在状态列标 `人工补写`，方便与报告来源的条目区分。
SUPPLEMENT_ROWS = [
    # --- 仓库根 ---
    ("AGENTS.md", "Markdown 硬规则 + 版本状态流水",
     "仓库根", "不可让渡的硬规则与逐版本状态：每条记录安装版本、验证证据等级、未验证面；新版本往顶部追加，旧内容降级为历史",
     "CODEBUDDY.md 用 `@AGENTS.md` 导入；docs/CHANGELOG.md、docs/AGENTS状态历史.md", "人工补写"),
    ("CODEBUDDY.md", "Markdown 项目入口",
     "仓库根", "CodeBuddy 的项目入口：环境事实、目录职责、命令、权威顺序（源码 > 文档 > 记忆）、协作偏好与文档指针",
     "AGENTS.md、README.md、.codebuddy/rules/", "人工补写"),
    ("README.md", "Markdown 面向人的说明",
     "仓库根", "面向玩家的功能说明与验证证据分级（编译 / 服务端夹具 / 无图形客户端验证），并声明“注释必须中文”的协作规则",
     "docs/CHANGELOG.md、artifacts/install-result.json", "人工补写"),
    (".gitattributes", "Git 属性",
     "仓库根", "统一换行策略（`* text=auto`，源码与文本强制 LF）并声明 `*.tmod`/`*.png` 为二进制，避免跨平台 diff 污染",
     "需与新目录中的二进制资产同步", "人工补写"),
    (".gitignore", "Git 忽略规则",
     "仓库根", "忽略 `**/bin/`、`**/obj/`、7 个 `*-profile/`、`*-playtest/` 隔离运行目录；这些目录同样被 checks/map_audit.py 排除出索引分母",
     "与 checks/map_audit.py 的排除规则必须一致", "人工补写"),
    # --- 模组元数据 ---
    ("ZenithReborn/description.txt", "tModLoader 模组描述",
     "工程元数据", "模组列表里显示的描述文本，随版本手写更新（当前停在 0.3.112 的玻璃材质说明，落后于 build.txt 的 0.3.130）",
     "ZenithReborn/build.txt（版本号在此）、README.md", "人工补写"),
    # --- 夹具工程资产 ---
    ("checks/ZenithPrototypeCheck/Assets/Baselines/SwordFlow0379.fxc", "编译后的像素着色器（二进制）",
     "夹具资产·基线", "0.3.79 剑流的冻结基线着色器，仅供隔离 GPU 对照读回，不接入生产模组",
     "provenance.json、checks/run_archived_vfx_capture.ps1", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/Baselines/SwordFlow0380.fxc", "编译后的像素着色器（二进制）",
     "夹具资产·基线", "0.3.80 剑流的冻结基线着色器，与 0379 做回归差分对照",
     "provenance.json、SwordFlow0379.fxc", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/Baselines/provenance.json", "JSON 溯源记录",
     "夹具资产·基线", "登记两份基线 fxc 的来源（`artifacts/ZenithReborn-0.3.80-source.zip` 内的 `ZRSwordFlow.fxc`）与 sha256，并声明“仅隔离 GPU 对照，不进入生产模组”",
     "SwordFlow0379.fxc、SwordFlow0380.fxc", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlass.fx", "HLSL 着色器源码",
     "夹具资产·液态玻璃", "液态玻璃研究原型：屏幕画布按宽 2 高 1 的等尺度表面坐标，条带只提供高度、由最终梯度形成同一法线，再算面光反射与背景折射；不接入生产刀光",
     "LiquidGlass.fxc、checks/compile_liquid_glass.py", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlass.fxc", "编译后的着色器（二进制）",
     "夹具资产·液态玻璃", "LiquidGlass.fx 的 fxc 编译产物，由夹具在运行时加载",
     "LiquidGlass.fx、checks/compile_liquid_glass.py", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassPerfBase.fx", "HLSL 着色器源码",
     "夹具资产·液态玻璃", "性能检查的同网格基线：保留相同投影、采样、覆盖与混合，但跳过液态高度与光学计算，用于扣掉几何/带宽成本",
     "LiquidGlassPerfBase.fxc、checks/compile_liquid_glass_perf.py", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassPerfBase.fxc", "编译后的着色器（二进制）",
     "夹具资产·液态玻璃", "LiquidGlassPerfBase.fx 的 fxc 编译产物",
     "LiquidGlassPerfBase.fx", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassTrail.fxc", "编译后的着色器（二进制）",
     "夹具资产·液态玻璃", "LiquidGlassTrail.fx 的 fxc 编译产物",
     "LiquidGlassTrail.fx", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassTrail.fx", "HLSL 着色器源码",
     "夹具资产·液态玻璃", "真实轨迹网格试验：UV 梯度由检查端在世界像素空间按共享顶点汇总（不能用单位方向替代尺度）；世界到屏幕矩阵只含旋转与等比缩放，平移与投影不作用于折射方向",
     "LiquidGlassTrail.fxc、checks/compile_liquid_glass_trail.py", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/ParameterProbe.fx", "HLSL 着色器源码",
     "夹具资产·探针", "固定分支探针：按 `uProbeMode` 输出纯色，只用于确认浮点参数确实提交到了像素着色器",
     "ParameterProbe.fxc、checks/compile_liquid_glass_parameter_probe.py", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/ParameterProbe.fxc", "编译后的着色器（二进制）",
     "夹具资产·探针", "ParameterProbe.fx 的 fxc 编译产物",
     "ParameterProbe.fx", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/Sentinel.fx", "HLSL 着色器源码",
     "夹具资产·探针", "哨兵着色器：恒定输出品红，只用于确认隔离测试中的像素着色器执行与读回路径通了",
     "Sentinel.fxc、checks/compile_liquid_glass_sentinel.py", "人工补写"),
    ("checks/ZenithPrototypeCheck/Assets/LiquidGlass/Sentinel.fxc", "编译后的着色器（二进制）",
     "夹具资产·探针", "Sentinel.fx 的 fxc 编译产物",
     "Sentinel.fx", "人工补写"),
    ("checks/ZenithPrototypeCheck/Localization/en-US_Mods.ZenithPrototypeCheck.hjson", "本地化文件（空）",
     "夹具工程元数据", "夹具模组的英文本地化表，当前是空文件（2 字节）；夹具文案全部硬编码在 C# 里，不走本地化",
     "checks/ZenithPrototypeCheck/Localization/ 目录", "人工补写"),
    # --- 顶层导航文档（本轮并行编写） ---
    ("docs/ARCHITECTURE.md", "Markdown 架构总览",
     "导航文档", "19 模块边界表（负责/不负责/核心文件）、三条端到端链路（战斗·挥臂到五族派发 / UI·开魂域到改槽同步 / 构建·改源码到安装）、模块级 Mermaid 依赖图、8 条共享状态单一入口、15×11 依赖矩阵、6 条修改爆炸半径、13 条硬坑一行式。核心行号锚点：`SoulItem.cs:82`(Active)、`SoulSupport.cs:43`(FamilyItems)、`VfxIsolation.cs:8`(总闸)、`SoulBlade.cs:14`(SoulSource)、`ZenithReborn.cs:18`(HandlePacket)、`ZenithPrototypeCheck/Check.cs:11`(夹具总调度)",
     "docs/CODEBASE_MAP.md、docs/HOTSPOTS.md、artifacts/codebase-map/", "人工补写"),
    ("docs/CODEBASE_MAP.md", "Markdown 代码地图",
     "导航文档", "全仓结构总图：模块清单、目录职责、文件到模块的归属，是其余导航文档的事实底座",
     "docs/ARCHITECTURE.md、docs/FILE_INDEX.md", "人工补写"),
    ("docs/HOTSPOTS.md", "Markdown 热区速查",
     "导航文档", "按“要改某类东西”组织的文件:行号速查表，并逐条列出改动会连带打破什么",
     "docs/CODEBASE_MAP.md、docs/PROBLEM_INDEX.md", "人工补写"),
    ("docs/PROBLEM_INDEX.md", "Markdown 问题反向索引",
     "导航文档", "从玩家/开发者看到的现象反查优先检查的符号，并给出能复现或证伪该分支的夹具/脚本",
     "docs/HOTSPOTS.md、checks/ 夹具", "人工补写"),
]

# 分组索引：同一目录下同类批量文件，逐行列会淹没索引，改为一行 + 成员清单 txt。
# map_audit.py 的 GROUP_MANIFESTS 会把清单展开成成员，覆盖率照样算。
GROUP_ROWS = {
    "checks": [{
        "label": "(分组) checks/archived-oneoff/（20 个 .py）",
        "type": "历史归档脚本",
        "module": "归档·一次性",
        "duty": "20 个扁平存放的一次性取证/生成脚本：14 个 `make_*_evidence.py`/`make_*_preview.py`（截图与取证图）、3 个 `make_*`（mask/colorfields/gif 工具）、1 个 `extract_`（从 Calamity 抄剑弧）、1 个 `localize_`（批量改注释语言）。**无一条断言**，只产 png/json 供人看；无 csproj、无执行入口，`run_*.ps1`/`*.py` 全文检索零命中",
        "rel": "成员清单 `artifacts/codebase-map/archived-oneoff-manifest.txt`；目录成因见 `artifacts/codebase-map/F-checks-fixtures.md` 5.1~5.2",
        "status": "已扫描（分组索引）",
    }],
}

SECTION_TITLES = [
    ("core", "一、`ZenithReborn/` 源码（`*.cs`）",
     "模组主工程的全部 C# 源文件。改这里等于改行为。"),
    ("assets", "二、`ZenithReborn/` 工程与资产 + 仓库根",
     "贴图、着色器、本地化、构建清单与仓库根的说明文件。不写逻辑，但删一个就编译/运行失败。"),
    ("checks", "三、`checks/` 验证体系",
     "全部编排脚本、静态检查、夹具体系与归档材料。不参与模组编译。"),
    ("docs", "四、`docs/` 文档",
     "版本记录、设计定稿、专项结论与归档。`docs/archive/` 为历史归档。"),
    ("tools", "五、`tools/` 工具",
     "反编译参考、材质实验室与第三方工具包。"),
    ("vfx", "六、`VfxLibrary/` 原版素材库",
     "原版 Terraria 素材快照，不逐个进索引，见本节的分组统计与清单指针。"),
]


# ---------------------------------------------------------------------------
# 报告解析
# ---------------------------------------------------------------------------

def split_row(line: str) -> list[str]:
    s = line.strip()
    if s.startswith("|"):
        s = s[1:]
    if s.endswith("|"):
        s = s[:-1]
    return [c.strip() for c in s.split("|")]


def is_separator(cells: list[str]) -> bool:
    return bool(cells) and all(re.fullmatch(r":?-{1,}:?", c) for c in cells if c) and any(c for c in cells)


def clean_path_cell(cell: str) -> str:
    s = cell.strip()
    if s.startswith("`") and s.count("`") >= 2:
        s = s[1:s.index("`", 1)]
    else:
        s = s.strip("`")
    return s.strip()


def default_prefix_for(report_name: str) -> str:
    for key, prefix in DEFAULT_PREFIX.items():
        if report_name.startswith(key):
            return prefix
    return ""


def expand_glob(root: Path, pattern: str) -> list[str]:
    try:
        hits = [p for p in root.glob(pattern) if p.is_file()]
    except (ValueError, OSError):
        return []
    return sorted(p.relative_to(root).as_posix() for p in hits)


def parse_report(path: Path, root: Path, warn: list[str]) -> tuple[list[dict], dict]:
    """返回 (记录列表, 统计)。记录字段见 STATIC_ROWS。"""
    lines = path.read_text(encoding="utf-8").splitlines()
    records: list[dict] = []
    stats = {"tables": 0, "rows": 0, "elided": 0, "unresolved": 0, "glob_rows": 0}
    prefix = default_prefix_for(path.name)

    i = 0
    while i < len(lines):
        line = lines[i].strip()
        # 只认首列表头恰好是「文件路径」的表格。报告里另有 `| 路径 | 文件数 |` 这类
        # 分类统计表，首列也是路径但不是逐文件索引，必须排除。
        if not line.startswith("|") or split_row(line)[0].strip().strip("`") != "文件路径":
            i += 1
            continue
        head_cells = split_row(line)
        colmap = {}
        for idx, cell in enumerate(head_cells):
            key = HEADER_ALIASES.get(cell.strip().strip("`"))
            if key:
                colmap[key] = idx
        if "path" not in colmap:
            i += 1
            continue

        stats["tables"] += 1
        i += 1
        last_dir = ""

        while i < len(lines) and lines[i].strip().startswith("|"):
            cells = split_row(lines[i])
            i += 1
            if is_separator(cells):
                continue
            if not cells or not cells[0]:
                continue
            raw = clean_path_cell(cells[0])
            if not raw or set(raw) <= set("-: "):
                continue

            def get(key: str) -> str:
                idx = colmap.get(key)
                if idx is None or idx >= len(cells):
                    return ""
                return cells[idx].strip().strip("`") if key == "path" else cells[idx].strip()

            rec = {
                "path": "",
                "type": get("type"),
                "module": get("module"),
                "duty": get("duty"),
                "rel": get("rel"),
                "status": get("status"),
            }
            stats["rows"] += 1

            if "..." in raw:
                stats["elided"] += 1
                if last_dir:
                    rec.update(path=last_dir, type=rec["type"] or "第三方文件", module=rec["module"] or "分组",
                               duty=rec["duty"] or ("省略写法的分组行，同目录成员见上一行所在目录"))
                    records.append(rec)
                continue

            if "*" in raw:
                stats["glob_rows"] += 1
                members = expand_glob(root, raw)
                if members:
                    for m in members:
                        records.append(dict(rec, path=m, duty=rec["duty"] or ("分组展开：" + raw)))
                else:
                    parent = raw.rsplit("/", 1)[0]
                    if (root / parent).is_dir():
                        records.append(dict(rec, path=parent + "/", duty=rec["duty"] or ("分组展开：" + raw)))
                continue

            if raw.startswith(ROOT_PREFIXES):
                full = raw
            elif prefix:
                full = prefix + raw
            else:
                stats["unresolved"] += 1
                warn.append("%s: 无法归属路径 %r（无根前缀、无默认前缀、无通配）" % (path.name, raw))
                continue

            full = full.replace("\\", "/")
            while full.startswith("./"):
                full = full[2:]

            if full.endswith("/") or (root / full).is_dir():
                if full.endswith("/") and (root / full).is_dir():
                    rec.update(path=full, type=rec["type"] or "目录分组", module=rec["module"] or "分组")
                    records.append(rec)
                    last_dir = full
                continue

            if (root / full).is_file():
                rec["path"] = full
                records.append(rec)
                last_dir = full.rsplit("/", 1)[0] + "/" if "/" in full else ""
            else:
                stats["unresolved"] += 1
                warn.append("%s: 报告路径在磁盘上不存在 %r" % (path.name, full))
        continue

    return records, stats


# ---------------------------------------------------------------------------
# 合并 / 分节
# ---------------------------------------------------------------------------

def longest(a: str, b: str) -> str:
    a, b = (a or "").strip(), (b or "").strip()
    if not a:
        return b
    if not b:
        return a
    if a == b:
        return a
    return a if len(a) >= len(b) else b


def merge_records(records: list[dict]) -> tuple[list[dict], int]:
    merged: dict[str, dict] = {}
    dup = 0
    order: list[str] = []
    for rec in records:
        key = rec["path"]
        if key not in merged:
            merged[key] = dict(rec)
            order.append(key)
            continue
        dup += 1
        cur = merged[key]
        cur["type"] = longest(cur["type"], rec["type"])
        cur["module"] = longest(cur["module"], rec["module"])
        cur["duty"] = longest(cur["duty"], rec["duty"])
        cur["rel"] = longest(cur["rel"], rec["rel"])
        statuses = [s for s in (cur["status"], rec["status"]) if s]
        uniq = []
        for s in statuses:
            if s not in uniq:
                uniq.append(s)
        if len(uniq) > 1:
            uniq.append(CROSS_MARK)
        cur["status"] = STATUS_JOIN.join(uniq)
    return [merged[k] for k in order], dup


def section_of(rel: str) -> str:
    if rel.startswith("VfxLibrary/"):
        return "vfx"
    if rel.startswith("ZenithReborn/"):
        rest = rel[len("ZenithReborn/"):]
        if rest.endswith(".cs") and "/" not in rest:
            return "core"
        return "assets"
    if rel.startswith("checks/"):
        return "checks"
    if rel.startswith("docs/"):
        return "docs"
    if rel.startswith("tools/"):
        return "tools"
    return "assets"   # 仓库根文件


def md_cell(text: str) -> str:
    return (text or "").replace("|", "\\|").replace("\n", " ").strip()


def dwidth(text: str) -> int:
    """终端显示宽度：中日韩全角字符按 2 列算。"""
    import unicodedata
    return sum(2 if unicodedata.east_asian_width(ch) in ("W", "F") else 1 for ch in text)


def pad(text: str, width: int) -> str:
    return text + " " * max(0, width - dwidth(text))


# ---------------------------------------------------------------------------
# VfxLibrary 统计
# ---------------------------------------------------------------------------

def vfx_stats(root: Path) -> dict:
    base = root / "VfxLibrary"
    manifest = report_dir(root) / VFX_MANIFEST_NAME
    files = [p for p in base.rglob("*") if p.is_file()] if base.is_dir() else []
    top: dict[str, int] = {}
    ext: dict[str, int] = {}
    for p in files:
        parts = p.relative_to(base).parts
        key = parts[0] if len(parts) > 1 else "(根)"
        top[key] = top.get(key, 0) + 1
        ext[p.suffix.lower() or "(无扩展名)"] = ext.get(p.suffix.lower() or "(无扩展名)", 0) + 1
    m_lines = 0
    if manifest.is_file():
        m_lines = len([ln for ln in manifest.read_text(encoding="utf-8").splitlines() if ln.strip()])
    return {"files": len(files), "top": top, "ext": ext, "manifest_lines": m_lines,
            "manifest_ok": manifest.is_file(),
            "manifest_rel": manifest.relative_to(root).as_posix()}


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------

def build(root: Path) -> tuple[str, dict]:
    warn: list[str] = []
    all_records: list[dict] = []
    per_report: list[tuple[str, dict]] = []

    src_dir = report_dir(root)
    reports = sorted(p for p in src_dir.glob("*.md") if p.name not in REPORT_SKIP)
    for rp in reports:
        recs, stats = parse_report(rp, root, warn)
        per_report.append((rp.name, stats))
        all_records.extend(recs)

    merged, dup = merge_records(all_records)
    have = {r["path"] for r in merged}
    added = 0
    supplement = [dict(zip(("path", "type", "module", "duty", "rel", "status"), row)) for row in SUPPLEMENT_ROWS]
    for rec in STATIC_ROWS + SURVEY_ARCHIVE_ROWS + supplement:
        if (root / rec["path"]).exists() and rec["path"] not in have:
            merged.append(dict(rec))
            have.add(rec["path"])
            added += 1

    buckets: dict[str, list[dict]] = {k: [] for k, _, _ in SECTION_TITLES}
    for rec in merged:
        buckets[section_of(rec["path"])].append(rec)
    for k in buckets:
        buckets[k].sort(key=lambda r: r["path"])

    vfx = vfx_stats(root)

    out: list[str] = []
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    total_rows = sum(len(v) for k, v in buckets.items() if k != "vfx")

    out.append("# 文件索引（FILE_INDEX）")
    out.append("")
    out.append("> **本文件由脚本生成，不要手改。**修改会被下一次生成覆盖，正确做法是改调查报告或源码后重跑。")
    out.append("")
    out.append("```text")
    out.append("生成命令 : python checks/build_file_index.py      （配套审计：python checks/map_audit.py）")
    out.append("生成时间 : %s" % now)
    out.append("数据来源 : %s/ 下 %d 份分组调查报告（A~I）的六列索引表" % (src_dir.relative_to(root).as_posix(), len(reports)))
    out.append("索引条目 : %d 条（去重合并 %d 次重复出现；报告未覆盖的人工补写 %d 条）" % (total_rows, dup, added))
    out.append("覆盖口径 : 仓库内全部自有文件，与 checks/map_audit.py 的分母一致")
    out.append("排除目录 : artifacts/  bin/  obj/  *-profile/  *-playtest/  .git/  编辑器配置目录")
    out.append("整体记录 : VfxLibrary/（11756 个原版素材，按第六节单独核对）")
    out.append("           tools/ilspy/  tools/xnbcli/  tools/xnbcli-binary/（第三方，记“第三方-未深入”）")
    out.append("如何更新 : 改完源码或调查报告后跑  python checks/build_file_index.py && python checks/map_audit.py")
    out.append("```")
    out.append("")
    out.append("## 怎么用这张表")
    out.append("")
    out.append("1. **先定位，再读码。**按路径前缀进对应节，看「文件职责」判断要不要打开，看「关键关联」判断改它还会连带什么。")
    out.append("2. **改之前看状态。**`已验证` 表示报告作者读过源码并给出证据；`部分待核实` / `第三方-未深入` 表示结论有缺口，别把它当权威。")
    out.append("3. **`多代理交叉`** 表示同一文件被两份以上报告独立分析过，职责列取了最长的一版，通常也是最可信的一版。")
    out.append("4. **失效路径 = 索引没跟上代码。**索引里的路径在磁盘上消失会被审计判 FAIL，删文件/改名后要顺手重跑生成。")
    out.append("")
    out.append("## 抽取统计")
    out.append("")
    out.append("```text")
    cols = [22, 6, 6, 8, 8, 8]
    heads = ["报告", "索引表", "原始行", "通配展开", "省略合并", "无法解析"]
    out.append("".join(pad(h, w) + "  " for h, w in zip(heads, cols)).rstrip())
    for name, s in per_report:
        vals = [name, str(s["tables"]), str(s["rows"]), str(s["glob_rows"]), str(s["elided"]), str(s["unresolved"])]
        out.append("".join(pad(v, w) + "  " for v, w in zip(vals, cols)).rstrip())
    out.append("```")
    out.append("")

    for key, title, blurb in SECTION_TITLES:
        rows = buckets[key]
        out.append("---")
        out.append("")
        out.append("## " + title)
        out.append("")
        out.append(blurb)
        out.append("")
        if key == "vfx":
            out.append("```text")
            out.append("磁盘文件数   : %d" % vfx["files"])
            out.append("清单指针     : %s（%s，%d 行）" % (
                vfx["manifest_rel"],
                "存在" if vfx["manifest_ok"] else "缺失",
                vfx["manifest_lines"]))
            out.append("审计口径     : 不进主分母，单独核对（见 checks/map_audit.py 第五节）")
            out.append("```")
            out.append("")
            out.append("```text")
            out.append("按顶层目录：")
            for k in sorted(vfx["top"], key=lambda x: (-vfx["top"][x], x)):
                out.append("  " + pad(k, 26) + str(vfx["top"][k]))
            out.append("  " + pad("(合计)", 26) + str(vfx["files"]))
            out.append("")
            out.append("按扩展名：")
            for k in sorted(vfx["ext"], key=lambda x: (-vfx["ext"][x], x)):
                out.append("  " + pad(k, 26) + str(vfx["ext"][k]))
            out.append("```")
            out.append("")
            out.append("逐文件清单不写进本文件（11756 行会淹没其余 650 个文件）。需要查具体素材时直接看清单 txt 或用 `checks/vfx_library.py` 检索。")
            out.append("")
            continue

        out.append("| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |")
        out.append("|---|---|---|---|---|---|")
        for r in rows:
            out.append("| `%s` | %s | %s | %s | %s | %s |" % (
                md_cell(r["path"]), md_cell(r["type"]), md_cell(r["module"]),
                md_cell(r["duty"]), md_cell(r["rel"]), md_cell(r["status"])))
        for g in GROUP_ROWS.get(key, []):
            out.append("| %s | %s | %s | %s | %s | %s |" % (
                md_cell(g["label"]), md_cell(g["type"]), md_cell(g["module"]),
                md_cell(g["duty"]), md_cell(g["rel"]), md_cell(g["status"])))
        out.append("| (合计) | %d 条 |  |  |  |  |" % len(rows))
        out.append("")

    return "\n".join(out).rstrip() + "\n", {"rows": total_rows, "dup": dup, "added": added, "warn": warn,
                                            "buckets": {k: len(v) for k, v in buckets.items()},
                                            "reports": per_report}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="合并建图调查报告的索引表 → docs/FILE_INDEX.md（优先 docs/archive/codebase-map-survey-2026-11-03/，回退 artifacts/codebase-map/）")
    parser.add_argument("--root", default=str(Path(__file__).resolve().parent.parent), help="仓库根")
    parser.add_argument("--check", action="store_true", help="只比对是否与磁盘上的文件一致，不写文件")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    text, info = build(root)
    target = root / OUT_REL

    for w in info["warn"]:
        print("[警告] " + w, file=sys.stderr)

    if args.check:
        drop = lambda t: "\n".join(ln for ln in t.splitlines() if not ln.startswith("生成时间 :"))
        old = target.read_text(encoding="utf-8") if target.is_file() else ""
        same = drop(old) == drop(text)
        print("[索引生成] --check：%s" % ("一致" if same else "不一致，需要重跑生成"))
        return 0 if same else 1

    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")

    print("[索引生成] 写出 %s" % target)
    print("  条目合计 : %d（去重合并 %d 次重复，报告未覆盖的人工补写 %d 条）" % (info["rows"], info["dup"], info["added"]))
    for key, title, _ in SECTION_TITLES:
        print("    %-8s %d 条" % (key, info["buckets"][key]))
    if info["warn"]:
        print("  警告     : %d 条（见 stderr）" % len(info["warn"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
