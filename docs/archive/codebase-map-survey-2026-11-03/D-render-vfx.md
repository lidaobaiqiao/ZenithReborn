# D —— 渲染 / 特效 / 着色器 / 资产 建图报告

调查范围：`ZenithReborn/` 根目录 32 个渲染相关 `.cs`、`Assets/**`、`Localization/*.hjson`、`build.txt`、`ZenithReborn.csproj`。
工作区状态：存在未提交改动（木质族 V2 等），本报告以**当前工作区文件内容**为准。
所有行号均为当前文件行号。

---

## 第一部分：模块级说明

### 组 1　特效总闸与调试入口

**职责一句话**：所有渲染分支的开关、通用 GPU 状态包装、F7~F10 逐项热键调试，集中在这里；改"某个特效出不来/关不掉"第一步永远看本组。

**核心文件**
- `ZenithReborn/VfxIsolation.cs`（244 行，纯静态开关表）
- `ZenithReborn/VfxPartDebug.cs`（116 行，热键面板）
- `ZenithReborn/VfxRenderState.cs`（32 行，绘制状态保存/恢复）

**绘制入口与注入点**
- 无自身绘制。`VfxPartDebug.PostUpdateEverything`（`VfxPartDebug.cs:23`）只读键、写 `VfxIsolation` 字段、`Main.NewText` 回报。
- `VfxRenderState.DrawWithRestoredState`（`VfxRenderState.cs:9`）是**其他所有手写 Device 绘制**的统一外壳：保存 Blend/Depth/Raster/Scissor/0~2 号 sampler 与 texture/顶点缓冲/索引缓冲，进入时置 `BlendState.Additive`，退出时 `Main.pixelShader.CurrentTechnique.Passes[0].Apply()` 并还原。

**渲染管线顺序**
- 本组不参与绘制顺序，只被调用：`SoulVisuals.PostDrawTiles`（`SoulVisuals.cs:65`）、`ViscousSlash.DrawHook`（`ViscousSlash.cs:94`）、`SoulSceneRefraction.PrepareNormals` 的三段循环（`SoulSceneRefraction.cs:87/93/110`）。
- 注意 `DrawWithRestoredState` 只保存 **0~2 号**纹理槽，而 `SurfaceMaterialRenderer.Draw` 与 `SwordFlowMaterial.Unload` 用的是 16 槽；混用时有槽位遗漏风险（见"未确认项"）。

**关联模块**：被 B/C/D/E/F 全部组读取；`VfxPartDebug` 的分组计数直接依赖 `VfxIsolation.Branches/Details/Parts/GlassParts` 四个数组长度（`VfxPartDebug.cs:17-21`），增删数组项会自动反映到 `[n/61]` 显示。

**常见问题定位入口**
| 症状 | 先查 |
|---|---|
| 某特效完全不出 | ①对应 `VfxIsolation.Xxx` 是否为 `false`；②`PureCodeOnly`（`VfxIsolation.cs:12`）是否被改成 `true`（会把 12 项外部输入分支与全部贴图加载一起关掉）；③按 F10 是否误触"全关" |
| 某特效关不掉 | 检查该特效是否**根本不在 `Branches` 里**——`ViscousSlash` 曾被写死类内 `Enabled=true` 绕过总闸（`VfxIsolation.cs:33-36` 注释记录了这个历史）；`Parts/GlassParts` 目前**对画面无效**（见下） |
| 太亮 | 先看 `PureCodeOnly` 分支统计口径（`VfxRenderState` 进 Additive），再查各分支的 alpha 常量（如 `FlyingBladeVisuals.cs:35-45`） |
| 太暗 | ①加法混合下 alpha 被乘两次的经典坑（`FlyingBladeVisuals.cs:94-102` 注释）；②`GlassMaterialPreset.Opacity` |
| 错位/残留 | `VfxRenderState` 状态未还原（表现为后续原版绘制花屏）；`SoulTrailEnding`/`ViscousSlash` 切世界未清空 |

**修改影响提醒**
- 改 `PureCodeOnly` = 同时关掉：`EchoBlades/TipGlow/FlowSpark/GlassFilter`、`Tip*/Echo*/Spark*/FlyingDust` 全部外部输入项、`SwordFlowMaterial.Load` 整段贴图加载（`SwordFlowMaterial.cs:57`）、`VfxSwordMask.Load` 图集登记（`VfxSwordMask.cs:29`）、`SwordSpark`/`SoulFlowSpark` 贴图路径回退 MagicPixel、`SoulSceneRefraction` 滤镜激活。**影响面覆盖全模组所有非纯几何特效**。
- 改 `SetAllBranches` 会同时改 10 个分支；`ViscousSlash` 故意不参与（`VfxIsolation.cs:45-48` 只调 `SetAllBranches`）。
- **重要事实（本次核实）**：`Parts`（P01~P26）与 `GlassParts`（B7-1~B7-7）是**只改字符串、不改画面**的空开关。证据：
  1. `VfxIsolation.PartValue(string)`（`VfxIsolation.cs:141`）在全代码库**零调用**；
  2. `ZRSwordFlow.fx` 全文**没有** `uPartShape`/`uPart*` 任一 uniform（声明的 36 个参数见 `ZRSwordFlow.fx:3-78`）；
  3. `ZRGlassScreen.fx` 只有 6 个 float uniform（`ZRGlassScreen.fx:5-10`），**没有** `uGlassPart*`；唯一出现 `uGlassPartSpaceWarp` 的地方是未被任何 `.fx` include 的 `ZRSwordSpaceWarp.fxh:2`；
  4. `ZRSwordFlow.fxc` / `ZRGlassScreen.fxc` / `ZRMaterialSurface.fxc` 中 `strings | grep uPart` 计数均为 0。
  因此"P01 主体形状（关掉整条带面消失）"这类标称**当前不成立**，按 F9 只会改面板文字。

---

### 组 2　飞剑拖尾与剑体

**职责一句话**：把 `SoulBlade` 记录的剑身历史（60 帧 × 4 个单元）铺成带面三角形，交给 `ZRSwordFlow` 着色器；剑体本身与三枚残影由 `SoulBlade.PreDraw` 直接贴图绘制。

**核心文件**
- `SoulMaterialTrail.cs`（150 行）：带面几何 + UV + 顶点色，`Draw` 转发到 `SwordFlowMaterial.Draw`
- `SoulTrailUnits.cs`（23 行）：本体 + 3 枚残影的窗口定义（`Count=4`、`Spacing=15`）
- `SoulTrailMotion.cs`（21 行）：残影随玩家位移平移
- `SoulTrailEnding.cs`（99 行）：弹幕销毁后的独立"余迹"片
- `SoulVisuals.cs`（77 行）：取色表 + 世界层绘制入口
- `SoulRibbonMesh.cs`（90 行）：旧版 4 段带面几何（仅回归校验用）
- `SwordFlightTrail.cs`（26 行）：矿石飞剑的 8 点短历史
- `SwordBodyColorNoise.cs`（47 行）：剑身慢底色程序噪声
- `VfxSwordMask.cs`（145 行）+ `VfxMaskData.cs`（39 行）：矿石剑身"色场 × 蒙版"离屏合成

**绘制入口与注入点**
- `SoulVisuals.PostDrawTiles()`（`SoulVisuals.cs:54`）——**世界层主入口**：物块画完、实体画之前。内部 `VfxRenderState.DrawWithRestoredState` → 遍历 `SoulBlade.DrawRibbonShader` + `SoulTrailEnding.DrawAll`（`SoulVisuals.cs:65-71`）。
  - 有提前返回：`if(SoulSceneRefraction.CompositingReady) return;`（`SoulVisuals.cs:58`）——滤镜已接管时不重复画。
- `SoulBlade.PreDraw`（`SoulBlade.cs:485`）：三枚残影（`VfxIsolation.EchoBlades`，`SoulBlade.cs:501`）→ 剑体贴图（`SoulBlade.cs:516`）→ 剑尖星芒（`SoulBlade.cs:518`）。
- `VfxSwordMask.PrepareFrame` 挂在 `Main.OnPreDraw`（注册 `VfxSwordMask.cs:35`，实现 `:54`）：每个 `OreFlyingSword`/`OrePierceFeedback` 的残留剑，把 16 帧图集两帧插值 + 两层滚动噪声合成到 640×256 的 RenderTarget。
- 消费端：`OreFlyingSword.cs:251-255`、`OreWeaponEnding.cs:56-57` 用 `Main.spriteBatch.Draw` 画 `VfxSwordMask.Draw`。

