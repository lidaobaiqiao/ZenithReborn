# 任务图

持久记录任务拆解、依赖与状态，供编排激活入口读写。

状态枚举：`pending/ready/running/blocked/review/completed/failed`

| ID | 目标 | 前置 | 状态 | owns | forbid | 验证结论 | 阻塞 |
| --- | --- | --- | --- | --- | --- | --- | --- |

集成成功后清空本表或归档。
