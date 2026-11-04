# H · docs/archive 与根目录文档建图

调查范围：`docs/archive/*.md`（116 篇）+ 根目录 `AGENTS.md` / `README.md` / `CODEBUDDY.md` / `.gitignore` / `.gitattributes`。
只读调查；事实核对基准为 `ZenithReborn/build.txt`（version = 0.3.130）与当前源码。

---

## 第一部分 · 归档文档索引（116/116）

说明：归档全部为历史记录，`类型` 统一填 `历史归档`，`分析状态` 统一填 `已扫描`（依据 = 读取文件标题行 + 首段正文，非凭文件名推断）。"所属模块"按内容主题归类，"关键关联"给出当前仍承载该职责的源码文件或现行文档。

### A 组 · 早期机制与寄宿基础（0.3.0–0.3.12，13 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/0.3.0-木质族实装说明.md | 历史归档 | 木质族 / 寄宿 | 木质族 W2.1 首版：12 把木剑、10 普通槽 + 2 森林奖励 + 5 支援槽、P 键寄宿与右键管理 | SoulItem.cs · WoodTypes.cs | 已扫描 |
| docs/archive/0.3.1-全部槽位与友善史莱姆王.md | 历史归档 | 剧情 / 槽位 | 17 槽全解锁，友善史莱姆王授福流程（凝胶信物唤出、逐段对话接受「附着」） | AttachmentStory.cs | 已扫描 |
| docs/archive/0.3.2-月亮领主召唤修复.md | 历史归档 | 世界 / Boss | 四柱后月亮事件未启动的根因：`UpdateLunarApocalypse` 在标记 false 时直接返回，附带恢复方案 | LunarSummonRecovery.cs | 已扫描 |
| docs/archive/0.3.3-五段挥臂与单剑配色.md | 历史归档 | 出剑 | 一次挥臂分 20/40/60/80/100% 五节点逐把生成真实剑影，手臂姿势跟随节点 | SoulBlade.cs · SoulVisuals.cs | 已扫描 |
| docs/archive/0.3.4-复生伤害修复与战斗对比.md | 历史归档 | 战斗 / 伤害 | 移除"除以 √剑数"分摊与"终生只命中一次"局部免疫，改回冷却后可再命中 | SoulCombat.cs | 已扫描 |
| docs/archive/0.3.5-逐剑伤害与独立触发.md | 历史归档 | 战斗 / 伤害 | 确定为逐剑独立伤害模型，撤回统一天顶伤害下限与共享属性模板 | SoulCombat.cs | 已扫描 |
| docs/archive/0.3.6-肉后三矿实装.md | 历史归档 | 矿石族 | 六把肉后矿石剑获得剑魂专属机制（首版六项效果表） | OreSwordTraits.cs | 已扫描 |
| docs/archive/0.3.7-寄宿界面优化.md | 历史归档 | UI | 界面拆分为三页（寄宿 / 成长 / 图鉴）的信息架构 | SoulUI.cs | 已扫描 |
| docs/archive/0.3.8-界面点击穿透修复.md | 历史归档 | UI / 输入 | 在 `PostUpdateInput` 提前捕获面板范围内鼠标，解决点击穿透到背包 | SoulInput.cs | 已扫描 |
| docs/archive/0.3.9-矿石族飞剑实装.md | 历史归档 | 矿石族 | 百炼飞剑首版（含被否的 25 槽方案与 `MissingResourceException` 贴图热修） | OreFlyingSword.cs | 已扫描 |
| docs/archive/0.3.11-项目跟进与17槽恢复.md | 历史归档 | 槽位 | 25 槽未获用户确认，回退 17 有效槽；发现 hide 缺少 DrawBehind 导致飞剑不绘制 | SlotProgression.cs | 已扫描 |
| docs/archive/0.3.12-生效特性与图鉴.md | 历史归档 | UI | 信息区只显示当前构筑"生效"特性，并新增"特性图鉴"页与激活条件标注 | SoulTraits.cs | 已扫描 |
| docs/archive/验证记录-v0.3.11.md | 历史归档 | 验证 | 0.3.11 的编译/夹具证据清单（零警告、生产与检查项目） | checks/ | 已扫描 |

