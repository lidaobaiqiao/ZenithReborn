# 任务图

本轮（2026-11-04 架构清理：删死码 / 同步夹具与本地化 / 修索引与文档）已集成并验证，按规则清空任务表。
基线与结论见 `docs/0.3.130-木质族V2重做与口径统一.md` 的「2026-11-04 架构清理」一节：编译 0 错 0 警、资产 35/35 PASS、`PASS: 8509 focused checks`、`map_audit` 100.00%（669/669）。
本轮**未做**（留待后续独立设计）：阈值硬合并、显卡状态 5 处收敛、`DomainMode` 休眠路径、`ViscousSlash`+`EvapCircle` 整链删除、8 个零编排分支接入、`.csproj` 去硬编码 `E:/`。

状态枚举：`pending/ready/running/blocked/review/completed/failed`
类型：`modify`（owns 非空）/ `readonly`（owns 为空）

| ID | 类型 | 目标 | 状态 |
| --- | --- | --- | --- |
| — | — | 无进行中任务 | — |

集成成功后清空本表。
