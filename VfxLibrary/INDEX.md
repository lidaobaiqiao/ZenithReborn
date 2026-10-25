

> 当前源码0.3.91为[ClearHighlightR1候选](Materials/SwordTrail/Glass/ClearHighlightR1/entry.json)：79基底的透明主体＋曲面有色高光，保留薄亮刃，不新增贴图。扇形分段/重复轮廓未解决，美术未确认、未安装；90原始79基底已保存，正式仍80。见[91记录](../docs/0.3.91-透明主体与有色高光候选.md)。

> 当前开发基线已按用户要求恢复原始79（开发标记0.3.90）；80 R3与89 NightClean均退出生产，89取消待安装、仅收藏。正式磁盘仍原始80。本轮只恢复基底，没有完成新Shader或剑身取色，见[90记录](../docs/0.3.90-以原始79恢复开发基线.md)。下方历史配方启用状态不代表当前开发基底。

> 0.3.89当前开发配方：[NightCleanR1](Materials/SwordTrail/Glass/NightCleanR1/entry.json)，原始80为基底，暗处平滑环境图并响应线性照明，明亮输入与80差0。全部旧素材和79/80/88收藏保留，美术未确认。见[贴图消融与修订](../docs/0.3.89-夜间环境反射灰纹修订.md)，实际安装看最新记录；下方历史启用状态不代表当前版本。

> 当前正式安装已按用户要求临时切换为原始0.3.80，0.3.88源码/素材/安装包已独立保存。开发源码继续保留88，下述88验证不适用于当前安装80。见 [保存88并试用原始80](../docs/临时试用原始0.3.80.md)。


> 0.3.88接续用户「所有武器都是一个特效」反馈，默认主飞剑改为实际剑身取色，内部流纹/扫光/反射保色，保留79式薄白刃芯与80玻璃基底。8393项引擎、七剑生产GPU、固定输入光学对照、默认及正式五模组组合隔离世界和三个剑影伤害通过；自然鼠标/真实多人未测，美术待用户确认。见 [88实际剑色修订](../docs/0.3.88-实际剑色与玻璃流纹.md)。以下颜色输入完全沿用79的记录属于历史。

> 0.3.87按用户要求将融合版颜色锁定为0.3.79方案：逐剑基础配色、主体/刃缘输入与高光混色均已核对；86的玻璃质感、光效宽度/强度及攻击保持。见 [../docs/0.3.87-融合版沿用79配色.md](../docs/0.3.87-融合版沿用79配色.md)。以下版本状态为历史。


> 0.3.86按用户要求完成第三版：0.3.80玻璃弧面/反射/折射基底＋参考0.3.79的薄亮刃缘及窄扫光。同一材质中局部补光；固定GPU法线/光学/透射/Alpha与0.3.80差异均0。8393项引擎、材质GPU、三剑世界及三个剑影实际伤害通过；未测自然鼠标/完整组合/多人，美术待用户确认。原79/80收藏保留。见 [../docs/0.3.86-亮刃与玻璃质感融合.md](../docs/0.3.86-亮刃与玻璃质感融合.md)。以下旧版状态为历史。

> 用户对两版的最新判断：0.3.79光效不错；0.3.80质感很好但太暗淡。已运行原始包独立客户端抓帧，真实AI位置/角度逐项相同；当前正式特效不变。详见[两版画面对比](../docs/0.3.79与0.3.80-独立客户端画面对比.md)。

> 用户已单独收藏原始0.3.80玻璃R3效果，作为以后可选方案；当前不启用。见[独立快照](Materials/SwordTrail/Glass/R3/Snapshots/0.3.80/README.md)；来源和逐文件哈希见同目录manifest.json。


> 0.3.85按用户最新要求将特效完整恢复为0.3.79（包括原配色）。120项生产文件与0.3.79归档逐字节一致，仅版本号不同；0.3.80至0.3.84材质已备份，R3退出生产。8393项引擎通过；本轮图形客户端因Steam会话校验失败未能启动，不能沿用历史GPU/世界结果充当本轮通过。见 [../docs/0.3.85-特效回退至0.3.79.md](../docs/0.3.85-特效回退至0.3.79.md)。以下0.3.80至0.3.84状态为历史记录。

> 当前主飞剑材质：[0.3.84按用户指定旧版恢复](../docs/0.3.84-指定旧版材质恢复.md)。原Shader/反光参数回到0.3.81，四组固定GPU像素一致，逐剑取色仍为0.3.82。用户图是本项目旧成果，不能当成待设计的新素材。