**渲染管线顺序**
1. `OnPreDraw(VfxSwordMask.PrepareFrame)` → 读 `Assets/Vfx/SwordBody/*.png` 图集 + `SwordBodyColorNoise.Create` 生成的 512² 噪声 → 写 `targets[itemType]`（每剑一张 640×256 RT）。
2. `PostDrawTiles` → `SoulMaterialTrail.BuildLayered` 生成顶点 → `SwordFlowMaterial.Draw` → `ZRSwordFlow`。
3. `SoulBlade.PreDraw` 的残影/剑体用原版 `Main.EntitySpriteDraw`（不走自定义 Effect），所以**残影会盖在带面之上**。
4. 剑身蒙版（`VfxSwordMask`）与带面（`ZRSwordFlow`）是两条互不相干的路径：前者属矿石飞剑 `OreFlyingSword`，后者属 `SoulBlade`；只有 `SoulBlade` 有带面。

**关联**：取色来自 `SoulVisuals.Tint`（`SoulVisuals.cs:39`，木质 8 色 → 原版剑表 → 稳定 FNV 哈希回退）；实体类 `SoulBlade`/`OreFlyingSword`/`OrePierceFeedback` 不在本组范围但耦合最紧。

**常见问题定位**
| 症状 | 先查 |
|---|---|
| 带面不出现 | `VfxIsolation.SwordTrail`；`SoulBlade.DrawRibbonShader` 的 `trailCount<2` 门槛（`SoulBlade.cs:460`）；`SoulTrailUnits.At` 的 `Samples` 是否算成 0 |
| 带面变灰膜/没透光 | `SoulSceneRefraction.Scene` 恒为 `null`（`Capture()` 全代码库无调用）→ `uHasScene=0`，世界内不折射，折射只在屏幕滤镜合成阶段发生 |
| 残影残留 | `SoulTrailEnding` 的 256 容量与代际淘汰（`SoulTrailEnding.cs:64-65`）；`ViscousSlash` 的 `OnWorldUnload` 清空 |
| 带面错位/拉丝 | `SoulMaterialTrail.BuildStrip` 的 300px 跳变保护（`SoulMaterialTrail.cs:81`）；`VfxRenderState` 未还原 |
| 剑身蒙版不变色 | `VfxMaskData.Rows` 21 行是否覆盖该 `ItemID`；`VfxSwordMask.Has` 的 `disabled` 集合（异常时自动加黑名单，`VfxSwordMask.cs:117`） |

**修改影响提醒**：`SoulMaterialTrail.Draw` 的另一层身份是"所有带面的唯一出口"——改它的 `opacity`/`dissolve` 参数会同时影响主拖尾与消散片（消散片走同一函数，`SoulTrailEnding.cs:93`）。

---

### 组 3　屏幕折射与玻璃材质

**职责一句话**：把带面法线/表面写进两张屏幕尺寸 RT，再由原版滤镜 `ZenithReborn:SwordGlass` 读取当前帧背景做折射、模糊与涂层合成。

**核心文件**
- `SoulSceneRefraction.cs`（205 行）：滤镜注册、RT 管理、`PrepareNormals`、`GlassScreenData`
- `GlassMaterialPreset.cs`（19 行）：`checks/emit_glass_preset.py` 生成的常量（Ior 1.5 / Thickness 72 / Dispersion .35 / Ssr .6 / Emission .55 / SpaceWarp 10,36,1.3）
- `SwordSpaceDistortion.cs`（34 行）：空间扭曲参数入口（**当前无人调用**）

**绘制入口与注入点**
- `SoulSceneRefraction.Load`（`:41`）`+= On_FilterManager.BeginCapture`，并在主线程注册 `Filters.Scene["ZenithReborn:SwordGlass"]`（`:47`，`EffectPriority.VeryHigh`）。
- 激活/停用：`PostUpdateEverything`（`:51-58`）——条件是 `!PureCodeOnly && GlassFilter && !gameMenu` 且（有消散片 或 存在任一 `SoulBlade`）。
- 真正绘制：`BeginWorldCapture`（`:59`）→ `PrepareNormals`（`:64`）在**原版抓屏之前**把带面画进 `normalMask` / `surfaceMask`（+ 可选 `materialProperties` / `materialTransmission`）；随后滤镜 `Apply`（`GlassScreenData.Apply` `:132`）在合成阶段采样。

**渲染管线顺序（关键）**
```
PostUpdateEverything → 决定滤镜 Activate/Deactivate
OnPreDraw → (BlessingCamera.PrepareTargets, VfxSwordMask.PrepareFrame)
Draw 世界 → 物块 → SoulVisuals.PostDrawTiles（若 CompositingReady 则跳过）
            → 实体/弹幕 → Dust → On_Main.DrawDust(ViscousSlash)
On_FilterManager.BeginCapture
   ├─ PrepareNormals: RT normalMask  ← SwordTrail + TrailEnding
   │                  RT surfaceMask ← 同上
   │                  (可选) RT materialProperties / materialTransmission ← 仅当 HasSurfaceMaterials
   └─ orig(BeginCapture) 抓当前帧
Filter.Apply → ZRGlassScreen 读 screen/normal/surface(+material) 合成
```
- RT 清单：`normalMask`、`surfaceMask`、`materialProperties`、`materialTransmission`（均 `PreserveContents`，屏幕尺寸）；`snapshot` + `Capture()` 是**未启用的安全快照退路**。
- 安全保障：`PrepareNormals` 前若检测到任何非 `PreserveContents` 的绑定目标直接 `DeferredFrames++` 跳过（`:68`）；状态全量保存/还原（`:71-124`）。

**关联**：`SwordFlowMaterial.Draw` 通过 `uOutputRefractionMask` / `uOutputSurface` / `MaterialOutputMode` 得知"现在是在给滤镜烤图"，`MaterialOutputMode>=3` 时直接 `return`（`SwordFlowMaterial.cs:246`），避免把材质输出写进法线通道。`SoulVisuals` 用 `CompositingReady`（`:32`）避免双重绘制。

**常见问题定位**
| 症状 | 先查 |
|---|---|
| 折射整块不生效 | 滤镜是否 Active（`SoulSceneRefraction.cs:56`）；`maskReady` 是否被 `DeferredFrames` 卡住（`:68-69`）；`SoulTrailEnding.HasActive` 与 `SoulBlade` 是否存在 |
| 折射错位/跟随镜头漂移 | `uSceneUvTransform` 来自 `UvTransform`，而 `UvTransform` 只在 `Capture()` 里赋值 → **恒为 (0,0,1,1)**，窗口缩放/视口偏移时会有偏差 |
| 玻璃太亮/太暗 | `GlassMaterialPreset` 常量（生成文件，改要改 `checks/emit_glass_preset.py` 再重新生成）；`uMaskReady` 为 0 时 coverage=0 |
| 空间扭曲完全没效果 | `SwordSpaceDistortion.Apply` 无调用者，且 `ZRSwordSpaceWarp.fxh` 未被任何 `.fx` include |

**修改影响提醒**：`GlassFilter` 关闭会同时让 `PrepareNormals` 不再执行 → 省下每帧 2~4 次全屏 RT 清屏与整条带面重绘，是最大的性能开关。

---

### 组 4　表面材质管线（当前处于休眠态）

**职责一句话**：数据驱动的 PBR 材质系统（40+ 参数 / 4 层细节 / 10 张贴图），把"每把剑一份 JSON 材质"喂给 `ZRMaterialSurface.fx`；**当前因配置为空而未被生产路径触发**。

**核心文件**
- `SwordFlowMaterial.cs`（319 行）：`Artwork` 图集绑定、贴图/原始数据加载、`Draw` 分流（有材质走 `SurfaceMaterialRenderer`，否则走常量路径 `ZRSwordFlow`）、`PostUpdateEverything` 世界光
- `SurfaceMaterialDefinition.cs`（159 行）：数据合同 + `Control[]` 调参元数据 + `Validate`
- `SurfaceMaterialRenderer.cs`（192 行）：`ZRMaterialSurface` 的唯一绘制者，含 UV→切线基重建、高度图 CPU 位移
- `MaterialInheritance.cs`（97 行）：`"base": "路径"` 递归继承，未知字段忽略（前向兼容）
- `MaterialQuality.cs`（41 行）：按同屏飞剑数 8/16 自动降档（`ThicknessScale` / `DetailScale` / `UseEnvironmentMaps`）
- `SwordFlowMeshBasis.cs`（74 行）：跨三角形 UV 梯度并查集（**当前无调用者**）
- `SwordFlowPalette.cs`（41 行）：剑色 → 暗/中/亮三阶（消色差走冷钢色阶）

