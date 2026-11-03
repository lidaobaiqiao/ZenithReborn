# C · UI 层建图（ZenithReborn 剑魂界面）

覆盖范围：`ZenithReborn/` 根目录下 22 个 UI 相关文件，**22 / 22 已通读**（只读，未修改任何源码）。
工作区状态：大量未提交改动（木质族 V2、`SoulUIWoodPanel` 新面板、`BondSnapshot` 阈值改 1/2/4/6/8），以下按**当前工作区**为准。

---

## 0. 总览：`SoulUI` 分部拆分结构

`SoulUI` 是一个 `ModSystem`（`SoulUI.cs:14`），被拆成 **18 个 partial 文件**（含主文件）。拆分逻辑是"按界面区域 + 按职责层"两层切割，不是按代码长度切：

| 层 | 文件 | 职责 |
|---|---|---|
| 主控 | `SoulUI.cs` | 状态字段、开关/同步、输入钩子、界面层注册、**唯一的总绘制入口 `Draw()`**、页面分发、公共小件（`Box/Text/Border/Button/Paragraph/Section`）、剑位命中框 `SlotBounds`、旧扩展页 |
| 页面身份 | `SoulUIPages.cs` | 页面常量（0/1/2/4/5）、导航集合、`Resolve`、名字与配色。**不是 partial 类**，是独立 `static class` |
| 基础绘制 | `SoulUIStyle.cs` | 圆角框、浮窗骨架、页面底板 |
| 特效/几何 | `SoulUIEffects.cs` | 时间基准、剑环半径/圆心/极坐标、发光/线/弧、旧剑环整体绘制、传奇剑位 |
| 图标 | `SoulUIGlyphs.cs` | 16 种矢量徽记 + 三张映射表（页面/特性分类/诅咒） |
| 信息页组件 | `SoulUIInformation.cs` | `Info*` 组件族（裁剪、滚动、块、按钮、标题）、信息页导航 |
| 排版 | `SoulInfoLayout.cs` | 列数、宫格、主从分栏（独立 `static class`） |
| 域界面 | `SoulDomainUI.cs` | `DomainMode`、工作区、背景、页头、难度按钮、背包取放、归还 |
| 配阵 | `SoulUIArmory.cs` / `SoulUIArmoryScene.cs` / `SoulUIArmoryInteraction.cs` / `SoulUIArmoryLayout.cs` | 配阵协调 / 剑阵场景与节点 / 候选与确认状态机 / 纯几何 record |
| 剑位详情 | `SoulUIArmoryDetails.cs` / `SoulArmoryPreview.cs` | 详情面板 / 只读换入评估 record |
| 羁绊核心 | `SoulUIArmoryBondCore.cs` | 中央核心 + 流入流光 |
| 剑库 | `SoulUILibrary.cs` | 背包只读索引、筛选、列表 |
| 特性页 | `SoulUIArchive.cs`（**文件名与内容不符**：实际是"剑魂特性"工作区） | `DrawTraits` |
| 成长页 | `SoulUIMemories.cs`（实际是"成长记忆"`DrawProgress`）/ `SoulUICurseUnlocks.cs`（六封印卡）/ `SoulUIBossUnlocks.cs`（Boss 铭刻两列） | 成长三分组 + 诅咒解锁复用块 |
| 环境页 | `EnvironmentUI.cs` | 环境图鉴 + 领域/常驻操作 |
| 木质族面板 | `SoulUIWoodPanel.cs` | 配阵左缘的木质族 V2 面板 |

**重要结构性事实**：`SoulUI.DomainMode` 默认为 `true`（`SoulDomainUI.cs:13`），**游戏内从未被设为 false**；只有 `checks/` 下的 UI 自检会写 false（`checks/ZenithPrototypeCheck/Check.cs:77`、`Check.cs:128`、`DomainCheck.cs:85`、`FollowupCheck.cs:47`）。因此：

- 旧"浮窗 + 剑环"整套代码是**仅自检可达的休眠路径**：`DrawChrome`（`SoulUIStyle.cs:39`）、`DrawSoulRing`（`SoulUIEffects.cs:125`）、`DrawLegendarySlot` 的浮窗调用、`HandleWindowDrag`（`SoulUI.cs:156`，第一行 `if(editing==null || DomainMode) return false` → 恒 false）、`PanelBounds` 非域分支、`TabBounds` 非域分支、`DrawHosts` 的 `SideInspector` 分支。
- 读 `SoulUI.cs` 时看到"拖动标题栏/浮窗"字样不要当成线上行为（`SoulUI.cs:357` 的"拖动标题栏"文字在域模式下不绘制）。

---

## 1. 模块级说明

### 1.1 入口与生命周期（玩家怎么进来）

- **按键 P**：`ZenithReborn.cs:14` 注册 `OpenSoul`（默认 `"P"`）→ `SoulUI.PostUpdateInput`（`SoulUI.cs:79`）→ 取手持 `SoulItem`，否则取背包第一把 → `Toggle`（`SoulUI.cs:46`）。
- **手持剑魂右键**：`SoulItem.CanUseItem`（`SoulItem.cs:371-377`，`AltFunctionUse` 恒 true）→ `SoulUI.Toggle`。
- **剧情回调**：`AttachmentStory.cs:147` 调 `SoulUI.Open(soul, true)` → 打开并直接进成长页（`Open` → `SelectPage(2)`，`SoulUI.cs:33`）。
- **强制打开背包**：`Toggle` 里 `Main.playerInventory = true`（`SoulUI.cs:53`）。
- **自动关闭**：`PostUpdateInput`（`SoulUI.cs:89-91`）——`!Main.playerInventory` / 玩家死亡 / 背包中不再有这把剑，三者任一成立即 `Sync` + `editing=null`。**Esc 关闭是通过"关背包"间接实现的**（此点为代码推断，未实测）。
- **关闭按钮**：`CloseBounds`（`SoulUI.cs:204`）命中于 `HandleNavigation`（`SoulUI.cs:293`）。
- **绘制注册**：`ModifyInterfaceLayers`（`SoulUI.cs:102`）插入两层——`"ZenithReborn: Input Shield"`（层 0，屏蔽原版输入）与 `"ZenithReborn: Soul"`（插在 `Vanilla: Mouse Text` 前）。域模式下还会删掉除光标/鼠标文本外的 `Vanilla:*` 层（`SoulUI.cs:105-107`）。
- **输入隔离**：`SoulInput`（`SoulInput.cs`）在 `PostUpdateInput` 里 `Capture`，绘制层前后 `BeginSoulLayer/EndSoulLayer`，`PostDrawInterface` 里 `RestoreAfterInterface`。这解释了"为什么普通写法拿不到鼠标点击"——点击被截走后又按前景层重放。