> 用户最新选择：0.3.82颜色保留、质感回到0.3.81基准。[0.3.83恢复玻璃反差](../docs/0.3.83-保留剑色恢复玻璃反差.md)撤下面状强发光，保留剑身取色；不能把0.3.82的发光总成视为认可配方。

> 主飞剑R3当前配方：[0.3.82能量玻璃与剑身取色](../docs/0.3.82-能量玻璃与剑身取色.md)。材质颜色来自实际剑身帧，七剑实际GPU取色与三剑世界画面已核对；成熟成品的明暗/色彩对照见References/能量玻璃-成品对照-2026-10-25.md。

> R3最新调校：[0.3.81玻璃流光可见度增强](../docs/0.3.81-玻璃流光可见度增强.md)，沿用现有全部贴图，材质固定GPU亮度提高31%–33%，Alpha不变。

> [玻璃材质R3](Materials/SwordTrail/Glass/R3/entry.json)：内置生图环境/灰度原稿与同源数据图已接入。默认GPU、45组材质对照和隔离世界通过，正式安装看最新记录；美术待用户判断。见[实现和预览](../docs/0.3.80-玻璃反射与同源材质R3.md)。

> 0.3.78改为[逐剑影独立短尾](../docs/0.3.78-逐剑影独立短尾.md)：本体与原三个剑影各有13/11/9/7点短尾，保留采样和材质；结束不再冻结整条带面。旧剑影更短、更暗、更早收尽，素材不重做。

> 0.3.71修订[飞剑沿迹收尾](../docs/0.3.71-飞剑真实点击与沿迹收尾.md)：结束蒙版使用独立归一化轨迹年龄，从旧尾收向剑尖，正常36帧、提前24帧。同四模组组合隔离真实点击已观察到父弹幕消失后的收尾；活动期和素材保持，视觉待用户判断。

> 0.3.70补齐[真实剑迹独立消散](../docs/0.3.70-真实剑迹独立消散.md)：沿用已接入素材和主体，返回后保留真实轨迹再侵蚀熄灭，父弹幕提前销毁也能收尽。活动期六张实际GPU图逐像素保持；默认/分区材质的隔离世界验证通过。

> 新增[能量薄刃A1·泰拉之刃制作候选](Materials/Surface/TerraBlade/EnergyBladeA1/entry.json)：用户八张贴图已验收、生成法线与表面图、打包四区数据并完成独立GPU回放；尚未替换正式武器外观。见[验收与预览说明](../docs/能量薄刃A1-八张贴图验收与制作候选.md)。

> 0.3.69完成[成熟模组实物拆解](References/MatureVfx-20261024/entry.json)，并在自有材质接入RGBA分区、局部粗糙度/金属度/自发光、细节层继承与导出导入。45组GPU与四区隔离通过；通道打包和语义mip工具已有离线产物，尚未作为运行端新布局/mip启用。见[本轮记录](../docs/0.3.69-成熟模组拆解与材质分区.md)。

> 0.3.65新增[统一表面材质](Materials/Surface/entry.json)与[独立制作实验室](../tools/ZenithMaterialLab/README.md)，实际GPU逐项对照与工作台操作通过；默认剑的材质绑定保持，实验室不进入正式玩家界面。

> 0.3.62：主飞剑玻璃[制作端预设](Materials/SwordTrail/Glass/preset.json)，只在制作时调参；矿石H4自有蒙版保留，玩家没有材质设置面板。

> 当前0.3.61：21把矿石剑自有蒙版合成已恢复，只将借用噪声改为数学生成；主飞剑为自有透光玻璃拖尾。第三方着色器／笔刷／噪声仍不在生产包中。以下0.3.60停用状态属于历史，见[本轮记录](../docs/0.3.61-恢复自有剑身蒙版与透光拖尾.md)。

> 当前0.3.60：旧第三方材质外观已撤下，以下历史制作／接入状态以[迁移记录](../docs/0.3.60-撤下旧材质依赖.md)为准。普通主飞剑使用自有着色器，矿石剑身图集保留待接入；旧色场资源不再启用。

# 特效库索引

当前0.3.59主飞剑使用[自有剑流着色器](Shaders/SwordFlow/entry.json)、[自有动态形状](Masks/SwordTrail/_Shared/ZRSwordFlow/entry.json)与[程序慢底色](ColorFields/SwordTrail/_Shared/ZRSlowField/entry.json)。默认无第三方贴图，原版剑体与粒子保留；巨剑与矿石其他路径仍待迁移。见[美术接入与替换计划](Shaders/SwordFlow/美术接入与替换计划.md)。

