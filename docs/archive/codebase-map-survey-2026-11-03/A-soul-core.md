# A 组：剑魂核心（soul-core）模块地图

> 范围：`ZenithReborn/` 根目录 32 个剑魂核心文件（不含 `SoulUI*`、`SoulTrail*`、`SoulRibbon*`、`Wood*`、`Ore*`、`Environment*`、`FlyingBlade*`、`HolyHeaven*`、`LivingArmament*`）。
> 全部结论来自实际读取源码 + 跨文件 grep 取证；表中 `文件:行号` 均可直接跳转。
> 工作区当前处于「木质族 V2」未提交改动状态，本文件按**当前磁盘状态**描述，不把未提交改动视为异常。

---

## 第一部分：模块级说明

### 模块 1 · 剑魂数据模型与槽位引擎

**职责一句话**：定义剑魂这件物品本身（寄宿槽数组、槽位编号映射、容量/效力/射程、换入换出校验、存读档与网络同步），以及"支援格"这一通用奖励池的解锁算法。

**核心文件**：`SoulItem.cs`、`SoulSupport.cs`

**重要类 / 方法 / 入口**
- `SoulItem`（`partial class`，`SoulItem.cs:11`）—— 数据模型本体，另有 4 个分部：`SoulCombat.cs:5`（伤害）、`SoulCurses.cs:76`（诅咒/特性过滤）、`SoulLegacyMartialData.cs:7`（旧档字段）、`EnvironmentAttributes.cs:133` / `EnvironmentRules.cs:95`（环境，非本组范围）。
- 槽位数组：`Hosts[12]`（`SoulItem.cs:13`）、`SupportHosts[11]` / `ForestHosts[2]` / `CrownHosts[2]`（`SoulItem.cs:14`）、`AdditionalSupports`（`SoulItem.cs:15`）、`LegendaryHost`（`SoulItem.cs:16`）。
- 槽位编号映射：`SupportSlotId(int)`（`SoulItem.cs:26`）、`SupportOrdinal(int)`（`SoulItem.cs:28`）、`IsCrownSlot(int)`（`SoulItem.cs:25`）、`LegendarySlotId = 34`（`SoulItem.cs:17`）、`CrownSlotBase = 35`（`SoulItem.cs:24`）。编号契约写在 `SoulItem.cs:23` 的注释里：0-9 普通、10-14 基础支援、15-16 全收集奖励、17-24 旧版取回、34 传奇、35-36 圣林之冠。
- 反查层：`ItemAt(int slot)`（`SoulItem.cs:67`）、`SlotItems()`（`SoulItem.cs:76`）、`VisibleSlotIds`（`SoulItem.cs:31`）、`VisibleSupportCount`（`SoulItem.cs:30`）、`Stored`（`SoulItem.cs:61`）。
- 容量：`BaseCapacity`（`SoulItem.cs:47`）= `SoulProgress.Capacity(Kills,Cores)`；`BonusCapacity`（`SoulItem.cs:57`，矿石圆满 +2）；`CrownCapacity`（`SoulItem.cs:60`，`WoodCrown.Active` +2）；`SupportCapacity`（`SoulItem.cs:51`）→ `SoulSupport.Capacity`。
- 有效集合：`Active`（`SoulItem.cs:82`，主攻口径）、`Supports`（`SoulItem.cs:66`，只取 `Take(SupportCapacity)` 且非空）。
- 换入换出：`TrySwapHost(int, ref Item, out string)`（`SoulItem.cs:95`）—— 全模组唯一的寄宿写入入口。
- 校验：`CanHost(Item)`（`SoulItem.cs:92`）—— 近战、无镐斧锤、非消耗、非 `SoulItem`、非天顶剑、`maxStack==1`。
- 派生数值：`Stage`（`:85`）、`Awakened`（`:86`）、`Potency`（`:89`，未附 0 / 常态 `.65+Stage*.04` / 觉醒 1.1）、`Reach(Player)`（`:419`）、`UseSpeedMultiplier`（`:412`）。
- `SoulSupport`（`SoulSupport.cs:8`）：`Grants`（`:10`，木质 `[1,2,4,6,8]` / 矿石 `[1,2,4,6,8,10]`）、`Capacity`（`:16`，不动点迭代：解锁新格 → 重算族数 → 再解锁）、`SetItems`（`:38`）、`FamilyItems`（`:43`，= `Active ∪ Supports`）、`GrantedBy`（`:45`）、`Breakdown`（`:53`）。

**关联模块**
- 上游读取者：`SoulUI*`（配阵/详情/特效全部通过 `VisibleSlotIds`/`ItemAt`/`SlotItems` 取数据）、`SoulDomainUI`、`LivingArmament*`、`SoulArmoryPreview`。
- 下游依赖：`SoulProgress`（`Progression.cs`/`SlotProgression.cs`）、`SoulCurses`（特性开关与诅咒分级）、`WoodTypes` / `WoodCrown` / `OreFamily`（族计数）、`EnvironmentAttributes`（`RegisterEnvironments`）。

**关键数据流**
1. 解锁链：`SoulCore.UseItem` → `SoulProgress.AcceptCore` → `soul.Cores |= 1<<index` → `SoulProgress.Capacity(Kills,Cores)` → `SoulItem.BaseCapacity` → `Active` → `EmitBlade`。
2. 支援格链：`SoulItem.SupportCapacity` → `SoulSupport.Capacity` → 循环 `SetItems` → `WoodTypes.Mask` / `OreFamily.Mask` → `Grants[].Thresholds.Count(...)` → 回写格数。
3. 换装链：`SoulUI/Armory` 鼠标点击 → `SoulItem.TrySwapHost` → `SoulSupport.CanSupport`（= `CanHost`）/ 同种武器去重 → `RefreshStats`。

**常见问题定位入口（现象 → 先看哪里）**
- "某个槽位点不动 / 提示未解锁"：`SoulItem.TrySwapHost`（`SoulItem.cs:95`）的 `unlocked` 分支（`:141-166`）+ `SoulProgress.SlotHint`（`SlotProgression.cs:59`）。
- "支援格数量和预期不符"：`SoulSupport.Capacity`（`SoulSupport.cs:16`），注意木质族**只**统计 `items`（不含森林/圣林奖励位），矿石族统计 `roster`（含奖励位）—— 见 `SoulSupport.cs:31-32`。
- "放进武器后伤害/射程没变"：`RefreshStats`（`SoulItem.cs:387`）只改物品面板；真实伤害在 `SoulItem.BladeDamage`（`SoulCombat.cs:9`）+ `EmitBlade`（`SoulItem.cs:444`）。
- "槽位错位 / 放进去的物品跑到别的位置"：`ItemAt`（`SoulItem.cs:67`）与 `SupportSlotId`/`SupportOrdinal`（`:26`/`:28`）必须与 `SoulArmoryLayout.Slot`（`SoulArmoryLayout.cs:31`）三处同步。
- "旧存档武器消失/出现幽灵槽"：`Hosts.Skip(10)` + `SupportHosts.Skip(5)`（`SoulItem.cs:54` `HasLegacyExpansion`）、`SoulItem.LoadData`（`:307`）。

**修改影响提醒**
- 改动槽位编号（34/35/36、17-24 段）会同时打破：`ItemAt`、`SlotItems`、`VisibleSlotIds`、`SoulArmoryLayout.Slot`、`SoulUI*` 的命中框、`SoulUIEffects` 绘制、旧存档兼容。**17-24 段是存档兼容段，不可回收复用。**
- 改动 `CanHost` 会波及：`SoulSupport.CanSupport`、`SoulDomainUI` 的库存筛选、`SoulArmoryInteraction` 的可拖拽判定、`EnvironmentTooltips`、`SoulUILibrary`。
- 改动 `Active` 口径会同时改变：伤害均值、暴击均值、射程、`WoodTypes` 族计数、`OreFamily` 飞剑资格、`BondSnapshot`、`SoulTraits` 判定 —— 这是全模组最敏感的单点。
- 改动 `Potency` 或 `BladeDamage` 会直接影响 `checks/ZenithPrototypeCheck` 的期望值。

---

### 模块 2 · 诅咒、祝福与难度

**职责一句话**：把"六条诅咒/六条祝福"的进度分级（诅咒中→削弱→解除→祝福）换算成出剑把数、命中期数、穿地深度、射程下限与剑影伤害倍率，并提供御剑爆发与四类祝福运行时的增益。

**核心文件**：`SoulCurses.cs`、`SoulCurseDescriptions.cs`、`SoulEchoDamage.cs`、`SoulBlessings.cs`、`SoulDifficulty.cs`