### 1.2 骨架与总分发（`SoulUI.cs:341` `Draw()`）

固定顺序：`PanelBounds` → 背景（`DrawDomainBackdrop`）/浮窗骨架 → 关闭按钮 → `HandleNavigation` → 滚轮（非 0/1/2/5 页，`SoulUI.cs:367`）→ **Scissor 裁剪主体**（`SoulUI.cs:370-377`，结束恢复 `RasterizerState`）→ 页面分发 → 滚动条 → 页脚提示。

页面分发（`SoulUI.cs:382`）：

| page | 绘制函数 | 所在文件 |
|---|---|---|
| 0 武器寄宿 | `DrawHosts` → 域模式下 `DrawArmory` | `SoulUI.cs:404` / `SoulUIArmory.cs:9` |
| 1 剑魂特性 | `DrawTraits` | `SoulUIArchive.cs:39` |
| 2 成长记忆 | `DrawProgress` | `SoulUIMemories.cs:10` |
| 5 环境之力 | `DrawEnvironments` | `EnvironmentUI.cs:17` |
| 4 旧物取回（不在导航） | `DrawExtended` | `SoulUI.cs:438` |

页号 3 空缺。导航只含 `{0,1,2,5}`（`SoulUIPages.cs:9`），`Recovery(4)` 只能从"旧物取回"按钮进（`SoulUI.cs:432`、`SoulUIArmoryDetails.cs:80`），`Resolve` 会在无 legacy 物品时回落到 0（`SoulUIPages.cs:10`）。
**注意**：`Draw()` 里成长页写的是字面量 `page==2`（`SoulUI.cs:382`）而非 `SoulUIPages.Memories`，与 `TraitsPage/RecoveryPage/EnvironmentPage` 三个常量风格不一致——改页号时这是唯一漏点。

### 1.3 御剑配阵（Armory，page 0 的域模式主体）

- **绘制入口**：`DrawArmory`（`SoulUIArmory.cs:9`）→ 三块：剑库 `DrawArmoryLibrary`（`SoulUILibrary.cs:27`）、剑阵 `DrawArmoryScene`（`SoulUIArmoryScene.cs:24`）、详情 `DrawArmoryDetails`（`SoulUIArmoryDetails.cs:15`）；附加 `DrawArmoryAttunement`（归位飞行动画）与 `DrawCoreFlow`（灌入核心的流光）。
- **布局全部来自纯几何 record**：`SoulArmoryLayout.Create(width,height)`（`SoulArmoryLayout.cs:9`）。`Compact = width<1180 || height<620` 时三块共用同一矩形并切成上图/列表/详情三个 tab（`Mode(i)`，`SoulArmoryLayout.cs:24`；tab 状态 `armoryPane` 在 `SoulUIArmoryInteraction.cs:9`）。
- **交互链路（真实落地）**：
  1. 鼠标在某剑位节点 → `DrawArmoryNode` 的 hover 分支（`SoulUIArmoryScene.cs:91-101`）→ `SelectSlot(id)`（`SoulDomainUI.cs:32`，会 `ClearArmoryCandidate`）。
  2. 鼠标按下且释放 → `soul.TrySwapHost(id, ref Main.mouseItem, out error)`（`SoulItem.cs:95`）→ 成功后 `Sync(soul)`（多人同步，`SoulUI.cs:56`）+ `TriggerCoreFlow`（`SoulUIArmoryInteraction.cs:19`）→ `DomainNote` 提示（`SoulDomainUI.cs:69`）。
  3. 从剑库选的路径不同：点列表行 → `SelectArmoryCandidate(invSlot)`（`SoulUIArmoryInteraction.cs:34`）只登记 `candidateIndex + candidateReference`（**不动物品**）→ 详情页 `CurrentArmoryPreview`（:42）算只读对比 → 点"寄宿到剑位" → `ConfirmArmoryCandidate`（:49）→ `EquipFromInventory`（`SoulDomainUI.cs:44`）→ `TrySwapHost` + `SyncInventory` + `Sync`。
  4. 取出：`ReturnSelected`（`SoulDomainUI.cs:55`）找第一个空格，`TrySwapHost` 到该格。
- **数据来源**：`soul.ItemAt(id)`（`SoulItem.cs:67`）、`soul.VisibleSlotIds`（`SoulItem.cs:31`）、`Available()`（`SoulUI.cs:333`，容量规则聚合点）、`SoulSupport.FamilyItems`（`SoulSupport.cs:43`）、`BondSnapshot`（羁绊核心与木质面板）、`SoulArmoryPreview.Evaluate`（`SoulArmoryPreview.cs:8`）。
- **"寄宿"的三重校验都在数据层**：`Available` 只决定 UI 是否画成"未解锁"，真正拦截在 `TrySwapHost`（含容量、封印、是否 Melee 等）。UI 侧永远先尝试再显示 `error`。
- **常见问题定位**：
  - 剑位画歪/重叠 → `SoulArmoryLayout.Slot`（`SoulArmoryLayout.cs:31`）+ `SlotBounds`（`SoulUI.cs:211`）。注意 `SlotBounds` 域模式 page0 直接委托 `ArmoryLayout.Slot`，两处口径必须一致。
  - 点了没反应 → 先看 `InfoClip` 的 `infoClipBounds`（`SoulUIInformation.cs:44`）——命中判定会被裁剪区二次过滤，画出来但点在裁剪区外就无效。
  - 详情页数字与背包不一致 → `SoulArmoryPreview` 是对 `Item.Clone()` 后的 `SoulItem` 跑 `TrySwapHost`，不写回真身（`SoulArmoryPreview.cs:10`）。
