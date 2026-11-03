# B 组：族系统调查报告（木质 / 矿石 / 飞刃 / 神圣天堂 / 剑灵 / 环境属性 / 族归属标签）

> 调查范围：`ZenithReborn/` 根目录 39 个 `.cs` 文件（团队下发的清单实际为 39 项，非 38）。
> 全部结论来自实际读取源码，含未提交的木质族 V2 改动（当前有效状态）。
> 引用格式 `文件:行号` 均指当前工作区磁盘内容。

---

## 0. 阅读前必须知道的 4 个前提

1. **族计数只有一个真源**：`SoulSupport.FamilyItems(soul) => soul.Active.Concat(soul.Supports)`
   （`SoulSupport.cs:43`）。`SoulItem.Active` 的定义是
   `Hosts.Take(Capacity) ∪ ForestHosts.Take(BonusCapacity) ∪ CrownHosts.Take(CrownCapacity) ∪ LegendarySlots`
   （`SoulItem.cs:82-84`），因此 **Active 天然包含 森林奖励位、圣林位、传奇位**。
2. **"族内计数" ≠ "支援格解锁计数"**。后者用 `SoulSupport.SetItems`（只有普通槽 + 支援槽，
   `SoulSupport.cs:38-39`），刻意排除奖励位与传奇位（`SoulItem.cs:19` 注释：否则传奇位白送支援格）。
   两条口径在同名族里并存，是本模块最容易踩的坑。
3. **木质族有第二套计数**：`SoulItem.CombatWood`（`SoulCurses.cs:87`）在 `FamilyItems` 之上再叠
   `TraitEnabled` 过滤（切换诅咒未解时只认第一把主剑）。战斗强度 `S`、森林之力层数、木质羁绊全部吃
   `CombatWood`；`WoodTypes.Evaluate`（`WoodTypes.cs:51-57`）不过滤 `TraitEnabled`，目前除
   `SoulItem.Wood`（`SoulItem.cs:46`）外**无战斗调用点**，属遗留入口。
4. **所有族命中都从 `SoulBlade.OnHitNPC` 一处扇出**（`SoulBlade.cs:294-332`）：
   `WoodCombat.FinishPrimaryHit` → 木质羁绊 A/B/C 派发 → `HardOrePlayer.Hit` →
   `OreFamily.OnMainHit` → `HolyHeavenProc.OnMainHit` → `Blessings.Hit`。

---

## 第一部分：模块级说明

### 1. 木质族（Wood，8 把）

**核心机制**：把 8 把原版木剑放进剑魂的任意有效槽，按"已收集种类数（1/2/4/6/8）"给出
环境倍率 `S`（1.10/1.20/1.35/1.50/2.00，圣林归冠覆盖为 3.00）与 +5/+10/+15/+25/+40 生命；
每种木剑绑一条命中补丁（森息/霜寂/盐蚀/瘴毒/蚀枯/血馥/烬燃/净辉）；8 种齐全后开"森林之力"成长，
命中积层换伤害/速度/防御/回复/DoT/减伤，并解锁 4 组复合羁绊。

**名册来源（真实数量 8）**：`WoodTypes.Types`（`WoodTypes.cs:29-31`），
`[ItemID.WoodenSword, BorealWoodSword, PalmWoodSword, RichMahoganySword, EbonwoodSword, ShadewoodSword, AshWoodSword, PearlwoodSword]`。
满套判定 `WoodLoadout.Full => Mask == 0xFF`（`WoodTypes.cs:17`）。仙人掌剑已退出名册
（`WoodTypes.cs:8` 注释），改为 `BladeFamily.Solid`。

**关键类/方法/入口**
- `WoodTypes`（`WoodTypes.cs:26`）：`Types/Names/Effects` 三张平行表（29-36）、`Index`（37）、
  `IsWood`（38）、`Mask`（41-50）、`Evaluate`（51-57）、`Description` 八条文案（60-74）。
- `WoodLoadout`（`WoodTypes.cs:14`）：`Count`（16）、`Full`（17）、`Strength`（20-21）、`Life`（23）。
- `WoodPlayer`（`WoodPlayer.cs:8`）：成长字段（12-16）、`HeldSoul`（19）、`Loadout`（20）、
  `EffectiveLayers`（22）、`Ready`（24-30）、`AdvanceGrowth`（35-43）、`PreUpdateBuffs`（44-52）、
  `PostUpdateEquips`（57-64，生命/防御/减伤）、`UpdateLifeRegen`（65-77）、`BoostSoulHealing`（78-83）、
  `HealFromSoul`（85-93）、`GrantHitBuffs`（94-100）、`SyncPlayer`（103-110，包 action 4）、
  `ReceiveGrowth`（111-117）。
- `ForestPower`（`ForestPower.cs:12`，**同时是 ModBuff 类**）：`BaseCap=6`（14）、`CrownCap=12`（15）、
  `FramesPerLayer=300`（16）、`CombatGrace=300`（17）、`DecayInterval=120`（18）、
  对外查询 `Layers/DamageBonus/SpeedBonus/DefenseBonus/RegenBonus/DotBonusPercent/DamageReduction`（21-33）、
  `FullSet`（44）、`Advance`（47-70）、`RecordHit`（72-78）、`Reset`（80-84）。
- `WoodCombat : GlobalNPC`（`WoodCombat.cs:43`）：八特性注释总表（28-42）、
  `WoodContribution`（11-25）、`Apply`（108-143）、`ModifyPrimaryHit`（152-166）、
  `PostAI` 霜寂减速（211-225）、`UpdateLifeRegen` DoT（230-259）、`SendExtraAI/ReceiveExtraAI`（260-285）。
- 复合羁绊判定 `WoodBondCore`（`WoodBondCore.cs:29-61`）：A 锻根共生 `ActiveForge`（29-36）、
  B 腐林凋蚀 `ActiveRot`（39-46）、C 灰烬繁生 `ActiveEmber`（49-56）、D 圣林归冠 `ActiveCrown`（61）。
  命中上下文 `WoodBondHit`（`WoodBondCore.cs:71-83`）。
- 执行侧：`WoodBondForge`（锻芽，`WoodBondForge.cs:20-107`）、`WoodBondRot`（凋蚀释放，`WoodBondRot.cs:18-65`）、
  `WoodBondEmber`（焚叶，`WoodBondEmber.cs:20-73`）、多人中继 `WoodBondRelay`（`WoodBondForge.cs:114-175`，action 12）。

**关键数据流（挥臂 → 判族 → 计数 → 加伤 → 生成弹幕）**
1. 命中判定：`SoulBlade.OnHitNPC`（`SoulBlade.cs:294`）→ `WoodCombat.FinishPrimaryHit`（`SoulBlade.cs:307`）
   → `WoodPlayer.RecordHit`（`WoodCombat.cs:76`）→ `ForestPower.RecordHit`（`ForestPower.cs:72-78`，写 `RecentCombat=300`）。
2. 伤害修正：`SoulBlade.ModifyHitNPC`（`SoulBlade.cs:333`）→ `WoodCombat.PreparePrimaryHit`（339，施加八条标记）
   → `WoodCombat.ModifyPrimaryHit`（340，霜寂 +1%×S、净辉 +1.5%×S、盐蚀穿甲 +2×S）。
3. 复合羁绊：`SoulBlade.cs:314-327` 按 `SoulSource.SourceWeapon` 三路短路派发
   （铜/铁阔剑→A，魔光/永夜→B，火山→C）。派生伤害一律 `NPC.StrikeNPC(HitInfo)` 直伤，不回灌管道。
4. 弹幕生成：木质族**自己不产生弹幕**，全部作用于既有 `SoulBlade` 与 DoT 通道；唯一新弹幕来自
   `EnvironmentEffect`（环境系统）与 `HardOreShot`（矿石系统）。

**与其他模块的关系**
- `SoulItem`：`CombatWood`（`SoulCurses.cs:87`）是唯一战斗快照；`CrownCapacity`（`SoulItem.cs:60`）反查
  `WoodCrown.Active`，形成"槽位数 ← 归冠 ← 槽位内容"的循环，靠 `WoodCrown.evaluating` 护盾（`WoodCrown.cs:18,24`）打破。
