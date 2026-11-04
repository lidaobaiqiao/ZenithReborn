# 派单契约 · agent-orchestration（组件 D）

## 契约引用
> 任务 ID、状态、字段、回交五项、路径根、工具口径**只以主文件 [SKILL.md](~/.workbuddy/skills/agent-orchestration/SKILL.md) 为准**（唯一副本，本文件不复制）。
> 路径根 = `${WORKSPACE}`（当前工作区）；项目内文件一律写作 `${WORKSPACE}/.codebuddy/...`。

## 1. 派单模板
**唯一版本在主文件 [SKILL.md](~/.workbuddy/skills/agent-orchestration/SKILL.md) §派单模板**（`Skill()` 只展开主文件，模板放在这里加载不到）。
模板 9 行已覆盖项目硬规则要求的全部字段：目标、范围与输入假设、技术约束与允许操作、`owns`/`forbid`、验证条件、交付物、超时上报、回交五项。本节不另设第二套清单。
**禁写项**：派单里禁止塞 O(n) 防御动作——「通读全文」「全库逐一核对」「写 30 行报告」「跑全量修到全绿」。这些跨文件、无单点验收、把失败范围无限放大；一律换成本任务相关的最小只读片段 + 一条判据。

## 2. 上下文隔离规则
- 主上下文**只留**：目标 / 架构 / 依赖图 / 状态 / 结论 / 集成状态。
- 源码全文、日志、试错过程**留在代理侧**，不回灌主上下文。
- 只传任务相关上下文：该任务的 `Consumes`、`owns`、`verify`、上游依赖结论；不传全库、不传无关任务源码。
- 回交五项**不设人为字数上限**，但禁止搬运原始日志 / 堆栈全文；只给结论 + 证据定位（`文件:行` / 命令退出码 / 夹具名）。
- 代理中断用 `name` + `resume` 续接，主上下文不重建、不重放历史。

## 3. 真实并行调用写法
```
同一消息内多个 Agent 调用 = 并行（无 worktree/isolation 参数，靠非重叠写集合隔离）：
  Agent(name="t1", prompt=派单T1, ...)
  Agent(name="t3", prompt=派单T3, ...)   ← 与 t1 并列发出

建任务 / 加依赖：
  TaskCreate(...)                                  → 落 T1/T2/T3，status=pending
  TaskUpdate(taskId="T2", addBlockedBy=["T1"])   → 建依赖；T1=completed 后 T2 转 ready
```
- 调度：**T1 完成 → 立即派 T2（前置仅 T1），不等 T3**；只有前置含 T3 的任务才等 T3。
- 后台命令：
```
  Bash(command="python tools/check_index.py --check", run_in_background:true)  → task_id
  TaskOutput(task_id=..., block=true)
```
- **`run_in_background:true` 必须写明「超过 N 分钟无输出即停并报告」**（`TaskStop` + 报 `BLOCKED`），漏写视为未派单。

## 4. 观察与失败
- `TaskOutput(task_id, ...)` 收后台结果；`SendMessage(recipient=name, ...)` 与具名代理通信（问卡点 / 发改图 / 催回交）。
- 代理报 `BLOCKED` 或范围外依赖（需改 `owns` 外文件）→ **主代理改依赖图 / 新增任务 / 调整 `owns`**，不替代理扩权、不代改文件。
- 回交缺项或 `Verification/Evidence` 为空 → 打回 `review`，不算 `completed`；`failed` 保留现场，`resume` 或重派。
- **不得伪造派单、不得伪造结果、不得代代理补证据**；状态流转只由真实回交与 `TaskUpdate` 驱动。
