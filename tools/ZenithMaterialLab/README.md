# 独立材质实验室

这是开发者制作工具，不放入正式玩家的启用列表。当前实验室0.1.1依赖天顶复苏0.3.69或更新版本，与正式飞剑共用`SurfaceMaterialDefinition`、`SurfaceMaterialRenderer`及`ZRMaterialSurface`，不会另外画一套只用于展示的材质。

## 打开

已经构建后，双击上级目录的`打开材质实验室.cmd`。也可运行`checks/run_material_lab.ps1`。首次构建或修改材质核心后运行`checks/build_material_lab.ps1`。制作客户端使用`artifacts/material-lab-profile`独立档案，只启用天顶复苏与实验室，不复制正式人物或世界。

独立客户端进入菜单后自动显示工作台，不需要创建角色。左侧分类与滑块控制材质，右侧提供球体、平面、剑迹网格，棋盘、校准树干及导入背景。球体用于观察光照，平面用于看贴图和高度置换，剑迹通过合成历史输入调用生产网格生成器，检查材质在带面上的表现。这里的校准树干与轨迹输入属于合成场景，不冒充真实世界攻击测试。

## 操作

- 顶部：加载、保存、导出、刷新素材、预览截图、撤销、重做、正常退出。
- 保存写入独立档`MaterialLab/active.material.json`；加载读取同一文件。可以在外部编辑后加载。保存使用临时文件替换，避免半写入的JSON。
- 滑块拖动实时生效；Ctrl+Z／Ctrl+Y可撤销／重做。数值范围来自正式材质合同。
- 玻璃、金属、涂层、薄膜、发光、散射是制作起点，不代表已批准应用于所有武器。
- “随剑配色”启用后保留实际武器Tint；旁边可切换泰拉之刃、附魔剑、星怒、彩虹猫作配色预览。
- 可暂停材质动画、旋转主光、切换最终画面／法线／反光／材质属性／透射数据。
- “逐项核对”生成实际GPU的45组A/B图与结果，并检查形成／消散、慢色场、蒙版图集、状态恢复、正式合成和RGBA四区。逐项对照不是手动世界战斗或完整物理材质认证。
- “分区示例”展示四个独立属性区域。`DetailLayers`在材质JSON中编辑，RGBA分别控制第1～4层；`Weight/Blend/Tint`控制表面颜色，`RoughnessDelta/MetallicDelta/EmissionStrength`控制局部属性。当前没有逐层专用滑块，保存/加载/导出均会保留这些字段。

## 导入美术

将PNG放到独立档`MaterialLab/Input/`，点击对应素材槽，再“刷新素材”。示例文件不存在时数学生成，有同名文件时不覆盖。替换原文件后刷新即可。

| 文件 | 用途 | 数据约定 |
|---|---|---|
| color.png | 一个缓慢流动底色 | RGB为颜色，Alpha不定义剑形 |
| mask.png | 一套动态形状 | Alpha决定形状；可用图集帧数、列数、播放速度 |
| normal.png | 切线空间法线 | RGB映射到-1～1，绿色正向沿UV的V方向 |
| surface.png | 金属／粗糙度／遮蔽 | R金属度、G粗糙度、B环境遮蔽；线性数据 |
| height.png | 凹凸和绘制网格置换 | R为高度，0.5为中性；不移动伤害碰撞 |
| emission.png | 自发光分布 | RGB乘以自发光颜色和强度 |
| environment.png | 环境反射图 | 经纬展开，Y轴向上；可替换为自制环境图 |
| regions.zrdata | 四个材质属性区域 | 原始RGBA数据，四通道分别对应四层；不改变整体动态形状 |
| background.png | 预览背景 | 点击“导入背景”，不随材质导出 |

材质JSON内的Assets可以改为Input内其他相对路径。图集需正确设置Frames、Columns和LoopSeconds，最多256帧。素材槽用于光学输入，不是额外叠一层特效形状：底色与动态蒙版仍各一套。

## 导出到实际剑

“导出”生成`MaterialLab/Exports/时间戳/`，包含material.json、实际引用的PNG/原始区域数据及来源哈希清单。导出不会自动改正式包。

使用`checks/import_material_export.py <导出目录> --weapon TerraBlade`先查看操作；加`--apply`后才写入源码。脚本验证参数、PNG和哈希，给该把实际剑设置SurfaceMaterial引用，并按底色、蒙版、法线、表面、高度、自发光、环境图各大类登记到特效库。已有目标文件会备份到artifacts/material-import-backups，不改其他剑绑定。随后重新构建、验证再打包。

WorldLightStrength只在实际飞剑存在时调用原版照明接口，预览球自发光不会照亮一个不存在的世界。环境图反射是真正按方向采样贴图，当前没有实时场景反射或光线追踪。次表面为背光包裹近似，体积为解析薄层散射／吸收，薄膜为三波段近似；均不等同Blender完整BSDF、三维体积或路径追踪。

## 区域图与通道工具

画四张灰度区域图后，用`checks/prepare_material_maps.py pack`明确分配RGBA通道，再用`data`转换成`regions.zrdata`。之所以不用普通透明PNG直接载入，是图像流程可能清空Alpha为0处的RGB，破坏其他三个独立数据通道；当前载荷直接上传RGBA字节，实际GPU已检查四区均保留。

详细命令、数据格式、通道打包实测与mip烘焙限制见[本轮记录](../../docs/0.3.69-成熟模组拆解与材质分区.md)。mip工具目前只生成离线候选，不表示游戏已启用mip。自动`-Check`使用独立的版本检查档，不覆盖正在制作的Input和active.material.json。
