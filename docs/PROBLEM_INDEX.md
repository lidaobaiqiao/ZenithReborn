# 天顶复苏 · 诅咒残魂 — 问题反向索引

> 基线：`ZenithReborn/build.txt` `version = 0.3.132`。本文按**当前磁盘内容**描述；偏锋替换后诅咒口径见 0.3.131 记录。
> 用法：从玩家/开发者的**现象**出发，按表内顺序（1 = 最可能）逐个检查「优先检查」列的符号；「关联检查位置」是能复现或证伪该分支的夹具/脚本。
> 所有行号均对照源码核过一次；不确定级别的只写文件名。

## 读表前必须知道的三件事

1. **本仓库的"通过"只到源码 + 已编译 + 夹具**。`artifacts/install-result.json` 里 `graphics_*_tested` 仍为 `false`，视觉与真实多人从未被自动验证。
2. **族计数须区分用途**：木质战斗与面板对同一有效寄宿集合当前应一致；支援格授予另有范围（见「族计数不对」）。
3. **渲染开关有两类**：一类真生效（绘制前 `if` 判断），一类只是调试面板里的数字（见现象 11）。

---

### 现象：物品名称错误、显示成英文类型名或搜不到
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 木质族手写译名表 | `ZenithReborn/WoodTypes.cs:34`（`Names` 8 项）。历史上首项写作"灰烬木剑"，官方 zh-Hans 为**白蜡木剑**（`ItemID.AshWoodSword` = 5284）；"灰烬"是地狱地形 `AshBlock` 的译名。当前已改对，警示注释留在 `:32-33` | `checks/ZenithPrototypeCheck/Check.cs`（内联本地化断言，全库仅 1 条） |
| 2 | 物品显示名走 hjson，不走 `.cs` | `ZenithReborn/Localization/zh-Hans.hjson`（Items 仅 10 条）；原 `SlashZenithTest` 中文侧两键被注释会回落类型名，该遗留测试剑及本地化键已随 T5 删除，此坑已失效（保留历史记录） | `checks/check_mod_assets.py` |
| 3 | 族名/特性名硬编码在代码里 | `ZenithReborn/WoodTypes.cs:36`（`Effects`）、`:60`（`Description`）；`ZenithReborn/OreFamily.cs:21-41`（`Names/Tints`） | `checks/ZenithPrototypeCheck/FamilyAudit*` |
| 4 | 界面文案全部硬编码 | `ZenithReborn/SoulUIPages.cs:8`（"武器寄宿/剑魂特性/成长记忆/环境之力"）。**改界面文案改 `.cs`，改物品名改 hjson** | — |
| 5 | 弹幕中文名缺口 | `ZenithReborn/Localization/zh-Hans.hjson` 的 `Projectiles` 缺 6 条（`FlyingBladeShot`/`HolyHeavenStar`/`HolyHeavenImpact`/`LivingArmament`/`EnvironmentEffect`/`EnvironmentDomain`），这 6 个都有绘制代码 | — |

### 现象：物品拿不到，或缺某个材料剑
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 剑魂本体入手 | `ZenithReborn/Progression.cs:52`（`SoulPlayer` 初始物品 / 掉落），以及 `AttachmentStory.CanStart`（`ZenithReborn/AttachmentStory.cs:39`，白天 + 地表 + 信物或 600px 内史莱姆） | `checks/ZenithPrototypeCheck/`（`UiFixture` 备好剑魂的前置） |
| 2 | 魂芯不掉 / 掉了点不动 | `ZenithReborn/Progression.cs:136`（`OnKill` 按需掉核）；`ZenithReborn/SlotProgression.cs:38`（`RecoverableCore` 三条件） | `checks/ZenithPrototypeCheck/SlotCheck.cs` |
| 3 | 觉醒十把材料剑 | `ZenithReborn/Progression.cs:15`（`ZenithWeapons[10]`）；未集齐不会给觉醒 | `checks/ZenithPrototypeCheck/Check.cs`（十剑觉醒内联套件） |
| 4 | 对侧邪恶召唤物 | `ZenithReborn/CrossSummon.cs:56`（`CrossSummonDrops` 只由**本世界原生**邪恶 Boss 掉对侧） | `checks/ZenithPrototypeCheck/CrossSummonCheck.cs` |
| 5 | 名册里根本没登记这把剑 | `ZenithReborn/FlyingBladeFamily.cs:19`（31 把）、`ZenithReborn/OreFamily.cs:43`（`MaterialSwords` 14 行 22 个 ID）、`ZenithReborn/HolyHeavenFamily.cs:9`（8 把） | `checks/ZenithPrototypeCheck/FlyingBladeCheck.cs`（"可合成与掉落两组名册成员就位"） |

