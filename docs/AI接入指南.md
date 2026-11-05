# AI 接入指南（ZenithReborn）

> 读者：接手本项目的另一个 AI。本文自包含，读完即可开工。
> 本指南不替代 AGENTS.md / CODEBUDDY.md；动手前先读 AGENTS.md、CODEBUDDY.md，以及 docs/archive/DeepSeek开发交接-v0.3.11.md、docs/archive/DeepSeek接手提示词.md。
> 生成时间：2026-11-04，对应版本见 `ZenithReborn/build.txt`。

## 1. 这是什么项目

**「天顶复苏 · 诅咒残魂」（ZenithReborn）** —— tModLoader 1.4.4 的角色成长型天顶剑模组，无灾厄依赖。

核心机制：把近战武器寄宿进残魂物品，挥臂时由剑魂为每把寄宿剑各掷一把天顶式飞剑。成长靠六诅咒逐道解除限制并给对应祝福，**不是数值堆叠**。

| 项 | 值 |
|---|---|
| 版本 | `0.3.130`（以 `ZenithReborn/build.txt` 为准） |
| 回归基线 | 8516 项（2026-11-05 全量重跑），以 `artifacts/wood-check.txt` 首行为准；项数随夹具断言增删变化，`install-result.json` 的 8511 是 11-04 安装时的旧记录 |
| 仓库 | `https://github.com/lidaobaiqiao/ZenithReborn.git` |
| 源码规模 | `ZenithReborn/` 121 个 `.cs`，平铺根目录、单命名空间 |

## 2. 第一步：clone 并让它能编译

```bash
git clone https://github.com/lidaobaiqiao/ZenithReborn.git
```

**不用再改仓库里的任何文件**。跑一次上手脚本，它会把本机 tModLoader 路径写进 `local.props`（已 gitignore，永不入库），并装好 git 钩子、准备 Luminance、编译一次：

```bash
pwsh -File checks/dev_setup.ps1 -LoaderRoot "D:/你的路径/tModLoader"
```

路径解析优先级（三处消费方一致）：环境变量 `ZENITH_TML_ROOT` > `local.props` 的 `TmlRoot` > 默认 `E:/SteamLibrary/steamapps/common/tModLoader`。涉及 `ZenithReborn/ZenithReborn.csproj`、`checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj`、`checks/run_wood_checks.ps1`。

`checks/` 下另有三十余个专项探测脚本（GPU 采集、材质实验室）仍写死默认路径，换机后按需本机改，**改完不要提交**。

多人协作的同步流程（分支、PR、冲突高发点）见 [协作开发指南.md](协作开发指南.md)。

## 3. 环境四件套

| 项 | 要求 | 踩坑 |
|---|---|---|
| .NET | net8.0 / C# 12 | 本机 PATH 的 `dotnet` 是 10.x，不是项目要求；回归打包用 `E:/SteamLibrary/steamapps/common/tModLoader/dotnet/dotnet.exe` |
| tModLoader | 1.4.4 | `tMLMod.targets` 在安装根目录；`dotnet.exe` 在 `tModLoader/dotnet/` **子目录** |
| PowerShell | **必须 pwsh 7**（本机 7.6.6，`C:\Program Files\PowerShell\7\pwsh.exe`） | Windows PowerShell 5.1 不可用（无 BOM UTF-8 脚本会被按 GBK 读而解析失败） |
| Python | 3.x | 全部脚本只用标准库，无需 pip |

## 4. 三条命令

```bash
# 只验 C#（不验本地化与资产）
dotnet build ZenithReborn/ZenithReborn.csproj -c Release

# 全量隔离回归：着色器编译 → 原生打包 → 起隔离服务端跑断言
pwsh -File checks/run_wood_checks.ps1

# 资产完整性（缺同名 png 又没 Texture override 就失败）
python checks/check_mod_assets.py
```

回归的性质：

- **fail-fast**：第一条断言失败就抛异常，后面的检查根本不跑。看到"只跑了 300 项就失败"不是套件少，是第一条就挂了。
- 项数**动态统计**（当前 8516），历史文档里的 8393 / 8453 / 8498 / 8509 / 8511 均已过时。
- 耗时约 70–85 秒，不要按"十几分钟"设超时。
- 结果落 `artifacts/wood-check.txt`（首行 `PASS: N focused checks [suite=full ZENITH_WOOD]`，末行 `SUITE=ZENITH_WOOD`）；`artifacts/native-build-check.json` 记录两包 sha256 与 `passed`。
- 全量回归由脚本用 tModLoader 自带 dotnet 重新打包（`run_wood_checks.ps1:24`，`dotnet ./tModLoader.dll -server -build`），与 §4 的 `dotnet build` 是两条独立链路；§7.1 关于夹具引用 `bin/Release` 旧 DLL 的告警只针对手动编译夹具。

