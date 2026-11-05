# 文件索引（FILE_INDEX）

> **本文件由脚本生成，不要手改。**修改会被下一次生成覆盖，正确做法是改调查报告或源码后重跑。

```text
生成命令 : python checks/build_file_index.py      （配套审计：python checks/map_audit.py）
生成时间 : 2026-11-05 15:10:33
数据来源 : docs/archive/codebase-map-survey-2026-11-03/ 下 9 份分组调查报告（A~I）的六列索引表
索引条目 : 764 条（去重合并 4 次重复出现；报告未覆盖的人工补写 51 条）
覆盖口径 : 仓库内全部自有文件，与 checks/map_audit.py 的分母一致
排除目录 : artifacts/  bin/  obj/  */__pycache__/  *.pyc  *-profile/  *-playtest/  .git/  编辑器配置目录
整体记录 : VfxLibrary/（11756 个原版素材，按第六节单独核对）
           tools/ilspy/  tools/xnbcli/  tools/xnbcli-binary/（第三方，记“第三方-未深入”）
如何更新 : 改完源码或调查报告后跑  python checks/build_file_index.py && python checks/map_audit.py
```

## 怎么用这张表

1. **先定位，再读码。**按路径前缀进对应节，看「文件职责」判断要不要打开，看「关键关联」判断改它还会连带什么。
2. **改之前看状态。**`已验证` 表示报告作者读过源码并给出证据；`部分待核实` / `第三方-未深入` 表示结论有缺口，别把它当权威。
3. **`多代理交叉`** 表示同一文件被两份以上报告独立分析过，职责列取了最长的一版，通常也是最可信的一版。
4. **失效路径 = 索引没跟上代码。**索引里的路径在磁盘上消失会被审计判 FAIL，删文件/改名后要顺手重跑生成。

## 抽取统计

```text
报告                    索引表  原始行  通配展开  省略合并  无法解析  历史快照
A-soul-core.md          1       32      0         0         0         1
B-families.md           1       39      0         0         0         0
C-ui.md                 1       22      0         0         0         0
D-render-vfx.md         1       49      5         0         0         0
E-checks-scripts.md     1       93      0         0         0         0
F-checks-fixtures.md    2       113     0         0         0         0
G-docs-current.md       1       64      0         0         0         0
H-docs-archive.md       7       116     0         0         0         0
I-tools-vfx.md          10      163     1         40        0         0
```

---

## 一、`ZenithReborn/` 源码（`*.cs`）

模组主工程的全部 C# 源文件。改这里等于改行为。

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `ZenithReborn/AttachmentStory.cs` | 静态类 + ModNPC + ModItem + GlobalNPC | 进度解锁 | 附着叙事线：条件检查、召唤友善史莱姆王、4 句对话推进、授予 `Attached`、凝胶信物 | `SoulUI`、`SoulProgress`、`SoulItem.RefreshStats`、`ZenithReborn.HandlePacket` | 已验证 |
| `ZenithReborn/BlessingCamera.cs` | C# ModSystem | 杂项 | ILHook `InitTargets` 扩容离屏留白与 RT 上限 | `BlessingPlayer`、`Main.OnPreDraw` | 已验证 |
| `ZenithReborn/BondSnapshot.cs` | record + 静态类 | 羁绊快照 | 木质/矿石的阈值、阶段名、进度、配色统一为只读展示模型 | `SoulSupport.FamilyItems`、`OreFamily`、`SoulUIArmoryBondCore`、`SoulUIWoodPanel` | 已验证 |
| `ZenithReborn/CrossSummon.cs` | ModItem 抽象 + GlobalNPC | 进度解锁 | 无生物群落限制的脑/世吞召唤物，只由本世界原生邪恶 Boss 掉落对侧 | `SoulCurses.BossSeal` | 已验证 |
| `ZenithReborn/EnvironmentAttributes.cs` | 静态注册表 + ModSystem | 环境属性系统 | 21 环境枚举、属性定义、武器→环境手工绑定表、`SoulItem` 环境视图 | `OreFamily`（矿石批量绑定）、`SoulItem`、`EnvironmentRules` | 已验证 |
| `ZenithReborn/EnvironmentEffects.cs` | ModProjectile ×2 | 环境属性系统 | `EnvironmentEffect` 8 种形状的机制弹幕 + `EnvironmentDomain` 960px 领域视觉环 | `EnvironmentPlayer.Hit`、`EnvironmentTarget` | 已验证 |
| `ZenithReborn/EnvironmentPlayer.cs` | ModPlayer + GlobalNPC + ModSystem | 环境属性系统 | 强度计算、21 环境机制、冰甲/叶甲/蜜露/汲取、DoT、命中网络回传、减益缩短钩子 | `SoulBlade`（Hit/ModifyHit）、`WoodPlayer`、`OreFlyingSword` | 已验证 |
| `ZenithReborn/EnvironmentRules.cs` | 静态规则 + 分发 | 环境属性系统 | 位运算、强度公式、21 项机制名/属性/文案/颜色、自然掩码、互斥克制；另含 `SoulItem` 环境分部 | `EnvironmentAttributes`、`SoulItem` 存档字段 | 已验证 |
| `ZenithReborn/EnvironmentTooltips.cs` | GlobalItem | 环境属性系统 / 族标签 | tooltip 插入"所属种族"行（带飞刃阶位）与"所属环境"折行 | `WeaponFamilyTags`、`FlyingBladeFamily`、`SoulUI.EditingSoul` | 已验证 |
| `ZenithReborn/EnvironmentUI.cs` | `SoulUI` 分部（UI） | 环境属性系统 | 21 格环境图鉴、详情强度文案、4 个动作（领域/常驻/恢复/说明）、规则说明 | `EnvironmentRules`、`EnvironmentAttributes`、`EnvironmentPlayer` | 部分待核实（布局辅助在 `SoulUI` 内，未在本组范围核对）；已验证；多代理交叉 |
| `ZenithReborn/EvapCircle.cs` | C# ModDust | 粒子 | 借原版 Cloud 帧的上升缩小圆 | 仅 `ViscousSlash:52` 调用 | 已验证 |
| `ZenithReborn/FlyingBladeFamily.cs` | 静态名册 | 飞刃族 | 31 把名册、1/2/4/6/8/10 阶门槛、+3%×n 加伤、四档能力开关 | `SoulItem.Active/Supports`、`SoulCurses.Blades`、`WeaponFamilyTags` | 已验证 |
| `ZenithReborn/FlyingBladeHand.cs` | ModPlayer | 飞刃族 | 0.3 秒/把蓄能、挥臂齐射（5° 扇面）、与本体飞剑互斥 | `SoulSwingPlayer.Begin`、`SoulCurses.Blades`、`SoulItem.BladeDamage` | 已验证 |
| `ZenithReborn/FlyingBladeShot.cs` | ModProjectile | 飞刃族 | 三阶段飞刃（直飞/自旋/收回）、旋转期 50% 伤害、第 4 阶收回留伤 | `FlyingBladeVisuals`（外范围）、`FlyingBladeFamily.Sigil` | 已验证 |
| `ZenithReborn/FlyingBladeVisuals.cs` | C# 类 | 飞刃 | 拖尾/环影/光点/光环/火花/命中爆发；`PreDraw` 内切批次 | `FlyingBladeShot`、`SoulVisuals.Tint` | 已验证 |
| `ZenithReborn/ForestPower.cs` | ModBuff（兼静态契约） | 木质族 | 森林之力成长引擎（6/12 层上限、300 帧/层、脱战 -1/120 帧）、六项收益查询、buff 文案 | `WoodPlayer`、`WoodCrown`、`WoodCombat`、`SoulSupport.FamilyItems` | 已验证 |
| `ZenithReborn/GlassMaterialPreset.cs` | C# 生成常量 | 折射 | 玻璃光学常量（Ior/Thickness/Dispersion/Ssr/Emission/SpaceWarp） | `SwordFlowMaterial`、`SoulSceneRefraction`、`SwordSpaceDistortion` | 已验证 |
| `ZenithReborn/GlobalUsings.cs` | 编译配置 | 存档兼容/工程 | 补齐 tModLoader 编译器缺失的 System 系列全局 using | 全项目 | 已验证 |
| `ZenithReborn/HardOrePlayer.cs` | ModPlayer + GlobalNPC | 矿石族 | 肉后 6 材质连击状态机、导刃标记、硬解、续锻治疗、钛片/钴刃/绽金生成 | `SoulBlade`、`OreFamily`、`WoodPlayer.HealFromSoul`、`SoulCurses` | 已验证 |
| `ZenithReborn/HardOreShot.cs` | ModProjectile | 矿石族 | 特性附属弹幕（钴刃/绽金花瓣/钛片），按 `FlightDamageShare` 折算防御，锁目标代际 | `HardOrePlayer.Spawn`、`OreFlyingSword`、`OreWeaponEnding` | 已验证 |
| `ZenithReborn/HardOreTypes.cs` | 静态表 + GlobalItem | 矿石族 | 肉后 6 剑名册、六特性文案、`Traits/Enabled` 判定、tooltip | `OreFamily`（材质映射）、`SoulItem.Active/TraitSupports` | 已验证 |
| `ZenithReborn/HeroPrefixCompat.cs` | internal 静态类 | 存档兼容 | 英雄模组前缀重建时的全量数据抢救缓存与空剑魂回填 | `SoulItem` 全部字段、`SoulItem.ApplyPrefix`、`SoulPlayer.PostUpdate` | 已验证 |
| `ZenithReborn/HolyHeavenFamily.cs` | 静态名册 | 神圣天堂族 | 8 把名册与启用判定（`TraitItems` 去重计数） | `SoulCurses.TraitItems`、`WeaponFamilyTags` | 已验证 |
| `ZenithReborn/HolyHeavenProc.cs` | ModSystem | 神圣天堂族 | 主命中 300 帧冷却落星（`StarDamage=1000`） | `SoulBlade.OnHitNPC`、`HolyHeavenStar`、`HardOrePlayer.ValidTarget` | 已验证 |
| `ZenithReborn/HolyHeavenStar.cs` | ModProjectile ×2 | 神圣天堂族 | 原版落星复用 + 120px 冲击波（排除直击主体、根去重） | `HolyHeavenStarVisuals`（外范围）、`HolyHeavenProc.StarDamage` | 已验证 |
| `ZenithReborn/HolyHeavenStarVisuals.cs` | C# 静态工具 | 杂项 | 落星飞行 dust + 命中 12 道 StarSpark | `HolyHeavenStar` | 已验证 |
| `ZenithReborn/LivingArmament.cs` | ModProjectile | 剑灵 | 隐形召唤体：跟随/索敌/站位，驱动代理玩家挥砍，用原版物品贴图绘制 | `LivingWeaponProxy`、`LivingArmamentFamily`、`SoulItem.VisibleSlotIds` | 部分待核实（:10 注释称"玩家身体保持不可见"，本范围内无实现） |
| `ZenithReborn/LivingArmamentFamily.cs` | 静态名册 | 剑灵 | 暂定 4 把候选名册与启用判定 | `SoulCurses.TraitItems`、`WeaponFamilyTags` | 已验证 |
| `ZenithReborn/LivingArmamentPlayer.cs` | ModPlayer | 剑灵 | 为每个 `TraitMains` 出战槽补生成召唤体（只本地玩家） | `LivingArmament`、`SoulItem.VisibleSlotIds` | 已验证 |
| `ZenithReborn/LivingChannelProjectile.cs` | GlobalProjectile + ModSystem | 剑灵 | 给 aiStyle 9/15/99 弹幕在 `On_Projectile.AI` 期间临时替换玩家与鼠标 | `LivingWeaponProxy` | 已验证 |
| `ZenithReborn/LivingProxyOwnerState.cs` | C# 源码（状态复制 / ModSystem） | 剑灵·活体武器代理 | 活体武器代理的玩家数值复制与蝙蝠棍治疗转发：LivingProxyOwnerState.Copy 把各伤害类的伤害/暴击/攻速/护穿/击退、手套与饰品字段、buff 与 ownedProjectileCounts 数组从真实玩家拷进代理玩家（数组用 Array.Copy 后清尾，不共享可变数组）；LivingProxyHealing 用 On_Player.Heal 钩子把原版蝙蝠棍命中回血从代理转发给真实玩家 | LivingArmament.cs、LivingWeaponProxy.cs、checks/ZenithPrototypeCheck/LivingBatHealCheck.cs | 人工补写 |
| `ZenithReborn/LivingWeaponProxy.cs` | 普通类 | 剑灵 | 每槽独立假 Player；`ItemCheck()` 走引擎物品检查攻击 | `LivingArmament`、`LivingChannelProjectile` | 已验证 |
| `ZenithReborn/LunarSummonRecovery.cs` | ModSystem + GlobalNPC + ModCommand | 进度解锁 | 四柱→月总召唤的卡死检测与修复（含磁盘 JSON 请求、`/zmoon repair`） | `ZenithReborn.HandlePacket`、`AttachmentStory.Notice` | 已验证 |
| `ZenithReborn/MaterialInheritance.cs` | C# 静态工具 | 材质 | `"base"` 递归继承（最大 8 层）、未知字段忽略 | `SwordFlowMaterial.Load` | 已验证 |
| `ZenithReborn/MaterialQuality.cs` | C# 静态类 | 材质 | 8/16 把飞剑阈值自动降档 | `SwordFlowMaterial` | 已验证 |
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
| `ZenithReborn/Progression.cs` | 静态类 + ModPlayer + ModSystem + GlobalNPC + ModItem | 进度解锁 | 12 位击杀掩码、`Capacity`/`Next`、玩家冷却与初始物品、战斗参与记录、Boss 授勋、6 种魂芯 | `SoulItem`、`SoulCurses`、`SoulUI`、`AttachmentStory` | 已验证 |
| `ZenithReborn/SlotProgression.cs` | `SoulProgress` + `SlotGates` | 进度解锁 | 9 条槽位门槛表、魂芯受理/补办、槽位提示与缺口文案 | `SoulItem`、`SoulSupport`、`SoulUIMemories` | 已验证 |
| `ZenithReborn/SoulArmoryLayout.cs` | `readonly record struct` | UI 基础设施 | 纯几何：工作区/剑库/舞台/详情、Compact 切换、槽位坐标、木质面板锚点、列表行、按钮位 | `SoulItem.LegendarySlotId/CrownSlotBase/SupportOrdinal`、`SoulUI*` | 已验证 |
| `ZenithReborn/SoulArmoryPreview.cs` | `sealed record` | UI 基础设施 | 只读换入评估：克隆剑魂跑 `TrySwapHost`，输出允许/原因/伤害暴击 delta/容量 | `SoulItem.TrySwapHost/ItemAt/Capacity`、`SoulUIArmoryInteraction`/`SoulUIArmoryDetails` | 已验证 |
| `ZenithReborn/SoulBlade.cs` | ModProjectile + GlobalProjectile | 战斗 | 飞剑弹幕：来源身份 `SoulSource`、椭圆轨迹、15 更新剑影采样、命中单元判定、伤害乘区整合、特技释放、绘制 | `SoulItem.EmitBlade`、`WoodCombat`、`WoodBond*`、`HardOrePlayer`、`OreFamily`、`HolyHeavenProc`、`BlessingPlayer`、`SoulTrail*` | 部分待核实（木质复合羁绊与矿石命中细节在 `WoodCombat.cs`/`Ore*`，不在本组） |
| `ZenithReborn/SoulBlessings.cs` | ModPlayer + ModProjectile + ModBuff + ModSystem | 诅咒/难度 | 御剑蓄力/爆发、首击接力、归鞘留锋、破障穿地、距离增伤、御剑条 UI | `SoulItem.Reach`、`SoulSource`、`SoulBlade`、`SoulCurses`、`SoulUI.IsEditing` | 部分待核实（`BlessingStrike` 的绘制依赖 `OreSwordEffects`，未在本组） |
| `ZenithReborn/SoulBossAnimation.cs` | record | 进度解锁 | 96px 棋盘图集的时间轴：按 ms 取帧、算源矩形 | `SoulBossPortraits.Draw` | 已验证 |
| `ZenithReborn/SoulBossPortraitCatalog.cs` | 生成代码 | 进度解锁 | 15 个 Boss 的图集列数与每帧时长（由 `checks/prepare_boss_portraits.py` 生成） | `SoulBossPortraits`、`SoulBossAnimation` | 已验证 |
| `ZenithReborn/SoulBossPortraits.cs` | `SoulBossPortraits` 分部 | 进度解锁 | 按封印位取图集 key、绘制动态头像（有 body 的双层叠加） | `SoulBossPortraitCatalog`、`SoulBossAnimation`、`SoulUIBossUnlocks` | 已验证 |
| `ZenithReborn/SoulBossUnlocks.cs` | 静态类 + 3 record | 进度解锁 | 只读展示模型：每诅咒的 Boss 目标、四档奖励与当前/旧档状态 | `SoulCurses`、`SoulUIBossUnlocks`、`SoulUICurseUnlocks`、`SoulUIArchive` | 已验证 |
| `ZenithReborn/SoulCombat.cs` | `SoulItem` 分部 | 战斗 | 单把宿主的真实伤害 = 武器伤害 × `Potency` × 矿石主剑倍率 | `SoulItem`、`OreFamily` | 已验证 |
| `ZenithReborn/SoulCurseDescriptions.cs` | `SoulCurses` 分部 | 诅咒/难度 | 诅咒与祝福的阶段说明/需求文案，UI 与图鉴共用 | `SoulCurses`、`SoulBossUnlocks`、`SoulUIArchive` | 已验证 |
| `ZenithReborn/SoulCurses.cs` | 静态类 + `SoulItem` 分部 | 诅咒/难度 | 18 位铭刻、6 组诅咒分级、出剑数/命中期/穿地上限、特性开关、诅咒存档网络、地形穿透判定 | `SoulBossCredit`、`SoulSwingPlayer`、`SoulBlade`、`SoulTraits`、`SoulBossUnlocks` | 已验证 |
| `ZenithReborn/SoulDifficulty.cs` | ModSystem | 诅咒/难度 | 传奇难度开关：`GameMode==2 && getGoodWorld`，真实改写世界难度并持久化 | `SoulItem.LegendaryOn`、`SoulDomainUI` | 已验证 |
| `ZenithReborn/SoulDomainUI.cs` | partial 分部 | 域界面 | `DomainMode`、`DomainWorkBounds`、`RingViewport`、`SelectSlot`、`LibraryItems`、`EquipFromInventory`、`ReturnSelected`、背景/页头/难度按钮 | `SoulArmoryLayout`、`SoulDifficulty`、`SoulInput` | 已验证 |
| `ZenithReborn/SoulEchoDamage.cs` | 静态类 | 诅咒/难度 | 断锋诅咒 → 剑影伤害倍率（1 / 0.8^unit / 0.20） | `SoulBlade.ModifyHitNPC`、`SoulTrailUnits` | 已验证 |
| `ZenithReborn/SoulFlowParticles.cs` | C# ModDust + 发射器 | 粒子 | `SoulFlowStyle` 配色、`SlashSparkEmitter`、`SoulFlowSpark`（借用 SharpTears） | `OreSwordEffects:130`、`OrePierceFeedback:33`、`SoulBlade:241` | 已验证 |
| `ZenithReborn/SoulInfoLayout.cs` | 静态类 | UI 基础设施 | `Columns`、`Tile` 宫格、`MasterDetail` 主从分栏、`Ease/Approach` | `SoulUIArchive`、`SoulUICurseUnlocks`、`SoulUIMemories`、`SoulUIInformation`、`EnvironmentUI` | 已验证 |
| `ZenithReborn/SoulInput.cs` | 静态类 + ModPlayer | UI 基础设施 | 鼠标捕获/屏蔽/还原时序，阻止点击穿透到世界；`SetControls` 禁使用键 | `SoulUI`（Capture/Begin/End/Restore） | 已验证 |
| `ZenithReborn/SoulItem.cs` | ModItem（partial 主分部） | 数据模型 | 剑魂物品本体：5 组槽位数组、槽号映射、容量/效力/射程、`TrySwapHost`、存读档与网络 | `SoulSupport`、`SoulProgress`、`SoulCurses`、`SoulDifficulty`、`SoulUI`、`SoulArmoryLayout` | 已验证 |
| `ZenithReborn/SoulLegacyMartialData.cs` | `SoulItem` 分部 | 存档兼容 | 旧版武技字段（充能/阶位）的存档与网络字段，注释明确不参与玩法 | `SoulItem.SaveData/LoadData/NetSend/NetReceive` | 已验证 |
| `ZenithReborn/SoulMaterialTrail.cs` | C# 几何 | 拖尾 | 60 帧历史 → 3 步 × 4 行带面顶点；`Visibility`/`Dissolve` 曲线 | `SwordFlowMaterial.Draw`、`SoulBlade`、`SoulTrailEnding` | 已验证 |
| `ZenithReborn/SoulRibbonMesh.cs` | C# 几何（旧） | 拖尾 | 4 段 × 3 带旧版 ribbon，带亮边流动噪声 | `SoulBlade.BuildRibbon`（无调用者）、`checks/RibbonCheck` | 已验证 |
| `ZenithReborn/SoulSceneRefraction.cs` | C# ModSystem(Client) | 折射 | 滤镜注册、normal/surface RT、`PrepareNormals`、`GlassScreenData` | `ZRGlassScreen.fx`、`SwordFlowMaterial`、`SoulVisuals` | 已验证 |
| `ZenithReborn/SoulSupport.cs` | 静态类 | 数据模型 | 支援格通用奖励池：两条 `SupportGrant`、不动点迭代求 `Capacity`、`FamilyItems` 族判定集合 | `SoulItem`、`WoodTypes`、`OreFamily`、`WoodCrown`、`BondSnapshot` | 已验证 |
| `ZenithReborn/SoulSwingPlayer.cs` | ModPlayer | 战斗 | 挥砍发射调度：选出本轮主攻、按独行上限分配节点、归鞘打断、飞刃族互斥 | `SoulItem.UseAnimation/Shoot/EmitBlade`、`SoulCurses.Blades`、`FlyingBladeHand` | 已验证 |
| `ZenithReborn/SoulTrailEnding.cs` | C# ModSystem | 拖尾 | 弹幕销毁后 36/24 帧余迹，容量 256，按代际淘汰 | `SoulVisuals`、`SoulSceneRefraction` | 已验证 |
| `ZenithReborn/SoulTrailMotion.cs` | C# 工具 | 拖尾 | 残影历史随主人位移平移；90000 平方距离上限 | `SoulBlade` | 已验证 |
| `ZenithReborn/SoulTrailUnits.cs` | C# 常量 | 拖尾 | 本体 + 3 残影的 `(start, samples, opacity)` 窗口 | `SoulBlade.DrawRibbonShader`、`SoulTrailEnding.CaptureUnit` | 已验证 |
| `ZenithReborn/SoulTraits.cs` | 静态类 + 2 record/enum | 特性图鉴 | 一次性构建全部特性条目（族、阶位、特技、诅咒、复生）供图鉴筛选 | `SoulCurses`、`SoulSupport`、`OreFamily`、`WeaponFamilies`、`SoulBossUnlocks` | 部分待核实（各族 `Active` 判定散落在 `WoodBondCore`/`OreSwordTraits`/`FlyingBladeFamily` 等） |
| `ZenithReborn/SoulUI.cs` | partial 主文件 (`ModSystem`) | 主控/骨架 | 状态字段、`Open/Toggle/Sync/SelectPage`、输入与界面层注册、`Draw()` 总入口与页面分发、公共小件、`SlotBounds`/`TryClickHost`、旧扩展页 | `SoulUIPages`、`SoulInput`、所有 `Draw*` 分部 | 已验证 |
| `ZenithReborn/SoulUIArchive.cs` | partial 分部 | 剑魂特性页 | **文件名为 Archive，实为 `DrawTraits`**：范围/分类筛选 + 列表详情双栏 + Boss 区块挂载 | `SoulTraits.All`、`SoulBossUnlocks`、`SoulCurses` | 已验证 |
| `ZenithReborn/SoulUIArmory.cs` | partial 分部 | 配阵 | `DrawArmory` 三块协调 + Compact tab、`DrawArmoryHeader`（定位空位/难度/导航） | 剑库/剑阵/详情/BondCore/WoodPanel | 已验证 |
| `ZenithReborn/SoulUIArmoryBondCore.cs` | partial 分部 | 羁绊核心 | `DrawBondCore`（主导羁绊段环/族徽/棱彩）、`DrawCoreFlow`（剑位→核心流光） | `BondSnapshot`、`BondColors`、`ArmoryLayout` | 已验证 |
| `ZenithReborn/SoulUIArmoryDetails.cs` | partial 分部 | 剑位详情 | 详情滚动态（角色/数值/族名/木质进度/环境/条件/下一步）+ 底部确认与取出按钮 | `SoulArmoryPreview`、`BondSnapshot.Wood`、`SoulProgress` | 已验证 |
| `ZenithReborn/SoulUIArmoryInteraction.cs` | partial 分部 | 配阵·状态机 | 候选/预览/归位动画/coreFlow 状态、`SelectArmoryCandidate`、`CurrentArmoryPreview`、`ConfirmArmoryCandidate`、定位空位、槽位码与配色 | `SoulArmoryPreview`、`EquipFromInventory`、`SoulCrown` | 已验证 |
| `ZenithReborn/SoulUIArmoryScene.cs` | partial 分部 | 配阵·剑阵 | 仪盘装饰、`DrawArmoryScene`、`DrawArmoryNode`（悬停+点击换位）、归位飞行动画 | `ArmoryLayout.Slot`、`TrySwapHost`、`TriggerCoreFlow` | 已验证 |
| `ZenithReborn/SoulUIBossUnlocks.cs` | partial 分部 | Boss 铭刻 | 两列（目标 / 累计奖励）与窄屏改单列；目标卡、奖励卡绘制 | `SoulBossUnlockView`、`SoulBossPortraits.Draw` | 已验证 |
| `ZenithReborn/SoulUICurseUnlocks.cs` | partial 分部 | 成长·封印 | `DrawSealUnlockList` 六封印卡，点开后行内展开 `DrawBossUnlocks` | `SoulCurses.Groups/Count`、`SoulBossUnlocks.Read` | 已验证 |
| `ZenithReborn/SoulUIEffects.cs` | partial 分部 | 公共特效/几何 | `UiTime`、剑环半径/圆心/极坐标、`Glow/Line/Arc/CenterText/Fit`、`DrawNavigation`、`DrawSoulRing`(休眠)、`DrawLegendarySlot` | `ContentBounds`、`SoulVisuals.Tint`、`SoulProgress.SlotHint` | 已验证 |
| `ZenithReborn/SoulUIGlyphs.cs` | partial 分部 + enum | 公共图标 | 16 种矢量徽记 `Glyph`，以及页面/特性分类/诅咒三张映射 | `SoulUIPages`、`TraitCategory`、`SoulCurse` | 已验证 |
| `ZenithReborn/SoulUIInformation.cs` | partial 分部 | 信息页组件 | `InfoClip/InfoScroll/InfoSurface/InfoLines/InfoAction/InfoBlock/InfoTitle/InfoBadge`、`DrawInformationNavigation`、动效缓存 `infoMotion` | 所有信息页、`ContentBounds`、`clipState` | 已验证 |
| `ZenithReborn/SoulUILibrary.cs` | partial 分部 | 军械库列表 | `LibraryBounds`、`ArmoryLibraryItems`（筛选/排序）、`DrawArmoryLibrary`、旧物 `DrawLibrary` | `SoulItem.CanHost`、`SelectArmoryCandidate`、`SoulVisuals.Tint` | 已验证 |
| `ZenithReborn/SoulUIMemories.cs` | partial 分部 | 成长记忆页 | `DrawProgress` 三分组、`DrawSwordMemories`（剧情+十剑宫格）、`DrawCoreMemories`（6 魂芯） | `SoulProgress`、`AttachmentStory`、`SoulCore` | 已验证 |
| `ZenithReborn/SoulUIPages.cs` | 静态类 | 页面身份 | 页号常量 0/1/2/4/5、导航集合、`Resolve/Index/Name/Color` | `SoulUI.SelectPage`、`TabBounds`、`DrawNavigation` | 已验证 |
| `ZenithReborn/SoulUIStyle.cs` | partial 分部 | 公共样式 | `RoundedBox`、`DrawChrome`(休眠)、`DrawPageSurface`、`GroupCaption`、`Champagne` 色 | `SoulUI.Box/Line` | 已验证 |
| `ZenithReborn/SoulUIWoodPanel.cs` | partial 分部 | 木质族面板（V2） | 标题/5 阶段轨/8 剑网格/4 数值/4 复合羁绊灯/自绘 tooltip；两套木剑口径 | `BondSnapshot.Wood`、`WoodBondCore`、`WoodTypes`、`ForestPower`、`WoodCrown` | 已验证 |
| `ZenithReborn/SoulVisuals.cs` | C# ModSystem | 拖尾/取色 | `Tint()` 三级取色表 + `PostDrawTiles` 世界层绘制入口 | 全模块取色、`SoulSceneRefraction.CompositingReady` | 已验证 |
| `ZenithReborn/SoulZenithEffects.cs` | C# 静态工具 | 星芒 | 星芒 `DrawTip`、飞行 dust、两处光照、`Profile` | `SoulBlade.PreDraw/AI`、`FinalFractalHelper` | 已验证 |
| `ZenithReborn/SurfaceMaterialDefinition.cs` | C# 数据合同 | 材质 | 40+ 参数 + 4 细节层 + 10 贴图路径 + `Validate`/`Control[]` | `MaterialInheritance`、`SurfaceMaterialRenderer` | 已验证 |
| `ZenithReborn/SurfaceMaterialRenderer.cs` | C# ModSystem(Client) | 材质 | `ZRMaterialSurface` 绘制、UV→切线基、高度图位移 | `SurfaceMaterialDefinition`、`SwordFlowMaterial` | 已验证 |
| `ZenithReborn/SwordBodyColorNoise.cs` | C# 工具 | 剑体 | 3 层 value noise → 512² 灰度纹理 | `VfxSwordMask.Prepare` | 已验证 |
| `ZenithReborn/SwordEnchantmentVisuals.cs` | C# GlobalProjectile | 杂项 | 三类飞剑 `noEnchantmentVisuals=true` | `SoulBlade`/`OreFlyingSword`/`OreSkyfall` | 已验证 |
| `ZenithReborn/SwordFlightTrail.cs` | C# 类 | 拖尾 | 8 点短历史 + `Strength`/`Formation` 曲线 | `OreFlyingSword`、`OreSkyfall`、`HardOreShot`；原列 `OreSwordEffects.Trail` 为其使用者，该路径已随 T3 删除 `OreSwordEffects.Trail` 而移除（保留历史记录） | 已验证 |
| `ZenithReborn/SwordFlowMaterial.cs` | C# ModSystem | 材质 | 效果加载、图集绑定、`Draw` 分流、`ZoneMask.zrdata` 上传、世界光 | `SurfaceMaterialRenderer`、`SoulSceneRefraction`、`Material*` | 已验证 |
| `ZenithReborn/SwordFlowMeshBasis.cs` | C# 几何 | 材质 | 跨三角形 UV 梯度（并查集加权） | 仅 `SwordFlowMaterial.Unload`→`Clear()`；`Prepare` 无调用者 | 已验证 |
| `ZenithReborn/SwordFlowPalette.cs` | C# 静态工具 | 材质 | 剑色 → 暗/中/亮三阶，低饱和走冷钢 | `SwordFlowMaterial.Draw` | 已验证 |
| `ZenithReborn/SwordSpaceDistortion.cs` | C# 静态工具 | 折射 | 空间扭曲参数包 `At`/`Apply` | `ZRSwordSpaceWarp.fxh`（孤儿）；**无调用者** | 已验证 |
| `ZenithReborn/SwordSpark.cs` | C# ModDust | 粒子 | 预乘发光小尘，自带光照 | `SwordSpark.Emit` **无调用者**；贴图落回 MagicPixel in PureCode | 已验证 |
| `ZenithReborn/VfxIsolation.cs` | C# 静态开关 | 总闸 | 10 分支 / 18 构成 / 26 带面构成 / 7 玻璃构成的唯一开关源 | 被全部渲染文件读取 | 已验证 |
| `ZenithReborn/VfxMaskData.cs` | C# 生成数据 | 剑体 | 21 把矿石剑的色场配方与几何常量 | `VfxSwordMask.Load`、`checks/emit_vfx_mask_data.py` | 已验证 |
| `ZenithReborn/VfxPartDebug.cs` | C# ModSystem | 总闸 | F7/F8 移动、F9 切换、F10 全关、Shift/Ctrl+F10 全开/基线 | `VfxIsolation` 四数组长度 | 已验证 |
| `ZenithReborn/VfxRenderState.cs` | C# 静态工具 | 总闸 | 保存/还原 Blend·Depth·Raster·Scissor·3 槽纹理采样·顶点索引，进入置 Additive | SoulVisuals、ViscousSlash、SoulSceneRefraction | 已验证 |
| `ZenithReborn/VfxSwordMask.cs` | C# ModSystem | 剑体 | `Main.OnPreDraw` 离屏合成色场×蒙版（640×256/剑） | `VfxMaskData`、`OreFlyingSword`、`OreWeaponEnding` | 已验证 |
| `ZenithReborn/ViscousSlash.cs` | C# ModSystem(Client) | 粒子 | `On_Main.DrawDust` 加法批次残影扫掠 | `VfxIsolation.ViscousSlash`、`EvapCircle` | 已验证 |
| `ZenithReborn/WeaponFamilies.cs` | 静态类 | 战斗 | 剑类五族（迅刃/实锋/寒霜/自然/灵能）名册、判定、文案、攻速与射程系数 | `SoulItem.UseSpeedMultiplier/Reach/EmitBlade`、`SoulBlade`、`SoulTraits`、`WoodTypes` | 已验证 |
| `ZenithReborn/WeaponFamilyTags.cs` | 静态类 | 特性图鉴 | "所属种族"文本的单一出口，固定拼接顺序，附飞刃族阶位提示 | `FlyingBladeFamily`、`HolyHeavenFamily`、`LivingArmamentFamily`、`WoodTypes`、`OreFamily` | 已验证 |
| `ZenithReborn/WoodBondCore.cs` | 静态判定 + 数据类型 | 木质族 | 四组复合羁绊判定（A/B/C/D）与 `WoodBondHit` 命中上下文 | `SoulSupport.FamilyItems`、`WoodCrown`、`SoulBlade` | 已验证 |
| `ZenithReborn/WoodBondEmber.cs` | 静态执行 | 木质族 | 灰烬繁生·焚叶：64px/30%×D/最多 3 目标/180 帧冷却 | `WoodBondCore.ActiveEmber`、`WoodCombat.HasToxinAndEmber`、`WoodBondRelay` | 已验证 |
| `ZenithReborn/WoodBondForge.cs` | 静态执行 + 中继 | 木质族 | 锻根共生·锻芽（3 层/180 帧/补斩 30%×D）+ A/B/C 共用中继协议（action 12） | `WoodBondCore.ActiveForge`、`WoodCombat.HasSalt`、`ZenithReborn.HandlePacket` | 已验证 |
| `ZenithReborn/WoodBondRot.cs` | 静态执行 | 木质族 | 腐林凋蚀·凋蚀释放：提前结算 25% DoT 预算、单次 ≤50%×D、180 帧冷却 | `WoodCombat.OwnDotBudget/ConsumeOwnDot`、`WoodPlayer.Ready` | 已验证 |
| `ZenithReborn/WoodCombat.cs` | GlobalNPC + ModBuff ×6 | 木质族 | 八木剑特性状态表（标记/DoT/减速）、伤害修正、网络同步、DoT 结算 | `SoulBlade`（Prepare/Modify/Finish）、`ForestPower`、`WoodBondCore` | 已验证 |
| `ZenithReborn/WoodCrown.cs` | 静态判定 | 木质族 | 圣林归冠：8 木 Full + 泰拉刃在非圣林槽；含防循环重入护盾 | `SoulSupport.FamilyItems`、`SoulItem.CrownCapacity`、`ForestPower` | 部分待核实（:27 注释称"不含森林/圣林/冠位奖励槽"，但 :29 实际用 `FamilyItems` 会包含，注释与实现不一致） |
| `ZenithReborn/WoodPlayer.cs` | ModPlayer + ModBuff ×2 | 木质族 | 成长字段与同步（action 4）、生命/防御/减伤/回复、治疗预算、`Ready` 冷却 | `ForestPower`、`WoodCombat`、`EnvironmentPlayer` | 已验证 |
| `ZenithReborn/WoodTypes.cs` | 静态表 + GlobalItem | 木质族 | 8 把名册、`WoodLoadout`（S/生命）、掩码、`Evaluate`、八条特性文案、tooltip | `SoulSupport.FamilyItems`、`WoodCrown`、`HardOreTypes`/`WeaponFamilies` 兜底文案 | 已验证 |
| `ZenithReborn/ZenithReborn.cs` | Mod 入口类 | 装配/网络 | 注册 P/O 键位；`HandlePacket` 分发 action 1-12 到 9 个业务模块 | `AttachmentStory`、`SoulProgress`、`SoulBossCredit`、`BlessingPlayer`、`LunarSummonRecovery`、`WoodCombat`、`EnvironmentPlayer` | 已验证 |
| (合计) | 121 条 |  |  |  |  |