### 现象：某一族完全没生效（剑魂在、剑也在，就是没反应）
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 飞刃族三条件 | `ZenithReborn/FlyingBladeFamily.cs:70` `Enabled = Attached && TraitMains.Any() && Activated` | `checks/ZenithPrototypeCheck/FlyingBladeCheck.cs` |
| 2 | 木质族有效寄宿与特性 | `HostedWood`（`SoulCurses.cs:90`）供阶段/生命/倍率/Full/森林/归冠，`CombatWood`（`:91`）供战斗特性；二者保留独立入口，但 0.3.131 偏锋替换后 `TraitEnabled` 恒为 true，特性不会再因旧切换诅咒只认第一把。看到「8 把却只 1 把」先核对同一剑魂的有效主槽、已生效支援槽及两处实际输入 | `checks/ZenithPrototypeCheck/WoodCheck.cs`、`CurseCheck.cs` |
| 3 | 矿石族入场资格 | `ZenithReborn/OreFamily.cs:85`（`TriggerEligible`：`PrimaryOrdinal>=0` 且 `Count>0`）；未解除切换时 `OreFlightMains` 只取前 4 把（`ZenithReborn/SoulCurses.cs:81`） | `checks/ZenithPrototypeCheck/OreCheck.cs` |
| 4 | 神圣天堂门槛 | `ZenithReborn/HolyHeavenFamily.cs:13` `Enabled` 要求至少一把在 `TraitItems` | `checks/ZenithPrototypeCheck/HolyHeavenCheck.cs`（`Check.cs:63`） |
| 5 | 剑灵族 | `ZenithReborn/LivingArmamentFamily.cs:9`（4 把，两处标注"暂定候选"） | `checks/ZenithPrototypeCheck/LivingArmamentCheck.cs`（`Check.cs:57`） |
| 6 | 族判定被短路 | `ZenithReborn/WeaponFamilies.cs:18`：木质/矿石**优先短路**返回 `Plain`，属设计，不是 bug | — |

### 现象：族计数不对（面板显示 N/M，但实际寄宿数不一样）
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | **战斗侧口径** | `ZenithReborn/SoulCurses.cs:91`：`CombatWood` 的 `FamilyItems` 仍经过 `TraitEnabled`，但该方法当前恒为 true；不应因偏锋塌到 1 | `checks/ZenithPrototypeCheck/CurseCheck.cs` |
| 2 | **族内真源口径**（不过滤） | `ZenithReborn/SoulSupport.cs:43`：`FamilyItems = Active ∪ Supports`，含森林/圣林奖励位与传奇位 | `checks/ZenithPrototypeCheck/WoodCheck.cs` |
| 3 | **UI 口径** | `ZenithReborn/BondSnapshot.cs:59`（`Wood`）直接数 `FamilyItems`；对同一剑魂和有效寄宿集合，当前木质计数应与战斗侧一致 | — |
| 4 | 支援格授予范围 | `ZenithReborn/SoulSupport.cs:45`（`GrantedBy`）：木质只用 `SetItems`，矿石还并入已生效的全收集奖励槽与圣林槽；两者都不靠传奇槽自举支援格 | — |
| 5 | 矿石按**材质**去重，不是按把数 | `ZenithReborn/OreFamily.cs:91`（`Count` → `OreItems`）；短/阔剑同材质只算 1 | `checks/ZenithPrototypeCheck/OreCheck.cs` |
| 6 | 超额被 `Clamp` 吞掉 | `ZenithReborn/BondSnapshot.cs:74`（`Math.Clamp(count,0,max)`），9 把木剑也只显示 8/8 | — |

