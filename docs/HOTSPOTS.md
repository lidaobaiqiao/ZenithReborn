# 开发热区速查（HOTSPOTS）

> 基线：`ZenithReborn/build.txt` → `version = 0.3.130`。配套文档：[CODEBASE_MAP.md](./CODEBASE_MAP.md)（结构全图）、[PROBLEM_INDEX.md](./PROBLEM_INDEX.md)（按现象反查）。
> 本文只回答一件事：**要改某类东西，从哪个文件的哪一行下手，以及会连带打破什么。**

## 用法与约定

- 所有 `文件:行号` 已对照当前磁盘源码核验；行号表达"区块起点"，函数体长度会变化，改动手前用符号名再 grep 一次更稳。
- **高频** 需要真实的修改频率证据。本仓库没有提交历史级的频率统计，所以除少数被多次热修、被多份文档反复引用的点（版本号、回归入口、槽位常量）外，一律标 `（重要入口，非高频）`——那是"绕不开的枢纽"，不是"经常改的地方"。
- `（重要入口，非高频）` 标注位于该行末列。
- 每条只写"入口 → 场景 → 依赖"，细节论证见 CODEBASE_MAP 对应小节。

---

## 1. 物品与注册

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 剑魂本体 | `ZenithReborn/SoulItem.cs:11` | `partial class SoulItem : ModItem` | 加字段、改 tooltip、改 `CanUseItem` | `SaveData/LoadData`、`NetSend/NetReceive`、hjson `Items.SoulItem` |
| 模组主类 | `ZenithReborn/ZenithReborn.cs:8` | `class ZenithReborn : Mod` | 注册 `ModSystem`、全局 `ILHook`、加载期入口 | `BlessingCamera`、`SoulVisuals` 等 `Autoload` 失败即整体崩溃 |
| 魂芯物品 | `ZenithReborn/Progression.cs:162` 区块 | `abstract class SoulCore` + 6 子类 | 新增魂芯、改魂芯掉率/条件 | `SlotProgression.CanAcceptCore`、hjson `Items` 段 6 条 |
| 觉醒所需十剑 | `ZenithReborn/Progression.cs:15` | `ZenithWeapons[10]` | 替换材料剑、改觉醒门槛 | `SoulItem.Potency`、`AttachmentStory`、`SoulTraits` 文案 |
| 改这类东西必看 | — | — | 新增 `ModItem` **必须**有同名 png 或显式 `Texture` override，否则客户端加载即崩（0.3.127 真实热修）；物品名/说明在 hjson，界面文案在 `.cs` | `checks/check_mod_assets.py`、`Localization/zh-Hans.hjson` |

## 2. 剑魂数据

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 槽位常量与容量 | `ZenithReborn/SoulItem.cs:13-20` | `Hosts[12]`、`LegendarySlotId=34`、`CrownSlotBase=35` | 加槽、改槽号、改传奇/圣林槽语义 | `ItemAt`、`VisibleSlotIds`、`SoulArmoryLayout.Slot`、存档 |
| 槽序号双射 | `ZenithReborn/SoulItem.cs:26` / `:28` | `SupportSlotId` / `SupportOrdinal` | 支援槽编号规则调整 | UI 命中框、`SoulSupport.Capacity` |
| 有效出战集合 | `ZenithReborn/SoulItem.cs:82` | `Active` | 改"哪些槽真正出战" | 伤害、射程、六套族计数、`BondSnapshot` |
| 容量派生 | `ZenithReborn/SoulItem.cs:47` / `:48` | `BaseCapacity` / `Capacity` | 改槽数上限（当前 `min(10, 进度)`） | `SoulProgress.Capacity`、`SetItems` |
| 唯一寄宿写入 | `ZenithReborn/SoulItem.cs:95` | `TrySwapHost` | 槽位校验、同种去重、封印拦截 | `CanHost`、`RefreshStats`、UI 取放 |
| 派生值刷新 | `ZenithReborn/SoulItem.cs:387` | `RefreshStats` | 任何改了物品字段之后 | 不调它 = 面板/图鉴全部陈旧；夹具固定三步之一 |
| 改这类东西必看 | — | — | 槽号 17-24 是存档兼容段不可回收；改 34/35/36 会同时打破 `ItemAt`、`VisibleSlotIds`、`SoulArmoryLayout.Slot`、UI 命中框与 `SoulUIEffects` 绘制 | `SoulProgress.SlotGates`、`SoulSupport` |