**重要类 / 方法 / 入口**
- 枚举：`SoulCurse`（`SoulCurses.cs:9`，Binding/Switching/Sheathing/Barrier/Solitude/Bluntness）、`CurseStage`（`:10`，Cursed/Weakened/Released/Blessed）。
- 铭刻位：`Eye..Deer` 共 18 位（`SoulCurses.cs:15`）、`All=(1<<18)-1`（`:14`）、`Groups[6][]`（`:19`，每条诅咒绑定的 Boss 组）、`BossNames[6][]`（`:21`）。
- 查询：`BossSeal(int npcType)`（`:24`）、`Count`（`:31`）、`RecordedStage`（`:32`）、`StageForCount`（`:37`）、`Stage`（`:38`，含旧档 `LegacyReleased` 抬升）、`StageName`（`:43`）。
- 分级换算：`ScreenEdge`（`:46`）、`Blades`/`BladesAtStage`（`:55`/`:56`）、`HitLimit`（`:59`）、`TerrainAllowance`（`:62`）。
- `SoulItem` 分部（`SoulCurses.cs:76`）：`Seals`/`LegacyReleased`（`:78`）、`OreFlightMains`（`:81`）、`TraitEnabled`（`:82`）、`TraitMains`/`TraitSupports`/`TraitItems`（`:83-85`）、`CombatWood`（`:87`）、`SaveCurses`/`LoadCurses`/`SendCurses`/`ReceiveCurses`（`:88-95`）。
- `SoulTerrain`（`SoulCurses.cs:98`）：`SolidAt`（`:100`）、`Sweep`（`:109`）—— 阻隔诅咒的地形穿透判定。
- 文案：`SoulCurses.Description`（`SoulCurseDescriptions.cs:6`）、`StageDescription`（`:14`）、`Requirement`（`:36`）—— 界面与图鉴共用同一文案源。
- 剑影伤害：`SoulEchoDamage.Multiplier(soul, unit)`（`SoulEchoDamage.cs:7`）：祝福=1、解除=`0.8^unit`、其余=0.20。
- 祝福运行时：`BlessingTuning`（`SoulBlessings.cs:15`，900 帧蓄力 / 600 帧爆发 / 接力 5 层 / 10% 步进）、`BlessingPlayer`（`:26`，`TryActivate` `:51`、`PostUpdate` `:85`（蓄力+爆发计时+缩放混合）、`RelayPower` `:102`、`CommitRelay` `:109`、`StoreReturn`/`ConsumeReturn` `:118`/`:120`、`DistanceMultiplier` `:123`）、`BladeBlessings`（`:132`，逐弹幕首击/穿障/归鞘状态）、`SoulBreach`（`:171`，破甲 buff）、`BlessingTarget`（`:176`，+15 护甲穿透）、`BlessingStrike`（`:183`，祝福斩击弹幕）、`BlessingInterface`（`:230`，御剑条 UI + `ModifyTransformMatrix` 缩放）。
- 难度：`SoulDifficulty.Legendary`（`SoulDifficulty.cs:14`，`GameMode==2 && getGoodWorld`）、`BlockReason`（`:16`）、`Toggle`（`:25`）、`SaveWorldData`/`LoadWorldData`（`:49`/`:54`）—— 会真实改写 `Main.GameMode` / `Main.getGoodWorld`。

**关联模块**
- 读取者：`SoulItem`（`UseSpeedMultiplier`/`Reach`/`LegendaryOn`）、`SoulSwingPlayer`（`SoulCurses.Blades`、归鞘打断）、`SoulBlade`（`HitLimit`、`TerrainAllowance`、`SoulEchoDamage`）、`SoulTraits`（诅咒条目）、`SoulUIBossUnlocks`/`SoulUICurseUnlocks`/`SoulUIArchive`、`SoulDomainUI`（难度开关）、`CrossSummon`（`BossSeal` 防重复召唤）、`Progression.SoulBossCredit`（写 `Seals`）。
- 被写入者：只有 `SoulBossCredit.OnKill`（`Progression.cs:150`）与存档加载会改 `Seals`。

**关键数据流**
1. 铭刻→分级：`SoulBossCredit.OnKill` → `soul.Seals |= seal`（`Progression.cs:150`）→ `SoulCurses.Count` → `StageForCount` → `SoulItem.Curse(curse)` → `Blades/HitLimit/TerrainAllowance`。
2. 祝福→战斗：`BlessingPlayer.TryActivate`（按 O）→ `BurstLeft=600` → `SoulItem.Reach`（`range*=2`）+ `BlessingPlayer.DistanceMultiplier`（`SoulSource.ModifyHitNPC`，`SoulBlade.cs:35`）。
3. 首击接力：`SoulBlade.ModifyHitNPC` → `BladeBlessings.Prepare` → `BlessingPlayer.RelayPower` → `TraitPower` → 霜火/中毒时长乘算（`SoulBlade.cs:300-305`）。

**常见问题定位入口**
- "诅咒没解除 / 分级不动"：先看 `SoulBossCredit.OnKill` 是否记录了参与者（`SoulEncounters.Hits`，`Progression.cs:94`），再看 `SoulCurses.Groups` 的分组位（双 Boss 需要两个 bit 都置位）。
- "射程突然变得极短/极长"：`SoulItem.Reach`（`SoulItem.cs:419`）里的 `Binding` 分支 + `SoulCurses.ScreenEdge`（`:46`）；注意祝福态返回 `Unbounded=1e6`，`BurstFor` 的 ×2 会被跳过。
- "同一次挥砍只出 2 把剑"：`SoulCurses.Blades`（`:55`）—— 独行诅咒未解除时上限 2。
- "剑影几乎没伤害"：`SoulEchoDamage.Multiplier`（`:7`）—— 断锋诅咒未解除时剑影只有 20%。
- "御剑条不涨/按 O 没反应"：`BlessingPlayer.PostUpdate`（`SoulBlessings.cs:88-94`）要求"手持剑魂 + 缚身已祝福 + 最近 45 帧有战斗 + 魂域未打开"。
- "难度开关点了没反应"：`SoulDifficulty.BlockReason`（`:16`）—— 联机/菜单/旅途/Boss 战期间全部拒绝。

**修改影响提醒**
- `Groups` 的分组数量直接决定 `StageForCount` 的四档阈值（1 个=削弱、n-1=解除、n=祝福）。改长度会同时改 UI 奖励列表行数（`SoulBossUnlocks.Read`，`SoulBossUnlocks.cs:30`）。
- `SoulCurses.All` 是 18 位掩码，`LegacyReleased` 是 6 位；两者都参与存档与网络（`SendCurses`/`ReceiveCurses`），扩位必须同步改存档版本。
- `SoulEchoDamage.Multiplier` 与 `SoulBlade.ResolveHitUnit` 的 `HitUnit` 一一对应；改任一都会让"剑影命中"的伤害与视觉脱节。
- `SoulDifficulty.Toggle` 真实改写世界难度并写 world 存档（`legendaryApplied`），属于高破坏性操作，禁止在非单人环境放开。

---

### 模块 3 · 剑魂出剑与战斗

**职责一句话**：把"挥一次剑魂"翻译成"按诅咒上限轮流发射若干把飞剑弹幕"，并由 `SoulBlade` 负责弹幕的椭圆轨迹、十五更新采样的剑影、命中单元判定、伤害乘区整合与特技弹幕释放。

**核心文件**：`SoulBlade.cs`、`SoulCombat.cs`、`SoulSwingPlayer.cs`、`WeaponFamilies.cs`

**重要类 / 方法 / 入口**
- `SoulSource : GlobalProjectile`（`SoulBlade.cs:14`）：`SoulId`/`SourceWeapon`/`PrimaryOrdinal`/`HostSlot`（`:17-21`），`OnSpawn` 从 `EntitySource` 继承来源（`:38`），`ModifyHitNPC` 叠祝福距离倍率 + 环境乘区（`:31`），`AI` 里做 `LeashRadius` 超距销毁（`:56`），网络同步在 `SendExtraAI`（`:52`）。
- `SoulBlade : ModProjectile`（`SoulBlade.cs:63`）：`ConfigureReborn`（`:81`，设 8 游戏帧命中免疫=16 更新）、`CanDamage`（`:141`）、`TryGetEchoPose`（`:150`）、`CanHitNPC`（`:160`，读 `SoulCurses.HitLimit`）、`AI`（`:171`，椭圆轨迹 + 轨迹历史 + 地形穿透 + 24 帧释放特技）、`ResolveHitUnit`（`:268`，本体优先、其次最近剑影）、`OnHitNPC`（`:294`）、`ModifyHitNPC`（`:333`）、`ReleaseSpecial`/`ReleaseWeaponSpecial`（`:352`/`:363`）、`PreDraw`（`:485`）。
- `SoulItem.BladeDamage`（`SoulCombat.cs:9`）= `GetWeaponDamage(host) * Potency * OreFamily.MainDamageMultiplier`。
- `SoulSwingPlayer : ModPlayer`（`SoulSwingPlayer.cs:7`）：`Begin`（`:16`，选本挥所有有效主攻 + 计算 `budget=min(hosts, Blades(soul))` + 归鞘打断旧轮 + 飞刃族互斥 `budget=0`）、`FireHost`（`:39`，≤3 把时每节点一把）、`PostItemCheck`（`:57`，>3 把时均匀分布）、`Valid`（`:35`，每帧校验武器未换/魂域未开）。
- `WeaponFamilies`（`WeaponFamilies.cs:8`）：`BladeFamily` 六类（`:6`）、`Of`（`:18`，木质/矿石优先短路）、`Name`/`Description`（`:22`/`:25`）、`Interval`（`:32`）、`Range`（`:33`）。【已删除/历史快照：其中 `Interval`（`:32`）已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】

**关联模块**
- 上游：`SoulItem.Shoot`（`SoulItem.cs:440`）→ `SoulSwingPlayer.FireHost`；`SoulItem.UseAnimation`（`:437`）→ `Begin`。
- 下游：`WoodCombat`（`PreparePrimaryHit`/`ModifyPrimaryHit`/`FinishPrimaryHit`）、`WoodBondForge/Rot/Ember`、`HardOrePlayer`、`OreFamily.OnMainHit`、`HolyHeavenProc`、`BlessingPlayer`/`BladeBlessings`、`SoulTrailUnits`/`SoulMaterialTrail`/`SoulRibbonMesh`/`SoulTrailEnding`（绘制）、`SoulZenithEffects`、`SoulVisuals`、`FlyingBladeHand`。