### B 组 · 出剑节奏与飞剑表现（0.3.13–0.3.31，19 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/0.3.13-出剑节奏与首击飞剑.md | 历史归档 | 出剑 / 矿石族 | 挥臂周期改为平均间隔 ×1.5（25~90 帧）；只有主剑"首次有效命中"才积攒一把飞剑 | SoulBlade.cs · SoulSwingPlayer.cs | 已扫描 |
| docs/archive/0.3.14-飞剑表现与窗口拖动.md | 历史归档 | UI / 出剑 | 飞剑 20 帧渐显 + 剑尖闪点；剑魂窗口支持空手拖标题栏且位置按世界记忆 | SoulUI.cs | 已扫描 |
| docs/archive/0.3.15-武器速度与模型尺寸.md | 历史归档 | 出剑 | 回应用户"仍然太快、模型过小"：周期 ×2.25（40~135 帧）、飞剑显示为原武器 72% | SoulBlade.cs | 已扫描 |
| docs/archive/0.3.16-精简描述与速度微调.md | 历史归档 | UI / 本地化 | 悬浮描述从"全特性全文"压缩为三行短提示、每行 ≤28 字符 | SoulItem.cs | 已扫描 |
| docs/archive/0.3.17-三剑慢挥与百炼齐射.md | 历史归档 | 出剑 | 每轮最多三把、周期 ×2.5（45~150 帧），确认不恢复 25 槽 | SoulSwingPlayer.cs | 已扫描 |
| docs/archive/0.3.18-动态剑环界面.md | 历史归档 | UI | 寄宿页改为普通 01–10 圆环 + 支援 S1–S5 右下外弧的动态布局 | SoulArmoryLayout.cs | 已扫描 |
| docs/archive/0.3.19-悬浮剑环与柔边暗底.md | 历史归档 | UI | 去掉整窗蓝色底框，改边缘渐隐椭圆暗底 + 窄圆角悬浮条 | SoulUIStyle.cs | 已扫描 |
| docs/archive/0.3.20-共享奖励与逐击飞剑.md | 历史归档 | 槽位 / 矿石族 | F1/F2 改称 R1/R2 共享全收集奖励槽；飞剑由"一次齐射"改为"逐击积攒一把" | SoulItem.cs · OreFlyingSword.cs | 已扫描 |
| docs/archive/0.3.21-四剑挥臂与追加攻速.md | 历史归档 | 出剑 | 四节点（25/50/75/100%）逐把释放，攻速在 0.3.20 基础上再提 10% | SoulSwingPlayer.cs | 已扫描 |
| docs/archive/0.3.22-背后收刀与追加攻速.md | 历史归档 | 出剑 / 渲染 | 飞行结束后追加 10 帧纯视觉收刀，新增 SoulSheathingLayer 夹在人物层前 | （已被 0.3.25 撤销） | 已扫描 |
| docs/archive/0.3.23-归魂吸收与追加攻速.md | 历史归档 | 出剑 / 渲染 | 收刀改为"被人物体内核心吸取"的 12 帧动作 + 实尺寸缩小 | （已被 0.3.25 撤销） | 已扫描 |
| docs/archive/0.3.24-剑尾朝内吸收.md | 历史归档 | 出剑 / 渲染 | 纠正朝向后加半圈，使剑尾朝内、剑尖朝外（点积 < -0.999 作引擎断言） | （已被 0.3.25 撤销） | 已扫描 |
| docs/archive/0.3.25-原版天顶轨迹与回收.md | 历史归档 | 出剑 / 反编译 | 删除 0.3.22~24 的自研收刀，改按原版 `AI_182_FinalFractal` 阶段 0–120，附 tML 哈希与 MVID 证据 | SoulBlade.cs · SoulTrailMotion.cs | 已扫描 |
| docs/archive/0.3.26-原版攻速与飞行粒子.md | 历史归档 | 出剑 | 采用原版 30 帧挥臂 / 10 帧使用间隔、Swing 方式，撤掉四节点与 34 帧下限 | SoulSwingPlayer.cs | 已扫描 |
| docs/archive/0.3.27-六诅咒与个人封印.md | 历史归档 | 诅咒 / 进度 | 六诅咒首版实装：两组/三组 Boss 的阶段表、独行成长、旧档兼容标记 | SoulCurses.cs · SoulBossUnlocks.cs | 已扫描 |
| docs/archive/0.3.28-天顶魂域与传奇切换.md | 历史归档 | UI | 全屏魂域 + 左侧随身剑库（只列背包前 50 格可寄宿剑）+ 原版传奇按钮单人限制 | SoulDomainUI.cs · SoulUILibrary.cs | 已扫描 |
| docs/archive/0.3.29-矿石族阶梯与天铸巨剑.md | 历史归档 | 矿石族 | 矿石 1/2/4/6/8/10 层阶梯表、折返第二趟与天铸巨剑召唤条件 | OreFamily.cs · OreSkyfall.cs | 已扫描 |
| docs/archive/0.3.30-百炼飞剑特效优化.md | 历史归档 | 特效 | 飞剑加边光/局部柔光、剑尖改用原版星芒、出现与齐射闪光 | SwordSpark.cs | 已扫描 |
| docs/archive/0.3.31-鼠标与飞剑圆环修复.md | 历史归档 | UI / 特效 | 根因：魂域筛除原版界面时误删 `Vanilla: Cursor`；飞剑圆环改用统一角度而非各剑年龄 | SoulDomainUI.cs | 已扫描 |

### C 组 · 槽位、支援与伤害审计（0.3.32–0.3.37，5 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/0.3.32-共用支援与矿石伤害审计.md | 历史归档 | 支援 / 矿石族 | 支援改为全族共用池（木质 5 + 矿石 6 累加），确立 150% 基础飞剑伤害 | SoulSupport.cs | 已扫描 |
| docs/archive/0.3.33-传奇槽位.md | 历史归档 | 槽位 | 34 号传奇真实主攻剑位（仅大师 + FTW 且已附着生效），含 0.3.35 的 SupportOrdinal 误判修复 | SoulItem.cs | 已扫描 |
| docs/archive/0.3.34-支援S6与飞剑首击修复.md | 历史归档 | 支援 / 矿石族 | S6 归位到支援弧；取消飞剑共用 10 帧冷却，开放全 Active 主攻与主槽矿材 | SoulSupport.cs · OreFlyingSword.cs | 已扫描 |
| docs/archive/0.3.36-DeepSeek接手审查与修复.md | 历史归档 | 交接 / 审查 | 以交付 0.3.32 源码对照复核 0.3.35：支援密集重叠、传奇保留编号、安装哈希核对 | SoulSupport.cs | 已扫描 |
| docs/archive/0.3.37-切换诅咒四剑例外.md | 历史归档 | 诅咒 / 矿石族 | 切换诅咒未解除时飞剑只取有效主攻序列前 4 把，按真实来源位置识别防重复绕过 | SoulCurses.cs · OreFlyingSword.cs | 已扫描 |

### D 组 · 环境属性、祝福与武技原型（0.3.38–0.3.47，11 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/0.3.38-五祝福与手动御剑.md | 历史归档 | 祝福 | 五祝福实装；御剑必须手动开启（满条不自动消耗），首版参数集中设定 | SoulBlessings.cs | 已扫描 |
| docs/archive/0.3.39-环境属性基础层.md | 历史归档 | 环境 | 21 项环境稳定编号 1–21 与 Id/Key/Name 结构，Values/Traits 全部留空不填假定数值 | EnvironmentAttributes.cs | 已扫描 |
| docs/archive/0.3.39-环境属性基础层-独立复核.md | 历史归档 | 环境 / 审查 | WK 独立重跑编译/回归/哈希复核同一版本，不复用被审方结论 | checks/ | 已扫描 |
| docs/archive/0.3.40-环境之力与主动领域.md | 历史归档 | 环境 | 接入 21 环境的属性、独立机制、收集成长、祝福常驻与主动领域 | EnvironmentEffects.cs · EnvironmentPlayer.cs | 已扫描 |
| docs/archive/0.3.41-环境光环满屏色带修复.md | 历史归档 | 环境 / 渲染 | 根因：MagicPixel 实为 1×1000，缩放被放大千倍形成辐射色带；改为显式裁 1×1 | EnvironmentEffects.cs | 已扫描 |
| docs/archive/0.3.42-剑的所属环境标记.md | 历史归档 | UI / 环境 | 物品悬停追加"所属环境"，未归类显式写"未归类"不虚构归属 | EnvironmentTooltips.cs | 已扫描 |
| docs/archive/0.3.43-武技三档攻击试作.md | 历史归档 | 武技（已删） | 剑势蓄条 + G 启动/V 切换的三档攻击原型，明确只是原型非整族完成 | （0.3.123 已移除） | 已扫描 |
| docs/archive/0.3.44-武技斩击拖影.md | 历史归档 | 武技（已删） | 复用 Luminance 的 PrimitiveRenderer 做连续渐变剑弧与三道渐隐剑影 | （0.3.123 已移除） | 已扫描 |
| docs/archive/0.3.45-原版流光与侵蚀剑弧.md | 历史归档 | 特效 / 武技 | 弃规整色带，改用原版彩虹魔杖流光与侵蚀材质的 VertexStrip 主体 | （0.3.123 已移除） | 已扫描 |
| docs/archive/0.3.46-鬼切参考与碎光粒子.md | 历史归档 | 特效 | 参考灾厄大修鬼切，在流光主体上叠加碎光/火花/渐隐粒子（附包哈希与边界） | Assets/ThirdParty/ | 已扫描 |
| docs/archive/0.3.47-厚月牙与白热刃缘.md | 历史归档 | 特效 / 第三方 | 逐字节复用 OniCrimsonSlash 编译着色器与笔刷/噪声，MIT 许可与逐资源 SHA256 | Assets/ThirdParty/CalamityOverhaul/ | 已扫描 |