### 现象：羁绊不触发，或阶段一直不涨
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 复合羁绊的三条额外条件 | `ZenithReborn/WoodBondCore.cs:29`（棕榈 + 铜/铁阔剑）、`:39`（红木 + 乌木 + 魔光/永夜）、`:49`（红木 + 灰烬木剑(源码名白蜡木剑) + 火山） | `checks/ZenithPrototypeCheck/WoodBondCheck.cs` |
| 2 | 阈值表与文案表不同步 | `ZenithReborn/BondSnapshot.cs:38`（`WoodThresholds=[1,2,4,6,8]`）/ `:40`（`OreThresholds` 到 10）vs `ZenithReborn/SoulTraits.cs:26-43` 的写死文案 | `checks/ZenithPrototypeCheck/`（羁绊阶段断言） |
| 3 | 森林之力不涨 | `ZenithReborn/WoodPlayer.cs`（`AdvanceGrowth` 在 `HeldSoul==null` 直接 return；`EffectiveLayers` 还要求 `CurrentSoul==soul.SoulId`）；常量见 `ZenithReborn/ForestPower.cs:14-18`（`BaseCap=6` / 300 帧一层 / 脱战 120 帧 -1） | `checks/ZenithPrototypeCheck/WoodCheck.cs` |
| 4 | 矿石圆满永不回退 | `ZenithReborn/SoulItem.cs:40`（`OreCollectionMask` 只增不减）→ 取走剑后 14/14 仍然成立 | — |
| 5 | 归冠自举被拦 | `ZenithReborn/WoodCrown.cs:20`（`Active` 要求泰拉刃在**非圣林槽**）；重入护盾在 `:18`/`:23` | `checks/ZenithPrototypeCheck/WoodCheck.cs` |

### 现象：伤害倍率与生命加成，和面板显示对不上
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 面板只改数字，真实伤害在别处 | `ZenithReborn/SoulItem.cs:387`（`RefreshStats` 只写物品面板）；真实值 = `ZenithReborn/SoulCombat.cs:9`（`BladeDamage = GetWeaponDamage × Potency × MainDamageMultiplier`） | `checks/ZenithPrototypeCheck/`（夹具模板固定三步含 `RefreshStats()`） |
| 2 | 效力 `Potency` 三档 | `ZenithReborn/SoulItem.cs:89`：未附 0；常态 `.65 + Stage*.04`；觉醒 `1.1` | — |
| 3 | 木质 `Strength/Life` 与归冠覆盖 | `ZenithReborn/WoodTypes.cs:20` / `:23`（归冠覆盖 3.00 / 100） | `checks/ZenithPrototypeCheck/WoodCheck.cs` |
| 4 | 剑影伤害另有 0.8/0.2 惩罚 | `ZenithReborn/SoulEchoDamage.cs:7`（祝福 1 / 解除 `0.8^unit` / 其余 `0.20`） | `checks/check_echo_damage_delivery.py` |
| 5 | 恒星伤害是常量 | `ZenithReborn/HolyHeavenProc.cs:11`（`StarDamage=1000`，**不吃任何倍率**） | `checks/ZenithPrototypeCheck/HolyHeavenCheck.cs` |
| 6 | 飞刃只吃一条公式 | `ZenithReborn/FlyingBladeFamily.cs:63`（`DamageBonus`，+3%/把，上限 +27%） | `checks/ZenithPrototypeCheck/FlyingBladeCheck.cs` |

### 现象：进度不动 / Boss 打了但槽位没解锁
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 门槛是**连续**的，断档后续全不计 | `ZenithReborn/Progression.cs:24`（`Capacity` 逐门槛连续通过才 +1） | `checks/ZenithPrototypeCheck/SlotCheck.cs` |
| 2 | **三套并行 Boss 位映射** | 诅咒 18 位：`ZenithReborn/SoulCurses.cs:24`（`BossSeal`）；槽位 12 位：`ZenithReborn/Progression.cs:34`（`BossBit`）。`Queen/Empress/Duke/Deer` **只在诅咒位存在** → 打它们只解诅咒、不给槽位，属设计 | `checks/ZenithPrototypeCheck/CurseCheck.cs`（"外围 Boss 只记独立胜利不解咒"） |
| 3 | 参与者集合为空 | `ZenithReborn/Progression.cs:92`（`SoulEncounters.Hits`，Boss 消失即清）→ `:136`（`OnKill` 读不到人就不记账） | — |
| 4 | `Cores` 与 `Kills` 是两个独立门 | `ZenithReborn/SlotProgression.cs:29`/`:31`；只写 `Kills` → 槽位永远是 1 个 | `checks/ZenithPrototypeCheck/Check.cs`（明确断言"Boss 旗标单独不能替代魂核"） |
| 5 | 玩家长时间看不到下一步该干什么 | `ZenithReborn/Progression.cs:44`（`Next`）；缺口文案 `ZenithReborn/SlotProgression.cs:47`/`:59` | — |
| 6 | 双 Boss 组需要两个 bit | `ZenithReborn/SoulCurses.cs:19`（`Groups[6][]`，长度决定四档阈值） | — |