**关键数据流**
```
SoulItem.CanUseItem → SoulSwingPlayer.Begin
  → (循环) SoulItem.EmitBlade → Projectile.NewProjectile(SoulBlade)
    → SoulSource.OnSpawn（继承 SoulId/HostSlot/PrimaryOrdinal/SourceWeapon）
  → SoulBlade.AI（椭圆轨迹 + trailCenters 历史 + SoulTerrain.Sweep 阻隔）
  → SoulBlade.ModifyHitNPC → SoulEchoDamage.Multiplier(HitUnit)
       → Blessings.Prepare / WoodCombat.ModifyPrimaryHit / HardOrePlayer.Modify
  → SoulBlade.OnHitNPC → 霜火/中毒 → WoodCombat.FinishPrimaryHit
       → WoodBond{Crown/Forge/Rot/Ember} 派发 → HardOrePlayer.Hit / OreFamily.OnMainHit
       → HolyHeavenProc.OnMainHit → Blessings.Hit
```

**常见问题定位入口**
- "右键/左键挥了但不出剑"：`SoulItem.CanUseItem`（`SoulItem.cs:371`，`!Attached` 或 `Active` 为空）→ `SoulSwingPlayer.Valid`（`:35`）。
- "只出 1 把 / 出的把数不对"：`SoulSwingPlayer.Begin` 的 `budget`（`:27`）→ `SoulCurses.Blades`；>3 时的节奏在 `PostItemCheck`（`:57`）。
- "飞剑打不中 / 剑影不造成伤害"：`SoulBlade.CanDamage`（`:141`）与 `ResolveHitUnit`（`:268`）、`TryGetEchoPose`（`:150`）的 `opacity<=.02` 提前返回。
- "同一目标打不满次数"：`SoulBlade.CanHitNPC`（`:160`）+ `SoulCurses.HitLimit`。
- "穿墙穿不过去 / 突然断剑"：`SoulBlade.AI` 的 `terrainArmed`/`SoulTerrain.Sweep`（`SoulBlade.cs:214-216`）+ `SoulCurses.TerrainAllowance`。
- "特技弹幕（蜜蜂/南瓜/星）不触发"：`ReleaseSpecial`（`:352`）要求 `phase>=24`、`TraitEnabled(type)`、`SoulProgress.ZenithWeapons.Contains(type)`，灵能族另有 45 帧冷却（`SoulPlayer.TryArcane`，`Progression.cs:57`）。
- "武器族加成没生效"：`WeaponFamilies.Of`（`WeaponFamilies.cs:18`）先判 `WoodTypes.IsWood` 再判表，木质/矿石永远返回 `Plain`，属设计而非 bug。

**修改影响提醒**
- `SoulBlade` 同时承担"运动学 / 碰撞 / 伤害 / 特技 / 绘制数据源"五件事，且服务端与客户端共用同一份 `trailCenters` 历史（`SoulBlade.cs:244-253`）。任何改动必须保证 **服务端也走同样分支**，否则剑影"看得见打不到"。
- `SoulSource` 是跨模块的**来源身份总载体**：`BlessingPlayer.ValidSource`、`WoodBondHit`、`SoulBossCredit`、`FlyingBladeFamily` 全部依赖它。改字段语义会连锁影响 5 个模块。
- `SoulSwingPlayer.begin` 中调用 `FlyingBladeHand.Launch` 会把 `budget` 归零，属飞刃族与本体的**互斥契约**（`SoulSwingPlayer.cs:26-27`）。
- `WeaponFamilies` 的 `Solid` 表把木质剑也列入（`WeaponFamilies.cs:15-17`），但 `Of` 先行短路返回 `Plain` —— 若调整判定顺序会瞬间改变木质族手感。

---

### 模块 4 · 进度、魂芯、铭刻与解锁视图

**职责一句话**：管理"这把剑魂个人打到了哪"（12 位 Boss 击杀掩码 + 6 枚魂芯掩码 → 10 个普通槽的门槛），并负责战斗参与记录、魂芯补办、Boss 解锁展示模型、四柱/月总召唤修复与跨邪恶召唤物。

**核心文件**：`Progression.cs`、`SlotProgression.cs`、`SoulBossUnlocks.cs`、`SoulBossPortraits.cs`、`SoulBossPortraitCatalog.cs`、`SoulBossAnimation.cs`、`AttachmentStory.cs`、`CrossSummon.cs`、`LunarSummonRecovery.cs`

**重要类 / 方法 / 入口**
- `SoulProgress`（`static partial`，两分部）：`Progression.cs:9` + `SlotProgression.cs:9`。
  - 位定义：`Eye..Moon` 12 位、`All=4095`（`Progression.cs:11-14`）；`ZenithWeapons[10]`（`:15`）；`CoreNames[6]`（`:18`）。
  - 计算：`Has`（`:19`）、`Mechs`（`:20`）、`Milestones`（`:22`）、`Capacity(kills,cores)`（`:24`，逐门槛 `SlotGates` 连续通过才 +1）、`BossBit(npcType)`（`:34`）、`Next(soul)`（`:44`，玩家可见的"下一步"）。
  - 魂芯：`SlotGates[9]`（`SlotProgression.cs:12`）、`CoreKills`（`:23`）、`CoreInstructions`（`:24`）、`CanAcceptCore`（`:29`）、`AcceptCore`（`:31`）、`RecoverableCore`（`:38`）、`Missing`（`:47`）、`SlotHint`（`:59`）、`RequestCoreRecovery`（`:81`）、`RecoverCore`（`:87`）。
- `SoulPlayer : ModPlayer`（`Progression.cs:52`）：`TryArcane`（`:57`，灵能 45 帧冷却）、`AddStartingItems`（`:67`，送剑魂+凝胶信物）、`PostUpdate`（`:75`，冷却递减 + `HeroPrefixCompat.Remember` + `Flow`）。
- `SoulEncounters : ModSystem`（`Progression.cs:92`）：`Hits[seal] = HashSet<(Player,Soul)>`，Boss 消失即清除（`:97`）。
- `SoulBossCredit : GlobalNPC`（`Progression.cs:105`）：`Register`（`:107`）、`ReceiveCredit`（`:115`，服务端校验弹幕身份）、`ModifyHitByProjectile`（`:124`，客户端发 packet 8）、`OnKill`（`:136`，写 `Kills|bit`、`Seals|=seal`、按需掉落魂芯）。
- `SoulCore : ModItem`（`Progression.cs:162`）+ 6 个子类（`:200-205`）：`Target`（`:177`，优先当前打开的剑魂）、`UseItem`（`:185`）。
- 解锁视图（纯只读）：`SoulBossUnlockView`/`SoulBossTarget`/`SoulBossReward`（`SoulBossUnlocks.cs:5-14`）、`TryCurse`（`:19`）、`Read`（`:24`）。
- 头像：`SoulBossPortraits.Key`（`SoulBossPortraits.cs:12`）、`HasBody`（`:17`）、`Draw`（`:18`，立即加载贴图，按 ms 取帧）；`Catalog`（`SoulBossPortraitCatalog.cs:6`，脚本 `checks/prepare_boss_portraits.py` 生成）；`SoulBossAnimation.FrameAt`/`FrameRectangle`（`SoulBossAnimation.cs:23`/`:38`）。
- 附着叙事：`AttachmentStory`（`AttachmentStory.cs:13`）：`CallingSoul`（`:17`）、`FindSoul`（`:26`）、`FindKing`（`:28`）、`Requirement`（`:30`）、`CanStart`（`:39`）、`RequestStart`（`:42`，包 1/7）、`Start`（`:48`，召唤友善史莱姆王）、`RequestAdvance`（`:65`，包 2）、`Advance`（`:71`，4 句对话后置 `Attached=true; KingMemory=true`）、`Notice`（`:98`）。配套 `GuardianSlimeKing`（`:105`）、`GuardianGel`（`:180`，凝胶信物）、`SlimeAudienceProtection`（`:208`，保护观众史莱姆）。
- 跨邪恶召唤：`CrossSummonItem`（`CrossSummon.cs:12`）、`CrimsonSigil`（`:42`）、`CorruptionLure`（`:48`）、`CrossSummonDrops`（`:56`，只由本世界原生邪恶 Boss 掉落对侧召唤物）。
- 月总修复：`LunarSummonRecovery`（`LunarSummonRecovery.cs:11`）：`PillarBit`（`:18`）、`RememberCountdown`（`:28`，钩 `On_WorldGen.StartImpendingDoom`）、`ObserveSpawn`/`ObserveKill`（`:53`/`:60`）、`TryRepair`（`:72`）、`UpdateRecovery`（`:81`）、`ApplyQueuedRepair`（`:121`，读 `ZenithReborn.moon-recovery.json`）、`RequestRepair`（`:135`）、`Status`（`:143`）；`LunarPillarTracker`（`:153`）、`MoonRecoveryCommand /zmoon`（`:158`）。

**关联模块**
- `ZenithReborn.HandlePacket`（`ZenithReborn.cs:18`）是这些流程的**唯一网络入口**：1/7→附着、2→推进、5→魂芯补办、8→Boss 授勋、9→月总修复。
- 视图消费方：`SoulUIArchive`、`SoulUICurseUnlocks`、`SoulUIBossUnlocks`、`SoulUIMemories`、`SoulUI`（`Next` 显示）。

