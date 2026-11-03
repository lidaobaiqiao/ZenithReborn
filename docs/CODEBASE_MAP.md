# 天顶复苏 · 诅咒残魂 — 代码地图

> 基线版本：`ZenithReborn/build.txt` → `version = 0.3.130`（`displayName = 天顶复苏 · 诅咒残魂`）
> 本文所有 `文件:行号` 均已对照当前工作区源码抽查核验；与既有调查稿不一致处以源码为准（差异清单见文末）。
> 工作区处于「木质族 V2」未提交改动状态，本文按**当前磁盘内容**描述。

---

## 0. 项目定位与技术栈

**一句话定位**：tModLoader 1.4.4 模组。玩家持有一件"剑魂"物品（`SoulItem`），把原版近战剑寄宿进它的槽位；模组按 **族（木质/矿石/飞刃/神圣天堂/剑灵）**、**六诅咒**、**21 环境属性** 三套正交维度重算伤害、射程、出剑数量与视觉表现。

| 项 | 内容 | 证据 |
|---|---|---|
| 目标框架 | `net8.0`，`LangVersion 12`，`BuildMod=false` | `ZenithReborn/ZenithReborn.csproj` |
| tML 版本 | 1.4.4（`tMLMod.targets`），引用 `Luminance` 做 primitive 渲染 | 同上 |
| 代码规模 | `ZenithReborn/*.cs` = **121** 个，平铺在根目录（无子命名空间分层） | `ls ZenithReborn/*.cs \| wc -l` |
| 着色器 | 3 个 `.fx`（`ZRSwordFlow` / `ZRGlassScreen` / `ZRMaterialSurface`）+ 3 个同名 `.fxc` 对照产物 | `ZenithReborn/Assets/Effects/` |
| 资产 | 52 张 PNG（矿石剑身图集 21 / 材质贴图 12 / Boss 立绘 19）+ `SwordSpark.png` + `ZoneMask.zrdata` | 见 §15 |
| 本地化 | `zh-Hans.hjson` / `en-US.hjson`，各 133 行，仅覆盖 `Items/Buffs/NPCs/Projectiles/Keybinds` | `ZenithReborn/Localization/` |
| 验证体系 | 独立附属模组 `checks/ZenithPrototypeCheck`（94 个 `.cs`）+ `checks/*.py` 54 个 + `checks/*.ps1` 37 个 | §17 |
| 打包排除 | `docs` / `checks` / `VfxLibrary` / `tools` 四目录不进成品包 | `checks/package_install.py:70` |

### 验证等级约定（全文通用）

| 标记 | 含义 |
|---|---|
| **源码** | 直接读代码得出，本次未运行 |
| **已编译** | 有构建/安装记录支撑（`artifacts/install-result.json`），本次未重跑 |
| **夹具** | 服务端夹具断言覆盖（`-server` 无窗口，**不含 GPU/实机画面**） |
| **实机** | 图形客户端人工确认；`install-result.json` 中 `graphics_*_tested` 目前仍为 `false` |

**结论**：本仓库到目前为止的"通过"全部属于 **源码 + 已编译 + 夹具**；视觉表现与真实多人从未被自动验证。

---

## 1. 目录职责表

| 路径 | 规模 | 职责 | 是否进成品包 |
|---|---|---|---|
| `ZenithReborn/` | 121 `.cs` + Assets + Localization | 主模组全部运行时代码；文件平铺，靠文件名前缀表达归属（`Soul*`=剑魂核心/UI，`Wood*`/`Ore*`/`FlyingBlade*`/`HolyHeaven*`/`Living*`/`Environment*`=各族，`Vfx*`/`Sword*`=渲染） | 是 |
| `ZenithReborn/Assets/Effects/` | 3 `.fx` + 3 `.fxc` + 4 `.fxh` + `build-manifest.json` | 着色器与编译对照。4 个 `.fxh` 是**双重孤儿**（无 `#include`，且被 `buildIgnore` 排除） | 是（`.fxh` 被排除） |
| `ZenithReborn/Assets/Vfx/` | `SwordBody/*.png` 21 张、`Materials/SwordFlow/*` 13 个 | 运行时贴图源（`ModContent.Request` 直接读） | 是 |
| `ZenithReborn/Assets/UI/BossPortraits/` | 19 张 PNG + `sources.json` | Boss 动态头像图集 | 是 |
| `checks/ZenithPrototypeCheck/` | 94 `.cs` + `Assets/` + `Localization/` | 独立验收模组，`modReferences = ZenithReborn`，用 **HintPath 引 Release DLL** | 否 |
| `checks/*.py` / `checks/*.ps1` | 54 / 37 | 构建期代码生成（`emit_*`）、资产校验、GPU 采集启动、打包安装 | 否 |
| `docs/` | 64 顶层 `.md` + `docs/archive/` 116 `.md` | 版本记录 / 设计定稿 / 参考表 / 索引 | 否 |
| `tools/` | 222 文件 | 制作端：`ZenithMaterialLab`（独立模组）、`ilspy`、`decomp`、`xnbcli` 源码与二进制 | 部分（`tools` 目录被排除，但 `ZenithMaterialLab` 源码入库） |
| `VfxLibrary/` | 11756 文件 | 制作端素材库 + 原版特效浏览器。`library.json` 自报 `runtimeIntegrated: false` | 否 |
| `artifacts/` | 约 16 GB | 构建产物、备份、安装记录、调查产物。**整目录被 `.gitignore` 忽略** | 否 |
| `build-profile/` `check-profile/` `wood-check-profile/` `flying-blade-profile/` 等 | — | 隔离运行档案（各自 `Mods/enabled.json`）。新增隔离目录必须手动加 `.gitignore` 规则 | 否 |

---

## 2. 剑魂数据模型

**职责**：定义剑魂物品本身——5 组槽位数组、槽号映射、容量/效力/射程派生、换入换出校验、存读档与网络；并把"族门槛 → 支援格数"做成不动点求解。

**核心文件**：`SoulItem.cs`（489 行，`partial`）、`SoulSupport.cs`（54 行）、`SoulCombat.cs`（11 行）

| 入口 | 位置 | 说明 |
|---|---|---|
| `Hosts[12]` | `SoulItem.cs:13` | 普通主攻槽（0-9 有效，10-11 为旧扩展） |
| `LegendarySlotId = 34` | `SoulItem.cs:17` | 传奇剑位（仅大师+FTW+已附着） |
| `CrownSlotBase = 35` | `SoulItem.cs:24` | 圣林之冠双剑位（35/36） |
| `SupportSlotId(int)` | `SoulItem.cs:26` | 序号→槽号；≥34 时 +2 跳开传奇与圣林 |
| `SupportOrdinal(int)` | `SoulItem.cs:28` | 槽号→序号；34 与圣林槽返回 `-1` |
| `VisibleSlotIds` | `SoulItem.cs:31` | 17 个基础槽 + 动态支援 + 传奇 + 圣林，UI/渲染唯一取槽列表 |
| `BaseCapacity` / `Capacity` | `SoulItem.cs:47` / `:48` | `Attached ? min(10, SoulProgress.Capacity(Kills,Cores)) : 0` |
| `SupportCapacity` | `SoulItem.cs:51` | → `SoulSupport.Capacity(this)` |
| `BonusCapacity` | `SoulItem.cs:57` | 仅 `OreMastered` 时 +2（森林奖励位 15/16） |
| `CrownCapacity` | `SoulItem.cs:60` | `WoodCrown.Active(this)` 时 +2 |
| `Stored` | `SoulItem.cs:61` | 全部数组串接，含传奇位；用于"去重/取回"判定 |
| `Supports` | `SoulItem.cs:66` | `AllSupportSlots.Take(SupportCapacity)` 且非空 |
| `ItemAt(int)` | `SoulItem.cs:67` | 槽号→`Item` 的唯一反查（覆盖 0-36 全段） |
| `Active` | `SoulItem.cs:82` | **全模组最敏感口径**：主攻 `Take(Capacity)` ∪ 森林奖励 ∪ 圣林 ∪ 传奇 |
| `Potency` | `SoulItem.cs:89` | 未附 0；常态 `.65 + Stage*.04`；觉醒 `1.1` |
| `CanHost(Item)` | `SoulItem.cs:92` | 近战 + 无镐斧锤 + 非剑魂 + 非天顶剑 + 非消耗 + `maxStack==1` |
| `TrySwapHost` | `SoulItem.cs:95` | **全模组唯一寄宿写入入口** |
| `RefreshStats` | `SoulItem.cs:387` | 写物品面板；**不调它则一切派生值陈旧** |
| `Reach(Player)` | `SoulItem.cs:419` | 基础 192 + `Stage*45` + 觉醒 150，再按族 Range 与环境/祝福调整 |
| `EmitBlade` | `SoulItem.cs:444` 区块 | 生成 `SoulBlade` 并写 `SoulSource` 四字段 |
| `BladeDamage` | `SoulCombat.cs:9` | `GetWeaponDamage(host) * Potency * OreFamily.MainDamageMultiplier` |
| `SoulSupport.Capacity` | `SoulSupport.cs:16` | 不动点迭代，上界 `MaximumGrants=11` |
| `SoulSupport.SetItems` | `SoulSupport.cs:38` | **不含**奖励位/传奇位（防自举） |
| `SoulSupport.FamilyItems` | `SoulSupport.cs:43` | `Active ∪ Supports`，族计数真源 |
| `SoulSupport.UnlockCount` | `SoulSupport.cs:44` | **生产零调用**，仅夹具/历史使用 |

