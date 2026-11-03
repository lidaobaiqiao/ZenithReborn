# F — `checks/ZenithPrototypeCheck/` 夹具体系代码库地图

> 调查代理：general-purpose-6　|　范围：`checks/ZenithPrototypeCheck/` 只读　|　方法：`check(...)` 描述串全量抽取 + 头部/调度代码精读

## 0. 前置事实：文件数是 94，不是 100

对目录做 `find . -name "*.cs" -not -path "./obj/*" -not -path "./bin/*"`，结果为 **94**。
`Assets/`（含 `Assets/Baselines`、`Assets/LiquidGlass`）与 `Localization/` 两个子目录**不含任何 `.cs`**，只有 png/json/xlf 等资产。

因此本报告索引表为 **94 行**，覆盖 94/94 = 100%。任务书中的"100 个 `.cs`"应由任务派发时的近似计数导致；如果期望值确实是 100，说明另有 6 个文件存在于别处（`checks/archived-*` 三个归档目录，或已删除），**不在本次范围内，未核实**。

---

## 第一部分：夹具体系说明

### 1.1 定位

`ZenithPrototypeCheck` 是一个**独立的 tModLoader 附属模组**，唯一目的是给主模组 `ZenithReborn` 做可自动化的验收。

`build.txt`（4 行，无 `author`/`homepage`）：
```
displayName = Zenith Prototype Check (isolated)
version = 0.1
modReferences = ZenithReborn
buildIgnore = bin/*, obj/*, *.csproj
```

`ZenithPrototypeCheck.csproj` 关键点：
- `TargetFramework=net8.0`、`BuildMod=false`、`ImplicitUsings=enable`
- `<Import Project="E:/SteamLibrary/steamapps/common/tModLoader/tMLMod.targets" />` — 硬编码到本机 tML 安装路径，**不可移植**
- `<Reference Include="ZenithReborn"><HintPath>../../ZenithReborn/bin/Release/net8.0/ZenithReborn.dll</HintPath><Private>false</Private></Reference>` — 直接引用主模组**已构建的 Release DLL**，不是源码引用。所以夹具永远测"上一次构建产物"，改主模组后不重打包会静默测旧逻辑。

`build.txt` 里**没有** `side = Server`，模组同时可在客户端加载；但断言路径分两条（见 1.2），一条走 `-server`，一条走客户端 `PostUpdateEverything`。

`enabled.json` **不在仓库内**，由启动脚本在临时 profile 里生成：`checks/run_wood_checks.ps1:26` 写入 `["ZenithReborn","ZenithPrototypeCheck"]` 到 `$taskRoot/wood-check-profile/Mods/enabled.json`。构建用 `dotnet tModLoader.dll -server -build <src> -tmlsavedirectory <profile>`（`run_wood_checks.ps1:31`），因此是**无窗口服务端构建 + 无窗口服务端运行**。

### 1.2 调度入口

**唯一总调度：`Check.cs`** —— 文件内两个类：

| 位置 | 作用 |
|---|---|
| `Check.cs:10` `public class ZenithPrototypeCheck : Mod { }` | 空 `Mod` 类，只为让附属模组能被 tML 识别为模组本体 |
| `Check.cs:11` `public class Check : ModSystem` | 真正的调度器 |

两个触发钩子，互斥靠 `ran` 标志：

- `Check.cs:19 PostUpdateEverything()` — 客户端菜单出现后在主线程跑一次。注释（`Check.cs:17-18`）说明为什么不用 `PostAddRecipes`：加载线程设置鼠标后，客户端更新会覆盖 `Main.mouseX/Y` 与 `Main.UIScale`，导致 UI 夹具不稳定。
- `Check.cs:26 PostAddRecipes()` — `if (!Main.dedServ || ran) return;` 即**仅在独立服务端**执行。这是所有自动化 CI 走的路径。

**注册机制 = 环境变量路由**（不是属性扫描、不是反射发现）。`Check.cs:34` 读 `ZENITH_CHECK_REPORT` 作为报告输出路径，为空则**整体退出、什么都不跑**。然后 `Check.cs:38-170` 是一串 `if (Environment.GetEnvironmentVariable("ZENITH_XXX")=="1")` 分支，每个分支硬编码一串 `XxxCheck.Run(Assert)` 调用，写完报告后 `Environment.Exit(0)` 立刻杀进程。

已确认的 14 个环境变量入口（`Check.cs`）：

| 行 | 环境变量 | 拉起的夹具 |
|---|---|---|
| 38 | `ZENITH_FLYING_BLADE_CHECK` | FlyingBladeCheck |
| 44 | `ZENITH_FLYING_BLADE` | FlyingBladeSurveyCheck（普查，不判定） |
| 51 | `ZENITH_MELEE_MATRIX` | LivingMeleeMatrix（矩阵，不判定） |
| 57 | `ZENITH_LIVING_ARMAMENT` | LivingArmamentCheck |
| 63 | `ZENITH_HEAVEN` | HolyHeavenCheck |
| 69 | `ZENITH_GLASS` | RibbonCheck |
| 75 | `ZENITH_INFORMATION_UI` | UiCheck, UiInputCheck, WindowDragCheck, TraitCheck, DomainCheck, InformationUiCheck, ArmoryUiCheck, BossUnlockUiCheck（8 个） |
| 83 | `ZENITH_ECHO_ORE` | EchoDamageCheck, OreStackCheck, OreCheck → 然后 CurseCheck, BlessingCheck |
| 92 | `ZENITH_LEGENDARY` | LegendaryCheck |
| 98 | `ZENITH_ORE` | StageOneCheck, RibbonCheck, OreCheck |
| 107 | `ZENITH_TRAIL_MOTION` | SoulTrailMotionCheck |
| 113 | `ZENITH_REMOVE_SYSTEMS` | RemovedSystemsCheck, SwingCheck, StageOneCheck, WoodCheck, WoodV2Check, OreCheck, OreStackCheck |
| 120 | `ZENITH_ECHO_PHASE` | EchoPhaseProbe |
| 125 | `ZENITH_WOOD` | **全量回归**，29 个夹具（见 `Check.cs:129-160`） |
| 164 | `ZENITH_STAGE_ONE` | StageOneCheck, RibbonCheck |
| （兜底）`Check.cs:171-308` | 以上都不设时 | 内置"prototype"综合套件，直接内联断言（不走任何 `*Check` 类） |

`ZENITH_WOOD` 是事实上的**总回归入口**：`StageOneCheck, RibbonCheck, WoodCheck, WoodV2Check, ProgressionCheck, SwingCheck, DamageBalanceCheck, VanillaReturnCheck, EchoDamageCheck, OreStackCheck, SparkCheck, NativeParticleCheck, HardOreCheck, LunarCheck, UiCheck, UiInputCheck, WindowDragCheck, TraitCheck, OreCheck, OreRewardCheck, SupportIntegrationCheck, CurseCheck, DomainCheck, LegendaryCheck, FollowupCheck, BlessingCheck, BlessingCameraCheck, EnvironmentAttributeCheck, EnvironmentCombatCheck, FlowParticleCheck, OrePierceCheck`。

**注意：49 个文件根本不经过 `Check.cs`。** 它们是 `ModSystem` 各自在 `PostAddRecipes`/`PostUpdateEverything`/`PostDraw` 里读**自己的**环境变量（`ZENITH_*_RENDER`、`ZENITH_*_GPU`、`ZENITH_*_GUI`、`ZENITH_PREFIX_REPORT` 等），由 `checks/run_*.ps1` 逐脚本调用。这是**双调度体系**：断言语义走 `Check.cs`，GPU/画面/实机采集走各 `ModSystem` 自注册。

### 1.3 断言原语

**唯一定义处：`Check.cs:15-16`**

```csharp
private void Assert(bool ok, string message)
{ if (!ok) throw new Exception(message); checks++; }
```

- 失败即 `throw new Exception(message)` → 被 `Check.cs:311 catch(Exception error)` 捕获 → 写 `"FAIL: " + error` 到 `ZENITH_CHECK_REPORT` → `Console.WriteLine(error)` → `Environment.Exit(1)`。
- 失败是**快速失败**：第 N 条断言抛异常后，本进程剩余所有断言都不执行。
- 计数 `checks` 只统计**通过的**断言；报告里的 `PASS: {checks} xxx checks` 是成功数，不是总数。
- 以 `Action<bool,string> check` 形式注入每个夹具：所有 `*Check.Run` 签名都是 `public static void Run(Action<bool,string> check)`。夹具**不知道** `checks` 计数器，无法自行提前退出或跳过。

`CurseFixture.NeutralDefaults` 是**全局开关式夹具**（`CurseCheck.cs:15-19`）：
```csharp
internal static bool NeutralDefaults;
public override void SetDefaults(Item item)
{ if (NeutralDefaults && item.ModItem is SoulItem soul) soul.LegacyReleased = 63; }
```
设为 `true` 时，所有新建 `SoulItem` 都被伪装成"旧档已解咒"状态，这样旧套件（WoodCheck/OreCheck 等）能测"解咒后的正常机制"。`Check.cs` 在调用这些套件前置 `true`，调用 `CurseCheck`/`BlessingCheck` 前置回 `false`（`Check.cs:85-88`、`Check.cs:127-151`）。**这是夹具之间最强的隐式耦合**。

### 1.4 分组（94 个文件）

| # | 分组 | 文件数 | 说明 |
|---|---|---|---|
| A | 调度与夹具基础设施 | 6 | Check.cs、GlobalUsings.cs、CombatFixture、UiFixture、BlessingGuiFixture、OreCombatFixture |
| B | 剑魂核心：出剑/命中/时序/尾迹 | 13 | StageOne、Swing、Ribbon、MaterialTrail、VanillaReturn、SoulTrailMotion、EchoDamage、DamageBalance、Legendary、Followup、FlowParticle、NativeParticle、Spark |
| C | 诅咒与祝福 | 4 | CurseCheck、BlessingCheck、BlessingCameraCheck、EchoPhaseProbe |
| D | 族·木质 | 3 | WoodCheck、WoodV2Check、ProgressionCheck |
| E | 族·矿石（含困难模式矿） | 6 | OreCheck、OreStackCheck、OreRewardCheck、HardOreCheck、OrePierceCheck、SupportIntegrationCheck |
| F | 族·飞刃 | 2 | FlyingBladeCheck、FlyingBladeSurveyCheck |
| G | 族·神圣/星辰/活武 | 4 | HolyHeavenCheck、LunarCheck、LivingArmamentCheck、LivingMeleeMatrix |
| H | 环境属性 | 2 | EnvironmentAttributeCheck、EnvironmentCombatCheck |
| I | UI 与交互 | 8 | UiCheck、UiInputCheck、WindowDragCheck、InformationUiCheck、ArmoryUiCheck、BossUnlockUiCheck、TraitCheck、DomainCheck |
| J | 存档迁移与回归基线 | 3 | RemovedSystemsCheck、Migration79RenderCheck、Baseline79RenderCheck |
| K | GPU 渲染 / 实机画面采集 | 26 | 全部 `*RenderCheck`、`Airflow*`、`SoulFlightWorldCheck`、`MaskCompositionPreview`、`SwordBodyMaskPreview` 等 |
| L | 探针 / 只测量不判定 / 几何工具 | 10 | PrefixProbe、MoonWorldProbe、WorldRefractionProbe、GlassBufferReadback、GlassHighlightProbe、LiquidGlassGpuTimer、LiquidGlassTrailMesh、SwordAirflowMask、SwordBodyVectorMask、PreviewColorNoise |
| M | 纯代码数学与视觉参数断言 | 7 | SwordColorNoise、SwordFlowMeshBasis、SwordFlowPalette、SwordSpaceDistortion、SwordTrailInnerContour、SpaceWarpRegion、LiquidGlassTrailGeometry |
| — | **合计** | **94** | |

断言语义（A–I + M，共 46 个文件）在服务端可跑；K 组 26 个必须有 GPU/窗口；L 组为探头工具。

经 `grep -c "check("` 统计：**45 个文件含 `check(...)` 调用**（共 967 处调用点附近），**49 个文件零 `check(...)`**（K/L/J 部分 + A 全部 + Check.cs 本体）。

---