### E 组 · 材质与特效迭代（0.3.48–0.3.91，42 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/0.3.48-矿石组流光重制.md | 历史归档 | 特效 / 矿石族 | 用新流光技术重做 14 材质飞剑、天铸巨剑与肉后六矿附属表现 | SwordFlowMaterial.cs | 已扫描 |
| docs/archive/0.3.49-飞剑后拉与真实加速穿刺.md | 历史归档 | 出剑 / 特效 | 区分"动效"与"动作"：末 6 帧真实后拉 22 像素 + 加速穿刺，不改播片 | OreFlightMotion.cs | 已扫描 |
| docs/archive/0.3.50-剑尖破空与独立结束特效.md | 历史归档 | 特效 | 随剑头移动的双层气刃与两侧气流，并给巨剑/附属弹幕独立结束表现 | （已被 0.3.53 取代） | 已扫描 |
| docs/archive/0.3.51-矿石特效体量增强.md | 历史归档 | 特效 | 剑尖气刃与气流线性尺寸 ×2.4、外弧半径 32→76.8、巨剑 80→192 像素 | （已被 0.3.53 取代） | 已扫描 |
| docs/archive/0.3.52-剑体与流光统一.md | 历史归档 | 特效 | 用原版纹理 Alpha/亮度生成中性副本统一着色，消除像素剑与平滑特效的冲突 | （已被 0.3.53 取代） | 已扫描 |
| docs/archive/0.3.53-单层动态燃烧蒙版.md | 历史归档 | 特效 | 用户否定多层拼贴，改为一片连续编译材质贯通尾迹/剑身/剑尖 | VfxSwordMask.cs | 已扫描 |
| docs/archive/0.3.54-灵能族基础层.md | 历史归档 | 灵能族（已删） | 只登记 22 项成员身份与查询接口，机制/数值/支援档位全部留空 | （0.3.123 已移除） | 已扫描 |
| docs/archive/0.3.55-剑形气流与长尾H4.md | 历史归档 | 特效 | H4 候选：放大的剑形只作影响域，逐剑保留配色，21 把矿石剑接入 | VfxSwordMask.cs | 已扫描 |
| docs/archive/0.3.56-飞剑附魔外观与画面缓存保护.md | 历史归档 | 特效 / 稳定性 | 移除飞剑火焰减益外观；记录"放置超级训练假人后世界变黑"的复现与夹具局限 | VfxRenderState.cs | 已扫描 |
| docs/archive/0.3.57-真实轨迹与巨剑材质拖尾.md | 历史归档 | 特效 | 修复 `DrawRibbonShader` 只读首尾角度、丢弃中段位置的问题，改连续材质带面 | SoulMaterialTrail.cs | 已扫描 |
| docs/archive/0.3.58-飞剑流动与收尾打磨.md | 历史归档 | 特效 | 带面按实际剑尖行程展开、静止姿态不生成刃面 | SoulMaterialTrail.cs | 已扫描 |
| docs/archive/0.3.59-自有着色器第一阶段.md | 历史归档 | 材质 | 自写 `ZRSwordFlow.fx`（慢色场/流线/动态暗侧/返回侵蚀），明确不复制 Oni 源码 | Assets/Effects/ZRSwordFlow.fx | 已扫描 |
| docs/archive/0.3.60-撤下旧材质依赖.md | 历史归档 | 材质 | 按"先撤后重建"删除依赖旧着色器的外观，不用纯色块填补 | （0.3.61 部分回滚） | 已扫描 |
| docs/archive/0.3.61-恢复自有剑身蒙版与透光拖尾.md | 历史归档 | 材质 | 纠正误删：H4 是 SpriteBatch 动态蒙版而非旧着色器；噪声改为自生成 512×512 | SwordBodyColorNoise.cs · VfxSwordMask.cs | 已扫描 |
| docs/archive/0.3.62-折射材质与制作端参数.md | 历史归档 | 材质 | 按 IOR 1.5/低粗糙度/低透射实现真折射，并删除玩家可见的调参界面 | SurfaceMaterialDefinition.cs | 已扫描 |
| docs/archive/0.3.63-玻璃背景与反光统一合成.md | 历史归档 | 材质 / 合成 | 修正"先把彩色材质画进世界再折射已被涂色画面"的合成顺序错误 | SoulSceneRefraction.cs | 已扫描 |
| docs/archive/0.3.64-树干轮廓折射与透射解耦.md | 历史归档 | 材质 | 用实测覆盖度中位数 0.533 与位移 2.73/5.81 像素证明前版 PASS 不成立 | SoulSceneRefraction.cs | 已扫描 |
| docs/archive/0.3.65-统一材质核心与独立实验室.md | 历史归档 | 材质 | `SurfaceMaterialDefinition` 作为唯一参数合同 + 独立测试模组与导入导出 | SurfaceMaterialDefinition.cs · SurfaceMaterialRenderer.cs | 已扫描 |
| docs/archive/0.3.69-成熟模组拆解与材质分区.md | 历史归档 | 材质 | 研究成熟模组包与公开源码的边界，确定材质分区与"核心自主"原则 | SurfaceMaterial*.cs | 已扫描 |
| docs/archive/0.3.70-真实剑迹独立消散.md | 历史归档 | 特效 | 修复父弹幕持续写历史导致带面被覆盖缩没，改为真实剑迹独立消散 | SoulTrailEnding.cs | 已扫描 |
| docs/archive/0.3.71-飞剑真实点击与沿迹收尾.md | 历史归档 | 特效 / 验证 | 用真实鼠标按下松开复现，证明 0.3.70 余迹存在但末期难辨 | SoulTrailEnding.cs | 已扫描 |
| docs/archive/0.3.73-四区遮罩原始数据与天顶贴图重做.md | 历史归档 | 材质 | 四区遮罩（刃口/脊线/腹部/磨损）改原始 RGBA 直接上传，四区同时生效 | VfxMaskData.cs | 已扫描 |
| docs/archive/0.3.76-武技三档动作与旋转返航.md | 历史归档 | 武技（已删） | 三档巨剑蓄力后时停突进 + 旋转返航的动作顺序 | （0.3.123 已移除） | 已扫描 |
| docs/archive/0.3.77-飞剑残影与原版采样对齐.md | 历史归档 | 特效 | 剑影连成实心链条的根因：把采样改回 15/30/45 但材质仍铺满 60 点 | SoulTrailUnits.cs | 已扫描 |
| docs/archive/0.3.78-逐剑影独立短尾.md | 历史归档 | 特效 | 单元独立短尾：头点 0/15/30/45，尾窗 13/11/9/7 点，间隙不铺三角形 | SoulTrailUnits.cs | 已扫描 |
| docs/archive/0.3.79-原始包夜间观察.md | 历史归档 | 验证 | 直接运行收藏的原始 0.3.79 包做夜间受控观察（附包 SHA-256） | artifacts/vfx-presets/ | 已扫描 |
| docs/archive/0.3.79-可见剑影实体伤害.md | 历史归档 | 战斗 / 特效 | 三个可见剑影参与实体碰撞，按 0.02 可见阈值判定，短尾仍无伤害 | SoulEchoDamage.cs | 已扫描 |
| docs/archive/0.3.79与0.3.80-独立客户端画面对比.md | 历史归档 | 验证 / 美术 | 同时运行 0.3.79 与 0.3.80 原始包截取画面对比（后续引出 0.3.86 融合版） | artifacts/ | 已扫描 |
| docs/archive/0.3.80-玻璃反射与同源材质R3.md | 历史归档 | 材质 | R3：拆掉独立于法线的坐标亮线，让法线同时决定高光与折射 | GlassMaterialPreset.cs | 已扫描 |
| docs/archive/0.3.80-用户昼夜录像诊断.md | 历史归档 | 验证 / 美术 | 从用户昼夜两段录屏各取 6 帧，定位"白天通透、夜晚灰膜"的具体差异 | GlassMaterialPreset.cs | 已扫描 |
| docs/archive/0.3.81-玻璃流光可见度增强.md | 历史归档 | 材质 | 仅提高自有材质的曝光（6→10）与高光系数（2.2→3.0），不加叠层 | SwordFlowMaterial.cs | 已扫描 |
| docs/archive/0.3.82-能量玻璃与剑身取色.md | 历史归档 | 材质 | 引入实际剑身帧取色 + 整面能量发光（后续被 0.3.83 判定压平反差） | SwordFlowPalette.cs | 已扫描 |
| docs/archive/0.3.83-保留剑色恢复玻璃反差.md | 历史归档 | 材质 | 保留 0.3.82 取色、撤回整面发光，恢复逐通道压缩与弱内部底色 | SwordFlowMaterial.cs | 已扫描 |
| docs/archive/0.3.83-演示与真实鼠标攻击核对.md | 历史归档 | 验证 | 核对正式客户端日志与三份包 SHA256 一致，排除"未更新"归因 | artifacts/install-result.json | 已扫描 |
| docs/archive/0.3.84-指定旧版材质恢复.md | 历史归档 | 材质 | 承认 0.3.83 只回退发光、漏回退反光染色 0.55→0.22 与刃缘 0.30→0.64 | SwordFlowMaterial.cs | 已扫描 |
| docs/archive/0.3.85-特效回退至0.3.79.md | 历史归档 | 材质 / 回滚 | 以 `artifacts/ZenithReborn-0.3.79-source.zip` 为依据整体回退，不按截图估参 | （0.3.90 复用同一依据） | 已扫描 |
| docs/archive/0.3.86-亮刃与玻璃质感融合.md | 历史归档 | 材质 | 以 0.3.80 为基底补回 0.3.79 的薄亮刃缘/内侧细边/窄扫光 | SwordFlowMaterial.cs | 已扫描 |
| docs/archive/0.3.87-融合版沿用79配色.md | 历史归档 | 材质 | 只把新增亮刃与扫光颜色对齐 0.3.79 调色表，不动质感参数 | SoulVisuals.cs | 已扫描 |
| docs/archive/0.3.88-实际剑色与玻璃流纹.md | 历史归档 | 材质 | 从实际剑身帧采样主色（缓存键含物品/帧/纹理），银白剑保持无彩色 | SwordFlowPalette.cs | 已扫描 |
| docs/archive/0.3.89-夜间环境反射灰纹修订.md | 历史归档 | 材质 | 贴图消融实验定位夜间灰白粗纹来源，加暗处同源平滑环境反射 | （已收藏不采用） | 已扫描 |
| docs/archive/0.3.90-以原始79恢复开发基线.md | 历史归档 | 材质 / 基线 | 以 0.3.79 源码为唯一恢复依据，120 项生产文件逐字节一致仅改版本号 | artifacts/ZenithReborn-0.3.79-source.zip | 已扫描 |
| docs/archive/0.3.91-透明主体与有色高光候选.md | 历史归档 | 材质 | 按"主体透明 + 过渡色即高光"思路做的候选，附三色高光导出图，未获美术认可 | SwordFlowMaterial.cs | 已扫描 |