**关联模块**：全部。上游写入方只有 `TrySwapHost`（UI）与 `LoadData`/`NetReceive`（存档/网络）；下游读取方涵盖 UI、战斗、五族、羁绊、图鉴。

**关键数据流**
1. 解锁链：`SoulCore.UseItem` → `SoulProgress.AcceptCore` → `Cores |= 1<<i` → `Capacity` → `Active` → 出剑把数。
2. 支援链：`SupportCapacity` → `SoulSupport.Capacity` 循环 `SetItems` → `WoodTypes.Mask`/`OreFamily.Mask` → 命中门槛 → 回写格数（2–3 轮收敛）。
3. 换装链：UI 点击 → `TrySwapHost` → 容量/封印/`CanHost` 校验 → 同种去重 → `RefreshStats`。

**常见问题定位入口**
- "槽位点不动/提示未解锁"：`TrySwapHost`（`:95`）的 `unlocked` 分支 + `SoulProgress.SlotHint`（`SlotProgression.cs:59`）。
- "支援格数不符预期"：`SoulSupport.Capacity`（`:16`）第 32 行的 `g.Id=="wood" ? items : roster` —— **木质只数 `items`，矿石数 `roster`（含奖励位）**。
- "放进去伤害没变"：`RefreshStats` 只改面板，真实伤害在 `SoulCombat.BladeDamage`。

**修改影响提醒**
- 槽号 17-24 是**存档兼容段，不可回收**；改 34/35/36 会同时打破 `ItemAt`/`VisibleSlotIds`/`SoulArmoryLayout.Slot`/UI 命中框/`SoulUIEffects` 绘制。
- 改 `Active` 组成会同时改变：伤害均值、射程、木质/矿石/飞刃/神圣/剑灵/环境六套计数、`BondSnapshot`、`SoulTraits`。
- `AllSupportSlots` 只 `Take(5)` 是刻意的存档兼容，不是 bug。

---

## 3. 诅咒与难度

**职责**：把"打到过哪些 Boss"翻译成六条能力轴上的四档等级（诅咒中→已削弱→已解除→已祝福），再翻译成出剑把数、每目标命中期数、穿地深度、射程下限与剑影伤害倍率；并实现御剑爆发与四类祝福运行时。

**核心文件**：`SoulCurses.cs`（121）、`SoulCurseDescriptions.cs`、`SoulEchoDamage.cs`（17）、`SoulBlessings.cs`（257）、`SoulDifficulty.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `SoulCurse` / `CurseStage` 枚举 | `SoulCurses.cs:9` / `:10` | 六诅咒 × 四档 |
| 铭刻位 `Eye..Deer`、`All=(1<<18)-1` | `SoulCurses.cs:14-15` | 18 位 |
| `Groups[6][]` | `SoulCurses.cs:19` | 每诅咒的 Boss 组；长度决定四档阈值 |
| `BossSeal(int)` | `SoulCurses.cs:24` | 18 位映射（与 `SoulProgress.BossBit` 12 位是两套） |
| `Count` / `RecordedStage` / `Stage` | `SoulCurses.cs:31` / `:32` / `:38` | `Stage` 会被 `LegacyReleased` 抬到 Released |
| `StageForCount` | `SoulCurses.cs:37` | 1→Weakened、n-1→Released、n→Blessed |
| `ScreenEdge` | `SoulCurses.cs:46` | 人物到屏幕左右边界距离，无图形环境退回 960 |
| `Blades` / `BladesAtStage` | `SoulCurses.cs:55` / `:56` | 独行诅咒：2/3/4/…/10 |
| `HitLimit` / `TerrainAllowance` | `SoulCurses.cs:59` / `:62` | 断锋 3/5/∞；屏障 32/64/∞ |
| `TraitEnabled(type)` | `SoulCurses.cs:82` | 切换诅咒未解时只认**第一把**有效主攻 |
| `CombatWood` | `SoulCurses.cs:87` | `FamilyItems` 再过 `TraitEnabled` 的木质快照 |
| `SoulEchoDamage.Multiplier` | `SoulEchoDamage.cs:7` | 祝福 1 / 解除 `0.8^unit` / 其余 `0.20` |
| `BlessingTuning` | `SoulBlessings.cs:17` | `ChargeFrames=900, BurstFrames=600, CombatGrace=45` |
| `BlessingPlayer.TryActivate` | `SoulBlessings.cs:51` | 按 O；要求手持剑魂 + 缚身已祝福 + 满充能 |
| `BlessingPlayer.PostUpdate` | `SoulBlessings.cs:85` | 充能/爆发计时；最近 45 帧有战斗才涨 |
| `DistanceMultiplier` | `SoulBlessings.cs:123` | 近身 1 → 远端 +50%，分母用 `ScreenEdge` |
| `SoulDifficulty.Legendary` | `SoulDifficulty.cs:14` | `GameMode==2 && getGoodWorld` |
| `SoulDifficulty.Toggle` | `SoulDifficulty.cs:25` | **真实改写 `Main.GameMode`/`getGoodWorld` 并写世界存档** |

**关键数据流**：`SoulBossCredit.OnKill` → `Seals |= seal` → `Count` → `StageForCount` → `Blades/HitLimit/TerrainAllowance/Reach`。祝福：`TryActivate` → `BurstLeft=600` → `Reach` 的 `Unbounded` 分支 + `SoulSource.ModifyHitNPC` 的距离/环境乘区。

**常见问题定位入口**
- "诅咒没解除"：先看 `SoulBossCredit.OnKill`（`Progression.cs:136`）是否拿到参与者（`SoulEncounters.Hits`），再看 `Groups` 是否双 Boss 需两个 bit。
- "射程忽长忽短"：`Reach`（`SoulItem.cs:419`）的 `Unbounded=1e6` 分支会吞掉后续 ×2。
- "只出 2 把剑"：`Blades`（`:55`）—— 独行未解除时上限 2。
- "御剑条不涨"：`PostUpdate`（`:85`）四条件（手持剑魂 + 缚身祝福 + 45 帧内战斗 + 魂域关闭）。
- "难度按钮没反应"：`BlockReason`（`:16`）—— 联机/菜单/旅途/Boss 战全部拒绝。

**修改影响提醒**
- 改 `Groups` 长度 = 同时改四档阈值与 UI 奖励行数（`SoulBossUnlocks.Read`）。
- `SoulCurses.All`（18 位）与 `LegacyReleased`（6 位）参与存档与网络，扩位必须同步改存档版本。
- `SoulDifficulty.Toggle` 是高破坏性操作，禁止在非单人环境放开（`DomainCheck` 有断言，但只是单进程语义）。

---

## 4. 出剑与战斗

**职责**：把"挥一次剑魂"翻译成"按诅咒上限轮流发射若干把飞剑"，由 `SoulBlade` 负责弹幕运动学、剑影历史、命中单元判定、伤害乘区整合、特技释放与绘制数据源。

**核心文件**：`SoulBlade.cs`（521）、`SoulSwingPlayer.cs`（70）、`WeaponFamilies.cs`、`SoulCombat.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `SoulSource : GlobalProjectile` | `SoulBlade.cs:14` | 跨模块**来源身份总线**：`SoulId/SourceWeapon/PrimaryOrdinal/HostSlot/LeashRadius` |
| `SoulSource.ModifyHitNPC` | `SoulBlade.cs:31` | 叠祝福距离倍率 + 环境乘区 |
| `SoulSource.AI` | `SoulBlade.cs:56` | `LeashRadius` 超距销毁 |
| `SoulBlade : ModProjectile` | `SoulBlade.cs:63` | 弹幕本体 |
| `TryConsumeOreHit` | `SoulBlade.cs:103` | 矿石首击资格消费 |
| `CanDamage` | `SoulBlade.cs:141` | 本体停伤后由剑影接管 |
| `TryGetEchoPose` | `SoulBlade.cs:150` | 剑影姿态（绘制与碰撞共用），`opacity<=.02` 提前返回 |
| `CanHitNPC` | `SoulBlade.cs:160` | 读 `SoulCurses.HitLimit` |
| `AI` | `SoulBlade.cs:171` | 椭圆轨迹 + `trailCenters` 历史 + 地形穿透 + 24 帧后释放特技 |
| `ResolveHitUnit` | `SoulBlade.cs:268` | 本体优先，其次最近剑影 |
| `OnHitNPC` | `SoulBlade.cs:294` | **五族命中唯一扇出点** |
| `ModifyHitNPC` | `SoulBlade.cs:333` | 剑影倍率 + 木质标记 + 矿石/祝福修改 |
| `ReleaseSpecial` / `ReleaseWeaponSpecial` | `SoulBlade.cs:352` / `:363` | 特技弹幕（硬编码 5 把武器） |
| `DrawRibbonShader` | `SoulBlade.cs:458` | 带面顶点入口 |
| `PreDraw` | `SoulBlade.cs:485` | 三残影 → 剑体 → 剑尖星芒 |
| `SoulSwingPlayer.Begin` | `SoulSwingPlayer.cs:16` | 选主攻、`budget=min(hosts, Blades(soul))`、归鞘打断、飞刃互斥 |
| `budget` 计算 | `SoulSwingPlayer.cs:27` | 飞刃 `Launch` 成功则置 0 |
| `Valid` | `SoulSwingPlayer.cs:35` | 每帧校验武器未换/魂域未开 |
| `FireHost` / `PostItemCheck` | `SoulSwingPlayer.cs:39` / `:57` | ≤3 把每节点一把；>3 把均匀分布 |
| `WeaponFamilies.Of` | `WeaponFamilies.cs:18` | 木质/矿石**优先短路**返回 `Plain` |