[0.3.58历史主飞剑材质参考](Masks/SwordTrail/_Shared/OniTrajectory/entry.json)：连续带面的运行时共用引用，逐剑配色，复用原始编译材质。与矿石H4剑身一体蒙版分属不同调用路径，尚待用户美术评价。

## 参考拆解（别人为什么好看）

- [源码核对与复用要点](References/OniCrimsonSlash-源码核对与复用要点.md)：对照实际模式、时间驱动和现有接入参数，修正厚度指数、侵蚀阈值及功能启用状态；保留单底色＋动态蒙版原则。
- [CalamityOverhaul 0.9209 材质资源清单](References/CalamityOverhaul-材质清单.json)：已校验容器的源文件／编译效果索引，尚未作为额外素材复制或作为预设验收。

- [OniCrimsonSlash 拆解](References/OniCrimsonSlash-拆解.md)：Calamity Overhaul 的刀光 shader 逐层分析——不对称边缘、扫掠揭示、双层侵蚀、刃线最后死、散锋、飞白、透密调制。结论：每个视觉特征都被定义成语义，没有随手调的参数。

钛金剑剑身一体组当前选择用户偏好的[R2视觉基准](Masks/SwordBody/TitaniumSword/R2/README.md)，R3／R4及钩子衍生研究已撤回；21把矿石剑已按R2配方家族批量制作自动配准候选（见[铜短剑R2](Masks/SwordBody/CopperShortsword/R2/authoring.json)），技术校验通过、未经美术审核、未接入游戏；天铸巨剑无原版贴图，蒙版待制作；圆环与粒子逐剑素材仍待制作。用户偏好与批量制作都不等于正式包已安装或实战验收。