- `SoulSupport`：木质 5 档门槛 `[1,2,4,6,8]`（`SoulSupport.cs:11`）用于**开支援格**，与族内计数不同口径。
- `SoulCurses`：`TraitEnabled`（`SoulCurses.cs:82`）决定 `CombatWood` 里哪些木剑算数（切换诅咒未解 = 只认首把）。
- `SoulTraits`/`SoulUIWoodPanel`：羁绊与木剑条展示复用 `WoodBondCore`/`FamilyItems`（`SoulTraits.cs:37-43`、`SoulUIWoodPanel.cs:22-27,41`）。
- `BondSnapshot.cs`：另一处独立读取 `WoodTypes.Mask(SoulSupport.FamilyItems(soul))`（:63）。
- `EnvironmentPlayer`：`HealingMultiplier`（`EnvironmentPlayer.cs:67`）乘进 `WoodPlayer.BoostSoulHealing`（`WoodPlayer.cs:82`）。

**常见问题定位入口（"木质族没生效"）**
1. 8 位是否齐：`WoodTypes.Mask(...) == 0xFF`（`WoodTypes.cs:17`）——注意奖励位/圣林位/传奇位都算。
2. 是否被切换诅咒压制：`CombatWood` 的 `TraitEnabled` 过滤（`SoulCurses.cs:87`），表现为"伤害只有第一把生效"。
3. 森林之力不涨：`WoodPlayer.AdvanceGrowth` 在 `HeldSoul == null` 时直接 return（`WoodPlayer.cs:41`）；
   `EffectiveLayers` 还要求 `CurrentSoul == soul.SoulId`（`WoodPlayer.cs:22`）。
4. 羁绊不触发：`WoodBondCore` 三个额外条件（棕榈/铜铁阔剑、红木+乌木+魔光+永夜、红木+白蜡+火山）任一缺失即 false。
5. 归冠不开：需 `TerraBlade` 落在**非 CrownHosts** 的有效槽（`WoodCrown.cs:33-36`），圣林槽自举被重入护盾拦掉。

**修改影响提醒**
- 改 `WoodTypes.Types` 会同时改：满套位宽（`0xFF` 硬编码 3 处：`WoodTypes.cs:17`、`ForestPower.cs:44`、`WoodCrown.cs:29`）、
  `WoodKind` 枚举顺序（`WoodTypes.cs:10`）、`Names/Effects/Description` 三张表、`WoodCombat` 的 `Time/Power` 8 元数组（`WoodCombat.cs:16-18`）、
  存档同步位宽（`WoodCombat.cs:266,279` 固定 8 次循环）。**任何从 8 变动都要同步这 6 处**。
- 改 `WoodLoadout.Strength`/`Life` 会同时改战斗、UI 文案（`WoodTypes.cs:86`）、`ForestPower` buff 文本（`ForestPower.cs:98-101`）。

---

### 2. 矿石族（Ore，14 材质）

**核心机制**：14 种矿石材质（肉前 8 × 短剑+阔剑、肉后 6 × 剑）寄宿后，主剑命中会在目标周围
集结同材质的"百炼飞剑"环形阵列，凑齐后齐射；材质种类数决定伤害倍率、成员数与额外效果；
6 种肉后材质各有一条连击特性（快淬/续锻/导刃/绽金/硬解/环钛）；14 材质集满 = `OreMastered`，
开 2 个森林奖励位 + 每 300 帧一次"天空落剑"。

**名册来源（真实数量 14 材质 / 20 把剑）**
- 材质枚举 `OreMaterial`：14 项（`OreFamily.cs:8-12`）。
- `MaterialCount = 14`、`AllMaterials = (1<<14)-1`（`OreFamily.cs:16-17`）。
- 判定表 `MaterialSwords`：肉前 8 材质各 `[短剑, 阔剑]`、肉后 6 材质各 1 把，
  共 `8*2 + 6 = 22` 个 ItemID 分布在 14 行（`OreFamily.cs:43-59`）。
- `MaterialOf`（`OreFamily.cs:61-66`）线性扫描返回 0..13，`IsOre`（68）为 `>=0`。
- 注意 `HardOreTypes.Types` 只有 6 把肉后剑（`HardOreTypes.cs:11-12`），**不能**单独用作"矿石族"名册，
  `WeaponFamilyTags.cs:18-20` 已明确说明。

**关键类/方法/入口**
- `OreFamily`（`OreFamily.cs:14`）：`DefaultType/Names/Tints`（21-41）、`Mask`（70-80，按材质位或）、
  `PopCount`（82）、`MainDamageMultiplier`（83-84，2 材质 +5%、4 材质 +10%，走 `SoulCombat.BladeDamage`）、
  `TriggerEligible`（85-87）、`CanFlightTrait`（88，≥8 材质）、`Count`（91）、`OreItems`（93）、
  `Materials`（95-99）、`Representative`（101-109）、`DamageFactor`（111-112，1.5×1.05^(n-1)×(n≥10?1.5)×(mastered?1.3)）、
  `OnMainHit`（116-131）。
- 阵列编排 `OreSystem : ModSystem`（`OreFamily.cs:134-222`）：`Formation` 内部类（141-148）、
  `Release`（151-160）、`AddSword`（161-202）、`PostUpdateEverything`（203-218，超时释放 + 冷却清理）。
- 飞剑本体 `OreFlyingSword`（`OreFlyingSword.cs:11`）：`FlightSpeed`（36-37，含 `EnvironmentPlayer.Stat("FlightSpeed")`）、
  `TraitWindow`（38，≥8 且未尝试）、`FlightDistance`（39，560/840）、`FormationAngle`（43）、
  `SetFormationStacks`（26-31）、`AI`（146-211，含 `SetCount>=6` 折返第二趟）、`ModifyHitNPC`（219-226）、
  `OnHitNPC`（227-241）。
- 运动曲线 `OreFlightMotion`（`OreFlightMotion.cs:7-25`）：后拉 6 帧 22px、7 帧 0.28→2.5 倍加速。
- 六特性 `HardOreTypes`（`HardOreTypes.cs:9-34`）+ 状态机 `HardOrePlayer`（`HardOrePlayer.cs:18-168`）：
  连击计数与冷却（32-43）、`ValidTarget`（48-49）、`Modify` 硬解 +12% / 导刃标记消费（66-80）、
  `Hit` 连击阈值 `{Cobalt:3, Palladium:5, Mythril/Titanium:4, 其他:1}`（98-99）、
  `Heal`（125-138）、`Spawn`（139-167）。
- 附属弹幕 `HardOreShot`（`HardOreShot.cs:10-102`）：`ModifyHitNPC` 按 `FlightDamageShare` 折算防御（16-17）、
  `CanHitNPC` 锁定目标代际（40-46）、`AI` 飞向目标或绕发射者环绕（49-82）。
- 落剑 `OreSkyfall`（`OreSkyfall.cs:11-123`）：`DamageMultiplier = 12×1.3×(≥10 材质?1.5)`（26）、
  `Spawn`（27-38）、警告 30 帧（13）、`SourceValid` 要求 `OreMastered`（48-54）。
- 视觉/收尾（纯客户端）：`OreStackGrowth`（层数↔伤害）、`OreStackGlow`（像素增亮）、
  `OreSwordEffects`（光谱贴图 / 拖尾 / 光斑）、`OrePierceFeedback`（音效 + `DrawDust` 挂钩）、
  `OreWeaponEnding`（残影寿命 20/36 帧）。

**关键数据流（挥臂 → 判族 → 计数 → 加伤 → 生成弹幕）**
1. 主剑伤害：`SoulCombat.BladeDamage`（`SoulCombat.cs:9-10`）= `GetWeaponDamage × Potency × OreFamily.MainDamageMultiplier`。
2. 命中：`SoulBlade.OnHitNPC` → `HardOrePlayer.Hit`（`SoulBlade.cs:328`，连击特性）→
   `OreFamily.OnMainHit`（`SoulBlade.cs:329`）。
3. 生成条件链（`OreFamily.cs:118-126`）：服务端/非本人/非 SoulBlade/伤害 0 → 目标合法性 →
   `SourceWeapon` + `PrimaryOrdinal>=0` + `TriggerEligible` → 铜特性 `EchoStacking` 允许时忽略 `HitUnit` →
   `blade.TryConsumeOreHit`。