**关键数据流**
```
SoulItem.CanUseItem → SoulSwingPlayer.Begin
  → SoulItem.EmitBlade → NewProjectile(SoulBlade) → SoulSource.OnSpawn（继承身份）
  → SoulBlade.AI（椭圆 + trailCenters + SoulTerrain.Sweep）
  → SoulBlade.ModifyHitNPC → SoulEchoDamage.Multiplier + WoodCombat.ModifyPrimaryHit
  → SoulBlade.OnHitNPC → WoodCombat.FinishPrimaryHit（羁绊 A/B/C）
       → HardOrePlayer.Hit → OreFamily.OnMainHit → HolyHeavenProc.OnMainHit → Blessings.Hit
```

**常见问题定位入口**
- "挥了不出剑"：`CanUseItem`（`SoulItem.cs:371` 区块，`!Attached` 或 `Active` 为空）→ `Valid`（`:35`）。
- "出剑把数不对"：`budget`（`SoulSwingPlayer.cs:27`）→ `SoulCurses.Blades`；>3 把的节奏在 `PostItemCheck`。
- "看得见打不到"：`CanDamage`（`:141`）与 `ResolveHitUnit`（`:268`）—— 服务端必须走同一 `trailCenters` 分支（注释在 `SoulBlade.cs:244` 附近）。
- "族加成不生效"：`WeaponFamilies.Of` 的木质/矿石短路，属设计。

**修改影响提醒**
- `SoulBlade` 同时承担运动学/碰撞/伤害/特技/绘制数据源五件事，`trailCenters/trailRotations/trailPhases/trailDirections` 四个平行数组靠 `trailCount` 手工维护。
- `OnHitNPC`（`:294`）是**隐式优先级链**（木质羁绊 → 矿石 → 神圣 → 祝福），没有事件总线；新增族必须插入此处并确认顺序。
- `SoulSource` 字段语义被 `BlessingPlayer.ValidSource`、`WoodBondHit`、`SoulBossCredit`、`FlyingBladeFamily` 四处依赖。

---

## 5. 进度与解锁

**职责**：12 位 Boss 击杀掩码 + 6 枚魂芯掩码 → 10 个普通槽门槛；同时承担战斗参与记录、魂芯补办、Boss 解锁视图、附着叙事线、跨邪恶召唤、四柱→月总召唤修复。

**核心文件**：`Progression.cs`（205）、`SlotProgression.cs`（109）、`SoulBossUnlocks.cs`、`SoulBossPortraits.cs`、`SoulBossPortraitCatalog.cs`、`SoulBossAnimation.cs`、`AttachmentStory.cs`、`CrossSummon.cs`、`LunarSummonRecovery.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `SoulProgress.All = 4095` | `Progression.cs:11` 附近 | 12 位击杀掩码 |
| `ZenithWeapons[10]` | `Progression.cs:15` | 觉醒所需十把材料剑 |
| `SoulProgress.Capacity(kills,cores)` | `Progression.cs:24` | 逐门槛**连续**通过才 +1（断档后续全不计） |
| `BossBit(int)` | `Progression.cs:34` | 12 位映射 |
| `Next(soul)` | `Progression.cs:44` | 玩家可见的"下一步" |
| `SoulPlayer` | `Progression.cs:52` | 灵能冷却、初始物品、`HeroPrefixCompat.Remember` |
| `SoulEncounters` | `Progression.cs:92` | `Hits[seal] = HashSet<(Player,Soul)>`，Boss 消失即清 |
| `SoulBossCredit.ModifyHitByProjectile` | `Progression.cs:124` | 客户端发 packet 8 |
| `SoulBossCredit.OnKill` | `Progression.cs:136` | 写 `Kills|=bit`、`Seals|=seal`、按需掉魂芯 |
| `SoulCore` + 6 子类 | `Progression.cs:162` / `:200` 区块 | 魂芯物品 |
| `SlotGates[9]` | `SlotProgression.cs:12` | **存档兼容契约**（顺序不可插删） |
| `CanAcceptCore` / `AcceptCore` | `SlotProgression.cs:29` / `:31` | |
| `RecoverableCore` | `SlotProgression.cs:38` | 未满 10 槽 + 门槛满足 + 背包有核 |
| `Missing` / `SlotHint` | `SlotProgression.cs:47` / `:59` | 面向玩家的缺口文案 |
| `AttachmentStory.CanStart` | `AttachmentStory.cs:39` | 白天 + 地表 + （信物 或 600px 内史莱姆） |
| `AttachmentStory.Advance` | `AttachmentStory.cs:71` | 4 句对话后 `Attached=true; KingMemory=true`（`:82`） |
| `CrossSummonDrops` | `CrossSummon.cs:56` | 只由本世界原生邪恶 Boss 掉对侧召唤物 |
| `LunarSummonRecovery.TryRepair` | `LunarSummonRecovery.cs:72` | 四柱已尽但无倒计时则补 60 秒 |
| `UpdateRecovery` | `LunarSummonRecovery.cs:81` | 三个早退分支 |
| `RequestRepair` / `Status` | `LunarSummonRecovery.cs:135` / `:143` | `/zmoon repair` |

**关键数据流**：命中 → `ModifyHitByProjectile` →（客户端）packet 8 → `HandlePacket` → `ReceiveCredit` → `Hits[seal].Add` → `OnKill` → 写掩码 → `SyncEquipment`。

**常见问题定位入口**
- "打了 Boss 槽位没解锁"：`Capacity`（`:24`）连续门槛；再查 `OnKill` 的参与者集合是否为空。
- "和朋友一起打只有一人记录"：`OnKill` 只遍历 `inventory[0..57]` 找同 `SoulId` 的剑魂。
- "魂芯点了没反应"：`RecoverableCore`（`:38`）三条件。
- "四柱打完月总不来"：`UpdateRecovery`（`:81`）早退分支，或 `/zmoon repair`。

**修改影响提醒**
- `SlotGates` 顺序 = 存档语义；中间插删会让老档 `Capacity` 直接变化。
- **三套并行 Boss 位映射**：`SoulCurses.BossSeal`（18 位）、`SoulProgress.BossBit`（12 位）、`SoulBossPortraits` 图集 key。`Queen/Empress/Duke/Deer` 只在诅咒位存在 —— **打它们只解诅咒，不给槽位**，这是设计而非 bug。

---

## 6. 羁绊快照

**职责**：把木质/矿石的"数量 → 阶段 → 进度 → 颜色"统一成只读展示模型，供配阵核心环、木质面板、详情页共用。

**核心文件**：`BondSnapshot.cs`（150）

| 入口 | 位置 | 说明 |
|---|---|---|
| `WoodThresholds=[1,2,4,6,8]` | `BondSnapshot.cs:38` | |
| `OreThresholds=[1,2,4,6,8,10]` | `BondSnapshot.cs:40` | |
| `WoodStageNames` | `BondSnapshot.cs:42` | 萌芽/生根/繁枝/森罗/森林共鸣 |
| `Wood(soul)` | `BondSnapshot.cs:59` | 用 `SoulSupport.FamilyItems`（**不受切换诅咒过滤**） |
| `Ore(soul)` | `BondSnapshot.cs:67` | 分母固定 10（14 材质属"全收集奖励"另一套） |
| `Build` | `BondSnapshot.cs:74` | `Math.Clamp(count,0,max)` 会隐藏超额 |
| `Evaluate` / `Leading` | `BondSnapshot.cs:93` / `:96` | 主导族：先 Tier，再 Count，再 Progress |
| `BondColors` | `BondSnapshot.cs:112` | 阶段色 / 棱彩 |
| `NextThreshold` / `PrevThreshold` | `BondSnapshot.cs:23` / `:25` | **全项目零外部引用**（早期设计残留） |

**关键数据流**：`SoulSupport.FamilyItems` → `WoodTypes.Mask` → `PopCount` → `Build` → `BondState` → `SoulUIArmoryBondCore.DrawCoreFlow` / `SoulUIWoodPanel` / `SoulUIArmoryDetails`。

**修改影响提醒**：改阈值必须同步 `SoulTraits` 的写死文案（`SoulTraits.cs:26-43`）与 `SoulSupport.Grants`。`BondSnapshot.Wood` 用的是**第三套口径**（不过滤诅咒），与 `CombatWood`（过滤）可能同屏不同值 —— 属预期。

---

## 7. 族·木质

**职责**：8 把原版木剑按种类数给环境倍率 `S`（1.10/1.20/1.35/1.50/2.00，归冠 3.00）与生命加成；每把绑一条命中补丁；8 种齐开"森林之力"成长；8 木 + 泰拉刃触发圣林归冠双剑位；三组复合羁绊。

**核心文件**：`WoodTypes.cs`（88）、`WoodPlayer.cs`、`ForestPower.cs`、`WoodCombat.cs`、`WoodBondCore.cs`、`WoodBondForge.cs`、`WoodBondRot.cs`、`WoodBondEmber.cs`、`WoodCrown.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `enum WoodKind` | `WoodTypes.cs:10` | 8 值：Forest/Boreal/Palm/Mahogany/Ebon/Shade/Ash/Pearl |
| `Types` | `WoodTypes.cs:29` | 8 个 `ItemID`；满套判定 `Mask==0xFF` |
| `Strength` / `Life` | `WoodTypes.cs:20` / `:23` | 归冠覆盖 3.00 / 100 |
| `Mask(IEnumerable<Item>)` | `WoodTypes.cs:41` | |
| `Evaluate` | `WoodTypes.cs:51` | 不过滤 `TraitEnabled`，仅 `SoulItem.Wood` 使用 |
| `ForestPower` 常量 | `ForestPower.cs:14-18` | `BaseCap=6`、`CrownCap=12`、`300 帧/层`、脱战 `120 帧 -1` |
| `ForestPower.Layers` | `ForestPower.cs:21` | 读 `WoodPlayer.EffectiveLayers` |
| `WoodCombat.PreparePrimaryHit` | `WoodCombat.cs:58` | 施加八条标记 |
| `WoodCombat.FinishPrimaryHit` | `WoodCombat.cs:70` | 命中后派发 + 森林之力计数 |
| `WoodCombat.ModifyPrimaryHit` | `WoodCombat.cs:152` | 霜寂 +1%×S / 净辉 +1.5%×S / 盐蚀 +2×S 穿甲 |
| `WoodBondCore.ActiveForge/Rot/Ember/Crown` | `WoodBondCore.cs:29` / `:39` / `:49` / `:61` | 四组复合羁绊判定 |
| `WoodBondHit` | `WoodBondCore.cs:71` | 命中上下文 record |
| `WoodCrown.Active` | `WoodCrown.cs:20` | 8 木 + 泰拉刃在**非圣林槽**；`evaluating` 重入护盾（`:18`/`:23`） |