- **修改影响提醒**：
  - 改 `SoulArmoryLayout` 的 margin/left/right 会同时挤压三块；`Compact` 阈值 1180/620 是"三栏 vs 单栏"的唯一开关。
  - 改剑位尺寸会连带影响 `DrawWoodPanel` 的可画性：`WoodPanel` 宽度 = 圆环左缘 − 舞台左缘 − 16，`<96` 或高度 `<150` 直接返回 `Empty` **不画**（`SoulArmoryLayout.cs:55-63`）。窄屏看不到木质面板是设计行为，不是 bug。
  - 左侧剑库宽度 `Math.Clamp(width/5,244,292)`，右侧详情 `Math.Clamp(width/4,292,340)`；把任一侧调大会先吃掉中间舞台。

### 1.4 剑位详情

- 入口 `DrawArmoryDetails`（`SoulUIArmoryDetails.cs:15`），分顶部滚动态（`DetailBody`）与底部固定操作区（`Confirm` / `Return`，`SoulArmoryLayout.cs:67-70`）。
- 内容顺序即信息优先级：槽位码 → 武器名 → 角色（支援/传奇/圣林/矿石奖励/普通）→ 伤害暴击两联卡 → 换入 delta → 族名（`WeaponFamilyTags.Line`）→ **木质族专属行**（`SoulUIArmoryDetails.cs:47-52`，读 `BondSnapshot.Wood`）→ 环境标签 → 可归位判定 → 条件 `SoulProgress.SlotHint` → 下一步 `SoulProgress.Next`。
- 底部两个按钮语义：无候选时是"从剑库选择武器"（Compact 下切 tab），有候选时变成"寄宿到剑位"；右按钮"取回背包"。
- **常见问题**："详情页看不到某条信息" → 大概率是 `armoryDetailHeight` 只在 `InfoClip` 内累加（:81），若新增内容写在 `end=y+armoryDetailScroll` 之后则滚动条长度不对，内容被裁在视口外。
- **修改影响**：底部操作区高度由 `SoulArmoryLayout.Confirm/Return` 的 `Detail.Height<260` 分支决定，详情内容变长只会滚动，不会顶掉按钮。

### 1.5 羁绊核心

- `DrawBondCore`（`SoulUIArmoryBondCore.cs:9`）：`BondSnapshot.Evaluate` → `Leading` 取主导族 → 段环（木质 5 段 / 矿石 6 段，:44）→ 族徽（Leaf / Ingot / Core）→ 满羁绊棱彩光点 → 族名/阶段/数量文字。
- 颜色规则集中在 `BondColors`（`BondSnapshot.cs:112`）：`Tier()` 取阶段色，`Prismatic(time,offset)` 取满羁绊。
- `orbitRadius<145` 时进入 `compactCore`（:16），文字压成单行 `阶段 · n/max`；这是小屏不溢出的关键分支。
- **常见问题**："核心显示的不是我想要的族" → 主导规则在 `BondSnapshot.Leading`（`BondSnapshot.cs:96`）：先比 Tier，再比 Count，再比 Progress。同 Tier 同 Count 时矿石/木质谁先取决于 `Evaluate` 的返回顺序（木质在前，`BondSnapshot.cs:93`）。
- **修改影响**：`DrawCoreFlow` 的起点用 `layout.Slot(ev.Slot,...)`，若改了槽位几何而没改流光，飞线会指向空处。

### 1.6 军械库列表（随身剑库）

- `DrawArmoryLibrary`（`SoulUILibrary.cs:27`），数据来自 `ArmoryLibraryItems`（:13）→ `LibraryItems(player)`（`SoulDomainUI.cs:41`）= 背包 0..49 中 `SoulItem.CanHost` 的格子。
- 两档筛选（全部 / 未寄宿，`libraryFilter`）+ 一个排序开关（背包序 / 伤害↓，`armorySortDamage`）。
- 行副标题：族名 + 环境（`WeaponFamilyTags.Of` + `EnvironmentSwordTooltips.Label`），已寄宿同种显示"已有同种寄宿"。
- **点击只做候选登记**，不移动物品（:67-68）——这是"预览-确认"两段式的第一步。
- **注意**：`LibraryBounds()` 有 `DomainMode&&page==0 ? ArmoryLayout.Library : new(20,118,...)` 两个分支；旧物取回页（page 4）的"旧物归还"板（`DrawLibrary`，:20）走的是后者。两个函数名相近（`DrawLibrary` vs `DrawArmoryLibrary`），极易看错。
- **常见问题**："库里没有我的剑" → `SoulItem.CanHost`（`SoulItem.cs:92`）要求：有伤害、Melee 伤害类型、镐/斧/锤为 0、非剑魂、非天顶剑、非消耗品、最大堆叠 1。任何一条不满足就不会出现。

### 1.7 剑魂特性（`SoulUIArchive.cs`）

- `DrawTraits`（`SoulUIArchive.cs:39`）。三段结构：范围开关（当前生效 / 全部）→ 8 个分类筛选（`catalogFilter`，0=全部，1..7 对应 `TraitCategory`）→ `SoulInfoLayout.MasterDetail` 左右分栏（列表滚 + 详情滚）。
- 列表项状态文案（:85-87）是"Boss 诅咒特性"和"普通特性"的分叉点：`SoulBossUnlocks.TryCurse(key)` 命中 → 显示 `SoulCurses.StageName(soul.Curse(curse))`；否则显示 生效/待激活。
- 详情右栏尾部（:109）如果命中诅咒，改画 `DrawBossUnlocks`（整块 Boss 铭刻两列），否则画普通"激活条件"。
- **常见问题**："特性页少了条目" → `TraitEntries`（:20）同时被 `activeTraitsOnly` 与 `catalogFilter` 过滤；`traitSelection` 失效时会回落到第一条（:69）。
- **修改影响**：列表行高写死 66（:70、:76），滚动高度 `entries.Length*66`；改条目排版要同步这两个数。