## 第二部分：文件索引表（94 行）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| Check.cs | ModSystem 调度器 | A 调度 | 定义 `ZenithPrototypeCheck : Mod` 空类；`Assert(bool,string)` 抛异常式断言，成功才 ++；按 `ZENITH_*` 环境变量分派 14 条测试流水线，兜底跑内联 prototype 套件（本地化/槽位/深克隆/存档/网络/重铸/HERO/机械封印/十剑觉醒/交叉召唤掉落） | 被全部 `*Check.Run` 依赖；定义 `Assert` 唯一实现；`CurseFixture.NeutralDefaults` 在此翻转 | 已验证 |
| GlobalUsings.cs | global using | A 调度 | 7 条 `global using`（System/集合/IO/Linq/Text/Threading/Tasks）。无逻辑 | csproj `ImplicitUsings=enable` 的补充 | 已验证 |
| CombatFixture.cs | ModSystem 实机夹具 | A 调度 | 只在 `ZENITH_COMBAT_GUI=1` 且 `SavePath` 含 `stage-one-playtest` 时启用；`PostUpdateWorld` 第 60 tick 造 5 把矿剑剑魂 + 20 万血木桩，摆好光照/时间，产出两个确定性视觉检查点（bloom → 穿透飞行），Esc 切换 | 依赖 `OreFamily.DefaultType`、`SoulProgress.All/Cores`；纯客户端 | 已验证 |
| UiFixture.cs | ModSystem 实机夹具 | A 调度 | 三条互斥路径：①`ZENITH_DRAG_REPLAY=1` 第 120-140 帧合成拖拽并写 `window-drag-replay.json`（注释明确声明"非 OS 拖拽证据"）；②`ZENITH_UI_GUI`/`ZENITH_ORE_GUI`/`ZENITH_VISUAL_GUI` 驱动真实面板点击；③`ZENITH_DOMAIN_GUI` 驱动全屏配阵 | 直接调 `SoulUI.PanelBounds/HandleWindowDrag`；写 `Main.SavePath` | 已验证 |
| BlessingGuiFixture.cs | ModSystem 遥测 | A 调度 | 只读遥测：等 `UiFixture` 备好剑魂后采集 `activated/rendered/samples` 写 `blessing-gui.json`；**不自动按键、不自动激活**，必须真实键位触发 | 依赖 `UiFixture` 先跑；`Main.dedServ` 直接跳过 | 已验证 |
| OreCombatFixture.cs | ModSystem 实机夹具 | A 调度 | `ZENITH_FIX_GUI=1` 下的矿石实战采集：`ore-fix-combat.json`、`ore-tier-combat.json`、第 233 tick 的 `cursor-ring-validation.json`、共享支援 `ZENITH_SHARED_SUPPORT_GUI` | 依赖 `SoulUI.EditingSoul`；纯客户端 | 已验证 |
| StageOneCheck.cs | 静态断言套件 | B 核心 | 第一阶段全流程：新魂不稳定、可预览不可攻击、旧魂迁移保值、守护者友好、对话前置、接受末行才给羁绊、不伪造 Boss 击杀/魂核、祝福开 1 槽 + 65% 强度、不可重复领、祝福+前缀持久化、网络持久化、哥布林重铸保值、冰剑给霜burn/草剑给毒、可原样取回 | `SoulProgress`、`SoulItem.LegacyReleased`、`SoulGuardian` | 已验证 |
| SwingCheck.cs | 静态断言套件 | B 核心 | 复刻原版 `ItemCheck` 射剑时钟：与真实 Zenith 在相同前缀/攻速下 tick 与动画计时一致；跨 10 宿主边界不重置；一次机会只射 1 把（永不批量）；0/10/20 出手、第 30 帧复用；稀疏装不重复宿主；空节点留空；缺宿主时不射；仅 owner 客户端造剑；背包刷新保持前缀时序；逐剑调色板保留 | `SoulPlayer`、`PrefixID`、`SoulBlade` | 已验证 |
| RibbonCheck.cs | 静态断言套件 | B 核心 | 剑尾几何：有界三角形、贴近武器路径不横跨屏幕、按刃宽淡出、尾端透明、相机平移不改尺寸、不足 2 个采样不画、隐藏剑不画、传送断点不连成整屏、非法旋转不进 GPU；飞剑拖尾 newest-first 有界、尾部渐透明、传送不连屏、非法采样丢弃；阵型 20 帧淡入 | `SoulTrailUnits`、`Soul` 网格；也用于 `ZENITH_GLASS`/`ZENITH_ORE`/`ZENITH_STAGE_ONE` | 已验证 |
| MaterialTrailCheck.cs | 静态断言套件 | B 核心 | 材质拖尾：有界三角几何、沿真实非圆路径、相机平移不滑材质坐标、自有 shader 吃普通距离坐标、每姿态生命期 alpha（含最旧透明样本）、结束坐标独立于材质平铺、父历史被覆盖后仍保留真实路径、冻结结束随相机不动世界、结束按满密度腐蚀、进度单调且与剑速无关、最旧锥形收零、无路径前不出材质、内边界固定姿态下推移、动画内边界不动真实刃尖、随剑可见度渐显、正常收回无残料、静止姿态不铺满、非法动画时间/旋转/传送被拒；剑影保 3 个原版历史采样、4 个剑尾窗口递减且严格留白 | `MaterialTrail`、`SwordTrail`；`EchoPhaseProbe` 的对照基准 | 已验证 |
| VanillaReturnCheck.cs | 静态断言套件 | B 核心 | 回归原版天顶飞行：子更新频率一致、同帧到期、`Projectile.Update` 相位与位置无时间重缩放、朝向正负曲线一致、物品美术旋转/翻转保留、原版淡出不做模型吸收；本体 60 阶段后停伤、仅可见剑影另行判定；自定义吸收彻底移除 | `SoulBlade`、`AI_182`；与 `EchoDamageCheck` 互补 | 已验证 |
| SoulTrailMotionCheck.cs | 静态断言套件 | B 核心 | 剑影相位一致性实测：移动/静止主体相对路径一致、移动不改变剑影可见时间、剑影随主人平移且形状朝向保持、平移前后碰撞一致、覆盖"可见"剑影、大距离传送清旧轨迹 | `EchoPhaseProbe` 的结论验收；`ZENITH_TRAIL_MOTION` | 已验证 |
| EchoDamageCheck.cs | 静态断言套件 | B 核心 | 三剑影伤害资格：本体停伤后剑影仍可攻、专用服务端能查到相同 3 个攻击姿态、单元比例、切剑影不绕冷却、剑影间空隙无整片拖尾伤害、本体+3 剑影各自消耗矿石首击、3 剑影共用锋钝每目标上限、熄灯后消散材质无伤、提前归鞘立即全关 | `SoulBlade` echo 段、`OreStack` 首击资格 | 已验证 |
| DamageBalanceCheck.cs | 静态断言套件 | B 核心 | 伤害分配：初始阶段保 65% 宿主伤害、进度不能替代十材质觉醒、觉醒铜剑无共享天顶伤害地板、加强宿主不摊薄弱宿主、宿主伤害只加自己那把、面板/前缀不能替代宿主伤害、发射剑用自己伤害且不按槽数均分、暴击逐剑（含前缀）、击退逐剑、原生飞行时钟替代宿主速度且每帧跑两次、模型放大 60% 但碰撞尺度保持、玩家攻速不重缩放 AI、重生态与命中间隔过网络、非重生 8 帧间隔、引擎强制同刃间隔、他刃独立命中、弱刃冷却后可再命中、附魔剑与星怒同帧触发、特殊弹幕伤害各自取源、各自冷却 45 帧 | `SoulBlade`、`SoulPlayer.Flow` | 已验证 |
| LegendaryCheck.cs | 静态断言套件 | B 核心 | 传奇位：仅在真实传奇模式可见、不被当作支援槽寻址、接受主武器、计入活跃主武器、过存读档、过网络往返、退出传奇后暂停槽仍可见可取回、暂停态拒绝插入、空手可取回 | `SoulItem.Legendary` 索引；与 `WoodV2Check` 的传奇 34 号槽交叉 | 已验证 |
| FollowupCheck.cs | 静态断言套件 | B 核心 | 后续回收路径：审计构造真能激活 11 个共享支援、掉到 0 授予时保留各自暂停命中目标、真实暂停槽点击取回自己那把、保留的传奇/皇冠 ID 不能撞未来第 15 个支援或重复可见槽、超越传奇/皇冠 ID 的未来暂停支援可取回且不碰传奇存储、图鉴主材质拆分与冷却文案与已确认诅咒例外一致 | `SupportIntegrationCheck` 的 11 支援上限；`TraitCheck` 图鉴 | 已验证 |
| FlowParticleCheck.cs | 静态断言套件 | B 核心 | 挥砍粒子：火花沿正向扫掠切线、镜像火花反向、传送不发射桥接粒子、非法姿态不发射、持久挥砍视觉受每次发射预算限制、离开挥砍重置发射器、脱离的尘埃无 projectile/父位置仍移动减速、脱离火花在父挥砍结束后仍按时消亡 | `SwordFlow` 粒子通道 | 已验证 |
| NativeParticleCheck.cs | 静态断言套件 | B 核心 | 原版粒子对齐：原版剑用其真实安装的 delegate/颜色/宽度、原生 AI 与剑魂每子更新产尘数一致、生成尘埃的贴图/类型/颜色/缩放/淡出/重力/alpha 匹配、同随机种子下位置与初速一致、比较确实走了真实产尘（非空发射）、附加宿主经原生实现保调色板、剑尖星光有原始相位时序 | `Main.rand` 种子；`UnifiedRandom` | 已验证 |
| SparkCheck.cs | 静态断言套件 | B 核心 | 自定义剑粒子：16 像素可见精灵且发光、黑暗中保持自发光不继承场景暗度、辉光独立于网格拖尾漂移淡出、有界生命期与褪色 | 自有粒子类型；与 `FlowParticleCheck` 分属"自制"与"原版对齐" | 已验证 |
| CurseCheck.cs | 静态断言套件 + GlobalItem | C 诅咒 | 文件内含 `CurseFixture : GlobalItem`（`NeutralDefaults` 开关，见 1.3）；六组诅咒全谱：新魂默认六诅咒且不看世界标志/兼容豁免（48 条）：阶段判定任意顺序、同一 Boss 不重复推进、单双子不算机械王、双双子算 1、外围 Boss 只记独立胜利不解咒、眼单独解绑且射程≥屏幕中点且不超已解射程、空封印仍算诅咒、切换只留第一个有效主剑、Brain 恢复全部特性不改独立伤害/不要求邪教徒、第一回归组 Boss 允许旧波继续、诅咒手势让旧主剑退役不删宿主、退役主剑视觉淡出无吸收、独行预算 2/3/常态4/祝福4、真实手势发射对应预算、祝福手势间隔可超 3 至 10、真实伤害路径限制 3/5/已解 重复命中、同刃可打不同敌人、每把新主剑独立命中字典、副剑命中不能绕过切换施加减益、第一切换组 Boss 恢复副剑减益、祝福下真实 ItemCheck 永不瞬时批量、祝福用唯一宿主并延续游标、原生手势 30 帧且稀疏宿主至 10、诅咒屏障挖约 2 格、弱化 4 格、扫掠地形不能穿墙、诅咒/弱化屏障挡住超 64 像素墙、已解恢复完整地形穿透、平台不静默挡剑、超额度立即截停、封印与显式遗留豁免持久不伪造击杀、全魂网络包保留诅咒记录且终点精确、克隆进度独立、旧魂可玩且不给 Brain/Eater 二义位、空旧残魂仍以新诅咒起步、哥布林重铸保封印与兼容态、真实前缀重建经 HERO 钩子恢复进度、真实遭遇区分 Brain/Eater、可选 Boss 进真实封印组、第二丛林 Boss 在 Golem 前完全移除屏障、第三丛林 Boss 记独立祝福阶段、外围胜利独立记录、剩余虫段阻止提前记账与重复交叉召唤、末段给待定个人封印并放行交叉召唤 | `SoulProgress`、`SoulCurse`、`CrossSummonDrops`、HERO 钩子 | 已验证 |
| BlessingCheck.cs | 静态断言套件 | C 诅咒 | 御剑祝福（38 条）：空表拒绝、15 秒有效战斗充满但不自动激活、满表 20 秒脱战不流失、换武器与死亡不清空、存量过存档/克隆/重铸/网络（精确载荷消费）、剑魂编辑不能误花满表、仅手动激活并消耗、起 10 秒、爆发不缩小已无界射程且激活中不可重启、距离倍率近身从 1 起、远端恰好 +50%、距离伤害不超 50% 上限、相机平滑到双倍视距不跳、剑魂编辑器暂停已激活爆发、爆发到期恢复相机与射程、仅释放/迁移旧魂不白拿祝福、训练木桩不能充能、不同主剑首击建真实 10% 中继、帧伤不重复吃首击中继、中继不再虚增木质标记只喂祝福伤害通道、中继上限 50%、3 秒无中继首击清加成、中继把山铜花瓣预算 30→33 不改冷却、原生完整回收武装同把宿主、回充不泄漏到别的宿主、充能首击发真实 50% 源挥砍、后续帧不重复消耗一次回充、回砍造成真实引擎伤害、祝福挥砍不递归充能矿石飞行、被打断/退役的刃不给回充、穿过实体砖武装 4 秒破甲 debuff、破甲真给 15 护甲穿透、每把发射剑消耗一次破甲且帧伤不复施、连斩祝福已替换旧剑痕爆炸不叠加、祝福后本体+3 剑影都用全量伤害、旧释放态不给新祝福加成、每个已祝福诅咒都解释一个已实现的额外机制 | `SoulBlessing`、`SoulBlade`、`Orichalcum` 预算 | 已验证 |
| BlessingCameraCheck.cs | 静态断言套件 | C 诅咒 | 相机 padding：原生 `InitTargets` 同时接受 192 像素 padding 补丁与两处 target-cap 访问；1280 宽半缩放截图获得超过原 192 边距；正常缩放保持原始地形缓冲 padding | 反射 `InitTargets`；GPU 侧另见 `BlessingCameraCheck` 与 `BlessingGuiFixture` | 已验证 |
| EchoPhaseProbe.cs | 探针（只测不判） | C 诅咒 | 实测"剑影亮灭时间窗"与"真实历史年龄"的偏差，用于确认与原版 933 一致。按 `SoulTrailUnits.Spacing=15` 采样、可见度按 `phase - index*Spacing`；每次更新推进 0.7~2 而非 1，故恒有偏差。文件头（`EchoPhaseProbe.cs:6-13`）明确写"这是原版行为不是缺陷，不要修" | 引用 `docs/原版天顶飞剑源码.md:111-113,400-405`；`AI_182_FinalFractal` | 已验证 |
| WoodCheck.cs | 静态断言套件 | D 木质 | 木质族（52 条）：8 把原版木剑按 `WoodKind` 顺序、枚举 8 连续且有本地化名、每把支持两种槽、阈值表、仅 `0xFF` 算满套、空装无收益、皇冠覆盖 3.00 强度 +100 生命、支援/奖励槽不能自举、满木给 5 支援无奖励槽（无矿石精通）、真实支援槽收货并保前缀、普通与支援数组去重、可取回、深克隆、全槽存读档/网络、V2 数组缺失迁移为空且不碰普通槽、哥布林重铸保支援与皇冠数组、森林/荫蔽首击 buff 仍生效、分数回复无每帧最小值、30 秒有效战斗 6 层基础上限、无皇冠不超过 6 层、6 层成长表数值、满木生命/防御/DR 用真实玩家属性、宽限期后每 2 秒掉 1 层、去掉任一木即失满套并立即清成长、恢复满套后续涨、换魂重置、死亡清零、6 个定时木标记都是 4 秒、每个标记都给可见减益、木 DoT 由主 P 与强度 2.00 推导、凋零每点强度只放大自身 DoT 2%、重复首击共享每目标冷却、自有木 DoT 按半速转 lifeRegen、NPC 木状态与剩余时长过网络、棕榈破甲与针叶/珍珠复合易伤、毒/火免疫独立于自定义标记、木桩排除刷 proc、针叶每点强度减速 4% 且不塌速度、Boss 不被强减速、8 木不开奖励/皇冠槽、矿石精通重开 2 奖励槽、矿石完奖励槽收真武器、非皇冠槽放泰拉刃激活皇冠覆盖、皇冠把上限翻倍到 12 层、皇冠奖励可取回、失皇冠夹回 6 层、17 个 UI 槽不重叠、满套给森林之力 buff、缺木源撤 buff | `WoodKind`、`SoulItem.Supports/Crown`、`SoulPlayer` | 已验证 |
| WoodV2Check.cs | 静态断言套件 | D 木质 | V2 木质（48 条）：名册严格 8 把原版木剑、保强度/生命/支援数、8 木给 mask 0xFF + Full + 5 支援、皇冠 2.00/+40 → 3.00/+100；三条羁绊锻造链（Forge：4 木 + 棕榈 + 铜/铁宽剑；缺铁断链；3 木不足第四阶）（Rot：6 木含红木/乌木 + 村正/永夜；缺红木断链）（Ember：6 木含红木/灰烬 + 炽焰巨剑；缺剑断链）；皇冠需 8 木 + 皇冠槽外的泰拉刃；皇冠槽内泰拉刃不能自解；8 木单独不够；皇冠槽 35/36 永不作支援序号；传奇 34 号槽被序号链跳过；序号 14 ↔ 槽 37 双向；基础序号 0-4 在槽 10-14；额外序号从槽 25 起；皇冠激活暴露两槽、未激活隐藏、激活收真武器、未激活拒绝插入并留光标；8 木不开奖励槽、满矿收藏开 2 槽；森林之力 6/12 层上限；0 层无成长；300 有效战斗帧 1 层；基础每层 2% 伤害/1% 速度/2 防御/0.3 回复/3% DoT/0.5% DR；基础 6 层封顶；皇冠激活条件是泰拉刃在宿主；皇冠每层 3%/2%/3/0.5/5%/1%；皇冠到 12 层；已移除木剑不再注册、ZenithReborn 无自定义木剑类、`WoodKind` 无 Fey 成员；传奇位第 8 把木剑使族计数 7→8、清空回到 7、未激活不计、传奇位不给额外支援槽、7 木+传奇第 8+泰拉刃达 8/8 与皇冠、清传奇位掉回 7 木与 1.50 强度；满矿收藏真开 2 森林槽、6 主木+2 森林木达 8/8、森林木不给额外支援槽或种族阶、皇冠槽内木剑不能解皇冠也不能喂计数、基线 8/8 真开 2 皇冠槽、木剑移进皇冠槽使皇冠自引用崩塌 | `WoodKind`、`SoulItem.Crown/Legendary`、`SoulBond` 三条链 | 已验证 |
| ProgressionCheck.cs | 静态断言套件 | D 木质 | 进度与引导（33 条）：新玩家得友好王令、中核死亡不复制开局礼、王令无史莱姆也能开友好遭遇、王令解释白天限制、真实 `NPC.NewNPC` 在隔离图格生成友好王、友好王无害不可杀可直接对话、重复用令不刷复制王、NPC 直聊推进一条故事线、给羁绊与第一普通槽、友好祝福不伪造原版 Boss 击败/进度、祝福不可二次、祝福后王安全消失、通关玩家可召回友好向导、召回王只给进度与寄宿不给第二次祝福、晚期 Boss 单独不能跳早期槽门、新未祝福魂不能吸收/回收魂核、正确已击败魂被选中给核、核解锁目标槽不改另一魂、眼击杀给精确缺核与回收、下一步推进到邪恶 Boss 第三槽、邪恶 Boss 解锁第三槽且不重复给核、骷髅王用复用黎明核开第四槽、肉墙等血肉核、单双子不够机械核/第六槽、邪教徒用复用神庙核开第九槽、所有槽门通向真实永久觉醒目标、已完成核回收不可重复、每普通/支援/奖励槽都有可用条件提示、回收在可用时消耗既有核、解锁后回收按钮不能再花核、丢核可从该魂已得胜利重建、无主魂身份不能回收、每个真实木质阈值解锁对应支援槽 | `SoulProgress`、`SoulGuide`、`SoulCore` | 已验证 |
| OreCheck.cs | 静态断言套件 | E 矿石 | 矿石族主干（53 条）：短/宽剑算 1 材质、10 矿石主剑无木给 6 通用支援、4 合法矿石支援与 10 主剑并存且排除退役存储、旧扩展数据可安全恢复、遗留普通/支援存储拒收、全部非活跃遗留项过存读档不截断、遗留存储网络精确消费、深克隆、重铸保非活跃扩展武器、8 个旧槽都能空光标取回、清空后回收页消失；飞行伤害 150% 基 + 材质缩放 + 十阶加成、1 次真实首击恰好造 1 把矿剑不是 6 把、部分收藏在首刃成形后仍hold fire、同一发射主剑的重复帧伤不能加剑、6 次独立首击收 6 种材质、收齐放整轮齐射、新旧收集剑等同最后一剑沉降充能、全部同时以初始加速度发射、远距离阵保留成长后沉降动画、显示放大不改原碰撞、真实宿主剑有可见正常绘制路径、真实命中穿透多敌且保速度、同剑同帧不能二次命中、6 帧重复伤害可用且不递归造剑、新主剑首击不被别的剑 proc 挡、同一主剑不能在后续帧重试、非法触发器/复用目标槽/非法源被拒、原生 Update 飞行能打带防御的移动目标、高防命中保源伤害份额而非最低 1、目标死亡把部分齐射放向最后位置而非删除、被打断的部分收藏超时后释放、暂停与回收槽不登记收藏、未附着预览不登记、连续真实有效寄宿收 14 材质不需额外槽、精通移除后仍持久并解锁 2 共享奖励槽、纯矿精通激活第一奖励槽并保武器前缀、纯矿精通激活两槽、矿石精通不要求也不伪造森林之力、两收藏族共享 2 奖励槽不叠成 4、去木撤森林之力但矿精通保共享奖励槽可用、收藏过存档/克隆/网络（精确包）/重铸保护、HERO 前缀重建恢复累计收藏、旧档不从 Boss 进度或非活跃槽推断满收藏、精通真给新波 30% 伤害 +50% 飞行距离、发射剑越过已死原目标跨精通射程继续、波相位/精通/距离过网络、精通飞剑在有界距离终止 | `OreFamily`、`SoulItem.Ore`、`SoulBlade` | 已验证 |
| OreStackCheck.cs | 静态断言套件 | E 矿石 | 铜锡叠层（20 条）：有效铜锡材质激活两项成阵联动、本体与各实体剑影原生命中各 +1 飞剑、叠层更新已存在与新生成员、同阵全剑拿当前层数增伤、同一单元重复帧伤不重复积攒、4 种矿材保留独立弹幕不合并、4 层齐射快照 = 基础 172×1.15 而不是逐层复乘、亮度动画连续不越界闪白、12 帧达目标亮度 95%、飞剑保原始伤害快照/层数/防御份额、接收层数从当前显示进度缓动不跳亮、新阵从 1 层与自身伤害重开且已释放阵不续叠、新旧波伤害隔离各自保倍率、无铜特性剑影仍伤害但不积百炼、剑影命中不提前吃本体资格、无锡特性多剑保原伤不提亮、切换诅咒的前四矿材例外不放开铜锡普通特性、有效支援里的铜锡短剑也提供同材质联动、未附着预览不给矿石特性、14 层上限 +65% 且异常层数不无限增伤 | `OreStack`、`StackGrowth`；与 `EchoDamageCheck` 首击资格交叉 | 已验证 |
| OreRewardCheck.cs | 静态断言套件 | E 矿石 | 矿石奖励与飞剑全谱（54 条）：1 材质解锁飞行且无隐藏主伤加成、1 材质真造首把飞剑、真实 1 材质飞行弹幕拿 100 伤父的 150%、150% 不把敌防乘 1.5、真实非矿主剑经 1 个活跃矿支援充 150% 飞剑、单活跃矿主剑为非矿主剑解锁首击充能且保真实源、非矿主剑单独不能造矿飞剑、切换允许 4 主限内第二把、错峰真实首击在共享预发射阵造 6 把不同龄剑、6 个真实环位等角含闭合缝、二阶/四阶给每矿主 5%/10% 且不乘数量、矿主加成不改无关剑伤害、六阶飞行折返并保伤第二趟、第二趟有限距离不死循环、5 材质拿不到六阶回攻、八阶飞行钴首击立即触发自身既有 proc、飞剑子弹幕保自身材质并通过真实源校验、补充材质用自身小份额缩放敌防、飞行帧伤不重复触发材质效果、八阶飞行山铜发既有三花瓣、钛碎片绕真实飞剑父、飞行钯用真实治疗并花既有 life-steal 预算、钯帧伤不重复治疗同一飞剑、飞行秘银标记被另一真实源剑消费、飞行精金用自身防御阈值、材质子弹幕不能递归充另一飞剑阵、7 材质不泄漏八阶效果、十阶矿支援让非矿主剑恰好充 1 把、10 次被支持首击完成一次十材质阵、十阶伤害与原生移速都 +50%、阶/动龄/额外趟过网络精确消费、掉到 10 以下保留非矿主充能但立即撤十阶速度、切换把矿主限到 4 而普通特性只留首主、同帧 10 个不同主击只出 4 把诅咒飞剑且无共享冷却、飞行限制保留结构支援与全直伤主剑、第一切换组 Boss 完全恢复矿石计数与触发资格不等祝福、已解切换允许第一把以外的主剑充能、第五把与第一把同类型也不能绕限、允许的主剑把真实源位置传给飞剑、弹幕网络包保源序号并消费整条消息、空普通槽不占 4 个有效主位、诅咒与图鉴都解释四主例外、满收藏给完整齐射加真实巨剑、巨剑有显式大额源缩放伤与无害预警、预警 30 帧、落下造成真实引擎伤害且首触不消失、巨剑不能每帧重复乘同一目标伤害、满收藏巨剑守共享 5 秒冷却、冷却后下次齐射再召巨剑、被打断的部分齐射不能触发完成才有的巨剑、加速原生移动 + 扫掠碰撞能打中 8 像素目标来去两趟、原生弹幕前几帧加速而非匀速滑、加速出程显著快于旧 49 tick 滑、巨剑预警/下落/原生碰撞产一次大命中 | `OreFamily`、`OreReward`、`OreGiant`、`SoulBlade` | 已验证 |
| HardOreCheck.cs | 静态断言套件 | E 矿石 | 六种困难模式矿（37 条）：6 个不同困难矿剑 ID、矿剑可进攻击与支援槽、矿特性有可见描述；钴：同帧重复命中不充能、大目标内真实主击正确充能、第 3 次合法钴击发真实有界 40% 刃、子弹幕继承剑魂与真实源武器元数据、钴子弹幕造成真实引擎伤害、子击不充下一次钴 proc、下次钴 proc 仍需 3 主击；山铜：三花瓣共享 30% 总预算、花瓣 AI 跑动并用合法自有源、花瓣守自身 120 帧冷却、低伤花瓣不能乘最低 1 伤；秘银：不能同趟消耗自己新标记、别的剑用自己的命中倍率恰好消耗一次、单槽秘银可下次飞行消耗、过期标记无加成；精金：排除低于防御阈的目标、达阈给 12% 源修正；钛：碎片 30% 伤并真实绕源剑、源通道不能保留两片活碎、碎片随精确源飞行结束而结束；支援槽：换入接受已注册矿特性、支援钴用独立计数器与每主源真实伤害、支援矿永不发独立主剑、移除支援提供者立刻结束其子攻击、支援秘银造跨源标记且不借支援伤害；钯金：8 点滚动预算跨 5 源通道共享并花 life-steal 资源、6 秒窗过期后预算恢复、过期连击不留旧充能；虫：虫段共享一个每目标充能门、共享虫根可在后续合法帧充能；元数据过真实包序列化 | `HardOreTypes`、`HardOrePlayer.Clock`、`SoulBlade` | 已验证 |
| OrePierceCheck.cs | 静态断言套件 | E 矿石 | 巨剑余波与剑体开关（7 条）：非法结束不进图形历史、巨剑余波存活超过小刃生命期且保接触独立于末刃姿态、独立余波无活武器也会过期、密集结束事件有固定内存预算、世界清理也清武器结束；剑体关附魔粒子但保留减益：关剑体粒子后原版火焰附魔仍能命中施加 | `OreGiant` 余波环形缓冲；`SoulBlade` 附魔开关 | 已验证 |
| SupportIntegrationCheck.cs | 静态断言套件 | E 矿石 | 共享支援池（27 条）：任意已注册或普通近战剑走真实通用支援转移、矿支援在 1 而非 10 解锁飞行、非木装配开通用支援阈且不造木 buff、真实纯矿支援插入接受材质、10 矿主 + 4 真支援产全 14 材质与共享奖励槽（无木）、活跃 UI 报真实主/支援材质贡献与当前确认伤害基、第 6 个已得支援用新存储、旧遗留 S6 永不静默激活、通用容量与全支援材质过真实存读档、新 S6 与退役 S6 存读档后仍分离且前缀精确、共享支援往返精确材质总量并消费扩展网络模式、新支援存储深克隆、重铸保新第 6 支援与前缀、HERO 重建把新得支援与退役存储分开并保前缀、混合种族池收木支援、混合种族池收额外矿支援、5 木 + 6 矿相加 11 共享支援而不是取最大或夹到 5、全部 6 个额外支援槽过序列化不截断在原 5、失全部锚点暂停每项授予但留额外道具可安全回收、暂停的第 11 支援有真实空光标取回路径、旧档不把禁用的扩展内容重解释为已得新支援、矿短/宽变体跨共享支援边界算 1 材质、锁定/奖励/退役存储不能自举通用支援或材质总量、最少活跃前缀只含 3 个可达支援并安全保留后续暂停项、切换诅咒压制支援战斗特性而不毁结构解锁、森林奖励槽计入族总量且无奖励槽例外、切换诅咒解除后战斗计数与全集一致、确认独立飞行快照对 100 伤源为 150/326/482 | `SoulItem.Supports` 扩展存储；与 `FollowupCheck` 的 11 支援上限交叉 | 已验证 |
| FlyingBladeCheck.cs | 静态断言套件 | F 飞刃 | 飞刃族（30 条）：可合成与掉落两组名册成员就位、弹幕剑与非常规不入族、名册 31 把、名册全部无引擎弹幕、族内无武器不激活、1 把即激活、首个蓄能周期叠出第 1 把、同周期不重复叠层、间隔 18 帧叠第 2 把、蓄能到独行上限待命、齐射掷出全部、掷出后清空重新蓄能、相邻飞刃相隔 5° 且速度一致、直飞 600 像素后进入旋转、旋转 45 帧存活、旋转结束转收回、收回途中不再造成伤害、射程/旋转周期/持续伤害/速度为暂定初值、2 把进第 2 阶并按数量放大伤害、4 把进第 3 阶、第 3 阶让非本族主剑也能掷飞刃、6 把进第 4 阶、第 4 阶把收回期伤害开关写进弹幕、第 4 阶收回期保留伤害并可命中、未解咒时族内数量照常统计、族激活时本体飞剑完全不发射、族激活时挥臂只掷飞刃且场上无本体飞剑、族无武器时取代不生效、族未激活时挥臂照常发本体飞剑、族未激活时挥臂不出飞刃、族激活但无蓄能飞刃时本次挥臂不取代 | `FlyingBladeFamily`、`FlyingBladeShot`；依赖 `CurseFixture.NeutralDefaults=true` | 已验证 |
| FlyingBladeSurveyCheck.cs | 普查探针（不判定） | F 飞刃 | 遍历 `1..ItemID.Count` 全部原版可寄宿近战物品，逐项记录 `Item.shoot` 弹幕字段与合成配方情况，写 JSON 数组（`FlyingBladeSurveyCheck.cs:32`）。注释（`:8-10`）明确只读引擎数据、不改行为不写档；走特殊机制而非 `shoot` 的挥舞弹幕会显示为"无弹幕"，需另行核对 | 消费 `SoulItem.CanHost`；产出 `flying-blade-survey.json`；决定名册"可合成/仅购买掉落"分组 | 已验证 |
| HolyHeavenCheck.cs | 静态断言套件 | G 神圣/星辰 | 神圣与星辰族（15 条）：名册边界、有效主槽激活族特性、界面显示有效族特性、独立种族筛选能找到落星特性、有效星辰支援可激活、取下唯一族成员立即失效、有效命中生成一颗落星、基础 1000 伤且从上方真实坠落、同一剑魂冷却阻止连续生成、真实弹幕直击产生伤害并转入冲击、冲击不重复伤害直击目标但命中范围内其他敌人、同一冲击不重复伤害目标、范围与初始冷却一致、世界清理后可再触发、方块撞击产生冲击 | `HolyHeaven` 族、落星弹幕、冲击波 | 已验证 |
| LunarCheck.cs | 静态断言套件 | G 神圣/星辰 | 月亮事件恢复（22 条）：复现原版"无事件标志则无倒计时即使四柱已尽"、三柱不能触发恢复、四柱真实击杀在无原版标志下恢复 60 秒倒计时、既有原版倒计时永不被重置为 60、世界重载恢复剩余倒计时与原最大值、已观测到的月总消耗恢复使 Boss 死后不重生、完成战斗不自动重复、活柱即使历史说四柱全死也阻恢复、恢复等活着的活跃玩家、延迟恢复在玩家活后继续、客户端不能本地起权威 Boss 倒计时、仅历史柱标志永不自动召唤旧世界、手动修复不能绕过未完成柱史、已完成的旧世界显式修复起一次正常倒计时、重复修复不复制不重启待定召唤、原生天界符倒计时被同一持久钩子捕获、新柱循环不继承旧的待定召唤、排队修复夹具限在隔离 profile、排队恢复永不作用于不同 world GUID、匹配世界恢复应用一次后归档、原版倒计时到期真造月总核心 NPC、成功原版生成清持久待定态 | `LunarEvent`、`WorldFileData` GUID、`ZENITH_LUNAR` | 已验证 |
| LivingArmamentCheck.cs | 静态断言套件 | G 活武 | 活武族（11 条）：新种族与神圣/矿石/木质同时生效、每个有效出战槽各有召唤体、重复更新不重复召唤、出战武器通过玩家近战流程真实命中目标、种子弯刀通过原版物品检查发射、星怒通过原版物品检查发射、飞刀从玩家通道发射、飞刀继续由代理持有并恢复真实玩家、代理结束后保留真实玩家物品/位置/鼠标、失去激活成员时召唤体退出、召唤体消失后飞刀退出代理并恢复真实玩家 | `LivingArmament` 代理玩家；`ZENITH_LIVING_ARMAMENT` | 已验证 |
| LivingMeleeMatrix.cs | 矩阵探针（不判定） | G 活武 | 对同一固定目标与武器，逐项比较"真实玩家"与"生产代理"两条路径，记录未能验证的情况，写 `ZENITH_MELEE_MATRIX_OUTPUT` JSON（`LivingMeleeMatrix.cs:73`）。重置 `Main.rand=UnifiedRandom(318)`、`projectile/npc` 全灭、`netMode=SinglePlayer` 保证可重复 | 需要 `CurseFixture.NeutralDefaults=true`（`Check.cs:53`）；产出 `matrix.json` | 已验证 |
| EnvironmentAttributeCheck.cs | 静态断言套件 | H 环境 | 环境属性元数据（15 条）：环境基底恰好用户定义的 21 个唯一稳定定义、环境 ID 往返与 E01-E21 一致、每个环境发布其调好的值与命名机制、未设/未知 ID 不是真实环境属性、确认的木质环境主题由真实加载钩子注册、空与未分配弓不发明环境标签、一把真实矿剑可接受多个独立存储环境属性（复制并去重作者输入）、非法作者输入不能部分替换既有武器绑定、类型元数据过真实物品存读档/克隆/前缀且不加每件存档状态、真实物品网络读看到相同元数据且不扩载荷、宿主属性视图同时覆盖普通活跃剑与通用支援、退役回收存储不静默并入活跃元数据视图、改环境元数据不换种族身份也不改伤害/套数/支援容量、传奇主剑通过既有活跃源枚举参与、元数据作者测试会还原原矿环境绑定 | `SoulEnvironment`（21 项）、`SoulItem` 绑定 | 已验证 |
| EnvironmentCombatCheck.cs | 静态断言套件 | H 环境 | 环境战斗机制（38 条）：21 个不同环境给加法 420% 成长、森林无削弱环境、森林固定 15% 只作用一次不自我递归、类型化被动在共享成长/森林因子前合并、活跃领域只给它对应环境 +50%、集齐所有环境不等于点亮所有机制、选定祝福在原生物群系外运行、真实命中修饰把溢出暴击转成伤害系数而非平加伤害、引擎 hit-info 真的把暴击乘溢出系数、休眠神圣保被动但不能转溢出、持久机制选择尊重 5 个真实解锁槽、环境账本/选择/领域一起克隆、真实 ItemIO 存读档保环境选择、真实网络往返消费完整 v8 环境载荷、重铸保全部环境进度与选择、HERO 重建保环境账本与选择、森林真养一片叶并消耗它换一次必闪、未熟叶不能反复无视伤害、冰机制产真实可吸收护甲资源、地狱伤害产真实治疗并花 life-steal 预算、月噬阻环境吸血、12 秒战斗产就绪蜂蜜资源、蜂蜜在受击后真给渐进治疗、独立机制分别执行、自有毒芽走引擎 NPC 回血伤害路径、后续帧命中不能额外加主 proc 计数、自然发现不能插非法环境位、换武器移除剑魂被动与机制、遗留回收物品不能制造环境收集进度、未收集环境不能被选为活跃领域、自然战斗收集神庙且命名群系压过通用洞穴深度、通用洞穴可在命名群系外收集、人造领域不能生成自己的自然探索发现、非法领域与超上限选择被安全归一、真实引擎弹幕伤害调用环境首击流水线、环境子弹幕造成真实引擎伤害、真实子伤害不能递归生成环境攻击、爆发根免疫在普通 NPC 免疫被清后仍有效 | `SoulEnvironment` 机制、`SoulBlade` 首击钩子 | 已验证 |
| UiCheck.cs | 静态断言套件 | I UI | 面板布局与点击（18 条）：轨道导航在裁剪宿主画布内、关闭按钮留屏、槽水平边界与可用目标、槽永不重叠、10 主槽共享一条圆轨道、森林奖励槽占左上弧、支援槽占右下弧、碎片导航不重叠任何武器或碎片目标、真实可见 handler 在缩放下插入并保前缀、真实 handler 返回预览武器、被拒物品留光标、隐藏槽不能吃其他页点击、体外不能换武器、17 个槽索引都能正确取回暂停项、Tab 导航消费点击不关编辑器、导航永不存光标物品、关闭按钮不动光标物品 | `SoulUI.SlotBounds/TryClickHost`、`Main.UIScale` | 已验证 |
| UiInputCheck.cs | 静态断言套件 | I UI | 输入屏蔽（17 条）：屏蔽层先于原版背包而前景面板保绘制序、更新期像素命中与 UI 面板在缩放下一致、面板外视口在缩放下仍可交互、按钮与释放边在背景 UI 更新前被藏、世界动作在玩家控制拷贝前被遮、真实底层原版槽在捕获点击下不能取/装/分、世界使用/放方块被阻而移动保留、前景槽仍恰好收到一次捕获点击、前景面板之后图层也收不到、物理状态恢复且点击被消费到下一帧、按住不重复交换、拖到外面保持所有权、在外面释放释放捕获、未覆盖的原版背包仍可用、关闭按钮能穿过捕获、关闭不把仍按住的点击泄漏给世界、关闭并释放后输入恢复 | `SoulUI` 输入层、`PlayerInput` 掩码 | 已验证 |
| WindowDragCheck.cs | 静态断言套件 | I UI | 窗口拖拽（10 条）：真实输入钩子在缩放下先于世界输入起头拖、移动面板保持背景屏蔽、拖到外面夹住窗口并保点击所有权、外面释放结束拖拽、拖拽释放后外部输入恢复、重开记住本次会期窗口位置、光标武器不能误起拖拽、关闭按钮排除在拖拽把手外 | `SoulUI.HandleWindowDrag`、`HeaderDragBounds` | 已验证 |
| InformationUiCheck.cs | 静态断言套件 | I UI | 信息页布局（11 条）：信息卡在窄/宽屏保持实际可读宽度、多列卡片独立命中框不互覆、入场与悬停不改卡片命中区、信息页用完整工作区不被旧剑库边栏挤占、四个任务入口在信息页可见、主导航只有四项且旧物回收不常驻、旧图鉴页已删除（回收只在有旧物时可进）、列表与详情各自固定在工作区内不互覆、窄屏上下排列宽屏左右分栏、悬停动画单调收敛不闪烁不越界、短动画半秒内收敛端点固定 | `SoulUI` 信息页/图鉴页 | 已验证 |
| ArmoryUiCheck.cs | 静态断言套件 | I UI | 剑库/配剑界面（19 条）：剑位保持可用尺寸（暂停与扩展支援也在可滚动画布内）、主攻/奖励/传奇/全支援命中框无重叠、三区或窄屏视图都在当前工作区、宽屏剑库与详情不重叠、短视口改并排操作（确认与取回始终可达）、详情与剑库滚动区各在自己面板内、滚动详情与固定操作可见区分离、中央羁绊核心读真实木质阶段不写剑魂、换入计算不写真实快照/收藏/背包、选择候选只改界面状态、再选同剑位退出候选预览不改真实物品、选其他剑位退出预览并显示当前、确认交换真实物品并保前缀且原武器回原背包格、完成寄宿后清候选不能重复提交旧对象、背包格被替换后旧候选不能误寄宿新物品、同种限制用原规则（拒绝预览不改收藏）、窄屏隐藏的剑阵不能吃剑库点击、定位空位只选已开放空位并返回剑阵、未解锁剑位候选预览被真实规则拒绝 | `SoulUI` 剑库、`SoulBond`；与 `DomainCheck` 互补 | 已验证 |
| BossUnlockUiCheck.cs | 静态断言套件 | I UI | Boss 解锁界面（11 条）：亮暗只取个人铭刻（任意击败顺序正确）、阶段收益按累计数量点亮、未来奖励与当前说明共用同一来源、预览未来奖励不改剑魂数据、旧档解咒不伪造击败头像且不提前点亮祝福、双子只杀一只显示部分完成不加完整铭刻、已有铭刻但未附着时不宣称属性生效、普通特性不会误用 Boss 解锁列表、每个 Boss 都有真实多帧 Wiki 动画、动画尊重 GIF 原始时长并循环、六组 15 个挑战目标都有头像资源 | `SoulProgress` 铭刻、资产目录 `Assets/Baselines` | 已验证 |
| TraitCheck.cs | 静态断言套件 | I UI | 特性与图鉴（25 条）：未附着预览永不显示活跃特性、图鉴在无装备时保全部 8 木 + 6 实现困难矿效果、每条图鉴有唯一 key/当前描述/激活条件、有效木主剑 + 钴支援激活对应效果、一个活跃矿支援经共享机制解锁飞行且不发明满森林或缺木、六个当前诅咒态替换过时机械诅咒条目、锁定的重复矿主不改变合法支援提供的 1 材质、已解锁矿主激活波且兼容态恢复正常能力不发明个人祝福、回收存储与暂停森林槽不把效果漏进摘要、有效支援族出现在活跃摘要、失木解锁立即暂停冰支援、满有效木集显示 8 效果与森林之力、满森林装配只留 3 行紧凑 tooltip（详细在 UI）、每条自定义 tooltip 短且单行、长宿主名不撑大剑魂 tooltip、去木在真实 tooltip 隐藏其效果与森林之力但保留图鉴链接、图鉴分类筛选返回正确实现条目、生效筛选只显示当前实际生效条目、同一工作区可恢复全部特性及激活条件、浏览筛选图鉴不改存储物品或进度、目录不能吃槽点击或存光标物品、有旧物时也不加主导航（回收由配剑区提示进入）、配剑区回收视图保旧物原引用（取空后不再有旧物）、脱离的存储进度永不宣称可用能力、未附着 tooltip 只加一条短"下一步"提示 | `SoulTrait` 图鉴、`SoulItem` 特性摘要 | 已验证 |
| DomainCheck.cs | 静态断言套件 | I UI | 全屏配阵/领域（29 条）：剑库只读真实合格背包武器（排除弓/工具/递归剑魂）、独立场景覆盖完整 UI 视口、配阵主页宽屏三区分开窄屏同区切换全在视口内、世界难度与退出控件可达、碎片导航不撞背包或难度控件、全屏场景移除隐藏背景悬停产生者但保留原生鼠标文本、真实原版光标层在全屏场景之上保原像素比例、手持光标物品在原生光标之上可见但不恢复隐藏背包、直接存入转移精确带前缀物品不克隆、替换把出向剑原子回原背包位、锁定主槽拒收剑库存入且不动源、剑库不能重开退役扩展槽、重复宿主校验也作用于快速装备、手持光标物品不能被剑库操作覆盖、空手星图点击只选择不抽进隐藏背包、起飞把精确剑还给真实背包、满包拒绝起飞不掉不删、遗留回收仍走新背包路径还给物品、剑库选择并存入真实第 6 个已得共享支援、切页保持活跃新支援选择而不与退役存储混淆、满领域能把暂停新支援带前缀还给真实背包、既有原生传奇世界不被误降级、难度按钮改真实原版 master/FTW 世界标志、存档保还原出处、二次点击还原精确原始难度与 FTW、旅程世界保其原生世界类型、客户端不能静默改多人世界、既有 Boss 战不能中途换属性基线、待定月总事件也阻切换 | `SoulUI.DomainMode`、`WorldGen` 标志位 | 已验证 |
| RemovedSystemsCheck.cs | 静态断言套件 | J 迁移 | 已移除系统回归（8 条）：已移除的系统不再注册、木剑/矿石剑/前缀经旧档读写保留、原灵能名册武器仍可存支援槽、旧武技字段可回退但不再参与战斗、旧武技字段保网络末尾三字节格式、网络数据完整读取且保留旧字段、网络同步保木质与矿石寄宿物品、重铸保旧字段与寄宿物品 | `SoulItem` 遗留字段、`archived-removed-systems/` | 已验证 |
| Migration79RenderCheck.cs | 渲染检查 ModSystem | J 迁移 | 恢复操作直接对照"收藏 79"的编译着色器，不用新版本美术阈值重新定义 79；写 `result.json` / `error.txt` | `Assets/Baselines` 的 79 基线；`ZENITH_MIGRATION_79_RENDER` | 部分待核实 |
| Baseline79RenderCheck.cs | 渲染检查（静态） | J 迁移 | `Run(string output)` 产 `baseline79.json`，用于 79 基线像素级对照 | 与 `Migration79RenderCheck`、`check_color79.py` 成组 | 部分待核实 |
| AirflowAtlasBatchCheck.cs | GPU 导出 ModSystem | K 渲染 | 一个隔离 GPU 客户端顺序导出整组气流图集（避免逐剑重启游戏）；读 `ZENITH_AIRFLOW_EXPORT`/`ZENITH_AIRFLOW_RECIPES`，逐剑写 `profile.json`+`gpu.json`，全部完成写 `complete.txt`，异常写 `error.txt` | 消费 `SwordAirflowMask`；`run_airflow_batch.ps1` | 部分待核实 |
| AirflowMaterialCheck.cs | GPU 合成 ModSystem | K 渲染 | 调生产合成入口做隔离 GPU 验证：Alpha 相乘、状态恢复、原版剑体遮挡顺序；延迟解析生产合成 API（普通素材导出模式不引用旧包缺失方法）；绑定另一绘制者的丢弃型目标检查合成返回时原画面是否仍在 | `run_airflow_material.ps1` | 部分待核实 |
| AirflowWorldCheck.cs | 实机世界夹具 | K 渲染 | 只在专用隔离目录启用：加载已复制的测试人物/世界，直接观察生产飞剑 AI 与绘制；提供 `ZENITH_DUMMY_CHECK`/`ZENITH_DUMMY_PLACEMENT_ONLY`/`ZENITH_DUMMY_BEFORE_PLACEMENT` 三开关；走原版完整物品使用流程放稻草人（覆盖全局物品钩子与挥手动画，不用单回调替代）；不叠加第二套预览特效、不保存测试物品/NPC、不读正式存档 | `run_airflow_world.ps1`；注释 `AirflowWorldCheck.cs:10-11` | 部分待核实 |
| ArmoryInteractionRenderCheck.cs | 渲染交互（静态） | K 渲染 | 通过生产界面绘制接收模拟点击，所有交换只发生在临时人物与剑魂上；反射 `SoulUI` 私有字段；测量真实详情内容末尾再调窗口高度，让被裁剪的成长按钮恰好在固定操作按钮后 | `run_information_ui_render.ps1` 的 `ZENITH_ARMORY_INTERACTION_GPU` | 部分待核实 |
| ArmoryUiRenderCheck.cs | 渲染（静态） | K 渲染 | 在独立临时剑魂上调用真实绘制与按钮；归位与汇流共用同一事件年龄（先到剑位再进核心）；另留核心流光单独固定时间样本核对弯曲拖尾与加速吸入；开局与满支援两份独立样本避免绘制时伪造标签 | `InformationUiRenderCheck.cs:64` 调用 | 部分待核实 |
| BossUnlockUiRenderCheck.cs | 渲染（静态） | K 渲染 | 在独立剑魂上覆盖六组封印及关键边界，只跑本次修改对应的界面渲染 | `InformationUiRenderCheck.cs:87` 的 `ZENITH_BOSS_UNLOCKS_GPU` | 部分待核实 |
| ClearGlassRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 清透玻璃 GPU 检查，写 `clear-glass.json`；含 `ZENITH_CLEAR_GLASS_BASELINE` 与内嵌 `ZENITH_GLASS_HIGHLIGHT_PROBE` 分支 | `run_clear_glass.ps1`、`compare_clear_glass.py` | 部分待核实 |
| ClearHighlightRenderCheck.cs | 渲染（静态） | K 渲染 | 高光像素统计，写 `clear-highlight.json` | 与 `ClearGlassRenderCheck` 配对 | 部分待核实 |
| EnvironmentRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 环境属性视觉，写 `environment-gpu.json` / `environment-gpu-error.txt` | `ZENITH_ENV_RENDER` | 部分待核实 |
| InformationUiRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 信息/配剑/剑库界面实拍主入口：`ZENITH_INFORMATION_GPU`，内部分派 `ZENITH_ARMORY_INTERACTION_GPU`、`ZENITH_BOSS_UNLOCKS_GPU` 三条子路径，各写 `gpu.json`，异常写 `failure.txt` 并 `Exit(1)` | 调用 `ArmoryInteractionRenderCheck`/`ArmoryUiRenderCheck`/`BossUnlockUiRenderCheck` | 部分待核实 |
| LiquidGlassPerfCheck.cs | GPU 性能 ModSystem | K 渲染 | 液态玻璃性能采集，写 `liquid-performance.json`；唯一的**性能类**夹具 | `run_liquid_glass_perf.ps1`、`compile_liquid_glass_perf.py` | 部分待核实 |
| LiquidGlassRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 液态玻璃外观主检查，写 `liquid-glass.json` | `run_liquid_glass.ps1` | 部分待核实 |
| LiquidGlassTrailRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 液态玻璃拖尾：先导 `trail-mesh.json`（逐顶点），再写 `liquid-trail.json` | 依赖 `LiquidGlassTrailMesh`、`LiquidGlassTrailGeometryCheck` | 部分待核实 |
| OreStackRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 矿石叠层亮度/提亮渲染，写 `gpu.json`，异常写 `failure.txt` 并 `Exit(1)` | `ZENITH_ORE_STACK_GPU`；对应 `OreStackCheck` 的服务端断言 | 部分待核实 |
| PureCodeVfxRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 纯代码特效（无贴图资产）渲染检查，写 `pure-code.json` | `run_pure_code_vfx.ps1`、`prepare_pure_code_package.py` | 部分待核实 |
| RefractionRenderCheck.cs | 渲染（静态） | K 渲染 | 相同背景/网格/透明度下比较折射率，禁止用颜色或透明度变化冒充折射；输出 `Result` 含 IOR 差、锐/粗高光像素、Ior/Roughness/Transmission | `SpaceWarpRegionCheck` 同族；`check_glass_*` 系列 | 部分待核实 |
| SoulFlightWorldCheck.cs | 实机世界夹具 | K 渲染 | 最大型实机夹具：在隔离世界驱动剑魂飞行。开关矩阵含 `ZENITH_WORLD_LIGHT_PRESET`（day 等）、`ZENITH_SURFACE_WORLD`、`ZENITH_SOUL_INPUT_CHECK`（真实输入）、`ZENITH_REQUIRE_INPUT_ARM`、`ZENITH_ECHO_DAMAGE_CHECK`、`ZENITH_WORLD_MOVING` | `run_soul_flight_world.ps1`、`pick_soul_flight_frames.py` | 部分待核实 |
| SoulTrailRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 剑魂拖尾实拍，写 `gpu.json` | `run_soul_trail_render.ps1` | 部分待核实 |
| SwordBodyMaskPreview.cs | GPU 预览 ModSystem | K 渲染 | 剑体遮罩预览/导出：读 `ZENITH_SWORDBODY_PREVIEW/ATLAS/AUTHORING/REVISION/COLORFIELD/LOCAL/EMISSIVE/ITEM`，产出 `profile.json` 与预览图；`REVISION` 默认 R1 | `SwordBodyVectorMask`、`register_swordbody_vector.py`、`pack_swordbody_mask.py` | 部分待核实 |
| SwordHighlightRenderCheck.cs | 渲染（静态） | K 渲染 | `Run(Func<string,int,int,Color[]> render)` 高光渲染对照，返回 `Result` | 被 `SwordSpaceDistortionRenderCheck` 同族调用 | 部分待核实 |
| SwordSpaceDistortionRenderCheck.cs | 渲染（静态） | K 渲染 | `Run(output, coverage, width, height)` 空间扭曲渲染对照 | 配对 `SwordSpaceDistortionCheck` 的几何断言；`run_surge_preview.ps1` | 部分待核实 |
| SwordTrailHeadJoinRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 剑尾头部接合实拍，写 `head-join.json` | `run_sword_head_join.ps1`；配对 `SwordTrailInnerContourCheck` | 部分待核实 |
| TexturedSwordMaterialRenderCheck.cs | GPU 渲染 ModSystem | K 渲染 | 贴图剑材质合成实拍，写 `textured-material.json`（缩进格式） | `run_textured_sword_material.ps1` | 部分待核实 |
| VfxStripeAblationCheck.cs | GPU 渲染 ModSystem | K 渲染 | 条纹消融（stripe ablation）对照，写 `ablation.json` | `run_vfx_stripe_ablation.ps1`、`verify_own_effect.py` | 部分待核实 |
| MaskCompositionPreview.cs | GPU 预览 ModSystem | K 渲染 | 贴图候选枚举 + 合成预览：先写 `assets.json`，`ZENITH_MASK_ASSETS_ONLY=1` 时只枚举；`ZENITH_MASK_SOURCE` 默认 `Flames`；再写 `preview.json` | `run_mask_preview.ps1`、`prepare_material_maps.py` | 部分待核实 |
| GlassBloomRenderCheck.cs | 渲染（静态） | K 渲染 | 玻璃泛光（bloom）阈值/扩散对照，返回结果对象 | `prepare_glass_reflection_lowpass.py` | 部分待核实 |
| GlassBufferReadback.cs | 缓冲读回工具 | L 工具 | 把 `Texture2D` 读成 `Color[]`，支持 `SurfaceFormat.HalfVector4`（Half → Vector4 → Color）与普通路径。注释（`:6`）明确"原始线性数据只用于检查，不能当色调审看图" | 被 `LiquidGlassRenderCheck`、`RefractionRenderCheck` 等消费 | 部分待核实 |
| GlassHighlightProbe.cs | 探针 | L 工具 | 高光像素探针，写 `highlight-probe.json` | `ZENITH_GLASS_HIGHLIGHT_PROBE`（`ClearGlassRenderCheck.cs:58` 内嵌调用） | 部分待核实 |
| LiquidGlassGpuTimer.cs | GPU 计时工具 | L 工具 | `IDisposable`，通过 P/Invoke 读 FNA 的 D3D11 设备做时间戳查询测显卡命令；不改图形状态、不释放借来的设备/上下文 | 独有消费方 `LiquidGlassPerfCheck` | 部分待核实 |
| LiquidGlassTrailMesh.cs | 网格结构 | L 工具 | 定义拖尾顶点结构与 `VertexDeclaration`（含自定义 `Vertex` 结构），供渲染与几何校验共用 | 被 `LiquidGlassTrailRenderCheck`、`LiquidGlassTrailGeometryCheck` 消费 | 部分待核实 |
| PreviewColorNoise.cs | 数学噪声工具 | L 工具 | 纯数学 hash 噪声（128×128、8 格、`374761393`/`668265263`/`1274126177` 常量），无外部图片、不装入生产模组 | `prepare_material_maps.py`、`generate_noise.py` | 部分待核实 |
| MoonWorldProbe.cs | 存档探针 | L 探针 | 直接读原始世界文件：`new WorldFileData(input,false)` + `BinaryReader` + 反射取静态字段，产 `ZENITH_MOON_REPORT`；异常写 `FAIL: + error` 并 `Exit(1)` | `run_*` 月相相关脚本；只读不写入 | 部分待核实 |
| PrefixProbe.cs | 无头探针 | L 探针 | `-server` 下精确报告是哪个 gate 挡住了剑魂的 `PrefixID.Legendary`，写 `ZENITH_PREFIX_REPORT` | 与 `Check.cs:228` `CanApplyPrefix(Legendary)` 断言配对 | 部分待核实 |
| WorldRefractionProbe.cs | 世界折射探针 | L 探针 | 在实机世界里采剑魂飞行时的原生折射数据，写 `native-probe.json` 到 `ZENITH_SOUL_FLIGHT_WORLD` | 与 `SoulFlightWorldCheck` 共用输出目录 | 部分待核实 |
| SwordAirflowMask.cs | 遮罩生成器 | L 工具 | H3 动态 Alpha 气流遮罩：`IDisposable`、128×256、复用原版贴图与 SpriteBatch；按轮廓与流向编排的**近似**（注释 `:5-6` 明确不冒充完整流体模拟），含 `distance/upper/lower/solid` 场与 `loop/length/viscosity` 参数 | 被 `AirflowAtlasBatchCheck`/`AirflowMaterialCheck` 消费；`emit_vfx_mask_data.py` | 部分待核实 |
| SwordBodyVectorMask.cs | 遮罩生成器 | L 工具 | 向量化剑体遮罩，产 `softblade-diag.txt` 与 `field-diag.txt`（尺寸/solid 计数/maxDist 诊断） | 配对 `SwordBodyMaskPreview`；`register_swordbody_vector.py` | 部分待核实 |
| SwordColorNoiseCheck.cs | 静态断言套件 | M 数学 | 底色噪声 4 条：同参数可重复生成、只提供不透明灰度不覆盖逐剑颜色与形状透明度、有可见明暗变化、慢底色两轴循环接缝连续 | `PreviewColorNoise` 的验收；`repoint_colorfields.py` | 已验证 |
| SwordFlowMeshBasisCheck.cs | 静态断言套件 | M 数学 | 网格基 6 条：连续朝向不移动顶点/不改 UV/不改逐顶点覆盖度、共享顶点的全部三角形副本朝向完全一致、连续朝向单位化且无非法数值、观察矩阵旋转正确传到屏幕空间朝向、空间重合但 UV 不同的表面不被错误焊接、退化尾尖用稳定中性朝向不除零 | `SwordFlow` 网格；`SwordFlowMeshBasis` 生产侧 | 已验证 |
| SwordFlowPaletteCheck.cs | 静态断言套件 | M 数学 | 调色板 4 条：剑色明暗锚点有限且在有效色域、暗部/中间调/高光感知亮度依次递增、有色剑三锚点都保留色彩而非叠白、不同剑色生成各自明暗阶 | `SwordFlowPalette` 生产侧；`check_sword_color_delivery.py` | 已验证 |
| SwordSpaceDistortionCheck.cs | 静态断言套件 | M 数学 | 空间扭曲几何 6 条：位移有界且小于影响半径、非法时钟/相机输入不产偏移、相机原点与时间相位显式传递且不改强度、屏幕缩放经逆变换还原世界相位坐标、奇异观察矩阵安全关闭扭曲、独立开关且受纯代码屏幕输入限制 | 配对 `SwordSpaceDistortionRenderCheck`、`SpaceWarpRegionCheck` | 已验证 |
| SwordTrailInnerContourCheck.cs | 静态断言套件 | M 数学 | 内缘轮廓 3 条：头到旧尾连续变窄不形成先缩后鼓的多余扇面、头部保留刀身连接长度（不撑到整把武器外）、内缘曲线由几何统一负责不再用接合权重覆盖材质边界 | 配对 `SwordTrailHeadJoinRenderCheck` | 已验证 |
| SpaceWarpRegionCheck.cs | 几何校验（静态） | M 数学 | 用积分图（前缀和）算覆盖邻域，做**保守边界**验收：`ChangedPixels`/`OutsideBladeChangedPixels`/`BeyondReachChangedPixels`/`Difference`。注释（`:5`）明确不复刻着色器里的采样方向与压力公式 | 配对 `SwordSpaceDistortionCheck`；`run_surge_preview.ps1` | 已验证 |
| LiquidGlassTrailGeometryCheck.cs | 几何校验（静态） | M 数学 | 旋转 + 等比缩放后连续 UV 梯度应随坐标作逆尺度变换；两组用例 `(.75, .43)` 与 `(1.5, -.61)`；输出 `Scale/Rotation/VertexCount/MaxAbsoluteError/DegenerateTriangles/UnrepresentedVertices/Passed` | 配对 `LiquidGlassTrailMesh`、`LiquidGlassTrailRenderCheck` | 已验证 |