## 5. 改代码之前先看地图

项目已建好代码库地图（2026-11-03 全量调查产出），共 5 份：

| 场景 | 文档 |
|---|---|
| 判断功能属于哪个模块、改它会波及什么 | `docs/CODEBASE_MAP.md` |
| 手上有现象要排查（不生效 / 数值不对 / 回归失败） | `docs/PROBLEM_INDEX.md` |
| 知道要改什么，找具体入口与行号 | `docs/HOTSPOTS.md` |
| 判断依赖方向、跨模块改动 | `docs/ARCHITECTURE.md` |
| 确认某文件干什么、属于哪个模块 | `docs/FILE_INDEX.md` |

三条使用纪律：

1. **地图是快照，源码是事实。** 冲突时按代码做，并顺手修正索引。
2. 先按地图定位，再读实际源码确认。**不要一上来全库 grep**，那是最慢的路径——用检索命令代替：
   ```bash
   python checks/map_query.py 飞刃            # 一步给「模块 / 文件 / 职责」，源码排在文档前
   python checks/map_query.py --module 木质族  # 看某模块下全部文件
   ```
3. 文件新增、删除、重命名后必须跑：
   ```bash
   python checks/build_file_index.py     # 重生成 docs/FILE_INDEX.md（生成物，不许手改）
   python checks/map_audit.py            # 覆盖率审计，<100% 或失效路径非 0 退出
   python checks/map_freshness.py        # 索引新鲜度 + 文件:行号 + 版本号 + 计数一致性
   ```

`map_freshness.py` 已挂进 `.git/hooks/pre-commit`（`pwsh -File checks/install_git_hooks.ps1` 安装，钩子不入库，新克隆要重跑一次）：地图落后于源码会**直接拦住提交**，确需绕过用 `git commit --no-verify` 并在提交说明里写原因。它拦得住索引过期、行号越界、版本号与计数不符；**拦不住模块职责描述是否仍然准确**——那部分只能靠改代码的人在同一个提交里更新。

判定口径：`map_freshness.py` 退出码 0 才代表地图可用；`map_audit.py` 100% 只代表"每个文件都被索引过"，两者不互相替代。

## 6. 六条硬规则（违反会返工）

1. **代码注释必须中文**，标识符保持英文。原作者靠注释理解代码，英文注释等于没有。
2. **先找归属再写代码**，不把多职责塞进同一文件/方法。`SoulItem`(27KB) / `SoulUI`(28KB) / `SoulBlade`(29KB) 已职责超载，新增能力优先开新文件。
3. **证据分级：隔离服务端夹具 ≪ 图形客户端 ≪ 真实多人。** 服务端夹具看不到图形资产——新增 Mod 类型必须有贴图或显式 `Texture` override，否则客户端加载即崩（这条是真实热修教训）。
4. **检查只用隔离目录**，绝不碰 `My Games/Terraria/tModLoader/` 下的正式人物与世界。只替换目标 `.tmod`，先备份；文件被锁就等玩家退出，**不得强杀游戏进程**。
5. 大改动前**提醒用户**先落 git 提交点（CODEBUDDY.md:71），不要自行提交；当前工作区状态未知，动手前先确认。
6. **不凭世界已击败 Boss 的全局标记伪造剑魂成长记录**，成长必须来自剑魂自己的记录。

## 7. 六个已经踩过的坑

### 7.1 构建产物不重编，夹具静默测旧逻辑
夹具用 HintPath 引用 `ZenithReborn/bin/Release/net8.0/ZenithReborn.dll`。改了主模组不重新 `dotnet build`，夹具测的是旧 DLL，**全绿也不代表你的改动正确**。核对：`checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj`。

### 7.2 小套件冒充全量回归
`checks/ZenithPrototypeCheck/Check.cs` 的分支顺序是 `GLASS:69 → INFORMATION_UI:75 → ECHO_ORE:83 → WOOD:125`，设了 `-Glass` / `-InformationUi` / `-EchoOre` 就只跑对应小套件，输出同样是 `PASS: ` 前缀。
`checks/package_install.py` 现已校验 `SUITE=ZENITH_WOOD` 标记 + 6800 项地板，但仍**永远用 `run_wood_checks.ps1` 跑全量**，不要手造报告。