**关键数据流**
1. 伤害：`SoulBlade.ModifyHitNPC` → `WoodCombat.PreparePrimaryHit`（标记）→ `ModifyPrimaryHit`（倍率）。
2. 命中后：`WoodBlade.OnHitNPC` → `FinishPrimaryHit` → `WoodPlayer.RecordHit` → `ForestPower.RecordHit`（写 `RecentCombat=300`）。
3. 复合羁绊：`OnHitNPC` 内按 `SoulSource.SourceWeapon` 三路短路派发（铜/铁阔剑→A，魔光/永夜→B，火山→C），派生伤害走 `NPC.StrikeNPC` 直伤，不回灌管道。
4. 木质族**不产生弹幕**，全部作用于既有 `SoulBlade` 与 DoT 通道。

**常见问题定位入口**
- "木质不生效"：`CombatWood`（`SoulCurses.cs:87`）的 `TraitEnabled` 过滤 → 表现为"只有第一把生效"。
- "森林之力不涨"：`WoodPlayer.AdvanceGrowth` 在 `HeldSoul==null` 直接 return；`EffectiveLayers` 还要求 `CurrentSoul==soul.SoulId`。
- "羁绊不触发"：`WoodBondCore` 三条额外条件（棕榈+铜/铁阔剑、红木+乌木+魔光/永夜、红木+灰烬+火山）。
- "归冠不开"：泰拉刃必须在**非 CrownHosts** 的有效槽，圣林槽自举被重入护盾拦掉。

**修改影响提醒**
- 名册从 8 变动要同步 6 处：`WoodTypes.cs:17`（`0xFF`）、`ForestPower.cs:44`（`FullSet`）、`WoodCrown.cs:29`（`0xFF`）、`WoodKind` 枚举、`Names/Effects/Description` 三表、`WoodCombat` 的 8 元数组与 `SendExtraAI/ReceiveExtraAI` 的固定 8 次循环。
- **木质存在三套并行计数口径**（见 §12 的坑表），改一处必须三处同改。

---

## 8. 族·矿石

**职责**：14 种矿石材质（肉前 8 × 短/阔剑 + 肉后 6 × 单剑 = 22 把）按材质种类计数，主剑首击在目标周围集结同材质飞剑环阵，凑齐齐射；6 种肉后材质各有一条连击特性；14 材质集满开 2 个森林奖励位 + 每 300 帧"天空落剑"。

**核心文件**：`OreFamily.cs`（222）、`OreFlyingSword.cs`、`OreFlightMotion.cs`、`OreSkyfall.cs`、`OreStackGrowth.cs`、`OreStackGlow.cs`、`OreSwordEffects.cs`、`OreSwordTraits.cs`、`OreWeaponEnding.cs`、`OrePierceFeedback.cs`、`HardOreTypes.cs`、`HardOrePlayer.cs`、`HardOreShot.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `enum OreMaterial` | `OreFamily.cs:8` | 14 项 |
| `MaterialCount=14` / `AllMaterials` | `OreFamily.cs:16` / `:17` | |
| `MaterialSwords` | `OreFamily.cs:43` | 14 行，共 22 个 ItemID |
| `MaterialOf` | `OreFamily.cs:61` | 线性扫描返回 0..13 |
| `Mask` | `OreFamily.cs:70` | 按**材质位**或（不是按把数） |
| `MainDamageMultiplier` | `OreFamily.cs:83` | 2 材质 +5%、4 材质 +10%，走 `BladeDamage` |
| `TriggerEligible` | `OreFamily.cs:85` | `PrimaryOrdinal>=0` 且 `Count>0` |
| `Count` / `OreItems` | `OreFamily.cs:91` / `:93` | `OreItems = OreFlightMains ∪ TraitSupports` |
| `OnMainHit` | `OreFamily.cs:116` | 生成条件链在 `:118-131` |
| `OreSystem` / `Release` / `PostUpdateEverything` | `OreFamily.cs:134` / `:151` / `:203` | 阵列编排 |
| `HardOreTypes.Types` | `HardOreTypes.cs:11` | 仅 6 把肉后剑，**不能**当矿石族名册 |
| `HardOreTypes.Enabled` | `HardOreTypes.cs:23` | 要求 `soul.Active` 含来源武器 |
| `HardOrePlayer.Clock` | `HardOrePlayer.cs:27` | 手工推帧时钟 |
| `HardOrePlayer.ValidTarget` | `HardOrePlayer.cs:48` | |

**关键数据流**
1. 伤害：`SoulCombat.BladeDamage` = `GetWeaponDamage × Potency × MainDamageMultiplier`。
2. 命中：`OnHitNPC` → `HardOrePlayer.Hit`（连击特性）→ `OreFamily.OnMainHit`。
3. 成阵：`OreSystem.AddSword` → 材质序列变化即释放旧阵 → `NewProjectile(OreFlyingSword)` → 达 `materials.Length` 才 `Release`。

**常见问题定位入口**
- "矿石飞剑不出现"：`TriggerEligible`（`:85`）→ 切换诅咒 `OreFlightMains` 只取前 4 把（`SoulCurses.cs:81`）→ `blade.TryConsumeOreHit`。
- "永远凑不齐"：`AddSword` 发现 `Materials` 序列变化会先释放旧阵（`OreFamily.cs:164` 附近）。
- "肉后特性不触发"：只放支援槽的矿剑走 `TraitSupports` 分支，不走 `Active` 分支。

**修改影响提醒**
- 改 `MaterialCount`/枚举顺序要同步 6 处：`DefaultType/Names/Tints` 三表、`AllMaterials` 位宽、`OreCollectionMask` 存档语义、`OreFlyingSword` 的 `Clamp(...,0,13)`、`HardOreShot` 的 `Tints[8+i]` 偏移。
- `DamageFactor` 是唯一伤害公式入口，改它同时影响成员伤害、`DamageShare` 防御折算、`OreStackGrowth` 上限。

---

## 9. 族·飞刃

**职责**：31 把"纯挥砍无弹幕"剑；装族内剑后按 0.3 秒/把在手蓄能，挥臂时一次性掷出全部（5° 扇面），并**取代**本次本体飞剑。三阶段：直飞 → 命中/满 600px 自旋 → 收回。

**核心文件**：`FlyingBladeFamily.cs`（71）、`FlyingBladeHand.cs`、`FlyingBladeShot.cs`、`FlyingBladeVisuals.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `Weapons` | `FlyingBladeFamily.cs:19` | 31 把（7 光剑 + 7 晶光刃 + 3 + 14） |
| `Stages=[1,2,4,6,8,10]` | `FlyingBladeFamily.cs:33` | 8/10 阶**已定义无效果** |
| `StepPercent=3` / `StepCap=10` | `FlyingBladeFamily.cs:37` / `:38` | +3%/把，最高 +27% |
| `Activated/Tally/Pass/Sigil` | `FlyingBladeFamily.cs:57-60` | 1/2/4/6 把四档能力 |
| `DamageBonus` | `FlyingBladeFamily.cs:63` | |
| `Enabled` | `FlyingBladeFamily.cs:70` | `Attached && TraitMains.Any() && Activated` |
| `FlyingBladeHand.Launch` | `FlyingBladeHand.cs:32` | 本地生成、扇面、写 `ai[0]=host.type` |
| `FlyingBladeShot.AI` | `FlyingBladeShot.cs:50` | 三阶段 |

