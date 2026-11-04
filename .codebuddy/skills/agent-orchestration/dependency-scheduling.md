# 依赖与调度（agent-orchestration）

## 契约引用
> 任务 ID、状态、字段、回交五项、路径根、工具口径与派单模板**只以主文件 [SKILL.md](~/.workbuddy/skills/agent-orchestration/SKILL.md) 为准**（唯一副本，本文件不复制）。
> 路径根 = `${WORKSPACE}`（当前工作区）；项目内文件一律写作 `${WORKSPACE}/.codebuddy/...`，跨项目时自动指向该项目自己的目录。

## 1. 四类依赖判定表
| 类型 | 判定依据 | 可否并行 | 本项目例子 |
|---|---|---|---|
| 硬依赖 | 后置任务的输入/接口来自前置产物 | 否：前置 `completed` 后置才 `ready` | T2 调用 T1 冻结的共享接口/类型 |
| 共享文件冲突 | 两任务写同一文件 | 否：串行，或合并为单任务 | 两代理同改一个 `.cs` / `SKILL.md` |
| 共享资源冲突 | 争用非文件资源或隐式生成物 | 否：串行，或各自独立输出目录 | 两代理同时 `dotnet build` 抢 `obj/`、`bin/`；夹具报告 / 安装目录被覆盖 |
| 独立 | 写集合、读接口、生成物、隐式执行依赖均无交集 | 是 | 各写各文件、只读不同模块、不触发同一构建 |

> **不同文件 ≠ 自动独立**：判独立前必查 ①共享接口/签名 ②生成物（`bin/obj/报告/夹具`）③隐式执行依赖（构建、安装、夹具、测试运行）。任一相交即降级为冲突类。

## 2. 就绪五条件（全部满足才置 `ready`）
| # | 条件 | 检查点 |
|---|---|---|
| 1 | 前置完成 | `prerequisites` 全为 `completed` |
| 2 | 接口已定 | 依赖任务交付的共享接口/签名已冻结 |
| 3 | 资源可用 | 构建/夹具/安装目录等共享资源空闲 |
| 4 | 无冲突所有权 | 无 `running` 任务的 `owns`/资源与候选冲突 |
| 5 | 输入与验收明确 | `owns`/`forbid`/`verify` 与验收条件已写明 |

## 3. 动态调度循环
`扫就绪前沿 → 选可并行任务（受运行时并发上限约束）→ 真实派单（同一消息多个 Agent 调用 / TaskCreate+addBlockedBy）→ 记活跃 running → 收结果（回交五项）→ 更新状态/依赖 → 立刻派新解锁任务`。
- **不等整批完成**：任一任务 `completed` 立即重算前沿并派新解锁项，无刚性批次屏障。
- **真实派单非纸面**：每次派遣落到 `Agent`/`TaskCreate`，`TaskList` 可见状态。
- **并发上限**：并行数 ≤ 运行时允许值，超出者留 `ready` 排队。

## 4. TASK_GRAPH.md 格式（可复制）
路径 `${WORKSPACE}/.codebuddy/agent-orchestration/TASK_GRAPH.md`，仅存结论：

```markdown
| ID | 目标 | 前置 | 状态 | owns | forbid | verify 结论 | 阻塞 |
|---|---|---|---|---|---|---|---|
| T1 | 冻结共享接口 IScore | — | completed | src/Contracts/IScore.cs | src/Game/** | `dotnet build` 通过；签名冻结 | — |
| T2 | 实现计分服务 | T1 | ready | src/Score/ScoreService.cs | src/Contracts/** | `dotnet test --filter Score` 通过 | — |
```
- 列固定为 ID/目标/前置/状态/owns/forbid/verify 结论/阻塞；`verify 结论` 只写结果，不放过程。
- **禁止**放原始日志、临时输出、废弃实验；集成成功后清空或归档该表。
- 跨会话恢复 = 读此表 + `TaskList` 对齐状态。

## 5. 中断恢复
1. 新会话先读 `TASK_GRAPH.md`，与 `TaskList` 对齐。
2. 按第 2 节重算就绪前沿。
3. 只重跑 `running`/`failed` 中未确认产物项（先校验磁盘/构建证据再定）。
4. `completed` 不重跑；`blocked` 待依赖解锁后转 `ready`。