### 1.8 成长记忆（`SoulUIMemories.cs` + `SoulUICurseUnlocks.cs`）

- `DrawProgress`（`SoulUIMemories.cs:10`）：顶部标题（`普通剑位 unlocked/10 · 魂芯 popcount(Cores&63)/6`）+ "下一步" + 三个 tab（剑位路线 / 魂芯收藏 / 封印铭刻，`memoryTab`）+ 单视口滚动。
- tab0 `DrawSwordMemories`（:31）：史莱姆王剧情块（`AttachmentStory`）→ 十剑宫格（`SoulProgress.ZenithWeapons`）→ 选中节点条件块 → 凝胶信物提示。`growthFocus` 默认跟随 `unlocked`（:54）。
- tab1 `DrawCoreMemories`（:75）：6 魂芯宫格 + 可补办入口 `SoulProgress.RecoverableCore/RequestCoreRecovery`。
- tab2 `DrawSealUnlockList`（`SoulUICurseUnlocks.cs:9`）：6 张封印卡，**按行展开**——点开的卡在本行下方插入完整 `DrawBossUnlocks` 区块（:40），所以这一页高度是动态的（`sealFocus`）。
- **常见问题**："成长页滚动到底还有内容看不到" → `growthContentHeight` 由 `InfoClip` 内返回值决定（`SoulUIMemories.cs:28`），若新增块写在 `bottom` 之外就会少算。
- **修改影响**：`DrawSealUnlockList` 复用了特性页的 `DrawBossUnlocks`，**改 Boss 铭刻排版会同时影响特性页与成长页**。

### 1.9 史料：Boss 铭刻 / 诅咒进度（`SoulUIBossUnlocks.cs`）

- `DrawBossUnlocks`（:11）两列：左"挑战目标"（`SoulBossTarget`），右"累计铭刻 → 解锁效果"（`SoulBossReward`）。宽 `<540` 时改为先全列目标再全列奖励（:41-53）。
- 数据来自 `SoulBossUnlocks.Read(soul, curse)`（`SoulBossUnlocks.cs:24`），本身是纯 view record，不含存档字段。
- 诅咒相关文案：`SoulCurses.Names`（`SoulCurses.cs:17`）、`SoulCurses.StageName`（:43）、`SoulCurses.Count`（:31，按 `Seals` 位与）。
- **常见问题**："诅咒进度显示 n/2" → 分母是 `Groups[i].Length`（`SoulCurses.cs:19`），不是固定 2；`Duke/Deer` 不在 Groups 里，只在成长页底部"外域留痕"出现（`SoulUICurseUnlocks.cs:42`）。
- **修改影响**：`BossRewardHeight`（:56）用 `InfoLines(draw:false)` 预量高度，改字号会改变行高→两列可能不同步（左列取 `Math.Max(132, ...)` 右侧高度）。

### 1.10 环境之力（`EnvironmentUI.cs`）

- `DrawEnvironments`（:17）。顶部聚合："已收集 n/21 · 共同强度 x倍 · 常驻 m/slots"；左图鉴宫格（21 格，`EnvironmentAttributes.All`），右详情 + 4 个动作按钮（展开领域 / 常驻机制 / 恢复自然 / 规则说明）。
- 状态优先级（:50-51）：未收集 > 主动领域 > 常驻祝福 > 自然机制运行中 > 属性生效但机制休眠。
- 强度公式（:48-49）：`Shared(collection) × Forest(collection) × (主动?1.5:1) × Adaptation(...)`。前两项在 `EnvironmentRules.cs:16-17`。
- 操作落地：`soul.SetEnvironmentDomain(id)` / `soul.ToggleEnvironmentBlessing(id)` + `Sync(soul)`（:78-86）。失败（常驻名额不足）走 `feedback` 而不是弹窗（:83）。
- **常见问题**："点了没变成主动领域" → `InfoAction` 的 `enabled` 传的是 `collected`（:78），未收集环境按钮是禁用的，但**仍会画出来**（只是灰），容易被误判为"点击无效"。
- **修改影响**：`EnvironmentIcons` 是按下标硬编码的 21 个原版物品（:13-16），与 `SwordEnvironment` 枚举顺序强绑定；插值枚举会整体错位。

### 1.11 木质族面板（`SoulUIWoodPanel.cs`，V2 新面板）

- `DrawWoodPanel`（:33）：锚点 `layout.WoodPanel(...)`，五段固定高度布局（标题20 + 阶段轨24 + 网格53 + 数值40 + 羁绊灯16 = 153，剩余高度按 4 个间隔均分，最小 gap=2，:58-61）。
- **两套木剑口径必须区分**（:39-42）：
  - `hostedMask = WoodTypes.Mask(SoulSupport.FamilyItems(soul))` —— 全部已寄宿木剑（含森林/圣林/传奇位，未过滤诅咒），用于阶段与 8 宫格"已寄宿"。
  - `soul.CombatWood.Mask`（`SoulCurses.cs:87`）—— 特性实际生效的木剑（切换诅咒会压制），用于"特性生效"、倍率、生命、支援数。
  - 二者差集 = "已寄宿但被诅咒压制"，网格右上加小锁（:174-175）。