**关键数据流**
1. 击杀铭刻：`SoulBlade` 命中 → `SoulBossCredit.ModifyHitByProjectile` →（客户端）packet 8 → `HandlePacket` → `ReceiveCredit` → `SoulEncounters.Hits[seal].Add` → `OnKill` → `soul.Kills |= bit; soul.Seals |= seal` → 服务端 `SyncEquipment`。
2. 魂芯：`SoulCore.UseItem` → `Target` → `SoulProgress.CanAcceptCore` → `AcceptCore` → `RefreshStats` → `SoulUI.Sync`。
3. 附着：`GuardianGel.UseItem` → `RequestStart` → 包 1/7 → `Start` → `GuardianSlimeKing.OnChatButtonClicked` → `RequestAdvance` → 包 2 → `Advance` → `Attached=true`。

**常见问题定位入口**
- "打了 Boss 但槽位没解锁"：`SoulProgress.Capacity`（`Progression.cs:24`）是**连续门槛**：任一 `SlotGates` 断档后续全部不计；再看 `SoulBossCredit.OnKill` 的 `SoulEncounters.Hits.Remove`（`:142`）是否拿到了参与者。
- "和朋友一起打 Boss 只有一人记录"：`SoulEncounters.Hits` 存的是 `(Player, SoulId)` 对，但 `OnKill` 只遍历 `inventory[0..57]` 找同 `SoulId` 的剑魂（`Progression.cs:147-149`）。
- "魂芯点了没反应"：`SoulProgress.RecoverableCore`（`SlotProgression.cs:38`）要求"未满 10 槽 + 当前门槛的击杀位已满足 + 背包里有对应魂芯"。
- "NPC 名人堂头像不动"：`SoulBossPortraits.Draw`（`:18`）按 `seconds*1000` 取帧，`Catalog` 里没有的 key 直接 return。
- "四柱打完了月亮领主要不来"：`LunarSummonRecovery.UpdateRecovery`（`:81`）的三个早退分支（`HasMoonLord`/`LunarApocalypseIsUp`/`MoonLordCountdown>0`），或用 `/zmoon repair`。
- "史莱姆王不出现"：`AttachmentStory.CanStart`（`:39`）要求白天 + 地表 + （凝胶信物 或 附近 600px 普通史莱姆）；`Start` 还会做 7 个偏移点的碰撞检测（`:54-56`）。

**修改影响提醒**
- `SlotGates` 是**存档兼容契约**：`Capacity` 依赖门槛顺序，任何中间插入/删除都会让老存档的 `Capacity` 直接变化；`Missing`/`SlotHint`/`SoulTraits` 文案也全部按索引取。
- `SoulCurses.BossSeal` 与 `SoulProgress.BossBit` 是**两套独立映射**（前者 18 位诅咒位，后者 12 位进度位），同名 Boss 在两套里编号不同，改一处不改另一处会导致"诅咒解除了但槽位没解锁"。
- `SoulBossCredit.OnKill` 会 `QuickSpawnItem` 发魂芯且写 `NetMessage.SyncEquipment`，改这段要考虑联机重复发放。
- `LunarSummonRecovery` 直接改 `NPC.TowerActive*` / `NPC.MoonLordCountdown` 等原版全局状态，且会读写磁盘 JSON；属于高副作用模块。

---

### 模块 5 · 羁绊快照

**职责一句话**：把木质族/矿石族的"数量 → 阶段 → 进度 → 颜色"统一成只读展示模型，供配阵页核心环、木质面板与详情页共用。

**核心文件**：`BondSnapshot.cs`

**重要类 / 方法 / 入口**
- `BondKind`（`:6`）、`BondState`（`:9`，`Active`/`NextThreshold`/`PrevThreshold`/`StageName`/`StageCode`/`StageLine`）。【已删除/历史快照：其中 `NextThreshold`/`PrevThreshold` 两个属性已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】
- `BondSnapshot`（`:35`）：`WoodThresholds=[1,2,4,6,8]`（`:38`）、`OreThresholds=[1,2,4,6,8,10]`（`:40`）、`WoodStageNames`（`:42`，萌芽/生根/繁枝/森罗/森林共鸣）、`StageNameOf`（`:48`）、`StageCodeOf`（`:56`）、`Wood(soul)`（`:59`）、`Ore(soul)`（`:67`）、`Build`（`:74`）、`Evaluate`（`:93`）、`Leading`（`:96`）。
- `BondColors`（`:112`）：`Base`（`:134`）、`Tier`（`:137`）、`Prismatic`（`:144`）。

**关联模块**
- 数据来源：`SoulSupport.FamilyItems`（木质）、`OreFamily.Count`（矿石）。
- 消费者：`SoulUIArmoryBondCore`（`:11`/`:89` `Evaluate`+`Leading`）、`SoulUIArmoryDetails`（`:49` `Wood`）、`SoulUIWoodPanel`（`:44` `Wood`）。

**关键数据流**：`SoulSupport.FamilyItems(soul)` → `WoodTypes.Mask` → `BitOperations.PopCount` → `Build` → `BondState` → `SoulUIArmoryBondCore.DrawCoreFlow` / `SoulUIWoodPanel`。

**常见问题定位入口**
- "核心环显示的数量和面板不一致"：`BondSnapshot.Wood`（`:59`）用 `FamilyItems`（含奖励位/传奇位），而 `SoulItem.Active` 不含支援 —— 两者口径本就不同，`SoulTraits` 的 `wood-set` 用的是 `CombatWood`（第三种口径，还额外过 `TraitEnabled`）。
- "矿石分母是 10 但提示说 14"：`BondSnapshot.Ore`（`:67`）分母固定 10，14 种是"全收集奖励"另一套（`SoulItem.BonusCapacity`）。

**修改影响提醒**：`BondSnapshot` 声称只读，但实际上三个界面的显示一致性完全依赖它；改阈值必须同步 `SoulTraits`（`SoulTraits.cs:26-43` 的文案把 1/2/4/6/8 写死）与 `SoulSupport.Grants`。

---

### 模块 6 · 特性图鉴与族标签

**职责一句话**：生成"这把剑魂当前生效/未生效的全部特性条目"（图鉴数据源），并提供"这件武器属于哪些族"的单一展示出口。

**核心文件**：`SoulTraits.cs`、`WeaponFamilyTags.cs`

**重要类 / 方法 / 入口**
- `TraitCategory`（`SoulTraits.cs:7`，顺序即筛选按钮顺序，只能追加）、`SoulTrait`（`:8`）。
- `SoulTraits.Active`（`:15`）、`SoulTraits.All(soul)`（`:16`，一次性构建全部条目）。
- 条目分区：木质 `wood-set`/`forest`/8 条单木（`:26-34`）、三条复合羁绊 + 圣林归冠（`:36-43`）、共用支援（`:46`）、全收集奖励（`:48`）、矿石飞剑与 2/4/6/8/10 阶（`:50-65`）、矿石圆满（`:68`）、肉后六矿特性（`:72-78`）、神圣天堂（`:80`）、剑灵（`:82`）、飞刃族五阶（`:85-92`）、剑类五族（`:93-98`）、铜短剑初心（`:99`）、天顶材料特技（`:101-116`）、附着（`:117`）、六条诅咒（`:119-124`）、复生（`:125`）。
- `WeaponFamilyTags.Of(type)`（`WeaponFamilyTags.cs:10`，固定拼接顺序 飞刃→神圣天堂→剑灵→木质→矿石）、`IsAny`（`:25`）、`Line`（`:29`，含飞刃族阶位提示 `ActivationHint` `:37`）。

**关联模块**
- 判定依赖：`SoulCurses.TraitEnabled`/`CombatWood`、`SoulSupport.Capacity`/`FamilyItems`、`OreFamily`、`HardOreTypes`、`HolyHeavenFamily`、`LivingArmamentFamily`、`FlyingBladeFamily`、`WoodBondCore`、`WoodTypes`、`WeaponFamilies`。
- 消费者：`SoulUIArchive`（`:20`/`:41`/`:56`）、`SoulUI`（`:423`）、`SoulUIArmoryDetails`（`:44`）、`SoulUILibrary`（`:61`）、`EnvironmentTooltips`（`:48`）。

**关键数据流**：`SoulTraits.All(soul)` → `SoulUIArchive.TraitEntries`（按 `Category` 过滤）→ 条目卡片；点击 `curse-*` key → `SoulBossUnlocks.TryCurse` → `Read` → 解锁详情。

**常见问题定位入口**
- "图鉴里某特性显示未激活但战斗有加成"：多数条目的 `Active` 是**手写复算**，与战斗侧判定是两份代码（例：`ore-tier-2` 用 `oreCount`，战斗侧用 `OreFamily.MainDamageMultiplier`）。
- "族标签缺一行"：`WeaponFamilyTags.Of`（`:10`）的五个 `if` 是唯一来源，新增族必须在此追加。
- "筛选按钮错位"：`TraitCategory` 枚举顺序被 UI 索引（`SoulUIArchive.cs:56`）。

**修改影响提醒**：`SoulTraits` 是 129 行的巨型方法，任何数值改动都要与战斗侧同步；改 `TraitCategory` 顺序会直接打乱存档无关但已发布的 UI 编号。

---

### 模块 7 · 界面排版与输入基础设施

**职责一句话**：为魂域窗口提供纯几何/纯输入的底层设施——配阵页布局、换装预览沙盒、信息页自适应栅格、以及"点击不穿透到世界"的鼠标捕获层。

**核心文件**：`SoulArmoryLayout.cs`、`SoulArmoryPreview.cs`、`SoulInfoLayout.cs`、`SoulInput.cs`

