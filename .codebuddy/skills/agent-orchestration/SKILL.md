---
name: agent-orchestration
display_name: 代理编排（项目内指针）
description: "项目内入库指针。实际加载的用户级单一真源：~/.workbuddy/skills/agent-orchestration/SKILL.md。触发词：多文件开发、并行编排、任务拆解、子代理调度、集成验证。"
version: "1.2.0"
author: WorkBuddy
---

# 代理编排 · 项目内入库指针（非规则真源）

**规则与派单模板的唯一真源**：`~/.workbuddy/skills/agent-orchestration/SKILL.md`
（WorkBuddy 实测 `Skill(agent-orchestration)` 直接加载该真源；自包含全部规则与 §派单模板，无需重启或新会话。）

**本文件只是入库指针**，不是规则真源；下列 4 个支持文件涉及的规则与模板，真源均在用户级 `~/.workbuddy/skills/agent-orchestration/SKILL.md`：

- `task-decomposition.md` — 交付物拆解、好/坏派单对照、粒度规则
- `dependency-scheduling.md` — 四类依赖、就绪五条件、TASK_GRAPH 格式、中断恢复
- `delegation-contract.md` — 短派单模板、上下文隔离、并行写法、回交五项
- `verification.md` — 集成检查、风险分级复核、窄修复、测试记录

项目专属配置：

- 激活规则 `.codebuddy/rules/agent-orchestration.md`（`alwaysApply: true`）
- 硬规则 `.codebuddy/rules/00-delegation.md`（V3）
- 持久任务图 `.codebuddy/agent-orchestration/TASK_GRAPH.md`

改编排规则与模板只改用户级真源，不要在本目录改。