| 剑名 | 剑身一体蒙版 | 圆环蒙版 | 粒子蒙版 | 底色入口 |
|---|---|---|---|---|
| 铜短剑 | [剑身一体组](Masks/SwordBody/CopperShortsword/entry.json) | [圆环](Masks/Rings/CopperShortsword/entry.json) | [粒子](Masks/Particles/CopperShortsword/entry.json) | [剑身](ColorFields/SwordBody/CopperShortsword/entry.json) / [圆环](ColorFields/Rings/CopperShortsword/entry.json) / [粒子](ColorFields/Particles/CopperShortsword/entry.json) |
| 铜宽剑 | [剑身一体组](Masks/SwordBody/CopperBroadsword/entry.json) | [圆环](Masks/Rings/CopperBroadsword/entry.json) | [粒子](Masks/Particles/CopperBroadsword/entry.json) | [剑身](ColorFields/SwordBody/CopperBroadsword/entry.json) / [圆环](ColorFields/Rings/CopperBroadsword/entry.json) / [粒子](ColorFields/Particles/CopperBroadsword/entry.json) |
| 锡短剑 | [剑身一体组](Masks/SwordBody/TinShortsword/entry.json) | [圆环](Masks/Rings/TinShortsword/entry.json) | [粒子](Masks/Particles/TinShortsword/entry.json) | [剑身](ColorFields/SwordBody/TinShortsword/entry.json) / [圆环](ColorFields/Rings/TinShortsword/entry.json) / [粒子](ColorFields/Particles/TinShortsword/entry.json) |
| 锡宽剑 | [剑身一体组](Masks/SwordBody/TinBroadsword/entry.json) | [圆环](Masks/Rings/TinBroadsword/entry.json) | [粒子](Masks/Particles/TinBroadsword/entry.json) | [剑身](ColorFields/SwordBody/TinBroadsword/entry.json) / [圆环](ColorFields/Rings/TinBroadsword/entry.json) / [粒子](ColorFields/Particles/TinBroadsword/entry.json) |
| 铁短剑 | [剑身一体组](Masks/SwordBody/IronShortsword/entry.json) | [圆环](Masks/Rings/IronShortsword/entry.json) | [粒子](Masks/Particles/IronShortsword/entry.json) | [剑身](ColorFields/SwordBody/IronShortsword/entry.json) / [圆环](ColorFields/Rings/IronShortsword/entry.json) / [粒子](ColorFields/Particles/IronShortsword/entry.json) |
| 铁宽剑 | [剑身一体组](Masks/SwordBody/IronBroadsword/entry.json) | [圆环](Masks/Rings/IronBroadsword/entry.json) | [粒子](Masks/Particles/IronBroadsword/entry.json) | [剑身](ColorFields/SwordBody/IronBroadsword/entry.json) / [圆环](ColorFields/Rings/IronBroadsword/entry.json) / [粒子](ColorFields/Particles/IronBroadsword/entry.json) |
| 铅短剑 | [剑身一体组](Masks/SwordBody/LeadShortsword/entry.json) | [圆环](Masks/Rings/LeadShortsword/entry.json) | [粒子](Masks/Particles/LeadShortsword/entry.json) | [剑身](ColorFields/SwordBody/LeadShortsword/entry.json) / [圆环](ColorFields/Rings/LeadShortsword/entry.json) / [粒子](ColorFields/Particles/LeadShortsword/entry.json) |
| 铅宽剑 | [剑身一体组](Masks/SwordBody/LeadBroadsword/entry.json) | [圆环](Masks/Rings/LeadBroadsword/entry.json) | [粒子](Masks/Particles/LeadBroadsword/entry.json) | [剑身](ColorFields/SwordBody/LeadBroadsword/entry.json) / [圆环](ColorFields/Rings/LeadBroadsword/entry.json) / [粒子](ColorFields/Particles/LeadBroadsword/entry.json) |
| 银短剑 | [剑身一体组](Masks/SwordBody/SilverShortsword/entry.json) | [圆环](Masks/Rings/SilverShortsword/entry.json) | [粒子](Masks/Particles/SilverShortsword/entry.json) | [剑身](ColorFields/SwordBody/SilverShortsword/entry.json) / [圆环](ColorFields/Rings/SilverShortsword/entry.json) / [粒子](ColorFields/Particles/SilverShortsword/entry.json) |
| 银宽剑 | [剑身一体组](Masks/SwordBody/SilverBroadsword/entry.json) | [圆环](Masks/Rings/SilverBroadsword/entry.json) | [粒子](Masks/Particles/SilverBroadsword/entry.json) | [剑身](ColorFields/SwordBody/SilverBroadsword/entry.json) / [圆环](ColorFields/Rings/SilverBroadsword/entry.json) / [粒子](ColorFields/Particles/SilverBroadsword/entry.json) |
| 钨短剑 | [剑身一体组](Masks/SwordBody/TungstenShortsword/entry.json) | [圆环](Masks/Rings/TungstenShortsword/entry.json) | [粒子](Masks/Particles/TungstenShortsword/entry.json) | [剑身](ColorFields/SwordBody/TungstenShortsword/entry.json) / [圆环](ColorFields/Rings/TungstenShortsword/entry.json) / [粒子](ColorFields/Particles/TungstenShortsword/entry.json) |
| 钨宽剑 | [剑身一体组](Masks/SwordBody/TungstenBroadsword/entry.json) | [圆环](Masks/Rings/TungstenBroadsword/entry.json) | [粒子](Masks/Particles/TungstenBroadsword/entry.json) | [剑身](ColorFields/SwordBody/TungstenBroadsword/entry.json) / [圆环](ColorFields/Rings/TungstenBroadsword/entry.json) / [粒子](ColorFields/Particles/TungstenBroadsword/entry.json) |
| 金短剑 | [剑身一体组](Masks/SwordBody/GoldShortsword/entry.json) | [圆环](Masks/Rings/GoldShortsword/entry.json) | [粒子](Masks/Particles/GoldShortsword/entry.json) | [剑身](ColorFields/SwordBody/GoldShortsword/entry.json) / [圆环](ColorFields/Rings/GoldShortsword/entry.json) / [粒子](ColorFields/Particles/GoldShortsword/entry.json) |
| 金宽剑 | [剑身一体组](Masks/SwordBody/GoldBroadsword/entry.json) | [圆环](Masks/Rings/GoldBroadsword/entry.json) | [粒子](Masks/Particles/GoldBroadsword/entry.json) | [剑身](ColorFields/SwordBody/GoldBroadsword/entry.json) / [圆环](ColorFields/Rings/GoldBroadsword/entry.json) / [粒子](ColorFields/Particles/GoldBroadsword/entry.json) |
| 铂金短剑 | [剑身一体组](Masks/SwordBody/PlatinumShortsword/entry.json) | [圆环](Masks/Rings/PlatinumShortsword/entry.json) | [粒子](Masks/Particles/PlatinumShortsword/entry.json) | [剑身](ColorFields/SwordBody/PlatinumShortsword/entry.json) / [圆环](ColorFields/Rings/PlatinumShortsword/entry.json) / [粒子](ColorFields/Particles/PlatinumShortsword/entry.json) |
| 铂金宽剑 | [剑身一体组](Masks/SwordBody/PlatinumBroadsword/entry.json) | [圆环](Masks/Rings/PlatinumBroadsword/entry.json) | [粒子](Masks/Particles/PlatinumBroadsword/entry.json) | [剑身](ColorFields/SwordBody/PlatinumBroadsword/entry.json) / [圆环](ColorFields/Rings/PlatinumBroadsword/entry.json) / [粒子](ColorFields/Particles/PlatinumBroadsword/entry.json) |
| 钴剑 | [剑身一体组](Masks/SwordBody/CobaltSword/entry.json) | [圆环](Masks/Rings/CobaltSword/entry.json) | [粒子](Masks/Particles/CobaltSword/entry.json) | [剑身](ColorFields/SwordBody/CobaltSword/entry.json) / [圆环](ColorFields/Rings/CobaltSword/entry.json) / [粒子](ColorFields/Particles/CobaltSword/entry.json) |
| 钯金剑 | [剑身一体组](Masks/SwordBody/PalladiumSword/entry.json) | [圆环](Masks/Rings/PalladiumSword/entry.json) | [粒子](Masks/Particles/PalladiumSword/entry.json) | [剑身](ColorFields/SwordBody/PalladiumSword/entry.json) / [圆环](ColorFields/Rings/PalladiumSword/entry.json) / [粒子](ColorFields/Particles/PalladiumSword/entry.json) |
| 秘银剑 | [剑身一体组](Masks/SwordBody/MythrilSword/entry.json) | [圆环](Masks/Rings/MythrilSword/entry.json) | [粒子](Masks/Particles/MythrilSword/entry.json) | [剑身](ColorFields/SwordBody/MythrilSword/entry.json) / [圆环](ColorFields/Rings/MythrilSword/entry.json) / [粒子](ColorFields/Particles/MythrilSword/entry.json) |
| 山铜剑 | [剑身一体组](Masks/SwordBody/OrichalcumSword/entry.json) | [圆环](Masks/Rings/OrichalcumSword/entry.json) | [粒子](Masks/Particles/OrichalcumSword/entry.json) | [剑身](ColorFields/SwordBody/OrichalcumSword/entry.json) / [圆环](ColorFields/Rings/OrichalcumSword/entry.json) / [粒子](ColorFields/Particles/OrichalcumSword/entry.json) |
| 精金剑 | [剑身一体组](Masks/SwordBody/AdamantiteSword/entry.json) | [圆环](Masks/Rings/AdamantiteSword/entry.json) | [粒子](Masks/Particles/AdamantiteSword/entry.json) | [剑身](ColorFields/SwordBody/AdamantiteSword/entry.json) / [圆环](ColorFields/Rings/AdamantiteSword/entry.json) / [粒子](ColorFields/Particles/AdamantiteSword/entry.json) |
| 钛金剑 | [剑身一体组](Masks/SwordBody/TitaniumSword/entry.json) | [圆环](Masks/Rings/TitaniumSword/entry.json) | [粒子](Masks/Particles/TitaniumSword/entry.json) | [剑身](ColorFields/SwordBody/TitaniumSword/entry.json) / [圆环](ColorFields/Rings/TitaniumSword/entry.json) / [粒子](ColorFields/Particles/TitaniumSword/entry.json) |
| 天铸巨剑 | [剑身一体组](Masks/SwordBody/HeavenforgedGreatsword/entry.json) | [圆环](Masks/Rings/HeavenforgedGreatsword/entry.json) | [粒子](Masks/Particles/HeavenforgedGreatsword/entry.json) | [剑身](ColorFields/SwordBody/HeavenforgedGreatsword/entry.json) / [圆环](ColorFields/Rings/HeavenforgedGreatsword/entry.json) / [粒子](ColorFields/Particles/HeavenforgedGreatsword/entry.json) |

共享来源：

- [M1慢流动底色](ColorFields/_Shared/SlowFlowM1/entry.json)
- [R2青紫慢流动底色候选](ColorFields/_Shared/SlowFlowRadiantR2/entry.json)
- [R3青紫慢流动底色候选](ColorFields/_Shared/SlowFlowRadiantR3/entry.json)
- [原版火焰动态形状来源](Masks/Particles/_Shared/VanillaFlames/entry.json)
- [原版SharpTears粒子形状来源](Masks/Particles/_Shared/VanillaSharpTears/entry.json)
- [M1技术演示绑定](Examples/M1/composition.json)

分类及复用规则见[README](README.md)。