4. 阵列：`OreSystem.AddSword`（`OreFamily.cs:161`）→ 材质序列变化即释放旧阵 → `Projectile.NewProjectile(OreFlyingSword)`
   → `BindTarget`/`SetCount`/`DamageShare`/`Capture` → `WaitForVolley` → 达到 `materials.Length` 才 `Release`。
5. 成员伤害叠层：`OreStackGrowth.SetLayers`（`OreFamily.cs:194` 循环调用）→ `DamageShare` 同步。

**与其他模块的关系**
- `SoulItem`：`OreMastered`（`SoulItem.cs:40`）由 `RegisterOreCollection`（41-45，取 `Active ∪ Supports`）
   决定；`BonusCapacity = 2`（57）只在矿石圆满时开森林奖励位；`OreFlightMains`（`SoulCurses.cs:81`）
   在切换诅咒未解时截断为 `Active.Take(4)`。
- `SoulSupport`：矿石 6 档门槛 `[1,2,4,6,8,10]`（`SoulSupport.cs:12`）；授予口径额外并入森林/圣林位（`SoulSupport.cs:31-32`）。
- `WoodPlayer`：`HealFromSoul`（`WoodPlayer.cs:85-93`）是续锻唯一治疗通道，受治疗预算与 `Player.lifeSteal` 双重夹紧（`HardOrePlayer.cs:129-136`）。
- `EnvironmentPlayer`：`Stat("FlightSpeed")` 直接影响飞剑速度（`OreFlyingSword.cs:36-37`）。
- `WeaponFamilies`：`BladeFamily.Solid` 决定 ≥8 材质时的击退 ×1.2 与碰撞宽度 ×1.15（`OreFamily.cs:186-187`、`OreFlyingSword.cs:217`）。

**常见问题定位入口（"矿石飞剑不出现"）**
1. `TriggerEligible`（`OreFamily.cs:85-87`）：`PrimaryOrdinal<0`（非主命中）或 `Count==0` 直接 false。
2. 切换诅咒：`OreFlightMains` 只取前 4 把（`SoulCurses.cs:81`）→ 后续槽位即使寄宿也不触发。
3. `blade.TryConsumeOreHit(blade.HitUnit)`（`OreFamily.cs:126`）：`HitUnit>0` 且未解锁铜特性时被拒。
4. 材质序列不稳定：`AddSword` 发现 `!old.Materials.SequenceEqual(materials)` 会先释放旧阵（`OreFamily.cs:164-166`），表现为"永远凑不齐"。
5. 特性不触发：`HardOreTypes.Enabled` 要求 `soul.Active.Any(i => i.type == source)`（`HardOreTypes.cs:23-24`）
   —— **只放在支援槽的肉后矿石剑不会启用特性**，支援侧另走 `TraitSupports` 分支（`HardOreTypes.cs:20`）。

**修改影响提醒**
- 改 `MaterialCount`/`OreMaterial` 顺序会同时改：`DefaultType/Names/Tints` 三表长度（21-41）、
  `AllMaterials` 位宽（17）、`SoulItem.OreCollectionMask` 存档字段语义、`OreStackGrowth.MaxStacks`（10）、
  `OreFlyingSword.ReceiveExtraAI` 的 `Math.Clamp(...,0,13)` 与 `Total` 上限 14（`OreFlyingSword.cs:120-121`）、
  `HardOreShot.GetAlpha/PreDraw` 的 `Tints[8+i]` 偏移（`HardOreShot.cs:48,85,94`）。**必须一起改**。
- `OreFamily.DamageFactor` 是唯一伤害公式入口，改它会同时影响成员伤害、`DamageShare` 防御折算与 `OreStackGrowth` 叠层上限。

---

### 3. 飞刃族（FlyingBlade，31 把）

**核心机制**：91 项纯挥砍剑中的 31 把（引擎核验无弹幕）组成族。装族内剑后按 0.3 秒/把在手里蓄能，
挥臂时**一次性掷出全部蓄能飞刃**（5° 扇面），并**取代**本次挥臂的本体飞剑。飞刃三阶段：
直飞 → 命中/满 600px 原地自旋 3 圈（旋转期 50% 伤害）→ 收回（默认无伤，第 4 阶保留 50%）。

**名册来源（真实数量 31）**：`FlyingBladeFamily.Weapons`（`FlyingBladeFamily.cs:19-29`）：
7 光剑 + 7 晶光刃 + `BloodButcherer/FieryGreatsword/Flymeal`（3）= 17，再加仅购买/掉落 14 把 = **31**。
注释声明来源为 `artifacts/flying-blade-candidates.json`（`FlyingBladeFamily.cs:6-8`）。

**阶级（门槛数组是真的数据源）**：`Stages = [1,2,4,6,8,10]`（`FlyingBladeFamily.cs:33`）：
- 1 把 `Activated`（57）→ 启用族；
- 2 把 `Tally`（58）→ `DamageBonus`（63-68），`+3%/把`、`StepCap=10`（37-38），最高 +27%；
- 4 把 `Pass`（59）→ 非本族主剑也能掷飞刃（`FlyingBladeHand.cs:37-39`）；
- 6 把 `Sigil`（60）→ 收回期保留 50% 伤害（`FlyingBladeShot.cs:31,66,92,104`）；
- 8 / 10 把门槛已定义但**当前无效果**（`FlyingBladeFamily.cs:15` 明示"内容尚未定案"）。

**关键类/方法/入口**
- `FlyingBladeFamily`（`FlyingBladeFamily.cs:17`）：`IsMember`（40）、`Count`（47）、`Stage`（50-55）、
  四个布尔门槛（57-60）、`DamageBonus`（63-68）、`Enabled`（70，另需 `TraitMains.Any()`）。
- `FlyingBladeHand : ModPlayer`（`FlyingBladeHand.cs:11`）：`Loaded/NextStack`（13-14）、
  `StackInterval=18`（15）、`PostUpdate` 蓄能（20-27，容量取 `SoulCurses.Blades(soul)`）、
  `Launch`（32-67，本地生成、扇面展开、写入 `ai[0]=host.type`、`ai[1]=sigil`）。
- `FlyingBladeShot`（`FlyingBladeShot.cs:13`）：`Stage {Fly,Rotate,Return}`（21）、`ReturnHits`（31）、
  `LaunchSpeed=12/MaxRange=600/RotationFrames=45/RotationTick=8/RotationDamage=.5`（32-36）、
  `AI`（50-78）、`CanHitNPC`（92）、`OnHitNPC`（95-100）、`ModifyHitNPC`（101-105）。
- 互斥入口：`SoulSwingPlayer.Begin`（`SoulSwingPlayer.cs:26-27`）——`Launch` 成功则 `budget=0`，本体飞剑完全不发。
- 绘制：`FlyingBladeVisuals`（**不在本组范围**，`FlyingBladeShot.cs:12` 明确分工）。

**关键数据流**
挥臂（`SoulSwingPlayer.Begin`）→ `FlyingBladeHand.Launch` → `FlyingBladeFamily.Enabled` →
取宿主列表（`Pass ? Active : Active.Where(IsMember)`）→ 逐把 `soul.BladeDamage(Player,host) × DamageBonus` →
`Projectile.NewProjectile(FlyingBladeShot, ..., ai0:host.type, ai1:sigil?1:0)` → 清空 `Loaded`。

**与其他模块的关系**
- `SoulCurses.Blades`（`SoulCurses.cs:55-58`）是飞刃容量的唯一来源（2/3/4/…/10，按独行诅咒与印记）。
- `SoulSwingPlayer`：飞刃与本体飞剑单次挥臂互斥（`SoulSwingPlayer.cs:22-27`）。
- `SoulItem.BladeDamage`（`SoulCombat.cs:9`）→ 飞刃吃矿石材质倍率，但 **飞刃弹幕不携带 SoulId**
  （`FlyingBladeHand.cs:31` 注释），因此不吃祝福、环境、木质/矿石命中管线，也不触发任何族的命中特性。

**常见问题定位入口（"飞刃不出"）**
1. `Enabled`（`FlyingBladeFamily.cs:70`）需要 `Attached && TraitMains.Any() && Stage>=1`；
   切换诅咒未解时 `TraitMains` 只含首把主剑（`SoulCurses.cs:83`）。