### F 组 · 设计稿、评审与调研（19 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/Boss分组-六方势力.md | 历史归档 | 诅咒 / 设计 | 并行阵营线背景（克眼+月总、克脑+教徒、世吞+骷髅王等），明确本页是势力背景非封印组 | docs/六诅咒-Boss分配与成长节奏.md | 已扫描 |
| docs/archive/Boss分组-四道封印.md | 历史归档 | 诅咒 / 设计 | 全文 3 行的废弃说明：按"幕/封印"线性分法被六方势力取代 | — | 已扫描 |
| docs/archive/六诅咒-Boss分配与成长节奏.md | 历史归档 | 诅咒 / 设计 | 六诅咒与 Boss 组的对应表、成长节奏，含"五种新祝福已采纳"更新头 | SoulCurses.cs | 已扫描 |
| docs/archive/六诅咒重构-切换与独行.md | 历史归档 | 诅咒 / 设计 | 切换与独行的核心限制/解除后能力表，标注以 0.3.27 实现记录为准 | SoulCurses.cs | 已扫描 |
| docs/archive/剑族R2-环境系统后的种族重构.md | 历史归档 | 种族 / 设计 | 环境系统后 103 项名册的种族重构，头部标注 2026-10-30 已删除武技与后续新增种族 | WeaponFamilies.cs | 已扫描 |
| docs/archive/剑族与单剑特性设计.md | 历史归档 | 种族 / 设计 | D1 时代十三族与单剑特性候选表，已被 R2 重构，仅留历史候选 | WeaponFamilies.cs | 已扫描 |
| docs/archive/动态蒙版M1-底色与形状分离.md | 历史归档 | 特效 / 设计 | 隔离模组内的"动态底色 + 形状蒙版"结构演示，未进生产 | VfxSwordMask.cs | 已扫描 |
| docs/archive/开源材质制作方案-2026-10-24.md | 历史归档 | 材质 / 调研 | 调研 Blender Principled BSDF 与开源材质库的复用可行性；头部注明最终未集成 Filament/MaterialX | SurfaceMaterialDefinition.cs | 已扫描 |
| docs/archive/武技族WJ1-剑势与招式开发准备.md | 历史归档 | 武技（已删） | 主修/副修、十把剑各有主动招式的首版草案，用户判定"鸡肋"未采纳 | — | 已扫描 |
| docs/archive/武技族WJ2-三档剑势与天顶巨剑.md | 历史归档 | 武技（已删） | 三档剑势与天顶巨剑的攻击体系设计，头部标注 2026-10-30 武技组已删除 | — | 已扫描 |
| docs/archive/液态玻璃-独立高度场原型.md | 历史归档 | 材质 / 原型 | 检查模组内的平面高度场着色器原型（曲面起伏→法线→反射折射），未接入生产 | SurfaceMaterialRenderer.cs | 已扫描 |
| docs/archive/特效审核-用户反馈以R2为基准-2026-10-23.md | 历史归档 | 美术 / 评审 | 用户原话"钩子巨丑，R2 已经很不错"，确立 R2 为视觉基准并优先于前轮返工要求 | — | 已扫描 |
| docs/archive/特效审核-钛金剑SwordBody-R1-2026-10-23.md | 历史归档 | 美术 / 评审 | R1 审核不通过：箭头式大色块遮住原版剑体、边缘流向不足 | VfxSwordMask.cs | 已扫描 |
| docs/archive/特效审核-钛金剑SwordBody-R2-2026-10-23.md | 历史归档 | 美术 / 评审 | R2 复审仍不通过：接近"原版剑 + 细轮廓 + 波动尾线"，要求单例做 R3 | VfxSwordMask.cs | 已扫描 |
| docs/archive/特效审核-钛金剑SwordBody-R3-2026-10-23.md | 历史归档 | 美术 / 评审 | R3 仍不通过：剑身能量可读性弱于 R2，鼓包变成向后滑动的尖鳍片 | VfxSwordMask.cs | 已扫描 |
| docs/archive/玩家武器偏好调研-2026-10-22.md | 历史归档 | 设计 / 调研 | 检索 r/Terraria 与 r/CalamityMod 真实讨论整理武器偏好，声明非强度榜 | docs/武技族WJ2 | 已扫描 |
| docs/archive/环境属性E4-21机制与共享增幅候选.md | 历史归档 | 环境 / 设计 | 21 种环境机制与 10% 线性增幅候选（已被每环境 20% + 森林 15% 取代） | docs/环境属性E8 | 已扫描 |
| docs/archive/能量薄刃A1-八张贴图验收与制作候选.md | 历史归档 | 特效 / 资产 | 验收 8 张 512×512 灰度图并给 U 向滚动配方（V 频率 0.8、纵向速度 0） | artifacts/vfx-textures/ | 已扫描 |
| docs/archive/需求-天顶飞剑拖尾与剑影形态.md | 历史归档 | 需求 / 特效 | 面向外部模型的需求书：只写现状与期望、不给实现建议，含 116 行现状描述 | SoulTrailUnits.cs | 已扫描 |