## 3. 诅咒与难度

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| Boss 组表 | `ZenithReborn/SoulCurses.cs:19` | `Groups[6][]` | 加 Boss、调四档阈值 | `StageForCount`、`SoulBossUnlocks.Read` 奖励行数 |
| 铭刻映射 | `ZenithReborn/SoulCurses.cs:24` | `BossSeal(int)` | 18 位封印扩位 | `SoulCurses.All`、`SaveCurses/LoadCurses`、网络包 |
| 出剑把数 | `ZenithReborn/SoulCurses.cs:55` / `:56` | `Blades` / `BladesAtStage` | 调"独行"上限（2/3/4…10） | `SoulSwingPlayer` 的 `budget` |
| 特性开关 | `ZenithReborn/SoulCurses.cs:82` | `TraitEnabled(type)` | 切换诅咒未解时只认**第一把**主攻 | `TraitMains/TraitSupports/TraitItems` |
| 受诅咒过滤的战斗口径 | `ZenithReborn/SoulCurses.cs:87` | `CombatWood`（注释在 `:86`） | 木质战斗用哪套计数 | `SoulSupport.FamilyItems` + `TraitEnabled`；与 `BondSnapshot.Wood` 差一份数据属预期 |
| 难度开关 | `ZenithReborn/SoulDifficulty.cs:25` | `Toggle` | 传奇/FTW 切换 | **真实改写 `Main.GameMode`/`getGoodWorld` 并写世界存档**，联机必须拒绝 |
| 改这类东西必看 | — | — | 三套并行 Boss 位映射不可混用：`BossSeal`(18 位) / `SoulProgress.BossBit`(12 位) / 头像图集 key；`Queen/Empress/Duke/Deer` 只解诅咒不给槽位 | `Progression.cs`、`SoulCurseDescriptions.cs` |

## 4. 族名册与判定

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 木质名册 | `ZenithReborn/WoodTypes.cs:29` | `Types`（8 木）+ `Mask`@`:41` | 换木剑、满套判定 `0xFF` | `ForestPower.FullSet`、`WoodCrown`、`WoodKind` 枚举 |
| 矿石名册 | `ZenithReborn/OreFamily.cs:43` | `MaterialSwords`（14 材质 22 把） | 加材质/换剑 | `MaterialCount=14`@`:16`、`Mask`@`:70`、`HardOreShot` 的 `Tints` 偏移 |
| 飞刃名册 | `ZenithReborn/FlyingBladeFamily.cs:19` | `Weapons`（31 把） | 换名册、改阶位 | `Stages`@`:33`、`WeaponFamilyTags.Of`、阶位 tooltip |
| 神圣/剑灵名册 | `ZenithReborn/HolyHeavenFamily.cs:9`、`ZenithReborn/LivingArmamentFamily.cs:9` | `Weapons`(8) / `Members`(4) | 换成员 | 珍珠木剑**同时属木质**；剑灵两处标注"暂定候选" |
| 剑灵玩家代理 | `ZenithReborn/LivingWeaponProxy.cs:30`、`ZenithReborn/LivingProxyOwnerState.cs:10` | `Tick` / `Copy` | 武器使用、战斗数值、招架/回血 | 鼠标与 Current 必须恢复；招架只同步直接命中时实际消费，蝙蝠棍治疗由 `LivingProxyHealing` 转发 |
| 剑灵弹幕与视线 | `ZenithReborn/LivingChannelProjectile.cs:17`、`:49`、`:67` | `OnSpawn` / `RunWithProxy` / `CanHitWithProxy` | 依附型弹幕、持续操控、方块视线 | 仅本地匹配代理期间临时替换 `Main.player[owner]`；AI、视线都需 `finally` 恢复 |
| 族标签唯一出口 | `ZenithReborn/WeaponFamilyTags.cs:10` | `Of(type)` | 新增族必须在此追加 | 固定顺序：飞刃→神圣→剑灵→木质→矿石 |
| 改这类东西必看 | — | — | 名册变动要同步"判定位宽 + 枚举 + 三张文案表 + 固定长度循环"：`0xFF`、`SendExtraAI/ReceiveExtraAI` 的 8 次循环、`Clamp(...,0,13)` 之类硬编码 | `SoulTraits`、`EnvironmentTooltips` |