---

## 二、`ZenithReborn/` 工程与资产 + 仓库根

贴图、着色器、本地化、构建清单与仓库根的说明文件。不写逻辑，但删一个就编译/运行失败。

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `.gitattributes` | Git 属性 | 仓库根 | 统一换行策略（`* text=auto`，源码与文本强制 LF）并声明 `*.tmod`/`*.png` 为二进制，避免跨平台 diff 污染 | 需与新目录中的二进制资产同步 | 人工补写 |
| `.gitignore` | Git 忽略规则 | 仓库根 | 忽略 `**/bin/`、`**/obj/`、7 个 `*-profile/`、`*-playtest/` 隔离运行目录；这些目录同样被 checks/map_audit.py 排除出索引分母 | 与 checks/map_audit.py 的排除规则必须一致 | 人工补写 |
| `AGENTS.md` | Markdown 硬规则 + 版本状态流水 | 仓库根 | 不可让渡的硬规则与逐版本状态：每条记录安装版本、验证证据等级、未验证面；新版本往顶部追加，旧内容降级为历史 | CODEBUDDY.md 用 `@AGENTS.md` 导入；docs/CHANGELOG.md、docs/AGENTS状态历史.md | 人工补写 |
| `CODEBUDDY.md` | Markdown 项目入口 | 仓库根 | CodeBuddy 的项目入口：环境事实、目录职责、命令、权威顺序（源码 > 文档 > 记忆）、协作偏好与文档指针 | AGENTS.md、README.md、.codebuddy/rules/ | 人工补写 |
| `README.md` | Markdown 面向人的说明 | 仓库根 | 面向玩家的功能说明与验证证据分级（编译 / 服务端夹具 / 无图形客户端验证），并声明“注释必须中文”的协作规则 | docs/CHANGELOG.md、artifacts/install-result.json | 人工补写 |
| `ZenithReborn/Assets/Effects/ZRClearGlass.fxh` | HLSL 头 | 着色器 | 玻璃光学 / 液态玻璃 / 高光 / 空间扭曲片段；**无任何 `.fx` include**，且被 `buildIgnore` 排除 | — | 已验证 |
| `ZenithReborn/Assets/Effects/ZRGlassScreen.fx` | HLSL | 着色器 | 屏幕折射滤镜（6 参数 + 2~4 张 RT） | `SoulSceneRefraction` | 已验证 |
| `ZenithReborn/Assets/Effects/ZRGlassScreen.fxc` | 编译产物 | 着色器 | 与 `.fx` 同名对照；运行时加载的是 `.fx` 经构建编译的 xnb | `build-manifest.json` | 部分待核实（构建是否真的重编 `.fx` 未在构建日志中确认） |
| `ZenithReborn/Assets/Effects/ZRLiquidGlass.fxh` | HLSL 头 | 着色器 | 玻璃光学 / 液态玻璃 / 高光 / 空间扭曲片段；**无任何 `.fx` include**，且被 `buildIgnore` 排除 | — | 已验证 |
| `ZenithReborn/Assets/Effects/ZRMaterialSurface.fx` | HLSL | 着色器 | 通用微表面材质（含 `uDetailA[4]` 等） | `SurfaceMaterialRenderer` | 已验证 |
| `ZenithReborn/Assets/Effects/ZRMaterialSurface.fxc` | 编译产物 | 着色器 | 与 `.fx` 同名对照；运行时加载的是 `.fx` 经构建编译的 xnb | `build-manifest.json` | 部分待核实（构建是否真的重编 `.fx` 未在构建日志中确认） |
| `ZenithReborn/Assets/Effects/ZRSwordFlow.fx` | HLSL | 着色器 | 带面主材质（36 参数，无 `uPart*`） | `SwordFlowMaterial` | 已验证 |
| `ZenithReborn/Assets/Effects/ZRSwordFlow.fxc` | 编译产物 | 着色器 | 与 `.fx` 同名对照；运行时加载的是 `.fx` 经构建编译的 xnb | `build-manifest.json` | 部分待核实（构建是否真的重编 `.fx` 未在构建日志中确认） |
| `ZenithReborn/Assets/Effects/ZRSwordHighlights.fxh` | HLSL 头 | 着色器 | 玻璃光学 / 液态玻璃 / 高光 / 空间扭曲片段；**无任何 `.fx` include**，且被 `buildIgnore` 排除 | — | 已验证 |
| `ZenithReborn/Assets/Effects/ZRSwordSpaceWarp.fxh` | HLSL 头 | 着色器 | 玻璃光学 / 液态玻璃 / 高光 / 空间扭曲片段；**无任何 `.fx` include**，且被 `buildIgnore` 排除 | — | 已验证 |
| `ZenithReborn/Assets/Effects/build-manifest.json` | JSON | 资产 | 3 组 `.fx↔.fxc` 的 sha256 与编译器信息（`d3dcompiler_43 / fx_2_0`） | `checks` 校验脚本 | 已验证 |
| `ZenithReborn/Assets/UI/BossPortraits/Bee.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Brain.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Cultist.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Destroyer.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/DestroyerBody.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Eater.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/EaterBody.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Empress.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Eye.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Flesh.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Flower.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Moon.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Prime.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Queen.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/QueenBody.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Skeleton.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/SkeletonBody.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Temple.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/Twins.png` | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/sources.json` | JSON | 资产 | 立绘来源登记 | UI 模块 | 部分待核实 |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/BrushDefault.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/ColorField.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/EmissionMap.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/EnvironmentMap.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/HeightMap.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/NoiseSeamless.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/NoiseSeamless_candidate.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/NormalMap.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/SurfaceMap.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/ZenithField.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/ZenithWear.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/ZoneMask.png` | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/ZoneMask.zrdata` | 自定义二进制 | 资产 | 四区遮罩原始 RGBA（魔数 ZRDT/v1） | `SwordFlowMaterial.TryLoadRaw` | 已验证 |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/bindings.json` | JSON 配置 | 资产 | `weapons` 图集绑定 + `SurfaceMaterial` 路径；**当前 `weapons: {}`** | `SwordFlowMaterial.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/AdamantiteSword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/CobaltSword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/CopperBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/CopperShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/GoldBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/GoldShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/IronBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/IronShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/LeadBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/LeadShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/MythrilSword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/OrichalcumSword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/PalladiumSword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/PlatinumBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/PlatinumShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/SilverBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/SilverShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/TinBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/TinShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/TungstenBroadsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/SwordBody/TungstenShortsword.png` | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Localization/en-US.hjson` | 本地化 | 本地化 | 英文键值 | `Localization` | 已验证 |
| `ZenithReborn/Localization/zh-Hans.hjson` | 本地化 | 本地化 | 简中键值 | `Localization` | 已验证 |
| `ZenithReborn/SwordSpark.png` | PNG（模组根，**不在 Assets 下**） | 资产 | 16×16 光斑，被 `SwordSpark`/`SoulFlowSpark` 引用 | `SwordSpark.cs:10`、`SoulFlowParticles.cs:77` | 已验证 |
| `ZenithReborn/ZenithReborn.csproj` | 构建配置 | 配置 | net8.0 / LangVersion12 / `BuildMod=false` / 引 Luminance | 构建 | 已验证 |
| `ZenithReborn/build.txt` | 构建配置 | 配置 | `buildIgnore = bin/*, obj/*, *.csproj, Assets/Effects/*.fxh` | 打包 | 已验证 |
| `ZenithReborn/description.txt` | tModLoader 模组描述 | 工程元数据 | 模组列表里显示的描述文本，随版本手写更新（当前停在 0.3.112 的玻璃材质说明，落后于 build.txt 的 0.3.130） | ZenithReborn/build.txt（版本号在此）、README.md | 人工补写 |
| (合计) | 77 条 |  |  |  |  |

---

## 三、`checks/` 验证体系

全部编排脚本、静态检查、夹具体系与归档材料。不参与模组编译。

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `checks/ZenithPrototypeCheck/AirflowAtlasBatchCheck.cs` | GPU 导出 ModSystem | K 渲染 | 一个隔离 GPU 客户端顺序导出整组气流图集（避免逐剑重启游戏）；读 `ZENITH_AIRFLOW_EXPORT`/`ZENITH_AIRFLOW_RECIPES`，逐剑写 `profile.json`+`gpu.json`，全部完成写 `complete.txt`，异常写 `error.txt` | 消费 `SwordAirflowMask`；`run_airflow_batch.ps1` | 部分待核实 |
| `checks/ZenithPrototypeCheck/AirflowMaterialCheck.cs` | GPU 合成 ModSystem | K 渲染 | 调生产合成入口做隔离 GPU 验证：Alpha 相乘、状态恢复、原版剑体遮挡顺序；延迟解析生产合成 API（普通素材导出模式不引用旧包缺失方法）；绑定另一绘制者的丢弃型目标检查合成返回时原画面是否仍在 | `run_airflow_material.ps1` | 部分待核实 |
| `checks/ZenithPrototypeCheck/AirflowWorldCheck.cs` | 实机世界夹具 | K 渲染 | 只在专用隔离目录启用：加载已复制的测试人物/世界，直接观察生产飞剑 AI 与绘制；提供 `ZENITH_DUMMY_CHECK`/`ZENITH_DUMMY_PLACEMENT_ONLY`/`ZENITH_DUMMY_BEFORE_PLACEMENT` 三开关；走原版完整物品使用流程放稻草人（覆盖全局物品钩子与挥手动画，不用单回调替代）；不叠加第二套预览特效、不保存测试物品/NPC、不读正式存档 | `run_airflow_world.ps1`；注释 `AirflowWorldCheck.cs:10-11` | 部分待核实 |
| `checks/ZenithPrototypeCheck/ArmoryInteractionRenderCheck.cs` | 渲染交互（静态） | K 渲染 | 通过生产界面绘制接收模拟点击，所有交换只发生在临时人物与剑魂上；反射 `SoulUI` 私有字段；测量真实详情内容末尾再调窗口高度，让被裁剪的成长按钮恰好在固定操作按钮后 | `run_information_ui_render.ps1` 的 `ZENITH_ARMORY_INTERACTION_GPU` | 部分待核实 |
| `checks/ZenithPrototypeCheck/ArmoryUiCheck.cs` | 静态断言套件 | I UI | 剑库/配剑界面（19 条）：剑位保持可用尺寸（暂停与扩展支援也在可滚动画布内）、主攻/奖励/传奇/全支援命中框无重叠、三区或窄屏视图都在当前工作区、宽屏剑库与详情不重叠、短视口改并排操作（确认与取回始终可达）、详情与剑库滚动区各在自己面板内、滚动详情与固定操作可见区分离、中央羁绊核心读真实木质阶段不写剑魂、换入计算不写真实快照/收藏/背包、选择候选只改界面状态、再选同剑位退出候选预览不改真实物品、选其他剑位退出预览并显示当前、确认交换真实物品并保前缀且原武器回原背包格、完成寄宿后清候选不能重复提交旧对象、背包格被替换后旧候选不能误寄宿新物品、同种限制用原规则（拒绝预览不改收藏）、窄屏隐藏的剑阵不能吃剑库点击、定位空位只选已开放空位并返回剑阵、未解锁剑位候选预览被真实规则拒绝 | `SoulUI` 剑库、`SoulBond`；与 `DomainCheck` 互补 | 已验证 |
| `checks/ZenithPrototypeCheck/ArmoryUiRenderCheck.cs` | 渲染（静态） | K 渲染 | 在独立临时剑魂上调用真实绘制与按钮；归位与汇流共用同一事件年龄（先到剑位再进核心）；另留核心流光单独固定时间样本核对弯曲拖尾与加速吸入；开局与满支援两份独立样本避免绘制时伪造标签 | `InformationUiRenderCheck.cs:64` 调用 | 部分待核实 |
| `checks/ZenithPrototypeCheck/Assets/Baselines/SwordFlow0379.fxc` | 编译后的像素着色器（二进制） | 夹具资产·基线 | 0.3.79 剑流的冻结基线着色器，仅供隔离 GPU 对照读回，不接入生产模组 | provenance.json、checks/run_archived_vfx_capture.ps1 | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/Baselines/SwordFlow0380.fxc` | 编译后的像素着色器（二进制） | 夹具资产·基线 | 0.3.80 剑流的冻结基线着色器，与 0379 做回归差分对照 | provenance.json、SwordFlow0379.fxc | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/Baselines/provenance.json` | JSON 溯源记录 | 夹具资产·基线 | 登记两份基线 fxc 的来源（`artifacts/ZenithReborn-0.3.80-source.zip` 内的 `ZRSwordFlow.fxc`）与 sha256，并声明“仅隔离 GPU 对照，不进入生产模组” | SwordFlow0379.fxc、SwordFlow0380.fxc | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlass.fx` | HLSL 着色器源码 | 夹具资产·液态玻璃 | 液态玻璃研究原型：屏幕画布按宽 2 高 1 的等尺度表面坐标，条带只提供高度、由最终梯度形成同一法线，再算面光反射与背景折射；不接入生产刀光 | LiquidGlass.fxc、checks/compile_liquid_glass.py | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlass.fxc` | 编译后的着色器（二进制） | 夹具资产·液态玻璃 | LiquidGlass.fx 的 fxc 编译产物，由夹具在运行时加载 | LiquidGlass.fx、checks/compile_liquid_glass.py | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassPerfBase.fx` | HLSL 着色器源码 | 夹具资产·液态玻璃 | 性能检查的同网格基线：保留相同投影、采样、覆盖与混合，但跳过液态高度与光学计算，用于扣掉几何/带宽成本 | LiquidGlassPerfBase.fxc、checks/compile_liquid_glass_perf.py | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassPerfBase.fxc` | 编译后的着色器（二进制） | 夹具资产·液态玻璃 | LiquidGlassPerfBase.fx 的 fxc 编译产物 | LiquidGlassPerfBase.fx | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassTrail.fx` | HLSL 着色器源码 | 夹具资产·液态玻璃 | 真实轨迹网格试验：UV 梯度由检查端在世界像素空间按共享顶点汇总（不能用单位方向替代尺度）；世界到屏幕矩阵只含旋转与等比缩放，平移与投影不作用于折射方向 | LiquidGlassTrail.fxc、checks/compile_liquid_glass_trail.py | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassTrail.fxc` | 编译后的着色器（二进制） | 夹具资产·液态玻璃 | LiquidGlassTrail.fx 的 fxc 编译产物 | LiquidGlassTrail.fx | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/ParameterProbe.fx` | HLSL 着色器源码 | 夹具资产·探针 | 固定分支探针：按 `uProbeMode` 输出纯色，只用于确认浮点参数确实提交到了像素着色器 | ParameterProbe.fxc、checks/compile_liquid_glass_parameter_probe.py | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/ParameterProbe.fxc` | 编译后的着色器（二进制） | 夹具资产·探针 | ParameterProbe.fx 的 fxc 编译产物 | ParameterProbe.fx | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/Sentinel.fx` | HLSL 着色器源码 | 夹具资产·探针 | 哨兵着色器：恒定输出品红，只用于确认隔离测试中的像素着色器执行与读回路径通了 | Sentinel.fxc、checks/compile_liquid_glass_sentinel.py | 人工补写 |
| `checks/ZenithPrototypeCheck/Assets/LiquidGlass/Sentinel.fxc` | 编译后的着色器（二进制） | 夹具资产·探针 | Sentinel.fx 的 fxc 编译产物 | Sentinel.fx | 人工补写 |
| `checks/ZenithPrototypeCheck/Baseline79RenderCheck.cs` | 渲染检查（静态） | J 迁移 | `Run(string output)` 产 `baseline79.json`，用于 79 基线像素级对照 | 与 `Migration79RenderCheck`、`check_color79.py` 成组 | 部分待核实 |
| `checks/ZenithPrototypeCheck/BlessingCameraCheck.cs` | 静态断言套件 | C 诅咒 | 相机 padding：原生 `InitTargets` 同时接受 192 像素 padding 补丁与两处 target-cap 访问；1280 宽半缩放截图获得超过原 192 边距；正常缩放保持原始地形缓冲 padding | 反射 `InitTargets`；GPU 侧另见 `BlessingCameraCheck` 与 `BlessingGuiFixture` | 已验证 |
| `checks/ZenithPrototypeCheck/BlessingCheck.cs` | 静态断言套件 | C 诅咒 | 御剑祝福（38 条）：空表拒绝、15 秒有效战斗充满但不自动激活、满表 20 秒脱战不流失、换武器与死亡不清空、存量过存档/克隆/重铸/网络（精确载荷消费）、剑魂编辑不能误花满表、仅手动激活并消耗、起 10 秒、爆发不缩小已无界射程且激活中不可重启、距离倍率近身从 1 起、远端恰好 +50%、距离伤害不超 50% 上限、相机平滑到双倍视距不跳、剑魂编辑器暂停已激活爆发、爆发到期恢复相机与射程、仅释放/迁移旧魂不白拿祝福、训练木桩不能充能、不同主剑首击建真实 10% 中继、帧伤不重复吃首击中继、中继不再虚增木质标记只喂祝福伤害通道、中继上限 50%、3 秒无中继首击清加成、中继把山铜花瓣预算 30→33 不改冷却、原生完整回收武装同把宿主、回充不泄漏到别的宿主、充能首击发真实 50% 源挥砍、后续帧不重复消耗一次回充、回砍造成真实引擎伤害、祝福挥砍不递归充能矿石飞行、被打断/退役的刃不给回充、穿过实体砖武装 4 秒破甲 debuff、破甲真给 15 护甲穿透、每把发射剑消耗一次破甲且帧伤不复施、连斩祝福已替换旧剑痕爆炸不叠加、祝福后本体+3 剑影都用全量伤害、旧释放态不给新祝福加成、每个已祝福诅咒都解释一个已实现的额外机制 | `SoulBlessing`、`SoulBlade`、`Orichalcum` 预算 | 已验证 |
| `checks/ZenithPrototypeCheck/BlessingGuiFixture.cs` | ModSystem 遥测 | A 调度 | 只读遥测：等 `UiFixture` 备好剑魂后采集 `activated/rendered/samples` 写 `blessing-gui.json`；**不自动按键、不自动激活**，必须真实键位触发 | 依赖 `UiFixture` 先跑；`Main.dedServ` 直接跳过 | 已验证 |
| `checks/ZenithPrototypeCheck/BossUnlockUiCheck.cs` | 静态断言套件 | I UI | Boss 解锁界面（11 条）：亮暗只取个人铭刻（任意击败顺序正确）、阶段收益按累计数量点亮、未来奖励与当前说明共用同一来源、预览未来奖励不改剑魂数据、旧档解咒不伪造击败头像且不提前点亮祝福、双子只杀一只显示部分完成不加完整铭刻、已有铭刻但未附着时不宣称属性生效、普通特性不会误用 Boss 解锁列表、每个 Boss 都有真实多帧 Wiki 动画、动画尊重 GIF 原始时长并循环、六组 15 个挑战目标都有头像资源 | `SoulProgress` 铭刻、资产目录 `Assets/Baselines` | 已验证 |
| `checks/ZenithPrototypeCheck/BossUnlockUiRenderCheck.cs` | 渲染（静态） | K 渲染 | 在独立剑魂上覆盖六组封印及关键边界，只跑本次修改对应的界面渲染 | `InformationUiRenderCheck.cs:87` 的 `ZENITH_BOSS_UNLOCKS_GPU` | 部分待核实 |
| `checks/ZenithPrototypeCheck/Check.cs` | ModSystem 调度器 | A 调度 | 定义 `ZenithPrototypeCheck : Mod` 空类；`Assert(bool,string)` 抛异常式断言，成功才 ++；按 `ZENITH_*` 环境变量分派 14 条测试流水线，兜底跑内联 prototype 套件（本地化/槽位/深克隆/存档/网络/重铸/HERO/机械封印/十剑觉醒/交叉召唤掉落） | 被全部 `*Check.Run` 依赖；定义 `Assert` 唯一实现；`CurseFixture.NeutralDefaults` 在此翻转 | 已验证 |
| `checks/ZenithPrototypeCheck/ClearGlassRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 清透玻璃 GPU 检查，写 `clear-glass.json`；含 `ZENITH_CLEAR_GLASS_BASELINE` 与内嵌 `ZENITH_GLASS_HIGHLIGHT_PROBE` 分支 | `run_clear_glass.ps1`、`compare_clear_glass.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/ClearHighlightRenderCheck.cs` | 渲染（静态） | K 渲染 | 高光像素统计，写 `clear-highlight.json` | 与 `ClearGlassRenderCheck` 配对 | 部分待核实 |
| `checks/ZenithPrototypeCheck/CombatFixture.cs` | ModSystem 实机夹具 | A 调度 | 只在 `ZENITH_COMBAT_GUI=1` 且 `SavePath` 含 `stage-one-playtest` 时启用；`PostUpdateWorld` 第 60 tick 造 5 把矿剑剑魂 + 20 万血木桩，摆好光照/时间，产出两个确定性视觉检查点（bloom → 穿透飞行），Esc 切换 | 依赖 `OreFamily.DefaultType`、`SoulProgress.All/Cores`；纯客户端 | 已验证 |
| `checks/ZenithPrototypeCheck/CurseCheck.cs` | 静态断言套件 + GlobalItem | C 诅咒 | 文件内含 `CurseFixture : GlobalItem`（`NeutralDefaults` 开关，见 1.3）；六组诅咒全谱：新魂默认六诅咒且不看世界标志/兼容豁免（48 条）：阶段判定任意顺序、同一 Boss 不重复推进、单双子不算机械王、双双子算 1、外围 Boss 只记独立胜利不解咒、眼单独解绑且射程≥屏幕中点且不超已解射程、空封印仍算诅咒、切换只留第一个有效主剑、Brain 恢复全部特性不改独立伤害/不要求邪教徒、第一回归组 Boss 允许旧波继续、诅咒手势让旧主剑退役不删宿主、退役主剑视觉淡出无吸收、独行预算 2/3/常态4/祝福4、真实手势发射对应预算、祝福手势间隔可超 3 至 10、真实伤害路径限制 3/5/已解 重复命中、同刃可打不同敌人、每把新主剑独立命中字典、副剑命中不能绕过切换施加减益、第一切换组 Boss 恢复副剑减益、祝福下真实 ItemCheck 永不瞬时批量、祝福用唯一宿主并延续游标、原生手势 30 帧且稀疏宿主至 10、诅咒屏障挖约 2 格、弱化 4 格、扫掠地形不能穿墙、诅咒/弱化屏障挡住超 64 像素墙、已解恢复完整地形穿透、平台不静默挡剑、超额度立即截停、封印与显式遗留豁免持久不伪造击杀、全魂网络包保留诅咒记录且终点精确、克隆进度独立、旧魂可玩且不给 Brain/Eater 二义位、空旧残魂仍以新诅咒起步、哥布林重铸保封印与兼容态、真实前缀重建经 HERO 钩子恢复进度、真实遭遇区分 Brain/Eater、可选 Boss 进真实封印组、第二丛林 Boss 在 Golem 前完全移除屏障、第三丛林 Boss 记独立祝福阶段、外围胜利独立记录、剩余虫段阻止提前记账与重复交叉召唤、末段给待定个人封印并放行交叉召唤 | `SoulProgress`、`SoulCurse`、`CrossSummonDrops`、HERO 钩子 | 已验证 |
| `checks/ZenithPrototypeCheck/DamageBalanceCheck.cs` | 静态断言套件 | B 核心 | 伤害分配：初始阶段保 65% 宿主伤害、进度不能替代十材质觉醒、觉醒铜剑无共享天顶伤害地板、加强宿主不摊薄弱宿主、宿主伤害只加自己那把、面板/前缀不能替代宿主伤害、发射剑用自己伤害且不按槽数均分、暴击逐剑（含前缀）、击退逐剑、原生飞行时钟替代宿主速度且每帧跑两次、模型放大 60% 但碰撞尺度保持、玩家攻速不重缩放 AI、重生态与命中间隔过网络、非重生 8 帧间隔、引擎强制同刃间隔、他刃独立命中、弱刃冷却后可再命中、附魔剑与星怒同帧触发、特殊弹幕伤害各自取源、各自冷却 45 帧 | `SoulBlade`、`SoulPlayer.Flow` | 已验证 |
| `checks/ZenithPrototypeCheck/DomainCheck.cs` | 静态断言套件 | I UI | 全屏配阵/领域（29 条）：剑库只读真实合格背包武器（排除弓/工具/递归剑魂）、独立场景覆盖完整 UI 视口、配阵主页宽屏三区分开窄屏同区切换全在视口内、世界难度与退出控件可达、碎片导航不撞背包或难度控件、全屏场景移除隐藏背景悬停产生者但保留原生鼠标文本、真实原版光标层在全屏场景之上保原像素比例、手持光标物品在原生光标之上可见但不恢复隐藏背包、直接存入转移精确带前缀物品不克隆、替换把出向剑原子回原背包位、锁定主槽拒收剑库存入且不动源、剑库不能重开退役扩展槽、重复宿主校验也作用于快速装备、手持光标物品不能被剑库操作覆盖、空手星图点击只选择不抽进隐藏背包、起飞把精确剑还给真实背包、满包拒绝起飞不掉不删、遗留回收仍走新背包路径还给物品、剑库选择并存入真实第 6 个已得共享支援、切页保持活跃新支援选择而不与退役存储混淆、满领域能把暂停新支援带前缀还给真实背包、既有原生传奇世界不被误降级、难度按钮改真实原版 master/FTW 世界标志、存档保还原出处、二次点击还原精确原始难度与 FTW、旅程世界保其原生世界类型、客户端不能静默改多人世界、既有 Boss 战不能中途换属性基线、待定月总事件也阻切换 | `SoulUI.DomainMode`、`WorldGen` 标志位 | 已验证 |
| `checks/ZenithPrototypeCheck/EchoDamageCheck.cs` | 静态断言套件 | B 核心 | 三剑影伤害资格：本体停伤后剑影仍可攻、专用服务端能查到相同 3 个攻击姿态、单元比例、切剑影不绕冷却、剑影间空隙无整片拖尾伤害、本体+3 剑影各自消耗矿石首击、3 剑影共用锋钝每目标上限、熄灯后消散材质无伤、提前归鞘立即全关 | `SoulBlade` echo 段、`OreStack` 首击资格 | 已验证 |
| `checks/ZenithPrototypeCheck/EchoPhaseProbe.cs` | 探针（只测不判） | C 诅咒 | 实测"剑影亮灭时间窗"与"真实历史年龄"的偏差，用于确认与原版 933 一致。按 `SoulTrailUnits.Spacing=15` 采样、可见度按 `phase - index*Spacing`；每次更新推进 0.7~2 而非 1，故恒有偏差。文件头（`EchoPhaseProbe.cs:6-13`）明确写"这是原版行为不是缺陷，不要修" | 引用 `docs/原版天顶飞剑源码.md:111-113,400-405`；`AI_182_FinalFractal` | 已验证 |
| `checks/ZenithPrototypeCheck/EnvironmentAttributeCheck.cs` | 静态断言套件 | H 环境 | 环境属性元数据（15 条）：环境基底恰好用户定义的 21 个唯一稳定定义、环境 ID 往返与 E01-E21 一致、每个环境发布其调好的值与命名机制、未设/未知 ID 不是真实环境属性、确认的木质环境主题由真实加载钩子注册、空与未分配弓不发明环境标签、一把真实矿剑可接受多个独立存储环境属性（复制并去重作者输入）、非法作者输入不能部分替换既有武器绑定、类型元数据过真实物品存读档/克隆/前缀且不加每件存档状态、真实物品网络读看到相同元数据且不扩载荷、宿主属性视图同时覆盖普通活跃剑与通用支援、退役回收存储不静默并入活跃元数据视图、改环境元数据不换种族身份也不改伤害/套数/支援容量、传奇主剑通过既有活跃源枚举参与、元数据作者测试会还原原矿环境绑定 | `SoulEnvironment`（21 项）、`SoulItem` 绑定 | 已验证 |
| `checks/ZenithPrototypeCheck/EnvironmentCombatCheck.cs` | 静态断言套件 | H 环境 | 环境战斗机制（38 条）：21 个不同环境给加法 420% 成长、森林无削弱环境、森林固定 15% 只作用一次不自我递归、类型化被动在共享成长/森林因子前合并、活跃领域只给它对应环境 +50%、集齐所有环境不等于点亮所有机制、选定祝福在原生物群系外运行、真实命中修饰把溢出暴击转成伤害系数而非平加伤害、引擎 hit-info 真的把暴击乘溢出系数、休眠神圣保被动但不能转溢出、持久机制选择尊重 5 个真实解锁槽、环境账本/选择/领域一起克隆、真实 ItemIO 存读档保环境选择、真实网络往返消费完整 v8 环境载荷、重铸保全部环境进度与选择、HERO 重建保环境账本与选择、森林真养一片叶并消耗它换一次必闪、未熟叶不能反复无视伤害、冰机制产真实可吸收护甲资源、地狱伤害产真实治疗并花 life-steal 预算、月噬阻环境吸血、12 秒战斗产就绪蜂蜜资源、蜂蜜在受击后真给渐进治疗、独立机制分别执行、自有毒芽走引擎 NPC 回血伤害路径、后续帧命中不能额外加主 proc 计数、自然发现不能插非法环境位、换武器移除剑魂被动与机制、遗留回收物品不能制造环境收集进度、未收集环境不能被选为活跃领域、自然战斗收集神庙且命名群系压过通用洞穴深度、通用洞穴可在命名群系外收集、人造领域不能生成自己的自然探索发现、非法领域与超上限选择被安全归一、真实引擎弹幕伤害调用环境首击流水线、环境子弹幕造成真实引擎伤害、真实子伤害不能递归生成环境攻击、爆发根免疫在普通 NPC 免疫被清后仍有效 | `SoulEnvironment` 机制、`SoulBlade` 首击钩子 | 已验证 |
| `checks/ZenithPrototypeCheck/EnvironmentRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 环境属性视觉，写 `environment-gpu.json` / `environment-gpu-error.txt` | `ZENITH_ENV_RENDER` | 部分待核实 |
| `checks/ZenithPrototypeCheck/FlowParticleCheck.cs` | 静态断言套件 | B 核心 | 挥砍粒子：火花沿正向扫掠切线、镜像火花反向、传送不发射桥接粒子、非法姿态不发射、持久挥砍视觉受每次发射预算限制、离开挥砍重置发射器、脱离的尘埃无 projectile/父位置仍移动减速、脱离火花在父挥砍结束后仍按时消亡 | `SwordFlow` 粒子通道 | 已验证 |
| `checks/ZenithPrototypeCheck/FlyingBladeCheck.cs` | 静态断言套件 | F 飞刃 | 飞刃族（30 条）：可合成与掉落两组名册成员就位、弹幕剑与非常规不入族、名册 31 把、名册全部无引擎弹幕、族内无武器不激活、1 把即激活、首个蓄能周期叠出第 1 把、同周期不重复叠层、间隔 18 帧叠第 2 把、蓄能到独行上限待命、齐射掷出全部、掷出后清空重新蓄能、相邻飞刃相隔 5° 且速度一致、直飞 600 像素后进入旋转、旋转 45 帧存活、旋转结束转收回、收回途中不再造成伤害、射程/旋转周期/持续伤害/速度为暂定初值、2 把进第 2 阶并按数量放大伤害、4 把进第 3 阶、第 3 阶让非本族主剑也能掷飞刃、6 把进第 4 阶、第 4 阶把收回期伤害开关写进弹幕、第 4 阶收回期保留伤害并可命中、未解咒时族内数量照常统计、族激活时本体飞剑完全不发射、族激活时挥臂只掷飞刃且场上无本体飞剑、族无武器时取代不生效、族未激活时挥臂照常发本体飞剑、族未激活时挥臂不出飞刃、族激活但无蓄能飞刃时本次挥臂不取代 | `FlyingBladeFamily`、`FlyingBladeShot`；依赖 `CurseFixture.NeutralDefaults=true` | 已验证 |
| `checks/ZenithPrototypeCheck/FlyingBladeSurveyCheck.cs` | 普查探针（不判定） | F 飞刃 | 遍历 `1..ItemID.Count` 全部原版可寄宿近战物品，逐项记录 `Item.shoot` 弹幕字段与合成配方情况，写 JSON 数组（`FlyingBladeSurveyCheck.cs:32`）。注释（`:8-10`）明确只读引擎数据、不改行为不写档；走特殊机制而非 `shoot` 的挥舞弹幕会显示为"无弹幕"，需另行核对 | 消费 `SoulItem.CanHost`；产出 `flying-blade-survey.json`；决定名册"可合成/仅购买掉落"分组 | 已验证 |
| `checks/ZenithPrototypeCheck/FollowupCheck.cs` | 静态断言套件 | B 核心 | 后续回收路径：审计构造真能激活 11 个共享支援、掉到 0 授予时保留各自暂停命中目标、真实暂停槽点击取回自己那把、保留的传奇/皇冠 ID 不能撞未来第 15 个支援或重复可见槽、超越传奇/皇冠 ID 的未来暂停支援可取回且不碰传奇存储、图鉴主材质拆分与冷却文案与已确认诅咒例外一致 | `SupportIntegrationCheck` 的 11 支援上限；`TraitCheck` 图鉴 | 已验证 |
| `checks/ZenithPrototypeCheck/GlassBloomRenderCheck.cs` | 渲染（静态） | K 渲染 | 玻璃泛光（bloom）阈值/扩散对照，返回结果对象 | `prepare_glass_reflection_lowpass.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/GlassBufferReadback.cs` | 缓冲读回工具 | L 工具 | 把 `Texture2D` 读成 `Color[]`，支持 `SurfaceFormat.HalfVector4`（Half → Vector4 → Color）与普通路径。注释（`:6`）明确"原始线性数据只用于检查，不能当色调审看图" | 被 `LiquidGlassRenderCheck`、`RefractionRenderCheck` 等消费 | 部分待核实 |
| `checks/ZenithPrototypeCheck/GlassHighlightProbe.cs` | 探针 | L 工具 | 高光像素探针，写 `highlight-probe.json` | `ZENITH_GLASS_HIGHLIGHT_PROBE`（`ClearGlassRenderCheck.cs:58` 内嵌调用） | 部分待核实 |
| `checks/ZenithPrototypeCheck/GlobalUsings.cs` | global using | A 调度 | 7 条 `global using`（System/集合/IO/Linq/Text/Threading/Tasks）。无逻辑 | csproj `ImplicitUsings=enable` 的补充 | 已验证 |
| `checks/ZenithPrototypeCheck/HardOreCheck.cs` | 静态断言套件 | E 矿石 | 六种困难模式矿（37 条）：6 个不同困难矿剑 ID、矿剑可进攻击与支援槽、矿特性有可见描述；钴：同帧重复命中不充能、大目标内真实主击正确充能、第 3 次合法钴击发真实有界 40% 刃、子弹幕继承剑魂与真实源武器元数据、钴子弹幕造成真实引擎伤害、子击不充下一次钴 proc、下次钴 proc 仍需 3 主击；山铜：三花瓣共享 30% 总预算、花瓣 AI 跑动并用合法自有源、花瓣守自身 120 帧冷却、低伤花瓣不能乘最低 1 伤；秘银：不能同趟消耗自己新标记、别的剑用自己的命中倍率恰好消耗一次、单槽秘银可下次飞行消耗、过期标记无加成；精金：排除低于防御阈的目标、达阈给 12% 源修正；钛：碎片 30% 伤并真实绕源剑、源通道不能保留两片活碎、碎片随精确源飞行结束而结束；支援槽：换入接受已注册矿特性、支援钴用独立计数器与每主源真实伤害、支援矿永不发独立主剑、移除支援提供者立刻结束其子攻击、支援秘银造跨源标记且不借支援伤害；钯金：8 点滚动预算跨 5 源通道共享并花 life-steal 资源、6 秒窗过期后预算恢复、过期连击不留旧充能；虫：虫段共享一个每目标充能门、共享虫根可在后续合法帧充能；元数据过真实包序列化 | `HardOreTypes`、`HardOrePlayer.Clock`、`SoulBlade` | 已验证 |
| `checks/ZenithPrototypeCheck/HolyHeavenCheck.cs` | 静态断言套件 | G 神圣/星辰 | 神圣与星辰族（15 条）：名册边界、有效主槽激活族特性、界面显示有效族特性、独立种族筛选能找到落星特性、有效星辰支援可激活、取下唯一族成员立即失效、有效命中生成一颗落星、基础 1000 伤且从上方真实坠落、同一剑魂冷却阻止连续生成、真实弹幕直击产生伤害并转入冲击、冲击不重复伤害直击目标但命中范围内其他敌人、同一冲击不重复伤害目标、范围与初始冷却一致、世界清理后可再触发、方块撞击产生冲击 | `HolyHeaven` 族、落星弹幕、冲击波 | 已验证 |
| `checks/ZenithPrototypeCheck/InformationUiCheck.cs` | 静态断言套件 | I UI | 信息页布局（11 条）：信息卡在窄/宽屏保持实际可读宽度、多列卡片独立命中框不互覆、入场与悬停不改卡片命中区、信息页用完整工作区不被旧剑库边栏挤占、四个任务入口在信息页可见、主导航只有四项且旧物回收不常驻、旧图鉴页已删除（回收只在有旧物时可进）、列表与详情各自固定在工作区内不互覆、窄屏上下排列宽屏左右分栏、悬停动画单调收敛不闪烁不越界、短动画半秒内收敛端点固定 | `SoulUI` 信息页/图鉴页 | 已验证 |
| `checks/ZenithPrototypeCheck/InformationUiRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 信息/配剑/剑库界面实拍主入口：`ZENITH_INFORMATION_GPU`，内部分派 `ZENITH_ARMORY_INTERACTION_GPU`、`ZENITH_BOSS_UNLOCKS_GPU` 三条子路径，各写 `gpu.json`，异常写 `failure.txt` 并 `Exit(1)` | 调用 `ArmoryInteractionRenderCheck`/`ArmoryUiRenderCheck`/`BossUnlockUiRenderCheck` | 部分待核实 |
| `checks/ZenithPrototypeCheck/LegendaryCheck.cs` | 静态断言套件 | B 核心 | 传奇位：仅在真实传奇模式可见、不被当作支援槽寻址、接受主武器、计入活跃主武器、过存读档、过网络往返、退出传奇后暂停槽仍可见可取回、暂停态拒绝插入、空手可取回 | `SoulItem.Legendary` 索引；与 `WoodV2Check` 的传奇 34 号槽交叉 | 已验证 |
| `checks/ZenithPrototypeCheck/LiquidGlassGpuTimer.cs` | GPU 计时工具 | L 工具 | `IDisposable`，通过 P/Invoke 读 FNA 的 D3D11 设备做时间戳查询测显卡命令；不改图形状态、不释放借来的设备/上下文 | 独有消费方 `LiquidGlassPerfCheck` | 部分待核实 |
| `checks/ZenithPrototypeCheck/LiquidGlassPerfCheck.cs` | GPU 性能 ModSystem | K 渲染 | 液态玻璃性能采集，写 `liquid-performance.json`；唯一的**性能类**夹具 | `run_liquid_glass_perf.ps1`、`compile_liquid_glass_perf.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/LiquidGlassRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 液态玻璃外观主检查，写 `liquid-glass.json` | `run_liquid_glass.ps1` | 部分待核实 |
| `checks/ZenithPrototypeCheck/LiquidGlassTrailGeometryCheck.cs` | 几何校验（静态） | M 数学 | 旋转 + 等比缩放后连续 UV 梯度应随坐标作逆尺度变换；两组用例 `(.75, .43)` 与 `(1.5, -.61)`；输出 `Scale/Rotation/VertexCount/MaxAbsoluteError/DegenerateTriangles/UnrepresentedVertices/Passed` | 配对 `LiquidGlassTrailMesh`、`LiquidGlassTrailRenderCheck` | 已验证 |
| `checks/ZenithPrototypeCheck/LiquidGlassTrailMesh.cs` | 网格结构 | L 工具 | 定义拖尾顶点结构与 `VertexDeclaration`（含自定义 `Vertex` 结构），供渲染与几何校验共用 | 被 `LiquidGlassTrailRenderCheck`、`LiquidGlassTrailGeometryCheck` 消费 | 部分待核实 |
| `checks/ZenithPrototypeCheck/LiquidGlassTrailRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 液态玻璃拖尾：先导 `trail-mesh.json`（逐顶点），再写 `liquid-trail.json` | 依赖 `LiquidGlassTrailMesh`、`LiquidGlassTrailGeometryCheck` | 部分待核实 |
| `checks/ZenithPrototypeCheck/LivingArmamentCheck.cs` | 静态断言套件 | G 活武 | 活武族（11 条）：新种族与神圣/矿石/木质同时生效、每个有效出战槽各有召唤体、重复更新不重复召唤、出战武器通过玩家近战流程真实命中目标、种子弯刀通过原版物品检查发射、星怒通过原版物品检查发射、飞刀从玩家通道发射、飞刀继续由代理持有并恢复真实玩家、代理结束后保留真实玩家物品/位置/鼠标、失去激活成员时召唤体退出、召唤体消失后飞刀退出代理并恢复真实玩家 | `LivingArmament` 代理玩家；`ZENITH_LIVING_ARMAMENT` | 已验证 |
| `checks/ZenithPrototypeCheck/LivingBatHealCheck.cs` | C# 夹具（静态断言） | 夹具·活体武器代理 | 对照原版蝙蝠棍命中回血：独立服务端构造真人与代理两种 Trial，断言真人基准命中回血 1、代理命中后真实玩家回血 1、同一次挥击不重复回血、满血不超上限、无效目标不触发、真实玩家普通使用不受转发影响；用占满 combatText 槽规避无字体资源的绘制 | ZenithReborn/LivingProxyOwnerState.cs（LivingProxyHealing）、Check.cs | 人工补写 |
| `checks/ZenithPrototypeCheck/LivingMeleeMatrix.cs` | 矩阵探针（不判定） | G 活武 | 对同一固定目标与武器，逐项比较"真实玩家"与"生产代理"两条路径，记录未能验证的情况，写 `ZENITH_MELEE_MATRIX_OUTPUT` JSON（`LivingMeleeMatrix.cs:73`）。重置 `Main.rand=UnifiedRandom(318)`、`projectile/npc` 全灭、`netMode=SinglePlayer` 保证可重复 | 需要 `CurseFixture.NeutralDefaults=true`（`Check.cs:53`）；产出 `matrix.json` | 已验证 |
| `checks/ZenithPrototypeCheck/LivingParryCheck.cs` | C# 夹具（剑灵招架） | 验证体系 | 对照真人与剑灵代理的真实近战命中与一次性招架增益，断言两者命中判定一致 | LivingWeaponProxy.cs、LivingArmamentCheck.cs | 人工补写 |
| `checks/ZenithPrototypeCheck/LivingProjectileOwnershipCheck.cs` | C# 夹具（静态断言） | 夹具·活体武器代理 | 从生产召唤槽发射真实原版弹幕（铜短剑/长矛/附魔回旋镖/泰拉刃/飞刀/吸血鬼刀），核对代理身份绑定与还原：断言弹幕由召唤体实际生成、依附型身份判定（吸血鬼刀除外）、AI 后恢复真实玩家与鼠标位置、近战弹幕位置依附远处代理、飞刀在召唤体结束后恢复真实玩家回收 | LivingChannelProjectile.cs、LivingArmament.cs、Check.cs | 人工补写 |
| `checks/ZenithPrototypeCheck/LivingWallCollisionCheck.cs` | C# 夹具（视线遮挡） | 验证体系 | 用真实方块与弹幕伤害路径比较玩家与剑灵代理的视线判定，防止代理穿墙出手 | LivingWeaponProxy.cs、LivingParryCheck.cs | 人工补写 |
| `checks/ZenithPrototypeCheck/Localization/en-US_Mods.ZenithPrototypeCheck.hjson` | 本地化文件（空） | 夹具工程元数据 | 夹具模组的英文本地化表，当前是空文件（2 字节）；夹具文案全部硬编码在 C# 里，不走本地化 | checks/ZenithPrototypeCheck/Localization/ 目录 | 人工补写 |
| `checks/ZenithPrototypeCheck/LunarCheck.cs` | 静态断言套件 | G 神圣/星辰 | 月亮事件恢复（22 条）：复现原版"无事件标志则无倒计时即使四柱已尽"、三柱不能触发恢复、四柱真实击杀在无原版标志下恢复 60 秒倒计时、既有原版倒计时永不被重置为 60、世界重载恢复剩余倒计时与原最大值、已观测到的月总消耗恢复使 Boss 死后不重生、完成战斗不自动重复、活柱即使历史说四柱全死也阻恢复、恢复等活着的活跃玩家、延迟恢复在玩家活后继续、客户端不能本地起权威 Boss 倒计时、仅历史柱标志永不自动召唤旧世界、手动修复不能绕过未完成柱史、已完成的旧世界显式修复起一次正常倒计时、重复修复不复制不重启待定召唤、原生天界符倒计时被同一持久钩子捕获、新柱循环不继承旧的待定召唤、排队修复夹具限在隔离 profile、排队恢复永不作用于不同 world GUID、匹配世界恢复应用一次后归档、原版倒计时到期真造月总核心 NPC、成功原版生成清持久待定态 | `LunarEvent`、`WorldFileData` GUID、`ZENITH_LUNAR` | 已验证 |
| `checks/ZenithPrototypeCheck/MaskCompositionPreview.cs` | GPU 预览 ModSystem | K 渲染 | 贴图候选枚举 + 合成预览：先写 `assets.json`，`ZENITH_MASK_ASSETS_ONLY=1` 时只枚举；`ZENITH_MASK_SOURCE` 默认 `Flames`；再写 `preview.json` | `run_mask_preview.ps1`、`prepare_material_maps.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/MaterialTrailCheck.cs` | 静态断言套件 | B 核心 | 材质拖尾：有界三角几何、沿真实非圆路径、相机平移不滑材质坐标、自有 shader 吃普通距离坐标、每姿态生命期 alpha（含最旧透明样本）、结束坐标独立于材质平铺、父历史被覆盖后仍保留真实路径、冻结结束随相机不动世界、结束按满密度腐蚀、进度单调且与剑速无关、最旧锥形收零、无路径前不出材质、内边界固定姿态下推移、动画内边界不动真实刃尖、随剑可见度渐显、正常收回无残料、静止姿态不铺满、非法动画时间/旋转/传送被拒；剑影保 3 个原版历史采样、4 个剑尾窗口递减且严格留白 | `MaterialTrail`、`SwordTrail`；`EchoPhaseProbe` 的对照基准 | 已验证 |
| `checks/ZenithPrototypeCheck/Migration79RenderCheck.cs` | 渲染检查 ModSystem | J 迁移 | 恢复操作直接对照"收藏 79"的编译着色器，不用新版本美术阈值重新定义 79；写 `result.json` / `error.txt` | `Assets/Baselines` 的 79 基线；`ZENITH_MIGRATION_79_RENDER` | 部分待核实 |
| `checks/ZenithPrototypeCheck/MoonWorldProbe.cs` | 存档探针 | L 探针 | 直接读原始世界文件：`new WorldFileData(input,false)` + `BinaryReader` + 反射取静态字段，产 `ZENITH_MOON_REPORT`；异常写 `FAIL: + error` 并 `Exit(1)` | `run_*` 月相相关脚本；只读不写入 | 部分待核实 |
| `checks/ZenithPrototypeCheck/NativeParticleCheck.cs` | 静态断言套件 | B 核心 | 原版粒子对齐：原版剑用其真实安装的 delegate/颜色/宽度、原生 AI 与剑魂每子更新产尘数一致、生成尘埃的贴图/类型/颜色/缩放/淡出/重力/alpha 匹配、同随机种子下位置与初速一致、比较确实走了真实产尘（非空发射）、附加宿主经原生实现保调色板、剑尖星光有原始相位时序 | `Main.rand` 种子；`UnifiedRandom` | 已验证 |
| `checks/ZenithPrototypeCheck/OreCheck.cs` | 静态断言套件 | E 矿石 | 矿石族主干（53 条）：短/宽剑算 1 材质、10 矿石主剑无木给 6 通用支援、4 合法矿石支援与 10 主剑并存且排除退役存储、旧扩展数据可安全恢复、遗留普通/支援存储拒收、全部非活跃遗留项过存读档不截断、遗留存储网络精确消费、深克隆、重铸保非活跃扩展武器、8 个旧槽都能空光标取回、清空后回收页消失；飞行伤害 150% 基 + 材质缩放 + 十阶加成、1 次真实首击恰好造 1 把矿剑不是 6 把、部分收藏在首刃成形后仍hold fire、同一发射主剑的重复帧伤不能加剑、6 次独立首击收 6 种材质、收齐放整轮齐射、新旧收集剑等同最后一剑沉降充能、全部同时以初始加速度发射、远距离阵保留成长后沉降动画、显示放大不改原碰撞、真实宿主剑有可见正常绘制路径、真实命中穿透多敌且保速度、同剑同帧不能二次命中、6 帧重复伤害可用且不递归造剑、新主剑首击不被别的剑 proc 挡、同一主剑不能在后续帧重试、非法触发器/复用目标槽/非法源被拒、原生 Update 飞行能打带防御的移动目标、高防命中保源伤害份额而非最低 1、目标死亡把部分齐射放向最后位置而非删除、被打断的部分收藏超时后释放、暂停与回收槽不登记收藏、未附着预览不登记、连续真实有效寄宿收 14 材质不需额外槽、精通移除后仍持久并解锁 2 共享奖励槽、纯矿精通激活第一奖励槽并保武器前缀、纯矿精通激活两槽、矿石精通不要求也不伪造森林之力、两收藏族共享 2 奖励槽不叠成 4、去木撤森林之力但矿精通保共享奖励槽可用、收藏过存档/克隆/网络（精确包）/重铸保护、HERO 前缀重建恢复累计收藏、旧档不从 Boss 进度或非活跃槽推断满收藏、精通真给新波 30% 伤害 +50% 飞行距离、发射剑越过已死原目标跨精通射程继续、波相位/精通/距离过网络、精通飞剑在有界距离终止 | `OreFamily`、`SoulItem.Ore`、`SoulBlade` | 已验证 |
| `checks/ZenithPrototypeCheck/OreCombatFixture.cs` | ModSystem 实机夹具 | A 调度 | `ZENITH_FIX_GUI=1` 下的矿石实战采集：`ore-fix-combat.json`、`ore-tier-combat.json`、第 233 tick 的 `cursor-ring-validation.json`、共享支援 `ZENITH_SHARED_SUPPORT_GUI` | 依赖 `SoulUI.EditingSoul`；纯客户端 | 已验证 |
| `checks/ZenithPrototypeCheck/OrePierceCheck.cs` | 静态断言套件 | E 矿石 | 巨剑余波与剑体开关（7 条）：非法结束不进图形历史、巨剑余波存活超过小刃生命期且保接触独立于末刃姿态、独立余波无活武器也会过期、密集结束事件有固定内存预算、世界清理也清武器结束；剑体关附魔粒子但保留减益：关剑体粒子后原版火焰附魔仍能命中施加 | `OreGiant` 余波环形缓冲；`SoulBlade` 附魔开关 | 已验证 |
| `checks/ZenithPrototypeCheck/OreRewardCheck.cs` | 静态断言套件 | E 矿石 | 矿石奖励与飞剑全谱（54 条）：1 材质解锁飞行且无隐藏主伤加成、1 材质真造首把飞剑、真实 1 材质飞行弹幕拿 100 伤父的 150%、150% 不把敌防乘 1.5、真实非矿主剑经 1 个活跃矿支援充 150% 飞剑、单活跃矿主剑为非矿主剑解锁首击充能且保真实源、非矿主剑单独不能造矿飞剑、切换允许 4 主限内第二把、错峰真实首击在共享预发射阵造 6 把不同龄剑、6 个真实环位等角含闭合缝、二阶/四阶给每矿主 5%/10% 且不乘数量、矿主加成不改无关剑伤害、六阶飞行折返并保伤第二趟、第二趟有限距离不死循环、5 材质拿不到六阶回攻、八阶飞行钴首击立即触发自身既有 proc、飞剑子弹幕保自身材质并通过真实源校验、补充材质用自身小份额缩放敌防、飞行帧伤不重复触发材质效果、八阶飞行山铜发既有三花瓣、钛碎片绕真实飞剑父、飞行钯用真实治疗并花既有 life-steal 预算、钯帧伤不重复治疗同一飞剑、飞行秘银标记被另一真实源剑消费、飞行精金用自身防御阈值、材质子弹幕不能递归充另一飞剑阵、7 材质不泄漏八阶效果、十阶矿支援让非矿主剑恰好充 1 把、10 次被支持首击完成一次十材质阵、十阶伤害与原生移速都 +50%、阶/动龄/额外趟过网络精确消费、掉到 10 以下保留非矿主充能但立即撤十阶速度、切换把矿主限到 4 而普通特性只留首主、同帧 10 个不同主击只出 4 把诅咒飞剑且无共享冷却、飞行限制保留结构支援与全直伤主剑、第一切换组 Boss 完全恢复矿石计数与触发资格不等祝福、已解切换允许第一把以外的主剑充能、第五把与第一把同类型也不能绕限、允许的主剑把真实源位置传给飞剑、弹幕网络包保源序号并消费整条消息、空普通槽不占 4 个有效主位、诅咒与图鉴都解释四主例外、满收藏给完整齐射加真实巨剑、巨剑有显式大额源缩放伤与无害预警、预警 30 帧、落下造成真实引擎伤害且首触不消失、巨剑不能每帧重复乘同一目标伤害、满收藏巨剑守共享 5 秒冷却、冷却后下次齐射再召巨剑、被打断的部分齐射不能触发完成才有的巨剑、加速原生移动 + 扫掠碰撞能打中 8 像素目标来去两趟、原生弹幕前几帧加速而非匀速滑、加速出程显著快于旧 49 tick 滑、巨剑预警/下落/原生碰撞产一次大命中 | `OreFamily`、`OreReward`、`OreGiant`、`SoulBlade` | 已验证 |
| `checks/ZenithPrototypeCheck/OreStackCheck.cs` | 静态断言套件 | E 矿石 | 铜锡叠层（20 条）：有效铜锡材质激活两项成阵联动、本体与各实体剑影原生命中各 +1 飞剑、叠层更新已存在与新生成员、同阵全剑拿当前层数增伤、同一单元重复帧伤不重复积攒、4 种矿材保留独立弹幕不合并、4 层齐射快照 = 基础 172×1.15 而不是逐层复乘、亮度动画连续不越界闪白、12 帧达目标亮度 95%、飞剑保原始伤害快照/层数/防御份额、接收层数从当前显示进度缓动不跳亮、新阵从 1 层与自身伤害重开且已释放阵不续叠、新旧波伤害隔离各自保倍率、无铜特性剑影仍伤害但不积百炼、剑影命中不提前吃本体资格、无锡特性多剑保原伤不提亮、切换诅咒的前四矿材例外不放开铜锡普通特性、有效支援里的铜锡短剑也提供同材质联动、未附着预览不给矿石特性、14 层上限 +65% 且异常层数不无限增伤 | `OreStack`、`StackGrowth`；与 `EchoDamageCheck` 首击资格交叉 | 已验证 |
| `checks/ZenithPrototypeCheck/OreStackRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 矿石叠层亮度/提亮渲染，写 `gpu.json`，异常写 `failure.txt` 并 `Exit(1)` | `ZENITH_ORE_STACK_GPU`；对应 `OreStackCheck` 的服务端断言 | 部分待核实 |
| `checks/ZenithPrototypeCheck/PrefixProbe.cs` | 无头探针 | L 探针 | `-server` 下精确报告是哪个 gate 挡住了剑魂的 `PrefixID.Legendary`，写 `ZENITH_PREFIX_REPORT` | 与 `Check.cs:228` `CanApplyPrefix(Legendary)` 断言配对 | 部分待核实 |
| `checks/ZenithPrototypeCheck/PreviewColorNoise.cs` | 数学噪声工具 | L 工具 | 纯数学 hash 噪声（128×128、8 格、`374761393`/`668265263`/`1274126177` 常量），无外部图片、不装入生产模组 | `prepare_material_maps.py`、`generate_noise.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/ProgressionCheck.cs` | 静态断言套件 | D 木质 | 进度与引导（33 条）：新玩家得友好王令、中核死亡不复制开局礼、王令无史莱姆也能开友好遭遇、王令解释白天限制、真实 `NPC.NewNPC` 在隔离图格生成友好王、友好王无害不可杀可直接对话、重复用令不刷复制王、NPC 直聊推进一条故事线、给羁绊与第一普通槽、友好祝福不伪造原版 Boss 击败/进度、祝福不可二次、祝福后王安全消失、通关玩家可召回友好向导、召回王只给进度与寄宿不给第二次祝福、晚期 Boss 单独不能跳早期槽门、新未祝福魂不能吸收/回收魂核、正确已击败魂被选中给核、核解锁目标槽不改另一魂、眼击杀给精确缺核与回收、下一步推进到邪恶 Boss 第三槽、邪恶 Boss 解锁第三槽且不重复给核、骷髅王用复用黎明核开第四槽、肉墙等血肉核、单双子不够机械核/第六槽、邪教徒用复用神庙核开第九槽、所有槽门通向真实永久觉醒目标、已完成核回收不可重复、每普通/支援/奖励槽都有可用条件提示、回收在可用时消耗既有核、解锁后回收按钮不能再花核、丢核可从该魂已得胜利重建、无主魂身份不能回收、每个真实木质阈值解锁对应支援槽 | `SoulProgress`、`SoulGuide`、`SoulCore` | 已验证 |
| `checks/ZenithPrototypeCheck/PureCodeVfxRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 纯代码特效（无贴图资产）渲染检查，写 `pure-code.json` | `run_pure_code_vfx.ps1`、`prepare_pure_code_package.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/RefractionRenderCheck.cs` | 渲染（静态） | K 渲染 | 相同背景/网格/透明度下比较折射率，禁止用颜色或透明度变化冒充折射；输出 `Result` 含 IOR 差、锐/粗高光像素、Ior/Roughness/Transmission | `SpaceWarpRegionCheck` 同族；`check_glass_*` 系列 | 部分待核实 |
| `checks/ZenithPrototypeCheck/RemovedSystemsCheck.cs` | 静态断言套件 | J 迁移 | 已移除系统回归（8 条）：已移除的系统不再注册、木剑/矿石剑/前缀经旧档读写保留、原灵能名册武器仍可存支援槽、旧武技字段可回退但不再参与战斗、旧武技字段保网络末尾三字节格式、网络数据完整读取且保留旧字段、网络同步保木质与矿石寄宿物品、重铸保旧字段与寄宿物品 | `SoulItem` 遗留字段、`archived-removed-systems/` | 已验证 |
| `checks/ZenithPrototypeCheck/RibbonCheck.cs` | 静态断言套件 | B 核心 | 剑尾几何：有界三角形、贴近武器路径不横跨屏幕、按刃宽淡出、尾端透明、相机平移不改尺寸、不足 2 个采样不画、隐藏剑不画、传送断点不连成整屏、非法旋转不进 GPU；飞剑拖尾 newest-first 有界、尾部渐透明、传送不连屏、非法采样丢弃；阵型 20 帧淡入 | `SoulTrailUnits`、`Soul` 网格；也用于 `ZENITH_GLASS`/`ZENITH_ORE`/`ZENITH_STAGE_ONE` | 已验证 |
| `checks/ZenithPrototypeCheck/SoulFlightWorldCheck.cs` | 实机世界夹具 | K 渲染 | 最大型实机夹具：在隔离世界驱动剑魂飞行。开关矩阵含 `ZENITH_WORLD_LIGHT_PRESET`（day 等）、`ZENITH_SURFACE_WORLD`、`ZENITH_SOUL_INPUT_CHECK`（真实输入）、`ZENITH_REQUIRE_INPUT_ARM`、`ZENITH_ECHO_DAMAGE_CHECK`、`ZENITH_WORLD_MOVING` | `run_soul_flight_world.ps1`、`pick_soul_flight_frames.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SoulTrailMotionCheck.cs` | 静态断言套件 | B 核心 | 剑影相位一致性实测：移动/静止主体相对路径一致、移动不改变剑影可见时间、剑影随主人平移且形状朝向保持、平移前后碰撞一致、覆盖"可见"剑影、大距离传送清旧轨迹 | `EchoPhaseProbe` 的结论验收；`ZENITH_TRAIL_MOTION` | 已验证 |
| `checks/ZenithPrototypeCheck/SoulTrailRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 剑魂拖尾实拍，写 `gpu.json` | `run_soul_trail_render.ps1` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SpaceWarpRegionCheck.cs` | 几何校验（静态） | M 数学 | 用积分图（前缀和）算覆盖邻域，做**保守边界**验收：`ChangedPixels`/`OutsideBladeChangedPixels`/`BeyondReachChangedPixels`/`Difference`。注释（`:5`）明确不复刻着色器里的采样方向与压力公式 | 配对 `SwordSpaceDistortionCheck`；`run_surge_preview.ps1` | 已验证 |
| `checks/ZenithPrototypeCheck/SparkCheck.cs` | 静态断言套件 | B 核心 | 自定义剑粒子：16 像素可见精灵且发光、黑暗中保持自发光不继承场景暗度、辉光独立于网格拖尾漂移淡出、有界生命期与褪色 | 自有粒子类型；与 `FlowParticleCheck` 分属"自制"与"原版对齐" | 已验证 |
| `checks/ZenithPrototypeCheck/StageOneCheck.cs` | 静态断言套件 | B 核心 | 第一阶段全流程：新魂不稳定、可预览不可攻击、旧魂迁移保值、守护者友好、对话前置、接受末行才给羁绊、不伪造 Boss 击杀/魂核、祝福开 1 槽 + 65% 强度、不可重复领、祝福+前缀持久化、网络持久化、哥布林重铸保值、冰剑给霜burn/草剑给毒、可原样取回 | `SoulProgress`、`SoulItem.LegacyReleased`、`SoulGuardian` | 已验证 |
| `checks/ZenithPrototypeCheck/SupportIntegrationCheck.cs` | 静态断言套件 | E 矿石 | 共享支援池（27 条）：任意已注册或普通近战剑走真实通用支援转移、矿支援在 1 而非 10 解锁飞行、非木装配开通用支援阈且不造木 buff、真实纯矿支援插入接受材质、10 矿主 + 4 真支援产全 14 材质与共享奖励槽（无木）、活跃 UI 报真实主/支援材质贡献与当前确认伤害基、第 6 个已得支援用新存储、旧遗留 S6 永不静默激活、通用容量与全支援材质过真实存读档、新 S6 与退役 S6 存读档后仍分离且前缀精确、共享支援往返精确材质总量并消费扩展网络模式、新支援存储深克隆、重铸保新第 6 支援与前缀、HERO 重建把新得支援与退役存储分开并保前缀、混合种族池收木支援、混合种族池收额外矿支援、5 木 + 6 矿相加 11 共享支援而不是取最大或夹到 5、全部 6 个额外支援槽过序列化不截断在原 5、失全部锚点暂停每项授予但留额外道具可安全回收、暂停的第 11 支援有真实空光标取回路径、旧档不把禁用的扩展内容重解释为已得新支援、矿短/宽变体跨共享支援边界算 1 材质、锁定/奖励/退役存储不能自举通用支援或材质总量、最少活跃前缀只含 3 个可达支援并安全保留后续暂停项、切换诅咒压制支援战斗特性而不毁结构解锁、森林奖励槽计入族总量且无奖励槽例外、切换诅咒解除后战斗计数与全集一致、确认独立飞行快照对 100 伤源为 150/326/482 | `SoulItem.Supports` 扩展存储；与 `FollowupCheck` 的 11 支援上限交叉 | 已验证 |
| `checks/ZenithPrototypeCheck/SwingCheck.cs` | 静态断言套件 | B 核心 | 复刻原版 `ItemCheck` 射剑时钟：与真实 Zenith 在相同前缀/攻速下 tick 与动画计时一致；跨 10 宿主边界不重置；一次机会只射 1 把（永不批量）；0/10/20 出手、第 30 帧复用；稀疏装不重复宿主；空节点留空；缺宿主时不射；仅 owner 客户端造剑；背包刷新保持前缀时序；逐剑调色板保留 | `SoulPlayer`、`PrefixID`、`SoulBlade` | 已验证 |
| `checks/ZenithPrototypeCheck/SwordAirflowMask.cs` | 遮罩生成器 | L 工具 | H3 动态 Alpha 气流遮罩：`IDisposable`、128×256、复用原版贴图与 SpriteBatch；按轮廓与流向编排的**近似**（注释 `:5-6` 明确不冒充完整流体模拟），含 `distance/upper/lower/solid` 场与 `loop/length/viscosity` 参数 | 被 `AirflowAtlasBatchCheck`/`AirflowMaterialCheck` 消费；`emit_vfx_mask_data.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SwordBodyMaskPreview.cs` | GPU 预览 ModSystem | K 渲染 | 剑体遮罩预览/导出：读 `ZENITH_SWORDBODY_PREVIEW/ATLAS/AUTHORING/REVISION/COLORFIELD/LOCAL/EMISSIVE/ITEM`，产出 `profile.json` 与预览图；`REVISION` 默认 R1 | `SwordBodyVectorMask`、`register_swordbody_vector.py`、`pack_swordbody_mask.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SwordBodyVectorMask.cs` | 遮罩生成器 | L 工具 | 向量化剑体遮罩，产 `softblade-diag.txt` 与 `field-diag.txt`（尺寸/solid 计数/maxDist 诊断） | 配对 `SwordBodyMaskPreview`；`register_swordbody_vector.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SwordColorNoiseCheck.cs` | 静态断言套件 | M 数学 | 底色噪声 4 条：同参数可重复生成、只提供不透明灰度不覆盖逐剑颜色与形状透明度、有可见明暗变化、慢底色两轴循环接缝连续 | `PreviewColorNoise` 的验收；`repoint_colorfields.py` | 已验证 |
| `checks/ZenithPrototypeCheck/SwordFlowMeshBasisCheck.cs` | 静态断言套件 | M 数学 | 网格基 6 条：连续朝向不移动顶点/不改 UV/不改逐顶点覆盖度、共享顶点的全部三角形副本朝向完全一致、连续朝向单位化且无非法数值、观察矩阵旋转正确传到屏幕空间朝向、空间重合但 UV 不同的表面不被错误焊接、退化尾尖用稳定中性朝向不除零 | `SwordFlow` 网格；`SwordFlowMeshBasis` 生产侧 | 已验证 |
| `checks/ZenithPrototypeCheck/SwordFlowPaletteCheck.cs` | 静态断言套件 | M 数学 | 调色板 4 条：剑色明暗锚点有限且在有效色域、暗部/中间调/高光感知亮度依次递增、有色剑三锚点都保留色彩而非叠白、不同剑色生成各自明暗阶 | `SwordFlowPalette` 生产侧；`check_sword_color_delivery.py` | 已验证 |
| `checks/ZenithPrototypeCheck/SwordHighlightRenderCheck.cs` | 渲染（静态） | K 渲染 | `Run(Func<string,int,int,Color[]> render)` 高光渲染对照，返回 `Result` | 被 `SwordSpaceDistortionRenderCheck` 同族调用 | 部分待核实 |
| `checks/ZenithPrototypeCheck/SwordSpaceDistortionCheck.cs` | 静态断言套件 | M 数学 | 空间扭曲几何 6 条：位移有界且小于影响半径、非法时钟/相机输入不产偏移、相机原点与时间相位显式传递且不改强度、屏幕缩放经逆变换还原世界相位坐标、奇异观察矩阵安全关闭扭曲、独立开关且受纯代码屏幕输入限制 | 配对 `SwordSpaceDistortionRenderCheck`、`SpaceWarpRegionCheck` | 已验证 |
| `checks/ZenithPrototypeCheck/SwordSpaceDistortionRenderCheck.cs` | 渲染（静态） | K 渲染 | `Run(output, coverage, width, height)` 空间扭曲渲染对照 | 配对 `SwordSpaceDistortionCheck` 的几何断言；`run_surge_preview.ps1` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SwordTrailHeadJoinRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 剑尾头部接合实拍，写 `head-join.json` | `run_sword_head_join.ps1`；配对 `SwordTrailInnerContourCheck` | 部分待核实 |
| `checks/ZenithPrototypeCheck/SwordTrailInnerContourCheck.cs` | 静态断言套件 | M 数学 | 内缘轮廓 3 条：头到旧尾连续变窄不形成先缩后鼓的多余扇面、头部保留刀身连接长度（不撑到整把武器外）、内缘曲线由几何统一负责不再用接合权重覆盖材质边界 | 配对 `SwordTrailHeadJoinRenderCheck` | 已验证 |
| `checks/ZenithPrototypeCheck/TexturedSwordMaterialRenderCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 贴图剑材质合成实拍，写 `textured-material.json`（缩进格式） | `run_textured_sword_material.ps1` | 部分待核实 |
| `checks/ZenithPrototypeCheck/TraitCheck.cs` | 静态断言套件 | I UI | 特性与图鉴（25 条）：未附着预览永不显示活跃特性、图鉴在无装备时保全部 8 木 + 6 实现困难矿效果、每条图鉴有唯一 key/当前描述/激活条件、有效木主剑 + 钴支援激活对应效果、一个活跃矿支援经共享机制解锁飞行且不发明满森林或缺木、六个当前诅咒态替换过时机械诅咒条目、锁定的重复矿主不改变合法支援提供的 1 材质、已解锁矿主激活波且兼容态恢复正常能力不发明个人祝福、回收存储与暂停森林槽不把效果漏进摘要、有效支援族出现在活跃摘要、失木解锁立即暂停冰支援、满有效木集显示 8 效果与森林之力、满森林装配只留 3 行紧凑 tooltip（详细在 UI）、每条自定义 tooltip 短且单行、长宿主名不撑大剑魂 tooltip、去木在真实 tooltip 隐藏其效果与森林之力但保留图鉴链接、图鉴分类筛选返回正确实现条目、生效筛选只显示当前实际生效条目、同一工作区可恢复全部特性及激活条件、浏览筛选图鉴不改存储物品或进度、目录不能吃槽点击或存光标物品、有旧物时也不加主导航（回收由配剑区提示进入）、配剑区回收视图保旧物原引用（取空后不再有旧物）、脱离的存储进度永不宣称可用能力、未附着 tooltip 只加一条短"下一步"提示 | `SoulTrait` 图鉴、`SoulItem` 特性摘要 | 已验证 |
| `checks/ZenithPrototypeCheck/UiCheck.cs` | 静态断言套件 | I UI | 面板布局与点击（18 条）：轨道导航在裁剪宿主画布内、关闭按钮留屏、槽水平边界与可用目标、槽永不重叠、10 主槽共享一条圆轨道、森林奖励槽占左上弧、支援槽占右下弧、碎片导航不重叠任何武器或碎片目标、真实可见 handler 在缩放下插入并保前缀、真实 handler 返回预览武器、被拒物品留光标、隐藏槽不能吃其他页点击、体外不能换武器、17 个槽索引都能正确取回暂停项、Tab 导航消费点击不关编辑器、导航永不存光标物品、关闭按钮不动光标物品 | `SoulUI.SlotBounds/TryClickHost`、`Main.UIScale` | 已验证 |
| `checks/ZenithPrototypeCheck/UiFixture.cs` | ModSystem 实机夹具 | A 调度 | 三条互斥路径：①`ZENITH_DRAG_REPLAY=1` 第 120-140 帧合成拖拽并写 `window-drag-replay.json`（注释明确声明"非 OS 拖拽证据"）；②`ZENITH_UI_GUI`/`ZENITH_ORE_GUI`/`ZENITH_VISUAL_GUI` 驱动真实面板点击；③`ZENITH_DOMAIN_GUI` 驱动全屏配阵 | 直接调 `SoulUI.PanelBounds/HandleWindowDrag`；写 `Main.SavePath` | 已验证 |
| `checks/ZenithPrototypeCheck/UiInputCheck.cs` | 静态断言套件 | I UI | 输入屏蔽（17 条）：屏蔽层先于原版背包而前景面板保绘制序、更新期像素命中与 UI 面板在缩放下一致、面板外视口在缩放下仍可交互、按钮与释放边在背景 UI 更新前被藏、世界动作在玩家控制拷贝前被遮、真实底层原版槽在捕获点击下不能取/装/分、世界使用/放方块被阻而移动保留、前景槽仍恰好收到一次捕获点击、前景面板之后图层也收不到、物理状态恢复且点击被消费到下一帧、按住不重复交换、拖到外面保持所有权、在外面释放释放捕获、未覆盖的原版背包仍可用、关闭按钮能穿过捕获、关闭不把仍按住的点击泄漏给世界、关闭并释放后输入恢复 | `SoulUI` 输入层、`PlayerInput` 掩码 | 已验证 |
| `checks/ZenithPrototypeCheck/VanillaReturnCheck.cs` | 静态断言套件 | B 核心 | 回归原版天顶飞行：子更新频率一致、同帧到期、`Projectile.Update` 相位与位置无时间重缩放、朝向正负曲线一致、物品美术旋转/翻转保留、原版淡出不做模型吸收；本体 60 阶段后停伤、仅可见剑影另行判定；自定义吸收彻底移除 | `SoulBlade`、`AI_182`；与 `EchoDamageCheck` 互补 | 已验证 |
| `checks/ZenithPrototypeCheck/VfxStripeAblationCheck.cs` | GPU 渲染 ModSystem | K 渲染 | 条纹消融（stripe ablation）对照，写 `ablation.json` | `run_vfx_stripe_ablation.ps1`、`verify_own_effect.py` | 部分待核实 |
| `checks/ZenithPrototypeCheck/WindowDragCheck.cs` | 静态断言套件 | I UI | 窗口拖拽（10 条）：真实输入钩子在缩放下先于世界输入起头拖、移动面板保持背景屏蔽、拖到外面夹住窗口并保点击所有权、外面释放结束拖拽、拖拽释放后外部输入恢复、重开记住本次会期窗口位置、光标武器不能误起拖拽、关闭按钮排除在拖拽把手外 | `SoulUI.HandleWindowDrag`、`HeaderDragBounds` | 已验证 |
| `checks/ZenithPrototypeCheck/WoodCheck.cs` | 静态断言套件 | D 木质 | 木质族（52 条）：8 把原版木剑按 `WoodKind` 顺序、枚举 8 连续且有本地化名、每把支持两种槽、阈值表、仅 `0xFF` 算满套、空装无收益、皇冠覆盖 3.00 强度 +100 生命、支援/奖励槽不能自举、满木给 5 支援无奖励槽（无矿石精通）、真实支援槽收货并保前缀、普通与支援数组去重、可取回、深克隆、全槽存读档/网络、V2 数组缺失迁移为空且不碰普通槽、哥布林重铸保支援与皇冠数组、森林/荫蔽首击 buff 仍生效、分数回复无每帧最小值、30 秒有效战斗 6 层基础上限、无皇冠不超过 6 层、6 层成长表数值、满木生命/防御/DR 用真实玩家属性、宽限期后每 2 秒掉 1 层、去掉任一木即失满套并立即清成长、恢复满套后续涨、换魂重置、死亡清零、6 个定时木标记都是 4 秒、每个标记都给可见减益、木 DoT 由主 P 与强度 2.00 推导、凋零每点强度只放大自身 DoT 2%、重复首击共享每目标冷却、自有木 DoT 按半速转 lifeRegen、NPC 木状态与剩余时长过网络、棕榈破甲与针叶/珍珠复合易伤、毒/火免疫独立于自定义标记、木桩排除刷 proc、针叶每点强度减速 4% 且不塌速度、Boss 不被强减速、8 木不开奖励/皇冠槽、矿石精通重开 2 奖励槽、矿石完奖励槽收真武器、非皇冠槽放泰拉刃激活皇冠覆盖、皇冠把上限翻倍到 12 层、皇冠奖励可取回、失皇冠夹回 6 层、17 个 UI 槽不重叠、满套给森林之力 buff、缺木源撤 buff | `WoodKind`、`SoulItem.Supports/Crown`、`SoulPlayer` | 已验证 |
| `checks/ZenithPrototypeCheck/WoodV2Check.cs` | 静态断言套件 | D 木质 | V2 木质（48 条）：名册严格 8 把原版木剑、保强度/生命/支援数、8 木给 mask 0xFF + Full + 5 支援、皇冠 2.00/+40 → 3.00/+100；三条羁绊锻造链（Forge：4 木 + 棕榈 + 铜/铁宽剑；缺铁断链；3 木不足第四阶）（Rot：6 木含红木/乌木 + 村正/永夜；缺红木断链）（Ember：6 木含红木/灰烬 + 炽焰巨剑；缺剑断链）；皇冠需 8 木 + 皇冠槽外的泰拉刃；皇冠槽内泰拉刃不能自解；8 木单独不够；皇冠槽 35/36 永不作支援序号；传奇 34 号槽被序号链跳过；序号 14 ↔ 槽 37 双向；基础序号 0-4 在槽 10-14；额外序号从槽 25 起；皇冠激活暴露两槽、未激活隐藏、激活收真武器、未激活拒绝插入并留光标；8 木不开奖励槽、满矿收藏开 2 槽；森林之力 6/12 层上限；0 层无成长；300 有效战斗帧 1 层；基础每层 2% 伤害/1% 速度/2 防御/0.3 回复/3% DoT/0.5% DR；基础 6 层封顶；皇冠激活条件是泰拉刃在宿主；皇冠每层 3%/2%/3/0.5/5%/1%；皇冠到 12 层；已移除木剑不再注册、ZenithReborn 无自定义木剑类、`WoodKind` 无 Fey 成员；传奇位第 8 把木剑使族计数 7→8、清空回到 7、未激活不计、传奇位不给额外支援槽、7 木+传奇第 8+泰拉刃达 8/8 与皇冠、清传奇位掉回 7 木与 1.50 强度；满矿收藏真开 2 森林槽、6 主木+2 森林木达 8/8、森林木不给额外支援槽或种族阶、皇冠槽内木剑不能解皇冠也不能喂计数、基线 8/8 真开 2 皇冠槽、木剑移进皇冠槽使皇冠自引用崩塌 | `WoodKind`、`SoulItem.Crown/Legendary`、`SoulBond` 三条链 | 已验证 |
| `checks/ZenithPrototypeCheck/WorldRefractionProbe.cs` | 世界折射探针 | L 探针 | 在实机世界里采剑魂飞行时的原生折射数据，写 `native-probe.json` 到 `ZENITH_SOUL_FLIGHT_WORLD` | 与 `SoulFlightWorldCheck` 共用输出目录 | 部分待核实 |
| `checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj` | csproj | 夹具项目 | net8.0、`BuildMod=false`、导入 tMLMod.targets、引用主模组 bin/Release DLL（Private=false） | 所有 `-eac` 打包路径 | 已验证 |
| `checks/ZenithPrototypeCheck/build.txt` | txt | 夹具项目 | displayName "Zenith Prototype Check (isolated)"、version 0.1、`modReferences=ZenithReborn`、`buildIgnore` | 打包时需先有 ZenithReborn.tmod | 已验证 |
| `checks/archived-material-fixtures/0.3.89/DayNightMaterialRenderCheck.cs` | 历史归档夹具 | 材质·昼夜 | 固定输入对照"原始 80"：白天必须逐像素一致，暗处只降表面反光、不改折射数据与形状 | `HybridMaterialRenderCheck` 同批；`check_color79.py` | 已扫描 |
| `checks/archived-material-fixtures/0.3.89/GlassResponseRenderCheck.cs` | 历史归档夹具 | 材质·玻璃响应 | 固定几何/色场时间/覆盖度，只换光照与环境方向，验证高光响应的是材质而非坐标白线 | `check_glass_visibility_delivery.py` | 已扫描 |
| `checks/archived-material-fixtures/0.3.89/HybridMaterialRenderCheck.cs` | 历史归档夹具 | 材质·混合 | 唯一带 `ModSystem` 的一个：在专用菜单夹具内替换自有 `Effect`，逐像素比较 R3 基底与融合版的光学数据 | `run_hybrid_gpu.ps1`、`check_hybrid_preservation.py` | 已扫描 |
| `checks/archived-material-fixtures/0.3.89/SwordPaletteRenderCheck.cs` | 历史归档夹具 | 材质·调色 | 同一网格/时间/光向下换真实剑体身份，读生产输出色，避免只比较"有无像素差异" | 现役 `SwordFlowPaletteCheck` 的前身 | 已扫描 |
| `checks/archived-material-fixtures/0.3.89/TextureAblationRenderCheck.cs` | 历史归档夹具 | 材质·纹理消融 | 隔离菜单临时替换输入，用原始 80 着色器逐项定位纹理伪影，退出前恢复全部资源 | 现役 `VfxStripeAblationCheck` 的前身 | 已扫描 |
| `checks/archived-material-fixtures/0.3.96/SwordTrailHeadJoinCheck.cs` | 历史归档夹具 | 剑尾·头部接合 | **本组唯一含断言的**（3 条）：头部深度须准确覆盖当前刀身末端、接合只扩展刀身侧而真实剑尖不动、头部之外不继续施加接合权重 | 现役 `SwordTrailInnerContourCheck`、`SwordTrailHeadJoinRenderCheck` 的前身 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialActionVisuals.cs` | 历史归档夹具 | 武技·演出 | 编排动作伴随的雷光、蓄力流线与角色流光；不画屏幕遮罩、不改现有材质 | `MartialBlades.MartialVisual` 配对 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialArts.cs` | 历史归档夹具 | 武技·核心 | 武技系统主干：`MartialConfig : ModConfig`、`MartialTuning`、`MartialInput`、`MartialPlayer : ModPlayer`、`MartialTime`（只冻结战斗范围敌人，不停世界时钟/友军/输入） | 被 `RemovedSystemsCheck` 按名字断言"未注册" | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialBlades.cs` | 历史归档夹具 | 武技·弹幕 | `MartialVisual` + 三个 `ModProjectile`：`MartialBlade`（弹出旋转/突刺/嵌入/击飞/返航）、`MartialGiant`（雷临蓄力/起势/化光突进/顿挫/斩击/消散）、`MartialWave` | `MartialMotion` 提供共用曲线 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialCheck.cs` | 历史归档夹具 | 武技·断言 | 武技断言套件 **49 条**：空充能拒手动激活、原生剑伤真实充能武技、同刃帧伤不重复充能、派生波不递归充能、满充能不自动激活 | 现役对应物散落在 `BlessingCheck`（不自动激活）与 `OreRewardCheck`（不递归充能） | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialData.cs` | 历史归档夹具 | 武技·数据 | 武技数据表（无 `ModSystem`、无 hook、无断言），为 `MartialBlades`/`MartialTuning` 提供常量与索引 | 被 `MartialCheck`、`MartialBlades` 消费 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialGuiFixture.cs` | 历史归档夹具 | 武技·实机夹具 | `ModSystem`：只跑隔离副本世界，检查点可暂停查看，自动模式正常退出、不碰正式存档 | 与现役 `CombatFixture`/`UiFixture` 同构 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialMotion.cs` | 历史归档夹具 | 武技·运动 | `MartialMotion` 动作曲线与真实弹幕共用（预览不得另写更夸张的一套）；`MartialDashPlayer : ModPlayer` 仅在真正突进的 12 帧隐藏人物绘制层 | `MartialBlades` 消费同一曲线 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialMotionRenderCheck.cs` | 历史归档夹具 | 武技·GPU 回放 | `ModSystem`：隔离菜单内调真实弹幕 AI 与 `PreDraw` 做动作 GPU 回放；十字标记玩家中心、方框标记真实 NPC 碰撞框；不操作鼠标键盘 | `run_martial_motion_render.ps1` | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialSlashCheck.cs` | 历史归档夹具 | 武技·剑弧断言 | 剑弧历史断言 **7 条**：固定客户端视觉预算、传送断带不画整屏、非有限样本丢弃、跨角度回绕保持连续、残影按淡出窗完全过期、取消/返航清旧带、不连续旋转起新带 | 语义与现役 `RibbonCheck`/`FlowParticleCheck` 高度重叠 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialSlashRenderCheck.cs` | 历史归档夹具 | 武技·GPU 回放 | `ModSystem`：隔离菜单客户端回放当时原版流光绘制，不操作桌面、世界或正式存档 | 配对 `MartialSlashVisuals` | 已扫描 |
| `checks/archived-removed-systems/0.3.123/MartialSlashVisuals.cs` | 历史归档夹具 | 武技·剑弧绘制 | `MartialSlashRenderer : ModSystem`：只保存近期可见剑姿，**不参与**弹幕生成/伤害/碰撞/存档/网络/攻击计时；实体前绘制一次让剑身压在剑弧上，切 shader 与混合后必须恢复 | 现役 `MaterialTrailCheck` 的前身语义 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/SpiritFamily.cs` | 历史归档夹具 | 剑灵族·元数据 | 剑灵族类型元数据：`Scope` 只记 R2 开发计划、**不给** `CanHost` 权限；无 hook、计时器、玩家状态、收藏进度、支援授予或战斗效果 | 现役 `HolyHeavenCheck`/`FlyingBladeCheck` 的族模型前身 | 已扫描 |
| `checks/archived-removed-systems/0.3.123/SpiritFamilyCheck.cs` | 历史归档夹具 | 剑灵族·断言 | 剑灵族断言 **16 条**：任一武器不得同时属于两个 spirit 成员身份、已发布成员资格不可被消费者改写 | 现役无等价"跨族互斥"断言 | 已扫描 |
| `checks/audit_material_assets.py` | py | C 静态审计 | 材质贴图预算、shader 参数声明vs提交、源码与安装包身份 | artifacts/install-result.json、Assets/Vfx/Materials/SwordFlow | 已验证 |
| `checks/build_file_index.py` | 构建脚本（Python 标准库） | 索引 | 抽取 9 份报告的六列索引表 → 去重合并 → 按前缀分 6 节 → 写 docs/FILE_INDEX.md；输入目录优先 docs/archive/codebase-map-survey-2026-11-03/，缺失时回退 artifacts/codebase-map/；支持 --check 只报差异 | docs/archive/codebase-map-survey-2026-11-03/*.md、docs/FILE_INDEX.md | 已验证 |
| `checks/build_material_lab.ps1` | ps1 | D 打包 | 先跑主回归，再打包 tools/ZenithMaterialLab 到 build-profile | run_wood_checks.ps1:6 | 已验证 |
| `checks/check_airflow_assets.py` | py | C 静态检查 | 21 把剑 H4 导出/运行时素材/逐矿颜色/拖尾长度 | VfxLibrary/Masks/SwordBody/*/H4/authoring.json | 部分待核实 |
| `checks/check_color79.py` | py | C 交付核对 | 0.3.87 逐剑配色与亮刃混色沿用 79 | 硬编码 0.3.87 | 部分待核实 |
| `checks/check_color_glass_restoration.py` | py | C 交付核对 | 0.3.83 保留选色同时恢复玻璃 | 硬编码 0.3.83 | 部分待核实 |
| `checks/check_day_night_delivery.py` | py | C 交付核对 | 0.3.89 昼夜纹理修订 + 原始 80 保留 | 硬编码 0.3.89 | 部分待核实 |
| `checks/check_echo_damage_delivery.py` | py | C 交付核对 | 剑影伤害包的实机证据 + 素材保持 | build-profile | 部分待核实 |
| `checks/check_ending_preservation.py` | py | C 交付核对 | 结束修订前后 GPU 图与随包美术比对 | build-profile、numpy/PIL | 部分待核实 |
| `checks/check_glass_r3_delivery.py` | py | C 交付核对 | 0.3.80 R3 交付证据 | 硬编码 0.3.80 | 部分待核实 |
| `checks/check_glass_reference_restore.py` | py | C 交付核对 | 0.3.84 用归档旧版 GPU 像素核对恢复 | 硬编码 0.3.84 | 部分待核实 |
| `checks/check_glass_visibility_delivery.py` | py | C 交付核对 | 0.3.81 增亮只改光能 | 硬编码 0.3.81 | 部分待核实 |
| `checks/check_hybrid_preservation.py` | py | C 交付核对 | 0.3.86 融合版只改材质光效 | 硬编码 0.3.86、79/80 哈希常量 | 部分待核实 |
| `checks/check_liquid_glass_analytic.py` | py | C 数值复核 | 解析梯度标量差分 | K=0.012 | 部分待核实 |
| `checks/check_liquid_glass_geometry.py` | py | C 数值复核 | 截面与合并函数边界性质 | numerical-check.json | 部分待核实 |
| `checks/check_material_preparation.py` | py | C 静态检查 | 8 条离线语义自测（通道/sRGB/法线mip/图集） | prepare_material_maps.py | 已验证 |
| `checks/check_mod_assets.py` | py | C 静态检查 | 被注册类型必须有同名 png 或 Texture override（纯标准库） | run_wood_checks:13、ZenithReborn/*.png | 已验证 |
| `checks/check_sword_color_delivery.py` | py | C 交付核对 | 0.3.82 剑身取色/亮度/滤镜保留 | 硬编码 0.3.82 | 部分待核实 |
| `checks/check_vfx_ownership.py` | py | C 静态检查 | 包内禁止第三方材质、自有 effect 清单哈希、生产源码禁用符号 | build-manifest.json、package_install:30 | 已验证 |
| `checks/check_vfx_rollback_0379.py` | py | C 交付核对 | 与 0.3.79 归档逐字节核对 | `--version` 默认 0.3.85 | 部分待核实 |
| `checks/check_visible_weapon_colors.py` | py | C 交付核对 | 0.3.88 一次性证据：逐剑调色板、光学差异为 0、全模组世界日志 | 硬编码 version=='0.3.88' | 已验证 |
| `checks/collect_boss_unlock_evidence.py` | py | G 报告 | 汇总构建/检查/GPU/素材来源，对 0.3.108 基线做差异审计 | install-result.json | 部分待核实 |
| `checks/compare_clear_glass.py` | py | G 报告 | GPU 输出并排拼图 | numpy/PIL | 部分待核实 |
| `checks/compile_effects.py` | py | E 着色器 | 用 d3dcompiler_43 编译生产 .fx → .fxc + build-manifest.json | Assets/Effects/*.fx、4 个白名单 .fxh | 已验证 |
| `checks/compile_liquid_glass.py` | py | E 着色器 | 编译 LiquidGlass.fx 原型 | run_liquid_glass.ps1 | 部分待核实 |
| `checks/compile_liquid_glass_parameter_probe.py` | py | E 着色器 | 编译 ParameterProbe.fx | run_liquid_glass | 部分待核实 |
| `checks/compile_liquid_glass_perf.py` | py | E 着色器 | 编译 LiquidGlassPerfBase.fx | run_liquid_glass_perf | 部分待核实 |
| `checks/compile_liquid_glass_sentinel.py` | py | E 着色器 | 编译固定色 Sentinel.fx | run_liquid_glass | 部分待核实 |
| `checks/compile_liquid_glass_trail.py` | py | E 着色器 | 编译 LiquidGlassTrail.fx | run_liquid_glass_trail/perf | 部分待核实 |
| `checks/dev_setup.ps1` | PowerShell 脚本（上手配置） | 协作与上手 | 新克隆仓库的一键配置：按 -LoaderRoot > ZENITH_TML_ROOT > local.props > 默认值确定 tModLoader 路径，写 local.props、装 git 钩子、准备 Luminance、编译；幂等可重跑，配合 docs/协作开发指南.md | local.props、checks/install_git_hooks.ps1、checks/prepare_luminance.py、docs/协作开发指南.md | 人工补写 |
| `checks/emit_glass_preset.py` | py | E 生成器 | preset.json → GlassMaterialPreset.cs（13 参数带上下界断言） | VfxLibrary/Materials/SwordTrail/Glass/preset.json | 已验证 |
| `checks/emit_vfx_mask_data.py` | py | E 生成器 | H4 蒙版 → ZenithReborn/Assets/Vfx/SwordBody + 几何/底色表 | 生产资产 | 部分待核实 |
| `checks/encode_swordbody_preview.ps1` | ps1 | G 报告 | ffmpeg 把 5 个片段帧序列编 mp4 | artifacts/preview-tools/imageio_ffmpeg | 已验证 |
| `checks/evolve_swordbody_rhythm.py` | py | F 素材制作 | 按剑性格演化 R2 蒙版节奏（美术决策脚本） | VfxLibrary/Masks/SwordBody | 部分待核实 |
| `checks/generate_noise.py` | py | F 素材制作 | 纯 Python 生成可平铺噪声 → NoiseSeamless.png | 直接写生产资产 | 部分待核实 |
| `checks/hooks/pre-commit` | sh 钩子模板 | 建图维护 | 提交前跑 checks/map_freshness.py，地图落后即阻断提交（git commit --no-verify 可绕过）；优先用项目约定的 runtime python，缺失时回退 PATH 上的 python | checks/map_freshness.py、checks/install_git_hooks.ps1 | 人工补写 |
| `checks/import_material_export.py` | py | F 素材制作 | 核对实验室导出并按剑名接入（默认 dry-run，`--apply` 才写） | tools/ZenithMaterialLab 导出物 | 部分待核实 |
| `checks/install_git_hooks.ps1` | PowerShell 脚本（安装） | 建图维护 | 把 checks/hooks/pre-commit 复制到 .git/hooks/pre-commit；钩子本身不入库，新克隆的仓库要重跑一次 | checks/hooks/pre-commit | 人工补写 |
| `checks/make_autofit_recipe.py` | py | F 素材制作 | 为一把剑生成 R2 autoFit 配方 | run_swordbody_batch | 部分待核实 |
| `checks/map_audit.py` | 审计脚本（Python 标准库） | 索引 | 索引覆盖率审计：磁盘分母 vs docs/FILE_INDEX.md 已索引集合，报覆盖率、未索引清单、失效路径；并核对 VfxLibrary 清单 | docs/FILE_INDEX.md、docs/archive/codebase-map-survey-2026-11-03/vfx-library-manifest.txt | 已验证 |
| `checks/map_freshness.py` | Python 脚本（校验） | 建图维护 | 五项校验：A 索引与磁盘是否一致（调 build_file_index --check）、B 文档里 `文件:行号` 是否存在与越界、C CODEBASE_MAP 基线版本 vs build.txt、D 源码/夹具/checks 脚本计数、E 回归项数 vs wood-check.txt（只警告）；有 ERROR 退出码 1，供 pre-commit 与回归使用 | checks/build_file_index.py、checks/hooks/pre-commit、docs/CODEBASE_MAP.md | 人工补写 |
| `checks/map_query.py` | Python 脚本（只读检索） | 建图维护 | 按关键词检索 docs/FILE_INDEX.md 六列索引，输出「模块 / 文件 / 职责」；路径 3 分、模块 2 分、职责关联 1 分，并按目录优先级把源码排在文档前；支持 --module / --file / --json / --limit，用来替代全库 grep | docs/FILE_INDEX.md、.codebuddy/rules/01-codebase-map.md | 人工补写 |
| `checks/mark_swordbody_entries.py` | py | F 素材制作 | 标记 21 把矿石剑为 review-candidate R2 | VfxLibrary entry.json | 部分待核实 |
| `checks/pack_swordbody_mask.py` | py | F 素材制作 | 抽 alpha 单元并登记尖端，不重绘 | register_swordbody_vector | 部分待核实 |
| `checks/package_install.py` | py | D 打包安装 | 校验报告/包身份后原子安装、备份、写 install-result.json | wood-check.txt、check_vfx_ownership、prepare_luminance、tmod_archive、artifacts/backups | 已验证 |
| `checks/pick_soul_flight_frames.py` | py | G 报告 | 按非背景像素/亮度跨度挑帧 | artifacts 抓帧目录 | 部分待核实 |
| `checks/png_stdlib.py` | py | F 库 | 无依赖 PNG 读写（5 种扫描线滤波） | register_swordbody_vector 的 ImportError 回退 | 部分待核实 |
| `checks/prepare_boss_portraits.py` | py | F 素材制作 | Wiki GIF → 透明图集 + 生成 SwordBossPortraitCatalog.cs | 生产源码（生成物） | 部分待核实 |
| `checks/prepare_energy_blade.py` | py | F 素材制作 | 8 张原图 → 法线/表面/四区数据候选 | artifacts/energy-blade-a1 | 部分待核实 |
| `checks/prepare_glass_r3.py` | py | F 素材制作 | 原稿 → 环境/高度/法线/粗糙度 | ZenithReborn/Assets/Vfx/Materials/SwordFlow/GlassR3 | 部分待核实 |
| `checks/prepare_glass_reflection_lowpass.py` | py | F 素材制作 | 从 80 环境图生成暗处反射输入 | GlassR3 | 部分待核实 |
| `checks/prepare_luminance.py` | py | D 依赖准备 | 定位 workshop 里的 Luminance.tmod，抽 DLL 到 artifacts/dependencies，可选注入 profile | ZenithReborn.csproj 的 PrepareLuminance Target、package_install:23 | 已验证 |
| `checks/prepare_material_maps.py` | py | F 素材制作 | 通道打包 + 语义 mip 烘焙（被 check_material_preparation 自测） | bake_mips/pack/data_payload | 部分待核实 |
| `checks/prepare_pure_code_package.py` | py | D 打包 | 重新拼 TMOD 去掉 Assets/Vfx，保留同 DLL | run_pure_code_vfx.ps1 | 已验证 |
| `checks/prepare_smooth_glass_layers.py` | py | F 素材制作 | 平滑玻璃分层候选 + 原贴图检查 | artifacts/smooth-glass-layers-r1 | 部分待核实 |
| `checks/register_swordbody_vector.py` | py | F 素材制作 | 登记原生 GPU 导出蒙版（不重绘 alpha），写 clip.json | 三个批量脚本 | 部分待核实 |
| `checks/render_ribbon_preview.py` | py | G 报告 | CPU 复刻顶点网格预览 | numpy/PIL | 部分待核实 |
| `checks/repoint_colorfields.py` | py | F 素材制作 | 把各剑 R2 的 colorField 指向新金属场 | VfxLibrary entry.json | 部分待核实 |
| `checks/review_mod_vfx.py` | py | C 静态检查 | 只读打开 .tmod 提取研究样本 | tmod_archive | 部分待核实 |
| `checks/run_airflow_batch.ps1` | ps1 | B 批量 | 21 把剑 H3 逐个 preview+register，跳过已有 clip.json | run_swordbody_preview.ps1、register_swordbody_vector.py | 已验证 |
| `checks/run_airflow_export.ps1` | ps1 | B 图形夹具 | 构建夹具 csproj→`-eac` 打包→一个客户端导出 21 把图集 | artifacts/airflow-H4-export/complete.txt | 已验证 |
| `checks/run_airflow_material.ps1` | ps1 | B 图形夹具 | 生产材质 GPU 预览 + passed 断言 | artifacts/airflow-material-<ver>/gpu.json | 已验证 |
| `checks/run_airflow_world.ps1` | ps1 | B 图形夹具 | 隔离世界（可 FullModpack/Dummy/PlacementOnly/before-placement） | artifacts/airflow-world-*/world.json | 已验证 |
| `checks/run_archived_vfx_capture.ps1` | ps1 | B 图形夹具 | 0.3.79/0.3.80 归档包哈希校验后采集（world/gpu） | artifacts/vfx-comparison-<run>、capture-session.json | 已验证 |
| `checks/run_clear_glass.ps1` | ps1 | B 图形夹具 | 透明玻璃专项（可 -Baseline/-PackageVersion/-HighlightProbe） | clear-glass.json / highlight-probe.json | 已验证 |
| `checks/run_echo_phase_probe.ps1` | ps1 | B 服务端夹具 | 相位探针，产出 txt | 写 build-profile、不校验退出码 | 已验证 |
| `checks/run_energy_blade_preview.ps1` | ps1 | B 图形夹具 | 能量薄刃素材预览（材质实验室专用档） | artwork.json | 已验证 |
| `checks/run_flying_blade_check.ps1` | ps1 | B 服务端夹具 | 独立 flying-blade-profile 构建两包，跑 22 项飞刃断言 | build-profile（仅读 Luminance.tmod）、artifacts/flying-blade-check.txt | 已验证 |
| `checks/run_flying_blade_survey.ps1` | ps1 | B 服务端夹具 | 引擎级普查，输出弹幕/配方 JSON | 写 build-profile、artifacts/flying-blade-survey.json | 已验证 |
| `checks/run_hybrid_gpu.ps1` | ps1 | B 图形夹具 | 融合光学对照，异步不等待 | hybrid.json | 已验证 |
| `checks/run_information_gpu.ps1` | ps1 | B 图形夹具 | 信息界面 GPU，启动前清空输出目录 | artifacts/boss-unlocks-<ver>/gpu | 已验证 |
| `checks/run_information_ui_render.ps1` | ps1 | B 图形夹具 | 界面截屏+模拟输入，断言报告绑定包哈希 | gpu.json、client.pid | 已验证 |
| `checks/run_ingame_preview.ps1` | ps1 | B 已撤下 | 文件即一句 throw（随 0.3.60 旧材质撤下） | — | 已验证 |
| `checks/run_liquid_glass.ps1` | ps1 | B 图形夹具 | 编译 3 个原型 shader→打包夹具→断言 GPU 报告 | compile_liquid_glass*.py、shader-hashes.json | 已验证 |
| `checks/run_liquid_glass_perf.ps1` | ps1 | B 图形夹具 | GPU 计时基线（识别 Steam 登录失败） | liquid-performance.json | 已验证 |
| `checks/run_liquid_glass_trail.ps1` | ps1 | B 图形夹具 | 刀光网格原型 | liquid-trail.json | 已验证 |
| `checks/run_martial_motion_render.ps1` | ps1 | B 图形夹具 | 武技动作菜单 GPU，异步 | artifacts/martial-motion-<ver> | 已验证 |
| `checks/run_martial_world.ps1` | ps1 | B 图形夹具 | 武技世界自动跑（GUI+AUTORUN） | artifacts/martial-playtest-<ver> | 已验证 |
| `checks/run_mask_preview.ps1` | ps1 | B 图形夹具 | 蒙版合成预览（AssetsOnly/Source） | artifacts/mask-composition-preview | 已验证 |
| `checks/run_material_lab.ps1` | ps1 | B 图形夹具 | 材质实验室；-Check 时回写 schema-controls/material.json | artifacts/material-lab-<ver>/gpu.json | 已验证 |
| `checks/run_pure_code_vfx.ps1` | ps1 | B 图形夹具 | 有/无 VFX 素材两包逐像素一致性 | prepare_pure_code_package.py、independence.json | 已验证 |
| `checks/run_slash_render.ps1` | ps1 | B 图形夹具 | 菜单 GPU 斩击回放，异步 | artifacts/slash-gpu-<ver> | 已验证 |
| `checks/run_smooth_glass_layers.ps1` | ps1 | B 图形夹具 | 平滑玻璃分层候选预览 | artwork.json | 已验证 |
| `checks/run_soul_flight_world.ps1` | ps1 | B 图形夹具 | 主飞剑隔离世界（Surface/EchoDamage/FullModSet/FormalSettings/Original79/LightPreset/RunLabel） | world.json | 已验证 |
| `checks/run_soul_input.ps1` | ps1 | B 图形夹具 | 真实鼠标输入（REQUIRE_INPUT_ARM），异步靠 exit/input-arm | artifacts/soul-input-<ver>/validation.json | 已验证 |
| `checks/run_soul_trail_render.ps1` | ps1 | B 图形夹具 | 主飞剑拖尾 GPU（-WithoutThirdParty 重建隔离包、-TextureAblation） | artifacts/soul-trail-gpu-<ver> | 已验证 |
| `checks/run_surge_preview.ps1` | ps1 | B 图形夹具 | 重建两包→等 preview.json（失败强杀） | 硬编码 taskRoot | 已验证 |
| `checks/run_sword_head_join.ps1` | ps1 | B 图形夹具 | 指定包 + 夹具包做刀身接合检查 | head-join.json | 已验证 |
| `checks/run_swordbody_batch.ps1` | ps1 | B 批量 | 20 把矿剑 R2 断点续跑，一次重试，收尾 vfx_library validate | 开头强杀所有 dotnet | 已验证 |
| `checks/run_swordbody_preview.ps1` | ps1 | B 图形夹具 | 单剑单修订版蒙版 GPU 导出（被三个批量脚本调用） | VfxLibrary/library.json、ZENITH_SWORDBODY_* 8 个变量 | 已验证 |
| `checks/run_textured_sword_material.ps1` | ps1 | B 图形夹具 | 完整贴图材质检查 | textured-material.json | 已验证 |
| `checks/run_turbulence_batch.ps1` | ps1 | B 批量 | H2 湍流蒙版批量导出（无重试） | 同上 | 已验证 |
| `checks/run_vfx_stripe_ablation.ps1` | ps1 | B 图形夹具 | 条纹消融对照 | ablation.json | 已验证 |
| `checks/run_wood_checks.ps1` | ps1 | A 全量回归 | 主线：资产检查→生成预设→编译shader→双包打包→隔离服务端断言→落盘 | check_mod_assets.py、emit_glass_preset.py、compile_effects.py、build-profile、wood-check-profile、artifacts/wood-check.txt、native-build-check.json | 已验证 |
| `checks/simulate_viscous.py` | py | G 报告 | 离线复刻 ViscousSlash 并合成动图 | png_stdlib、ffmpeg | 部分待核实 |
| `checks/tmod_archive.py` | py | D 库 | TMOD 容器读写（TMOD 头/变长串/sha1/逐条 deflate），可当 CLI 解包 | package_install、prepare_pure_code_package、verify_own_effect、18 个 check_* | 已验证 |
| `checks/verify_own_effect.py` | py | C 静态检查 | 无第三方素材包与原包 GPU 图逐像素一致 | 产出 soul-trail-gpu-<ver>/independence.json（package_install:156 消费） | 部分待核实 |
| `checks/vfx_library.py` | py | C 编目校验 | validate：id/类别/目录层级/引用环/清单齐全；search 检索 | VfxLibrary/library.json、run_swordbody_batch 收尾 | 已验证 |
| (分组) checks/archived-oneoff/（20 个 .py） | 历史归档脚本 | 归档·一次性 | 20 个扁平存放的一次性取证/生成脚本：14 个 `make_*_evidence.py`/`make_*_preview.py`（截图与取证图）、3 个 `make_*`（mask/colorfields/gif 工具）、1 个 `extract_`（从 Calamity 抄剑弧）、1 个 `localize_`（批量改注释语言）。**无一条断言**，只产 png/json 供人看；无 csproj、无执行入口，`run_*.ps1`/`*.py` 全文检索零命中 | 成员清单 `artifacts/codebase-map/archived-oneoff-manifest.txt`；目录成因见 `artifacts/codebase-map/F-checks-fixtures.md` 5.1~5.2 | 已扫描（分组索引） |
| (合计) | 231 条 |  |  |  |  |