### G 组 · 交接、复盘与流程（7 篇）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| docs/archive/DeepSeek开发交接-v0.3.8.md | 历史归档 | 交接 | 0.3.8 期交接：基线、阅读顺序、模块清单与"最小不动点/禁止暂停槽自举"约定 | AGENTS.md | 已扫描 |
| docs/archive/DeepSeek开发交接-v0.3.11.md | 历史归档 | 交接 | 会随版本更新的长交接文件（14 KB），顶部堆叠 0.3.89/0.3.90/0.3.91 状态，正文为 0.3.11 基线 | AGENTS.md · docs/CHANGELOG.md | 已扫描 |
| docs/archive/DeepSeek接手提示词.md | 历史归档 | 交接 | 接手提示词（13 KB），顶部同样堆叠到 0.3.91，正文含协作禁令与证据等级 | CODEBUDDY.md | 已扫描 |
| docs/archive/交接-2026-10-24-2030.md | 历史归档 | 交接 | 一次性交接：四区遮罩已接入、三个新资源进包但后两个未绑定到武器 | VfxMaskData.cs | 已扫描 |
| docs/archive/开发复盘与DeepSeek交接要求.md | 历史归档 | 交接 / 复盘 | 0.2.4 时期复盘：对具体开发行为负责、证据边界、交接要求 | CODEBUDDY.md | 已扫描 |
| docs/archive/第一阶段计划-史莱姆王与附着.md | 历史归档 | 设计 / 剧情 | 0.2.x 第一阶段计划（被庇护者的回报），记录附着玩法的原始目标 | AttachmentStory.cs | 已扫描 |
| docs/archive/临时试用原始0.3.80.md | 历史归档 | 验证 / 版本管理 | 保存 0.3.88 到 `artifacts/vfx-presets/0.3.88/` 后临时换装原始 0.3.80 的操作与哈希核对 | artifacts/vfx-presets/ | 已扫描 |

**统计**：A 13 + B 19 + C 5 + D 11 + E 42 + F 19 + G 7 = 116（已用脚本比对 `docs/archive/*.md` 实际文件名集合，无遗漏、无重复、无多余）。E 组内 0.3.79/0.3.80/0.3.83 各有两篇同日不同主题记录，F 组含 4 篇非版本号审核稿。

---

## 第一部分附 · 归档里仍有参考价值的设计

以下条目虽已归档，但其结论仍是当前实现的直接依据，后续查证可直接引用（给 `文件:行号`）。

| # | 位置 | 仍有效的内容 |
|---|---|---|
| 1 | docs/archive/0.3.33-传奇槽位.md:9 | 传奇剑位生效条件 = `Main.GameMode==2 && Main.getGoodWorld`（大师 + FTW）且已附着；与 `SoulItem.cs:17 LegendarySlotId=34` 一致，0.3.129/130 也未改此规则 |
| 2 | docs/archive/0.3.33-传奇槽位.md:5 | `SupportOrdinal(34)` 必须返回 -1（传奇是主攻位不是支援位）；`SoulItem.cs:28` 现同时排除 34 与圣林槽，是本条的延伸 |
| 3 | docs/archive/0.3.27-六诅咒与个人封印.md:9 | 六诅咒 × Boss 组 × 阶段（诅咒/削弱/解除/祝福）的原始对应表；现行 `SoulCurses.cs:17-18` 名称与祝福名仍与此表同源 |
| 4 | docs/archive/0.3.37-切换诅咒四剑例外.md:5 | "切换未解除只算有效主攻序列前 4 把，按真实来源位置识别、重复同类武器不能绕过"——`AGENTS.md:39` 仍以此为准 |
| 5 | docs/archive/DeepSeek开发交接-v0.3.8.md:62 | 防自举约定原文："全套……最小不动点计算，不允许暂停支援自举解锁"；0.3.129 的 `CrownHosts` 防自举（docs/0.3.129-木质族V2与复合羁绊.md:19）是同一原则的延续 |
| 6 | docs/archive/0.3.78-逐剑影独立短尾.md:13 | 剑影采样布局 0/15/30/45 + 尾窗 13/11/9/7 的权威说明；`SoulTrailUnits.cs` 至今沿用，改任何剑影参数前必读 |
| 7 | docs/archive/0.3.79-可见剑影实体伤害.md:16 | `SoulBlade.cs:150` 的 `TryGetEchoPose` 作为绘制与碰撞共用入口，0.02 可见阈值；现行 `SoulEchoDamage.cs:6` 的 0.20/0.80 倍率建立在该入口之上 |
| 8 | docs/archive/0.3.41-环境光环满屏色带修复.md:6 | MagicPixel 实为 1×1000 而非 1×1 的教训，以及"显式裁 1×1 再缩放"的正确画线写法；新增任何 Primitive 画线都可能重犯 |
| 9 | docs/archive/0.3.25-原版天顶轨迹与回收.md:6 | 原版 `AI_182_FinalFractal` 阶段划分与 tML 哈希/MVID 证据；现行主飞剑阶段 0–120 的出处 |
| 10 | docs/archive/0.3.46-鬼切参考与碎光粒子.md:5 | 第三方资源来源包哈希 + MIT 许可 + 逐资源 SHA256 的记录格式；`Assets/ThirdParty/` 下 provenance.json 的模板 |