**重要类 / 方法 / 入口**
- `SoulArmoryLayout`（`SoulArmoryLayout.cs:7`）：`Create(w,h)`（`:9`，宽 <1180 或高 <620 → `Compact` 单栏）、`Mode`（`:24`）、`SupportColumns`（`:25`）、`SceneHeight`（`:26`）、`Radius`（`:27`）、`Center`（`:29`）、`SupportTop`（`:30`）、`Slot(id,supports,scroll)`（`:31`，0-9 圆环 36°、34 传奇右上、15/16 左上、35/36 圣林第二行、其余支援网格）、`WoodPanel`（`:55`）、`LibraryList`（`:64`）、`Confirm`/`Return`/`DetailBody`（`:67`/`:69`/`:71`）。
- `SoulArmoryPreview.Evaluate(source, slot, candidate)`（`SoulArmoryPreview.cs:8`）：克隆整件剑魂 → 在副本上跑真实 `TrySwapHost` → 返回 `Allowed/Reason/伤害差/暴击差/主攻数/容量`，**不写回真实物品**。
- `SoulInfoLayout`（`SoulInfoLayout.cs:6`）：`Columns`（`:9`，1120/620 两档）、`Tile`（`:10`）、`Ease`（`:15`）、`Approach`（`:16`）、`MasterDetail`（`:19`，宽 ≥850 左右分栏，否则上下分）。
- `SoulInput`（`SoulInput.cs:10`）：`Capture`（`:20`，记录按压并置 `Blocking`）、`MaskBackground`（`:41`，清 `Main.mouseLeft*` + `mouseInterface` + 锁滚轮）、`BeginSoulLayer`/`EndSoulLayer`（`:48`/`:54`）、`RestoreAfterInterface`（`:61`）；`SoulUIControls.SetControls`（`:71`，禁 `controlUseItem`/`controlUseTile`）。

**关联模块**：全部被 `SoulUI*` 使用（`SoulUIArmoryInteraction.cs:21` 是 `ArmoryLayout` 的缓存出口；`SoulUI.cs:98-123` 是输入时序）。`SoulInfoLayout` 另被 `EnvironmentUI.cs:22/30` 复用。

**关键数据流**：`SoulUI.PreDraw`/`Update` → `SoulInput.Capture(inside)` → 世界更新被 mask → `SoulInput.BeginSoulLayer` → 界面层消费 `Main.mouseLeft` → `EndSoulLayer` → `PostDrawInterface` → `RestoreAfterInterface`。

**常见问题定位入口**
- "点按钮时人物也在挥剑/挖东西"：`SoulInput.MaskBackground`（`:41`）必须同时清 `Main.mouseLeft`、`PlayerInput.Triggers.Current.MouseLeft`（`:37`）和 `Player.controlUseItem`（`:75`）三处，漏一处就会穿透。
- "关闭窗口后鼠标卡住":`SoulInput.Reset`（`:15`）在 `SoulUI` 卸载/切页时是否被调用。
- "配阵槽位和实际物品对不上"：`SoulArmoryLayout.Slot`（`:31`）必须与 `SoulItem.SupportOrdinal`（`SoulItem.cs:28`）编号一致。
- "窗口变形/重叠"：`Create` 的 `Compact` 阈值（`:13`）与 `Radius` 的 `Clamp(102,198)`（`:28`）。

**修改影响提醒**：`SoulArmoryLayout` 是纯几何，改它只影响绘制与命中；但它**同时被 `SoulUI.SlotBounds`（`SoulUI.cs:213`）与 `SoulDomainUI`（`:17`）复用**，一处偏移会同时错位视觉与点击。`SoulInput` 的时序一旦改动会波及整个游戏输入（不止模组 UI）。

---

### 模块 8 · 模组装配、网络中继与存档兼容

**职责一句话**：模组的 `Mod` 入口（键位注册 + 12 种网络包分发）、英雄模组前缀重建的抢救式兼容、旧版武技字段的"只存不用"、全局 using 补齐，以及一个遗留的测试武器（该测试物品已移除）。

**核心文件**：`ZenithReborn.cs`、`HeroPrefixCompat.cs`、`SoulLegacyMartialData.cs`、`GlobalUsings.cs`、`SlashZenithTest.cs`【已删除/历史快照：该测试物品源码与本地化键已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】

**重要类 / 方法 / 入口**
- `ZenithReborn : Mod`（`ZenithReborn.cs:8`）：`OpenSoul`(P) / `ActivateYujian`(O) 键位（`:10-16`）；`HandlePacket`（`:18`）动作表：

  | action | 接收方 | 含义 |
  |---|---|---|
  | 1 / 7 | `AttachmentStory.Start` | 附着开始（7 = 使用凝胶信物） |
  | 2 | `AttachmentStory.Advance` | 对话推进 |
  | 3 | `WoodCombat.ReceiveHit` | 木质族伤害中继 |
  | 4 | `WoodPlayer.ReceiveGrowth` | 成长同步 |
  | 5 | `SoulProgress.RecoverCore` | 魂芯补办 |
  | 6 | `GuardianSlimeKing` ai 同步 | 史莱姆王对话状态 |
  | 8 | `SoulBossCredit.ReceiveCredit` | Boss 授勋 |
  | 9 | `LunarSummonRecovery.RequestRepair` | 月总召唤修复 |
  | 10 | `BlessingPlayer.Receive` | 御剑爆发同步 |
  | 11 | `EnvironmentPlayer.ReceiveHit` | 环境伤害中继 |
  | 12 | `WoodBondRelay.Receive` | 木质复合羁绊中继 |

  注意：1/2/5/7/9 有 `Main.netMode!=Server` 的守卫（`:45`），3/4/6/8/10/11/12 在守卫之前处理。
- `HeroPrefixCompat`（`HeroPrefixCompat.cs:11`）：`Remember`（`:27`，鼠标持有剑魂时缓存全量数据）、`TryRestore`（`:48`，只在"空剑魂 + 全零状态"时回填）、`Clear`（`:73`）。静态缓存包含 `hosts/supports/forest/crown/additional/legendary/soulId/kills/cores/oreCollection/seals/legacy/yujian/martial/environments/domain/attached/memory`。
- `SoulLegacyMartialData`（`SoulLegacyMartialData.cs:7`）：`LegacyMartialCharge`/`LegacyMartialTier` + `Save/Load/Send/ReceiveLegacyMartial`（`:12-19`）—— 注释明确"不参与任何玩法"。
- `GlobalUsings.cs:3-9`：补 `System`/`Collections.Generic`/`IO`/`Linq`/`Text`/`Threading`/`Threading.Tasks`。
- `SlashZenithTest`（`SlashZenithTest.cs:10`）：测试用近战剑，木头 1 个合成；注释写了"正式内容请勿依赖，可整文件删除"。【已删除/历史快照：已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】

**关联模块**：`HandlePacket` 是模块 2/4 的公共入口；`HeroPrefixCompat` 依赖 `SoulItem` 的全部字段与 `SoulLegacyMartialData`。

**常见问题定位入口**
- "联机时附着/魂芯无效"：`HandlePacket` 的 `whoAmI` 守卫（`ZenithReborn.cs:45`）与各 `Receive*` 内部的 `netMode` 判断。
- "用英雄模组改前缀后剑魂清空"：`HeroPrefixCompat.TryRestore`（`:52`）的"必须是空剑魂"前置条件；`SoulItem.ApplyPrefix`（`SoulItem.cs:283`）是唯一调用点。
- "重铸（哥布林）后寄宿武器消失"：`SoulItem.PreReforge`/`PostReforge`（`SoulItem.cs:224`/`:251`）的静态快照字段。

**修改影响提醒**：新增网络包必须在 `HandlePacket` 里挑选**未占用**的 action 编号（当前 1-12 已用），且注意服务器守卫的位置；`HeroPrefixCompat` 是全局静态单缓存，多人/多剑魂场景下只能记住最后一次 `Remember` 的那把。

---