### 现象：加伤明显偏高或偏低
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 命中链是**隐式优先级链**，不是事件总线 | `ZenithReborn/SoulBlade.cs:294`（`OnHitNPC` 五族唯一扇出点：木质 → 矿石 → 神圣 → 祝福）；倍率侧在 `:333`（`ModifyHitNPC`） | `checks/ZenithPrototypeCheck/SwingCheck.cs` |
| 2 | 木质八条标记未施加 | `ZenithReborn/WoodCombat.cs:58`（`PreparePrimaryHit` 打标记）→ `:70`（`FinishPrimaryHit` 派发）→ `:152`（`ModifyPrimaryHit`：霜寂 +1%×S / 净辉 +1.5%×S / 盐蚀 +2×S 穿甲） | `checks/ZenithPrototypeCheck/WoodCheck.cs` |
| 3 | 复合羁绊走直伤，不回灌管道 | `ZenithReborn/WoodBondCore.cs:71`（`WoodBondHit` 上下文 + `NPC.StrikeNPC` 直伤）→ 会被别处的增伤再吃一次或完全不吃 | — |
| 4 | 环境乘区只叠在 `SoulSource` 上 | `ZenithReborn/SoulBlade.cs:31`（`SoulSource.ModifyHitNPC`）→ `ZenithReborn/EnvironmentPlayer.cs:155`（`Hit`，21 环境总表） | `checks/ZenithPrototypeCheck/EnvironmentCheck.cs` |
| 5 | 飞刃弹幕**不携带 `SoulId`** | `ZenithReborn/FlyingBladeHand.cs:31` 注释：不吃祝福、环境、矿石命中、木质/矿石管线，只吃 `BladeDamage × DamageBonus` | `checks/ZenithPrototypeCheck/FlyingBladeCheck.cs` |
| 6 | 觉醒 / 祝福期的额外乘区 | `ZenithReborn/SoulBlessings.cs:123`（`DistanceMultiplier` 近身 1 → 远端 +50%）；`ZenithReborn/SoulItem.cs:419` 的 `Unbounded=1e6` 分支会吞掉后续 ×2 | — |

### 现象：环境属性不生效（收集了但没属性 / 有属性但没机制）
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | **"已收集"只给属性，不给机制** | `ZenithReborn/EnvironmentPlayer.cs:35`（`Mechanism` 三级判定：祝福 → 领域 960 → 自然） | `checks/ZenithPrototypeCheck/EnvironmentCheck.cs` |
| 2 | 常驻名额不足，开关静默失败 | `ZenithReborn/EnvironmentRules.cs:15`（`BlessingSlots = Count(mask)/4`，4/8/12/16/20 → 1..5）；`ZenithReborn/EnvironmentRules.cs:106`（`ToggleEnvironmentBlessing` 名额不足 return false，UI 只走 `feedback` 不弹窗） | `checks/run_information_ui_render.ps1` |
| 3 | 武器没绑定环境 | `ZenithReborn/EnvironmentAttributes.cs:87`（`RegisterBuiltins`，**武器→环境唯一手工绑定表**）；未绑定 → tooltip 显示"未归类" | `checks/ZenithPrototypeCheck/EnvironmentCheck.cs` |
| 4 | 互斥群系被折半 | `ZenithReborn/EnvironmentRules.cs:80`（`Adaptation` ×0.5）；群系优先于深度 `:61` | — |
| 5 | 21 项手工同步表漏改 | `ZenithReborn/EnvironmentRules.cs:18`（`MechanismNames[22]`）、`ZenithReborn/EnvironmentUI.cs:17`（21 的硬编码）、`EnvironmentIcons` 必须 1:1 对齐 22 项 | — |

### 现象：特效不显示（拖尾、残影、星芒、玻璃全黑或全无）
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 总闸被打开 | `ZenithReborn/VfxIsolation.cs:12`（`PureCodeOnly => false`，**是表达式不是字段**，不可运行时切换；置 true 会一次关掉 12 个外部输入分支 + 全部贴图加载 + 滤镜） | `checks/run_pure_code_vfx.ps1` |
| 2 | 分支被单关 | `ZenithReborn/VfxIsolation.cs:63`（`Branches` B1–B10，含 B7 玻璃滤镜）；F10 全关 `ZenithReborn/VfxPartDebug.cs:23` | `checks/run_vfx_stripe_ablation.ps1` |
| 3 | 贴图降级成 MagicPixel | `ZenithReborn/SwordSpark.cs:10`、`ZenithReborn/SoulFlowParticles.cs:77` | — |
| 4 | 材质管线整体休眠 | `ZenithReborn/SwordFlowMaterial.cs:52`（`Load` 读 `bindings.json`，其 `weapons` 为 `{}`）→ `surfaceMaterials` 为空 → 永远走常量路径 | `checks/check_vfx_ownership.py` |
| 5 | 折射背景恒为空 | `ZenithReborn/SoulSceneRefraction.cs:64`（`PrepareNormals`）；`Capture()` 全库无调用 → `Scene` 恒 `null`，`uSceneUvTransform` 恒 `(0,0,1,1)` | `checks/run_clear_glass.ps1` |
| 6 | 空间扭曲完全无效果 | `ZenithReborn/SwordSpaceDistortion.cs`（参数入口无调用者，`ZRSwordSpaceWarp.fxh` 未被 include） | — |

