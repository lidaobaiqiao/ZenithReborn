# CODEBUDDY.md — ZenithReborn 项目入口

给 AI 代理看的项目索引。面向人的功能说明看 `README.md`，协作硬规则看 `AGENTS.md`，长期结论看 `.workbuddy/memory/MEMORY.md`。
本文件只放**环境事实、目录职责、命令、权威顺序与指针**，不重复上述文件的内容。

## 项目定位

「天顶复苏 · 诅咒残魂」（ZenithReborn），tModLoader 1.4.4 的角色成长型天顶剑模组，无灾厄依赖。
核心设计：把近战武器寄宿进残魂物品，挥臂时由剑魂为每把寄宿剑各掷一把天顶式飞剑；成长靠六诅咒逐道解除限制并给对应祝福，而非数值堆叠。
当前版本 **0.3.127**（`ZenithReborn/build.txt` 为准）。

## 环境（已实测）

| 项 | 值 |
|---|---|
| 目标框架 | .NET 8 / C# 12 |
| tModLoader | `E:/SteamLibrary/steamapps/common/tModLoader/`（`tModLoader.dll` 存在） |
| 官方 dotnet | `E:/SteamLibrary/steamapps/common/tModLoader/dotnet/dotnet.exe`（**不在** tModLoader 根目录） |
| 外壳 | 跑 `.ps1` 必须用 **pwsh 7**（`C:\Program Files\PowerShell\7\pwsh.exe`）；Windows PowerShell 5.1 会把无 BOM 的 UTF-8 脚本当 GBK 读而解析失败 |
| Python | 脚本优先用 `~/.cache/codex-runtimes/.../python.exe`，缺失时回退 `python`（3.13 可用，**未装 hjson 库**） |
| 外部依赖 | `Luminance.dll`（`artifacts/dependencies/`），由 `checks/prepare_luminance.py` 在编译前准备 |

## 目录职责

| 路径 | 职责 | 是否入库 |
|---|---|---|
| `ZenithReborn/` | 模组源码（112 个 `.cs`，平铺根目录、单命名空间）+ 资产 + 本地化 | 是 |
| `checks/` | 验证体系：`ZenithPrototypeCheck/` 隔离夹具模组、`run_*.ps1` 编排、`*.py` 静态与资产检查 | 是 |
| `docs/` | 版本记录、设计稿、机制定稿；`docs/archive/` 为历史 | 是 |
| `VfxLibrary/` | 原版特效素材库与可搜索图鉴（67 MB） | 是 |
| `tools/` | 反编译/XNB 解包/材质实验室等工具（体积大，二进制已 ignore） | 部分 |
| `artifacts/` | 构建、安装、检查产物与备份（16 GB） | 否 |
| `*-profile/`、`*-playtest/` | 隔离运行目录（build / check / wood-check / 实测） | 否 |

## 命令

```bash
# 编译（只验 C#，不验本地化与资产）
dotnet build ZenithReborn/ZenithReborn.csproj -c Release

# 全量隔离回归：着色器编译 → 原生打包两包 → 起隔离服务端跑断言
pwsh -File checks/run_wood_checks.ps1
```

回归的实测事实（2026-11-01）：
- 通过输出形如 `PASS: 8453 focused checks`，项数**按启用套件动态统计**，不要把它当成固定常数（历史文档里的 8393 已过时）。
- 脚本是 **fail-fast**：第一条断言失败即抛异常，后面的检查根本没跑，所以"通过项数"只在全绿时出现。
- 结果落盘 `artifacts/wood-check.txt`，另有 `artifacts/native-build-check.json` 记录本次两包 sha256 与 `passed`（失败时也会写 `passed:false`）。
- 单轮耗时约 70–85 秒，不要按"十几分钟"设超时上限。

## 权威顺序

`build.txt` / 当前源码 / 最新验证与安装记录 **>** `README.md` **>** 设计稿 **>** `docs/archive/` 历史记录。
设计稿大量存在"已实装 / 草案 / 已撤销"三态，读之前必须先看文件头部的状态注记。

`docs/CHANGELOG.md` 是版本索引（104 条，倒序）；版本号**不保证单调**（0.3.118 的安装晚于 0.3.119，是因为它撤回了 119 的加速）。

## 关键约束（违反会返工）

1. **所有代码注释必须中文**；标识符保持英文，只翻注释。
2. **先找归属再写代码**：按 `AGENTS.md` 的模块职责拆分；不把战斗规则、素材加载、绘制、UI、调试塞进同一文件。
3. `SoulItem` / `SoulUI` / `SoulBlade` / `SwordFlowMaterial` 已经是职责超载的大类（27–29 KB），新增能力优先开新文件而不是继续追加。
4. 检查脚本只用隔离目录，**不碰** `C:\Users\15537\Documents\My Games\Terraria\tModLoader\` 下的正式人物与世界；只替换目标 `.tmod`，先备份，文件被锁时等玩家退出，**不得强杀游戏**。
5. 大改动前提醒用户先落 git 提交点。
6. 证据分级：隔离夹具 < 图形客户端 < 真实多人。**服务端夹具看不到图形资产**——新增 ModItem/ModProjectile/ModBuff 必须有贴图或显式 `Texture` override，否则客户端加载即崩（0.3.127 热修的真实教训）。

## 已知长期空白

- 客户端画面、自然操作、多人联机、性能、美术观感**从未被自动验证**；`artifacts/install-result.json` 里 `graphics_tested` / `natural_gameplay_tested` / `live_multiplayer_tested` 长期为 `false`。
- 飞刃族「印记」是用户认定的核心设计，留白未实现（草案 `FlyingBladeMark.cs` 已入库留档）。
- 肉前八矿 M1 的独有机制仍是草案。
- 四个新弹幕（`FlyingBladeShot` / `HolyHeavenStar` / `HolyHeavenImpact` / `LivingArmament`）的中文显示名未定，`zh-Hans.hjson` 里暂标注使用默认值。

## 指针

- 功能全貌与核心数值表 → `README.md`
- 协作硬规则、版本状态段、历史决策 → `AGENTS.md`
- 长期结论（种族现状、剑范围硬边界、协作偏好） → `.workbuddy/memory/MEMORY.md`
- 调度规则 → `.codebuddy/rules/00-delegation.md`
- 逐版本细节 → `docs/0.3.*.md`；索引 → `docs/CHANGELOG.md`