2. `Loaded<=0`（`FlyingBladeHand.cs:34`）——刚换魂首个 0.3 秒窗口无飞刃。
3. `PostUpdate` 早退还清 `Loaded`（`FlyingBladeHand.cs:22-23`）：手持非剑魂物品会立刻归零。
4. 收回期打不动：`Sigil` 未达 6 把，`Projectile.damage` 被置 0（`FlyingBladeShot.cs:66`）。

**修改影响提醒**
- 改 `Weapons` 会影响：`WeaponFamilyTags.Of` 的"飞刃族"标签（`WeaponFamilyTags.cs:14`）、
  阶位 tooltip（`WeaponFamilyTags.cs:37-46`）、`EnvironmentTooltips` 的族阶位行（`EnvironmentTooltips.cs:48`）。
- 改 `Stages` 会同时改 `WeaponFamilyTags.ActivationHint` 的"下一阶"提示（`WeaponFamilyTags.cs:43-44`）与 `Stage()` 循环上限。
- 改 `StepCap` 会与主槽容量上限（10，`SoulItem.cs:47`）失配，注释已警告"会被当成数值 bug"（`FlyingBladeFamily.cs:35-36`）。

---

### 4. 神圣天堂族（HolyHeaven，8 把）

**核心机制**：族内 8 把"星光/神圣"系剑，只要有一把进入有效寄宿（`TraitItems`，受切换诅咒过滤），
主剑每次合法命中（300 帧一次/剑魂）从目标上方 600px 落一颗 `StarDamage=1000` 的流星，
流星命中或落地再生成 120px 半径冲击波（排除直击主体、按根 NPC 去重）。

**名册来源（真实数量 8）**：`HolyHeavenFamily.Weapons`（`HolyHeavenFamily.cs:9-10`）：
`PearlwoodSword, Starfury, Excalibur, TrueExcalibur, PiercingStarlight, StarWrath, SolarEruption, DayBreak`。
珍珠木剑**同时**属于木质族（`HolyHeavenFamily.cs:6` 注释、`WeaponFamilyTags.cs:14-17` 双标签）。

**关键类/方法/入口**
- `HolyHeavenFamily`（`:7`）：`IsMember`（11）、`Count`（12，`Distinct()` 按 type 去重）、`Enabled`（13）。
- `HolyHeavenProc : ModSystem`（`HolyHeavenProc.cs:9`）：`StarDamage=1000`（11）、`CooldownFrames=300`（12）、
  `LastFall` 字典（13）、`OnMainHit`（14-31），冷却键 `player:":"+SoulId`（23）。
- `HolyHeavenStar`（`HolyHeavenStar.cs:9`）：`CloneDefaults(FallingStar)`（15）、`OnHitNPC/OnTileCollide`
  → `Impact`（21-31）；`HolyHeavenImpact`（35-57，`Radius=120`，`timeLeft=2`，`struck` 去重）。
- 绘制：`HolyHeavenStarVisuals`（不在本组范围，`HolyHeavenStar.cs:20,29` 调用）。

**关键数据流**：`SoulBlade.OnHitNPC`（`SoulBlade.cs:330`）→ `HolyHeavenProc.OnMainHit` →
条件链（非服务端/本人/`SoulBlade`/`damageDone>0`/`ValidTarget`/`TraitEnabled`/`Enabled`）→ 冷却检查 →
`NewProjectile(HolyHeavenStar, 从 aim-600Y 向下 24px/帧)`。

**与其他模块的关系**：只依赖 `SoulBlade` 命中管线、`WoodCombat.FindSoul`、`HardOrePlayer.ValidTarget`、
`SoulSource.SoulId/SourceWeapon`。**不参与**木质/矿石状态机，也不进 `EnvironmentPlayer.Hit`。

**常见问题定位入口**：`TraitEnabled(source.SourceWeapon)`（`HolyHeavenProc.cs:22`）要求来源武器本身通过切换诅咒；
`HolyHeavenFamily.Enabled` 要求至少一把族成员在 `TraitItems` 里；冷却键按"玩家+剑魂"，换魂会重置。

**修改影响提醒**：`StarDamage` 是常量 1000（不吃任何倍率），改它等于全族线性改数值；
`HolyHeavenImpact` 的 120px 与 `timeLeft=2` 是"只炸一次"的实现方式，改 `timeLeft` 会导致重复结算。

---

### 5. 剑灵（LivingArmament，4 把）

**核心机制**：族内至少 1 把（附魔剑/飞刀/波涌之刃/无头骑士剑）+ 有有效出战主剑时，
为**每个正在出战的主攻槽**生成一个隐形召唤体 `LivingArmament`，自动索敌 900px 内最近目标并
用该槽位武器实际"挥砍"（通过 `LivingWeaponProxy` 伪造一个 Player 跑 `ItemCheck()`）。

**名册来源（真实数量 4）**：`LivingArmamentFamily.Members`（`LivingArmamentFamily.cs:9`），
文件注释自称"暂定候选名册"（:6），`SoulTraits.cs:83` 同样标注"暂定成员"。

**关键类/方法/入口**
- `LivingArmamentFamily`（`:7`）：`IsMember`（10）、`Count`（11）、`Enabled`（12）。
- `LivingArmamentPlayer : ModPlayer`（`LivingArmamentPlayer.cs:9-29`）：`PostUpdate` 按 `soul.VisibleSlotIds`
  找缺号召唤体并补生成（24-26），`ai[0]=slot`。
- `LivingArmament : ModProjectile`（`LivingArmament.cs:11-52`）：`SetDefaults` 关伤害（17）、`AI` 校验
  （20-27：持有同魂 / `Enabled` / `VisibleSlotIds.Contains(slot)` / `TraitMains` 含该 item）、
  索敌与站位（28-35）、`LivingWeaponProxy.Tick`（37-38）、`PreDraw` 用原版物品贴图 + 代理玩家挥臂姿态（40-51）。
- `LivingWeaponProxy`（`LivingWeaponProxy.cs:9-41`）：`[ThreadStatic] Current`（11）、`Active`（15）、
  `Matches`（27）、`Tick` 内临时替换 `Main.mouseX/Y` + `player.ItemCheck()`（28-40）。
- `LivingChannelProjectile` + `LivingChannelSystem`（`LivingChannelProjectile.cs:8-40`）：
  给 aiStyle 9/15/99（飞刀/链球/悠悠球类）的弹幕在 `On_Projectile.AI` 期间临时换上代理玩家与鼠标（22-39）。

**关键数据流**：`PostUpdate` → 检查既有召唤体（`owner + type + ai[0] + SoulId`）→ `NewProjectile` →
`AI` 每帧把 `Center` 向目标 `Lerp(.12)` → `proxy.Tick` → `ItemCheck` 走引擎发射原版弹幕。

**与其他模块的关系**：只用 `SoulItem.VisibleSlotIds`/`ItemAt`/`TraitMains`/`SoulSource`；
不读写木质/矿石/环境状态；召唤体本身 `CanDamage()=>false`（`LivingArmament.cs:17`），伤害全由原版弹幕产生。

**常见问题定位入口**：`Enabled` 需要 `TraitMains.Any()`（切换诅咒只留首把）+ `Count>0`；
`AI` 会在 `!ReferenceEquals(owner.HeldItem.ModItem, soul)` 时自杀（`LivingArmament.cs:23`）；
只有 `TraitMains` 里的槽位会出召唤体（`LivingArmamentPlayer.cs:18`），支援槽不出。

**修改影响提醒**：`LivingWeaponProxy` 是全局 `Main.player[owner]` 临时替换 + `[ThreadStatic]`，
任何在该窗口内读取 `Main.player[owner]` 的代码都会看到假玩家；改 `Tick` 必须保留 `finally` 还原
（`LivingWeaponProxy.cs:39`）与 `LivingChannelSystem` 的 `try/finally`（`LivingChannelProjectile.cs:31-38`）。

---

### 6. 环境属性系统（Environment，21 环境）

**核心机制**：21 个生物群系/地层环境构成与 `BladeFamily`/`WoodKind` **完全正交**的第二维度
（`EnvironmentAttributes.cs:9`）。每把可寄宿武器在 `RegisterBuiltins` 里静态绑定 1~N 个环境；
剑魂把已寄宿武器的环境登记为 `EnvironmentCollection` 位图；每种已收集环境按
`Shared(1+0.2×Count) × Forest(1.15) × 领域(1.5) × Adaptation(0.5/1)` 提供属性；
每收集 4 种得 1 个"常驻机制"名额（`BlessingSlots = Count/4`，4/8/12/16/20 → 1..5 个）。