**绘制入口与注入点**
- `SwordFlowMaterial.Draw`（`:223`）是唯一入口，由 `SoulMaterialTrail.Draw`（`:22-24`）转发；分流逻辑在 `:238-245`。
- `SwordFlowMaterial.Load`（`:52`）读 `Assets/Vfx/Materials/SwordFlow/bindings.json`；`Main.QueueMainThreadAction` 内上传 `ZoneMask.zrdata`（`:88-94`）。
- `PostUpdateEverything`（`:302`）为带材质的 `SoulBlade` 加世界光。

**渲染管线顺序**：`SoulMaterialTrail.Draw` → `SwordFlowMaterial.Draw` → 若 `material!=null` → `SurfaceMaterialRenderer.Draw` → `ZRMaterialSurface`（`BlendState.AlphaBlend`，16 纹理槽保存/还原）；否则 → 常量路径 `ZRSwordFlow`（`BlendState.AlphaBlend`，只保存 3 槽）。

**关联**：`SoulSceneRefraction`（`Scene`/`MaterialOutputMode`/`BuildingMask`/`BuildingSurface`）、`MaterialQuality`（`ThicknessScale` 也作用于常量路径 `SwordFlowMaterial.cs:252`）、`GlassMaterialPreset`（`DefaultOptics`）、`SoulBlade.ai[0]` → `itemType` → `ItemLoader.GetItem(itemType).FullName` 作为材质字典键。

**常见问题定位**
| 症状 | 先查 |
|---|---|
| 改了材质 json 没反应 | `bindings.json` 的 `weapons` 是否是空的（**当前就是空对象 `{}`**，见组 7），空则 `surfaceMaterials` 为空 → 永远走常量路径 |
| 材质校验刷警告 | `ValidateLoaded`（`SwordFlowMaterial.cs:163-187`）：参数越界 / 贴图缺失 / 继承断链 |
| 质感一阵好一阵坏 | `MaterialQuality.Current` 阈值（`MaterialQuality.cs:12-13`），降档时 `DegradedFor` 缓存键是 `(材质, 档位)` |
| 某把剑特别暗 | `SwordFlowPalette.From` 的消色差分支（`SwordFlowPalette.cs:22-23`）会把低饱和色拉向冷钢 |
| 高度图位移方向错 | `SurfaceMaterialRenderer` 的 `across` 基（`:104`）由 UV 反解得到，UV 退化时回落 `UnitY` |

**修改影响提醒**：`bindings.json` 一旦填入 `weapons`，`HasSurfaceMaterials` 立即变 true → `SoulSceneRefraction` 会多清两张全屏 RT、`SwordFlowMaterial.Draw` 全体改走 `ZRMaterialSurface`、`PostUpdateEverything` 开始加光。这是"一条配置改三处行为"的耦合点。

---

### 组 5　剑尖星芒、火花与飞行粒子

**职责一句话**：`SoulBlade` 飞行期在剑尖叠两笔十字星芒（复用原版 `SharpTears`），并发射飞行 dust 与两处光照；另有小型自发光 dust 类。

**核心文件**：`SoulZenithEffects.cs`（71 行）、`SwordSpark.cs`（34 行）、`SoulFlowParticles.cs`（123 行，含 `SoulFlowStyle` 配色与 `SlashSparkEmitter`）

**绘制入口与注入点**
- `SoulZenithEffects.DrawTip`（`:44`）由 `SoulBlade.PreDraw` 直接调用（`SoulBlade.cs:518`），**在剑体格之后**，用 `Main.EntitySpriteDraw`（不换批次）。
- `SoulZenithEffects.EmitFlying`（`:25`）由 `SoulBlade.AI` 调（`SoulBlade.cs:257`），只发 dust + 光，不绘制。
- `SoulFlowSpark.PreDraw`（`SoulFlowParticles.cs:122`）/`EvapCircle.PreDraw`（`EvapCircle.cs:61`）/`SwordSpark.Update`（`SwordSpark.cs:26`）走原版 dust 绘制阶段。

**渲染管线顺序**：星芒在实体绘制批次内（`Main.DrawProjectiles` 的批次）；dust 在 `Main.DrawDust` 批次；两者都在 `SoulVisuals.PostDrawTiles` 之后。

**关联**：`FinalFractalHelper.FinalFractalProfile`（原版天顶剑星芒参数，`SoulZenithEffects.cs:11-17`）决定星芒长度与颜色；`SoulBlade.SpriteVisibility` 决定包络。

**常见问题定位**
| 症状 | 先查 |
|---|---|
| 星芒不出现 | `SoulZenithEffects.Flare(phase)` 在 15~45 之外为 0（`:43`）；`VfxIsolation.TipGlow/TipHalo/TipCore` |
| 星芒贴剑心而非剑尖 | `VfxIsolation.TipOffset`（`:55`） |
| 星芒全程常亮不脉动 | `VfxIsolation.TipPulse`（`:48`） |
| 飞行期没有 dust | `VfxIsolation.FlyingParticles`（B5 总闸，`SoulBlade.cs:257`）→ 再叠 `FlyingDust && !PureCodeOnly`（`SoulZenithEffects.cs:33`） |
| 剑身亮但玩家不亮 | `FlyingBladeLight` 与 `FlyingPlayerLight` 是两条独立光照（`:38-39`） |

**修改影响提醒**：`SoulZenithEffects.Profile` 同时服务星芒、飞行 dust 与光照取色，改它三处一起变。

---

### 组 6　粒子与消散（含飞刃特效）

**职责一句话**：命中/消散类视觉——飞刃拖尾环影光点光环、粘性空腔残影、蒸发小圆、碎光喷发。

**核心文件**
- `FlyingBladeVisuals.cs`（328 行）：`FlyingBladeShot` 的纯绘制层，自带 4 条队列（拖尾 6 段 / 环影 15 段 / 光环 12 帧 / 火花计时）
- `ViscousSlash.cs`（128 行）：`On_Main.DrawDust` 挂载的残影扫掠（默认关闭）
- `EvapCircle.cs`（62 行）：借原版 Cloud 粉尘帧的蒸发圆（仅被 `ViscousSlash` 调用）
- `SoulFlowParticles.cs`：`SoulFlowSpark` dust + `SlashSparkEmitter`（**发射器当前无人使用**）

**绘制入口与注入点**
- `FlyingBladeVisuals.Draw`（`:150`）由 `FlyingBladeShot.PreDraw` 调（`FlyingBladeShot.cs:106/113`）。**它在 `PreDraw` 内部 `Main.spriteBatch.End()/Begin()` 切到加法混合再切回**（`:169-182`），这是已知技术债（注释 `:166-168` 明说彻底解法是挪到 `On_Main.DrawDust`）。
- `ViscousSlash.DrawHook`（`On_Main.DrawDust`，注册 `:30`，实现 `:86`）：先 `orig(self)` 再画，`VfxRenderState.DrawWithRestoredState` 内切加法批次。

**渲染管线顺序**：飞刃特效压在刀刃之下（先画特效再画贴图，`:164-185`）；粘性残影在 dust 之后（`orig` 之后）。

**关联**：`SoulVisuals.Tint(hostType)` → 再向冷白混 45%（`FlyingBladeVisuals.cs:300`）；`FlyingBladeShot.Stage`（Fly/Rotate/Return）驱动队列切换。

**常见问题定位**
| 症状 | 先查 |
|---|---|
| 旋转期啥都没有 | `echoOn` 单独判断（`:161`），因为旋转期 `trailCount` 被清零；`VfxIsolation.FlyingBladeTrail` |
| 旁人看不到命中光环 | **已知取舍**：`OnHitNPC` 只在 owner 端跑（`:140-141` 注释） |
| 亮背景下看不见 | 加法混合下 alpha 被乘两次 → 统一 `Glow()` 顶回 255（`:94-102`） |
| 切世界后残留剑影 | `ViscousSlash.OnWorldUnload/OnWorldLoad`（`:34-35`）；`VfxSwordMask.OnWorldUnload`（`:38`） |
| 粘性残影关不掉 | 只能改 `VfxIsolation.ViscousSlash`（`ViscousSlash.cs:40/89`），它**不在 F7~F10 的 61 项清单里** |