## 5. 计数与加伤

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 族计数唯一入口 | `ZenithReborn/SoulSupport.cs:43`（注释 `:39-43`） | `FamilyItems = Active ∪ Supports` | 任何"数几把剑"的地方 | 已含奖励位/传奇位；`SetItems`(`:38`) 不含，防自举 |
| 支援格授予 | `ZenithReborn/SoulSupport.cs:45` | `GrantedBy(soul,"wood"/"ore")` | 改"多少把剑给几格" | 木质只数 `SetItems`，矿石并入森林/圣林 |
| 木质支援不动点 | `ZenithReborn/SoulSupport.cs:16` | `Capacity`（上界 `MaximumGrants=11`） | 支援格算法调整 | 与 `Capacity`/`GrantedBy` 互为输入，2–3 轮收敛 |
| 剑伤公式 | `ZenithReborn/SoulCombat.cs:9` | `BladeDamage = GetWeaponDamage × Potency × MainDamageMultiplier` | 调伤害 | 唯一伤害入口；`Potency` 在 `SoulItem.cs:89` |
| 矿石倍率 | `ZenithReborn/OreFamily.cs:83` | `MainDamageMultiplier` | 改 2/4 材质加成 | 同时影响成员伤害与 `DamageShare` |
| 展示口径 | `ZenithReborn/BondSnapshot.cs:59` / `:67` | `Wood(soul)` / `Ore(soul)` | 面板数字对不上战斗 | `Wood` 不过滤诅咒（第三套口径），与 `CombatWood` 同屏不同值属预期 |
| 改这类东西必看 | — | — | 木质存在 **三套并行计数**（族内 / 战斗 / 支援授予），改一套必须三套同改；`UnlockCount`(`:44`) 生产零调用，别拿它当口径 | `EnvironmentRules.cs:15` 的收集位换算同理 |

## 6. 羁绊与门槛

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 阶段阈值 | `ZenithReborn/BondSnapshot.cs:38` / `:40` | `WoodThresholds=[1,2,4,6,8]` / `OreThresholds=[1,2,4,6,8,10]` | 调阶段 | `SoulTraits` 写死文案、`SoulSupport.Grants`、面板色阶 |
| 四复合羁绊 | `ZenithReborn/WoodBondCore.cs:29`/`:39`/`:49`/`:61` | `ActiveForge/Rot/Ember/Crown` | 加/改羁绊组合 | 直伤走 `NPC.StrikeNPC`，不回灌乘区管道 |
| 圣林归冠 | `ZenithReborn/WoodCrown.cs:20` | `Active`（重入护盾 `evaluating`@`:18`/`:23`） | 改归冠条件 | `SoulItem.CrownCapacity` → `FamilyItems` → `Active` 的唯一环 |
| 森林之力 | `ZenithReborn/ForestPower.cs:14-15` | `BaseCap=6` / `CrownCap=12` | 调层数上限/衰减 | `WoodPlayer.EffectiveLayers`、`RecentCombat` 300 帧 |
| 神圣落星 | `ZenithReborn/HolyHeavenProc.cs:11` / `:12` | `StarDamage=1000` 常量 / `CooldownFrames=300` | 调星伤、改冷却 | **不吃任何倍率**；键为 `player:SoulId`，换魂重置 |
| 改这类东西必看 | — | — | 阈值改动会让 `BondSnapshot.Build` 的 `Clamp` 隐藏超额；原 `NextThreshold/PrevThreshold`(`:23`/`:25`) 零外部引用、改它们没有效果，该属性已随死码清理删除（保留历史记录；`:23`/`:25` 为历史快照行号，已失效） | `SoulUIWoodPanel`、`SoulUIArmoryBondCore` |