### 现象：**特效开关按了没反应**（F9 切了，画面零变化）
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | `Parts` 26 项是**空开关** | `ZenithReborn/VfxIsolation.cs:107-134`（P01–P26，`uPartShape`…`uPartRidges`）。`ZRSwordFlow.fx` 全文 `uPart` 出现 **0 次**，`.fxc` 中 `grep uPart` 计数也为 0 | `checks/run_vfx_stripe_ablation.ps1` |
| 2 | `GlassParts` 7 项是**空开关** | `ZenithReborn/VfxIsolation.cs:226-235`（B7-1…B7-7，`uGlassPart*`）。`ZRGlassScreen.fx` 中 `uGlassPart` 出现 0 次 | `checks/run_liquid_glass.ps1` |
| 3 | 取值函数**全库零调用** | `ZenithReborn/VfxIsolation.cs:141`（`PartValue(string)`）。唯一的读写方是调试面板 `ZenithReborn/VfxPartDebug.cs:80/85/106/111` → 按下只改面板文字与 `PartValues` 数组，从不进 shader | — |
| 4 | `Branches` 才是真生效的那一类 | `ZenithReborn/VfxIsolation.cs:63-74`（B1–B10）。它们在各绘制入口前以 `if (VfxIsolation.Xxx)` 判断，**按下立刻有画面变化** —— 用它来对照即可确认"面板没坏，是 P/B7 项没接线" | `checks/run_slash_render.ps1` |
| 5 | 个别项不在 61 项清单里 | `ZenithReborn/VfxIsolation.cs:36`（`ViscousSlash` 默认 false，绕过总闸，只能改字段关） | `checks/run_liquid_glass_perf.ps1` |
| 6 | 注释里的顺序约束已失效 | `ZenithReborn/VfxIsolation.cs:101` 声称"顺序必须与 `ZRSwordFlow.fx` 声明顺序一致"，但 shader 已重写（36 参数，无 `uPart*`）→ 调参时下标对不上 | — |

**合计 33 项空开关 = P01–P26（26）+ B7-1…B7-7（7）。** 这不是"开关坏了"，是**从来没接线**：shader 没有对应 uniform，C# 侧也没有把 `PartValues` 传给任何 effect 参数。

### 现象：UI 显示的数据与实际不符
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 详情页走**克隆**评估，不写回真身 | `ZenithReborn/SoulArmoryPreview.cs:8`（`Evaluate` 克隆整件剑魂跑真实 `TrySwapHost`）→ 预览合法但真身未变 | `checks/ZenithPrototypeCheck/Check.cs`（深克隆套件） |
| 2 | 木质面板同屏两套口径 | `ZenithReborn/SoulUIWoodPanel.cs:41`（`hostedMask`，不过滤）vs `:42`（`CombatWood`，过滤） | `checks/run_information_ui_render.ps1` |
| 3 | 羁绊核心与战斗侧不同源 | `ZenithReborn/BondSnapshot.cs:59`（不叠 `TraitEnabled`）→ 与 `ZenithReborn/SoulCurses.cs:86` 可能差 1~7 把 | — |
| 4 | 图鉴 `Active` 是**手写复算**，与战斗侧两份代码 | `ZenithReborn/SoulTraits.cs:16`（`All`）；例：`ore-tier-2` 用 `oreCount`，战斗侧用 `OreFamily.MainDamageMultiplier`（`ZenithReborn/OreFamily.cs:83`） | `checks/ZenithPrototypeCheck/TraitsCheck.cs` |
| 5 | 成套浮窗路径是休眠代码 | `ZenithReborn/SoulDomainUI.cs:13`（`DomainMode` **默认 true，游戏内无写入点**，只有 checks 会置 false）→ `DrawChrome`/`DrawSoulRing`/`HandleWindowDrag` 约 300 行只在夹具里跑到 | `checks/ZenithPrototypeCheck/Check.cs:77`（`SoulUI.DomainMode=false`） |
| 6 | 点击命中框与绘制不同源 | `ZenithReborn/SoulUI.cs:211`（`SlotBounds`）vs `ZenithReborn/SoulArmoryLayout.cs:31`（`Slot`）vs `ZenithReborn/SoulItem.cs:28`（`SupportOrdinal`）三处必须同口径 | `checks/run_soul_input.ps1` |