**名册/范围（真实数量 21）**：`SwordEnvironment` 枚举 `None=0` + 1..21（`EnvironmentAttributes.cs:10-17`）；
`EnvironmentRules.All = (1<<21)-1`（`EnvironmentRules.cs:10`）；`EnvironmentAttributes.All` 21 条
`Define(...)`（`EnvironmentAttributes.cs:36-58`）；`MechanismNames` 22 项（含空串位，
`EnvironmentRules.cs:18`）；`stats` 21 组属性（19-29）；`Descriptions` 21 条（42-54）。

**关键类/方法/入口**
- `EnvironmentRules`（`EnvironmentRules.cs:8`）：`Bit`（12）、`Has`（13）、`Count`（14）、`BlessingSlots`（15）、
  `Shared`（16）、`Forest`（17）、`Values`（30-31）、`StatName`（32-38）、`StatsText`（39-41）、
  `Color`（55-60）、`Natural`（61-79，地形掩码，群系优先于深度）、`Adaptation`（80-92，互斥群系 ×0.5）。
- `WeaponEnvironmentAttributes`（`EnvironmentAttributes.cs:69`）：`ForType/For`（73-74）、
  `SetForType`（78-85）、`RegisterBuiltins`（87-124）—— **武器→环境的唯一手工绑定表**。
- `SoulItem` 分部（`EnvironmentRules.cs:95-120`）：`EnvironmentCollection`/`EnvironmentBlessings`/`DomainEnvironment`（97-98）、
  `RegisterEnvironments`（99-103）、`SetEnvironmentDomain`（104-105）、`ToggleEnvironmentBlessing`（106-112）、
  `NormalizeEnvironments`（113-119）。
- `EnvironmentPlayer : ModPlayer`（`EnvironmentPlayer.cs:11-214`）：`Strength`（29-34）、
  `Mechanism`（35-41，三级判定：祝福 → 领域半径 960 → 自然）、`RefreshEnvironmentStats`（42-48）、
  `PostUpdateEquips` 属性落地（49-57）、`PostUpdate`（68-91，藤叶蓄能/冰甲/蜜露/汲取/领域弹幕）、
  `ConsumableDodge` 叶甲免伤（96-102）、`ModifyHurt`/`OnHurt` 冰甲与受伤触发（103-121）、
  `Recover`（122-131）、`ModifyHit`（132-141）、`DiscoverNaturalKill`（142-148）、
  `Proc`（149-154）、`Hit` 21 环境机制总表（155-204）、`ReceiveHit`（205-213，包 action 11）。
- `EnvironmentTarget : GlobalNPC`（`EnvironmentPlayer.cs:216-241`）：`Mark`（219-223）、`Get/Find`（225-227）、
  `UpdateLifeRegen` 毒/流血 DoT（229-240）。
- `EnvironmentEffect`（`EnvironmentEffects.cs:14-124`）：`Spawn`（21-28）、8 种 `Shape` 的寿命/碰撞/绘制（29-123）。
- `EnvironmentDomain`（`EnvironmentEffects.cs:126-151`）：960px 领域环，纯视觉。
- `EnvironmentBuffHooks`（`EnvironmentPlayer.cs:243-252`）：`On_Player.AddBuff` 缩短减益。
- UI/Tooltip：`EnvironmentUI.DrawEnvironments`（`EnvironmentUI.cs:17-89`）、`EnvironmentGuide`（90-93）、
  `EnvironmentSwordTooltips`（`EnvironmentTooltips.cs:7-74`）。
- 注册：`EnvironmentAttributeSystem.Load`（`EnvironmentAttributes.cs:144-145`）。

**关键数据流**：武器 `Item` → `GetEnvironmentAttributes()`（`EnvironmentAttributes.cs:129`）→
`SoulItem.InspectEnvironmentAttributes()`（137-139，`Active ∪ Supports`）→
`RegisterEnvironments`（`EnvironmentRules.cs:99-103`）→ `EnvironmentCollection` →
`Strength`/`RefreshEnvironmentStats` → `PostUpdateEquips` 落地；命中侧 `EnvironmentPlayer.Hit` 按
`Mechanism(e, target.Center)` 逐环境触发。

**与其他模块的关系**
- `SoulItem`：`EnvironmentCollection` 是存档字段，`NormalizeEnvironments` 是唯一的合法化入口。
- `SoulBlade`：`SoulBlade.cs:29` 调 `EnvironmentPlayer.Hit`，`SoulBlade.cs:36` 调 `ModifyHit`。
- `WoodPlayer`：`WoodPlayer.BoostSoulHealing` 乘 `EnvironmentPlayer.HealingMultiplier`（`WoodPlayer.cs:82`）。
- `OreFlyingSword`：读 `EnvironmentPlayer.Stat("FlightSpeed")`（`OreFlyingSword.cs:36-37`）。
- `WeaponFamilyTags` / `SoulUILibrary`：`EnvironmentSwordTooltips.Label` 被图鉴复用（`SoulUILibrary.cs:61`）。

**常见问题定位入口（"环境机制不跑"）**
1. `Mechanism`（`EnvironmentPlayer.cs:35-41`）：必须满足「有祝福位」或「领域内且领域=该环境」或「自然掩码含该环境」之一；
   仅"已收集"只给属性，**不给机制**。
2. `SetForType` 未绑定 → `tags.Count==0` → tooltip 显示"未归类"，`Strength` 恒为 0。
3. `SetEnvironmentDomain` 会拒绝未收集的环境（`EnvironmentRules.cs:105`）；`NormalizeEnvironments` 会在收集位丢失时清领域（116）。
4. `EnvironmentBlessingSlots` 不足：`ToggleEnvironmentBlessing` 返回 false，UI 提示名额不足（`EnvironmentUI.cs:83`）。

**修改影响提醒**：新增环境必须同时改 `SwordEnvironment`（新增枚举值必须追加在 21 之后并调 `All` 位宽）、
`EnvironmentAttributes.All`、`MechanismNames`、`stats`、`Descriptions`、`Color`、`Natural`、`Adaptation`、
`EnvironmentIcons`（`EnvironmentUI.cs:13-16`，必须 1:1 对齐 22 项）、`EnvironmentUI` 的 `21` 硬编码（`EnvironmentUI.cs:21,23,45`）。
**这是全项目耦合点最多的手工同步表。**

---

### 7. 族归属标签（WeaponFamilyTags）

**核心机制**：武器"所属种族"显示的唯一数据源，只读、无缓存、任何界面可安全调用（`WeaponFamilyTags.cs:3-6`）。
输出顺序固定：飞刃族 → 神圣天堂族 → 剑灵 → 木质族 → 矿石族（`WeaponFamilyTags.cs:5,14-20`）。

**关键方法**：`Of(int)`（10-22）、`IsAny`（25）、`Line(int, SoulItem)`（29-34）、
`ActivationHint`（37-46，只对飞刃族输出阶位与"count/next"）。

**与其他模块的关系**：被 `EnvironmentTooltips`（`EnvironmentTooltips.cs:48`）、`SoulUI`（`SoulUI.cs:423`）、
`SoulUIArmoryDetails`（:44）、`SoulUILibrary`（:61）调用。**新增族必须只在这里加一行**（`WeaponFamilyTags.cs:9` 注释）。

**注意**：`Of` 不返回"剑灵以外的未实装族"，也不判断 `OreFamily` 的肉前/肉后差异
（`WeaponFamilyTags.cs:18-20` 解释为何不用 `HardOreTypes`）。

---