- 数据：`BondSnapshot.Wood`（阶段/阈值/点亮）、`WoodBondCore.Active{Forge,Rot,Ember,Crown}`（4 复合羁绊灯，:22-30）、`ForestPower.Layers/BaseCap/CrownCap`（森林层数，:91-92）。
- 悬停提示**在裁剪区外**画（:108 `DrawWoodTooltip`），所以 `hoverTip` 先在裁剪内被记录成字符串。这是"面板内加 tooltip 的标准做法"。
- **常见问题**：
  - 面板消失 → `layout.WoodPanel` 返回 `Empty`（窄舞台/高度不足），见 1.3 的说明。
  - 8 宫格第 i 格恒暗 → `WoodTypes.Types[i]` 与 `Effects[i]` 下标必须与 `WoodKind` 枚举对齐（`WoodTypes.cs:29/34/36`）。
  - 倍率显示 ×0 → `CombatWood` 在未附着/未寄宿时为 0 强度；`Strength` 表在 `WoodTypes.cs:20`（Crown 覆盖 3.0）。
- **修改影响**：`contentHeight` 常量（:60）= 153，新增一行必须先改这个数，否则 gap 变负（被 `Math.Max(2, ...)` 兜住，但内容会溢出面板底）。面板宽度上限 244，超过会被 `Math.Min` 截断。

### 1.12 域界面（`SoulDomainUI.cs`）

- `DomainMode=true`（:13）是全局前提。`DomainWorkBounds`（:15）按 page 返回三种工作区：page0 → `ArmoryLayout.Workspace`；信息页 → 全屏留边；page4 → 剑库右侧。
- `SideInspector`（:25）= 宽 ≥790 时在剑阵右侧切出 280px 检查器；`RingViewport`（:26）相应缩窄。
- 背景：`DrawDomainBackdrop`（:71）三速漂移星点 + 地平线网格 + 分隔线。**纯装饰，不参与命中**。
- 页头：`DrawDomainHeader`（:99）——page0 交给 `DrawArmoryHeader`（`SoulUIArmory.cs:30`，含"定位空剑位"与难度按钮），其余页只画标题 + 导航。
- 难度按钮：`DrawDomainDifficulty`（:112）→ `SoulDifficulty.Toggle`（`SoulDifficulty.cs:25`）/ `BlockReason`（:16）。`Legendary = GameMode==2 && getGoodWorld`（:14），**开关是改世界难度，不是改本模组状态**。
- 取放物品的两个唯一出口：`EquipFromInventory`（:44）、`ReturnSelected`（:55）。两者都在动手前后 `SyncInventory`（:67）+ `Sync(editing)`。
- **常见问题**："界面打开后原版按钮全没了" → `ModifyInterfaceLayers` 删 `Vanilla:*`（`SoulUI.cs:105-107`），只保留 Mouse Text / Cursor / Mouse Item。
- **修改影响**：`DifficultyBounds` 的坐标是 `Main.screenWidth` 直接算的（:30），不走面板；改全屏布局时要单独照顾它。

### 1.13 公共样式与布局（三个"被所有人依赖"的文件）

- `SoulUIStyle.cs`：`RoundedBox`（:13，手工画圆角，无贴图）、`DrawChrome`（:39，浮窗，休眠）、`DrawPageSurface`（:63，域模式 page≠0 时给 body 铺暗底 `RoundedBox(body, new Color(11,17,26)*.93f, 14)`）。**若某个信息页背景突然透明，先看这里**。
- `SoulUIEffects.cs`：`UiTime`（:16，Stopwatch 秒）、`BaseRingCanvasHeight`（:20，402..498/580）、`RingRadius`（:24）、`RingCenter`（:25，含 `-scroll`，所以滚动时环心跟着动）、`OrbitPoint`（:26）。**所有环形几何都从这里派生**。
- `SoulUIInformation.cs`：`Info*` 家族。三个高频误用点：
  1. `InfoClip`（:44）内部会 `End/Begin` spriteBatch 并改 `infoClipBounds`，**不要在 `InfoClip` 里再嵌套未经裁剪的 `PanelBounds` 命中判断**。
  2. `InfoHover(key,target)`（:19）按 key 缓动，key 必须唯一稳定（现在用 `"armory/"+id`、`"env/"+id`、`"action/"+label` 等）。
  3. `InfoScroll`（:63）只在 `InfoHovered(bounds)` 时吃滚轮，外层 `Draw()` 的滚轮（`SoulUI.cs:367`）会把这些页面排除，两套滚动不会打架。
- `SoulInfoLayout.cs`：`Columns`（:9）、`Tile`（:10）、`MasterDetail`（:19，宽 ≥850 左右分栏，否则上下分栏）。**信息页的"列表+详情"全走它**。

---