---

## 第二部分 · 三份根文档的现状分析

### 2.1 `AGENTS.md`（57 行）

**章节结构**

| 行段 | 内容 |
|---|---|
| 1–19 | 版本状态区（L3 当前 0.3.130；L5 0.3.129；L7 0.3.128；L9 0.3.127；L11 0.3.126；L13 0.3.123；L15 历史段外链；L17 VfxLibrary；L19 调度规则与 CODEBUDDY 导入关系） |
| 21–30 | 长期开发硬规则：模块职责与代码归属（"先找归属再写代码"） |
| 32–35 | 索引指针 + 中文注释硬规则 |
| 36–57 | 逐条历史决策流水（0.3.27 至 0.3.39 的技术规则，每条一段） |

**当前记录的版本状态段**：L3 声称"当前正式安装 0.3.130"，附备份路径 `artifacts/backups/ZenithReborn.tmod.bak-20261103-134149` 与 sha256 前缀 `01FB0890…92FDB`、`run_wood_checks PASS 8498 项`。`build.txt` 与 `artifacts/install-result.json` 的 version 均为 0.3.130（一致），但 install-result 记录的是更晚一次安装（backup `20261103-225935`、sha256 `7b246fda…c831`、`focused_checks: 8511`）——即 L3 描述的是 13:41 那次安装，不是仓内最新一次。

**硬规则清单（可执行部分）**

1. 模块职责与代码归属（L23–L30）：先找归属、职责分开、同类集中、共享有归属、依赖接口清楚、测试与模块对应、交付前检查组织是否被破坏。
2. 代码注释必须中文，标识符保持英文（L34）——README.md:7 重复强调。
3. 六祝福与御剑：充能存 `SoulItem`、满条不衰减、仅手动开启（默认 O）、禁止自动触发；`LegacyReleased` 不授予祝福（L38）。
4. 切换诅咒下飞剑只算前 4 把（L39）；传奇槽 34、支援编号必须跳过 34（L40）。
5. 保留逐剑独立伤害、不分摊、原槽位索引与词缀保护（L46、L48）。
6. 不得凭世界 Boss 全局旗标伪造剑魂成长记录（L51）。
7. UI 输入必须在底层界面/世界处理前拦截（L54）。
8. 正式存档与隔离测试分开、先备份、文件被锁时等玩家退出、不得强杀游戏或关机（L56）。
9. 提交范围：仅源码/文档/检查脚本/脱敏结果；不提交人物世界、测试配置、完整日志、反编译文件、凭据；不改仓库可见性（L57）。

**会随版本变旧的具体行（已过期或半过期）**

| 行 | 问题 |
|---|---|
| L7 | 128 段写"飞刃族名册前四把各带阶梯效果（蓝相位剑激活族…）`Enabled` 已改为必须寄宿蓝相位剑"，与最终实现（按族内数量 1/2/4/6/8/10 分阶，`Enabled` 只看附着 + TraitMains + Stage≥1）不符；且链接 `docs/0.3.128-族归属显示与阶梯四把.md` **文件不存在**（实际文件名为 `0.3.128-族归属显示与数量分阶.md`） |
| L11 | "当前正式安装 0.3.126"——与 L3 的 0.3.130 冲突，只是历史段落未改标题 |
| L13 | "当前正式安装 0.3.123"——同上冲突 |
| L37 | "当前源码基线为 0.3.127"——实际 0.3.130 |
| L47 | "木质全套也能解锁同两格（共享全收集奖励槽）"——0.3.129 起 `ForestHosts` 仅矿石全收集解锁，木质改走 `CrownHosts` 35/36；本条已被 L5 自身推翻 |
| L3 | 回归项数 8498 与最新 install-result 的 8511 不符（同一版本多次重装导致） |

### 2.2 `README.md`（116 行）

面向玩家的功能说明，结构：版本头（L1–L7）→ 从开局开始（L11–L19）→ 核心系统（L23–L77：剑魂与槽位 / 六诅咒 / 出剑与伤害 / 祝福与御剑爆发 / 种族 / 环境之力）→ 构建与验证（L80–L91）→ 文档导航（L95–L112）→ 下载（L116）。

**与当前实现的差距（已 Grep 验证，详见第三部分）**

1. L1/L3 版本头写 0.3.128，实际 0.3.130；且 L3 把飞刃族描述为"名册前四把各带一层阶梯效果"，实际为数量分阶（`FlyingBladeFamily.cs:29-31`）。
2. L71 木质族"12 把木剑、强度 1.10/1.20/1.35/1.50/1.75/3.00、生命 5/10/15/20/30/100"——实际 8 把、1.10/1.20/1.35/1.50/2.00/3.00、5/10/15/25/40/100（`WoodTypes.cs:10,20-23`）。
3. L33 奖励槽条目"木 12 种全套 或 矿石 14 材质集齐"——`BonusCapacity` 现在仅 `OreMastered` 时给 2（`SoulItem.cs:57`），木质全套改为解锁圣林归冠双真实剑位（README 完全未提 Crown 35/36）。
4. L5 回归项数 8453、L99 "CHANGELOG 全部 104 条"——实际 install-result 记 8511，CHANGELOG 现有 107 行条目。
5. L71 森林成长只写"满 300 帧涨 1 层，上限 12"，漏掉基础上限 6（圣林归冠才提到 12）与脱战每 120 帧 -1 层（`ForestPower.cs:14-18`）。