---

## 四、`docs/` 文档

版本记录、设计定稿、专项结论与归档。`docs/archive/` 为历史归档。

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `docs/0.3.100-通透金属与零粗糙度.md` | 版本记录 | 特效/材质 | Opacity 0.85 / Metallic 0.25 / Roughness 0 | preset.json, GlassMaterialPreset.cs | 已验证 |
| `docs/0.3.101-不透明度0.1.md` | 版本记录 | 特效/材质 | 仅改全局 Opacity 至 0.1，实测不可用 | 被 0.3.102 取代 | 已验证 |
| `docs/0.3.102-玻璃主体与覆盖度分离.md` | 版本记录 | 特效/合成 | 主体密度与覆盖度分离，反射在低 Opacity 下保留 | SwordFlowMaterial.cs, ZRGlassScreen.fx | 已验证 |
| `docs/0.3.103-剑色明暗过渡与材质泛光.md` | 版本记录 | 特效/着色 | 新增 SwordFlowPalette 三色锚点，撤向白混合 | SwordFlowPalette.cs | 已验证 |
| `docs/0.3.104-刀光周围空间扭曲.md` | 版本记录 | 特效/合成 | 新增 B7-7 uGlassPartSpaceWarp 空间偏折 | SwordSpaceDistortion.cs, ZRSwordSpaceWarp.fxh | 已验证 |
| `docs/0.3.105-剑影断锋与百炼增辉.md` | 版本记录 | 战斗/剑影 | 断锋 20%/×0.8/祝福 100%，铜锡叠影联动 | SoulEchoDamage.cs, OreStackGrowth.cs | 已验证 |
| `docs/0.3.106-剑魂档案信息界面.md` | 版本记录 | UI | 四信息页档案化卡片与图标动画 | SoulUIArchive.cs, SoulUIInformation.cs | 已验证 |
| `docs/0.3.107-按操作重排界面与精简入口.md` | 版本记录 | UI | 主导航收为四项，合并特性页，旧物取回转提示 | SoulUIPages.cs, SoulUI.cs | 已验证 |
| `docs/0.3.108-剑魂配阵与羁绊核心.md` | 版本记录 | UI/配阵 | 三栏配阵主页、候选对比、实时羁绊核心 | SoulUIArmory*.cs, SoulUIArmoryBondCore.cs | 已验证 |
| `docs/0.3.109-Boss动态头像与解锁效果.md` | 版本记录 | UI/封印 | 六封印绑 Boss 组，动态头像与累计阶段（特性页） | SoulBossPortraits.cs, SoulBossUnlocks.cs | 已验证 |
| `docs/0.3.109-Boss解锁列表.md` | 版本记录 | UI/封印 | 同需求在成长记忆页的横向 Boss 区块版本 | SoulUIBossUnlocks.cs, SoulUIMemories.cs | 已验证 |
| `docs/0.3.110-剑位选择与点击穿透修复.md` | 版本记录 | UI/交互 | 剑位选中清候选；裁剪区与命中范围一致 | SoulUIArmoryInteraction.cs, SoulUIInformation.cs | 已验证 |
| `docs/0.3.111-透射玻璃与重叠合成.md` | 版本记录 | 特效/合成 | 独立透射与表面光四缓冲，修重叠厚膜 | ZRClearGlass.fxh, SoulSceneRefraction.cs | 已验证 |
| `docs/0.3.112-玻璃反射研究与小面光试作.md` | 版本记录 | 特效/研究 | 介电反射研究 + 面光候选 D（未安装） | 未进入生产包 | 已验证 |
| `docs/0.3.117-迁移79剑迹与保留剑影.md` | 版本记录 | 特效/迁移 | 整体迁移 0.3.79 剑流/屏幕合成/折射协议 | SwordFlowMaterial.cs, SoulMaterialTrail.cs, SoulSceneRefraction.cs | 已验证 |
| `docs/0.3.118-恢复刀光内缘接合.md` | 版本记录 | 特效/几何 | 恢复 1.20→1 收束，修迁移引入的缺口 | SoulMaterialTrail.Sample | 已验证 |
| `docs/0.3.118-撤回飞剑加速并安装.md` | 版本记录 | 战斗/剑影 | 撤回 119 加速，回到每帧两次更新/八帧共享免疫 | SoulBlade.cs, SoulEchoDamage.cs | 已验证 |
| `docs/0.3.119-主飞剑速度恢复与安装.md` | 版本记录 | 战斗/剑影 | 每帧更新两次→四次使飞剑提速两倍 | 已被 118 号第二篇撤回 | 已验证 |
| `docs/0.3.120-柔化高光与移动轨迹.md` | 版本记录 | 特效/轨迹 | 光晕弥散并入合成；历史顶点随人物位移平移 | SoulTrailMotion.cs, ZRGlassScreen.fx | 已验证 |
| `docs/0.3.121-剑色亮阶与简化反射.md` | 版本记录 | 特效/着色 | 逐剑亮阶下发高光，单次屏幕反射菲涅耳加权 | SwordFlowPalette.cs, SwordFlowMaterial.cs | 已验证 |
| `docs/0.3.122-合并外缘高光与亮阶过渡.md` | 版本记录 | 特效/着色 | 外侧扫光并入刃缘，核心高光由亮阶渐白 | ZRSwordFlow.fx | 已验证 |
| `docs/0.3.123-移除武技与后续种族.md` | 版本记录 | 战斗/清理 | 删武技三档/HUD/G-V 键与灵能族，留只读遗留字段 | SoulLegacyMartialData.cs, VfxRenderState.cs | 已验证 |
| `docs/0.3.124-神圣天堂与落星.md` | 版本记录 | 种族/战斗 | 八把神圣星辰武器成族，主剑命中召落星 | HolyHeavenFamily.cs, HolyHeavenProc.cs, HolyHeavenStar.cs | 已验证 |
| `docs/0.3.125-复用原版落星粒子.md` | 版本记录 | 特效/落星 | 落星改用原版 58/278 号粒子与原版音效 | HolyHeavenStarVisuals.cs | 已验证 |
| `docs/0.3.125-尚未归族武器清单.md` | 参考表 | 种族/名册 | 活文档：60 项未归族武器 + 全量 CanHost 判定 | SoulItem.CanHost, WoodTypes, OreFamily, HolyHeavenFamily | 部分待核实 |
| `docs/0.3.126-出战武器召唤初版.md` | 版本记录 | 种族/剑灵 | 不可见玩家代理自动使用寄宿武器 | LivingArmament*.cs, LivingWeaponProxy.cs | 已验证 |
| `docs/0.3.127-飞刃族初版.md` | 版本记录 | 种族/飞刃 | 31 把纯挥砍剑蓄能掷出，印记留白 | FlyingBladeFamily.cs, FlyingBladeHand.cs, FlyingBladeShot.cs | 已验证 |
| `docs/0.3.128-族归属显示与数量分阶.md` | 版本记录 | 种族/UI | tooltip+界面三处族名；飞刃族 1/2/4/6/8/10 分阶 | WeaponFamilies.Name, WeaponFamilyTags.cs, FlyingBladeFamily.cs | 已验证 |
| `docs/0.3.129-木质族V2与复合羁绊.md` | 版本记录 | 种族/木质 | 名册 12→8，森林之力重构，四复合羁绊，圣林归冠 | WoodTypes.cs, WoodBond*.cs, WoodCrown.cs | 已验证 |
| `docs/0.3.130-木质族V2重做与口径统一.md` | 版本记录 | 种族/木质 | 删残留木剑类、补属性面板、统一有效寄宿口径、三羁绊服务端重校验 | WoodTypes.cs, BondSnapshot.cs, SoulUIWoodPanel.cs | 已验证 |
| `docs/0.3.93-纯代码特效隔离与贴图独立验证.md` | 版本记录 | 特效/材质 | 建立 PureCodeOnly 统一输入限制策略 | VfxIsolation.cs, SwordFlowMaterial.cs | 已验证 |
| `docs/0.3.94-P18宽高光扇形格纹隔离.md` | 版本记录 | 特效/材质 | 关 P18 做扇形格纹消融，结论被 95 纠正 | VfxIsolation Parts, 0.3.95 | 已验证 |
| `docs/0.3.95-共享顶点连续朝向与棱纹修订.md` | 版本记录 | 特效/几何 | 定位棱纹根因为三角形朝向不连续 | SwordFlowMeshBasis.cs, SoulRibbonMesh.cs | 已验证 |
| `docs/0.3.96-刀身头部接合修复.md` | 已撤销 | 特效/几何 | 头部宽扇面补接方案，用户否定并回退 | 回退至 0.3.95 源码 | 已验证 |
| `docs/0.3.97-按蓝线修正刀光内缘.md` | 版本记录 | 特效/几何 | 带面宽度改 1.20→1 连续收束 | SoulMaterialTrail.Sample | 已验证 |
| `docs/0.3.98-全部纯代码选项开启.md` | 版本记录 | 特效/开关 | 28 项纯代码默认全开 | VfxIsolation.cs | 已验证 |
| `docs/0.3.99-贴图材质全开与昼夜实测.md` | 版本记录 | 特效/开关 | PureCodeOnly=false，56 项全开并测昼夜 | VfxIsolation.cs, GlassMaterialPreset.cs | 已验证 |
| `docs/AGENTS状态历史.md` | 索引 | 项目状态 | 由 AGENTS.md 迁出的历代状态注记 | CHANGELOG.md | 已验证 |
| `docs/AI接入指南.md` | Markdown 交接指南 | 导航文档 | 写给外部接手 AI 的自包含接入文档：项目定位、clone 后必改的两处 targets 路径、环境四件套、三条命令、地图选读表、六条硬规则、六个已踩过的坑、提交纪律、设计边界、卡住时的排查表 | docs/CODEBASE_MAP.md、docs/PROBLEM_INDEX.md、ZenithReborn/build.txt | 人工补写 |
| `docs/ARCHITECTURE.md` | Markdown 架构总览 | 导航文档 | 19 模块边界表（负责/不负责/核心文件）、三条端到端链路（战斗·挥臂到五族派发 / UI·开魂域到改槽同步 / 构建·改源码到安装）、模块级 Mermaid 依赖图、8 条共享状态单一入口、15×11 依赖矩阵、6 条修改爆炸半径、13 条硬坑一行式。核心行号锚点：`SoulItem.cs:82`(Active)、`SoulSupport.cs:43`(FamilyItems)、`VfxIsolation.cs:8`(总闸)、`SoulBlade.cs:14`(SoulSource)、`ZenithReborn.cs:18`(HandlePacket)、`ZenithPrototypeCheck/Check.cs:11`(夹具总调度) | docs/CODEBASE_MAP.md、docs/HOTSPOTS.md、artifacts/codebase-map/ | 人工补写 |
| `docs/CHANGELOG.md` | 索引 | 版本 | 107 行版本摘要表，倒序，前段指向 archive/ | docs/archive/, docs/0.3.*.md | 已验证 |
| `docs/CODEBASE_MAP.md` | Markdown 代码地图 | 导航文档 | 全仓结构总图：模块清单、目录职责、文件到模块的归属，是其余导航文档的事实底座 | docs/ARCHITECTURE.md、docs/FILE_INDEX.md | 人工补写 |
| `docs/FILE_INDEX.md` | 自动生成的索引入口 | 索引 | 全仓文件的唯一人工索引：6 节表格，由 checks/build_file_index.py 从 docs/archive/codebase-map-survey-2026-11-03/ 的 9 份调查报告抽取合并，覆盖率由 checks/map_audit.py 审计 | checks/build_file_index.py、checks/map_audit.py、docs/archive/codebase-map-survey-2026-11-03/ | 生成物 |
| `docs/HOTSPOTS.md` | Markdown 热区速查 | 导航文档 | 按“要改某类东西”组织的文件:行号速查表，并逐条列出改动会连带打破什么 | docs/CODEBASE_MAP.md、docs/PROBLEM_INDEX.md | 人工补写 |
| `docs/PROBLEM_INDEX.md` | Markdown 问题反向索引 | 导航文档 | 从玩家/开发者看到的现象反查优先检查的符号，并给出能复现或证伪该分支的夹具/脚本 | docs/HOTSPOTS.md、checks/ 夹具 | 人工补写 |
| `docs/archive/0.3.0-木质族实装说明.md` | 历史归档 | 木质族 / 寄宿 | 木质族 W2.1 首版：12 把木剑、10 普通槽 + 2 森林奖励 + 5 支援槽、P 键寄宿与右键管理 | SoulItem.cs · WoodTypes.cs | 已扫描 |
| `docs/archive/0.3.1-全部槽位与友善史莱姆王.md` | 历史归档 | 剧情 / 槽位 | 17 槽全解锁，友善史莱姆王授福流程（凝胶信物唤出、逐段对话接受「附着」） | AttachmentStory.cs | 已扫描 |
| `docs/archive/0.3.11-项目跟进与17槽恢复.md` | 历史归档 | 槽位 | 25 槽未获用户确认，回退 17 有效槽；发现 hide 缺少 DrawBehind 导致飞剑不绘制 | SlotProgression.cs | 已扫描 |
| `docs/archive/0.3.12-生效特性与图鉴.md` | 历史归档 | UI | 信息区只显示当前构筑"生效"特性，并新增"特性图鉴"页与激活条件标注 | SoulTraits.cs | 已扫描 |
| `docs/archive/0.3.13-出剑节奏与首击飞剑.md` | 历史归档 | 出剑 / 矿石族 | 挥臂周期改为平均间隔 ×1.5（25~90 帧）；只有主剑"首次有效命中"才积攒一把飞剑 | SoulBlade.cs · SoulSwingPlayer.cs | 已扫描 |
| `docs/archive/0.3.14-飞剑表现与窗口拖动.md` | 历史归档 | UI / 出剑 | 飞剑 20 帧渐显 + 剑尖闪点；剑魂窗口支持空手拖标题栏且位置按世界记忆 | SoulUI.cs | 已扫描 |
| `docs/archive/0.3.15-武器速度与模型尺寸.md` | 历史归档 | 出剑 | 回应用户"仍然太快、模型过小"：周期 ×2.25（40~135 帧）、飞剑显示为原武器 72% | SoulBlade.cs | 已扫描 |
| `docs/archive/0.3.16-精简描述与速度微调.md` | 历史归档 | UI / 本地化 | 悬浮描述从"全特性全文"压缩为三行短提示、每行 ≤28 字符 | SoulItem.cs | 已扫描 |
| `docs/archive/0.3.17-三剑慢挥与百炼齐射.md` | 历史归档 | 出剑 | 每轮最多三把、周期 ×2.5（45~150 帧），确认不恢复 25 槽 | SoulSwingPlayer.cs | 已扫描 |
| `docs/archive/0.3.18-动态剑环界面.md` | 历史归档 | UI | 寄宿页改为普通 01–10 圆环 + 支援 S1–S5 右下外弧的动态布局 | SoulArmoryLayout.cs | 已扫描 |
| `docs/archive/0.3.19-悬浮剑环与柔边暗底.md` | 历史归档 | UI | 去掉整窗蓝色底框，改边缘渐隐椭圆暗底 + 窄圆角悬浮条 | SoulUIStyle.cs | 已扫描 |
| `docs/archive/0.3.2-月亮领主召唤修复.md` | 历史归档 | 世界 / Boss | 四柱后月亮事件未启动的根因：`UpdateLunarApocalypse` 在标记 false 时直接返回，附带恢复方案 | LunarSummonRecovery.cs | 已扫描 |
| `docs/archive/0.3.20-共享奖励与逐击飞剑.md` | 历史归档 | 槽位 / 矿石族 | F1/F2 改称 R1/R2 共享全收集奖励槽；飞剑由"一次齐射"改为"逐击积攒一把" | SoulItem.cs · OreFlyingSword.cs | 已扫描 |
| `docs/archive/0.3.21-四剑挥臂与追加攻速.md` | 历史归档 | 出剑 | 四节点（25/50/75/100%）逐把释放，攻速在 0.3.20 基础上再提 10% | SoulSwingPlayer.cs | 已扫描 |
| `docs/archive/0.3.22-背后收刀与追加攻速.md` | 历史归档 | 出剑 / 渲染 | 飞行结束后追加 10 帧纯视觉收刀，新增 SoulSheathingLayer 夹在人物层前 | （已被 0.3.25 撤销） | 已扫描 |
| `docs/archive/0.3.23-归魂吸收与追加攻速.md` | 历史归档 | 出剑 / 渲染 | 收刀改为"被人物体内核心吸取"的 12 帧动作 + 实尺寸缩小 | （已被 0.3.25 撤销） | 已扫描 |
| `docs/archive/0.3.24-剑尾朝内吸收.md` | 历史归档 | 出剑 / 渲染 | 纠正朝向后加半圈，使剑尾朝内、剑尖朝外（点积 < -0.999 作引擎断言） | （已被 0.3.25 撤销） | 已扫描 |
| `docs/archive/0.3.25-原版天顶轨迹与回收.md` | 历史归档 | 出剑 / 反编译 | 删除 0.3.22~24 的自研收刀，改按原版 `AI_182_FinalFractal` 阶段 0–120，附 tML 哈希与 MVID 证据 | SoulBlade.cs · SoulTrailMotion.cs | 已扫描 |
| `docs/archive/0.3.26-原版攻速与飞行粒子.md` | 历史归档 | 出剑 | 采用原版 30 帧挥臂 / 10 帧使用间隔、Swing 方式，撤掉四节点与 34 帧下限 | SoulSwingPlayer.cs | 已扫描 |
| `docs/archive/0.3.27-六诅咒与个人封印.md` | 历史归档 | 诅咒 / 进度 | 六诅咒首版实装：两组/三组 Boss 的阶段表、独行成长、旧档兼容标记 | SoulCurses.cs · SoulBossUnlocks.cs | 已扫描 |
| `docs/archive/0.3.28-天顶魂域与传奇切换.md` | 历史归档 | UI | 全屏魂域 + 左侧随身剑库（只列背包前 50 格可寄宿剑）+ 原版传奇按钮单人限制 | SoulDomainUI.cs · SoulUILibrary.cs | 已扫描 |
| `docs/archive/0.3.29-矿石族阶梯与天铸巨剑.md` | 历史归档 | 矿石族 | 矿石 1/2/4/6/8/10 层阶梯表、折返第二趟与天铸巨剑召唤条件 | OreFamily.cs · OreSkyfall.cs | 已扫描 |
| `docs/archive/0.3.3-五段挥臂与单剑配色.md` | 历史归档 | 出剑 | 一次挥臂分 20/40/60/80/100% 五节点逐把生成真实剑影，手臂姿势跟随节点 | SoulBlade.cs · SoulVisuals.cs | 已扫描 |
| `docs/archive/0.3.30-百炼飞剑特效优化.md` | 历史归档 | 特效 | 飞剑加边光/局部柔光、剑尖改用原版星芒、出现与齐射闪光 | SwordSpark.cs | 已扫描 |
| `docs/archive/0.3.31-鼠标与飞剑圆环修复.md` | 历史归档 | UI / 特效 | 根因：魂域筛除原版界面时误删 `Vanilla: Cursor`；飞剑圆环改用统一角度而非各剑年龄 | SoulDomainUI.cs | 已扫描 |
| `docs/archive/0.3.32-共用支援与矿石伤害审计.md` | 历史归档 | 支援 / 矿石族 | 支援改为全族共用池（木质 5 + 矿石 6 累加），确立 150% 基础飞剑伤害 | SoulSupport.cs | 已扫描 |
| `docs/archive/0.3.33-传奇槽位.md` | 历史归档 | 槽位 | 34 号传奇真实主攻剑位（仅大师 + FTW 且已附着生效），含 0.3.35 的 SupportOrdinal 误判修复 | SoulItem.cs | 已扫描 |
| `docs/archive/0.3.34-支援S6与飞剑首击修复.md` | 历史归档 | 支援 / 矿石族 | S6 归位到支援弧；取消飞剑共用 10 帧冷却，开放全 Active 主攻与主槽矿材 | SoulSupport.cs · OreFlyingSword.cs | 已扫描 |
| `docs/archive/0.3.36-DeepSeek接手审查与修复.md` | 历史归档 | 交接 / 审查 | 以交付 0.3.32 源码对照复核 0.3.35：支援密集重叠、传奇保留编号、安装哈希核对 | SoulSupport.cs | 已扫描 |
| `docs/archive/0.3.37-切换诅咒四剑例外.md` | 历史归档 | 诅咒 / 矿石族 | 切换诅咒未解除时飞剑只取有效主攻序列前 4 把，按真实来源位置识别防重复绕过 | SoulCurses.cs · OreFlyingSword.cs | 已扫描 |
| `docs/archive/0.3.38-五祝福与手动御剑.md` | 历史归档 | 祝福 | 五祝福实装；御剑必须手动开启（满条不自动消耗），首版参数集中设定 | SoulBlessings.cs | 已扫描 |
| `docs/archive/0.3.39-环境属性基础层-独立复核.md` | 历史归档 | 环境 / 审查 | WK 独立重跑编译/回归/哈希复核同一版本，不复用被审方结论 | checks/ | 已扫描 |
| `docs/archive/0.3.39-环境属性基础层.md` | 历史归档 | 环境 | 21 项环境稳定编号 1–21 与 Id/Key/Name 结构，Values/Traits 全部留空不填假定数值 | EnvironmentAttributes.cs | 已扫描 |
| `docs/archive/0.3.4-复生伤害修复与战斗对比.md` | 历史归档 | 战斗 / 伤害 | 移除"除以 √剑数"分摊与"终生只命中一次"局部免疫，改回冷却后可再命中 | SoulCombat.cs | 已扫描 |
| `docs/archive/0.3.40-环境之力与主动领域.md` | 历史归档 | 环境 | 接入 21 环境的属性、独立机制、收集成长、祝福常驻与主动领域 | EnvironmentEffects.cs · EnvironmentPlayer.cs | 已扫描 |
| `docs/archive/0.3.41-环境光环满屏色带修复.md` | 历史归档 | 环境 / 渲染 | 根因：MagicPixel 实为 1×1000，缩放被放大千倍形成辐射色带；改为显式裁 1×1 | EnvironmentEffects.cs | 已扫描 |
| `docs/archive/0.3.42-剑的所属环境标记.md` | 历史归档 | UI / 环境 | 物品悬停追加"所属环境"，未归类显式写"未归类"不虚构归属 | EnvironmentTooltips.cs | 已扫描 |
| `docs/archive/0.3.43-武技三档攻击试作.md` | 历史归档 | 武技（已删） | 剑势蓄条 + G 启动/V 切换的三档攻击原型，明确只是原型非整族完成 | （0.3.123 已移除） | 已扫描 |
| `docs/archive/0.3.44-武技斩击拖影.md` | 历史归档 | 武技（已删） | 复用 Luminance 的 PrimitiveRenderer 做连续渐变剑弧与三道渐隐剑影 | （0.3.123 已移除） | 已扫描 |
| `docs/archive/0.3.45-原版流光与侵蚀剑弧.md` | 历史归档 | 特效 / 武技 | 弃规整色带，改用原版彩虹魔杖流光与侵蚀材质的 VertexStrip 主体 | （0.3.123 已移除） | 已扫描 |
| `docs/archive/0.3.46-鬼切参考与碎光粒子.md` | 历史归档 | 特效 | 参考灾厄大修鬼切，在流光主体上叠加碎光/火花/渐隐粒子（附包哈希与边界） | Assets/ThirdParty/ | 已扫描 |
| `docs/archive/0.3.47-厚月牙与白热刃缘.md` | 历史归档 | 特效 / 第三方 | 逐字节复用 OniCrimsonSlash 编译着色器与笔刷/噪声，MIT 许可与逐资源 SHA256 | Assets/ThirdParty/CalamityOverhaul/ | 已扫描 |
| `docs/archive/0.3.48-矿石组流光重制.md` | 历史归档 | 特效 / 矿石族 | 用新流光技术重做 14 材质飞剑、天铸巨剑与肉后六矿附属表现 | SwordFlowMaterial.cs | 已扫描 |
| `docs/archive/0.3.49-飞剑后拉与真实加速穿刺.md` | 历史归档 | 出剑 / 特效 | 区分"动效"与"动作"：末 6 帧真实后拉 22 像素 + 加速穿刺，不改播片 | OreFlightMotion.cs | 已扫描 |
| `docs/archive/0.3.5-逐剑伤害与独立触发.md` | 历史归档 | 战斗 / 伤害 | 确定为逐剑独立伤害模型，撤回统一天顶伤害下限与共享属性模板 | SoulCombat.cs | 已扫描 |
| `docs/archive/0.3.50-剑尖破空与独立结束特效.md` | 历史归档 | 特效 | 随剑头移动的双层气刃与两侧气流，并给巨剑/附属弹幕独立结束表现 | （已被 0.3.53 取代） | 已扫描 |
| `docs/archive/0.3.51-矿石特效体量增强.md` | 历史归档 | 特效 | 剑尖气刃与气流线性尺寸 ×2.4、外弧半径 32→76.8、巨剑 80→192 像素 | （已被 0.3.53 取代） | 已扫描 |
| `docs/archive/0.3.52-剑体与流光统一.md` | 历史归档 | 特效 | 用原版纹理 Alpha/亮度生成中性副本统一着色，消除像素剑与平滑特效的冲突 | （已被 0.3.53 取代） | 已扫描 |
| `docs/archive/0.3.53-单层动态燃烧蒙版.md` | 历史归档 | 特效 | 用户否定多层拼贴，改为一片连续编译材质贯通尾迹/剑身/剑尖 | VfxSwordMask.cs | 已扫描 |
| `docs/archive/0.3.54-灵能族基础层.md` | 历史归档 | 灵能族（已删） | 只登记 22 项成员身份与查询接口，机制/数值/支援档位全部留空 | （0.3.123 已移除） | 已扫描 |
| `docs/archive/0.3.55-剑形气流与长尾H4.md` | 历史归档 | 特效 | H4 候选：放大的剑形只作影响域，逐剑保留配色，21 把矿石剑接入 | VfxSwordMask.cs | 已扫描 |
| `docs/archive/0.3.56-飞剑附魔外观与画面缓存保护.md` | 历史归档 | 特效 / 稳定性 | 移除飞剑火焰减益外观；记录"放置超级训练假人后世界变黑"的复现与夹具局限 | VfxRenderState.cs | 已扫描 |
| `docs/archive/0.3.57-真实轨迹与巨剑材质拖尾.md` | 历史归档 | 特效 | 修复 `DrawRibbonShader` 只读首尾角度、丢弃中段位置的问题，改连续材质带面 | SoulMaterialTrail.cs | 已扫描 |
| `docs/archive/0.3.58-飞剑流动与收尾打磨.md` | 历史归档 | 特效 | 带面按实际剑尖行程展开、静止姿态不生成刃面 | SoulMaterialTrail.cs | 已扫描 |
| `docs/archive/0.3.59-自有着色器第一阶段.md` | 历史归档 | 材质 | 自写 `ZRSwordFlow.fx`（慢色场/流线/动态暗侧/返回侵蚀），明确不复制 Oni 源码 | Assets/Effects/ZRSwordFlow.fx | 已扫描 |
| `docs/archive/0.3.6-肉后三矿实装.md` | 历史归档 | 矿石族 | 六把肉后矿石剑获得剑魂专属机制（首版六项效果表） | OreSwordTraits.cs | 已扫描 |
| `docs/archive/0.3.60-撤下旧材质依赖.md` | 历史归档 | 材质 | 按"先撤后重建"删除依赖旧着色器的外观，不用纯色块填补 | （0.3.61 部分回滚） | 已扫描 |
| `docs/archive/0.3.61-恢复自有剑身蒙版与透光拖尾.md` | 历史归档 | 材质 | 纠正误删：H4 是 SpriteBatch 动态蒙版而非旧着色器；噪声改为自生成 512×512 | SwordBodyColorNoise.cs · VfxSwordMask.cs | 已扫描 |
| `docs/archive/0.3.62-折射材质与制作端参数.md` | 历史归档 | 材质 | 按 IOR 1.5/低粗糙度/低透射实现真折射，并删除玩家可见的调参界面 | SurfaceMaterialDefinition.cs | 已扫描 |
| `docs/archive/0.3.63-玻璃背景与反光统一合成.md` | 历史归档 | 材质 / 合成 | 修正"先把彩色材质画进世界再折射已被涂色画面"的合成顺序错误 | SoulSceneRefraction.cs | 已扫描 |
| `docs/archive/0.3.64-树干轮廓折射与透射解耦.md` | 历史归档 | 材质 | 用实测覆盖度中位数 0.533 与位移 2.73/5.81 像素证明前版 PASS 不成立 | SoulSceneRefraction.cs | 已扫描 |
| `docs/archive/0.3.65-统一材质核心与独立实验室.md` | 历史归档 | 材质 | `SurfaceMaterialDefinition` 作为唯一参数合同 + 独立测试模组与导入导出 | SurfaceMaterialDefinition.cs · SurfaceMaterialRenderer.cs | 已扫描 |
| `docs/archive/0.3.69-成熟模组拆解与材质分区.md` | 历史归档 | 材质 | 研究成熟模组包与公开源码的边界，确定材质分区与"核心自主"原则 | SurfaceMaterial*.cs | 已扫描 |
| `docs/archive/0.3.7-寄宿界面优化.md` | 历史归档 | UI | 界面拆分为三页（寄宿 / 成长 / 图鉴）的信息架构 | SoulUI.cs | 已扫描 |
| `docs/archive/0.3.70-真实剑迹独立消散.md` | 历史归档 | 特效 | 修复父弹幕持续写历史导致带面被覆盖缩没，改为真实剑迹独立消散 | SoulTrailEnding.cs | 已扫描 |
| `docs/archive/0.3.71-飞剑真实点击与沿迹收尾.md` | 历史归档 | 特效 / 验证 | 用真实鼠标按下松开复现，证明 0.3.70 余迹存在但末期难辨 | SoulTrailEnding.cs | 已扫描 |
| `docs/archive/0.3.73-四区遮罩原始数据与天顶贴图重做.md` | 历史归档 | 材质 | 四区遮罩（刃口/脊线/腹部/磨损）改原始 RGBA 直接上传，四区同时生效 | VfxMaskData.cs | 已扫描 |
| `docs/archive/0.3.76-武技三档动作与旋转返航.md` | 历史归档 | 武技（已删） | 三档巨剑蓄力后时停突进 + 旋转返航的动作顺序 | （0.3.123 已移除） | 已扫描 |
| `docs/archive/0.3.77-飞剑残影与原版采样对齐.md` | 历史归档 | 特效 | 剑影连成实心链条的根因：把采样改回 15/30/45 但材质仍铺满 60 点 | SoulTrailUnits.cs | 已扫描 |
| `docs/archive/0.3.78-逐剑影独立短尾.md` | 历史归档 | 特效 | 单元独立短尾：头点 0/15/30/45，尾窗 13/11/9/7 点，间隙不铺三角形 | SoulTrailUnits.cs | 已扫描 |
| `docs/archive/0.3.79-原始包夜间观察.md` | 历史归档 | 验证 | 直接运行收藏的原始 0.3.79 包做夜间受控观察（附包 SHA-256） | artifacts/vfx-presets/ | 已扫描 |
| `docs/archive/0.3.79-可见剑影实体伤害.md` | 历史归档 | 战斗 / 特效 | 三个可见剑影参与实体碰撞，按 0.02 可见阈值判定，短尾仍无伤害 | SoulEchoDamage.cs | 已扫描 |
| `docs/archive/0.3.79与0.3.80-独立客户端画面对比.md` | 历史归档 | 验证 / 美术 | 同时运行 0.3.79 与 0.3.80 原始包截取画面对比（后续引出 0.3.86 融合版） | artifacts/ | 已扫描 |
| `docs/archive/0.3.8-界面点击穿透修复.md` | 历史归档 | UI / 输入 | 在 `PostUpdateInput` 提前捕获面板范围内鼠标，解决点击穿透到背包 | SoulInput.cs | 已扫描 |
| `docs/archive/0.3.80-玻璃反射与同源材质R3.md` | 历史归档 | 材质 | R3：拆掉独立于法线的坐标亮线，让法线同时决定高光与折射 | GlassMaterialPreset.cs | 已扫描 |
| `docs/archive/0.3.80-用户昼夜录像诊断.md` | 历史归档 | 验证 / 美术 | 从用户昼夜两段录屏各取 6 帧，定位"白天通透、夜晚灰膜"的具体差异 | GlassMaterialPreset.cs | 已扫描 |
| `docs/archive/0.3.81-玻璃流光可见度增强.md` | 历史归档 | 材质 | 仅提高自有材质的曝光（6→10）与高光系数（2.2→3.0），不加叠层 | SwordFlowMaterial.cs | 已扫描 |
| `docs/archive/0.3.82-能量玻璃与剑身取色.md` | 历史归档 | 材质 | 引入实际剑身帧取色 + 整面能量发光（后续被 0.3.83 判定压平反差） | SwordFlowPalette.cs | 已扫描 |
| `docs/archive/0.3.83-保留剑色恢复玻璃反差.md` | 历史归档 | 材质 | 保留 0.3.82 取色、撤回整面发光，恢复逐通道压缩与弱内部底色 | SwordFlowMaterial.cs | 已扫描 |
| `docs/archive/0.3.83-演示与真实鼠标攻击核对.md` | 历史归档 | 验证 | 核对正式客户端日志与三份包 SHA256 一致，排除"未更新"归因 | artifacts/install-result.json | 已扫描 |
| `docs/archive/0.3.84-指定旧版材质恢复.md` | 历史归档 | 材质 | 承认 0.3.83 只回退发光、漏回退反光染色 0.55→0.22 与刃缘 0.30→0.64 | SwordFlowMaterial.cs | 已扫描 |
| `docs/archive/0.3.85-特效回退至0.3.79.md` | 历史归档 | 材质 / 回滚 | 以 `artifacts/ZenithReborn-0.3.79-source.zip` 为依据整体回退，不按截图估参 | （0.3.90 复用同一依据） | 已扫描 |
| `docs/archive/0.3.86-亮刃与玻璃质感融合.md` | 历史归档 | 材质 | 以 0.3.80 为基底补回 0.3.79 的薄亮刃缘/内侧细边/窄扫光 | SwordFlowMaterial.cs | 已扫描 |
| `docs/archive/0.3.87-融合版沿用79配色.md` | 历史归档 | 材质 | 只把新增亮刃与扫光颜色对齐 0.3.79 调色表，不动质感参数 | SoulVisuals.cs | 已扫描 |
| `docs/archive/0.3.88-实际剑色与玻璃流纹.md` | 历史归档 | 材质 | 从实际剑身帧采样主色（缓存键含物品/帧/纹理），银白剑保持无彩色 | SwordFlowPalette.cs | 已扫描 |
| `docs/archive/0.3.89-夜间环境反射灰纹修订.md` | 历史归档 | 材质 | 贴图消融实验定位夜间灰白粗纹来源，加暗处同源平滑环境反射 | （已收藏不采用） | 已扫描 |
| `docs/archive/0.3.9-矿石族飞剑实装.md` | 历史归档 | 矿石族 | 百炼飞剑首版（含被否的 25 槽方案与 `MissingResourceException` 贴图热修） | OreFlyingSword.cs | 已扫描 |
| `docs/archive/0.3.90-以原始79恢复开发基线.md` | 历史归档 | 材质 / 基线 | 以 0.3.79 源码为唯一恢复依据，120 项生产文件逐字节一致仅改版本号 | artifacts/ZenithReborn-0.3.79-source.zip | 已扫描 |
| `docs/archive/0.3.91-透明主体与有色高光候选.md` | 历史归档 | 材质 | 按"主体透明 + 过渡色即高光"思路做的候选，附三色高光导出图，未获美术认可 | SwordFlowMaterial.cs | 已扫描 |
| `docs/archive/Boss分组-六方势力.md` | 历史归档 | 诅咒 / 设计 | 并行阵营线背景（克眼+月总、克脑+教徒、世吞+骷髅王等），明确本页是势力背景非封印组 | docs/六诅咒-Boss分配与成长节奏.md | 已扫描 |
| `docs/archive/Boss分组-四道封印.md` | 历史归档 | 诅咒 / 设计 | 全文 3 行的废弃说明：按"幕/封印"线性分法被六方势力取代 | — | 已扫描 |
| `docs/archive/DeepSeek开发交接-v0.3.11.md` | 历史归档 | 交接 | 会随版本更新的长交接文件（14 KB），顶部堆叠 0.3.89/0.3.90/0.3.91 状态，正文为 0.3.11 基线 | AGENTS.md · docs/CHANGELOG.md | 已扫描 |
| `docs/archive/DeepSeek开发交接-v0.3.8.md` | 历史归档 | 交接 | 0.3.8 期交接：基线、阅读顺序、模块清单与"最小不动点/禁止暂停槽自举"约定 | AGENTS.md | 已扫描 |
| `docs/archive/DeepSeek接手提示词.md` | 历史归档 | 交接 | 接手提示词（13 KB），顶部同样堆叠到 0.3.91，正文含协作禁令与证据等级 | CODEBUDDY.md | 已扫描 |
| `docs/archive/codebase-map-survey-2026-11-03/A-soul-core.md` | 调查报告（Markdown，488 行） | 建图调查归档 | A 组：剑魂核心（soul-core）32 个 `.cs` 的模块级说明 + 逐文件六列索引；开篇声明按建图当天的磁盘状态描述，不把未提交改动当异常 | docs/FILE_INDEX.md 第一节、docs/CODEBASE_MAP.md | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/B-families.md` | 调查报告（Markdown，515 行） | 建图调查归档 | B 组：族系统（木质 / 矿石 / 飞刃 / 神圣天堂 / 剑灵 / 环境属性 / 归属标签）39 个 `.cs`；给出「族计数只有一个真源 SoulSupport.FamilyItems」等 4 条阅读前提 | docs/FILE_INDEX.md 第一节、SoulSupport.cs、docs/环境族E1-21环境与武器映射.md | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/C-ui.md` | 调查报告（Markdown，267 行） | 建图调查归档 | C：UI 层 22 个文件 22/22 通读；说明 `SoulUI` 这个 `ModSystem` 按「界面区域 + 职责层」拆成 18 个 partial 的切割逻辑 | docs/FILE_INDEX.md 第一节、SoulUI.cs、docs/ARCHITECTURE.md | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/D-render-vfx.md` | 调查报告（Markdown，509 行） | 建图调查归档 | D：渲染 / 特效 / 着色器 / 资产——32 个渲染相关 `.cs` + `Assets/**` + `Localization/*.hjson` + `build.txt` + csproj，按特效总闸等组划分 | docs/FILE_INDEX.md 第一、二节、docs/武器分类表-常规与非常规.md | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/E-checks-scripts.md` | 调查报告（Markdown，566 行） | 建图调查归档 | E：`checks/` 编排脚本与静态检查 93 项（37 `.ps1` + 54 `.py` + 2 个项目文件）；含覆盖统计、逐条索引、主线与 5 个核心脚本的调用/断言顺序、9 条坑 | docs/FILE_INDEX.md 第三节、checks/、AGENTS.md | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/F-checks-fixtures.md` | 调查报告（Markdown，451 行） | 建图调查归档 | F：`checks/ZenithPrototypeCheck/` 夹具体系 94 个 `.cs` 的逐条索引，条目取自各检查的 `check(...)` 描述串；开篇纠正「100 个 `.cs`」的误计 | docs/FILE_INDEX.md 第三节、checks/ZenithPrototypeCheck/ | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/G-docs-current.md` | 调查报告（Markdown，319 行） | 建图调查归档 | G：`docs/` 顶层 64 篇现行文档的现状建图（不含 `docs/archive/` 的 116 篇），源码基线 `build.txt` = 0.3.130 | docs/FILE_INDEX.md 第四节、docs/ | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/H-docs-archive.md` | 调查报告（Markdown，312 行） | 建图调查归档 | H：`docs/archive/` 116 篇历史归档的索引 + 仓库根 5 个文件（`AGENTS.md`/`README.md`/`CODEBUDDY.md`/`.gitignore`/`.gitattributes`）；归档类型统一记「历史归档」 | docs/FILE_INDEX.md 第二节第四小节、docs/archive/ | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/I-tools-vfx.md` | 调查报告（Markdown，632 行） | 建图调查归档 | I：`tools/`（222 文件、5 个子目录）与 `VfxLibrary/`（11756 文件）；文件数与分组统计全部由 shell 命令生成，同目录 vfx-library-manifest.txt 是它的产物 | docs/FILE_INDEX.md 第五、六节、vfx-library-manifest.txt | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/README.md` | Markdown 目录说明 | 建图调查归档 | 本目录的说明与纪律：2026-11-03 首次建图的原始调查报告为什么从 artifacts/ 复制入库、为什么只读不得修改、以及克隆仓库后如何复现 docs/FILE_INDEX.md | checks/build_file_index.py、checks/map_audit.py、docs/FILE_INDEX.md | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/archived-oneoff-manifest.txt` | 分组成员清单（纯文本，20 行） | 建图调查归档 | `checks/archived-oneoff/` 20 个一次性脚本的成员清单；索引里只占一行分组行，map_audit.py 把它展开成成员后才算已索引（分母不变） | F-checks-fixtures.md 5.1~5.2、checks/map_audit.py 的 GROUP_MANIFESTS | 归档副本·只读 |
| `docs/archive/codebase-map-survey-2026-11-03/vfx-library-manifest.txt` | 逐文件清单（纯文本，11756 行） | 建图调查归档 | `VfxLibrary/` 的 11756 条相对路径快照；审计第五部分比对「清单行数 = 去重行数 = 磁盘文件数」，不一致默认只告警，`--strict-vfx` 可升级为失败 | I-tools-vfx.md、checks/map_audit.py 第五部分 | 归档副本·只读 |
| `docs/archive/临时试用原始0.3.80.md` | 历史归档 | 验证 / 版本管理 | 保存 0.3.88 到 `artifacts/vfx-presets/0.3.88/` 后临时换装原始 0.3.80 的操作与哈希核对 | artifacts/vfx-presets/ | 已扫描 |
| `docs/archive/交接-2026-10-24-2030.md` | 历史归档 | 交接 | 一次性交接：四区遮罩已接入、三个新资源进包但后两个未绑定到武器 | VfxMaskData.cs | 已扫描 |
| `docs/archive/六诅咒-Boss分配与成长节奏.md` | 历史归档 | 诅咒 / 设计 | 六诅咒与 Boss 组的对应表、成长节奏，含"五种新祝福已采纳"更新头 | SoulCurses.cs | 已扫描 |
| `docs/archive/六诅咒重构-切换与独行.md` | 历史归档 | 诅咒 / 设计 | 切换与独行的核心限制/解除后能力表，标注以 0.3.27 实现记录为准 | SoulCurses.cs | 已扫描 |
| `docs/archive/剑族R2-环境系统后的种族重构.md` | 历史归档 | 种族 / 设计 | 环境系统后 103 项名册的种族重构，头部标注 2026-10-30 已删除武技与后续新增种族 | WeaponFamilies.cs | 已扫描 |
| `docs/archive/剑族与单剑特性设计.md` | 历史归档 | 种族 / 设计 | D1 时代十三族与单剑特性候选表，已被 R2 重构，仅留历史候选 | WeaponFamilies.cs | 已扫描 |
| `docs/archive/动态蒙版M1-底色与形状分离.md` | 历史归档 | 特效 / 设计 | 隔离模组内的"动态底色 + 形状蒙版"结构演示，未进生产 | VfxSwordMask.cs | 已扫描 |
| `docs/archive/开发复盘与DeepSeek交接要求.md` | 历史归档 | 交接 / 复盘 | 0.2.4 时期复盘：对具体开发行为负责、证据边界、交接要求 | CODEBUDDY.md | 已扫描 |
| `docs/archive/开源材质制作方案-2026-10-24.md` | 历史归档 | 材质 / 调研 | 调研 Blender Principled BSDF 与开源材质库的复用可行性；头部注明最终未集成 Filament/MaterialX | SurfaceMaterialDefinition.cs | 已扫描 |
| `docs/archive/武技族WJ1-剑势与招式开发准备.md` | 历史归档 | 武技（已删） | 主修/副修、十把剑各有主动招式的首版草案，用户判定"鸡肋"未采纳 | — | 已扫描 |
| `docs/archive/武技族WJ2-三档剑势与天顶巨剑.md` | 历史归档 | 武技（已删） | 三档剑势与天顶巨剑的攻击体系设计，头部标注 2026-10-30 武技组已删除 | — | 已扫描 |
| `docs/archive/液态玻璃-独立高度场原型.md` | 历史归档 | 材质 / 原型 | 检查模组内的平面高度场着色器原型（曲面起伏→法线→反射折射），未接入生产 | SurfaceMaterialRenderer.cs | 已扫描 |
| `docs/archive/特效审核-用户反馈以R2为基准-2026-10-23.md` | 历史归档 | 美术 / 评审 | 用户原话"钩子巨丑，R2 已经很不错"，确立 R2 为视觉基准并优先于前轮返工要求 | — | 已扫描 |
| `docs/archive/特效审核-钛金剑SwordBody-R1-2026-10-23.md` | 历史归档 | 美术 / 评审 | R1 审核不通过：箭头式大色块遮住原版剑体、边缘流向不足 | VfxSwordMask.cs | 已扫描 |
| `docs/archive/特效审核-钛金剑SwordBody-R2-2026-10-23.md` | 历史归档 | 美术 / 评审 | R2 复审仍不通过：接近"原版剑 + 细轮廓 + 波动尾线"，要求单例做 R3 | VfxSwordMask.cs | 已扫描 |
| `docs/archive/特效审核-钛金剑SwordBody-R3-2026-10-23.md` | 历史归档 | 美术 / 评审 | R3 仍不通过：剑身能量可读性弱于 R2，鼓包变成向后滑动的尖鳍片 | VfxSwordMask.cs | 已扫描 |
| `docs/archive/玩家武器偏好调研-2026-10-22.md` | 历史归档 | 设计 / 调研 | 检索 r/Terraria 与 r/CalamityMod 真实讨论整理武器偏好，声明非强度榜 | docs/武技族WJ2 | 已扫描 |
| `docs/archive/环境属性E4-21机制与共享增幅候选.md` | 历史归档 | 环境 / 设计 | 21 种环境机制与 10% 线性增幅候选（已被每环境 20% + 森林 15% 取代） | docs/环境属性E8 | 已扫描 |
| `docs/archive/第一阶段计划-史莱姆王与附着.md` | 历史归档 | 设计 / 剧情 | 0.2.x 第一阶段计划（被庇护者的回报），记录附着玩法的原始目标 | AttachmentStory.cs | 已扫描 |
| `docs/archive/能量薄刃A1-八张贴图验收与制作候选.md` | 历史归档 | 特效 / 资产 | 验收 8 张 512×512 灰度图并给 U 向滚动配方（V 频率 0.8、纵向速度 0） | artifacts/vfx-textures/ | 已扫描 |
| `docs/archive/需求-天顶飞剑拖尾与剑影形态.md` | 历史归档 | 需求 / 特效 | 面向外部模型的需求书：只写现状与期望、不给实现建议，含 116 行现状描述 | SoulTrailUnits.cs | 已扫描 |
| `docs/archive/验证记录-v0.3.11.md` | 历史归档 | 验证 | 0.3.11 的编译/夹具证据清单（零警告、生产与检查项目） | checks/ | 已扫描 |
| `docs/六祝福-机制定稿与御剑爆发.md` | 设计定稿 | 战斗/祝福 | 六祝福机制表 + 御剑蓄条爆发定稿 | SoulCurses.cs, SoulBlessings.cs, BlessingCamera.cs | 已验证 |
| `docs/协作开发指南.md` | Markdown 流程文档 | 协作与上手 | 两人及以上共写时的同步流程：权限开放方式、首次上手、分支 + PR 节奏、冲突高发点与处理、不可提交清单、换行符与证据分级约定 | docs/AI接入指南.md、checks/dev_setup.ps1、.gitignore | 人工补写 |
| `docs/原版天顶飞剑源码.md` | 参考表 | 参考资料 | Projectile 933 FinalFractal 反编译摘录与复现命令 | SoulBlade.cs | 已验证 |
| `docs/战斗乘区总表与减益覆盖审计.md` | 参考表 | 战斗设计 | 12 大类 61 维度乘区目录 + 减益覆盖审计 | SoulCombat.cs, SoulTraits.cs | 部分待核实 |
| `docs/新种族-召唤出战武器候选.md` | 设计草案 | 种族/剑灵 | 剑灵候选武器表（附魔剑/飞刀/波涌/无头骑士） | LivingArmamentFamily.cs | 已验证 |
| `docs/新种族-召唤出战武器设计草案.md` | 设计草案 | 种族/剑灵 | 剑灵核心机制草案：代理玩家走原版攻击通道 | LivingArmament*.cs, LivingWeaponProxy.cs | 已验证 |
| `docs/木质族D2-环境特性与森林之力.md` | 已撤销 | 种族/木质 | 12 种木质族 + 森林之力 6 属性成长设计 | WoodTypes.cs, ForestPower.cs | 已验证 |
| `docs/武器分类表-常规与非常规.md` | 设计草案 | 种族/分类 | 101 项七族 R2 分族与阶段编号表 | WeaponFamilies.cs | 已验证 |
| `docs/特效库M2-分类与剑身一体组.md` | 设计草案 | 特效/素材 | 剑身一体蒙版库的分类与复用约定 | VfxLibrary/, File: SwordBodyVectorMask.cs | 已验证 |
| `docs/特效库M3-钛金剑一体蒙版打磨.md` | 设计草案 | 特效/素材 | 钛金剑 R2 单例蒙版与隔离 GPU 审核 | VfxLibrary/Masks/SwordBody/, File: SwordBodyMaskPreview.cs | 已验证 |
| `docs/特效库M4-矿石剑批量蒙版.md` | 设计草案 | 特效/素材 | 把 R2 配方铺到其余 21 把矿石剑 | SwordBodyVectorMask.cs, run_swordbody_preview.ps1 | 已验证 |
| `docs/特效开关总清单-56项.md` | 参考表 | 特效/开关 | 逐项开关表 + 历代默认值变迁 | VfxIsolation.cs, VfxPartDebug.cs | 已验证 |
| `docs/环境属性E2-与种族并行的多属性模型.md` | 设计草案 | 环境属性 | 环境为叠加在所有剑上的独立维度 | EnvironmentAttributes.cs | 已验证 |
| `docs/环境属性E3-独立机制与共享羁绊强化.md` | 设计草案 | 环境属性 | 每环境独立机制 + 共享羁绊增幅（已被 E5 修正） | EnvironmentRules.Shared | 已验证 |
| `docs/环境属性E5-适应性与环境祝福领域.md` | 设计定稿 | 环境属性 | 属性/机制分离，环境祝福常驻，环境领域切换 | EnvironmentRules.BlessingSlots, EnvironmentPlayer.cs | 已验证 |
| `docs/环境属性E6-21环境效果设计表.md` | 设计定稿 | 环境属性 | 21 环境逐项机制效果表 | EnvironmentRules.MechanismNames | 已验证 |
| `docs/环境属性E7-低基础数值候选.md` | 设计定稿 | 环境属性 | 21 环境按价值分别定价的基础值 | EnvironmentRules.Values | 已验证 |
| `docs/环境属性E8-每环境20与森林15增幅.md` | 设计定稿 | 环境属性 | 数量加算 20%/环境，森林固定 ×1.15 | EnvironmentRules.Shared/Forest | 已验证 |
| `docs/环境族E0-分类原则.md` | 设计定稿 | 环境属性 | 分类入口：环境为并行属性，非互斥种族 | 环境族E1, 环境属性E2 | 已验证 |
| `docs/环境族E1-21环境与武器映射.md` | 设计定稿 | 环境属性 | 91 项剑的 21 环境归属表 | EnvironmentTooltips.cs, 武器分类表 | 已验证 |
| `docs/矿石族M1-八种基础金属.md` | 设计草案 | 种族/矿石 | 八材质 16 把短剑/阔剑机制草案 | OreFamily.cs | 已验证 |
| `docs/矿石族M2-肉后三矿.md` | 设计定稿 | 种族/矿石 | 六把肉后矿剑单剑机制，0.3.6 实装 | HardOreTypes.cs, HardOre*.cs | 已验证 |
| `docs/矿石族羁绊-百炼飞剑.md` | 设计定稿 | 种族/矿石 | 锻金族按材质计数与环绕齐射羁绊 | OreFamily.MaterialCount, OreFlyingSword.cs | 已验证 |
| `docs/飞刃族F1-掷刃与印记.md` | 设计定稿 | 种族/飞刃 | 掷刃机制已实装，印记为留白核心设计 | FlyingBladeHand.cs, FlyingBladeShot.cs | 已验证 |
| `docs/飞刃族F2-数量分阶与族显示.md` | 设计定稿 | 种族/飞刃 | 前四把阶梯效果 + 族归属显示 | FlyingBladeFamily.cs, WeaponFamilyTags.cs | 已验证 |
| `docs/飞刃族FB1-候选名册与攻击方式.md` | 设计草案 | 种族/飞刃 | 引擎普查产出的 31 把名册与攻击方式 | FlyingBladeFamily.Weapons, flying-blade-survey.json | 已验证 |
| (合计) | 199 条 |  |  |  |  |

---

## 五、`tools/` 工具

反编译参考、材质实验室与第三方工具包。

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `tools/README.md` | md | tools 根 | 目录总说明表 | 指向其余 4 项 | 已验证 |
| `tools/ZenithMaterialLab/Assets/Diagnostics/RegionMask.zrdata` | zrdata |  | 四区诊断原始 RGBA8 载荷 |  |  |
| `tools/ZenithMaterialLab/GlobalUsings.cs` | cs |  | 显式 global using（tModLoader 原生编译不读隐式命名空间）（8 行） |  |  |
| `tools/ZenithMaterialLab/LabArtworkCheck.cs` | cs |  | 素材哈希与来源核对（64 行） |  |  |
| `tools/ZenithMaterialLab/LabChecks.cs` | cs |  | 45 组 GPU A/B 逐项核对（127 行） |  |  |
| `tools/ZenithMaterialLab/LabPipelineCheck.cs` | cs |  | 管线状态检查（87 行） |  |  |
| `tools/ZenithMaterialLab/LabPreview.cs` | cs |  | 预览渲染：球体/平面/剑迹网格、棋盘、校准树干（64 行） |  |  |
| `tools/ZenithMaterialLab/LabRegionCheck.cs` | cs |  | RGBA 四区载荷校验，重点验证 Alpha=0 处 RGB 不被清空（55 行） |  |  |
| `tools/ZenithMaterialLab/LabResources.cs` | cs |  | 素材槽加载与刷新（color/mask/normal/surface/height/emission/environment/regions/background）（130 行） |  |  |
| `tools/ZenithMaterialLab/Localization/en-US_Mods.ZenithMaterialLab.hjson` | hjson |  | 本地化 |  |  |
| `tools/ZenithMaterialLab/MaterialLabSystem.cs` | cs |  | 工作台系统：UI、滑块、加载/保存/导出、撤销重做、输入（184 行） |  |  |
| `tools/ZenithMaterialLab/README.md` | md |  | 完整使用说明：打开方式、操作、素材约定表、导出、区域图工具 |  |  |
| `tools/ZenithMaterialLab/ZenithMaterialLab.cs` | cs |  | **Mod 入口**（5 行，`class ZenithMaterialLab : Mod`） |  |  |
| `tools/ZenithMaterialLab/ZenithMaterialLab.csproj` | csproj |  | net8.0，`Reference ZenithReborn.dll`，`Import tMLMod.targets` |  |  |
| `tools/ZenithMaterialLab/bin/Release/net8.0/ZenithMaterialLab.deps.json` | json |  | 依赖图 |  |  |
| `tools/ZenithMaterialLab/bin/Release/net8.0/ZenithMaterialLab.dll` | dll |  | 编译产物 |  |  |
| `tools/ZenithMaterialLab/bin/Release/net8.0/ZenithMaterialLab.pdb` | pdb |  | 符号 |  |  |
| `tools/ZenithMaterialLab/build.txt` | txt |  | 模组元数据：v0.1.3，`modReferences = ZenithReborn@0.3.80`，`includeSource = true` |  |  |
| `tools/ZenithMaterialLab/description.txt` | txt |  | 模组描述（开发者独立工作台，不装进正式组合） |  |  |
| `tools/ZenithMaterialLab/obj/` | json/xml | 分组 | NuGet 还原 |  |  |
| `tools/ZenithMaterialLab/obj/Debug/net8.0/` | 混合 | 分组 | Debug 中间产物 |  |  |
| `tools/ZenithMaterialLab/obj/Release/net8.0/` | 混合 | 分组 | Release 中间产物 |  |  |
| `tools/ZenithMaterialLab/obj/project.assets.json` | json |  | 依赖资产 |  |  |
| `tools/build_vanilla_vfx_catalog.py` | py | tools 根 | 重建 Vanilla 图鉴 | 读 `decomp/ProjectileID` + `artifacts/vanilla-*ID.reference.cs` | 已验证 |
| `tools/decomp/Terraria.Graphics.FinalFractalHelper.decompiled.cs` | cs | decomp | 天顶剑最终分形绘制（204 行） | 多剑齐射视觉 | 第三方-未深入 |
| `tools/decomp/Terraria.Graphics.Light.LightingMode.decompiled.cs` | cs | decomp | **0 字节，空文件** | 未确认原因 | 第三方-未深入 |
| `tools/decomp/Terraria.Graphics.VertexStrip.decompiled.cs` | cs | decomp | 顶点带网格构造器（264 行） | 拖尾几何 | 第三方-未深入 |
| `tools/decomp/Terraria.ID.ProjectileID.decompiled.cs` | cs | decomp | 弹幕 ID 常量表（2864 行） | **被 `build_vanilla_vfx_catalog.py:11` 读取** | 第三方-未深入 |
| `tools/decomp/Terraria.LightMode.decompiled.cs` | cs | decomp | **0 字节，空文件** | 未确认原因 | 第三方-未深入 |
| `tools/decomp/Terraria.Main.decompiled.cs` | cs | decomp | 原版 `Terraria.Main` 反编译（87341 行 / 3.9 MB） | 光照与绘制顺序查证 | 第三方-未深入 |
| `tools/decomp/Terraria.Projectile.decompiled.cs` | cs | decomp | 原版 `Terraria.Projectile`（94255 行 / 3.9 MB） | 飞剑/拖尾行为对齐 | 第三方-未深入 |
| `tools/decomp/tModLoader.list.txt` | txt | decomp | 类型/枚举名清单（160 行） | 查类型存在性 | 第三方-未深入 |
| `tools/desktop_control.ps1` | ps1 | tools 根 | 窗口聚焦/鼠标键盘自动化 | `user32.dll` | 已验证 |
| `tools/ilspy/ilspycmd.zip` | zip |  | NuGet 包原件（4.18 MB） |  |  |
| `tools/ilspy/index.json` | json |  | 上游版本索引清单 |  |  |
| `tools/ilspy/pkg/.signature.p7s` | p7s |  | NuGet 包签名 |  |  |
| `tools/ilspy/pkg/ILSpyCmdNuGetPackageIcon.png` | png |  | 包图标 |  |  |
| `tools/ilspy/pkg/README.md` | md |  | 包说明 |  |  |
| `tools/ilspy/pkg/[Content_Types].xml` | xml |  | OPC 内容类型 |  |  |
| `tools/ilspy/pkg/_rels/.rels` | rels |  | OPC 根关系 |  |  |
| `tools/ilspy/pkg/ilspycmd.nuspec` | nuspec |  | **版本 9.1.0.7988 的唯一证据** |  |  |
| `tools/ilspy/pkg/package/services/metadata/core-properties/b41afabca47440d49a71e4d9b3a71796.psmdcp` | psmdcp |  | NuGet 核心属性 |  |  |
| `tools/xnbcli-binary/lz4.node` | node |  | 原生 LZ4 绑定 |  |  |
| `tools/xnbcli-binary/pack.bat` | bat |  | 打包入口 |  |  |
| `tools/xnbcli-binary/packed/PACKED.md` | md |  | 投放说明 |  |  |
| `tools/xnbcli-binary/unpack.bat` | bat |  | 解包入口 |  |  |
| `tools/xnbcli-binary/unpacked/UNPACKED.md` | md |  | 投放说明 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/HEAD` | git |  | 上游 clone 的 HEAD 指针 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/config` | git |  | 远端地址（固定上游 commit） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/description` | git |  | 仓库描述 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/applypatch-msg.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/commit-msg.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/fsmonitor-watchman.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/post-update.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/pre-applypatch.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/pre-commit.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/pre-merge-commit.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/pre-push.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/pre-rebase.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/pre-receive.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/prepare-commit-msg.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/push-to-checkout.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/sendemail-validate.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/update.sample` | git |  | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/index` | git |  | 暂存区索引 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/info/exclude` | git |  | 忽略规则 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/logs/HEAD` | git |  | reflog |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/logs/refs/heads/master` | git |  | 分支 reflog |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/logs/refs/remotes/origin/HEAD` | git |  | 远端 reflog |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/objects/pack/pack-b6c07b17749e803d99e73649f07a11aafb2ce51d.idx` | git |  | 对象包索引 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/objects/pack/pack-b6c07b17749e803d99e73649f07a11aafb2ce51d.pack` | git |  | 对象包 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/objects/pack/pack-b6c07b17749e803d99e73649f07a11aafb2ce51d.rev` | git |  | 反向索引 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/packed-refs` | git |  | 打包引用 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/refs/heads/master` | git |  | 分支引用 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/refs/remotes/origin/HEAD` | git |  | 远端引用 |  |  |
| `tools/xnbcli-binary/xnbcli-upstream-git/shallow` | git |  | **shallow clone 标记** |  |  |
| `tools/xnbcli-binary/xnbcli.exe` | exe |  | **编译好的 xnbcli 可执行** |  |  |
| `tools/xnbcli-binary/xxhash.node` | node |  | 原生 xxHash 绑定 |  |  |
| `tools/xnbcli-windows-x64.zip` | zip | tools 根 | xnbcli 发行包下载件（12.2 MB） | 与 `xnbcli-binary/` 同源 | 第三方-未深入 |
| `tools/xnbcli/.gitignore` | 配置 |  | 上游自身忽略规则 |  |  |
| `tools/xnbcli/.travis.yml` | 配置 |  | 上游 CI |  |  |
| `tools/xnbcli/LICENSE.LESSER` | 文本 |  | LGPL v3 正文（7.8 KB） |  |  |
| `tools/xnbcli/LICENSE.md` | md |  | 许可说明（35.8 KB） |  |  |
| `tools/xnbcli/README.md` | md |  | 用法、许可 |  |  |
| `tools/xnbcli/app/BufferReader.js` | js |  | 二进制流读取（15.7 KB） |  |  |
| `tools/xnbcli/app/BufferWriter.js` | js |  | 二进制流写入 |  |  |
| `tools/xnbcli/app/Enum.js` | js |  | 枚举辅助 |  |  |
| `tools/xnbcli/app/Log.js` | js |  | 日志 |  |  |
| `tools/xnbcli/app/Porter.js` | js |  | **XNB 编解码主流程** |  |  |
| `tools/xnbcli/app/Presser/Lzx.js` | js |  | **LZX 解压（31.4 KB）** |  |  |
| `tools/xnbcli/app/Presser/index.js` | js |  | 压缩分发 |  |  |
| `tools/xnbcli/app/Struct.js` | js |  | 结构描述 |  |  |
| `tools/xnbcli/app/Xact/AudioEngine.js` | js |  | 音频引擎 |  |  |
| `tools/xnbcli/app/Xact/SoundBank.js` | js |  | 音库 |  |  |
| `tools/xnbcli/app/Xact/WaveBank.js` | js |  | 波形库（空壳，55 B） |  |  |
| `tools/xnbcli/app/Xact/XactClip.js` | js |  | 音频片段 |  |  |
| `tools/xnbcli/app/Xact/XactSound.js` | js |  | 声音条目 |  |  |
| `tools/xnbcli/app/Xact/index.js` | js |  | XACT 音频分发 |  |  |
| `tools/xnbcli/app/Xnb/ReaderResolver.js` | js |  | reader 解析分发 |  |  |
| `tools/xnbcli/app/Xnb/Readers/ArrayReader.js` | js |  | 数组 |  |  |
| `tools/xnbcli/app/Xnb/Readers/BaseReader.js` | js |  | reader 基类 |  |  |
| `tools/xnbcli/app/Xnb/Readers/BmFontReader.js` | js |  | BMFont |  |  |
| `tools/xnbcli/app/Xnb/Readers/BooleanReader.js` | js |  | 布尔 |  |  |
| `tools/xnbcli/app/Xnb/Readers/CharReader.js` | js |  | 字符 |  |  |
| `tools/xnbcli/app/Xnb/Readers/DictionaryReader.js` | js |  | 字典 |  |  |
| `tools/xnbcli/app/Xnb/Readers/DoubleReader.js` | js |  | double |  |  |
| `tools/xnbcli/app/Xnb/Readers/EffectReader.js` | js |  | 着色器效果 |  |  |
| `tools/xnbcli/app/Xnb/Readers/Int32Reader.js` | js |  | Int32 |  |  |
| `tools/xnbcli/app/Xnb/Readers/ListReader.js` | js |  | 列表 |  |  |
| `tools/xnbcli/app/Xnb/Readers/NullableReader.js` | js |  | 可空 |  |  |
| `tools/xnbcli/app/Xnb/Readers/RectangleReader.js` | js |  | Rectangle |  |  |
| `tools/xnbcli/app/Xnb/Readers/ReflectiveReader.js` | js |  | 反射类型 |  |  |
| `tools/xnbcli/app/Xnb/Readers/SingleReader.js` | js |  | float |  |  |
| `tools/xnbcli/app/Xnb/Readers/SoundEffectReader.js` | js |  | 音效 |  |  |
| `tools/xnbcli/app/Xnb/Readers/SpriteFontReader.js` | js |  | SpriteFont |  |  |
| `tools/xnbcli/app/Xnb/Readers/StringReader.js` | js |  | 字符串 |  |  |
| `tools/xnbcli/app/Xnb/Readers/TBinReader.js` | js |  | TBin（泰拉瑞亚自定义二进制） |  |  |
| `tools/xnbcli/app/Xnb/Readers/Texture2DReader.js` | js |  | **贴图解包（本项目主用途）** |  |  |
| `tools/xnbcli/app/Xnb/Readers/UInt32Reader.js` | js |  | UInt32 |  |  |
| `tools/xnbcli/app/Xnb/Readers/Vector2Reader.js` | js |  | Vector2 |  |  |
| `tools/xnbcli/app/Xnb/Readers/Vector3Reader.js` | js |  | Vector3 |  |  |
| `tools/xnbcli/app/Xnb/Readers/Vector4Reader.js` | js |  | Vector4 |  |  |
| `tools/xnbcli/app/Xnb/Readers/index.js` | js |  | reader 注册表 |  |  |
| `tools/xnbcli/app/Xnb/TypeReader.js` | js |  | 类型读取基类 |  |  |
| `tools/xnbcli/app/Xnb/index.js` | js |  | XNB 容器读写（13.5 KB） |  |  |
| `tools/xnbcli/app/XnbError.js` | js |  | 错误类型 |  |  |
| `tools/xnbcli/appveyor.yml` | 配置 |  | 上游 CI |  |  |
| `tools/xnbcli/pack.bat` | 脚本 |  | 打包入口（3 平台） |  |  |
| `tools/xnbcli/package-lock.json` | json |  | 依赖锁定（13.4 KB） |  |  |
| `tools/xnbcli/package.json` | json |  | **版本 1.0.7**、依赖、脚本 |  |  |
| `tools/xnbcli/packed/PACKED.md` | md |  | 待解包投放说明 |  |  |
| `tools/xnbcli/unpack.bat` | 脚本 |  | 解包入口（3 平台） |  |  |
| `tools/xnbcli/unpacked/UNPACKED.md` | md |  | 待打包投放说明 |  |  |
| `tools/xnbcli/xnbcli.js` | js |  | CLI 入口（7.7 KB） |  |  |
| `tools/打开材质实验室.cmd` | bat | tools 根 | 启动材质实验室 | `checks/run_material_lab.ps1` | 已验证 |
| `tools/打开能量薄刃A1.cmd` | bat | tools 根 | 启动能量薄刃 A1 预览 | `checks/run_energy_blade_preview.ps1` | 已验证 |
| (合计) | 136 条 |  |  |  |  |

---

## 六、`VfxLibrary/` 原版素材库

原版 Terraria 素材快照，不逐个进索引，见本节的分组统计与清单指针。

```text
磁盘文件数   : 11756
清单指针     : docs/archive/codebase-map-survey-2026-11-03/vfx-library-manifest.txt（存在，11756 行）
审计口径     : 不进主分母，单独核对（见 checks/map_audit.py 第五节）
```

```text
按顶层目录：
  Vanilla                   11185
  Masks                     413
  ColorFields               74
  Materials                 58
  References                8
  LayerMasks                6
  (根)                      3
  Shaders                   2
  SurfaceMaps               2
  Brushes                   1
  Examples                  1
  HeightMaps                1
  NoiseMaps                 1
  NormalMaps                1
  (合计)                    11756

按扩展名：
  .png                      7929
  .json                     3792
  .md                       18
  .cs                       3
  .fx                       3
  .fxc                      3
  .py                       3
  .zrdata                   2
  .html                     1
  .js                       1
  .txt                      1
```

逐文件清单不写进本文件（11756 行会淹没其余 650 个文件）。需要查具体素材时直接看清单 txt 或用 `checks/vfx_library.py` 检索。
