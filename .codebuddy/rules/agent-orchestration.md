---
alwaysApply: true
---

# 项目规则：加载智能子代理编排

实质工程任务（开发、调试、重构、多文件改动）开始前，加载并应用用户级 Skill：
`~/.workbuddy/skills/agent-orchestration/SKILL.md`（调用 `Skill(agent-orchestration)` 即可，已实测当前会话可直接加载）。
项目内 `.codebuddy/skills/agent-orchestration/SKILL.md` 只是入库指针，不要当规则真源。

不可让渡的五条：

1. 按可独立验证的最小交付物拆解，不按通用角色或大子系统分块。
2. 四类依赖建图（硬依赖 / 共享文件 / 共享资源 / 独立），校验无环。
3. 就绪即派、完成即解锁，不用批次屏障。
4. 写集合非重叠（`owns`/`forbid`）；无原生 worktree 参数，靠所有权隔离。
5. 琐碎改动直接做；**代理数量不是合规指标**，委派是手段不是目的。

`00-delegation.md`（V3）保留的硬规则继续有效：中文注释、模块归属、git 提交点提醒、存档隔离与不强杀游戏、证据分级、提交范围脱敏。

持久任务图：`.codebuddy/agent-orchestration/TASK_GRAPH.md`；集成成功后清空。