**修改影响提醒**：`ViscousSlash` 是唯一在 `On_Main.DrawDust` 里 End/Begin 批次的模块，改动它要连同 `orig(self)` 的调用顺序一起考虑。

---

### 组 7　着色器与资产

**职责一句话**：3 个 `.fx`（编译产物 `.fxc` 同目录）+ 4 个 `.fxh`（**当前全是孤儿**）+ 一份手工维护的 `build-manifest.json`。

**核心文件**：`Assets/Effects/{ZRSwordFlow,ZRGlassScreen,ZRMaterialSurface}.fx`、对应 `.fxc`、`ZRClearGlass.fxh`、`ZRLiquidGlass.fxh`、`ZRSwordHighlights.fxh`、`ZRSwordSpaceWarp.fxh`、`build-manifest.json`

**绘制入口与注入点**：无（被 C/D 组 `ModContent.Request<Effect>(..., ImmediateLoad).Value.Clone()` 加载，克隆后各持一份参数表）：
- `SwordFlowMaterial.cs:49` → `ZenithReborn/Assets/Effects/ZRSwordFlow`
- `SoulSceneRefraction.cs:45` → `.../ZRGlassScreen`
- `SurfaceMaterialRenderer.cs:50` → `.../ZRMaterialSurface`

**渲染管线顺序**：见组 3/4 的 pass 顺序（`.fx` 各只有一个 technique 一个 pass：`ZRSwordFlow.fx:350`、`ZRGlassScreen.fx:78`、`ZRMaterialSurface.fx:202`）。

**关联**：`build.txt` 的 `buildIgnore` 只排除 `*.fxh`，`.fx` 与 `.fxc` 都会进包；但 `.fxh` 也**没有被任何 `.fx` include**（全库 `grep include` 命中为 0），所以排除它们对运行时无影响。

**常见问题定位**：改 shader 后画面没变 → 确认改的是 `.fx`（tModLoader 编译 `.fx`），`.fxc` 只是对照产物；uniform 名拼错在 `EffectParameterCollection` 上是**静默 null**（`SwordFlowMaterial` 对部分参数用 `parameters["x"]?.` 保护，部分直接索引会在缺失时抛异常）。

**修改影响提醒**：`ZRSwordFlow.fx` 同时被常量路径与"给滤镜烤图"两条路径复用，加 uniform 后要同时在 `SwordFlowMaterial.Draw`（`:247-296`）与 `SurfaceMaterialRenderer` 里设值，否则默认 0。

---

### 组 8　本地化

**职责一句话**：`Items / Buffs / NPCs / Projectiles / Keybinds` 五类的显示名与说明。

**核心文件**：`Localization/en-US.hjson`、`Localization/zh-Hans.hjson`（各 133 行，键结构一致）

**覆盖范围**（详见第四部分表格）：11 个 Items、10 个 Buffs、1 个 NPC、5 个 Projectiles（英文 11 个）、2 个 Keybinds。**全部渲染/特效类内部无任何本地化键**——`VfxPartDebug` 的面板文字（"B1 主飞剑拖尾带面"等）是硬编码中文字面量，`VfxIsolation` 的 `Label` 同理，不会随语言切换。

---

### 组 9　杂项渲染基础设施（不属上面 8 组，但属本范围）

- `BlessingCamera.cs`（77 行）：ILHook `Main.InitTargets`，把原版 192px 的离屏留白与 `_renderTargetMaxSize` 按 `PaddingFor(width,height,zoom)`（`:20`）动态放大，供祝福变焦不露边。入口 `Main.OnPreDraw`（`:36`）、`Load`（`:33`）。**这是"画面边缘出现空白/裁剪"类问题的第一嫌疑**（模式失配会抛 `InvalidOperationException`，`:26/:31`）。
- `HolyHeavenStarVisuals.cs`（40 行）：落星飞行的 dust（`:12`）与命中 8+4 道 `DustID.StarSpark`（`:19`），由 `HolyHeavenStar.cs:20/29` 调。
- `SwordEnchantmentVisuals.cs`（15 行）：`GlobalProjectile` 给 `SoulBlade`/`OreFlyingSword`/`OreSkyfall` 置 `noEnchantmentVisuals=true`，**只关外观不关减益**。
- `SwordSpaceDistortion.cs`：参数入口，无调用者。
- `SwordFlowMeshBasis.cs`：UV 梯度，仅 `SwordFlowMaterial.Unload` 调 `Clear()`（`:196`），`Prepare` 无调用者。
- `SoulRibbonMesh.cs`：仅 `SoulBlade.BuildRibbon`（`SoulBlade.cs:412`，本身也无调用者）与独立校验模组 `checks/ZenithPrototypeCheck/RibbonCheck.cs` 使用。

---