**关键数据流**：`SoulSwingPlayer.Begin` → `FlyingBladeHand.Launch` → 取宿主列表（`Pass ? Active : Active.Where(IsMember)`）→ 逐把 `BladeDamage × DamageBonus` → `NewProjectile(FlyingBladeShot)` → 清空 `Loaded`；成功后 `budget=0`（本体飞剑完全不发）。

**常见问题定位入口**
- "飞刃不出"：`Enabled` 三条件；`Loaded<=0`（刚换魂 0.3 秒窗口）；`PostUpdate` 在手持非剑魂时立刻清零。
- "收回期打不动"：`Sigil` 未达 6 把，`Projectile.damage` 被置 0。

**修改影响提醒**：**飞刃弹幕不携带 `SoulId`**（`FlyingBladeHand.cs:31` 注释），因此不吃祝福、环境、矿石命中、木质/矿石管线 —— 只吃 `BladeDamage × DamageBonus`。改 `Weapons` 会同时影响 `WeaponFamilyTags.Of`、阶位 tooltip、`EnvironmentTooltips` 的族阶位行。

---

## 10. 族·神圣天堂

**职责**：8 把星光/神圣系剑中任意一把进入有效寄宿，主剑每次合法命中（300 帧/剑魂）从目标上方 600px 落一颗 `StarDamage=1000` 的流星，命中或落地生成 120px 冲击波。

**核心文件**：`HolyHeavenFamily.cs`（14）、`HolyHeavenProc.cs`、`HolyHeavenStar.cs`、`HolyHeavenStarVisuals.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `Weapons` | `HolyHeavenFamily.cs:9` | 8 把；珍珠木剑**同时属木质族** |
| `Enabled` | `HolyHeavenFamily.cs:13` | 至少一把在 `TraitItems` |
| `StarDamage=1000` | `HolyHeavenProc.cs:11` | **常量，不吃任何倍率** |
| `CooldownFrames=300` | `HolyHeavenProc.cs:12` | 键 = `player:SoulId`，换魂重置 |
| `OnMainHit` | `HolyHeavenProc.cs:14` | 条件链在 `:22-24` |

**修改影响提醒**：`HolyHeavenImpact` 的 `timeLeft=2` 是"只炸一次"的实现方式，改它会导致重复结算。

---

## 11. 族·剑灵

**职责**：族内至少 1 把（附魔剑/飞刀/波涌之刃/无头骑士剑）+ 有有效出战主剑时，为**每个出战主攻槽**生成隐形召唤体，自动索敌 900px 内最近目标，用该槽武器实际"挥砍"（伪造一个 Player 跑 `ItemCheck()`）。

**核心文件**：`LivingArmamentFamily.cs`（13）、`LivingArmamentPlayer.cs`、`LivingArmament.cs`、`LivingWeaponProxy.cs`、`LivingChannelProjectile.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `Members` | `LivingArmamentFamily.cs:9` | 4 把，两处标注"暂定候选" |
| `LivingArmamentPlayer` | `LivingArmamentPlayer.cs:9` | 按 `VisibleSlotIds` 补生成 |
| `LivingArmament` | `LivingArmament.cs:11` | 召唤体本身 `CanDamage()=>false` |
| `LivingWeaponProxy.Tick` | `LivingWeaponProxy.cs:28` 附近 | 临时替换 `Main.mouseX/Y` + `player.ItemCheck()` |
| `LivingChannelProjectile` | `LivingChannelProjectile.cs:8` 附近 | 给 aiStyle 9/15/99 的弹幕换代理玩家 |

**修改影响提醒**：`LivingWeaponProxy` 是**全局 `Main.player[owner]` 临时替换 + `[ThreadStatic]`**，该窗口内任何读取 `Main.player[owner]` 的代码都会看到假玩家；改 `Tick` 必须保留 `finally` 还原。

---

## 12. 族归属标签与特性图鉴

**职责**：`WeaponFamilyTags` 是"这把武器属于哪些族"的**唯一展示出口**；`SoulTraits` 生成"当前生效/未生效的全部特性条目"（图鉴数据源）。

**核心文件**：`WeaponFamilyTags.cs`（47）、`SoulTraits.cs`（129）

| 入口 | 位置 | 说明 |
|---|---|---|
| `WeaponFamilyTags.Of(type)` | `WeaponFamilyTags.cs:10` | 固定顺序：飞刃→神圣天堂→剑灵→木质→矿石 |
| `IsAny` / `Line` / `ActivationHint` | `WeaponFamilyTags.cs:25` / `:29` / `:37` | 只有飞刃族输出阶位提示 |
| `TraitCategory` | `SoulTraits.cs:7` | 枚举顺序 = UI 筛选按钮顺序，**只能追加** |
| `SoulTrait` | `SoulTraits.cs:8` | record struct |
| `Active(soul)` | `SoulTraits.cs:15` | `All(soul).Where(t => t.Active)` |
| `All(soul)` | `SoulTraits.cs:16` | 一次性构建全部条目（族/阶位/特技/诅咒/复生） |

**常见问题定位入口**
- "族标签缺一行"：`Of` 的五个 `if` 是唯一来源，新增族必须在此追加。
- "图鉴说未激活但战斗有加成"：多数条目的 `Active` 是**手写复算**，与战斗侧是两份代码（例：`ore-tier-2` 用 `oreCount`，战斗侧用 `MainDamageMultiplier`）。

### 12.1 族计数口径对照（维护必读）

| 族/口径 | 计数表达式 | 位置 | 含奖励位/传奇位 |
|---|---|---|---|
| 木质（族内/战斗） | `PopCount(WoodTypes.Mask(FamilyItems))`，战斗再叠 `TraitEnabled` | `WoodTypes.cs:56`；`SoulCurses.cs:87` | 是 |
| 木质（支援格授予） | `GrantedBy("wood")` 用 `SetItems` | `SoulSupport.cs:45` | **否** |
| 矿石（族内） | `PopCount(OreFamily.Mask(OreItems))`，按**材质**去重 | `OreFamily.cs:91` | 是 |
| 矿石（支援格授予） | `SetItems ∪ 森林奖励 ∪ 圣林` | `SoulSupport.cs:31` | 部分（否传奇） |
| 矿石（圆满） | `OreCollectionMask == AllMaterials`，**只增不减** | `SoulItem.cs:40` | 是 |
| 飞刃 | `Active ∪ Supports` 过 `IsMember` 计**把数** | `FlyingBladeFamily.cs:47` | 是 |
| 神圣/剑灵 | `TraitItems` 按 `type` **去重** | `HolyHeavenFamily.cs:12` | 是 |
| 环境（收集位） | `EnvironmentCollection` 位图 | `EnvironmentRules.cs:99` | 是 |
| 环境（常驻名额） | `Count(collection) / 4` | `EnvironmentRules.cs:15` | 间接 |