> 行数核对：94 行（A=6, B=13, C=4, D=3, E=6, F=2, G=4, H=2, I=8, J=3, K=26, L=10, M=7）。

---

## 第三部分：覆盖矩阵

| 被测功能 | 覆盖夹具文件 | 断言要点 | 是否含图形/多人（否=服务端不可及） |
|---|---|---|---|
| **出剑与命中（时钟/节奏）** | SwingCheck、StageOneCheck、DamageBalanceCheck | 复刻原版 `ItemCheck`：0/10/20 出剑、30 帧复用、一次机会只出 1 把、跨 10 宿主边界、空节点留空、仅 owner 造剑 | 否（纯服务端可跑；画面另见 SoulFlightWorldCheck）／多人：仅 `SwingCheck` 断言"仅 owner 客户端造剑"这一条逻辑，**无真实双端** |
| **出剑与命中（轨迹/姿态）** | Check.cs:278-290、OreCheck、RibbonCheck | 10 把天顶剑轨迹向外、剑尖朝向误差 <0.05 rad、发射后 `CanDamage()`、真实命中穿透多敌且保速度、同剑同帧不能二次命中 | 否 |
| **伤害与免疫** | DamageBalanceCheck、EchoDamageCheck、OreCheck、OreRewardCheck、HardOreCheck、BlessingCheck | 逐剑伤害/暴击/击退不共享、65% 初始强度、150% 飞行基、防御阈值、最低 1 伤不可乘、破甲 15 穿透、爆发根免疫在普通免疫被清后仍有效、同目标冷却 | 否 |
| **诅咒·第一组（机械）** | CurseCheck、Check.cs:233-276 | 双双子只算 1 封印、三组机械逆转为祝福（1.2x）、真实 `ModifyHitByProjectile`+`OnKill` 记账、机械魂核掉落 | 否 |
| **诅咒·第二组（切换/主剑）** | CurseCheck | 切换过滤所有特性源到首个有效主剑、旧主剑退役不删宿主且淡出无吸收、副剑减益需切换组 Boss | 否 |
| **诅咒·第三组（回归/屏障）** | CurseCheck | 第一回归组 Boss 允许旧波继续、屏障挖 2 格（诅咒）/4 格（弱化）、超额度截停、平台不挡剑 | 否 |
| **诅咒·第四组（独行/预算）** | CurseCheck | 独行预算 2/3/常态4/祝福4、真实手势发射对应预算、祝福间隔可超 3 至 10 | 否 |
| **诅咒·第五组（切换四主例外）** | CurseCheck、OreRewardCheck、OreStackCheck | 切换把矿主限到 4、同帧 10 主只出 4 把、第五把同类型不能绕限、前四矿材例外不放开铜锡普通特性 | 否 |
| **诅咒·第六组（封印/遗留）** | CurseCheck、BossUnlockUiCheck、ProgressionCheck | 封印与显式遗留豁免持久不伪造击杀、旧魂不给 Brain/Eater 二义位、单双子的部分完成、15 个挑战目标头像 | 否（头像资源只做存在性/帧数，不做视觉审美）／多人：无 |
| **祝福（御剑）** | BlessingCheck、BlessingCameraCheck、BlessingGuiFixture | 15 秒充能 / 10 秒激活 / 满表不流失 / 过存档克隆重铸网络 / 距离 1→1.5 / 相机平滑 / 破甲 / 回充 / 中继 10% 上限 50% | 部分（`BlessingCameraCheck` 触及 `InitTargets`；`BlessingGuiFixture` 只遥测渲染计数）／多人：无 |
| **槽位与容量** | Check.cs:173-263、UiCheck、WoodCheck、OreCheck、SupportIntegrationCheck、DomainCheck | 空魂 1 槽、`Cores=63` 开 10 槽、Boss 旗标不替代魂核、10 主槽一圆轨、森林奖励左上弧、支援右下弧、17 槽不重叠、第 6 支援用新存储、11 共享支援上限 | 否 |
| **木质族·计数与阈值** | WoodCheck、WoodV2Check、Check.cs | 8 把原版木剑、`mask 0xFF` 才算满套、阈值表、缺任一木即失满套、满木给 5 支援不给奖励槽 | 否 |
| **木质族·羁绊门槛（三条锻造链）** | WoodV2Check | Forge（4 木+棕榈+铜/铁宽剑，缺铁断链，3 木不足第四阶）、Rot（6 木含红木/乌木+村正/永夜，缺红木断链）、Ember（6 木含红木/灰烬+炽焰巨剑，缺剑断链） | 否 |
| **木质族·皇冠** | WoodCheck、WoodV2Check、Check.cs | 皇冠覆盖 3.00/+100、泰拉刃必须在皇冠槽外、皇冠槽内泰拉刃不能自解、皇冠把层上限 6→12、失皇冠夹回 6 | 否 |
| **矿石族·材质与计数** | OreCheck、SupportIntegrationCheck、TraitCheck | 短/宽剑算 1 材质、14 材质全收、连续寄宿不占额外槽、精通后仍需真实寄宿 | 否 |
| **矿石族·阶与倍率** | OreRewardCheck、OreCheck、OreStackCheck | 1/2/4/6/8/10 阶、150% 基 + 材质缩放 + 十阶加成、五阶不能六阶回攻、七阶不泄漏八阶、十阶伤害与移速双 +50% | 否 |
| **矿石族·成阵与叠层** | OreStackCheck | 铜 = 每源 +1 飞剑、锡 = 层提亮、4 层齐射 = 基础×1.15 非复乘、14 层 +65% 封顶、新旧波伤害隔离 | 否 |
| **困难模式矿·六机制** | HardOreCheck、OreRewardCheck | 钴 3 击 40% 刃、山铜三花瓣共享 30%、秘银标记跨源消费、精金防御阈 12%、钛碎片 30% 绕源、钯金 8 点滚动跨 5 通道 | 否 |
| **飞刃族·蓄能与齐射** | FlyingBladeCheck | 31 把名册、18 帧叠 1 把、独行上限待命、齐射掷全部、相邻 5°、直飞 600 像素后旋转 45 帧、收回期不伤 | 否 |
| **飞刃族·取代本体** | FlyingBladeCheck | 族激活时本体飞剑完全不发射、挥臂只掷飞刃、无蓄能时本次不取代、未激活时照常发本体剑 | 否 |
| **神圣/星辰族** | HolyHeavenCheck | 名册边界、有效主槽/支援激活、落星 1000 伤从上方坠、同魂冷却、冲击不重复伤直击目标但伤范围内其他敌 | 否 |
| **活武（器灵）族** | LivingArmamentCheck、LivingMeleeMatrix | 与神圣/矿石/木同时生效、每有效槽一召唤体、重复更新不重复召唤、种子弯刀/星怒走原版 ItemCheck、飞刀代理玩家后恢复物品/位置/鼠标 | 部分（`LivingMeleeMatrix` 需要 `Main.GameViewMatrix`，本质仍是服务端）／多人：无 |
| **环境属性·元数据** | EnvironmentAttributeCheck | 21 个唯一稳定定义、E01-E21 往返、非法输入不部分替换、过存档/克隆/前缀/网络且不扩载荷 | 否 |
| **环境属性·战斗机制** | EnvironmentCombatCheck | 21 环境加法 420%、森林 15% 只作用一次不递归、领域 +50% 只给对应环境、溢出暴击转伤害系数、5 解锁槽、v8 载荷 | 否 |
| **UI·布局与点击映射** | UiCheck、ArmoryUiCheck、InformationUiCheck、Check.cs:186-203 | 4 种 UIScale（1/1.25/1.6296296/2）下槽点击插入/取回/拒绝、前缀保留、命中框不重叠、短视口改并排 | 否 |
| **UI·输入屏蔽** | UiInputCheck、WindowDragCheck | 屏蔽先于原版背包、真实底层槽不能取/装/分、关闭不泄漏按住点击、拖拽夹窗保所有权、光标武器不起拖拽 | 否 |
| **UI·图鉴与特性摘要** | TraitCheck、BossUnlockUiCheck | 图鉴 8 木 + 6 矿全在、唯一 key、生效筛选、3 行紧凑 tooltip、去木隐藏效果但保链接、浏览不改进度 | 否 |
| **UI·全屏配阵** | DomainCheck | 剑库只读合格武器、三区/窄屏切换、原子交换回原背包位、锁定槽拒收、满包拒绝起飞、难度按钮改真实 `WorldGen` 标志并二次点击还原 | 否（世界标志断言在服务端成立） |
| **存读档迁移** | Check.cs:208-214、RemovedSystemsCheck、StageOneCheck、WoodCheck、OreCheck、EnvironmentCombatCheck、SupportIntegrationCheck | `SaveData/LoadData` 深克隆、10 槽觉醒持久、旧武技三字节尾、V2 数组缺失迁移为空、旧扩展 8 槽回收、环境 v8 载荷、6 个额外支援槽不截断 | 否 |
| **网络序列化** | Check.cs:211-214、CurseCheck、OreRewardCheck、HardOreCheck、SupportIntegrationCheck、EnvironmentCombatCheck、LunarCheck | `NetSend/NetReceive` 精确消费、诅咒记录不丢、源序号、8 帧间隔、v8 环境、GUID 隔离 | 否（真机多人：无） |
| **HERO / 重铸兼容** | Check.cs:215-249、CurseCheck、WoodCheck、OreCheck、HardOreCheck、EnvironmentCombatCheck、SupportIntegrationCheck、StageOneCheck | `PreReforge`→`ResetPrefix`→`Prefix(-2)`→`PostReforge` 保宿主/进度/收藏/环境；`netDefaults`+`Prefix(Legendary)` 重建（HERO 路径）保宿主、`Kills`、`Cores`、克隆 | 否 |
| **传奇位** | LegendaryCheck、WoodV2Check、FollowupCheck、DomainCheck | 仅传奇模式可见、不被当支援槽寻址、计入活跃主、过存档/网络、退出后暂停态可见可取回但拒绝插入、槽 34 被序号链跳过、木剑第 8 把经传奇位达 8/8 | 否 |
| **Boss 解锁与魂核** | BossUnlockUiCheck、ProgressionCheck、CurseCheck | 亮暗取个人铭刻任意顺序、单双子部分完成、眼击杀给精确缺核、肉墙等血肉核、邪教徒复用神庙核、丢核可从已得胜利重建 | 否 |
| **月总事件恢复** | LunarCheck | 四柱真实击杀恢复 60 秒、活柱阻恢复、GUID 隔离、客户端不能起权威倒计时、到期真造月总核心 NPC | 否（多人仅断言"客户端不能起"这一条） |
| **已移除系统回归** | RemovedSystemsCheck、WoodV2Check、VanillaReturnCheck | 旧系统不再注册、无自定义木剑类、`WoodKind` 无 Fey、旧武技字段可读不参战、三字节尾格式保留、自定义吸收已移除 | 否 |
| **纯代码数学（网格/调色/扭曲）** | SwordFlowMeshBasisCheck、SwordFlowPaletteCheck、SwordSpaceDistortionCheck、SwordTrailInnerContourCheck、SwordColorNoiseCheck、SpaceWarpRegionCheck、LiquidGlassTrailGeometryCheck | 顶点不随朝向移动、退化尾尖不除零、亮度单调、有色剑不叠白、位移有界 < 半径、奇异矩阵安全、旋转缩放后 UV 逆尺度 | 否 |
| **视觉/渲染实拍** | Airflow*、ClearGlass*、EnvironmentRender、InformationUiRender、LiquidGlass*、OreStackRender、PureCodeVfx、Refraction、SoulFlightWorld、SoulTrailRender、SwordBodyMask、SwordHighlight、SwordSpaceDistortionRender、SwordTrailHeadJoin、TexturedSwordMaterial、VfxStripeAblation、MaskComposition、GlassBloom、Migration79、Baseline79 | 图片/JSON 像素统计，**无布尔断言**，靠人眼或 Python 脚本判读 | **是**（必须有 GPU 与窗口；`Main.dedServ` 下全部跳过） |