## 第二部分：文件索引表

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `ZenithReborn/VfxIsolation.cs` | C# 静态开关 | 总闸 | 10 分支 / 18 构成 / 26 带面构成 / 7 玻璃构成的唯一开关源 | 被全部渲染文件读取 | 已验证 |
| `ZenithReborn/VfxPartDebug.cs` | C# ModSystem | 总闸 | F7/F8 移动、F9 切换、F10 全关、Shift/Ctrl+F10 全开/基线 | `VfxIsolation` 四数组长度 | 已验证 |
| `ZenithReborn/VfxRenderState.cs` | C# 静态工具 | 总闸 | 保存/还原 Blend·Depth·Raster·Scissor·3 槽纹理采样·顶点索引，进入置 Additive | SoulVisuals、ViscousSlash、SoulSceneRefraction | 已验证 |
| `ZenithReborn/SoulMaterialTrail.cs` | C# 几何 | 拖尾 | 60 帧历史 → 3 步 × 4 行带面顶点；`Visibility`/`Dissolve` 曲线 | `SwordFlowMaterial.Draw`、`SoulBlade`、`SoulTrailEnding` | 已验证 |
| `ZenithReborn/SoulTrailUnits.cs` | C# 常量 | 拖尾 | 本体 + 3 残影的 `(start, samples, opacity)` 窗口 | `SoulBlade.DrawRibbonShader`、`SoulTrailEnding.CaptureUnit` | 已验证 |
| `ZenithReborn/SoulTrailMotion.cs` | C# 工具 | 拖尾 | 残影历史随主人位移平移；90000 平方距离上限 | `SoulBlade` | 已验证 |
| `ZenithReborn/SoulTrailEnding.cs` | C# ModSystem | 拖尾 | 弹幕销毁后 36/24 帧余迹，容量 256，按代际淘汰 | `SoulVisuals`、`SoulSceneRefraction` | 已验证 |
| `ZenithReborn/SoulVisuals.cs` | C# ModSystem | 拖尾/取色 | `Tint()` 三级取色表 + `PostDrawTiles` 世界层绘制入口 | 全模块取色、`SoulSceneRefraction.CompositingReady` | 已验证 |
| `ZenithReborn/SoulRibbonMesh.cs` | C# 几何（旧） | 拖尾 | 4 段 × 3 带旧版 ribbon，带亮边流动噪声 | `SoulBlade.BuildRibbon`（无调用者）、`checks/RibbonCheck` | 已验证 |
| `ZenithReborn/SwordFlightTrail.cs` | C# 类 | 拖尾 | 8 点短历史 + `Strength`/`Formation` 曲线 | `OreFlyingSword`、`OreSkyfall`、`HardOreShot`；原列 `OreSwordEffects.Trail` 为其使用者，该路径已随 T3 删除 `OreSwordEffects.Trail` 而移除（保留历史记录） | 已验证 |
| `ZenithReborn/SwordBodyColorNoise.cs` | C# 工具 | 剑体 | 3 层 value noise → 512² 灰度纹理 | `VfxSwordMask.Prepare` | 已验证 |
| `ZenithReborn/VfxSwordMask.cs` | C# ModSystem | 剑体 | `Main.OnPreDraw` 离屏合成色场×蒙版（640×256/剑） | `VfxMaskData`、`OreFlyingSword`、`OreWeaponEnding` | 已验证 |
| `ZenithReborn/VfxMaskData.cs` | C# 生成数据 | 剑体 | 21 把矿石剑的色场配方与几何常量 | `VfxSwordMask.Load`、`checks/emit_vfx_mask_data.py` | 已验证 |
| `ZenithReborn/SoulSceneRefraction.cs` | C# ModSystem(Client) | 折射 | 滤镜注册、normal/surface RT、`PrepareNormals`、`GlassScreenData` | `ZRGlassScreen.fx`、`SwordFlowMaterial`、`SoulVisuals` | 已验证 |
| `ZenithReborn/GlassMaterialPreset.cs` | C# 生成常量 | 折射 | 玻璃光学常量（Ior/Thickness/Dispersion/Ssr/Emission/SpaceWarp） | `SwordFlowMaterial`、`SoulSceneRefraction`、`SwordSpaceDistortion` | 已验证 |
| `ZenithReborn/SwordSpaceDistortion.cs` | C# 静态工具 | 折射 | 空间扭曲参数包 `At`/`Apply` | `ZRSwordSpaceWarp.fxh`（孤儿）；**无调用者** | 已验证 |
| `ZenithReborn/SwordFlowMaterial.cs` | C# ModSystem | 材质 | 效果加载、图集绑定、`Draw` 分流、`ZoneMask.zrdata` 上传、世界光 | `SurfaceMaterialRenderer`、`SoulSceneRefraction`、`Material*` | 已验证 |
| `ZenithReborn/SurfaceMaterialDefinition.cs` | C# 数据合同 | 材质 | 40+ 参数 + 4 细节层 + 10 贴图路径 + `Validate`/`Control[]` | `MaterialInheritance`、`SurfaceMaterialRenderer` | 已验证 |
| `ZenithReborn/SurfaceMaterialRenderer.cs` | C# ModSystem(Client) | 材质 | `ZRMaterialSurface` 绘制、UV→切线基、高度图位移 | `SurfaceMaterialDefinition`、`SwordFlowMaterial` | 已验证 |
| `ZenithReborn/MaterialInheritance.cs` | C# 静态工具 | 材质 | `"base"` 递归继承（最大 8 层）、未知字段忽略 | `SwordFlowMaterial.Load` | 已验证 |
| `ZenithReborn/MaterialQuality.cs` | C# 静态类 | 材质 | 8/16 把飞剑阈值自动降档 | `SwordFlowMaterial` | 已验证 |
| `ZenithReborn/SwordFlowMeshBasis.cs` | C# 几何 | 材质 | 跨三角形 UV 梯度（并查集加权） | 仅 `SwordFlowMaterial.Unload`→`Clear()`；`Prepare` 无调用者 | 已验证 |
| `ZenithReborn/SwordFlowPalette.cs` | C# 静态工具 | 材质 | 剑色 → 暗/中/亮三阶，低饱和走冷钢 | `SwordFlowMaterial.Draw` | 已验证 |
| `ZenithReborn/SoulZenithEffects.cs` | C# 静态工具 | 星芒 | 星芒 `DrawTip`、飞行 dust、两处光照、`Profile` | `SoulBlade.PreDraw/AI`、`FinalFractalHelper` | 已验证 |
| `ZenithReborn/SwordSpark.cs` | C# ModDust | 粒子 | 预乘发光小尘，自带光照 | `SwordSpark.Emit` **无调用者**；贴图落回 MagicPixel in PureCode | 已验证 |
| `ZenithReborn/SoulFlowParticles.cs` | C# ModDust + 发射器 | 粒子 | `SoulFlowStyle` 配色、`SlashSparkEmitter`、`SoulFlowSpark`（借用 SharpTears） | `OreSwordEffects:130`、`OrePierceFeedback:33`、`SoulBlade:241` | 已验证 |
| `ZenithReborn/EvapCircle.cs` | C# ModDust | 粒子 | 借原版 Cloud 帧的上升缩小圆 | 仅 `ViscousSlash:52` 调用 | 已验证 |
| `ZenithReborn/ViscousSlash.cs` | C# ModSystem(Client) | 粒子 | `On_Main.DrawDust` 加法批次残影扫掠 | `VfxIsolation.ViscousSlash`、`EvapCircle` | 已验证 |
| `ZenithReborn/FlyingBladeVisuals.cs` | C# 类 | 飞刃 | 拖尾/环影/光点/光环/火花/命中爆发；`PreDraw` 内切批次 | `FlyingBladeShot`、`SoulVisuals.Tint` | 已验证 |
| `ZenithReborn/SwordEnchantmentVisuals.cs` | C# GlobalProjectile | 杂项 | 三类飞剑 `noEnchantmentVisuals=true` | `SoulBlade`/`OreFlyingSword`/`OreSkyfall` | 已验证 |
| `ZenithReborn/BlessingCamera.cs` | C# ModSystem | 杂项 | ILHook `InitTargets` 扩容离屏留白与 RT 上限 | `BlessingPlayer`、`Main.OnPreDraw` | 已验证 |
| `ZenithReborn/HolyHeavenStarVisuals.cs` | C# 静态工具 | 杂项 | 落星飞行 dust + 命中 12 道 StarSpark | `HolyHeavenStar` | 已验证 |
| `ZenithReborn/Assets/Effects/ZRSwordFlow.fx` | HLSL | 着色器 | 带面主材质（36 参数，无 `uPart*`） | `SwordFlowMaterial` | 已验证 |
| `ZenithReborn/Assets/Effects/ZRGlassScreen.fx` | HLSL | 着色器 | 屏幕折射滤镜（6 参数 + 2~4 张 RT） | `SoulSceneRefraction` | 已验证 |
| `ZenithReborn/Assets/Effects/ZRMaterialSurface.fx` | HLSL | 着色器 | 通用微表面材质（含 `uDetailA[4]` 等） | `SurfaceMaterialRenderer` | 已验证 |
| `ZenithReborn/Assets/Effects/*.fxc`（3 个） | 编译产物 | 着色器 | 与 `.fx` 同名对照；运行时加载的是 `.fx` 经构建编译的 xnb | `build-manifest.json` | 部分待核实（构建是否真的重编 `.fx` 未在构建日志中确认） |
| `ZenithReborn/Assets/Effects/*.fxh`（4 个） | HLSL 头 | 着色器 | 玻璃光学 / 液态玻璃 / 高光 / 空间扭曲片段；**无任何 `.fx` include**，且被 `buildIgnore` 排除 | — | 已验证 |
| `ZenithReborn/Assets/Effects/build-manifest.json` | JSON | 资产 | 3 组 `.fx↔.fxc` 的 sha256 与编译器信息（`d3dcompiler_43 / fx_2_0`） | `checks` 校验脚本 | 已验证 |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/bindings.json` | JSON 配置 | 资产 | `weapons` 图集绑定 + `SurfaceMaterial` 路径；**当前 `weapons: {}`** | `SwordFlowMaterial.Load` | 已验证 |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/ZoneMask.zrdata` | 自定义二进制 | 资产 | 四区遮罩原始 RGBA（魔数 ZRDT/v1） | `SwordFlowMaterial.TryLoadRaw` | 已验证 |
| `ZenithReborn/Assets/Vfx/Materials/SwordFlow/*.png`（12 张） | PNG | 资产 | PBR/噪声/色场贴图（含 `*_candidate`） | `SurfaceMaterialRenderer.LoadInputs` | 部分待核实（`bindings.json` 为空，当前无一被引用） |
| `ZenithReborn/Assets/Vfx/SwordBody/*.png`（21 张） | PNG | 资产 | 矿石剑 16 帧 4×4 图集（640×256/帧） | `VfxSwordMask.Load` | 已验证 |
| `ZenithReborn/Assets/UI/BossPortraits/*.png`（19 张） | PNG | 资产（非本组绘制） | Boss 立绘/躯体图；属 UI 模块 | `SoulBossPortraitCatalog`（C 组范围） | 部分待核实（使用点不在本次范围） |
| `ZenithReborn/Assets/UI/BossPortraits/sources.json` | JSON | 资产 | 立绘来源登记 | UI 模块 | 部分待核实 |
| `ZenithReborn/SwordSpark.png` | PNG（模组根，**不在 Assets 下**） | 资产 | 16×16 光斑，被 `SwordSpark`/`SoulFlowSpark` 引用 | `SwordSpark.cs:10`、`SoulFlowParticles.cs:77` | 已验证 |
| `ZenithReborn/Localization/en-US.hjson` | 本地化 | 本地化 | 英文键值 | `Localization` | 已验证 |
| `ZenithReborn/Localization/zh-Hans.hjson` | 本地化 | 本地化 | 简中键值 | `Localization` | 已验证 |
| `ZenithReborn/build.txt` | 构建配置 | 配置 | `buildIgnore = bin/*, obj/*, *.csproj, Assets/Effects/*.fxh` | 打包 | 已验证 |
| `ZenithReborn/ZenithReborn.csproj` | 构建配置 | 配置 | net8.0 / LangVersion12 / `BuildMod=false` / 引 Luminance | 构建 | 已验证 |