## 第二部分：文件索引表

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `ZenithReborn/EnvironmentAttributes.cs` | 静态注册表 + ModSystem | 环境属性系统 | 21 环境枚举、属性定义、武器→环境手工绑定表、`SoulItem` 环境视图 | `OreFamily`（矿石批量绑定）、`SoulItem`、`EnvironmentRules` | 已验证 |
| `ZenithReborn/EnvironmentEffects.cs` | ModProjectile ×2 | 环境属性系统 | `EnvironmentEffect` 8 种形状的机制弹幕 + `EnvironmentDomain` 960px 领域视觉环 | `EnvironmentPlayer.Hit`、`EnvironmentTarget` | 已验证 |
| `ZenithReborn/EnvironmentPlayer.cs` | ModPlayer + GlobalNPC + ModSystem | 环境属性系统 | 强度计算、21 环境机制、冰甲/叶甲/蜜露/汲取、DoT、命中网络回传、减益缩短钩子 | `SoulBlade`（Hit/ModifyHit）、`WoodPlayer`、`OreFlyingSword` | 已验证 |
| `ZenithReborn/EnvironmentRules.cs` | 静态规则 + 分发 | 环境属性系统 | 位运算、强度公式、21 项机制名/属性/文案/颜色、自然掩码、互斥克制；另含 `SoulItem` 环境分部 | `EnvironmentAttributes`、`SoulItem` 存档字段 | 已验证 |
| `ZenithReborn/EnvironmentTooltips.cs` | GlobalItem | 环境属性系统 / 族标签 | tooltip 插入"所属种族"行（带飞刃阶位）与"所属环境"折行 | `WeaponFamilyTags`、`FlyingBladeFamily`、`SoulUI.EditingSoul` | 已验证 |
| `ZenithReborn/EnvironmentUI.cs` | `SoulUI` 分部（UI） | 环境属性系统 | 环境图鉴网格 + 详情面板 + 领域/常驻/恢复自然/规则说明 4 个操作 | `SoulUI` 布局辅助、`SoulItem.SetEnvironmentDomain` | 部分待核实（布局辅助在 `SoulUI` 内，未在本组范围核对） |
| `ZenithReborn/FlyingBladeFamily.cs` | 静态名册 | 飞刃族 | 31 把名册、1/2/4/6/8/10 阶门槛、+3%×n 加伤、四档能力开关 | `SoulItem.Active/Supports`、`SoulCurses.Blades`、`WeaponFamilyTags` | 已验证 |
| `ZenithReborn/FlyingBladeHand.cs` | ModPlayer | 飞刃族 | 0.3 秒/把蓄能、挥臂齐射（5° 扇面）、与本体飞剑互斥 | `SoulSwingPlayer.Begin`、`SoulCurses.Blades`、`SoulItem.BladeDamage` | 已验证 |
| `ZenithReborn/FlyingBladeShot.cs` | ModProjectile | 飞刃族 | 三阶段飞刃（直飞/自旋/收回）、旋转期 50% 伤害、第 4 阶收回留伤 | `FlyingBladeVisuals`（外范围）、`FlyingBladeFamily.Sigil` | 已验证 |
| `ZenithReborn/ForestPower.cs` | ModBuff（兼静态契约） | 木质族 | 森林之力成长引擎（6/12 层上限、300 帧/层、脱战 -1/120 帧）、六项收益查询、buff 文案 | `WoodPlayer`、`WoodCrown`、`WoodCombat`、`SoulSupport.FamilyItems` | 已验证 |
| `ZenithReborn/HardOrePlayer.cs` | ModPlayer + GlobalNPC | 矿石族 | 肉后 6 材质连击状态机、导刃标记、硬解、续锻治疗、钛片/钴刃/绽金生成 | `SoulBlade`、`OreFamily`、`WoodPlayer.HealFromSoul`、`SoulCurses` | 已验证 |
| `ZenithReborn/HardOreShot.cs` | ModProjectile | 矿石族 | 特性附属弹幕（钴刃/绽金花瓣/钛片），按 `FlightDamageShare` 折算防御，锁目标代际 | `HardOrePlayer.Spawn`、`OreFlyingSword`、`OreWeaponEnding` | 已验证 |
| `ZenithReborn/HardOreTypes.cs` | 静态表 + GlobalItem | 矿石族 | 肉后 6 剑名册、六特性文案、`Traits/Enabled` 判定、tooltip | `OreFamily`（材质映射）、`SoulItem.Active/TraitSupports` | 已验证 |
| `ZenithReborn/HolyHeavenFamily.cs` | 静态名册 | 神圣天堂族 | 8 把名册与启用判定（`TraitItems` 去重计数） | `SoulCurses.TraitItems`、`WeaponFamilyTags` | 已验证 |
| `ZenithReborn/HolyHeavenProc.cs` | ModSystem | 神圣天堂族 | 主命中 300 帧冷却落星（`StarDamage=1000`） | `SoulBlade.OnHitNPC`、`HolyHeavenStar`、`HardOrePlayer.ValidTarget` | 已验证 |
| `ZenithReborn/HolyHeavenStar.cs` | ModProjectile ×2 | 神圣天堂族 | 原版落星复用 + 120px 冲击波（排除直击主体、根去重） | `HolyHeavenStarVisuals`（外范围）、`HolyHeavenProc.StarDamage` | 已验证 |
| `ZenithReborn/LivingArmament.cs` | ModProjectile | 剑灵 | 隐形召唤体：跟随/索敌/站位，驱动代理玩家挥砍，用原版物品贴图绘制 | `LivingWeaponProxy`、`LivingArmamentFamily`、`SoulItem.VisibleSlotIds` | 部分待核实（:10 注释称"玩家身体保持不可见"，本范围内无实现） |
| `ZenithReborn/LivingArmamentFamily.cs` | 静态名册 | 剑灵 | 暂定 4 把候选名册与启用判定 | `SoulCurses.TraitItems`、`WeaponFamilyTags` | 已验证 |
| `ZenithReborn/LivingArmamentPlayer.cs` | ModPlayer | 剑灵 | 为每个 `TraitMains` 出战槽补生成召唤体（只本地玩家） | `LivingArmament`、`SoulItem.VisibleSlotIds` | 已验证 |
| `ZenithReborn/LivingChannelProjectile.cs` | GlobalProjectile + ModSystem | 剑灵 | 给 aiStyle 9/15/99 弹幕在 `On_Projectile.AI` 期间临时替换玩家与鼠标 | `LivingWeaponProxy` | 已验证 |
| `ZenithReborn/LivingWeaponProxy.cs` | 普通类 | 剑灵 | 每槽独立假 Player；`ItemCheck()` 走引擎物品检查攻击 | `LivingArmament`、`LivingChannelProjectile` | 已验证 |
| `ZenithReborn/OreFamily.cs` | 静态表 + ModSystem | 矿石族 | 14 材质映射/位掩码/倍率、主命中触发链、飞剑阵列编排（`OreSystem`） | `SoulBlade`、`HardOrePlayer`、`OreFlyingSword`、`OreSkyfall`、`SoulSupport` | 已验证 |
| `ZenithReborn/OreFlightMotion.cs` | 静态曲线 | 矿石族 | 飞剑后拉/加速曲线与行程帧数（AI 与视觉回放共用） | `OreFlyingSword` | 已验证 |
| `ZenithReborn/OreFlyingSword.cs` | ModProjectile | 矿石族 | 飞剑本体：成型环列/齐射/折返二段/碰撞线检测/命中特效/网络同步 | `OreFamily.OreSystem`、`OreStackGrowth/Glow`、`HardOrePlayer` | 已验证 |
| `ZenithReborn/OrePierceFeedback.cs` | ModSystem（Client） | 矿石族 | 飞剑出场/命中音效、残影历史更新、`DrawDust` 后置绘制 | `OreWeaponEnding`、`OreSwordEffects` | 已验证 |
| `ZenithReborn/OreSkyfall.cs` | ModProjectile | 矿石族 | 矿石圆满专属"天空落剑"（警告 30 帧、12×1.3 倍率、≥10 材质 1.5×） | `OreSystem.AddSword`、`OreFamily.Count` | 已验证 |
| `ZenithReborn/OreStackGlow.cs` | 普通类（视觉） | 矿石族 | 飞剑像素增亮与光照增益，向层数缓动 | `OreFlyingSword.StackGlow` | 已验证 |
| `ZenithReborn/OreStackGrowth.cs` | 普通类（数据） | 矿石族 | 阵内层数↔伤害倍率（+5%/层，上限 14）、伤害快照、网络收发 | `OreSystem.AddSword`、`OreFlyingSword`、`SoulTraits` 文案 | 已验证 |
| `ZenithReborn/OreSwordEffects.cs` | 静态绘制 | 矿石族 | 光谱贴图生成与缓存、刀光/星光/光环/拖尾/余烬等纯客户端绘制 | `OreFlyingSword`、`HardOreShot`、`OreSkyfall`、`SoulBlessings` | 已验证 |
| `ZenithReborn/OreSwordTraits.cs` | 静态判定 | 矿石族 | 铜（回响叠层）/锡（阵内增伤）两条共用特性开关 | `OreFamily.MaterialOf`、`SoulItem.TraitItems` | 已验证 |
| `ZenithReborn/OreWeaponEnding.cs` | 静态绘制 + 数据类型 | 矿石族 | 武器消散残影（`OreWeaponEnd`/`OreAirFront`/64 条历史/粒子） | `OrePierceFeedback`、`OreSwordEffects.SpectralTexture` | 已验证 |
| `ZenithReborn/WoodBondCore.cs` | 静态判定 + 数据类型 | 木质族 | 四组复合羁绊判定（A/B/C/D）与 `WoodBondHit` 命中上下文 | `SoulSupport.FamilyItems`、`WoodCrown`、`SoulBlade` | 已验证 |
| `ZenithReborn/WoodBondEmber.cs` | 静态执行 | 木质族 | 灰烬繁生·焚叶：64px/30%×D/最多 3 目标/180 帧冷却 | `WoodBondCore.ActiveEmber`、`WoodCombat.HasToxinAndEmber`、`WoodBondRelay` | 已验证 |
| `ZenithReborn/WoodBondForge.cs` | 静态执行 + 中继 | 木质族 | 锻根共生·锻芽（3 层/180 帧/补斩 30%×D）+ A/B/C 共用中继协议（action 12） | `WoodBondCore.ActiveForge`、`WoodCombat.HasSalt`、`ZenithReborn.HandlePacket` | 已验证 |
| `ZenithReborn/WoodBondRot.cs` | 静态执行 | 木质族 | 腐林凋蚀·凋蚀释放：提前结算 25% DoT 预算、单次 ≤50%×D、180 帧冷却 | `WoodCombat.OwnDotBudget/ConsumeOwnDot`、`WoodPlayer.Ready` | 已验证 |
| `ZenithReborn/WoodCombat.cs` | GlobalNPC + ModBuff ×6 | 木质族 | 八木剑特性状态表（标记/DoT/减速）、伤害修正、网络同步、DoT 结算 | `SoulBlade`（Prepare/Modify/Finish）、`ForestPower`、`WoodBondCore` | 已验证 |
| `ZenithReborn/WoodCrown.cs` | 静态判定 | 木质族 | 圣林归冠：8 木 Full + 泰拉刃在非圣林槽；含防循环重入护盾 | `SoulSupport.FamilyItems`、`SoulItem.CrownCapacity`、`ForestPower` | 部分待核实（:27 注释称"不含森林/圣林/冠位奖励槽"，但 :29 实际用 `FamilyItems` 会包含，注释与实现不一致） |
| `ZenithReborn/WoodPlayer.cs` | ModPlayer + ModBuff ×2 | 木质族 | 成长字段与同步（action 4）、生命/防御/减伤/回复、治疗预算、`Ready` 冷却 | `ForestPower`、`WoodCombat`、`EnvironmentPlayer` | 已验证 |
| `ZenithReborn/WoodTypes.cs` | 静态表 + GlobalItem | 木质族 | 8 把名册、`WoodLoadout`（S/生命）、掩码、`Evaluate`、八条特性文案、tooltip | `SoulSupport.FamilyItems`、`WoodCrown`、`HardOreTypes`/`WeaponFamilies` 兜底文案 | 已验证 |