**仍然准确的部分**（抽查）：L34 传奇槽 ID 34（`SoulItem.cs:17`）、L37 效能公式（`SoulItem.cs:89`）、L49 独行 2/3/4/最多 10（`SoulCurses.cs:54-56`）、L50 断锋本体 100%/剑影 20%（`SoulEchoDamage.cs:6`）、L59 命中冷却 8 帧、L60 剑影 15/30/45 参与碰撞、L61 断锋解除后 ×0.8、L67 御剑 45 帧内蓄到 900、O 键 600 帧（`SoulBlessings.cs:17`）、L13 残魂配方 5 铜锭 + 1 坠落之星（`SoulItem.cs:487`）、L17 基础射程 192（`SoulItem.cs:423`）。

### 2.3 `CODEBUDDY.md`（95 行）

**导入了什么**：L1 `@AGENTS.md`——这是唯一让 AGENTS.md 在编辑器里被加载的机制（两文件并存时编辑器只认 CODEBUDDY.md，见 L5 自述）。

**指向了哪些文件**

| 行 | 指向 | 状态 |
|---|---|---|
| L7 | `README.md`、`.workbuddy/memory/MEMORY.md` | 均存在 |
| L8 | `.codebuddy/rules/00-delegation.md` | 存在 |
| L14 | `ZenithReborn/build.txt` | 存在，但文件中写的版本号是 0.3.128（`build.txt` 实为 0.3.130）→ **已失效** |
| L25 | `Luminance.dll`、`checks/prepare_luminance.py` | 存在 |
| L31–L38 | 目录职责表（`ZenithReborn/`、`checks/`、`docs/`、`VfxLibrary/`、`tools/`、`artifacts/`、`*-profile/`、`.workbuddy/`） | 目录均在；但"112 个 `.cs`"实际为 121 个 → **计数过期**【历史快照标注：本表为 2026-11-03 调查快照（历史快照）；上述"112 个 / 121 个"旧计数为当时快照，保留原数字、不改为新值】 |
| L47/L50 | `checks/run_wood_checks.ps1`、`checks/check_mod_assets.py` | 存在 |
| L61–L63 | `docs/CHANGELOG.md`（称 104 条）、`docs/archive/`、`build.txt` | CHANGELOG 实为 107 条 → 计数过期 |
| L84 | `artifacts/install-result.json` | 存在，`graphics_*_tested` 仍为 `false`（与其自述一致） |
| L85 | `FlyingBladeMark.cs` | **不存在**（源码目录无此文件）→ 断链，与"已入库留档"的说法矛盾 |
| L87 | `FlyingBladeShot` / `HolyHeavenStar` / `HolyHeavenImpact` / `LivingArmament` 中文名未定 | 四类均存在（`FlyingBladeShot.cs` 等），描述仍成立 |
| L91–L95 | `README.md`、`AGENTS.md`、`MEMORY.md`、`00-delegation.md`、`docs/0.3.*.md` | 均存在 |

**无失效指针的结论**：`CODEBUDDY.md` 只有两处硬失效——L14 的版本号、L85 的 `FlyingBladeMark.cs`；其余为计数类过期（112 个 .cs / 104 条 CHANGELOG / 8453 项回归）。

### 2.4 `.gitignore`（30 行）

| 规则 | 作用 |
|---|---|
| `**/bin/` `**/obj/` | 编译中间产物 |
| `build-profile/` `check-profile/` `wood-check-profile/` `flying-blade-profile/` `stage-one-playtest/` `environment-render-profile/` `martial-playtest/` `.smoke-user-data/` | 各时期隔离运行目录，**逐个硬编码**（新增隔离目录必须手动加规则，否则会误入库） |
| `artifacts/` | 构建/安装/检查产物与备份，目录约 16 GB，整目录不入库 |
| `**/__pycache__/` | Python 缓存 |
| `*.wld` `*.twld` `*.plr` `*.tplr` | 人物与世界存档（对应提交硬规则 L57） |
| `*.bak` `*.log` | 备份与日志 |
| `.env` `.env.*` `*.pem` `*.key` | 凭据（对应 L57"不提交凭据"） |
| `tools/ilspy/` `tools/decomp/` | 反编译工具与输出，约 22 MB，可由 `tModLoader.dll` 重新生成 |
| `tools/xnbcli-binary/` `tools/xnbcli-windows-x64.zip` | XNB 解包二进制与下载包约 50 MB，脚本源码 `tools/xnbcli/` 保留 |
| `.workbuddy/` | 本地长期记忆与工作区状态 |

**入库策略结论**

- `artifacts/` 整目录忽略 → 但根文档大量引用其中的 `artifacts/ZenithReborn-0.3.79-source.zip`、`artifacts/vfx-presets/0.3.88/`、`artifacts/install-result.json`。**这些证据在克隆后不存在**，归档与根文档中"包哈希/回退依据"类引用对外部读者不可复现（0.3.85、0.3.90、临时试用原始0.3.80 三篇受影响最大）。
- `VfxLibrary/` **未**被忽略 → 67 MB 原版素材（3461 张 PNG）按 CODEBUDDY.md:34 入库，与 `.gitattributes` 里 `*.png binary` 配合。
- 隔离目录靠白名单枚举，`martial-playtest/` 是被删武技组遗留的规则（0.3.123 后已无用），属残留规则。
- `*.tmod` 未被忽略，但在仓库内只可能来自 `artifacts/`（已忽略），实际等价于不入库。

### 2.5 `.gitattributes`（10 行）

`* text=auto` 全局按文本处理；对 `*.cs` `*.csproj` `*.hjson` `*.md` `*.py` `*.ps1` 显式 `text eol=lf`（保证跨平台与 pwsh 7 脚本可解析，呼应 CODEBUDDY.md:23 的"无 BOM UTF-8"要求）；`*.tmod` `*.zip` `*.png` 标记 `binary`。作用单一：**强制 LF，防止 Windows 换行污染与脚本解析失败**。

---

## 第三部分 · 根文档与代码的事实核对