---

## 第三部分：`VfxIsolation` 分支全表

### 3.1　全局策略开关

| 开关字段 | 中文标签 | 默认值 | 控制什么绘制 | 使用点文件:行号 |
|---|---|---|---|---|
| `PureCodeOnly` | 纯代码档（禁贴图/禁屏幕采样/禁原版粒子） | `false`（硬编码表达式，非字段） | 一次性关掉 12 个外部输入分支 + 全部贴图加载 + 蒙版图集登记 + 滤镜 | `VfxIsolation.cs:12`；消费见 `SwordFlowMaterial.cs:32/36/57/102/113/124/201`、`VfxSwordMask.cs:29/53`、`SoulSceneRefraction.cs:54/55/88/94/111`、`SoulFlowParticles.cs:77/80`、`SwordSpark.cs:10/13`、`SoulZenithEffects.cs:33`、`OreSwordEffects.cs:57` |
| `GlassPlaneOnly` | 只做平面玻璃（不接外来贴图） | `false` | `BindMap` 时不查 `maps`，所有 PBR 贴图开关下发 0 | `SwordFlowMaterial.cs:102` |
| `GlassOpacity(float)` | 玻璃不透明度直通 | `normalOpacity`（恒等） | 预留接口，**无调用者** | `VfxIsolation.cs:16` |
| `ViscousSlash` | 粘性空腔挥动残影 | `false`（用户判定效果失败） | 记录/绘制武器残影 + 蒸发圆；**不参与 SetAll、不在 61 项清单内** | `ViscousSlash.cs:40`（记录）、`ViscousSlash.cs:89`（绘制） |

### 3.2　`Branches`（10 项，`VfxIsolation.cs:63-76`）

| 开关字段 | 中文标签 | 默认值 | 控制什么绘制 | 使用点文件:行号 |
|---|---|---|---|---|
| `SwordTrail` | B1 主飞剑拖尾带面 | `true` | `SoulBlade` 的主带面 + 残影短带面（`BuildLayered` 一次提交） | `SoulVisuals.cs:67`、`SoulSceneRefraction.cs:88`、`:94`、`:111` |
| `EchoBlades` | B2 三个剑影（15/30/45） | `!PureCodeOnly` → `true` | `SoulBlade.PreDraw` 的三枚整剑贴图残影 | `SoulBlade.cs:501` |
| `TipGlow` | B3 剑尖星芒 | `!PureCodeOnly` → `true` | 两笔光晕 + 两笔白核心 | `SoulBlade.cs:518` |
| `TrailEnding` | B4 独立消散片 | `true` | 弹幕销毁后残留的短带面 | `SoulVisuals.cs:70`、`SoulSceneRefraction.cs:89`、`:95`、`:112` |
| `FlyingParticles` | B5 飞行粒子与光照 | `true` | `EmitFlying` 整段（dust + 两处光照） | `SoulBlade.cs:257` |
| `FlowSpark` | B6 刃缘碎光 | `!PureCodeOnly` → `true` | 提前收回时沿旧刃缘甩出的 `SoulFlowSpark` | `SoulBlade.cs:240` |
| `GlassFilter` | B7 玻璃屏幕折射滤镜 | `!PureCodeOnly` → `true` | 滤镜激活 + `PrepareNormals` 整段 | `SoulSceneRefraction.cs:54`、`:55` |
| `FlyingBladeTrail` | B8 飞刃拖尾与环影 | `true` | 飞刃拖尾、旋转环影、尾部火花 | `FlyingBladeVisuals.cs:158`、`:161`、`:306` |
| `FlyingBladeGlow` | B9 飞刃光点与光环 | `true` | 中心光点、命中扩散光环、刃身光照 | `FlyingBladeVisuals.cs:135`、`:145`、`:162` |
| `FlyingBladeImpact` | B10 飞刃命中爆发 | `true` | 命中 8 粒 dust 扩散 | `FlyingBladeVisuals.cs:146` |

### 3.3　`Details`（18 项，`VfxIsolation.cs:198-218`）

| 开关字段 | 中文标签 | 默认值 | 控制什么绘制 | 使用点文件:行号 |
|---|---|---|---|---|
| `EchoNear` | B2-1 近位剑影（15） | `!PureCodeOnly` → `true` | 第 1 枚残影 | 经 `EchoSlotEnabled` `VfxIsolation.cs:168-174` → `SoulBlade.cs:506` |
| `EchoMid` | B2-2 中位剑影（30） | `!PureCodeOnly` → `true` | 第 2 枚残影 | 同上 |
| `EchoFar` | B2-3 远位剑影（45） | `!PureCodeOnly` → `true` | 第 3 枚残影 | 同上 |
| `EchoTint` | B2-4 剑影向白染色 22% | `!PureCodeOnly` → `true` | 残影色 `Lerp(tint, White, .22)` | `SoulBlade.cs:510` |
| `EchoBladeAlpha` | B2-5 剑影亮度阶梯 | `!PureCodeOnly` → `true` | 0.75/0.50/0.25 阶梯；关掉则全 1.0 | `SoulBlade.cs:508` |
| `TipHalo` | B3-1 星芒光晕 | `!PureCodeOnly` → `true` | `SharpTears` 两笔彩色 | `SoulZenithEffects.cs:60` |
| `TipCore` | B3-2 星芒白核心 | `!PureCodeOnly` → `true` | 两笔白色（尺寸 ×0.6） | `SoulZenithEffects.cs:65` |
| `TipPulse` | B3-3 星芒脉动包络 | `!PureCodeOnly` → `true` | 15~45 的 `Flare` 包络；关掉则常亮 | `SoulZenithEffects.cs:48` |
| `TipOffset` | B3-4 星芒贴剑尖偏移 | `!PureCodeOnly` → `true` | 关掉则星芒落在剑心 | `SoulZenithEffects.cs:55` |
| `EndingShape` | B4-1 消散片本体 | `true` | 关掉只跳过绘制，年龄照常推进 | `SoulTrailEnding.cs:88` |
| `EndingFadeCurve` | B4-2 消散片消失曲线 | `true` | 末端 6% 快速消失；关掉则恒定初始 alpha | `SoulTrailEnding.cs:91` |
| `EndingDissolve` | B4-3 消散片尾部吞噬 | `true` | `dissolve` 参数（着色器消融） | `SoulTrailEnding.cs:94` |
| `FlyingDust` | B5-1 飞行粒子本体 | `!PureCodeOnly` → `true` | `profile.dustMethod` 循环（**还额外要求 `!PureCodeOnly`**） | `SoulZenithEffects.cs:33` |
| `FlyingBladeLight` | B5-2 剑身光照 | `true` | `Lighting.AddLight(p.Center, tint*.5*opacity)` | `SoulZenithEffects.cs:38` |
| `FlyingPlayerLight` | B5-3 玩家光照 | `true` | `Lighting.AddLight(player.MountedCenter, ...*.15)` | `SoulZenithEffects.cs:39` |
| `SparkBody` | B6-1 碎光本体 | `!PureCodeOnly` → `true` | `SoulFlowSpark` 第一笔 | `SoulFlowParticles.cs:114` |
| `SparkCore` | B6-2 碎光亮芯 | `!PureCodeOnly` → `true` | 白芯（尺寸 ×0.42,0.85） | `SoulFlowParticles.cs:117` |
| `SparkMote` | B6-3 碎光横向第二笔 | `!PureCodeOnly` → `true` | `Mote` 类型的十字第二笔 | `SoulFlowParticles.cs:119` |

### 3.4　`Parts`（P01~P26，`VfxIsolation.cs:107-135`）—— ⚠ 当前对画面无效果

统一说明：**本表全部行的"使用点"只有 `VfxPartDebug` 的读写（`VfxPartDebug.cs:79-80`、`:106`），没有任何渲染代码读取；`ZRSwordFlow.fx` 也不声明对应 uniform。下表"控制什么绘制"填的是**设计意图**，不是当前实际行为。