### 7.3 夹具的全局毒开关
`checks/ZenithPrototypeCheck/CurseCheck.cs:15-19` 的 `NeutralDefaults` 默认 `false` 时，新剑魂带六诅咒，`TraitItems` 只返回首把主剑。夹具靠调用前手动置 `true` 规避，**顺序颠倒会让"多剑生效"类断言集体假阴性**——测试莫名失败时先查这里。

### 7.4 33 个特效开关是空开关
`ZenithReborn/VfxIsolation.cs` 的 `Parts`(P01~P26，约 `:107-135`) 与 `GlassParts`(7，约 `:226-235`) 共 33 项**没有渲染消费方**：`PartValue` 全库零调用，打包的 `.fx` 里没有对应 uniform。调这些值只改调试面板显示，不影响画面。调试面板已标「（未接线）」。真正生效的是 `Branches` 那 10 项。

### 7.5 木质族有三套并行计数口径
- `ZenithReborn/SoulCurses.cs` 的 `CombatWood`（约 `:86-87`，过诅咒过滤）——战斗真实值
- `ZenithReborn/SoulSupport.cs:43` 的 `FamilyItems`（不过滤）——族计数唯一入口
- `ZenithReborn/BondSnapshot.cs:59` 的 UI 取值（走 `FamilyItems`）

改族判定规则必须**三处同改**，否则出现"面板显示 8/8 但倍率只算 1.1"这种自相矛盾。

### 7.6 版本号不改会撞哈希断言
`checks/package_install.py` 要求要安装的包 sha256 与回归报告绑定的包完全一致。推断：改代码 → 重跑 `run_wood_checks.ps1` → 再安装。改 `build.txt` 的 `version` 必须重新打包，否则断言直接挡下并提示"你改完源码后没有重跑"。

## 8. 提交纪律

**不提交**：`artifacts/`（本地产物，约 16 GB）、`*-profile/`、`*-playtest/`、`*/bin`、`*/obj`、人物与世界存档、完整日志、反编译产物、凭据。

**提交前**：至少跑一次 `pwsh -File checks/run_wood_checks.ps1`，确认 PASS。

**范围**：只改自己负责的模块；跨模块修改要写清原因与影响范围。

**地图维护**：新增/删除/重命名文件、模块职责变化、跨模块依赖变化后，跑
```bash
python checks/build_file_index.py && python checks/map_freshness.py && python checks/map_audit.py
```
三条全绿再提交；pre-commit 钩子会对前两条做同样的把关。

## 9. 设计边界（不要自作主张）

- **剑范围硬边界 91 项**（71 常规 + 20 非常规）：回旋镖、长矛、连枷、悠悠球、投掷刀斧一律不收，哪怕行为同构。
- **设计宪法**：天顶剑的核心是"统御万千武器的攻击方式"。武器保持主体性、以自己的攻击方式作战，玩家不是亲自挥招。任何让玩家"亲自挥招"的设计都背离核心。
- **不要质疑既定设计**。只报实现层事实：编译、API 行为、性能、数据一致性。设计由项目作者定。
- **未实装的东西别当已实装**：
  - 飞刃族「印记」是设计留白（`ZenithReborn/` 下无 `FlyingBladeMark.cs`）
  - 肉前八矿 M1 的独有机制仍是草案
  - `FlyingBladeShot` / `HolyHeavenStar` / `HolyHeavenImpact` / `LivingArmament` 四个弹幕的中文显示名未定

## 10. 卡住了看哪里

| 现象 | 先看 |
|---|---|
| 编译失败 | 两处 `tMLMod.targets` 路径是否改对 |
| 回归莫名失败 | 三处硬编码路径是否都改对：两处 csproj 的 `tMLMod.targets` + `run_wood_checks.ps1:4` 的 `$loaderRoot`；再查是否重编 DLL（7.1）、是否撞毒开关（7.3） |
| 安装被拒 | 是否用 `run_wood_checks.ps1` 跑了全量（7.2）、是否改了源码没重跑（7.6） |
| 改了族判定但数字不对 | 三套口径是否同改（7.5） |
| 改了特效开关没反应 | 是不是空开关（7.4） |
| 不知道从哪改 | `docs/PROBLEM_INDEX.md` 按现象查 |