### 完全没有夹具覆盖的功能

以下项在 94 个文件里**找不到任何断言，也找不到任何采集代码**：

1. **真实多人（≥2 客户端 + 专用服）**。全库只有 2 处提到 `NetmodeID.MultiplayerClient`：`DomainCheck.cs`（"客户端不能静默改多人世界"）与 `LunarCheck.cs`（"客户端不能起权威倒计时"）。两者都是**在单进程里把 `netMode` 改成客户端后跑一段本地逻辑**，不是真的连服务器。`CurseCheck`/`OreRewardCheck`/`HardOreCheck` 里的 `NetSend/NetReceive` 也是内存 `MemoryStream` 往返。每份 `PASS` 报告都自认 "no live multiplayer validation"。
2. **性能/帧时间（除液态玻璃）**。唯一性能夹具是 `LiquidGlassPerfCheck`（写 `liquid-performance.json`）与 `LiquidGlassGpuTimer`。剑魂主循环、`ItemCheck`、矿石齐射、100 把飞刃同屏、UI 每帧重排——**均无 FPS/分配量/GC 断言**。
3. **实机画面的人工判读**。26 个渲染夹具只产 JSON 与 png，判定权在仓库外的 `checks/*.py` 与"人看"。所以"这特效好不好看/符不符合原版观感"零自动覆盖。
4. **音效**。全目录 `grep -i "sound\|audio\|SoundEngine"` 无命中。
5. **手柄 / 触屏 / 高 DPI 之外的输入设备**。所有 UI 夹具都用 `Main.mouseX/Y` + `mouseLeft`/`mouseLeftRelease` 合成，`UiInputCheck` 也只测鼠标路径。
6. **本地化完整性**。只有 `Check.cs:172` 一条断言（`DawnCore.DisplayName == "初光魂芯"`）。`Localization/` 目录下其余 100+ 键的值正确性、缺键回退、英文侧一致性——无覆盖。
7. **多人下的入侵/权限/反作弊**。`DomainCheck` 只测"客户端不能改世界标志"这一条语义，服务端侧校验与 RPC 鉴权无覆盖。
8. **跨平台**（Linux/macOS 专用服）。全部夹具依赖 `E:/SteamLibrary/...` 硬编码路径与 Windows 风格 `Main.SavePath` 判断（`CombatFixture.cs:19`），非 Windows 环境不可用。