| 开关字段（uniform） | 中文标签 | 默认值 | 设计意图 | 实际使用点 |
|---|---|---|---|---|
| `uPartShape` | P01 主体形状（关掉整条带面消失） | `1f` | 整条带面 | 无（`VfxPartDebug.cs:79`） |
| `uPartBend` | P02 边界正弦弯曲 | `0f` | 边界扰动 | 无 |
| `uPartThreads` | P03 尾部拉丝细缕 | `0f` | 尾部细丝 | 无 |
| `uPartDepthGradient` | P04 横向明暗梯度 | `0f` | 刃侧亮/腹部暗 | 无 |
| `uPartProcField` | P05 程序色场低频噪声 | `0f` | 低频色场 | 无 |
| `uPartDissolve` | P06 消融与回收前沿 | `0f` | 消融遮罩 | 无 |
| `uPartRim` | P07 消融前沿发光 | `0f` | 前沿亮边 | 无 |
| `uPartCurvature` | P08 双轴曲面法线 | `1f` | 曲面法线 | 无 |
| `uPartBevel` | P09 刃口倒角 | `1f` | 刃口倒角 | 无 |
| `uPartRazorCore` | P10 刃核白线 | `0f` | 白芯线 | 无 |
| `uPartRazorHalo` | P11 刃线中层光晕 | `0f` | 中层辉光 | 无 |
| `uPartRazorMist` | P12 刃线外层弥散 | `0f` | 外层弥散 | 无 |
| `uPartInnerRim` | P13 内缘线 | `0f` | 内缘 | 无 |
| `uPartBodyTint` | P14 剑身色铺底 | `0f` | 底色 | 无 |
| `uPartBandReflection` | P15 局部光面边缘 | `0f` | 局部光面 | 无 |
| `uPartGlassBase` | P16 玻璃基底不透明度 | `1f` | 基底 | 无 |
| `uPartGgxCore` | P17 GGX 高光核 | `0f` | GGX 核 | 无 |
| `uPartSoftLobe` | P18 宽高光包络 | `0f` | 宽瓣 | 无 |
| `uPartWhiteShift` | P19 高光亮色过渡 | `0f` | 白移 | 无 |
| `uPartHighlightNoise`（外部输入） | P20 噪声调制高光 | `0f` | 噪声高光 | 无 |
| `uPartDispersion`（外部输入） | P21 色散 | `0f` | 色散 | 无（实际由 `uDispersion` 常量控制） |
| `uPartSceneTransmission`（外部输入） | P22 背景透射 | `1f` | 透射 | 无（实际由 `uTransmission` 控制） |
| `uPartSsr`（外部输入） | P23 屏幕空间反射 | `1f` | SSR | 无（实际由 `uSsrStrength` 控制） |
| `uPartStripe` | P24 虚拟光面强度 | `0f` | 条纹 | 无（`uStripeStrength` 也从未被赋非 0 值） |
| `uPartEmission` | P25 虚拟光源亮度 | `0f` | 自发光 | 无（`uEmissionStrength` 从未被赋值 → 常数 0） |
| `uPartRidges` | P26 液态玻璃条带起伏（只驱动折射） | `0f` | 条带 | 无（`ZRLiquidGlass.fxh` 未被 include） |

### 3.5　`GlassParts`（B7-1~B7-7，`VfxIsolation.cs:226-235`）—— ⚠ 当前对画面无效果

统一说明：`ZRGlassScreen.fx` 只声明 6 个 float（`:5-10`），`GlassScreenData.Apply`（`SoulSceneRefraction.cs:132-147`）也从不读取 `GlassPartValues`；唯一消费方是 `VfxPartDebug.cs:85`/`:111`。

| 开关字段（uniform） | 中文标签 | 默认值 | 设计意图 | 实际使用点 |
|---|---|---|---|---|
| `uGlassPartCoverage` | B7-1 覆盖度（玻璃是否存在） | `1f` | 覆盖度 | 无（`VfxPartDebug.cs:85`）；真实覆盖度由 `uMaskReady` + `mask.a` 算 |
| `uGlassPartRefraction` | B7-2 背景折射偏移 | `1f` | 折射 | 无；真实开关是 `uGlassIor`/`uGlassThickness` |
| `uGlassPartBlur` | B7-3 粗糙度模糊 | `1f` | 模糊 | 无；真实值来自 `mask.b` |
| `uGlassPartTransmittance` | B7-4 透射吸收 | `1f` | 透射 | 无；真实值 `uGlassTransmission` |
| `uGlassPartCoating` | B7-5 高光合成 | `1f` | 涂层 | 无；真实值 `surfaceSampler` |
| `uGlassPartBloom` | B7-6 泛光扩散 | `1f` | 泛光 | 无；shader 中无此段 |
| `uGlassPartSpaceWarp`（外部输入） | B7-7 刀光周围空间扭曲 | `1f` | 空间扭曲 | 无；仅 `ZRSwordSpaceWarp.fxh:2` 声明，孤儿文件 |

### 3.6　命名规律小结

- **分支**：`B1`~`B7` 是"按视觉单元切"的十个总闸（B1~B7 属 `SoulBlade` 体系，B8~B10 属 `FlyingBladeShot` 体系的独立三项）；`B2`~`B6` 再向下拆 `B2-1`…`B6-3`。
- **带面构成**：`P01`~`P26`，`Parts[i].Uniform` 前缀统一 `uPartXxx`，顺序被注释强制要求与 `ZRSwordFlow.fx` 声明顺序一致（`VfxIsolation.cs:101`）——**该约束已失效**，因为 shader 已重写不再有这些 uniform。
- **玻璃构成**：`B7-1`~`B7-7`，uniform 前缀 `uGlassPartXxx`，标签与 `GlassParts` 数组顺序一一对应。
- **外部输入项**：`Branch`/`Detail`/`Part` 三个 record 都有 `UsesExternalInput` 旗标 + `Allowed => !PureCodeOnly || !UsesExternalInput`，共 12 项被标记（B2、B3、B6、B7 整支 + B5-1 + B6-1~3 + P20~P23 + B7-7）。
- **总数**：10 + 18 + 26 + 7 = **61**（`VfxPartDebug.cs:8` 注释与运行时 `Total` 一致）；另有 `ViscousSlash` 1 项**在册外**。

---

## 第四部分：资产清单

### 4.1　`Assets/Effects/*`

| 文件 | 用途 | 被谁 include / 加载 | 进发布包 |
|---|---|---|---|
| `ZRSwordFlow.fx` | 剑迹带面主材质（36 个 uniform，单 technique 单 pass，`L350`） | `ModContent.Request<Effect>("ZenithReborn/Assets/Effects/ZRSwordFlow")` ← `SwordFlowMaterial.cs:230`（`.Clone()`） | 是（`buildIgnore` 未排除 `*.fx`） |
| `ZRGlassScreen.fx` | 屏幕折射/模糊/涂层合成（6 float + s0~s4） | `SoulSceneRefraction.cs:45` → 装进 `Filters.Scene["ZenithReborn:SwordGlass"]` | 是 |
| `ZRMaterialSurface.fx` | 通用微表面材质（含 `uDetailA[4]`/`uMode`，`L202` technique） | `SurfaceMaterialRenderer.cs:90`（仅在 `HasSurfaceMaterials` 时触发） | 是 |
| `ZRSwordFlow.fxc` | `ZRSwordFlow.fx` 的编译对照（`fx_2_0`） | **无代码引用** | 是 |
| `ZRGlassScreen.fxc` | 同上 | 无代码引用 | 是 |
| `ZRMaterialSurface.fxc` | 同上 | 无代码引用 | 是 |
| `ZRClearGlass.fxh` | `GlassLinear` 等线性光小工具 | **无任何 `.fx` include** | **否**（`buildIgnore = ... Assets/Effects/*.fxh`） |
| `ZRLiquidGlass.fxh` | 液态玻璃条带起伏几何 | 无 include（注释声称 `ZRSwordFlow.fx`/`ZRMaterialSurface.fx` 共用，实际未接） | 否 |
| `ZRSwordHighlights.fxh` | 矩形面光镜面近似 | 无 include | 否 |
| `ZRSwordSpaceWarp.fxh` | 空间扭曲（声明唯一 `uGlassPartSpaceWarp`） | 无 include | 否 |
| `build-manifest.json` | 3 组 `.fx↔.fxc` 的 sha256 + 编译器信息 | `checks` 侧校验脚本 | 是 |

> 结论：4 个 `.fxh` 是**双重孤儿**（既无 `#include`，又被 `buildIgnore` 排除）。改它们不会影响画面，也不会进包。

### 4.2　52 张 PNG 分组

共 **52 张**，分 **3 组**：

