# 任务分解 · agent-orchestration（组件 A）

## 契约引用
> 任务 ID、状态、字段、回交五项、路径根、工具口径与派单模板**只以主文件 [SKILL.md](~/.workbuddy/skills/agent-orchestration/SKILL.md) 为准**（唯一副本，本文件不复制）。
> 路径根 = `${WORKSPACE}`（当前工作区）；项目内文件一律写作 `${WORKSPACE}/.codebuddy/...`，跨项目时自动指向该项目自己的目录。

## 1. 分解步骤（顺序执行）
1. 需求 → 压成一句话可验收目标。
2. 列交付物清单：逐项写清产物文件/路径与形态。
3. 切：按「一文件一职责 + 可独立验证」拆；每个交付物 = 一个任务。
4. 标接口：每任务写 `Consumes`（读谁的 Produces）/ `Produces`（产出什么，供谁 Consumes）。
5. 定依赖：B 的 Consumes 命中 A 的 Produces → 连边，写入 B 的 `prerequisites`。
6. 定写集合：每个任务在 `owns` 显式声明可写文件/行区，任务间非重叠；共享文件必须合并为单写者或串行。
7. 定验证：每任务一条可跑判据（命令 / 夹具 / `--check`），写进 `verify`，给通过条件。

**禁止**：按通用角色（探索 / 实现 / 验证）或大子系统（"渲染系统""网络层"）切派单——角色是能力不是交付物，子系统跨多文件、不可独立验证、写集合必然重叠。

## 2. 好 / 坏派单对照（各 5 条，本项目实例）
| # | 好派单 | 可派原因 | 坏派单 | 不可派原因 |
|---|---|---|---|---|
| 1 | 把 `FILE_INDEX.md` 的生成时间行改成 UTC 并验 `--check` | 单文件、单判据 | 调查整个渲染系统并修好 | 跨多文件、无边界、无判据 |
| 2 | 修 `WoodBondRot` 的结算上限并补 1 条夹具 | 一改动 + 一夹具，自洽可验 | 改夹具+跑全量+修到全绿 | 「全绿」把全库失败耦合进单任务，范围随失败膨胀 |
| 3 | 给 `FILE_INDEX.md` 补登 1 个缺失文件条目，仅该文件，验 `--check` | 写集合=1 文件，职责单一 | 读全项目改善架构 | O(n) 阅读、无交付物、无验收点 |
| 4 | 把 `WoodBondRot` 夹具里的期望值改成新上限并只跑该夹具 | 验证自足、不触其它 | 实现边界不清的功能、文件不受限 | `owns` 缺失 → 写集合不可隔离 |
| 5 | 在 `docs/CHANGELOG.md` 追加 1.3 记录，仅该文件 | 一文件一职责 | 通读所有源码后统一命名 / 逐一核对全部索引 | O(n) 防御动作、无单点判据 |

## 3. 粒度规则
- 一个任务 = 一个可独立验证交付物：跑一条 `verify` 判据即能判 `completed`。
- 足以抵得上一次委派开销：连续 3 个单行改动并入 1 任务，不单独派。
- 不过度碎片化：一个函数 / 一个夹具不拆成多任务。
- `owns` 必须显式声明；漏写视为未派单，代理不得动笔。`owns` 是**写集合**：`verify` 只是执行既有测试/检查时，测试文件不必进 `owns`；若任务要新增或修改用例，该用例文件必须写进 `owns`，否则验证越界。
- 发现集合外工作：**不扩权、不自改**，回报 `CHANGEREQUEST(文件,行,理由)`；主代理据此新增任务或调整 `owns`，再决定是否 `resume`。
- 局部小调整（同文件内、不改接口、已在 `owns` 内）可直接处理，不新开任务。

## 4. 拆解示例（直接复制）

### 示例 A · 硬依赖链
请求：新增 AX 配方解析 → 登记进索引 → 给 `--check` 加格式校验。
| ID | 目标 | 前置 | owns | forbid | verify |
|---|---|---|---|---|---|
| T1 | 实现 `RecipeParser.ParseAX` 返回 Recipe | — | `src/RecipeParser.cs` | `FILE_INDEX.md`、`tools/check_index.py` | `pytest tests/test_parser.py::test_ax` 通过 |
| T2 | 在索引登记 `RecipeParser.cs` 及导出符号 | T1 | `FILE_INDEX.md` | 源文件、校验脚本 | `python tools/check_index.py --check` 返回 0 |
| T3 | `check_index` 增加 AX 条目格式校验 | T2 | `tools/check_index.py` | 源文件、索引 | `--check` + 新增夹具通过 |

`verify` 消歧：T1 的 `pytest …::test_ax` 只**执行**既有测试，故 `owns` 仅 `src/RecipeParser.cs`；若该用例尚不存在、需要一并编写，则 `tests/test_parser.py` 必须加入 T1 的 `owns`。

依赖图 `T1→T2→T3`：T2 的 Consumes 命中 T1 的 Produces，T3 命中 T2，成硬链，不可并行。

### 示例 B · 共享文件冲突
请求：同时给 3 个新文件登记索引 + 把生成时间行改成 UTC——两件事都要写 `FILE_INDEX.md`，冲突。处理：合并为单写者 + 并行源文件。
| ID | 目标 | 前置 | owns | forbid | verify |
|---|---|---|---|---|---|
| T1 | `FILE_INDEX.md`：登记 3 个新文件 **且** 生成时间行改 UTC（合并单写者） | — | `FILE_INDEX.md` | 改任一源文件 | `--check` 返回 0 且 UTC 行匹配 |
| T2 | 实现 3 个源文件（各自独立） | — | `src/A.cs`、`src/B.cs`、`src/C.cs` | 写 `FILE_INDEX.md` | 各自单测通过 |

T2 若发现需登记索引 → 回报 `CHANGEREQUEST(FILE_INDEX.md, 行号, 新增导出)`，主代理并入 T1；**禁止两个任务同时 `owns FILE_INDEX.md`**，否则写集合重叠、互相覆盖。

### 示例 C · 并行 + 汇合（A 完成即派 C，不等 B）
请求：修 `WoodBondRot` 结算上限、把新上限写进索引说明、顺带修 `ItemTable.csv` 堆叠上限（无关）。
| ID | 目标 | 前置 | owns | forbid | verify |
|---|---|---|---|---|---|
| T1 | 修 `WoodBondRot` 结算上限 + 补 1 夹具 | — | `src/WoodBondRot.cs`、`tests/test_woodbondrot.py` | `FILE_INDEX.md`、`ItemTable.csv` | `pytest tests/test_woodbondrot.py` 通过 |
| T2 | 把新上限写进 `FILE_INDEX.md` 说明行 | T1 | `FILE_INDEX.md` | 源文件、数据表 | `--check` 返回 0 |
| T3 | 修 `ItemTable.csv` 第 42 行堆叠上限 | — | `ItemTable.csv` | 源文件、索引 | `--check` 返回 0 |

调度：T1 与 T3 写集合不重叠 → 同一消息并行派出；**T1 一完成立即派 T2（前置仅 T1，不等 T3）**；T2 与 T3 互不前置，关键路径为 `T1→T2`。