## 7. 环境属性

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 环境枚举 | `ZenithReborn/EnvironmentAttributes.cs:10` | `enum SwordEnvironment`（None=0 + 1..21） | 新增环境 | `EnvironmentRules.All=(1<<21)-1`、`MechanismNames[22]` |
| 武器→环境绑定 | `ZenithReborn/EnvironmentAttributes.cs:87` | `RegisterBuiltins` | 给新武器归环境 | **唯一手工绑定表**，漏绑 → tooltip"未归类"、机制不跑 |
| 名额与倍率 | `ZenithReborn/EnvironmentRules.cs:15` / `:16` | `BlessingSlots = Count/4` / `Shared = 1+0.2n` | 调数值 | `Forest` 1.15、`Adaptation` 0.5 |
| 地形判定 | `ZenithReborn/EnvironmentRules.cs:61` / `:80` | `Natural` / `Adaptation` | 改群系/地层归属 | 群系优先于深度；互斥群系 ×0.5 |
| 登记与开关 | `ZenithReborn/EnvironmentRules.cs:99` / `:106` | `RegisterEnvironments` / `ToggleEnvironmentBlessing` | 收集、祝福、领域 | 名额不足静默返回 false，UI 只走 `feedback` |
| 机制总表 | `ZenithReborn/EnvironmentPlayer.cs:35` / `:155` | `Mechanism` / `Hit` | 加环境机制 | **仅"已收集"只给属性不给机制** |
| 改这类东西必看 | — | — | 新增环境要同步 9 处（枚举、`All` 位宽、`MechanismNames`、`stats`、`Descriptions`、`Color`、`Natural`、`Adaptation`、`EnvironmentIcons` 必须 1:1 对齐 22 项）——**全项目耦合点最多的手工同步表** | `EnvironmentUI.cs` 里 21 的硬编码 |

## 8. UI 与文案

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 总绘制入口 | `ZenithReborn/SoulUI.cs:341` | `Draw()`（页面分发在 `:382` 附近） | 加页面、改布局 | `SoulUIPages`、`SlotBounds` |
| 命中框 | `ZenithReborn/SoulUI.cs:211` | `SlotBounds(index)` | 点不中/点错槽 | 域模式 page0 委托 `SoulArmoryLayout.Slot`@`:31` |
| 页面号与标题 | `ZenithReborn/SoulUIPages.cs:6` / `:13` | `SoulUIPages` / `Name(int)` | 改导航（当前只含 `{0,1,2,5}`） | 硬编码中文，不走本地化 |
| 输入隔离 | `ZenithReborn/SoulInput.cs:41` | `MaskBackground` | "点 UI 时人也在挥剑" | 必须同时清 `Main.mouseLeft`、`PlayerInput.Triggers.Current.MouseLeft`、`player.controlUseItem` |
| 木质面板 | `ZenithReborn/SoulUIWoodPanel.cs:33` | `DrawWoodPanel` | 木质数值展示 | 两套口径并存：`hostedMask` 与 `CombatWood` |
| 改这类东西必看 | — | — | `SoulArmoryLayout` 是 UI 与点击判定的**唯一共享几何源**，改它要同改 `SlotBounds` 与 `SupportOrdinal`；`SoulInput` 时序改动会波及整个游戏输入 | `SoulDomainUI`（`DomainMode` 默认 true，游戏内无写入点，浮窗分支约 300 行属休眠路径） |