### 现象：改完代码跑回归失败 / 回归假绿
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | **`CurseFixture.NeutralDefaults` 会改变新剑魂的诅咒基线** | `checks/ZenithPrototypeCheck/CurseCheck.cs:15-19`：true 为新剑魂设置 `LegacyReleased=63`，至少按已解除处理，不自动授予祝福；false 使用真实默认诅咒。全量套件前段设 true，进入诅咒专项前设回 false；检查受独行、薄刃等影响的断言时先核对开关 | `checks/ZenithPrototypeCheck/Check.cs` |
| 2 | **HintPath 引 Release DLL** | `checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj:4`（`../../ZenithReborn/bin/Release/net8.0/ZenithReborn.dll`）→ 改主模组**不重新 `-build`** 就静默测旧逻辑 | `checks/run_wood_checks.ps1`（脚本纪律：先重建） |
| 3 | **`-server` 下 26 个渲染夹具直接 return** | 所有 `*RenderCheck` 的 `Enabled` 都带 `!Main.dedServ`；`checks/ZenithPrototypeCheck/Check.cs:26` 的 `PostAddRecipes` 是唯一自动化入口。`-server` 的 `PASS` **完全不能证明画面正确** | `checks/run_*.ps1`（图形夹具 27 个） |
| 4 | `Main.UIScale` 跨断言污染 | 循环中途抛异常会停在 2f，后续 UI 断言坐标全错，且报错指向别处 | `checks/ZenithPrototypeCheck/Check.cs`（UI 分支） |
| 5 | 忘了 `RefreshStats()` / 用 `Prefix(-2)` 当"清除" | `ZenithReborn/SoulItem.cs:387`；`Prefix(-2)` 是**随机重铸**不是清除 | 夹具模板固定三步 |
| 6 | 重建型脚本污染构建目录 | 7 个脚本仍写 `build-profile`：`checks/run_flying_blade_survey.ps1`、`checks/run_echo_phase_probe.ps1`、`checks/run_airflow_export.ps1`、`checks/run_mask_preview.ps1`、`checks/run_swordbody_preview.ps1`、`checks/run_surge_preview.ps1`、`checks/build_material_lab.ps1` → 刷新 `.tmod` mtime，让下一次安装的时序断言炸 | `checks/package_install.py:25` |