| # | 文档声称（位置） | 源码/产物实际（位置） | 结论 |
|---|---|---|---|
| 1 | README.md:71 木质族"12 把木剑。1/2/4/6/8/全套 → 1.10/1.20/1.35/1.50/1.75/3.00，生命 5/10/15/20/30/100" | `WoodTypes.cs:10` `enum WoodKind` 仅 8 项；`WoodTypes.cs:29-31` `Types` 为 8 把原版木剑；`WoodTypes.cs:20-23` 强度 `1.10/1.20/1.35/1.50/2.00`、归冠 3.00，生命 `5/10/15/25/40`、归冠 100 | **过期**（0.3.129 V2 重做后的数值与名册都变了） |
| 2 | README.md:33 奖励槽"木 12 种全套 **或** 矿石 14 材质集齐"；AGENTS.md:47"木质全套也能解锁同两格" | `SoulItem.cs:57` `BonusCapacity => Attached && OreMastered ? 2 : 0`——只有矿石全收集给 2 格；木质全套改用 `CrownCapacity`（`SoulItem.cs:60`，`WoodCrown.Active` 判定），圣林槽 35/36 | **过期**（这是 0.3.129 的核心改动，AGENTS 自身 L5 与 L47 互相矛盾） |
| 3 | AGENTS.md:7 与 README.md:3 飞刃族"名册前四把各带阶梯效果（蓝相位剑激活族／红相位剑放大伤害／绿相位剑跨族授权／紫相位剑接管收回期伤害）"，"`Enabled` 已改为必须寄宿蓝相位剑" | `FlyingBladeFamily.cs:29-31` 门槛 `Stages=[1,2,4,6,8,10]`，注释 L8-13 明确"激活只看数量，不绑定任何特定武器"；`FlyingBladeFamily.cs:64` 四个开关 `Activated/Tally/Pass/Sigil` 全部按 `Stage(Count)` 判定；`FlyingBladeFamily.cs:73` `Enabled => Attached && TraitMains.Any() && Activated(soul)` | **过期**（0.3.128 内部已被"数量分阶"方案取代，两处文档都停在中间态） |
| 4 | AGENTS.md:37"当前源码基线为 0.3.127"；CODEBUDDY.md:14"当前版本 **0.3.128**（以 `ZenithReborn/build.txt` 为准）"；README.md:1 标题"0.3.128" | `ZenithReborn/build.txt` `version = 0.3.130`；`artifacts/install-result.json` `version: 0.3.130, installed: true` | **过期**（三份根文档全部落后 build.txt 两个版本） |
| 5 | CODEBUDDY.md:31"`ZenithReborn/`（112 个 `.cs`…）"；L63"`docs/CHANGELOG.md` 是版本索引（104 条）"；L54 回归"`PASS: 8453 focused checks`"；README.md:99"全部 104 条" | `ls ZenithReborn/*.cs` = 121；`grep -c "^| 0\.3\." docs/CHANGELOG.md` = 107；`artifacts/install-result.json` `focused_checks: 8511` | **过期**（三处计数全部落后；0.3.130 自身新增 `SoulUIWoodPanel.cs` 等文件） |
| 6 | AGENTS.md:7 链接 `docs/0.3.128-族归属显示与阶梯四把.md`；CODEBUDDY.md:85"草案 `FlyingBladeMark.cs` 已入库留档" | `docs/0.3.128-族归属显示与数量分阶.md` 才是实际文件名（标题"族归属显示与飞刃族数量分阶"）；`ZenithReborn/` 下无 `FlyingBladeMark.cs` | **断链 / 失效指针** |
| 7 | README.md:34"传奇槽（槽 ID 34）… 大师 + For the worthy 才生效"；L37"效能 `0.65 + Stage×0.04`，觉醒 1.1"；L67"45 帧内蓄能至 900，`O` 手动开启 600 帧" | `SoulItem.cs:17` `LegendarySlotId = 34`，`SoulItem.cs:89` `Potency => … 1.1f : .65f + Stage*.04f`；`SoulBlessings.cs:17` `ChargeFrames=900, BurstFrames=600, CombatGrace=45` | **一致** |
| 8 | README.md:50 断锋"本体 100%、剑影各 20%；同剑同目标原有上限 3 次（削弱态 5）"；L61"0.3.105 起断锋解除后剑影逐次乘 0.8" | `SoulEchoDamage.cs:6` `CursedMultiplier=.20f, ReleasedStep=.80f`，`Multiplier` 按 `CurseStage` 分段；`SoulCurses.cs:58` `HitLimitAtStage` 3/5/∞ | **一致** |
| 9 | README.md:32 支援槽"基础 5 + 动态扩展，上限 11；木质最多 5、矿石最多 6，累加" | `SoulSupport.cs:9-10` `Grants` 两族门槛 `[1,2,4,6,8]`（5 格）与 `[1,2,4,6,8,10]`（6 格）；`SoulItem.cs:14` `SupportHosts = EmptySlots(11)` | **一致** |
| 10 | AGENTS.md:3"run_wood_checks PASS 8498 项"、"备份 `artifacts/backups/ZenithReborn.tmod.bak-20261103-134149`"、"sha256 01FB0890…92FDB" | `artifacts/install-result.json` 最新一次：`focused_checks: 8511`、`backup: …/20261103-225935`、`sha256: 7b246fda…c831`；备份目录中 13:41 与 22:46/22:59 两批并存 | **部分过期**（描述的是同日更早一次安装，非仓内最新状态） |

---

## 第四部分 · 未确认项

1. **0.3.128 的"四把阶梯"是否曾真实存在**：AGENTS.md:7 与 README.md:3 都描述蓝/红/绿/紫相位剑各担一职，但源码里没有任何按具体武器分支的阶梯逻辑（`FlyingBladeFamily.cs` 全文按数量）。无法判断这是"设计中间态被文档记录"还是"实装后又被 0.3.128 自身替换"。需要查 0.3.128 的 git 历史或该版安装包才能定论。
2. **`artifacts/` 不入库导致的可复现性缺口**：0.3.85/0.3.90/临时试用原始0.3.80 三篇均以 `artifacts/ZenithReborn-0.3.79-source.zip`、`artifacts/vfx-presets/0.3.88/` 为唯一恢复依据。这些目录被 `.gitignore` 整目录排除，克隆仓库后无法按文档复现；未核对 GitHub 远端是否另有发布。
3. **archive 中大量 0.3.92–0.3.111 段落缺失**：归档编号从 0.3.91 直接跳到当前 0.3.130，`液态玻璃-独立高度场原型.md` 却提到"正式安装仍为 0.3.111"。0.3.92–0.3.122 的逐版本记录只存在于 `docs/`（如 0.3.120~0.3.125），不在 archive，两处边界如何划分未在文档中说明。
4. **`DeepSeek开发交接-v0.3.11.md` 与 `DeepSeek接手提示词.md` 的"活文档"性质**：两者正文基线是 0.3.11，但顶部被追加了 0.3.78/0.3.89/0.3.90/0.3.91 的状态段，形成"归档文件仍在被更新"的状态。本轮只确认了顶部段落，未逐段核对正文与 0.3.130 的一致性（该文件 14 KB / 13 KB，且 AGENTS.md:36 仍要求"先读这两份再读源码"）。
5. **`.workbuddy/` 与 `.codebuddy/` 的分工**：`.gitignore` 忽略 `.workbuddy/`（含 CODEBUDDY.md:7 声称的长期记忆 `MEMORY.md`），但 `.codebuddy/rules/00-delegation.md` 未在忽略列表内。两者一个入库一个不入库的策略差异未在任何根文档中说明。
6. **README 称"环境之力 21 种"**：已确认 `EnvironmentAttributes.cs:16` 枚举到 21（Dungeon=21），但 21 项的实际效果是否全部在 0.3.130 生效未逐项核对（属 E 组其他代理范围）。