**覆盖统计：39 / 39 文件已实际读取并撰写职责；其中 2 个标记为"部分待核实"（`LivingArmament.cs`、`WoodCrown.cs`），
`EnvironmentUI.cs` 因依赖 `SoulUI` 布局辅助亦标"部分待核实"。合计 3 个待核实、36 个已验证。**

---

## 第三部分：族的计数口径对照表

> 槽位编号取自 `SoulItem.cs:23` 注释：
> `0-9` 普通主攻 / `10-14` 基础支援 / `15-16` 森林(全收集)奖励 / `17-24` 旧版取回（不生效） /
> `25+` 追加支援 / `34` 传奇 / `35-36` 圣林之冠。
> "含传奇位"判断依据：`SoulItem.Active` 末尾 `.Concat(LegendarySlots)`（`SoulItem.cs:83`），
> 而 `LegendarySlots` 仅在 `LegendaryOn`（= `Attached && SoulDifficulty.Legendary`）时非空（`SoulItem.cs:18-20`）。

| 族 | 计数表达式 | 文件:行号 | 计入哪些槽位 | 是否含传奇位 |
|---|---|---|---|---|
| 木质族（**族内计数**：强度/生命/森林之力/羁绊） | `PopCount(WoodTypes.Mask(SoulSupport.FamilyItems(soul)))`，`FamilyItems = Active ∪ Supports` | `WoodTypes.cs:56`；`SoulSupport.cs:43`；`SoulItem.cs:82-84` | 0-9 普通 + 10-14/25+ 有效支援 + 15-16（需 `OreMastered`，`SoulItem.cs:57`）+ 35-36（需归冠，`SoulItem.cs:60`）+ 34 | **是** |
| 木质族（**战斗强度 S**，实际生效值） | 同上再叠 `.Where(i => TraitEnabled(i.type))` | `SoulCurses.cs:87`（`CombatWood`）；`SoulCurses.cs:82` | 同上，但切换诅咒未解时**只保留第一把主剑** | **是**（首把若在传奇位则算，但 `TraitsEnabled` 会把它挤出"首把"语义，实际几乎不可能） |
| 木质族（**支援格授予**） | `GrantedBy("wood")` → `count` 命中 `[1,2,4,6,8]` | `SoulSupport.cs:11,45-52`；门槛数组 `SoulSupport.cs:11` | 仅 `SetItems` = 0-9 普通 + 10-14/25+ 有效支援（`SoulSupport.cs:38-39`） | **否**（刻意排除，见 `SoulItem.cs:19` 注释） |
| 矿石族（**族内计数**：飞剑数/倍率/落剑） | `PopCount(OreFamily.Mask(OreItems(soul)))`，按**材质种类**去重，上限 14 | `OreFamily.cs:91,93`；`OreFamily.cs:16` | 主攻（未解切换诅咒时仅 `Active.Take(4)`，`SoulCurses.cs:80-81`）+ 有效支援（受 `TraitEnabled` 过滤）+ 15-16 + 35-36 + 34 | **是**（`Active` 含 `LegendarySlots`） |
| 矿石族（**支援格授予**） | `roster = SetItems ∪ ForestHosts.Take(reward?2:0) ∪ CrownHosts.Take(CrownCapacity)` 命中 `[1,2,4,6,8,10]` | `SoulSupport.cs:12,31-32,50` | 0-9 普通 + 10-14/25+ 有效支援 + 15-16 + 35-36 | **否** |
| 矿石族（**收集/圆满** `OreMastered`） | `OreCollectionMask` 累积 `OreFamily.Mask(Active ∪ Supports)`，`== AllMaterials` | `SoulItem.cs:40-45` | 0-9 + 支援 + 15-16 + 35-36 + 34 | **是**（一次性写进永久掩码，卸下也不回退） |
| 飞刃族 | `Active ∪ Supports` 过 `IsMember` 后 `Count(i => ...)`（按把数，不去重） | `FlyingBladeFamily.cs:47` | 0-9 + 支援 + 15-16 + 35-36 + 34 | **是** |
| 神圣天堂族 | `TraitItems.Where(IsMember).Select(type).Distinct().Count()` | `HolyHeavenFamily.cs:12`；`SoulCurses.cs:83-85` | `TraitMains`（Active 经 `TraitEnabled`）+ `TraitSupports`；含 15-16/35-36/34 | **是**（同一 `TraitsEnabled` 注意点） |
| 剑灵 | 同神圣天堂（`TraitItems` + `Distinct`） | `LivingArmamentFamily.cs:11` | 同上 | **是** |
| 环境（**收集位**） | `EnvironmentCollection` 位图，来自 `InspectEnvironmentAttributes()` 即 `Active ∪ Supports` | `EnvironmentRules.cs:99-103`；`EnvironmentAttributes.cs:137-139` | 0-9 + 支援 + 15-16 + 35-36 + 34 | **是** |
| 环境（**常驻名额**） | `BlessingSlots = Count(EnvironmentCollection) / 4` | `EnvironmentRules.cs:15` | 由收集位派生（4/8/12/16/20 → 1..5） | 间接（收集位来源含传奇） |