| 分组 | 数量 | 目录 | 代表文件 | 使用它的代码 |
|---|---|---|---|---|
| A. 矿石剑身图集（剑体色场蒙版） | **21** | `Assets/Vfx/SwordBody/` | `CobaltSword.png`、`AdamantiteSword.png`、`CopperShortsword.png` | `VfxSwordMask.Load`（路径拼接 `ZenithReborn/Assets/Vfx/SwordBody/` + `ItemID.Search.GetName`，`VfxSwordMask.cs:32`）；行数据来自 `VfxMaskData.cs:15-38`（21 行，与文件一一对应） |
| B. 剑流材质贴图（PBR / 噪声 / 色场） | **12** | `Assets/Vfx/Materials/SwordFlow/` | `NormalMap.png`、`ColorField.png`、`NoiseSeamless.png`、`ZoneMask.png`（另有 `.zrdata` 版）、`BrushDefault.png`、`EmissionMap.png`、`EnvironmentMap.png`、`HeightMap.png`、`SurfaceMap.png`、`ZenithField.png`、`ZenithWear.png`、`NoiseSeamless_candidate.png` | `SurfaceMaterialRenderer.LoadInputs`（`SurfaceMaterialRenderer.cs:51-80`）/`SwordFlowMaterial.BindMap`（`:102`）。**当前 `bindings.json` 的 `weapons` 为空 → 无一份材质引用它们，实际零加载** |
| C. Boss 立绘（UI，非本组绘制） | **19** | `Assets/UI/BossPortraits/` | `Eye.png`、`Moon.png`、`Empress.png`；另有 `*Body.png` 4 张（`Destroyer`/`Eater`/`Queen`/`Skeleton`） | `SoulBossPortraitCatalog` / `SoulBossPortraits`（C 组范围，本次未逐行核实） |

补充：`ZenithReborn/SwordSpark.png`（16×16）位于**模组根目录而非 Assets**，由 `SwordSpark.cs:10` 与 `SoulFlowParticles.cs:77` 以 `"ZenithReborn/SwordSpark"` 引用，`PureCodeOnly` 时回退 `Terraria/Images/MagicPixel`。它不计入 52。

### 4.3　两个 hjson 的键覆盖范围

两文件行数相同（133），**结构一致、内容分工明确**：

| 分类 | 有条目的键 | 走默认名（注释掉 / 缺失） |
|---|---|---|
| `Items`（11 个） | `GuardianGel`、`SoulItem`、`CrimsonSigil`、`CorruptionLure`、`DawnCore`、`FleshCore`、`MachineCore`、`LifeCore`、`TempleCore`、`CelestialCore` | 原 `SlashZenithTest`（中文侧两个键被注释掉；英文侧有值）→ 中文走类型名（该测试物品已移除：T5 已删 `SlashZenithTest.cs` 及 hjson 键；此行不再适用，保留 2026-11-03 调查快照记录） |
| `Buffs`（10 个） | `ForestBreath`、`BloodFragrance`、`ForestPower`、`FrostStill`、`SaltCorrosion`、`JungleToxin`、`WoodWither`、`EmberBurn`、`HallowRadiance`、`SoulBreach` | — |
| `NPCs`（1 个） | `GuardianSlimeKing.DisplayName` | — |
| `Projectiles`（中 5 / 英 11） | 中文：`BlessingStrike`、`SoulBlade`、`HardOreShot`、`OreFlyingSword`、`OreSkyfall`；英文额外有 `EnvironmentDomain`、`EnvironmentEffect`、`FlyingBladeShot`、`HolyHeavenImpact`、`HolyHeavenStar`、`LivingArmament` | 中文侧后 6 项被注释（`zh-Hans.hjson:120-125`）→ 中文客户端显示英文/类型名 |
| `Keybinds`（2 个） | `ActivateYujian`、`OpenSoul` | — |

**渲染相关缺口**：`FlyingBladeShot`、`HolyHeavenStar`、`LivingArmament` 三个**有绘制代码的实体**在中文侧无本地化条目；`VfxPartDebug` 的 61 项标签与 `VfxIsolation` 的 `Label` 全部是硬编码中文，不参与本地化（英文客户端也会看到中文调试文本）。

---

## 附：渲染注入点总览（按一帧时间顺序）

| # | 阶段 | 注入点 | 文件:行号 | 读写 RenderTarget |
|---|---|---|---|---|
| 1 | `PostUpdateEverything` | 滤镜激活判定 / 飞刃队列推进 / 粘性残影记录 / 世界光 | `SoulSceneRefraction.cs:51`、`FlyingBladeVisuals.cs:105`、`ViscousSlash.cs:38`、`SwordFlowMaterial.cs:302` | 否 |
| 2 | `Main.OnPreDraw` | `BlessingCamera.PrepareTargets` → 必要时重建原版离屏目标 | `BlessingCamera.cs:36` | 写原版 terrain RT |
| 3 | `Main.OnPreDraw` | `VfxSwordMask.PrepareFrame` → 21 把矿石剑的蒙版 | `VfxSwordMask.cs:35/54` | 写 `mask`(640×256) → `targets[itemType]`(640×256) |
| 4 | 世界绘制 | `SoulVisuals.PostDrawTiles` → 主拖尾 + 消散片（`CompositingReady` 时跳过） | `SoulVisuals.cs:54` | 否（直接画屏幕） |
| 5 | 实体绘制 | `SoulBlade.PreDraw` → 三残影 → 剑体 → 星芒 | `SoulBlade.cs:485/501/516/518` | 否 |
| 6 | 弹幕绘制 | `FlyingBladeShot.PreDraw` → `FlyingBladeVisuals.Draw`（内部 End/Begin 切 Additive） | `FlyingBladeShot.cs:106/113`、`FlyingBladeVisuals.cs:169-182` | 否 |
| 7 | Dust 绘制 | `SoulFlowSpark.PreDraw` / `EvapCircle.PreDraw` | `SoulFlowParticles.cs:122`、`EvapCircle.cs:61` | 否 |
| 8 | `On_Main.DrawDust` | `ViscousSlash.DrawHook`（先 `orig` 再画） | `ViscousSlash.cs:30/86` | 否 |
| 9 | `On_FilterManager.BeginCapture` | `PrepareNormals` → normalMask / surfaceMask / (materialProperties / materialTransmission) | `SoulSceneRefraction.cs:43/59/64` | 写 2~4 张全屏 RT |
| 10 | 滤镜合成 | `GlassScreenData.Apply` → `ZRGlassScreen` 读 s0~s4 合成到屏幕 | `SoulSceneRefraction.cs:132` | 读 2~4 张 RT |

---

## 未确认项 / 需要后续核实

1. **`.fxc` 是否参与构建**：`build-manifest.json` 记录了 `.fx→.fxc` 的 sha256，但 tModLoader 实际是把 `.fx` 编成 `.xnb`；本报告无法从代码确认 `.fxc` 是否被构建脚本使用（可能仅作离线对照）。需查 `checks/` 与构建日志。
2. **`PrepareNormals` 的纹理槽保存不完整**：`SoulSceneRefraction.cs:85/108` 主动清空 0~15 槽，但 `finally` 只在 `:124` 还原 0~15 —— 与实际 16 槽一致，此项应无问题；然而 `VfxRenderState`（`:14-15`）只保存 0~2 槽，若将来有 3 槽以上的绘制放在 `DrawWithRestoredState` 内会漏还原。属**潜在**风险，非现状缺陷。
3. **`SoulSceneRefraction.Capture()` / `snapshot` / `UvTransform` 整条退路无人调用** → `Scene` 恒 `null`，`uSceneUvTransform` 恒 `(0,0,1,1)`。是否有意保留（供未来窗口缩放修正）未在注释中说明。
4. **19 张 Boss 立绘的实际使用点**未逐行核实（属 C 组 UI 范围），仅确认目录与数量。
5. **`Assets/Vfx/Materials/SwordFlow` 的 12 张 PNG 与 `.zrdata`**：因 `bindings.json` 为空，无法确认哪张对应哪个字段（字段名只在 `SurfaceMaterialDefinition.MaterialArtwork` 里，无实际绑定）。
6. **`VfxMaskData.cs` 由 `checks/emit_vfx_mask_data.py` 生成**，本次未运行生成脚本核对 21 行是否与 `SwordAssets` 目录完全同步（文件数 21 == 行数 21，已静态对齐）。
7. **`MaterialQuality` 的降档只在 `material!=null` 分支生效**（`SwordFlowMaterial.cs:240-241`），常量路径仅使用 `ThicknessScale`；这是否为设计意图未在注释中说明。