## 9. 渲染与着色器

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 总闸 | `ZenithReborn/VfxIsolation.cs:12` | `PureCodeOnly => false`（**表达式不是字段**） | 一键关掉外部输入/贴图/滤镜 | 一改影响全部非纯几何特效，不可运行时切换 |
| 真生效开关 Branches | `ZenithReborn/VfxIsolation.cs:63-76` | `Branches`（B1–B10） | 关拖尾/剑影/星芒/玻璃滤镜 | 面板与 `ResetAll` 读同一元数据；B7 `GlassFilter` 是最大性能开关 |
| 空开关 Parts | `ZenithReborn/VfxIsolation.cs:107-134` | `Parts`（P01–P26） | 按 F9 只改面板文字，**画面零变化** | `PartValue`@`:141` 全库零调用；`.fx` 内 `uPart` 出现 0 次（重要入口，非高频） |
| 绘制状态保存 | `ZenithReborn/VfxRenderState.cs:9` | `DrawWithRestoredState` | 混合/深度/裁剪态泄漏 | 只保存 **0~2 号**纹理槽，而 `SurfaceMaterialRenderer` 用 16 槽（重要入口，非高频） |
| 世界层拖尾 | `ZenithReborn/SoulVisuals.cs:54` | `PostDrawTiles` | 拖尾不出/重影 | `CompositingReady` 时提前 return |
| 逐项调试 | `ZenithReborn/VfxPartDebug.cs:23` | `PostUpdateEverything`（F7/F8/F9/F10） | 现场试开关 | 61 项标签硬编码中文，不参与本地化 |
| 改这类东西必看 | — | — | `Parts`/`GlassParts` 共 33 项是空开关，别在它们上面调参；`SurfaceMaterial` 管线整体休眠（`bindings.json` 的 `weapons` 为 `{}`）；4 个 `.fxh` 是双重孤儿 | `checks/compile_effects.py`、`SwordFlowMaterial.cs:223` |

## 10. 本地化

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 中文条目 | `ZenithReborn/Localization/zh-Hans.hjson`（`Items` 段起 `:2`，`Projectiles`@`:114`） | `Mods.ZenithReborn.Items.*` | 改物品名/说明 | 两文件各 133 行，必须同键同步 |
| 英文条目 | `ZenithReborn/Localization/en-US.hjson:114` | 同上 | 同上 | 中文 `Projectiles` 缺 6 条（飞刃/星/冲击波/剑灵/环境机制） |
| 唯一本地化断言 | `checks/ZenithPrototypeCheck/Check.cs:172` | `Assert(...DawnCore.DisplayName == "初光魂芯")` | 验证本地化加载 | 全局只有这 1 条（重要入口，非高频） |
| 改这类东西必看 | — | — | **界面文字全部硬编码在 `.cs`**（`SoulUIPages.Name`、"武器寄宿"等）：改界面文案改代码，改物品名改 hjson；原 `SlashZenithTest` 中文侧两键被注释会退回类型名，该遗留测试剑及本地化键已随 T5 删除，此例已失效（已删除，保留历史记录） | `SoulTraits`、`EnvironmentTooltips` 的文案表 |

## 11. 测试与回归

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 调度入口 | `checks/ZenithPrototypeCheck/Check.cs:11` | `class Check : ModSystem` | 加分支 | `PostAddRecipes`(服务端) / `PostUpdateEverything`(客户端) |
| 路由机制 | `checks/ZenithPrototypeCheck/Check.cs:34` | 读 `ZENITH_CHECK_REPORT`，为空整体退出 | 新增 `ZENITH_*` 套件 | 14 个现成分支各自硬编码清单并 `Environment.Exit(0)` |
| 总回归入口 | `checks/ZenithPrototypeCheck/Check.cs:125` | `ZENITH_WOOD`（串联 29 个套件） | 改完主模组跑它 | 先 `dotnet build` 刷新 DLL，否则静默测旧逻辑 |
| 断言原语 | `checks/ZenithPrototypeCheck/Check.cs:15` | `Assert(bool,string)`（失败即抛） | 写新夹具 | `PASS: N` 是成功数不是总数；fail-fast |
| 夹具引用方式 | `checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj:4` | `HintPath` → `bin/Release/.../ZenithReborn.dll` | 改主模组后必须重编 | 引的是**上次构建产物**；`:3` 的 tML 路径硬编码 `E:/...`，不可移植 |
| 改这类东西必看 | — | — | `CurseFixture.NeutralDefaults`(`Check.cs:115`/`:127`/`:150`) 是全局毒开关，顺序不可颠倒，否则"多剑生效"类断言集体假阴性；`-server` 下 26 个渲染夹具直接 return，PASS 不能证明画面 | `checks/run_*.ps1`、`artifacts/*.json` |