## 第二部分：文件索引表

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `ZenithReborn/ZenithReborn.cs` | Mod 入口类 | 装配/网络 | 注册 P/O 键位；`HandlePacket` 分发 action 1-12 到 9 个业务模块 | `AttachmentStory`、`SoulProgress`、`SoulBossCredit`、`BlessingPlayer`、`LunarSummonRecovery`、`WoodCombat`、`EnvironmentPlayer` | 已验证 |
| `ZenithReborn/SoulItem.cs` | ModItem（partial 主分部） | 数据模型 | 剑魂物品本体：5 组槽位数组、槽号映射、容量/效力/射程、`TrySwapHost`、存读档与网络 | `SoulSupport`、`SoulProgress`、`SoulCurses`、`SoulDifficulty`、`SoulUI`、`SoulArmoryLayout` | 已验证 |
| `ZenithReborn/SoulSupport.cs` | 静态类 | 数据模型 | 支援格通用奖励池：两条 `SupportGrant`、不动点迭代求 `Capacity`、`FamilyItems` 族判定集合 | `SoulItem`、`WoodTypes`、`OreFamily`、`WoodCrown`、`BondSnapshot` | 已验证 |
| `ZenithReborn/SoulCurses.cs` | 静态类 + `SoulItem` 分部 | 诅咒/难度 | 18 位铭刻、6 组诅咒分级、出剑数/命中期/穿地上限、特性开关、诅咒存档网络、地形穿透判定 | `SoulBossCredit`、`SoulSwingPlayer`、`SoulBlade`、`SoulTraits`、`SoulBossUnlocks` | 已验证 |
| `ZenithReborn/SoulCurseDescriptions.cs` | `SoulCurses` 分部 | 诅咒/难度 | 诅咒与祝福的阶段说明/需求文案，UI 与图鉴共用 | `SoulCurses`、`SoulBossUnlocks`、`SoulUIArchive` | 已验证 |
| `ZenithReborn/SoulEchoDamage.cs` | 静态类 | 诅咒/难度 | 断锋诅咒 → 剑影伤害倍率（1 / 0.8^unit / 0.20） | `SoulBlade.ModifyHitNPC`、`SoulTrailUnits` | 已验证 |
| `ZenithReborn/SoulBlessings.cs` | ModPlayer + ModProjectile + ModBuff + ModSystem | 诅咒/难度 | 御剑蓄力/爆发、首击接力、归鞘留锋、破障穿地、距离增伤、御剑条 UI | `SoulItem.Reach`、`SoulSource`、`SoulBlade`、`SoulCurses`、`SoulUI.IsEditing` | 部分待核实（`BlessingStrike` 的绘制依赖 `OreSwordEffects`，未在本组） |
| `ZenithReborn/SoulDifficulty.cs` | ModSystem | 诅咒/难度 | 传奇难度开关：`GameMode==2 && getGoodWorld`，真实改写世界难度并持久化 | `SoulItem.LegendaryOn`、`SoulDomainUI` | 已验证 |
| `ZenithReborn/SoulBlade.cs` | ModProjectile + GlobalProjectile | 战斗 | 飞剑弹幕：来源身份 `SoulSource`、椭圆轨迹、15 更新剑影采样、命中单元判定、伤害乘区整合、特技释放、绘制 | `SoulItem.EmitBlade`、`WoodCombat`、`WoodBond*`、`HardOrePlayer`、`OreFamily`、`HolyHeavenProc`、`BlessingPlayer`、`SoulTrail*` | 部分待核实（木质复合羁绊与矿石命中细节在 `WoodCombat.cs`/`Ore*`，不在本组） |
| `ZenithReborn/SoulCombat.cs` | `SoulItem` 分部 | 战斗 | 单把宿主的真实伤害 = 武器伤害 × `Potency` × 矿石主剑倍率 | `SoulItem`、`OreFamily` | 已验证 |
| `ZenithReborn/SoulSwingPlayer.cs` | ModPlayer | 战斗 | 挥砍发射调度：选出本轮主攻、按独行上限分配节点、归鞘打断、飞刃族互斥 | `SoulItem.UseAnimation/Shoot/EmitBlade`、`SoulCurses.Blades`、`FlyingBladeHand` | 已验证 |
| `ZenithReborn/WeaponFamilies.cs` | 静态类 | 战斗 | 剑类五族（迅刃/实锋/寒霜/自然/灵能）名册、判定、文案、攻速与射程系数 | `SoulItem.UseSpeedMultiplier/Reach/EmitBlade`、`SoulBlade`、`SoulTraits`、`WoodTypes` | 已验证 |
| `ZenithReborn/Progression.cs` | 静态类 + ModPlayer + ModSystem + GlobalNPC + ModItem | 进度解锁 | 12 位击杀掩码、`Capacity`/`Next`、玩家冷却与初始物品、战斗参与记录、Boss 授勋、6 种魂芯 | `SoulItem`、`SoulCurses`、`SoulUI`、`AttachmentStory` | 已验证 |
| `ZenithReborn/SlotProgression.cs` | `SoulProgress` + `SlotGates` | 进度解锁 | 9 条槽位门槛表、魂芯受理/补办、槽位提示与缺口文案 | `SoulItem`、`SoulSupport`、`SoulUIMemories` | 已验证 |
| `ZenithReborn/SoulBossUnlocks.cs` | 静态类 + 3 record | 进度解锁 | 只读展示模型：每诅咒的 Boss 目标、四档奖励与当前/旧档状态 | `SoulCurses`、`SoulUIBossUnlocks`、`SoulUICurseUnlocks`、`SoulUIArchive` | 已验证 |
| `ZenithReborn/SoulBossPortraits.cs` | `SoulBossPortraits` 分部 | 进度解锁 | 按封印位取图集 key、绘制动态头像（有 body 的双层叠加） | `SoulBossPortraitCatalog`、`SoulBossAnimation`、`SoulUIBossUnlocks` | 已验证 |
| `ZenithReborn/SoulBossPortraitCatalog.cs` | 生成代码 | 进度解锁 | 15 个 Boss 的图集列数与每帧时长（由 `checks/prepare_boss_portraits.py` 生成） | `SoulBossPortraits`、`SoulBossAnimation` | 已验证 |
| `ZenithReborn/SoulBossAnimation.cs` | record | 进度解锁 | 96px 棋盘图集的时间轴：按 ms 取帧、算源矩形 | `SoulBossPortraits.Draw` | 已验证 |
| `ZenithReborn/AttachmentStory.cs` | 静态类 + ModNPC + ModItem + GlobalNPC | 进度解锁 | 附着叙事线：条件检查、召唤友善史莱姆王、4 句对话推进、授予 `Attached`、凝胶信物 | `SoulUI`、`SoulProgress`、`SoulItem.RefreshStats`、`ZenithReborn.HandlePacket` | 已验证 |
| `ZenithReborn/CrossSummon.cs` | ModItem 抽象 + GlobalNPC | 进度解锁 | 无生物群落限制的脑/世吞召唤物，只由本世界原生邪恶 Boss 掉落对侧 | `SoulCurses.BossSeal` | 已验证 |
| `ZenithReborn/LunarSummonRecovery.cs` | ModSystem + GlobalNPC + ModCommand | 进度解锁 | 四柱→月总召唤的卡死检测与修复（含磁盘 JSON 请求、`/zmoon repair`） | `ZenithReborn.HandlePacket`、`AttachmentStory.Notice` | 已验证 |
| `ZenithReborn/BondSnapshot.cs` | record + 静态类 | 羁绊快照 | 木质/矿石的阈值、阶段名、进度、配色统一为只读展示模型 | `SoulSupport.FamilyItems`、`OreFamily`、`SoulUIArmoryBondCore`、`SoulUIWoodPanel` | 已验证 |
| `ZenithReborn/SoulTraits.cs` | 静态类 + 2 record/enum | 特性图鉴 | 一次性构建全部特性条目（族、阶位、特技、诅咒、复生）供图鉴筛选 | `SoulCurses`、`SoulSupport`、`OreFamily`、`WeaponFamilies`、`SoulBossUnlocks` | 部分待核实（各族 `Active` 判定散落在 `WoodBondCore`/`OreSwordTraits`/`FlyingBladeFamily` 等） |
| `ZenithReborn/WeaponFamilyTags.cs` | 静态类 | 特性图鉴 | "所属种族"文本的单一出口，固定拼接顺序，附飞刃族阶位提示 | `FlyingBladeFamily`、`HolyHeavenFamily`、`LivingArmamentFamily`、`WoodTypes`、`OreFamily` | 已验证 |
| `ZenithReborn/SoulArmoryLayout.cs` | record struct | UI 基础设施 | 配阵页全部几何：分栏、圆环半径、槽位矩形、木质面板、库列表、按钮 | `SoulItem.LegendarySlotId/CrownSlotBase/SupportOrdinal`、`SoulUI*` | 已验证 |
| `ZenithReborn/SoulArmoryPreview.cs` | record | UI 基础设施 | 在整件克隆上跑真实 `TrySwapHost` 生成换装预览，绝不写回真品 | `SoulItem.TrySwapHost/ItemAt/Capacity`、`SoulUIArmoryInteraction`/`SoulUIArmoryDetails` | 已验证 |
| `ZenithReborn/SoulInfoLayout.cs` | 静态类 | UI 基础设施 | 信息页自适应栅格：列数、卡片矩形、主从分栏、缓动函数 | `SoulUIArchive`、`SoulUICurseUnlocks`、`SoulUIMemories`、`SoulUIInformation`、`EnvironmentUI` | 已验证 |
| `ZenithReborn/SoulInput.cs` | 静态类 + ModPlayer | UI 基础设施 | 鼠标捕获/屏蔽/还原时序，阻止点击穿透到世界；`SetControls` 禁使用键 | `SoulUI`（Capture/Begin/End/Restore） | 已验证 |
| `ZenithReborn/HeroPrefixCompat.cs` | internal 静态类 | 存档兼容 | 英雄模组前缀重建时的全量数据抢救缓存与空剑魂回填 | `SoulItem` 全部字段、`SoulItem.ApplyPrefix`、`SoulPlayer.PostUpdate` | 已验证 |
| `ZenithReborn/SoulLegacyMartialData.cs` | `SoulItem` 分部 | 存档兼容 | 旧版武技字段（充能/阶位）的存档与网络字段，注释明确不参与玩法 | `SoulItem.SaveData/LoadData/NetSend/NetReceive` | 已验证 |
| `ZenithReborn/GlobalUsings.cs` | 编译配置 | 存档兼容/工程 | 补齐 tModLoader 编译器缺失的 System 系列全局 using | 全项目 | 已验证 |
| `ZenithReborn/SlashZenithTest.cs` | ModItem | 遗留测试 | 复用天顶剑贴图的普通挥砍测试武器，木头 1 个合成，注释自述可删 | 无（全项目零引用） | 已验证【已删除/历史快照：已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】 |

**覆盖：32 / 32**

---

## 第三部分：枢纽文件专章

### 1. `ZenithReborn/SoulItem.cs`（489 行）—— 数据总线

**职责边界**：它是"剑魂这件物品"的唯一真相源：所有槽位内容、所有进度字段的落点、所有面板数值的出口。但它**不算伤害**（在 `SoulCombat.cs`）、**不算特性开关**（在 `SoulCurses.cs` 分部）、**不算容量公式**（在 `SoulProgress`/`SoulSupport`）——它只做聚合与协调。