**唯一有环的判定**：`SoulItem.CrownCapacity` → `WoodCrown.Active` → `SoulSupport.FamilyItems` → `SoulItem.Active` → `CrownCapacity`，由 `WoodCrown.evaluating`（`WoodCrown.cs:18`/`:23`）打破，并顺带实现"圣林槽不得自举归冠"。

---

## 13. 环境属性

**职责**：21 个生物群系/地层环境构成与 `BladeFamily`/`WoodKind` **完全正交**的第二维度。武器静态绑定 1~N 个环境；剑魂把已寄宿武器的环境登记成位图；每收集一种环境按 `Shared(1+0.2n) × Forest(1.15) × 领域(1.5) × Adaptation(0.5/1)` 提供属性；每 4 种得 1 个"常驻机制"名额。

**核心文件**：`EnvironmentAttributes.cs`（147）、`EnvironmentRules.cs`（120）、`EnvironmentPlayer.cs`（252）、`EnvironmentEffects.cs`、`EnvironmentTooltips.cs`、`EnvironmentUI.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `enum SwordEnvironment` | `EnvironmentAttributes.cs:10` | `None=0` + 1..21 |
| `EnvironmentRules.All=(1<<21)-1` | `EnvironmentRules.cs:10` | |
| `BlessingSlots` / `Shared` / `Forest` | `EnvironmentRules.cs:15` / `:16` / `:17` | 4/8/12/16/20 → 1..5 名额 |
| `MechanismNames[22]` | `EnvironmentRules.cs:18` | 首位空串 |
| `Values(E)` / `StatsText` / `Color` | `EnvironmentRules.cs:30` / `:39` / `:55` | |
| `Natural(Player)` | `EnvironmentRules.cs:61` | 地形掩码，群系优先于深度 |
| `Adaptation(E, terrain)` | `EnvironmentRules.cs:80` | 互斥群系 ×0.5 |
| `EnvironmentCollection` 等字段 | `EnvironmentRules.cs:97` | `SoulItem` 分部存档字段 |
| `RegisterEnvironments` | `EnvironmentRules.cs:99` | 唯一合法化入口之一 |
| `SetEnvironmentDomain` | `EnvironmentRules.cs:105` | 拒绝未收集环境 |
| `ToggleEnvironmentBlessing` | `EnvironmentRules.cs:108` | 名额不足返回 false |
| `WeaponEnvironmentAttributes.RegisterBuiltins` | `EnvironmentAttributes.cs:87` | **武器→环境唯一手工绑定表** |
| `EnvironmentPlayer.Mechanism` | `EnvironmentPlayer.cs:35` | 三级判定：祝福 → 领域 960 → 自然 |
| `EnvironmentPlayer.Hit` | `EnvironmentPlayer.cs:155` | 21 环境机制总表 |

**常见问题定位入口**
- "环境机制不跑"：`Mechanism`（`:35`）—— **仅"已收集"只给属性，不给机制**。
- "tooltip 显示未归类"：`SetForType` 未绑定 → `tags.Count==0`。
- "点了不生效"：`ToggleEnvironmentBlessing` 名额不足，UI 走 `feedback` 不弹窗。

**修改影响提醒**：新增环境必须同时改 `SwordEnvironment` 枚举（追加在 21 之后并调 `All` 位宽）、`EnvironmentAttributes.All`、`MechanismNames`、`stats`、`Descriptions`、`Color`、`Natural`、`Adaptation`、`EnvironmentIcons`（必须 1:1 对齐 22 项）、`EnvironmentUI` 里 21 的硬编码。**这是全项目耦合点最多的手工同步表。**

---

## 14. UI 层

**职责**：`SoulUI` 一个 `ModSystem` 被拆成 18 个 partial 文件，提供全屏"魂域"界面：配阵（剑库/剑阵/详情/羁绊核心/木质面板）、特性图鉴、成长记忆（剑位路线/魂芯/封印铭刻）、环境之力、旧物取回。

**核心文件**：`SoulUI.cs`（主控）+ `SoulUIPages` / `SoulUIStyle` / `SoulUIEffects` / `SoulUIGlyphs` / `SoulUIInformation` / `SoulInfoLayout` / `SoulDomainUI` / `SoulUIArmory*` / `SoulArmoryLayout` / `SoulArmoryPreview` / `SoulUIArmoryBondCore` / `SoulUILibrary` / `SoulUIArchive` / `SoulUIMemories` / `SoulUICurseUnlocks` / `SoulUIBossUnlocks` / `EnvironmentUI` / `SoulUIWoodPanel`

| 入口 | 位置 | 说明 |
|---|---|---|
| `SoulUI : ModSystem` | `SoulUI.cs:14` | |
| `Open` / `SelectPage` / `Toggle` / `Sync` | `SoulUI.cs:33` / `:38` / `:46` / `:56` | `Sync` 只对 `MultiplayerClient` 生效 |
| `PostUpdateInput` | `SoulUI.cs:75` | P 键、自动关闭（`!playerInventory`/死亡/背包无此魂） |
| `ModifyInterfaceLayers` | `SoulUI.cs:102` | 插 Input Shield + Soul 层；域模式删 `Vanilla:*` |
| `SlotBounds` | `SoulUI.cs:211` | 命中框；域模式 page0 委托 `ArmoryLayout.Slot` |
| `Draw()` | `SoulUI.cs:341` | 唯一总绘制入口，页面分发行 `:382` |
| `DrawHosts` | `SoulUI.cs:404` | page0 → 域模式转 `DrawArmory` |
| `SoulUIPages` 页号 | `SoulUIPages.cs:8` | 0 武器寄宿 / 1 特性 / 2 成长 / 4 旧物 / 5 环境；导航只含 `{0,1,2,5}` |
| `SoulDomainUI.DomainMode` | `SoulDomainUI.cs:13` | **默认 `true`，游戏内无写入点**；只有 checks 会置 false |
| `SoulDomainUI.SelectSlot` | `SoulDomainUI.cs:32` | 选槽会 `ClearArmoryCandidate` |
| `EquipFromInventory` / `ReturnSelected` | `SoulDomainUI.cs:44` / `:55` | 取放物品的两个唯一出口 |
| `SoulArmoryLayout.Create` / `Slot` | `SoulArmoryLayout.cs:9` / `:31` | 纯几何；`Compact = w<1180 \|\| h<620` |
| `SoulArmoryPreview.Evaluate` | `SoulArmoryPreview.cs:8` | 克隆整件剑魂跑真实 `TrySwapHost`，**不写回** |
| `DrawArmory` 三块 | `SoulUIArmory.cs:9` | |
| `DrawArmoryScene` | `SoulUIArmoryScene.cs:24` | |
| `DrawArmoryDetails` | `SoulUIArmoryDetails.cs:15` | |
| `DrawBondCore` | `SoulUIArmoryBondCore.cs:9` | |
| `DrawArmoryLibrary` | `SoulUILibrary.cs:27` | 点击只登记候选，不动物品 |
| `DrawTraits` | `SoulUIArchive.cs:39` | **文件名与内容不符**：实为特性页 |
| `DrawProgress` | `SoulUIMemories.cs:10` | **同上**：实为成长页 |
| `DrawSealUnlockList` | `SoulUICurseUnlocks.cs:9` | 卡内行内展开 `DrawBossUnlocks` |
| `DrawBossUnlocks` | `SoulUIBossUnlocks.cs:11` | 两列，宽 <540 改单列 |
| `DrawEnvironments` | `EnvironmentUI.cs:17` | |
| `DrawWoodPanel` | `SoulUIWoodPanel.cs:33` | 两套木剑口径：`hostedMask`（`:41`）与 `CombatWood`（`:42`） |
| `SoulInput.Capture/MaskBackground/Begin/End/Restore` | `SoulInput.cs:20` / `:41` / `:48` / `:54` / `:61` | 点击隔离时序 |

**关键数据流**：`Toggle` → `ModifyInterfaceLayers` 插层 → `PostUpdateInput` 里 `SoulInput.Capture` 屏蔽世界输入 → `Draw()` → `SoulInput.BeginSoulLayer` → 页面读 `soul.*` 只读数据 → 点击 → `TrySwapHost` → `SyncInventory` + `Sync` → `EndSoulLayer` → `RestoreAfterInterface`。

**常见问题定位入口**
- "点按钮时人物也在挥剑"：`MaskBackground`（`SoulInput.cs:41`）必须同时清 `Main.mouseLeft`、`PlayerInput.Triggers.Current.MouseLeft`、`Player.controlUseItem` 三处。
- "剑位画歪"：`SoulArmoryLayout.Slot`（`:31`）与 `SlotBounds`（`SoulUI.cs:211`）、`SoulItem.SupportOrdinal`（`:28`）三处必须同口径。
- "详情页数字与背包不一致"：`SoulArmoryPreview` 走克隆，不写回真身。

**修改影响提醒**：`SoulArmoryLayout` 是 UI 与点击判定的**唯一共享几何源**；`SoulInput` 时序一旦改动会波及整个游戏输入（不止模组 UI）。`DomainMode` 恒 true 使整套"浮窗 + 剑环"代码成为**仅自检可达的休眠路径**（`DrawChrome`、`DrawSoulRing`、`HandleWindowDrag`、`PanelBounds`/`TabBounds` 非域分支，约 300 行）。

---

## 15. 渲染与特效

**职责**：所有视觉分支的开关（`VfxIsolation`）、状态包装（`VfxRenderState`）、逐项热键调试（`VfxPartDebug`）；飞剑带面、屏幕折射、表面材质、星芒/粒子/飞刃特效。

**核心文件**：`VfxIsolation.cs`（244）、`VfxPartDebug.cs`、`VfxRenderState.cs`、`SoulMaterialTrail.cs`、`SoulTrailUnits.cs`、`SoulTrailMotion.cs`、`SoulTrailEnding.cs`、`SoulVisuals.cs`、`SoulRibbonMesh.cs`、`SwordFlightTrail.cs`、`SwordBodyColorNoise.cs`、`VfxSwordMask.cs`、`VfxMaskData.cs`、`SoulSceneRefraction.cs`、`GlassMaterialPreset.cs`、`SwordSpaceDistortion.cs`、`SwordFlowMaterial.cs`、`SurfaceMaterialDefinition.cs`、`SurfaceMaterialRenderer.cs`、`MaterialInheritance.cs`、`MaterialQuality.cs`、`SwordFlowMeshBasis.cs`、`SwordFlowPalette.cs`、`SoulZenithEffects.cs`、`SwordSpark.cs`、`SoulFlowParticles.cs`、`EvapCircle.cs`、`ViscousSlash.cs`、`FlyingBladeVisuals.cs`、`SwordEnchantmentVisuals.cs`、`BlessingCamera.cs`、`HolyHeavenStarVisuals.cs`

| 入口 | 位置 | 说明 |
|---|---|---|
| `VfxIsolation.PureCodeOnly => false` | `VfxIsolation.cs:12` | **是表达式不是字段**，不可运行时切换；置 true 会一次关掉 12 个外部输入分支 + 全部贴图加载 + 滤镜 |
| `SwordTrail` 等 `Branches` | `VfxIsolation.cs:63` 区块 | 10 项（B1–B10） |
| `Parts[26]` / `PartValues` | `VfxIsolation.cs:107` / `:138` | |
| `PartValue(string)` | `VfxIsolation.cs:141` | **全代码库零调用** |
| `GlassParts[7]` | `VfxIsolation.cs:226` 区块 | |
| `ViscousSlash` 开关 | `VfxIsolation.cs:36` | 默认 false，**不在 61 项清单内** |
| `VfxPartDebug.PostUpdateEverything` | `VfxPartDebug.cs:23` | F7/F8 移动、F9 切换、F10 全关 |
| `VfxRenderState.DrawWithRestoredState` | `VfxRenderState.cs:9` | 保存/还原 Blend/Depth/Raster/Scissor + **0~2 号**纹理槽，进入置 Additive |
| `SoulVisuals.PostDrawTiles` | `SoulVisuals.cs:54` | 世界层主入口；`CompositingReady` 时提前 return |
| `SoulMaterialTrail.Draw` | `SoulMaterialTrail.cs:22` | → `SwordFlowMaterial.Draw` |
| `SoulBlade.DrawRibbonShader` / `PreDraw` | `SoulBlade.cs:458` / `:485` | |
| `SwordFlowMaterial.Draw` / `Load` | `SwordFlowMaterial.cs:223` / `:52` | 有材质走 `SurfaceMaterialRenderer`，否则走常量路径 |
| `SoulSceneRefraction.PrepareNormals` | `SoulSceneRefraction.cs:64` | 在 `On_FilterManager.BeginCapture` 内烤 normal/surface RT |
| `GlassScreenData` | `SoulSceneRefraction.cs:127` | 滤镜合成 |
| `SwordFlowPalette` | `SwordFlowPalette.cs` | 剑色 → 暗/中/亮三阶 |
| `SoulZenithEffects.DrawTip` / `EmitFlying` | `SoulZenithEffects.cs:44` / `:25` | 星芒 / 飞行 dust + 两处光照 |
| `FlyingBladeVisuals.Draw` | `FlyingBladeVisuals.cs:150` | `PreDraw` 内部 End/Begin 切加法混合（已知技术债） |
| `BlessingCamera` | `BlessingCamera.cs:33` 区块 | ILHook `Main.InitTargets` 扩离屏留白 |

**渲染管线（一帧顺序）**：`PostUpdateEverything`（滤镜启停/队列推进）→ `OnPreDraw`（`BlessingCamera`、`VfxSwordMask.PrepareFrame`）→ 世界绘制 → `SoulVisuals.PostDrawTiles`（主拖尾 + 消散片）→ 实体 `PreDraw`（残影/剑体/星芒）→ 弹幕 `PreDraw`（飞刃）→ Dust → `On_Main.DrawDust`（`ViscousSlash`）→ `On_FilterManager.BeginCapture`（`PrepareNormals`）→ 滤镜合成。

**⚠ 已知的坑（本次调查最有价值的产出）**

| # | 事实 | 证据 |
|---|---|---|
| 1 | **P01~P26 与 GlassParts 共 33 项是空开关**：按 F9 只改面板文字，画面零变化 | `VfxIsolation.PartValue`（`:141`）零调用；`ZRSwordFlow.fx` 中 `uPart` 出现 0 次；`ZRGlassScreen.fx` 中 `uGlassPart` 出现 0 次；唯一消费方是 `VfxPartDebug.cs:80/85/106/111` 的读写 |
| 2 | `Parts` 数组顺序与 shader 声明顺序一致的注释约束**已失效**（shader 已重写） | `VfxIsolation.cs:101` 注释 vs 实际 uniform 表 |
| 3 | `ViscousSlash` 绕过总闸，**不在 61 项清单里**，只能改字段关 | `VfxIsolation.cs:33-36` 注释记录历史 |
| 4 | `SurfaceMaterial` 管线整体休眠：`bindings.json` 的 `weapons` 为 `{}` | `SwordFlowMaterial.Load`（`:52`）读空 → `surfaceMaterials` 为空 → 永远走常量路径 |
| 5 | 4 个 `.fxh` 是**双重孤儿**：无 `#include`，又被 `buildIgnore` 排除 | `build.txt` 与全库 `grep include` 命中 0 |
| 6 | `SoulSceneRefraction.Capture()` 全库无调用 → `Scene` 恒 `null`，`uSceneUvTransform` 恒 `(0,0,1,1)` | 窗口缩放/视口偏移时折射会有偏差 |
| 7 | `SwordSpaceDistortion` 参数入口无调用者，`ZRSwordSpaceWarp.fxh` 未被 include → 空间扭曲完全无效果 | `SwordSpaceDistortion.cs` 全文 |
| 8 | `VfxRenderState` 只保存 **0~2 号**纹理槽；`SurfaceMaterialRenderer` 用 16 槽 | 混用时有槽位遗漏风险（潜在，非现状缺陷） |