## 12. 构建与安装

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 唯一完整回归 | `checks/run_wood_checks.ps1` | `:13` 资产检查 → `:17` 着色器编译 → `:31` `ZENITH_WOOD=1` → `:35` `wood-check.txt` → `:44` `native-build-check.json` | 改完主模组走一遍 | 必须 `pwsh` 7（5.1 会按 GBK 解码中文注释直接报错） |
| 打包安装与三重校验 | `checks/package_install.py` | `:70` 排除 `docs/checks/VfxLibrary/tools`；`:20` 读回归通过数；`:222` 写 `install-result.json` | 出成品包/装机 | 断言"被测包 = 安装包"：`wood-check.txt` 时间戳晚于 DLL、包哈希三方一致 |
| 着色器编译 | `checks/compile_effects.py`（由上述 `:17` 调用） | `.fx` → `.fxc` + `build-manifest.json` | 改 `.fx` | 失败即中断回归；包内 shader 集合必须**恰好等于**清单（重要入口，非高频） |
| 构建排除 | `ZenithReborn/build.txt:5` | `buildIgnore = bin/*, obj/*, *.csproj, Assets/Effects/*.fxh` | 调整打包内容 | `.fxh` 被排除故成孤儿；`docs/checks` 另由 `package_install.py:70` 排除 |
| 改这类东西必看 | — | — | 先跑飞刃夹具≠能装：两者抢 `build-profile`；`install-result.json` 的 `graphics_*_tested` 目前全 `false`，报告字段有 `tested_package_sha256` / `testedPackageSha256` 两种拼写，写错静默变 False | `checks/prepare_luminance.py`、`artifacts/backups/` |

## 13. 版本号与文档

| 功能 | 精确路径:行号 | 关键符号 | 常见修改场景 | 相关依赖 |
|---|---|---|---|---|
| 版本号源头 | `ZenithReborn/build.txt:3` | `version = 0.3.130` | 每次发版 | 与 `:1` `displayName` 一起决定包名与备份名 |
| 版本索引 | `docs/CHANGELOG.md:8` | 表格首行（倒序） | 新增一行 | 版本号**不保证单调**（0.3.118 的安装晚于 119） |
| 结构图基线 | `docs/CODEBASE_MAP.md:3` | `> 基线版本：...0.3.130` | 发版同步 | 本文与 `PROBLEM_INDEX.md` 的行号也依赖它 |
| 协作上下文 | `AGENTS.md:3` | "当前正式安装0.3.130…" | 写验证结论/未验证项 | `CODEBUDDY.md:14`（当前 0.3.130，已同步） |
| 版本记录文件 | `docs/0.3.130-木质族V2重做与口径统一.md:1` | 每版一篇 | 新建 `docs/<ver>-<主题>.md` | 被 `CHANGELOG.md` 链接 |
| 改这类东西必看 | — | — | 改版本要同步 6 处：`build.txt:3`、`docs/CHANGELOG.md:8`、`docs/CODEBASE_MAP.md:3`、`AGENTS.md:3`、`CODEBUDDY.md:14`、新版本记录 md。文档里写"通过"必须区分 源码/已编译/夹具/实机 四档 | `artifacts/install-result.json` |

---

## 附：如果只记 8 条

1. `SoulItem.cs:82` `Active` —— 几乎所有派生值的上游。
2. `SoulSupport.cs:43` `FamilyItems` —— 族计数唯一入口。
3. `SoulCurses.cs:87` `CombatWood` —— 战斗口径（过滤）vs 面板口径（不过滤）。
4. `SoulItem.cs:95` `TrySwapHost` —— 唯一寄宿写入。
5. `SoulBlade.cs:294` `OnHitNPC` —— 五族命中唯一扇出点，隐式优先级链。
6. `VfxIsolation.cs:63` `Branches` 真生效 / `:107` `Parts` 空开关。
7. `checks/ZenithPrototypeCheck/Check.cs:125` `ZENITH_WOOD` —— 改完必跑。
8. `ZenithReborn/build.txt:3` —— 版本号，改它要连带 5 处文档。