---

## 第四部分：夹具编写模式与陷阱

### 4.1 典型写法

**造一个可攻击的 `SoulItem`**（`CurseCheck.cs:22-30` 是标准模板）：

```csharp
foreach(var p in Main.projectile)p.active=false;   // 先清场
foreach(var n in Main.npc)n.active=false;
Main.myPlayer=0; Main.netMode=NetmodeID.SinglePlayer;
var p1=new Player{whoAmI=0,active=true,position=new(1000,1000)};
Main.player[0]=p1; p1.ResetEffects();
var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
s.Attached=true; s.Kills=SoulProgress.All; s.Cores=63;   // 满进度
for(int i=0;i<hosts;i++) s.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
p1.inventory[0]=s.Item; s.RefreshStats();              // 必须 RefreshStats 才生效
```
三个必做动作：`new Item(type).ModItem` 取实例、写 `Hosts`/`Kills`/`Cores`、**最后必须 `RefreshStats()`**。少最后一步，`UseSpeedMultiplier`/`Capacity`/`Awakened` 全是旧值。

**改 `Main.myPlayer` / `netMode`**：`Main.myPlayer=0` + `Main.netMode=NetmodeID.SinglePlayer` 是几乎所有套件的公共前置（`CurseCheck.cs:25`、`FlyingBladeCheck`、`OreStackCheck`）。`LivingMeleeMatrix.cs:12-16` 还额外重置 `Main.rand=new UnifiedRandom(318)`、`Main.GameViewMatrix`、`Main.screenPosition/Width/Height`、`Main.mouseX/Y`——凡是要走"真实玩家近战流程"的夹具都必须这样对齐屏幕坐标，否则 `PlayerInput` 的鼠标世界坐标算错。