**修改影响提醒**：`PureCodeOnly` 一改影响全模组所有非纯几何特效（贴图加载、蒙版图集登记、粒子降级到 MagicPixel、滤镜激活）。`GlassFilter` 关闭同时省掉每帧 2~4 次全屏 RT 清屏 + 整条带面重绘，是最大的性能开关。

---

## 16. 资产与材质管线

**职责**：把制作端素材（`VfxLibrary`）在**构建期**转成主模组内的贴图与常量类；运行时不读 `VfxLibrary`。

| 环节 | 位置 | 说明 |
|---|---|---|
| 矿石剑身蒙版 → 贴图 | `checks/emit_vfx_mask_data.py:11` | `assert` 恰好 21 把 H4；`:20` 校验 `atlasSha256`；`:22` `shutil.copyfile` 到 `ZenithReborn/Assets/Vfx/SwordBody/` |
| 同上 → 常量表 | `ZenithReborn/VfxMaskData.cs` | 39 行，21 条 `Row`（生成文件，**禁手工改**，见 `:6-8` 注释） |
| 玻璃预设 → 常量类 | `checks/emit_glass_preset.py` | 读 `VfxLibrary/Materials/SwordTrail/Glass/preset.json`，`assert scope=='developer-authoring-only'` |
| 着色器编译 | `checks/compile_effects.py` | `run_wood_checks.ps1:17` 调用；失败即中断回归 |
| 运行时贴图请求 | `VfxSwordMask.cs:32` 区块 | `ModContent.Request<Texture2D>("ZenithReborn/Assets/Vfx/SwordBody/…", ImmediateLoad)` |
| 材质绑定（当前空） | `ZenithReborn/Assets/Vfx/Materials/SwordFlow/bindings.json` | `weapons: {}` |
| 逆向写回（唯一） | `checks/import_material_export.py` | 材质实验室导出 → `--apply` 才写，写前备份到 `artifacts/material-import-backups` |