## 2. 文件索引表

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `ZenithReborn/SoulUI.cs` | partial 主文件 (`ModSystem`) | 主控/骨架 | 状态字段、`Open/Toggle/Sync/SelectPage`、输入与界面层注册、`Draw()` 总入口与页面分发、公共小件、`SlotBounds`/`TryClickHost`、旧扩展页 | `SoulUIPages`、`SoulInput`、所有 `Draw*` 分部 | 已验证 |
| `ZenithReborn/SoulUIPages.cs` | 静态类 | 页面身份 | 页号常量 0/1/2/4/5、导航集合、`Resolve/Index/Name/Color` | `SoulUI.SelectPage`、`TabBounds`、`DrawNavigation` | 已验证 |
| `ZenithReborn/SoulUIStyle.cs` | partial 分部 | 公共样式 | `RoundedBox`、`DrawChrome`(休眠)、`DrawPageSurface`、`GroupCaption`、`Champagne` 色 | `SoulUI.Box/Line` | 已验证 |
| `ZenithReborn/SoulUIEffects.cs` | partial 分部 | 公共特效/几何 | `UiTime`、剑环半径/圆心/极坐标、`Glow/Line/Arc/CenterText/Fit`、`DrawNavigation`、`DrawSoulRing`(休眠)、`DrawLegendarySlot` | `ContentBounds`、`SoulVisuals.Tint`、`SoulProgress.SlotHint` | 已验证 |
| `ZenithReborn/SoulUIGlyphs.cs` | partial 分部 + enum | 公共图标 | 16 种矢量徽记 `Glyph`，以及页面/特性分类/诅咒三张映射 | `SoulUIPages`、`TraitCategory`、`SoulCurse` | 已验证 |
| `ZenithReborn/SoulUIInformation.cs` | partial 分部 | 信息页组件 | `InfoClip/InfoScroll/InfoSurface/InfoLines/InfoAction/InfoBlock/InfoTitle/InfoBadge`、`DrawInformationNavigation`、动效缓存 `infoMotion` | 所有信息页、`ContentBounds`、`clipState` | 已验证 |
| `ZenithReborn/SoulInfoLayout.cs` | 静态类 | 排版 | `Columns`、`Tile` 宫格、`MasterDetail` 主从分栏、`Ease/Approach` | 特性页、环境页、成长页 | 已验证 |
| `ZenithReborn/SoulDomainUI.cs` | partial 分部 | 域界面 | `DomainMode`、`DomainWorkBounds`、`RingViewport`、`SelectSlot`、`LibraryItems`、`EquipFromInventory`、`ReturnSelected`、背景/页头/难度按钮 | `SoulArmoryLayout`、`SoulDifficulty`、`SoulInput` | 已验证 |
| `ZenithReborn/SoulUIArmory.cs` | partial 分部 | 配阵 | `DrawArmory` 三块协调 + Compact tab、`DrawArmoryHeader`（定位空位/难度/导航） | 剑库/剑阵/详情/BondCore/WoodPanel | 已验证 |
| `ZenithReborn/SoulArmoryLayout.cs` | `readonly record struct` | 配阵布局 | 纯几何：工作区/剑库/舞台/详情、Compact 切换、槽位坐标、木质面板锚点、列表行、按钮位 | `SoulUI.SlotBounds`、`SoulUIArmoryScene`、`SoulUIWoodPanel` | 已验证 |
| `ZenithReborn/SoulUIArmoryScene.cs` | partial 分部 | 配阵·剑阵 | 仪盘装饰、`DrawArmoryScene`、`DrawArmoryNode`（悬停+点击换位）、归位飞行动画 | `ArmoryLayout.Slot`、`TrySwapHost`、`TriggerCoreFlow` | 已验证 |
| `ZenithReborn/SoulUIArmoryInteraction.cs` | partial 分部 | 配阵·状态机 | 候选/预览/归位动画/coreFlow 状态、`SelectArmoryCandidate`、`CurrentArmoryPreview`、`ConfirmArmoryCandidate`、定位空位、槽位码与配色 | `SoulArmoryPreview`、`EquipFromInventory`、`SoulCrown` | 已验证 |
| `ZenithReborn/SoulUIArmoryDetails.cs` | partial 分部 | 剑位详情 | 详情滚动态（角色/数值/族名/木质进度/环境/条件/下一步）+ 底部确认与取出按钮 | `SoulArmoryPreview`、`BondSnapshot.Wood`、`SoulProgress` | 已验证 |
| `ZenithReborn/SoulArmoryPreview.cs` | `sealed record` | 剑位详情 | 只读换入评估：克隆剑魂跑 `TrySwapHost`，输出允许/原因/伤害暴击 delta/容量 | `SoulItem.TrySwapHost`、`SoulItem.ItemAt` | 已验证 |
| `ZenithReborn/SoulUIArmoryBondCore.cs` | partial 分部 | 羁绊核心 | `DrawBondCore`（主导羁绊段环/族徽/棱彩）、`DrawCoreFlow`（剑位→核心流光） | `BondSnapshot`、`BondColors`、`ArmoryLayout` | 已验证 |
| `ZenithReborn/SoulUILibrary.cs` | partial 分部 | 军械库列表 | `LibraryBounds`、`ArmoryLibraryItems`（筛选/排序）、`DrawArmoryLibrary`、旧物 `DrawLibrary` | `SoulItem.CanHost`、`SelectArmoryCandidate`、`SoulVisuals.Tint` | 已验证 |
| `ZenithReborn/SoulUIArchive.cs` | partial 分部 | 剑魂特性页 | **文件名为 Archive，实为 `DrawTraits`**：范围/分类筛选 + 列表详情双栏 + Boss 区块挂载 | `SoulTraits.All`、`SoulBossUnlocks`、`SoulCurses` | 已验证 |
| `ZenithReborn/SoulUIMemories.cs` | partial 分部 | 成长记忆页 | `DrawProgress` 三分组、`DrawSwordMemories`（剧情+十剑宫格）、`DrawCoreMemories`（6 魂芯） | `SoulProgress`、`AttachmentStory`、`SoulCore` | 已验证 |
| `ZenithReborn/SoulUICurseUnlocks.cs` | partial 分部 | 成长·封印 | `DrawSealUnlockList` 六封印卡，点开后行内展开 `DrawBossUnlocks` | `SoulCurses.Groups/Count`、`SoulBossUnlocks.Read` | 已验证 |
| `ZenithReborn/SoulUIBossUnlocks.cs` | partial 分部 | Boss 铭刻 | 两列（目标 / 累计奖励）与窄屏改单列；目标卡、奖励卡绘制 | `SoulBossUnlockView`、`SoulBossPortraits.Draw` | 已验证 |
| `ZenithReborn/EnvironmentUI.cs` | partial 分部 | 环境之力页 | 21 格环境图鉴、详情强度文案、4 个动作（领域/常驻/恢复/说明）、规则说明 | `EnvironmentRules`、`EnvironmentAttributes`、`EnvironmentPlayer` | 已验证 |
| `ZenithReborn/SoulUIWoodPanel.cs` | partial 分部 | 木质族面板（V2） | 标题/5 阶段轨/8 剑网格/4 数值/4 复合羁绊灯/自绘 tooltip；两套木剑口径 | `BondSnapshot.Wood`、`WoodBondCore`、`WoodTypes`、`ForestPower`、`WoodCrown` | 已验证 |