**公开入口清单**
| 成员 | 行 | 谁在用（证据） |
|---|---|---|
| `Active` | 82 | `SoulSwingPlayer.cs:18`、`SoulBlessings.cs:45`、`SoulSupport.cs:43`、`OreFamily.cs` |
| `Supports` | 66 | `SoulSupport.cs:43`、`SoulCurses.cs:84` |
| `ItemAt(int)` | 67 | `SoulBlessings.cs:45`、`SoulArmoryPreview.cs:11`、`LivingArmament.cs:25`、`SoulUIArmoryDetails.cs:17` |
| `VisibleSlotIds` | 31 | `SoulUI.cs:273`、`SoulUIArmoryScene.cs:64`、`SoulUIEffects.cs:146`、`LivingArmamentPlayer.cs:15`、`SoulDomainUI.cs:34` |
| `VisibleSupportCount` | 30 | `SoulUI.cs:213`、`SoulUIArmoryBondCore.cs:15`、`SoulUIArmoryInteraction.cs:75` |
| `SlotItems()` | 76 | `SoulUI.cs:444` |
| `TrySwapHost` | 95 | `SoulUI.cs:278/288`、`SoulUIArmoryScene.cs:99`、`SoulDomainUI.cs:52/64`、`SoulArmoryPreview.cs:12` |
| `CanHost` | 92 | `SoulSupport.cs:14`、`SoulDomainUI.cs:42/50`、`SoulUIArmoryInteraction.cs:25/36`、`EnvironmentTooltips.cs:13` |
| `RefreshStats` | 387 | `AttachmentStory.cs:82`、`SlotProgression.cs:35` |
| `EmitBlade` | 444 | `SoulSwingPlayer.cs:53` |
| `BladeDamage` | `SoulCombat.cs:9` | `SoulItem.EmitBlade` |
| `SaveData/LoadData/NetSend/NetReceive` | 287/307/336/352 | 物品框架 + `ZenithReborn` 网络 |
| `HasLegacyExpansion` | 54 | `SoulUI.cs:40/343/430`、`SoulUIArmoryDetails.cs:79` |
| `Stored` | 61 | `SoulUILibrary.cs:16/52`、`HeroPrefixCompat.cs:52` |

**被谁调用**：上表 + `SoulTraits`（`soul.SupportCapacity`/`CombatWood`/`OreCollectionMask`）、`BondSnapshot`（间接经 `SoulSupport.FamilyItems`）、`SoulBlade`（`SourceSoul` 里经 `WoodCombat.FindSoul` 反查）。

**改动会影响哪些模块**：全部 8 个模块。它是唯一同时被 UI、战斗、存档、网络、图鉴读取的类型。

**已知历史包袱**
1. **职责超载**：一个类同时管槽位编号算术、容量派生、重铸快照、存档版本、网络协议、工具提示。`SlotItems()`（`:76`）与 `VisibleSlotIds`（`:31`）是两套并行的"该画哪些槽"逻辑（前者给背包式界面，后者给配阵/渲染）。
2. **三套"支援/奖励"数组并存**：`SupportHosts[11]`（前 5 有效、后 6 是 0.3.9/0.3.10 遗留）、`ForestHosts[2]`（V2 起停用但保留取回）、`CrownHosts[2]`（V2 新增）、`AdditionalSupports`（动态）。`AllSupportSlots` 只 `Take(5)`，这是刻意的存档兼容而非疏漏。
3. **兼容别名**：`WoodSupportCapacity`（`:50`）注释直言"仅保留兼容别名"；`HasLegacyExpansion`、`reforgeYujian` 等字段同理。
4. **重铸/前缀两套快照字段**：`reforge*`（`:213-223`）与 `HeroPrefixCompat` 的静态缓存重复实现同一件事，后者是前者的补丁，两者字段列表必须人工保持同步（`HeroPrefixCompat.cs:27-46` vs `SoulItem.cs:224-250`）。

---

### 2. `ZenithReborn/SoulCurses.cs`（121 行）+ `SoulCurseDescriptions.cs`（41 行）—— 规则中枢

**职责边界**：把"打到过哪些 Boss"翻译成"六条能力轴上的等级"，再翻译成具体数值（把数/命中数/穿地/射程下限）。它是**纯函数式规则层**，不持有运行时状态（状态在 `SoulItem.Seals`）。

**公开入口清单**：`BossSeal`（`:24`）、`Count`（`:31`）、`RecordedStage`（`:32`）、`StageForCount`（`:37`）、`Stage`（`:38`）、`Blades`/`BladesAtStage`（`:55`/`:56`）、`HitLimit`（`:59`）、`TerrainAllowance`（`:62`）、`ScreenEdge`（`:46`）、`FromOldKills`（`:65`）、`SoulItem.Curse`（`:79`）、`TraitEnabled`（`:82`）、`CombatWood`（`:87`）、`SoulTerrain.Sweep`（`:109`）。

**被谁调用（15 个文件）**：`SoulItem.cs`、`SoulBlade.cs`、`SoulSwingPlayer.cs`、`SoulBlessings.cs`、`SoulTraits.cs`、`SoulBossUnlocks.cs`、`SoulBossPortraits.cs`、`Progression.cs`、`CrossSummon.cs`、`WoodCombat.cs`、`FlyingBladeHand.cs`、`SoulUIArchive.cs`、`SoulUIBossUnlocks.cs`、`SoulUICurseUnlocks.cs`。

**改动会影响哪些模块**：战斗（把数/命中/穿地）、祝福（接力/归鞘/破障的开关）、图鉴（6 条诅咒条目）、Boss 解锁页、跨召唤防重复、木质族主剑限制（`OreFlightMains`）。

**已知历史包袱**
1. **两套 Boss 位映射**：`SoulCurses.BossSeal`（18 位）与 `SoulProgress.BossBit`（12 位）由不同文件维护；`Queen/Empress/Duke/Deer` 只存在于诅咒位、不在进度位，因此打它们只解诅咒不给槽位 —— 这是设计但极易误判为 bug。
2. **旧档兼容抬升**：`Stage`（`:38`）会在 `LegacyReleased` 置位时把等级强行抬到 `Released`，但 `RecordedStage` 保持真实值；`Description`（`SoulCurseDescriptions.cs:9-10`）会追加"旧档能力兼容解咒"字样。任何 UI 若误用 `Stage` 会显示假进度。
3. **文案与数值双源**：`BladesAtStage` 在解除态用 `Seals&Moon/Cultist/Temple/Flower` 单独判断（`:58`），与 `StageForCount` 的组计数体系不同源。
4. `SoulTerrain` 与 `SoulBlade` 的 `terrainArmed`（`SoulBlade.cs:216`）耦合：只有在离开实体地形后才开始累积深度，这个"武装"状态没有注释说明原因。

---

### 3. `ZenithReborn/SoulBlade.cs`（521 行）—— 战斗执行器

**职责边界**：单把飞剑的完整生命周期（发射配置 → 轨迹 → 剑影历史 → 命中判定 → 伤害修改 → 命中后派发 → 死亡绘制）。它是"表现"与"判定"共用一个数据源的地方，也是最容易产生客户端/服务端分歧的地方。

**公开入口清单**：`SoulSource`（`:14`，字段 `SoulId/SourceWeapon/PrimaryOrdinal/HostSlot/LeashRadius`）、`SoulBlade.ConfigureReborn`（`:81`）、`Retire`（`:101`）、`TryConsumeOreHit`（`:103`）、`Retired`（`:100`）、`BodyCanDamage`（`:140`）、`CanDamage`（`:141`）、`TryGetEchoPose`（`:150`）、`CanHitNPC`（`:160`）、`ResolveHitUnit`（`:268`）、`BodyCollides`（`:277`）。

**被谁调用**：`SoulItem.EmitBlade`（写 `TargetOffset/SwordScale/Vertical/SpecialDamage/Blessings.ReturnArmed`，并写 `SoulSource` 四个字段）、`SoulSwingPlayer`（间接）、`SoulBlessings.BlessingPlayer.ValidSource/Host`（读 `SoulSource`）、`SoulBossCredit.ReceiveCredit`（用 `SoulSource.SoulId` 找参与者）、`SoulTrailEnding`/`SoulMaterialTrail`/`SoulRibbonMesh`（绘制）、`SoulZenithEffects.EmitFlying`（粒子）。

**改动会影响哪些模块**：战斗数值（`SoulEchoDamage`）、木质族（`WoodCombat`/`WoodBond*`）、矿石族（`HardOrePlayer`/`OreFamily`）、神圣天堂（`HolyHeavenProc`）、祝福（接力/归鞘/破障）、飞刃族（`LeashRadius`）、Boss 授勋（`SoulSource` 身份）。

**已知历史包袱**
1. **一个类干五件事**：运动学、历史缓冲、碰撞、伤害、绘制。`trailCenters/trailRotations/trailPhases/trailDirections` 四个平行数组靠 `trailCount` 手工维护（`:246-253`）。
2. **刻意保留的服务端重算**：注释 `:244` 明确"服务器同样保留实际姿态历史"，因为剑影碰撞不能依赖客户端绘制；删掉这段会让联机剑影失效。
3. **12 处外部模块的命中后直调**（`:307-331`）：`WoodCombat.FinishPrimaryHit`、`WoodBondForge/Rot/Ember`、`HardOrePlayer.Hit`、`OreFamily.OnMainHit`、`HolyHeavenProc.OnMainHit`、`Blessings.Hit`。这是**隐式优先级链**，没有统一事件总线，新增族必须在这里插入且注意顺序（例如祝福在最后、木质羁绊在最前）。
4. **特技释放的硬编码清单**（`:382-391`）：`Starfury/StarWrath/BeeKeeper/Seedler/TheHorsemansBlade` 写死在 `ReleaseWeaponSpecial` 里，与 `SoulTraits.cs:101-116` 的描述表是两份手工同步的清单。
5. `VfxIsolation.*` 开关（`:240`、`:501-518`）是特效调试闸门，散布在正常运行路径里。