**运行时代码里的贴图引用**只有两类：`Terraria/Images/*`（24 处，原版资源）与 `ZenithReborn/Assets/Vfx/**`。`SwordSpark.cs:10`、`SoulFlowParticles.cs:77` 还有 `PureCodeOnly → MagicPixel` 降级分支。

---

## 17. 本地化

**职责**：`Items/Buffs/NPCs/Projectiles/Keybinds` 五类的显示名与说明。

| 分类 | 覆盖 | 缺口 |
|---|---|---|
| Items | 10 条（`SoulItem`、`GuardianGel`、4 个召唤物、6 个魂芯） | `SlashZenithTest` 中文侧两键被注释 → 走类型名 |
| Buffs | 10 条全覆 | — |
| NPCs | `GuardianSlimeKing.DisplayName` | — |
| Projectiles | 英文 11 / 中文 5 | **中文缺 `FlyingBladeShot`、`HolyHeavenStar`、`HolyHeavenImpact`、`LivingArmament`、`EnvironmentEffect`、`EnvironmentDomain`** —— 这 6 个都有绘制代码 |
| Keybinds | `OpenSoul`、`ActivateYujian` | — |

**重要事实**：
- **界面文字全部硬编码在 `.cs`**（如 `SoulUIPages.Name` 的"武器寄宿/剑魂特性/成长记忆/环境之力"）。**改界面文案改 `.cs`，改物品名/提示改 hjson。**
- `VfxPartDebug` 的 61 项标签与 `VfxIsolation.Label` 全是硬编码中文，**不参与本地化**，英文客户端也会看到中文调试文本。
- 全局只有 1 条本地化断言（`Check.cs` 校验 `DawnCore.DisplayName == "初光魂芯"`）。

---

## 18. 验证体系

**职责**：`checks/ZenithPrototypeCheck` 是**独立 tML 附属模组**，唯一目的是给主模组做可自动化验收。

| 项 | 内容 |
|---|---|
| 文件数 | **94** 个 `.cs`（不是 100） |
| 引用方式 | `HintPath = ../../ZenithReborn/bin/Release/net8.0/ZenithReborn.dll` —— **引的是上次构建产物**，不是源码 |
| 硬编码路径 | `<Import Project="E:/SteamLibrary/.../tMLMod.targets" />` → **不可移植** |
| 调度入口 | `Check.cs:11` `class Check : ModSystem`；`PostAddRecipes`（服务端）/ `PostUpdateEverything`（客户端） |
| 路由机制 | **环境变量**：`Check.cs:34` 读 `ZENITH_CHECK_REPORT`，为空则整体退出；`:38-170` 14 个 `ZENITH_*` 分支各自硬编码套件清单并 `Environment.Exit(0)` |
| 总回归入口 | `ZENITH_WOOD`（`Check.cs:125` 区块）串联 29 个套件 |
| 断言原语 | `Check.cs:15` `Assert(bool,string)` —— 失败即抛异常，**快速失败**；`PASS: N` 是成功数不是总数 |
| 第二调度体系 | 49 个文件不经过 `Check.cs`，各自在 `ModSystem` 钩子里读 `ZENITH_*_RENDER/GPU/GUI`，由 `checks/run_*.ps1` 启动 |
| 脚本 | `checks/*.py` 54 个（生成/校验/采集）、`checks/*.ps1` 37 个（构建/启动） |
| 打包安装 | `checks/package_install.py:70` 排除 `docs/checks/VfxLibrary/tools`；`:73` `tools` 下额外排除 dll/exe/pdb/zip/tmod |

**⚠ 夹具体系的 6 个陷阱（写新夹具或排查假阴性必读）**

| # | 陷阱 | 证据 |
|---|---|---|
| 1 | **`CurseFixture.NeutralDefaults` 是全局毒开关**：默认 false = 新魂带全六诅咒，此时 `TraitItems` 只返回第一把主攻 → 所有"多剑生效"类断言集体假阴性 | `Check.cs:115`/`:127` 置 true，`:150` 置回 false；顺序不可颠倒 |
| 2 | **HintPath 引 Release DLL**：改主模组不重新 `-build`，夹具会**静默测旧逻辑** | `.csproj:4`；靠 `run_wood_checks.ps1` 的脚本纪律（先重建）绕开 |
| 3 | **服务端 26 个渲染夹具直接 return**：`-server` 的 PASS **完全不能证明画面正确** | 所有 `*RenderCheck` 的 `Enabled` 都带 `!Main.dedServ` |
| 4 | `Main.UIScale` 跨断言污染：循环中途抛异常会停在 2f，后续 UI 断言坐标全错且报错指向别处 | `Check.cs` UI 分支 |
| 5 | `Cores` 与 `Kills` 是两个独立门：只写 `Kills` → 槽位永远是 1 个 | `Check.cs` 明确断言"Boss 旗标单独不能替代魂核" |
| 6 | `RefreshStats()` 不调 = 一切派生值陈旧；`Prefix(-2)` 是"随机重铸"不是"清除" | 夹具模板固定三步 |

**完全无覆盖（8 项）**：真实多人（≥2 客户端）、性能/帧时间（除液态玻璃）、实机画面人工判读、音效、手柄/触屏、本地化完整性、多人权限/RPC 鉴权、跨平台。

---

## 19. 工程工具与素材库

| 子项 | 规模 | 定位 |
|---|---|---|
| `tools/ZenithMaterialLab/` | 44（15 入库） | **独立 tModLoader 模组**（`modReferences = ZenithReborn@0.3.80`），材质调参工作台；单向依赖主模组，复用 `SurfaceMaterialDefinition`/`SurfaceMaterialRenderer`/`ZRMaterialSurface`；导出到 `MaterialLab/Exports/<ts>/` 再由 `import_material_export.py --apply` 落回 |
| `tools/ilspy/` | 68（忽略） | ILSpy 9.1.0.7988，可重新下载 |
| `tools/decomp/` | 8（忽略） | 反编译产物；其中两个 **0 字节空文件**（`Terraria.Graphics.Light.LightingMode`、`Terraria.LightMode`）原因未确认 |
| `tools/xnbcli/` + `-binary/` | 60（入库）+ 36（忽略） | xnbcli 1.0.7，解包原版 XNB |
| `tools/build_vanilla_vfx_catalog.py` | 1 | 重建 `VfxLibrary/Vanilla/` 图鉴；读 `tools/decomp/Terraria.ID.ProjectileID.decompiled.cs` |
| `VfxLibrary/` | 11756 | `Vanilla/` 11185（95.1%，纯参考）、`Masks/` 413、`ColorFields/` 74、`Materials/` 58；索引单位 `entry.json` 156 个 |
| `VfxLibrary/Vanilla/catalog.js` | 617 KB | 3785 条记录（3461 原图 + 324 Dust 切片），98.8% 有数字 ID |
| 已知脏数据 | — | `Thumbnails/其他/` 有 **150 张孤儿缩略图**（原图已删、缩略图残留） |

`git ls-files tools` = 80 / 222：能重新生成或重新下载的一律不入库。

---

## 20. 导航

| 文档 | 内容 |
|---|---|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | 模块职责与边界、核心调用方向（Mermaid）、三条端到端数据流、共享状态与单一管理入口、跨模块依赖矩阵、典型修改的影响半径 |
| [HOTSPOTS.md](./HOTSPOTS.md) | 高频/重要开发入口速查：`功能 → 路径 → 符号 → 场景 → 依赖`，覆盖 15 类改动 |
| [PROBLEM_INDEX.md](./PROBLEM_INDEX.md) | 问题定位反向索引：`现象 → 可能模块 → 优先检查文件(行号) → 关联检查位置`，含 14 类现象与排查顺序 |
| `artifacts/codebase-map/A~I-*.md` | 本次并行调查原始产出（9 份，含逐文件索引表与逐条断言清单） |

**关于 `E-checks-scripts.md`**：本次并行调查的 `.md` 产出实际只有 **8 份**（A/B/C/D/F/G/H/I），`checks/*.py`、`checks/*.ps1` 的系统性建图稿缺失。本文第 18 节中涉及脚本的行号（`package_install.py:70`、`run_wood_checks.ps1:17`、`emit_vfx_mask_data.py:11`）为撰写时**直接读源码补充核实**，非引自该缺失报告。