> 与代理 A/B 交叉的 4 个文件：`SoulArmoryLayout.cs`、`SoulArmoryPreview.cs`、`SoulInfoLayout.cs`、`EnvironmentUI.cs`。本文件只从**绘制与命中视角**记录；它们被非 UI 代码引用的部分（如 `SoulArmoryLayout` 被 `checks/ArmoryUiCheck` 校验）未展开。

---

## 3. UI 与数据的接口清单（UI 位置 → 数据成员 → 定义）

| # | UI 位置 | 数据入口 | 定义位置 |
|---|---|---|---|
| 1 | 木质面板 8 宫格点亮 / 阶段轨 | `WoodTypes.Mask(SoulSupport.FamilyItems(soul))` | `ZenithReborn/WoodTypes.cs:41` + `ZenithReborn/SoulSupport.cs:43` |
| 2 | 木质面板阶段文字 `Ⅳ 森罗` 与 5 个阈值点 | `BondSnapshot.Wood(soul)` → `Count/Tier/Thresholds/StageLine` | `ZenithReborn/BondSnapshot.cs:59`（阈值表 :38，阶段名 :42） |
| 3 | 木质面板"倍率 / 生命" | `soul.CombatWood` → `WoodLoadout.Strength` / `.Life` | `ZenithReborn/SoulCurses.cs:87` → `ZenithReborn/WoodTypes.cs:20` / `:23` |
| 4 | 木质面板"支援 n/m" | `CombatWood.SupportCount` + `soul.SupportCapacity` | `ZenithReborn/WoodTypes.cs:14` / `ZenithReborn/SoulItem.cs:51` |
| 5 | 木质面板"森林 x/y" | `ForestPower.Layers(Main.LocalPlayer)`、`BaseCap(6)`、`CrownCap(12)` | `ZenithReborn/ForestPower.cs:21` / `:14` / `:15` |
| 6 | 木质面板 4 复合羁绊灯 | `WoodBondCore.ActiveForge/ActiveRot/ActiveEmber/ActiveCrown` | `ZenithReborn/WoodBondCore.cs:29` / `:39` / `:49` / `:61` |
| 7 | 羁绊核心 主导族 + 段数 | `BondSnapshot.Evaluate` → `Leading` | `ZenithReborn/BondSnapshot.cs:93` / `:96` |
| 8 | 矿石族计数（核心 / 旧扩展页"材质 n/14"） | `OreFamily.Count(soul)` | `ZenithReborn/OreFamily.cs:91` |
| 9 | 主界面/配阵页主攻计数 `主攻 n/上限` | `soul.Active.Count()` / `Capacity+BonusCapacity+CrownCapacity+(LegendaryOn?1:0)` | `ZenithReborn/SoulItem.cs:82` / `:48`,`:57`,`:60`,`:18` |
| 10 | 支援计数 `n/SupportCapacity` | `soul.Supports` + `SoulSupport.Capacity` | `ZenithReborn/SoulItem.cs:66` / `:51` → `ZenithReborn/SoulSupport.cs:16` |
| 11 | 剑阵节点数量与编号（`L`/`C1`/`R1`/`S1`/`01`） | `soul.VisibleSlotIds` + `SupportOrdinal` / `IsCrownSlot` / `LegendarySlotId` | `ZenithReborn/SoulItem.cs:31` / `:28` / `:25` / `:17` |
| 12 | 剑位渲染内容 | `soul.ItemAt(id)`（槽号→`Hosts/SupportHosts/ForestHosts/CrownHosts/LegendaryHost/AdditionalSupports`） | `ZenithReborn/SoulItem.cs:67` |
| 13 | "可用/未解锁"灰化判定 | `SoulUI.Available`：`Capacity`/`SupportCapacity`/`BonusCapacity`/`CrownCapacity`/`LegendaryOn` | `ZenithReborn/SoulUI.cs:333` |
| 14 | 剑库行（名称/伤害/暴击/是否已寄宿） | `Main.LocalPlayer.inventory[i]` + `SoulItem.CanHost` + `soul.Stored` | `ZenithReborn/SoulItem.cs:92` / `:61` |
| 15 | 详情页"相对当前剑"delta | `SoulArmoryPreview.Evaluate(...)` → `DamageDelta/CritDelta/MainCount/MainCapacity` | `ZenithReborn/SoulArmoryPreview.cs:8` |
| 16 | 成长页标题 `普通剑位 n/10 · 魂芯 m/6` | `soul.Capacity` + `PopCount(soul.Cores & 63)` | `ZenithReborn/SoulItem.cs:48` / `:80` |
| 17 | 成长页十剑宫格 + 条件文案 | `SoulProgress.ZenithWeapons` / `SlotGates` / `SlotHint` / `Next` | `ZenithReborn/Progression.cs:15` / `SlotProgression.cs:12` / `:59` / `Progression.cs:44` |
| 18 | 成长页魂芯宫格与补办 | `soul.Cores` 位、`SoulProgress.RecoverableCore` | `ZenithReborn/SoulItem.cs:80` / `SlotProgression.cs:38` |
| 19 | 六封印卡进度 `n/total` 与阶段 | `SoulCurses.Count` / `Groups` / `StageName` / `soul.Seals` | `ZenithReborn/SoulCurses.cs:31` / `:19` / `:43` / `:78` |
| 20 | 特性列表"诅咒特性"状态 | `SoulBossUnlocks.TryCurse` + `SoulBossUnlocks.Read` | `ZenithReborn/SoulBossUnlocks.cs:19` / `:24` |
| 21 | 环境页 `已收集 n/21 · 强度 x · 常驻 m/s` | `EnvironmentRules.Count/BlessingSlots/Shared` + `soul.EnvironmentCollection/EnvironmentBlessings` | `ZenithReborn/EnvironmentRules.cs:14` / `:15` / `:16`；`SoulItem.cs` 存档字段 |
| 22 | 环境页单项状态（领域/常驻/已收集） | `soul.DomainEnvironment`、`EnvironmentRules.Has`、`EnvironmentRules.Adaptation` | `ZenithReborn/EnvironmentRules.cs:13` / `:80`；`SoulItem.cs:329` 存档 |
| 23 | 页头难度按钮 | `SoulDifficulty.Legendary` / `Toggle` / `BlockReason` | `ZenithReborn/SoulDifficulty.cs:14` / `:25` / `:16` |
| 24 | 页脚/主界面"效能 x%" | `soul.Potency`（附着 + 觉醒 + 阶段） | `ZenithReborn/SoulItem.cs:89` |
| 25 | 主界面标题"天顶复生 / 天顶残魂" | `soul.Awakened`（`AwakeningMemory || Cores==63 && 全部条件`） | `ZenithReborn/SoulItem.cs:86` |
| 26 | "旧物取回"入口是否出现 | `soul.HasLegacyExpansion` | `ZenithReborn/SoulItem.cs:54` |

