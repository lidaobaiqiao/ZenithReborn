@AGENTS.md

# CODEBUDDY.md — ZenithReborn 项目入口

上面导入的 `AGENTS.md` 是**不可让渡的硬规则与版本状态**（同时存在本文件时，AGENTS.md 不会被单独加载，只能靠这行导入）。
本文件补充：环境事实、目录职责、命令、权威顺序、协作偏好摘要与文档指针。
面向人的功能说明看 `README.md`；本地长期记忆看 `.workbuddy/memory/MEMORY.md`（**不会被自动加载**，需要时手动读）。
调度规则看 `.codebuddy/rules/00-delegation.md`（自动加载）。

## 项目定位

「天顶复苏 · 诅咒残魂」（ZenithReborn），tModLoader 1.4.4 的角色成长型天顶剑模组，无灾厄依赖。
核心设计：把近战武器寄宿进残魂物品，挥臂时由剑魂为每把寄宿剑各掷一把天顶式飞剑；成长靠六诅咒逐道解除限制并给对应祝福，而非数值堆叠。
当前版本 **0.3.130**（以 `ZenithReborn/build.txt` 为准）。

## 环境（已实测）

| 项 | 值 |
|---|---|
| 目标框架 | .NET 8 / C# 12 |
| tModLoader | `E:/SteamLibrary/steamapps/common/tModLoader/`（`tModLoader.dll` 存在） |
| 官方 dotnet | `E:/SteamLibrary/steamapps/common/tModLoader/dotnet/dotnet.exe`（**不在** tModLoader 根目录） |
| 外壳 | 跑 `.ps1` 必须用 **pwsh 7**（`C:\Program Files\PowerShell\7\pwsh.exe`）；Windows PowerShell 5.1 会把无 BOM 的 UTF-8 脚本当 GBK 读而解析失败 |
| Python | 脚本优先 `~/.cache/codex-runtimes/.../python.exe`，缺失时回退 `python`（3.13 可用，**未装 hjson 库**） |
| 外部依赖 | `Luminance.dll`（`artifacts/dependencies/`），由 `checks/prepare_luminance.py` 在编译前准备 |

## 目录职责

| 路径 | 职责 | 入库 |
|---|---|---|
| `ZenithReborn/` | 模组源码（121 个 `.cs`，平铺根目录、单命名空间）+ 资产 + 本地化 | 是 |
| `checks/` | 验证体系：`ZenithPrototypeCheck/` 隔离夹具模组、`run_*.ps1` 编排、`*.py` 静态与资产检查 | 是 |
| `docs/` | 版本记录、设计稿、机制定稿；`docs/archive/` 为历史 | 是 |
| `VfxLibrary/` | 原版特效素材库与可搜索图鉴（67 MB） | 是 |
| `tools/` | 反编译 / XNB 解包 / 材质实验室（体积大，二进制已 ignore） | 部分 |
| `artifacts/` | 构建、安装、检查产物与备份（16 GB） | 否 |
| `*-profile/`、`*-playtest/` | 隔离运行目录 | 否 |
| `.workbuddy/memory/` | 本地长期记忆（项目应用记忆，非本文件的记忆机制） | 否 |

## 命令

```bash
# 编译（只验 C#，不验本地化与资产）
dotnet build ZenithReborn/ZenithReborn.csproj -c Release

# 全量隔离回归：着色器编译 → 原生打包两包 → 起隔离服务端跑断言
pwsh -File checks/run_wood_checks.ps1

# 资产完整性（回归第一步，也可单独跑）：缺同名 png 又没 Texture override 就失败
python checks/check_mod_assets.py
```

回归的实测事实（2026-11-01）：
- 通过输出形如 `PASS: 8511 focused checks`，项数**按启用套件动态统计**（历史文档里的 8393、8453 已过时）。
- 脚本 **fail-fast**：第一条断言失败即抛异常，后面的检查根本不跑。
- 结果落 `artifacts/wood-check.txt`；`artifacts/native-build-check.json` 记录本次两包 sha256 与 `passed`（失败时也写 `passed:false`）。
- 单轮约 70–85 秒，不要按"十几分钟"设超时。

## 权威顺序

`build.txt` / 当前源码 / 最新验证与安装记录 **>** `README.md` **>** 设计稿 **>** `docs/archive/`。
设计稿存在"已实装 / 草案 / 已撤销"三态，读之前先看文件头部状态注记。
`docs/CHANGELOG.md` 是版本索引（107 条，倒序）；版本号**不保证单调**（0.3.118 的安装晚于 0.3.119，因为它撤回了 119 的加速）。

## 最容易踩的几条（硬规则原文见上方导入的 AGENTS.md）

1. **代码注释必须中文**，标识符保持英文。
2. **先找归属再写代码**，不把多职责塞进同一文件。
3. `SoulItem`(27 KB) / `SoulUI`(28 KB) / `SoulBlade`(29 KB) 已职责超载，新增能力优先开新文件。
4. 检查脚本只用隔离目录，**不碰** `My Games/Terraria/tModLoader/` 下的正式人物与世界；只替换目标 `.tmod`，先备份，文件被锁时等玩家退出，**不得强杀游戏**。
5. 大改动前提醒用户先落 git 提交点。
6. 证据分级：隔离夹具 < 图形客户端 < 真实多人。**服务端夹具看不到图形资产**——新增 Mod 类型必须有贴图或显式 `Texture` override，否则客户端加载即崩（0.3.127 热修的真实教训）。

## 协作偏好摘要（完整版在 `.workbuddy/memory/MEMORY.md`）

- **不质疑设计**：设计由用户定；只报实现层事实（编译、API 行为、性能、数据一致性），不把已定规则当"待定夺"回抛。
- **别问已有答案的问题**：用户给过的信息不复问；执行层细节自己定；只有真正的设计分叉才列选项，且以稿件内"待勾"形式呈现。工作模式是"我起草、他否决"。
- **剑范围硬边界 91 项**：71 常规 + 20 非常规；回旋镖、长矛、连枷、悠悠球、投掷刀斧一律不收。
- **设计宪法**：天顶剑的核心是"统御万千武器的攻击方式"，武器保持主体性、以自己的方式作战；玩家不是亲自挥招。
- 用户反感抄袭：借鉴只抄"问题"不抄"答案"。

## 已知长期空白

- 客户端画面、自然操作、多人联机、性能、美术观感**从未被自动验证**（`artifacts/install-result.json` 里对应三项长期为 `false`）。
- 飞刃族「印记」为用户认定的核心设计，留白未实现（设计草案，未实现：`ZenithReborn/` 下无 `FlyingBladeMark.cs`，机制描述见 `docs/飞刃族F1-掷刃与印记.md`）。
- 肉前八矿 M1 的独有机制仍是草案。
- `FlyingBladeShot` / `HolyHeavenStar` / `HolyHeavenImpact` / `LivingArmament` 四个弹幕的中文显示名未定。

## 指针

- 功能全貌与核心数值表 → `README.md`
- 硬规则、版本状态段、历史决策 → `AGENTS.md`（已由本文件顶部导入）
- 长期结论与用户偏好 → `.workbuddy/memory/MEMORY.md`（本地、不自动加载）
- 调度规则 → `.codebuddy/rules/00-delegation.md`
- 逐版本细节 → `docs/0.3.*.md`；索引 → `docs/CHANGELOG.md`
