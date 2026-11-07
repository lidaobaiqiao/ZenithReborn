# 任务图

本轮：无进行中任务。「2026-11-07 六诅咒口径修复」已集成完成（0.3.132 同版本内，正式包已安装，回归 PASS 8568）。

状态枚举：`pending/ready/running/blocked/review/completed/failed`
类型：`modify`（owns 非空）/ `readonly`（owns 为空）

## 任务

（集成成功后清空。新任务开工时按四类依赖重新建图。）

## 上一轮结论（供回退与验收参考）

- 修复两处实现层不一致：偏锋当前阶段说明改取 `SoulDeviation.MaxOffset(soul)` 实战真值（旧档位不再误显 24px）；`README.md` 缚身行由封顶改保底。签名、常量、`StageForCount`、用户定稿设计全部未动。
- 并行派单：T1（源码文案）/ T2（夹具断言）写集合互斥同时跑，T3 文档索引主代理同步做，T4 只读复核串行等落盘，T5 集成验证。两个实现代理均按时落盘；复核 PASS，文档一致性一项由主代理自查（闸门全绿）。
- 验证：生产编译 0 错 0 警、夹具 0 错（3 条既有警告）、回归 PASS 8568（+7 条断言）、map_freshness PASS、map_audit 100%（687/687）、索引 774 条。
- 安装：sha `323cb0da…`、备份 `artifacts/backups/20261107-141846`、enabled 保持开启。图形客户端/自然操作/真实多人未验证。
- 明确未修（等用户调整稿）：2 Boss 组 Weakened 不可达、`SoulTerrain.Sweep` 生产零调用、偏锋负面与祝福双口径、独行跨组读 Seals、攻速开局满速草案。