### 现象：打包 / 安装失败
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | **（历史）跑的是小套件，报告却照样放行** | 旧行为：`checks/ZenithPrototypeCheck/Check.cs` 分支顺序 `GLASS:69 → INFORMATION_UI:75 → ECHO_ORE:83 → WOOD:125`（各自 `Environment.Exit(0)`）；设了 `ZENITH_GLASS` 就只跑玻璃套件，而 `checks/package_install.py:19` 当时只校验 `startswith('PASS: ')` → 静默收窄，曾是最危险的假绿。**该风险已由 `checks/package_install.py` 新增的 `FULL_SUITE_MARKER`（`SUITE=ZENITH_WOOD`）与 `MIN_FULL_SUITE_CHECKS` 防假绿标记修复**：缺全量套件标记或项数不足即拒绝安装；当前 `artifacts/wood-check.txt` 含 `SUITE=ZENITH_WOOD` 且为 8509 项。此行保留为历史记录 | `checks/run_wood_checks.ps1 -EchoOre` |
| 2 | 时序断言：报告必须比包新 | `checks/package_install.py:25`（`report.stat().st_mtime > built.stat().st_mtime`）；包被任何脚本重建都会失败 —— 见上条现象第 6 行 | `checks/package_install.py` |
| 3 | 哈希退化路径 | `checks/package_install.py:31-36`：哈希不匹配时**不直接失败**，退化成"包内 DLL == `bin/Release/net8.0/ZenithReborn.dll"` 弱断言 | `artifacts/native-build-check.json` |
| 4 | 着色器编译失败即中断 | `checks/run_wood_checks.ps1:17` 调 `checks/compile_effects.py`；`.fx` 改了没同步 `.fxc` 会在这里炸 | `checks/ZenithReborn/Assets/Effects/build-manifest.json` |
| 5 | hjson 语法错误 / C# 编译失败 | 构建闸门在 `-server -build` 阶段；`checks/ZenithPrototypeCheck` 的 `.csproj` 还硬编码 `E:/SteamLibrary/.../tMLMod.targets` → **换机器直接不可构建** | — |
| 6 | 打包排除清单 | `checks/package_install.py:70`（排除 `docs`/`checks`/`VfxLibrary`/`tools`）；漏了会把 11756 个素材文件打进包 | — |

### 现象：存档损坏 / 多人不同步
| 顺序 | 可能模块 | 优先检查 | 关联检查位置 |
|---|---|---|---|
| 1 | 槽号是存档契约 | `ZenithReborn/SoulItem.cs:26`（`SupportSlotId`）/ `:28`（`SupportOrdinal`）；**17-24 是兼容段不可回收**；`AllSupportSlots` 只 `Take(5)` 是刻意的 | `checks/ZenithPrototypeCheck/Check.cs`（存档往返套件） |
| 2 | 诅咒存档有 schema 分支 | `ZenithReborn/SoulCurses.cs:87`（`SaveCurses` 写 `curseSchema=1`）/ `:88`（`LoadCurses` 无 schema 时走 `FromOldKills` + `LegacyReleased=Attached?63:0`） | `checks/ZenithPrototypeCheck/CurseCheck.cs`（"旧魂可玩"） |
| 3 | 网络只发增量，漏发就永久不同步 | `ZenithReborn/SoulCurses.cs:89`（`SendCurses` 只写 `Seals` + 1 字节 `LegacyReleased`）；`ZenithReborn/Progression.cs:124`（`ModifyHitByProjectile` 客户端发 packet 8） | `checks/ZenithPrototypeCheck/`（"全魂网络包保留诅咒记录"） |
| 4 | UI 同步只对纯客户端生效 | `ZenithReborn/SoulUI.cs:56`（`Sync` 只对 `MultiplayerClient`）；主机端走 `SyncInventory` | — |
| 5 | 一个 Boss 只记一个人 | `ZenithReborn/Progression.cs:136`（`OnKill` 只遍历 `inventory[0..57]` 找同 `SoulId` 的剑魂） | — |
| 6 | 难度开关是**真实改写世界** | `ZenithReborn/SoulDifficulty.cs:25`（`Toggle` 改 `Main.GameMode`/`getGoodWorld` 并写世界存档）；`DomainCheck` 只是单进程语义，不防多人 | `checks/ZenithPrototypeCheck/Check.cs` |

---

## 附：33 项空开关逐项清单（对应现象 11）

判定标准：**shader 里没有这个 uniform，C# 侧也没把值传出去**。改 `PartValues` 只会改调试面板显示。

| 项 | uniform | 面板标签 | 默认 | 谁来读 |
|---|---|---|---|---|
| P01 | `uPartShape` | 主体形状（关掉整条带面消失） | 1f | 无（只有 `VfxPartDebug.cs:79`） |
| P02 | `uPartBend` | 边界正弦弯曲 | 0f | 无 |
| P03 | `uPartThreads` | 尾部拉丝细缕 | 0f | 无 |
| P04 | `uPartDepthGradient` | 横向明暗梯度 | 0f | 无 |
| P05 | `uPartProcField` | 程序色场低频噪声 | 0f | 无 |
| P06 | `uPartDissolve` | 消融与回收前沿 | 0f | 无 |
| P07 | `uPartRim` | 消融前沿发光 | 0f | 无 |
| P08 | `uPartCurvature` | 双轴曲面法线 | 1f | 无 |
| P09 | `uPartBevel` | 刃口倒角 | 1f | 无 |
| P10 | `uPartRazorCore` | 刃核白线 | 0f | 无 |
| P11 | `uPartRazorHalo` | 刃线中层光晕 | 0f | 无 |
| P12 | `uPartRazorMist` | 刃线外层弥散 | 0f | 无 |
| P13 | `uPartInnerRim` | 内缘线 | 0f | 无 |
| P14 | `uPartBodyTint` | 剑身色铺底 | 0f | 无 |
| P15 | `uPartBandReflection` | 局部光面边缘 | 0f | 无 |
| P16 | `uPartGlassBase` | 玻璃基底不透明度 | 1f | 无 |
| P17 | `uPartGgxCore` | GGX 高光核 | 0f | 无 |
| P18 | `uPartSoftLobe` | 宽高光包络 | 0f | 无 |
| P19 | `uPartWhiteShift` | 高光亮色过渡 | 0f | 无 |
| P20 | `uPartHighlightNoise` | 噪声调制高光（需噪声图） | 0f | 无 |
| P21 | `uPartDispersion` | 色散（需背景透射才可见） | 0f | 无 |
| P22 | `uPartSceneTransmission` | 背景透射 | 1f | 无 |
| P23 | `uPartSsr` | 屏幕空间反射 | 1f | 无 |
| P24 | `uPartStripe` | 虚拟光面强度 | 0f | 无 |
| P25 | `uPartEmission` | 虚拟光源亮度 | 0f | 无 |
| P26 | `uPartRidges` | 液态玻璃条带起伏（只驱动折射） | 0f | 无 |
| B7-1 | `uGlassPartCoverage` | 覆盖度（玻璃是否存在） | 1f | 无 |
| B7-2 | `uGlassPartRefraction` | 背景折射偏移 | 1f | 无 |
| B7-3 | `uGlassPartBlur` | 粗糙度模糊 | 1f | 无 |
| B7-4 | `uGlassPartTransmittance` | 透射吸收 | 1f | 无 |
| B7-5 | `uGlassPartCoating` | 高光合成 | 1f | 无 |
| B7-6 | `uGlassPartBloom` | 泛光扩散 | 1f | 无 |
| B7-7 | `uGlassPartSpaceWarp` | 刀光周围空间扭曲 | 1f | 无（`ZRSwordSpaceWarp.fxh` 未被 include） |

对照证据：`ZRSwordFlow.fx` 声明 36 个参数、`uPart` 命中 0；`ZRGlassScreen.fx` 中 `uGlassPart` 命中 0；`ZenithReborn/VfxIsolation.cs:141` 的 `PartValue(string)` 全库零调用。

**要真接线，需要三处同时改**：`.fx` 声明 uniform 并在计算里用上 → C# 侧把 `PartValues[i]` 写进 effect 参数 → 保留 `VfxIsolation.cs:101` 那条"顺序必须一致"的约束（目前该约束已失效）。

## 附：Boss 位映射三套对照（对应现象 7）

| 用途 | 位宽 | 定义位置 | 谁在读 |
|---|---|---|---|
| 诅咒铭刻 | 18 位（`All=(1<<18)-1`） | `ZenithReborn/SoulCurses.cs:14-15`、`BossSeal` 在 `:24` | `SoulCurseDescriptions`、`SoulUICurseUnlocks`、`SoulBossUnlocks` |
| 槽位解锁 | 12 位（`All=4095`） | `ZenithReborn/Progression.cs:11` 附近、`BossBit` 在 `:34` | `SoulProgress.Capacity`（`:24`）、`SlotGates`（`ZenithReborn/SlotProgression.cs:12`） |
| 头像图集 key | 19 张 PNG | `ZenithReborn/SoulBossPortraits.cs` / `SoulBossPortraitCatalog.cs` | `ZenithReborn/Assets/UI/BossPortraits/sources.json` |

`Queen Slime / Empress of Light / Duke Fishron / Deerclops` **只在 18 位表里存在**（无对应槽位门槛）→ 打它们只解诅咒、不给槽位。这是设计，别当 bug 修。

## 附：一眼判断"该信谁"

| 你看到的 | 该去哪里验 |
|---|---|
| 面板数字 vs 实际伤害 | 面板读 `SoulItem.RefreshStats`（`ZenithReborn/SoulItem.cs:387`），实际读 `SoulCombat.BladeDamage`（`ZenithReborn/SoulCombat.cs:9`） |
| 族计数三个值 | `SoulCurses.cs:86`（战斗）/ `SoulSupport.cs:43`（族内）/ `BondSnapshot.cs:59`（UI） |
| 开关有没有接线 | `Branches`（`VfxIsolation.cs:63`）有绘制前 `if`；`Parts`/`GlassParts` 没有 |
| 回归到底跑了多少项 | `artifacts/wood-check.txt` 的 `PASS: <n>`，n 是 `Assert` 调用次数，**没有期望值**；8453/8498/8503/8507/8511 都出现过 |
| 夹具测的是不是新代码 | 看 `ZenithReborn/bin/Release/net8.0/ZenithReborn.dll` 的 mtime 是否晚于最后一次源码改动 |

## 附：完全无自动覆盖（8 项）

真实多人（≥2 客户端）、性能/帧时间（除液态玻璃）、实机画面人工判读、音效、手柄/触屏、本地化完整性、多人权限/RPC 鉴权、跨平台。
这 8 类现象无论本文怎么索引，最终都得人工复现。