**口径要点（维护必读）**
1. **含传奇位的族**：木质（族内计数）、矿石（族内计数 + 收集）、飞刃、神圣天堂、剑灵、环境收集 —— 全部含。
2. **不含传奇位的族**：木质/矿石的**支援格授予**（`SetItems` 口径），以及 `SoulSupport.UnlockCount`。
   `SoulItem.cs:19` 的注释给出了唯一理由：否则传奇位会白送支援格。
3. **唯一不含任何奖励位的口径**是木质族的支援格授予；其它所有口径都把 15-16/35-36 当"有效寄宿"。
4. **只有矿石族按"材质种类"去重**（`OreFamily.Mask` 是材质位），因此 22 把矿石剑最多只能贡献 14 个计数；
   木质/飞刃按把数、神圣/剑灵按 `type` 去重。
5. **`wood` 与 `ore` 的门槛数组不同**：`[1,2,4,6,8]`（5 档）vs `[1,2,4,6,8,10]`（6 档），
   合计 `SoulSupport.MaximumGrants = 11`（`SoulSupport.cs:13`），这是支援槽自举循环的上界。
6. **存在循环依赖的唯一环**：`SoulItem.CrownCapacity` → `WoodCrown.Active` → `SoulSupport.FamilyItems` →
   `SoulItem.Active` → `CrownCapacity`。由 `WoodCrown.evaluating` 静态标志在重入时返回 false 破解
   （`WoodCrown.cs:18,24`），并顺带实现"圣林槽不得自举归冠"的规则。
7. **`CombatWood` 与 `Evaluate` 不同口径**：前者过滤 `TraitEnabled`（战斗真实值），后者不过滤
   （仅 `SoulItem.Wood`，无战斗调用点）。排查"数值对不上 UI"时先确认看的是哪一个。

---

## 第四部分：最关键的跨模块关系（6 条）

1. **单点命中扇出**：`SoulBlade.OnHitNPC`（`SoulBlade.cs:294-332`）依次调用
   `WoodCombat.FinishPrimaryHit` → 木质 A/B/C 派发 → `HardOrePlayer.Hit` → `OreFamily.OnMainHit` →
   `HolyHeavenProc.OnMainHit` → `Blessings.Hit`。任何族的"命中不生效"都先确认这条链有没有被前置 return 截断。
2. **计数真源共用**：5 个族的计数全部落在 `SoulSupport.FamilyItems`（`SoulSupport.cs:43`）上，
   而它由 `SoulItem.Active`（`SoulItem.cs:82-84`）决定。**改 `Active` 的组成会同时改变木质/矿石/飞刃/神圣/剑灵/环境的判定**。
3. **支援槽引擎自举**：`SoulSupport.Capacity`（`SoulSupport.cs:16-37`）用迭代法求"族门槛 → 支援格数"不动点，
   上界 `MaximumGrants=11`。改任一族的门槛数组会连带改变可开支援格数，进而改变族计数本身。
4. **圣林归冠循环**：`SoulItem.CrownCapacity` ↔ `WoodCrown.Active` ↔ `FamilyItems`，唯一有环的判定点，
   靠 `WoodCrown.evaluating` 护盾（`WoodCrown.cs:18,24`）打破；任何"重入时当作未激活"的语义改动都会影响归冠门槛。
5. **两套武器分类表互不同步**：族名册（`WoodTypes.Types` / `FlyingBladeFamily.Weapons` / `OreFamily.MaterialSwords` /
   `HolyHeavenFamily.Weapons` / `LivingArmamentFamily.Members`）与环境绑定表
   （`EnvironmentAttributes.RegisterBuiltins`，`EnvironmentAttributes.cs:87-124`）是**各自硬编码**的 ItemID 列表，
   新增族成员**不会**自动获得环境标签，必须两处都改。
6. **网络帧统一入口**：`ZenithReborn.HandlePacket`（`ZenithReborn.cs:18-32`）分派
   action 11 = 环境命中（`EnvironmentPlayer.ReceiveHit`）、action 3 = 木质特性同步（`WoodCombat.ReceiveHit`）、
   action 12 = 木质 A/B/C 中继（`WoodBondRelay.Receive`）、action 4 = 森林之力成长（`WoodPlayer.ReceiveGrowth`）。
   新增族同步时必须避开这 4 个已占用 action 号。

**补充（第 7 条，但很关键）**：`FlyingBladeShot` **不携带 `SoulId`**（`FlyingBladeHand.cs:31` 注释明示），
因此飞刃不参与祝福、环境、矿石命中和任何 `SoulBlade` 管线；飞刃的伤害只吃 `BladeDamage × DamageBonus`。

---

## 第五部分：未确认的职责 / 待核实清单

| # | 事项 | 依据 | 影响 |
|---|---|---|---|
| 1 | `LivingArmament.cs:10` 注释"玩家身体保持不可见"，本组 39 个文件中**没有任何隐藏玩家身体的实现**（可能是绘制层 hook 在 `SoulVisuals`/`SoulSceneRefraction` 等外范围文件，也可能未实现） | `LivingArmament.cs:10` 与全项目 `LivingArmament` grep 结果 | 若未实现，则"剑灵"实际表现为"玩家 + 漂浮武器同时可见" |
| 2 | `WoodCrown.cs:27` 注释称木质遮罩"不含森林/圣林/冠位奖励槽"，但 `WoodCrown.cs:29` 实际调用 `SoulSupport.FamilyItems`，**该集合包含** 15-16 与 35-36 | `WoodCrown.cs:27-29` vs `WoodTypes.cs:55-56`、`SoulTraits.cs:43` | 注释与实现矛盾；实际语义（含奖励位）与 `WoodTypes`/`ForestPower`/`WoodBondCore` 一致，故**判定正确、注释过期** |
| 3 | `EnvironmentUI.cs` 的布局/滚动/命中全部依赖 `SoulUI` 内的 `InfoTitle/InfoSurface/InfoClip/InfoScroll/InfoAction` 等辅助方法（`EnvironmentUI.cs:22-88`），这些定义不在本组范围 | `EnvironmentUI.cs:9` 为 `public partial class SoulUI` | 界面行为细节无法在本组内闭合验证 |
| 4 | `EnvironmentRules.Natural` 使用 `p.ZoneDirtLayerHeight`/`ZoneRockLayerHeight` 判定"深层"，注释声明"群系优先于深度"（`EnvironmentRules.cs:65-68`），但 `E.Snow/E.Ice` 与 `E.Desert/E.UndergroundDesert` 仍按 `deep` 二选一 | `EnvironmentRules.cs:71-73` | 雪原+洞穴层会同时点亮 Ice 而 Forest/Underground 被 biome 抑制；是否符合设计需策划确认 |
| 5 | 飞刃族第 8/10 阶"门槛先保留，暂不挂效果"（`FlyingBladeFamily.cs:15`） | 注释 + `Activated/Tally/Pass/Sigil` 只到第 4 档 | 满阶玩家无额外奖励；UI 会显示"第5/6阶"但无实际增益 |
| 6 | 剑灵名册标注为"暂定候选"两处（`LivingArmamentFamily.cs:6`、`SoulTraits.cs:83`） | 同上 | 4 把成员可能随设计变动 |

---

## 附：本次调查的覆盖核对

- 应覆盖：39 个文件（团队消息称"约 38 个"，实际清单列出 39 项）。
- 已完整 Read：39 个。
- 外部依赖（已定向 Grep/Read 但**不属于**本组产出）：`SoulItem.cs`、`SoulCurses.cs`、`SoulSupport.cs`、
  `SoulBlade.cs`、`SoulSwingPlayer.cs`、`SoulCombat.cs`、`WeaponFamilies.cs`、`WeaponFamilyTags.cs`、
  `ZenithReborn.cs`、`BondSnapshot.cs`、`SoulTraits.cs`、`SoulUI*.cs`、`FlyingBladeVisuals.cs`、
  `HolyHeavenStarVisuals.cs`（后两者仅为确认分工边界而读）。