---

### 4. `ZenithReborn/SoulSupport.cs`（54 行）—— 奖励池算法核心

**职责边界**：回答唯一一个问题"这把剑魂现在有几格支援"。它不存数据，只按当前寄宿内容做不动点迭代。

**公开入口清单**：`Grants`（`:10`）、`MaximumGrants`（`:13`）、`CanSupport`（`:14`）、`DistinctCount`（`:15`）、`Capacity`（`:16`）、`SetItems`（`:38`）、`FamilyItems`（`:43`）、`UnlockCount`（`:44`）、`GrantedBy`（`:45`）、`Breakdown`（`:53`）。

**被谁调用（11 个文件）**：`SoulItem`（`SupportCapacity`/`WoodSupportCapacity`/`TrySwapHost`）、`SoulCurses`（`CombatWood`）、`SoulTraits`、`BondSnapshot`、`SlotProgression`（`SlotHint`）、`WoodTypes`、`WoodCrown`、`WoodBondCore`、`ForestPower`、`FlyingBladeFamily`、`SoulUIWoodPanel`。

**改动会影响哪些模块**：支援格数 → `SoulItem.TrySwapHost` 的放行条件 → `Active`/`Supports` → 全部族计数与羁绊显示 → 图鉴文案。

**已知历史包袱**
1. **双口径陷阱**：`Capacity`（`:32`）里 `g.Id=="wood" ? items : roster`，木质只看 `items`（不含奖励位），矿石看 `roster`（含 `ForestHosts`+`CrownHosts`）。注释解释了"防自举"，但这是最容易改错的一行。
2. **不动点迭代 + 上限**（`:26`）：`for(attempt=0; attempt<=MaximumGrants; attempt++)`，正常 2-3 轮收敛；`MaximumGrants` 是唯一的循环保险。
3. **`UnlockCount` 是死代码**：全项目仅自身定义处出现（grep 确认），注释却把它当作设计约束说明；`DistinctCount` 只服务于它。
4. `FamilyItems` 与 `Active ∪ Supports` 等价，但注释强调"支援解锁与种族授予不得使用它"——语义靠注释维系。

---

### 5. `ZenithReborn/Progression.cs`（205 行）+ `SlotProgression.cs`（109 行）—— 进度双分部

**职责边界**：`Progression.cs` 负责"位定义 + 运行时（冷却/参与记录/授勋/魂芯物品）"；`SlotProgression.cs` 负责"门槛表 + 补办 + 面向玩家的提示文案"。两者通过 `static partial class SoulProgress` 合并。

**公开入口清单**：`SoulProgress.Has/Mechs/Milestones/Capacity/BossBit/Next`（`Progression.cs:19-49`）、`SoulProgress.SlotGates/CoreKills/CanAcceptCore/AcceptCore/RecoverableCore/Missing/SlotHint/RequestCoreRecovery/RecoverCore`（`SlotProgression.cs:12-108`）、`SoulPlayer`（`:52`）、`SoulEncounters`（`:92`）、`SoulBossCredit`（`:105`）、`SoulCore`+6 子类（`:162-205`）。

**被谁调用**：`SoulItem`（`Capacity`/`Stage`/`Awakened`/`ZenithWeapons`）、`SoulBlade`（`ZenithWeapons`）、`AttachmentStory`（`Next`）、`HeroPrefixCompat`（`All`）、`SoulUI`/`SoulUIArmoryDetails`/`SoulUIEffects`/`SoulUIMemories`/`SoulZenithEffects`/`SoulCurses`、`ZenithReborn.HandlePacket`。

**改动会影响哪些模块**：槽位解锁、觉醒判定（`Cores==63` 且十把天顶材料剑齐）→ `Potency` → 所有伤害；魂芯物品；史莱姆王的"下一步"台词；配阵详情页。

**已知历史包袱**
1. `SoulProgress.All = 4095` 是 12 位，`Kills` 与 `Cores` 在 `LoadData`/`NetReceive` 都做 `& SoulProgress.All` / `& 63` 截断（`SoulItem.cs:311`、`:354`），越界字段被静默丢弃。
2. `Capacity` 用"逐门槛 `break`"实现，隐含**单调递增**假设：不能出现"后门槛比前门槛容易"的表。
3. `SoulEncounters.Hits` 是 `static` 全局字典，`OnWorldLoad/Unload` 清空，但**不按玩家断开清理**；`OnKill` 用 `inventory[0..57]` 线性查找剑魂。
4. `SoulCore.Target`（`:177`）优先取"当前打开的剑魂"，导致魂芯使用目标依赖 UI 状态 —— 这是有意的便利，但使 `UseItem` 非纯函数。
5. `SlotHint`（`SlotProgression.cs:59`）与 `Missing`（`:47`）内容重叠，都面向 UI；改文案需要同时改 `SoulTraits` 的对应条目。

---

### 6. `ZenithReborn/SoulBlessings.cs`（257 行）—— 祝福运行时

**职责边界**：四条祝福（除独行、断锋外）的战斗侧实现 + 御剑爆发条。它是唯一在 `ModPlayer` 里持有跨帧计时器的模块。

**公开入口清单**：`BlessingTuning.Blessed`（`:21`）、`BlessingPlayer.Held/BurstFor/ValidSource/RecordCombat/TryActivate/RelayPower/CommitRelay/StoreReturn/ConsumeReturn/DistanceMultiplier`、`BladeBlessings.Prepare/CrossedSolid/CompleteReturn/Hit`、`BlessingStrike.Spawn`、`BlessingInterface`（缩放 + 御剑条）。

**被谁调用**：`SoulItem.Reach`（`BurstFor` → ×2）、`SoulSource.ModifyHitNPC`（`DistanceMultiplier`）、`SoulBlade`（`Blessings.Prepare/Hit/CrossedSolid/CompleteReturn`、`TraitPower`）、`SoulItem.EmitBlade`（`ConsumeReturn`）、`ZenithReborn.HandlePacket` action 10、`SoulCurses.HitLimit` 的祝福分支。

**改动会影响哪些模块**：射程（御剑 ×2）、所有命中伤害（距离倍率 + 接力 + 归鞘斩 + 破甲）、霜火/中毒时长（`TraitPower`）、界面缩放（`ModifyTransformMatrix`）。

**已知历史包袱**
1. **蓄力条存在 SoulItem 里**：`soul.YujianCharge` 由 `SoulItem` 序列化（`SoulItem.cs:297/325/348/366`），但由 `BlessingPlayer` 推进 —— 跨两个类的单一状态。
2. **联机三处手写同步**：`SyncCharge`（`:62`，靠 `SyncEquipment` 整物品同步）、`SyncBurst`（`:68`，包 10）、`Receive`（`:75`，服务端二次广播）。这是最容易漏改的地方。
3. `BladeBlessings` 是**每弹幕实例**（`SoulBlade.Blessings`，`SoulBlade.cs:79`），但 `BlessingPlayer` 的状态是**每玩家**；接力字典的 key 是 `(SoulId, 目标 root id)`（`:105`）。
4. `DistanceMultiplier` 用 `ScreenEdge` 作分母（`:124`），只因祝福后射程为 `1e6` —— 这是被射程改动倒逼出来的补丁（注释 `:122` 有说明）。
5. `BlessingStrike` 的绘制调用 `OreSwordEffects`（`:217-225`），祝福模块因此对矿石族视觉层有硬依赖。

---

### 7. `ZenithReborn/BondSnapshot.cs`（150 行）—— 三处 UI 的共同契约

**职责边界**：纯只读。把两个族的"数量"翻译成阶段号/进度/颜色，不含任何玩法判定。

**被谁调用**：`SoulUIArmoryBondCore.cs:11/89`（核心环）、`SoulUIArmoryDetails.cs:49`、`SoulUIWoodPanel.cs:44`。仅此三处。

**改动会影响哪些模块**：只有配阵页视觉。但它是"数量口径"的**第三套实现**（`FamilyItems` 计数 vs `SoulItem.Active` vs `SoulCurses.CombatWood` 的 `TraitEnabled` 过滤），三者数值可能不同。

**已知历史包袱**
1. `BondState.NextThreshold`、`PrevThreshold` 两个属性**全项目零外部引用**（grep 确认，仅自身定义处出现），是早期"显示下一档门槛"设计的残留；`Active`/`StageLine`/`StageName`/`IsMax`/`Progress` 均有实际使用（`SoulUIWoodPanel.cs:74`、`SoulUIArmoryDetails.cs:50`、`SoulUIArmoryBondCore.cs:13/76`）。【已删除/历史快照：上述两个属性已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】
2. `Build`（`:74`）的 `Math.Clamp(count,0,max)` 会隐藏超额：木质超过 8 种时 `Count` 被截断为 8，UI 无法显示实际持有数。
3. `BondSnapshot.Wood` 的注释（`:61-62`）明确"不受切换诅咒过滤"，而 `SoulCurses.CombatWood`（`SoulCurses.cs:87`）**受** `TraitEnabled` 过滤 —— 同一屏内两个数字不一致属预期。
