# 2026-11-03 首次建图：调查原始记录（只读）

## 这是什么

2026-11-03 首次全仓建图时，9 个并行调查代理交回的**原始调查报告**（A~I），外加两份分组索引的**成员清单**。
它们是 `docs/FILE_INDEX.md` 的唯一事实来源：索引里的每一行「文件路径 / 类型 / 所属模块 / 文件职责 / 关键关联 / 分析状态」，
都是从这 9 份报告的六列索引表里机械抽取、去重合并出来的，没有任何一行是手抄的。

## 为什么放在这里

这批报告的原始产出位置是 `artifacts/codebase-map/`，而 `artifacts/` 被 `.gitignore` 排除。
结果是：`checks/build_file_index.py`（`AGENTS.md` 里写明的维护命令）在别人克隆下来的仓库里跑不起来——
输入不存在，`docs/FILE_INDEX.md` 就无法重建，索引会退化成一份无法复验的静态文本。

所以把它们复制到这里入库，让「重建索引」这件事脱离 `artifacts/` 也能复现。
现在 `checks/build_file_index.py` 与 `checks/map_audit.py` 都**优先读本目录，本目录不存在时才回退 `artifacts/codebase-map/`**。

## 只读，不要改

- 本目录的文件是**历史记录的冻结副本**，不是活文档。不要就地修改、不要润色、不要补新条目。
- 调查结论随源码演进而过时，这是正常的：报告描述的是 2026-11-03 当天的磁盘状态，**不能当成当前代码的事实来源**。
  当前事实请以源码为准，导航请走 `docs/ARCHITECTURE.md` / `docs/CODEBASE_MAP.md` / `docs/HOTSPOTS.md` / `docs/FILE_INDEX.md`。
- 报告里大量 `文件:行号` 引用只对建图当天有效，读的时候请自己核对源码。
- 要更新索引内容：改这里的报告是**错的**做法；正确做法是重新调查、覆盖式重写本目录，然后重跑
  `python checks/build_file_index.py && python checks/map_audit.py`。

## 目录内容

| 文件 | 内容 |
|---|---|
| `A-soul-core.md` | 剑魂核心（soul-core）32 个 `.cs` 的模块地图与索引表 |
| `B-families.md` | 族系统（木质 / 矿石 / 飞刃 / 神圣天堂 / 剑灵 / 环境属性 / 归属标签）39 个 `.cs` |
| `C-ui.md` | UI 层 22 个文件（`SoulUI` 拆成 18 个 partial 的切割逻辑） |
| `D-render-vfx.md` | 渲染 / 特效 / 着色器 / 资产：32 个 `.cs` + `Assets/**` + 本地化 + `build.txt` + csproj |
| `E-checks-scripts.md` | `checks/` 编排脚本与静态检查：37 个 `.ps1` + 54 个 `.py` + 2 个项目文件，共 93 项 |
| `F-checks-fixtures.md` | `checks/ZenithPrototypeCheck/` 夹具体系 94 个 `.cs` 逐条索引（从 `check(...)` 描述串抽取） |
| `G-docs-current.md` | `docs/` 顶层 64 篇现行文档 |
| `H-docs-archive.md` | `docs/archive/` 116 篇历史归档 + 仓库根文档 |
| `I-tools-vfx.md` | `tools/`（222 文件）与 `VfxLibrary/`（11756 文件） |
| `vfx-library-manifest.txt` | `VfxLibrary/` 逐文件清单（11756 行），审计第五部分用它核对清单/磁盘一致性 |
| `archived-oneoff-manifest.txt` | `checks/archived-oneoff/` 20 个一次性脚本的成员清单，分组索引靠它展开覆盖率 |

## 覆盖口径

与 `checks/map_audit.py` 完全一致：`VfxLibrary/`、`artifacts/`、`*-profile/`、`*-playtest/`、`bin/`、`obj/`、
`tools/{ilspy,xnbcli,xnbcli-binary}` 不进主分母；前两类单独核对，其余按整体记「第三方-未深入」。