**注入时钟**：不用 `Thread.Sleep`，全部手工推帧。两种模式：
- 直接调 AI：`for(int tick=0;tick<15;tick++) blade.AI();`（`Check.cs:284`）
- 手推 ModPlayer 时钟：`HardOrePlayer.Clock` 由套件自己递增（`HardOreCheck.cs` 断言串里反复出现 `clock=`）

**注入瞄准**：写 `blade.TargetOffset = new Vector2(160,20)`（`Check.cs:283`），不通过玩家鼠标。UI 夹具则反过来写 `Main.mouseX/Y = SoulUI.SlotBounds(0).Center`（`Check.cs:192-193`），且注释（`Check.cs:189-190`）说明必须传**界面空间**坐标，因为 `GameInterfaceLayer.Draw` 之前已调用 `PlayerInput.SetZoom_UI`。

**还原全局状态**：`Check.cs:184-185` 与 `:204` 是范本——改 `Main.UIScale` / `mouseItem` / `mouseLeft` 前后各置一次默认值。`Check.cs:307` 测完交叉召唤把 `WorldGen.crimson` 改回 `false`。`EnvironmentAttributeCheck` 最后一条断言专门用来"还原原矿环境绑定"。**失败的套件没有 `finally`，中途抛异常时全局状态会带毒**，这是设计上的已知弱点（靠 `Environment.Exit` 兜底，进程结束即清理）。