---

## 4. 跨模块关系

1. **UI → 数据是单向只读**：所有页面只调用 `soul.*` 的 getter 与 `SlotHint/Next` 文案函数；唯一的写操作集中在三个方法——`TrySwapHost`（`SoulItem.cs:95`）、`SetEnvironmentDomain` / `ToggleEnvironmentBlessing`（环境页 :79/:82）、`SoulDifficulty.Toggle`（世界难度，非剑魂数据）。因此"UI 改坏了存档"几乎不可能，问题多半在 `TrySwapHost`。
2. **UI → 存档 → 多人**：任何改物品的操作都成对 `SyncInventory(slot)` + `Sync(editing)`（SoulDomainUI.cs:53/65、77-86）。`Sync` 只对 `MultiplayerClient` 生效（`SoulUI.cs:58`），失败时 UI 已经改了本地状态——这是"多人下剑位不同步"的第一嫌疑点。
3. **UI → 战斗口径必须一致**：木质面板刻意用两套 Mask（`hostedMask` vs `CombatWood.Mask`，见 1.11），`BondSnapshot.Wood` 用的是**未过滤**的 `FamilyItems`（`BondSnapshot.cs:63`）。即：**羁绊阶段/核心显示不受切换诅咒影响，但倍率与特性受**。改木质规则时这三处（`BondSnapshot.Wood`、`SoulCurses.CombatWood`、`SoulTraits`）必须同改。
4. **UI → 布局 record → 命中**：`SoulArmoryLayout` 是 UI 与"点击判定"的唯一共享几何源（`SoulUI.SlotBounds` 域模式直接委托）。任何布局调整必须同时满足：`Slot` 与 `SlotBounds` 一致、`WoodPanel` 不返回 Empty、`SceneHeight` 覆盖支援行数。
5. **UI → checks 回归**：仓库有一套 UI 自检可直接定位回归：`checks/ZenithPrototypeCheck/ArmoryUiCheck.cs`（布局/命中，含 DomainMode 切换 :14/:24/:72）、`ArmoryUiRenderCheck.cs`、`ArmoryInteractionRenderCheck.cs`、`InformationUiCheck.cs`、`BossUnlockUiCheck.cs`、`WindowDragCheck.cs`（浮窗拖动，休眠路径）、`DomainCheck.cs`、`WoodV2Check.cs`、`UiFixture.cs`。**改 UI 前先看对应 check 的断言**。
6. **UI 文案不进本地化**：界面文字全部硬编码在 `.cs`（如 `SoulUIPages.Name` 的"武器寄宿/剑魂特性/成长记忆/环境之力"）；`Localization/zh-Hans.hjson` 只覆盖 `DisplayName/Description/Tooltip` 与按键名（`OpenSoul.DisplayName: 打开剑魂寄宿界面`，:130）。**改界面文案要改 .cs，改物品名/提示才改 hjson。**

---

## 5. 未确认 / 待核实职责

1. **Esc 关闭**：代码里只有 `!Main.playerInventory` 触发的自动关闭（`SoulUI.cs:89`），页脚却写"P / Esc 关闭"（:400）。按逻辑推断 Esc→关背包→自动关面板，但**未实测**，也未找到独立 Esc 处理。
2. **`SoulUI.DomainMode` 是否应长期为 true**：属性是 `{ get; set; }` 公开可写，游戏内无写入点；只有 checks 会设 false。若确认永久如此，`SoulUIStyle.DrawChrome`、`SoulUIEffects.DrawSoulRing/DrawLegendarySlot`、`SoulUI.HandleWindowDrag`、`SoulUI.DrawExtended` 里的 `if(!DomainMode)` 分支、`PanelBounds`/`TabBounds`/`ContentBounds` 的非域分支都是可删死代码（约 300 行）。**删除前需确认没有外部模组/兼容代码写这个属性。**
3. **`SoulUIArmoryDetails` 的 `armoryDetailHeight` 初值**：字段在 `:9` 声明但只在 `:83` 赋值；首帧 `InfoScroll(area, scroll, armoryDetailHeight)` 用的是 0，滚动条首帧不显示，第二帧才正常（自愈，不影响功能，但属实现瑕疵）。
4. **`SoulUIArchive.cs` / `SoulUIMemories.cs` 的命名与内容不符**（前者是特性页，后者是成长页）。是历史改名残留还是拆分顺序所致**未确认**；跨代理引用时按"内容"而非"文件名"找。
5. **`EnvironmentRules.Adaptation` 与 `Shared/Forest` 的乘算上限**：UI 直接展示 `strength`（`EnvironmentUI.cs:48-49`，四项相乘），未见 clamp；是否在战斗侧被再次限制**未核实**（属环境模块）。
6. **`SoulUIWoodPanel` 的 153 固定内容高度**：面板高度由 `SoulArmoryLayout.WoodPanel` 给（上限 212），gap 用 `Math.Max(2,...)` 兜底。极小窗口下内容会超出面板底边（被 `InfoClip` 裁掉），是否可接受**未确认**（渲染 check 里没有该组合）。