**时间预算与退出**：所有 `Check.cs` 分支末尾 `Environment.Exit(0)`；`InformationUiRenderCheck.cs:134` 异常时 `Exit(1)`。这意味着**一个进程只跑一条流水线**，无法在一次启动里跑多个 `ZENITH_*`。

### 4.2 陷阱（12 条，全部来自代码实证）

1. **`Seals` 未解除 → `TraitItems` 只剩首把**。`CurseFixture.NeutralDefaults` 默认为 `false`，即"新魂带全新六诅咒"。旧套件（WoodCheck/OreCheck/SwingCheck…）如果不先把它置 `true`，它们拿到的 `SoulItem` 是**被诅咒的**，`TraitItems` 只会返回第一个有效主剑，所有"多把剑都生效"的断言会集体失败。`Check.cs:127` 在 `ZENITH_WOOD` 分支第一行就置 `true`，`:150` 在调 `CurseCheck` 前置回 `false`——**顺序不能颠倒**。

2. **支援槽需要木族或矿族开启，不存在"裸支援"**。`WoodCheck`："满木给 5 支援槽（无矿石精通时不给奖励槽）"；`SupportIntegrationCheck`："非木装配开通用支援阈"要靠矿支援。若夹具只塞主剑不塞木/矿，`Supports[]` 全部 `IsAir`，任何"支援提供材质"的断言都会以"0 材质"失败——而失败信息只会说材质数不对，不会提示你忘了开槽。

3. **手动调 `AI()` 不积分位置，必须自己加 `position`**。`Check.cs:284` 的 `blade.AI()` 循环里，`p.Center` 不会自动沿速度推进（AI 只写 `velocity` 与 `ai[]`，位置推进在 `Projectile.Update` 里）。所以 `Check.cs:285` 断言 `p.Center.X > player.Center.X` 实际上依赖 `Projectile.NewProjectile` 时的初始位置已经偏右，**不是** AI 跑出来的位移。同理 `OreCheck` 里"原生 `Update` 飞行"与"手动 `AI`"是两套完全不同的验证强度，混用会得到假阳性。

4. **服务端不渲染 → 视觉零覆盖**。`Main.dedServ` 为真时所有 `*RenderCheck` 直接 return，`SoulFlightWorldCheck`/`CombatFixture`/`UiFixture` 的 `Enabled` 判断里都带 `!Main.dedServ`。所以 `-server` 跑出的 `PASS` **完全不能证明画面正确**，每个报告结尾都写了 "no graphical rendering/client validation"。把渲染类断言当作"通过"是误读。

5. **`ZenithReborn.dll` 是 `HintPath` 引用的构建产物**。`csproj` 指向 `../../ZenithReborn/bin/Release/net8.0/ZenithReborn.dll`。改完主模组源码不重新 `-build`，夹具会**静默通过旧逻辑**。`run_wood_checks.ps1:29-33` 强制先重建两个模组，绕开这一点靠的是脚本纪律，不是工程约束。

6. **`Main.UIScale` 污染会跨断言传播**。`Check.cs:187` 的 `foreach(float scale in ...)` 循环体每轮改 `Main.UIScale` 与 `Main.screenWidth/Height`，`:204` 才统一复位。若循环中途某个断言抛异常，`Main.UIScale` 停在 2f，**后续所有 UI 断言（含其他套件）坐标全错**，报错信息却指向错误的槽位。写新 UI 夹具时应在 `try` 里各自复位。

7. **`Cores` 与 `Kills` 是两个独立门**。`Check.cs:258-260` 明确断言："Boss 旗标单独不能替代魂核"（`Kills=All, Cores=0` → `Capacity==1`）、"`Cores=63` 全进度开 10 槽"。只写 `Kills` 不写 `Cores` 是常见错误，表现是"槽位永远是 1 个"。

8. **`RefreshStats()` 不调 = 一切派生值陈旧**。`Hosts`/`Supports`/`Crown` 任何数组写入后必须 `RefreshStats()`，否则 `Capacity`/`Awakened`/`UseSpeedMultiplier` 全是上一次的值。`Check.cs:217`、`:226`、`:263` 每处写完都紧跟一次。

9. **`Prefix(-2)` 是"随机前缀"不是"清除"**。`Check.cs:219-221` 的重铸流程是 `resetPrefix` → `Prefix(-2)` → `PostReforge`，用 `-2` 触发真实随机重铸路径。想清前缀必须用 `ResetPrefix()`。`Check.cs:229` 则用 `Prefix(PrefixID.Legendary)` 直接设值。

10. **`WorldGen.crimson` 是全局世界标志，必须还原**。`Check.cs:293-307` 交替设 `false`/`true` 测世界邪恶类型，末尾 `:307` 复位。忘了复位会让后续任何 `WorldGen.crimson` 相关逻辑（如 `CurseCheck` 的 Brain/Eater 区分）跑错分支，且**报错点在很远的别处**。

11. **`ModItem` 实例是"单例挂载在 Item 上"的**。`(SoulItem)new Item(item.type).ModItem` 每次新建 `Item` 得到**新** `SoulItem`（`Check.cs:176` 用这个特性断言 `SoulId` 独立）。但 `item.Clone()` 得到的是**深克隆后的新实例**（`Check.cs:206` 验证 `copy.Hosts[0].TurnToAir()` 不影响原 `soul`）。混用"同一 `Item` 的 `ModItem`"与"新 `Item` 的 `ModItem`"会让 `SoulId` 断言随机失败。

12. **GPU 夹具的产物是 JSON，不是 pass/fail**。`LiquidGlassRenderCheck`、`InformationUiRenderCheck`、`AirflowAtlasBatchCheck` 等只 `File.WriteAllText` 一个 json/png 就结束，**没有 `check(...)` 也没有退出码语义**（只有异常时才 `Exit(1)`）。`run_*.ps1` 里靠 `$LASTEXITCODE` 判断"跑没跑通"，不判断"画得对不对"——判读由 `checks/*.py` 与人工完成。把"脚本 exit 0"理解为"渲染正确"是最容易犯的错。

---

## 交付摘要

1. **覆盖文件数：94/94（100%）**。但目录实际只有 **94 个 `.cs`**，不是任务书说的 100。差 6 个；`Assets/`、`Localization/` 子目录内确认无 `.cs`。若确需 100，需确认是否把 `checks/archived-material-fixtures`、`archived-oneoff`、`archived-removed-systems` 三个归档目录算入（本次未纳入）。

2. **分组结果（94）**：
   - A 调度与夹具基础设施 **6**
   - B 剑魂核心：出剑/命中/时序/尾迹 **13**
   - C 诅咒与祝福 **4**
   - D 族·木质 **3**
   - E 族·矿石（含困难模式）**6**
   - F 族·飞刃 **2**
   - G 族·神圣/星辰/活武 **4**
   - H 环境属性 **2**
   - I UI 与交互 **8**
   - J 存档迁移与回归基线 **3**
   - K GPU 渲染/实机画面 **26**
   - L 探针/只测不判/几何工具 **10**
   - M 纯代码数学断言 **7**

3. **完全没覆盖（8 项）**：真实多人（≥2 客户端）、性能与帧时间（除液态玻璃）、实机画面人工判读、音效、手柄/触屏、本地化完整性（仅 1 条断言）、多人权限与 RPC 鉴权、跨平台。

4. **调度入口**：**`Check.cs:11` `class Check : ModSystem`**。注册机制是**环境变量路由**——`Check.cs:34` 读 `ZENITH_CHECK_REPORT` 决定是否跑，`Check.cs:38-170` 14 个 `if(Environment.GetEnvironmentVariable("ZENITH_*")=="1")` 分支各自硬编码套件清单并 `Environment.Exit(0)`。服务端走 `Check.cs:26 PostAddRecipes()`，客户端走 `Check.cs:19 PostUpdateEverything()`。另有 **49 个文件不经过它**，各自在 `ModSystem` 钩子里读自己的 `ZENITH_*_RENDER/GPU/GUI` 变量，由 `checks/run_*.ps1` 驱动。`ZENITH_WOOD`（`Check.cs:125`）是事实上的总回归入口，串联 29 个套件。

5. **未确认项**：
   - 任务书"100 个文件"与实测 94 的差额来源（是否含三个 `archived-*` 目录）。
   - K 组 26 个渲染夹具的**具体判定阈值**未逐行读（只读了 env 变量与输出文件名），标为 `部分待核实`；其 pass/fail 判定实际发生在仓库外的 `checks/*.py`，不在本次 csharp 范围内。
   - `Assets/Baselines`、`Assets/LiquidGlass` 下的 json/png 数量与 `BossUnlockUiCheck` 断言的"15 个挑战目标头像资源"是否一一对应，未逐一核对。
   - `LiquidGlassPerfCheck` 是否真的有通过阈值（还是只记录数字）未确认。

---

## 第五部分：归档夹具（`checks/archived-*`，19 个 `.cs`）

### 5.1 三个目录的实际构成

| 目录 | `.cs` | 其它文件 | 合计 | 版本子目录 |
|---|---|---|---|---|
| `checks/archived-material-fixtures/` | **6** | 0 | 6 | `0.3.89/`（5 个）、`0.3.96/`（1 个） |
| `checks/archived-removed-systems/` | **13** | 0 | 13 | `0.3.123/`（13 个） |
| `checks/archived-oneoff/` | **0** | **20 个 `.py`** | 20 | 无（扁平） |
| **合计** | **19** | **20** | **39** | |

**`archived-oneoff/` 没有任何 `.cs`**，实际是 20 个 Python 取证/生成脚本，全部扁平放在目录下、无子目录：

`extract_calamity_slash.py`、`localize_effect_comments.py`、`make_airflow_preview.py`、`make_archived_vfx_comparison.py`、`make_baseline79_night_evidence.py`、`make_clear_highlight_evidence.py`、`make_gif_stdlib.py`、`make_glass_evidence.py`、`make_hybrid_evidence.py`、`make_metal_colorfields.py`、`make_ore_preview.py`、`make_slash_preview.py`、`make_soul_input_evidence.py`、`make_sword_color_evidence.py`、`make_sword_spark.py`、`make_texture_ablation_evidence.py`、`make_unit_trail_evidence.py`、`make_visible_color_evidence.py`、`make_zenith_maps.py`、`make_zone_mask.py`

命名规律：14 个 `make_*_evidence.py` / `make_*_preview.py`（截图与取证图生成）、3 个 `make_*`（mask/colorfield/gif 工具）、1 个 `extract_`（从 Calamity 抄剑弧）、1 个 `localize_`（批量改注释语言）、1 个 `make_zenith_maps.py`。**没有一个是测试断言**，它们只产 png/json 供人看。

三个目录**均无** `.csproj`、`build.txt`、`enabled.json`、`.json`、`.tmod`（`find -name "*.csproj" -o -name "build.txt" -o -name "*.json" -o -name "*.tmod"` 零命中）。归档方式是**按模组版本号切开**（0.3.89 / 0.3.96 / 0.3.123），不是按功能。

### 5.2 是否参与编译 / 执行

**结论：既不参与编译，也不参与执行。**

- **不编译**：`checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj` 是 SDK 风格工程（`Microsoft.NET.Sdk` + `tMLMod.targets`），默认 glob 只收**工程目录自身**下的 `**/*.cs`。三个归档目录是 `ZenithPrototypeCheck/` 的**同级兄弟目录**，不在 glob 范围内，且**自身没有 csproj**，因此不会被任何 `dotnet build` / `tMLMod.targets` 收编。`build.txt` 的 `buildIgnore = bin/*, obj/*, *.csproj` 也没有提及它们（因为压根扫不到）。
- **不执行**：对 `checks/run_*.ps1`（50 个）与 `checks/*.py`（30 个）做全文检索 `archived-material-fixtures|archived-removed-systems|archived-oneoff`，**零命中**。没有任何启动脚本会拉起它们。
- **但有一处"按名字"的反向引用**：`ZenithPrototypeCheck/RemovedSystemsCheck.cs:17` 用字符串数组
  `{"MartialPlayer","MartialBlade","MartialGiant","MartialWave","MartialHUD","MartialConfig","MartialFreezeNPC","MartialFreezeProjectile","SpiritFamily"}`
  断言这些类型**不再注册**。这是归档目录里的类名唯一被"用到"的地方，但只是**字面量名字**，不引用类型、不 require 编译，**删除归档目录不会破坏该断言**。反之，这条断言正是归档存在的意义：它把"哪些东西被删了"固化成了回归。

也就是说：这三个目录是**纯考古材料**，编译器看不见、运行时不加载，唯一价值是给人看"删之前长什么样"。

### 5.3 归档文件索引（19 行）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| checks/archived-material-fixtures/0.3.89/DayNightMaterialRenderCheck.cs | 历史归档夹具 | 材质·昼夜 | 固定输入对照"原始 80"：白天必须逐像素一致，暗处只降表面反光、不改折射数据与形状 | `HybridMaterialRenderCheck` 同批；`check_color79.py` | 已扫描 |
| checks/archived-material-fixtures/0.3.89/GlassResponseRenderCheck.cs | 历史归档夹具 | 材质·玻璃响应 | 固定几何/色场时间/覆盖度，只换光照与环境方向，验证高光响应的是材质而非坐标白线 | `check_glass_visibility_delivery.py` | 已扫描 |
| checks/archived-material-fixtures/0.3.89/HybridMaterialRenderCheck.cs | 历史归档夹具 | 材质·混合 | 唯一带 `ModSystem` 的一个：在专用菜单夹具内替换自有 `Effect`，逐像素比较 R3 基底与融合版的光学数据 | `run_hybrid_gpu.ps1`、`check_hybrid_preservation.py` | 已扫描 |
| checks/archived-material-fixtures/0.3.89/SwordPaletteRenderCheck.cs | 历史归档夹具 | 材质·调色 | 同一网格/时间/光向下换真实剑体身份，读生产输出色，避免只比较"有无像素差异" | 现役 `SwordFlowPaletteCheck` 的前身 | 已扫描 |
| checks/archived-material-fixtures/0.3.89/TextureAblationRenderCheck.cs | 历史归档夹具 | 材质·纹理消融 | 隔离菜单临时替换输入，用原始 80 着色器逐项定位纹理伪影，退出前恢复全部资源 | 现役 `VfxStripeAblationCheck` 的前身 | 已扫描 |
| checks/archived-material-fixtures/0.3.96/SwordTrailHeadJoinCheck.cs | 历史归档夹具 | 剑尾·头部接合 | **本组唯一含断言的**（3 条）：头部深度须准确覆盖当前刀身末端、接合只扩展刀身侧而真实剑尖不动、头部之外不继续施加接合权重 | 现役 `SwordTrailInnerContourCheck`、`SwordTrailHeadJoinRenderCheck` 的前身 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialActionVisuals.cs | 历史归档夹具 | 武技·演出 | 编排动作伴随的雷光、蓄力流线与角色流光；不画屏幕遮罩、不改现有材质 | `MartialBlades.MartialVisual` 配对 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialArts.cs | 历史归档夹具 | 武技·核心 | 武技系统主干：`MartialConfig : ModConfig`、`MartialTuning`、`MartialInput`、`MartialPlayer : ModPlayer`、`MartialTime`（只冻结战斗范围敌人，不停世界时钟/友军/输入） | 被 `RemovedSystemsCheck` 按名字断言"未注册" | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialBlades.cs | 历史归档夹具 | 武技·弹幕 | `MartialVisual` + 三个 `ModProjectile`：`MartialBlade`（弹出旋转/突刺/嵌入/击飞/返航）、`MartialGiant`（雷临蓄力/起势/化光突进/顿挫/斩击/消散）、`MartialWave` | `MartialMotion` 提供共用曲线 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialCheck.cs | 历史归档夹具 | 武技·断言 | 武技断言套件 **49 条**：空充能拒手动激活、原生剑伤真实充能武技、同刃帧伤不重复充能、派生波不递归充能、满充能不自动激活 | 现役对应物散落在 `BlessingCheck`（不自动激活）与 `OreRewardCheck`（不递归充能） | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialData.cs | 历史归档夹具 | 武技·数据 | 武技数据表（无 `ModSystem`、无 hook、无断言），为 `MartialBlades`/`MartialTuning` 提供常量与索引 | 被 `MartialCheck`、`MartialBlades` 消费 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialGuiFixture.cs | 历史归档夹具 | 武技·实机夹具 | `ModSystem`：只跑隔离副本世界，检查点可暂停查看，自动模式正常退出、不碰正式存档 | 与现役 `CombatFixture`/`UiFixture` 同构 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialMotion.cs | 历史归档夹具 | 武技·运动 | `MartialMotion` 动作曲线与真实弹幕共用（预览不得另写更夸张的一套）；`MartialDashPlayer : ModPlayer` 仅在真正突进的 12 帧隐藏人物绘制层 | `MartialBlades` 消费同一曲线 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialMotionRenderCheck.cs | 历史归档夹具 | 武技·GPU 回放 | `ModSystem`：隔离菜单内调真实弹幕 AI 与 `PreDraw` 做动作 GPU 回放；十字标记玩家中心、方框标记真实 NPC 碰撞框；不操作鼠标键盘 | `run_martial_motion_render.ps1` | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialSlashCheck.cs | 历史归档夹具 | 武技·剑弧断言 | 剑弧历史断言 **7 条**：固定客户端视觉预算、传送断带不画整屏、非有限样本丢弃、跨角度回绕保持连续、残影按淡出窗完全过期、取消/返航清旧带、不连续旋转起新带 | 语义与现役 `RibbonCheck`/`FlowParticleCheck` 高度重叠 | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialSlashRenderCheck.cs | 历史归档夹具 | 武技·GPU 回放 | `ModSystem`：隔离菜单客户端回放当时原版流光绘制，不操作桌面、世界或正式存档 | 配对 `MartialSlashVisuals` | 已扫描 |
| checks/archived-removed-systems/0.3.123/MartialSlashVisuals.cs | 历史归档夹具 | 武技·剑弧绘制 | `MartialSlashRenderer : ModSystem`：只保存近期可见剑姿，**不参与**弹幕生成/伤害/碰撞/存档/网络/攻击计时；实体前绘制一次让剑身压在剑弧上，切 shader 与混合后必须恢复 | 现役 `MaterialTrailCheck` 的前身语义 | 已扫描 |
| checks/archived-removed-systems/0.3.123/SpiritFamily.cs | 历史归档夹具 | 剑灵族·元数据 | 剑灵族类型元数据：`Scope` 只记 R2 开发计划、**不给** `CanHost` 权限；无 hook、计时器、玩家状态、收藏进度、支援授予或战斗效果 | 现役 `HolyHeavenCheck`/`FlyingBladeCheck` 的族模型前身 | 已扫描 |
| checks/archived-removed-systems/0.3.123/SpiritFamilyCheck.cs | 历史归档夹具 | 剑灵族·断言 | 剑灵族断言 **16 条**：任一武器不得同时属于两个 spirit 成员身份、已发布成员资格不可被消费者改写 | 现役无等价"跨族互斥"断言 | 已扫描 |

> 行数核对：6 + 13 = **19 行**，与三个目录的实际 `.cs` 数一致。`archived-oneoff/` 的 20 个 `.py` 不建行（非夹具）。

### 5.4 仍可能被引用的历史断言（5 条，供判断能否删除）

以下按"现役夹具是否已有等价覆盖"排序，**从最不该删到最该删**：

1. **`SpiritFamilyCheck.cs` — "No weapon type belongs to two spirit member identities" + "Published membership cannot be mutated by consumers"**（16 条中的 2 条）
   现役 `HolyHeavenCheck`、`FlyingBladeCheck`、`LivingArmamentCheck` 各自只测**本族内**的名册边界与计数，**没有任何一处断言"一把武器不能同时属于两个族"**。这是全仓库唯一的跨族互斥保证。若要删，先确认 `SoulItem.CanHost` / 族判定已经结构上不可能重叠。

2. **`SwordTrailHeadJoinCheck.cs` — 3 条头部接合断言**（`archived-material-fixtures/0.3.96`）
   现役 `SwordTrailInnerContourCheck.cs` 只测"头部到旧尾连续变窄"、"头部保留刀身连接长度"、"内缘曲线由几何统一负责"。**没有覆盖**"头部深度准确覆盖刀身末端"与"头部之外不继续施加接合权重"这两条。它们对应 `SwordTrailHeadJoinRenderCheck` 的几何前提，删了会让渲染侧失去断言支撑。

3. **`MartialSlashCheck.cs` — "Teleport breaks the slash trail instead of drawing a screen-spanning ribbon" + "Non-finite slash samples are discarded"**（7 条中的 2 条）
   现役 `RibbonCheck` 有等价的 `"Teleport discontinuity cannot draw a screen-wide ribbon"` 与 `"Invalid rotation cannot reach the GPU"`，`MaterialTrailCheck` 也有 `"Material trail rejects teleport discontinuity"` 与 `"Invalid animation time cannot reach GPU geometry"`。**语义已被完全覆盖**，可删。

4. **`MartialCheck.cs` — "Full charge is retained without automatic activation"**
   现役 `BlessingCheck` 的 `"Fifteen seconds of valid combat fills without automatic activation"` 与 `"A full unspent meter survives twenty seconds out of combat"` 覆盖同一语义（充能不自动触发、满表不流失）。可删。

5. **`MartialCheck.cs` — "Derived wave cannot recursively generate 武技 charge"**
   现役 `OreRewardCheck` 的 `"Material children cannot recursively charge another flying-sword formation"`、`HardOreCheck` 的 `"Child hit does not charge the next Cobalt proc"`、`EnvironmentCombatCheck` 的 `"Actual child damage cannot recursively spawn environment attacks"` 已在三条不同通路上覆盖"子代不得递归充能/递归生成"。可删。

**结论**：真正"只有归档才有"的是第 1、2 条（跨族互斥、头部深度接合）。其余 3 条为**已被现役套件取代**的死断言。三个归档目录可整体删除而不影响任何构建或 `run_*.ps1`，唯一代价是失去第 1、2 条的独占覆盖。
