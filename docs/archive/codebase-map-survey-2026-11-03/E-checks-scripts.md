# E — checks/ 编排脚本与静态检查（模块图）

> 范围：`checks/*.ps1`（37）、`checks/*.py`（54）、`checks/` 根目录其余文件（无）、
> `checks/ZenithPrototypeCheck/` 的项目文件（`ZenithPrototypeCheck.csproj`、`build.txt`）。
> 不含 `checks/ZenithPrototypeCheck/*.cs`（94 个 C# 夹具）、`Assets/`、`Localization/`，
> 也不含 `archived-oneoff/`（20）、`archived-material-fixtures/`（6）、`archived-removed-systems/`（13）。
> 全部结论来自实读源码（PowerShell 37/37 通读，Python 12/54 通读、42/54 读头部+关键行并抽读断言段）。
> 未执行任何脚本。

**覆盖统计**：应有 93 项 = 37 `.ps1` + 54 `.py` + 2 个项目文件；实覆盖 93 项（100%）。
第一部分按 A–G 分组（1 + 34 + 22 + 5 + 8 + 15 + 6 = 91 个脚本，余下 2 个是夹具项目文件，见第二部分表尾），
第二部分逐条索引，第三部分拆主线与 5 个核心脚本的调用/断言顺序，第四部分给 9 条坑。

---

## 第一部分：验证体系的模块说明

整体只有**一条主线**（`run_wood_checks.ps1` → `package_install.py`），其余脚本都是可单独调用的
**专项夹具运行器 / 素材处理工具 / 一次性交付核对**。理解这个层级关系是本目录最关键的一件事：

```
主线（唯一能产出"可安装包 + 安装回执"的路径）
  run_wood_checks.ps1  →  package_install.py  →  install-result.json

专项（从主线产物出发，只读地"再加证据"；多数不写回主线）
  run_*_render / run_*_world / run_*_gpu  →  artifacts/<专项>/<报告>.json
      ↑ 依赖 build-profile/Mods/*.tmod（主线或专门的重建脚本产出）

离线工具（不启游戏）
  compile_effects.py / prepare_* / emit_* / audit_* / check_*（静态）
```

### A. 全量回归编排（1 个脚本）

- **文件**：`checks/run_wood_checks.ps1`
- **职责**：把"资产静态检查 → 生成制作端常量 → 编译自有 shader → 原生打包两包 → 起隔离服务端跑断言 → 落盘结果"串成一条 fail-fast 流水线。
- **入口**：`pwsh -File checks/run_wood_checks.ps1`（开关 `-EchoOre` / `-InformationUi` / `-Glass`，见坑 6 第 1 点：加了开关会**切走**主回归分支）。
- **关键行**：
  - `run_wood_checks.ps1:13` 调 `check_mod_assets.py`；`:14` 失败即 `throw`（第一条闸门）。
  - `run_wood_checks.ps1:15` `emit_glass_preset.py` → **会写生产源码** `ZenithReborn/GlassMaterialPreset.cs`。
  - `run_wood_checks.ps1:17` `compile_effects.py` → **会写生产资产** `.fxc` 与 `Assets/Effects/build-manifest.json`。
  - `run_wood_checks.ps1:19-29` 对 `ZenithReborn`、`ZenithPrototypeCheck` 各跑一次
    `tModLoader.dll -server -build <src> -tmlsavedirectory build-profile`，再 `Copy-Item` 到 `wood-check-profile/Mods/`，同时算 sha256。
  - `run_wood_checks.ps1:38` 起隔离服务端（`-nosteam -tmlsavedirectory wood-check-profile`）。
  - `run_wood_checks.ps1:43-44` 写 `artifacts/native-build-check.json`（含 `passed` 与两包 sha256）。
  - `run_wood_checks.ps1:45` 非零退出码抛异常。
- **产出物**：`artifacts/wood-check.txt`（`PASS: <n> focused checks; ...`）、
  `artifacts/native-build-check.json`、`artifacts/{ZenithReborn,ZenithPrototypeCheck}-wood-pack.log`、
  `artifacts/wood-load.log`、`artifacts/own-effect-build.log`、`artifacts/glass-preset-build.log`。
- **失败行为**：**先落盘再抛出**。`native-build-check.json` 在失败分支也会写 `passed:false`
  （注释 `run_wood_checks.ps1:42` 明确说明，防止上一轮成功记录冒充本次通过）；`wood-check.txt` 由夹具在
  通过时才写，所以失败时它**保持旧内容**——这正是 `package_install.py:25` 时序断言的由来。
- **依赖顺序**：`check_mod_assets` → `emit_glass_preset` → `compile_effects` → 打包 → 运行。
  **顺序不可换**：资产检查必须最先（服务端不校验贴图），`compile_effects` 必须在打包前（`.fxc` 要进包）。

### B. 专项夹具运行器（34 个 `run_*.ps1`：3 服务端 + 27 图形 + 3 批量 + 1 已撤下）

按"目标进程"分两类，模式高度统一：

| 子类 | 脚本 | 模式 |
|---|---|---|
| 服务端夹具（`-server`，无窗口，靠 `ZENITH_CHECK_REPORT`） | `run_wood_checks`、`run_flying_blade_check`、`run_flying_blade_survey`、`run_echo_phase_probe` | 打包 → `enabled.json` → 起 `-server -nosteam` → 读 txt 报告 |
| 图形客户端夹具（`Start-Process` + 隐藏窗口 + 环境变量开关注入） | 其余 27 个（`run_airflow_export/material/world`、`run_archived_vfx_capture`、`run_clear_glass`、`run_energy_blade_preview`、`run_hybrid_gpu`、`run_information_gpu`、`run_information_ui_render`、`run_liquid_glass{,_trail,_perf}`、`run_martial_motion_render`、`run_martial_world`、`run_mask_preview`、`run_material_lab`、`run_pure_code_vfx`、`run_slash_render`、`run_smooth_glass_layers`、`run_soul_flight_world`、`run_soul_input`、`run_soul_trail_render`、`run_surge_preview`、`run_sword_head_join`、`run_swordbody_preview`、`run_textured_sword_material`、`run_vfx_stripe_ablation`） | 复制 `build-profile/Mods/*.tmod` → 写 `config.json` → 起客户端 → 等 json 报告 |
| 批量素材导出（内部再起 `pwsh` 子进程） | `run_airflow_batch`、`run_swordbody_batch`、`run_turbulence_batch` | 逐把剑调 `run_swordbody_preview.ps1` + `register_swordbody_vector.py` |
| 已撤下 | `run_ingame_preview`（文件内容就是一句 `throw`） | — |

图形夹具的**四条通用防伪规则**（几乎每个脚本都有，是这套体系的核心不变式）：

1. **报告必须是本轮的**：`(Get-Item $report).LastWriteTimeUtc -lt $startedAt` 即 `throw`
   （例：`run_airflow_material.ps1`、`run_soul_flight_world.ps1`、`run_liquid_glass.ps1:52`）。
2. **报告必须绑定当次包**：`run_information_ui_render.ps1` 先算
   `expectedPackageHash=(Get-FileHash .../Mods/ZenithReborn.tmod).Hash`，再断言
   `$captureReport.testedPackageSha256 -ne $expectedPackageHash` 为失败。
3. **同名复跑必须清旧标记**：`run_information_ui_render.ps1` 删 `gpu.json`/`failure.txt`；
   `run_soul_input.ps1` 删 `exit`/`input-arm`；`run_information_gpu.ps1` 清空整个输出目录。
4. **清掉其他夹具的开关**：各脚本用 `[Environment]::SetEnvironmentVariable($name,$null,'Process')`
   把不属于自己的 `ZENITH_*` 逐一清空，防止"上一个夹具的开关让本轮客户端自动进世界/自动退出"。
5. **不强杀**：超时一律 `throw` 并保留 PID（`run_airflow_world.ps1`、`run_liquid_glass.ps1`、
   `run_material_lab.ps1` 等）。例外：`run_information_ui_render.ps1`（先 `CloseMainWindow()`，
   2 秒不退才 `Stop-Process`）与 `run_surge_preview.ps1`（轮询失败直接 `Stop-Process`）。

- **入口**：各自独立，例：`pwsh -File checks/run_soul_flight_world.ps1 -Surface -LightPreset night`。
- **产出物**：`artifacts/<专项>-<version>/…json`，目录名带版本号，便于 `package_install` 按版本反查。
- **失败行为**：抛异常 + 打印 `error.txt`/日志尾部，不写"通过"。

### C. 资产与静态检查（22 个 Python，全部离线，不启游戏）

- **`check_mod_assets.py`（761 行，主线第一闸门）**：自研 C# 词法+结构解析器，找出"会被注册但拿不到贴图"的
  `ModItem/ModProjectile/ModBuff/ModNPC/ModDust` 派生类。判定：本类或祖先有 `Texture =>` 覆盖 → PASS；
  否则要求 `<模组根>/<命名空间路径>/<类名>.png` 存在；都不满足 → 列缺口、`return 1`（`:743-754`）。
  纯标准库，无 Pillow/numpy 依赖。**这是唯一能覆盖"客户端加载崩溃"的静态手段**（服务端 `ModContent.HasAsset` 恒 false）。
- **`check_vfx_ownership.py`**：对**已打包的 .tmod**做包内审计（详见第三部分 D）。
- **`check_material_preparation.py`**：对 `prepare_material_maps.py` 的语义做离线自测（通道无损、sRGB 线性平均≈188、
  法线 mip 重归一化、图集不串帧），8 条断言，产出 `artifacts/mod-vfx-research-20261024/preparation-check.json`。
- **`check_liquid_glass_analytic.py` / `check_liquid_glass_geometry.py`**：对隔离原型的**标量数值**做独立复核，
  产出 `artifacts/liquid-glass-prototype/numerical-check.json`；两个文件都自带"不替代 GPU/画面评审"的免责声明。
- **`check_airflow_assets.py`**：核对 21 把剑的 H4 导出、运行时素材、逐矿材颜色与拖尾长度（用 Pillow）。
- **`audit_material_assets.py`**：材质管线静态审计（PNG 尺寸/通道/mip 预算、shader 声明 vs C# 提交的参数名集合、
  与安装包源码逐文件比对、`installedMatchesReceipt`），**只写 `--output` 指定的 json**。
- **`review_mod_vfx.py`**：**只读**打开任意 `.tmod`，按路径提取研究样本，不安装不执行。
- **`vfx_library.py`**：素材库编目校验（id 唯一、类别/剑名与目录层级一致、颜色引用无环、每个剑×类别×kind 齐全），
  `validate` 被 `run_swordbody_batch.ps1` 收尾调用；`search` 是检索入口。
- **交付核对类 `check_*_delivery.py` / `check_color79.py` / `check_hybrid_preservation.py` / `check_visible_weapon_colors.py` /
  `check_vfx_rollback_0379.py` / `check_echo_damage_delivery.py` / `check_ending_preservation.py`**：
  每个都是**某一版的一次性证据封存**——硬编码 `version='0.3.NN'`，逐字节比对当时允许改动的文件白名单，
  再断言当时的 GPU/世界报告哈希。**对当前 0.3.130 全部会失败**（见坑 7）。
- **入口/产出**：全部 `python checks/xxx.py`；产出分散在各 `artifacts/*.json`，命名带版本号。

### D. 打包与安装（4 个 Python + 1 个编排 ps1）

- **`package_install.py`（289 行）**：唯一写正式 `Mods/` 的脚本（详见第三部分 B）。
  - `ZENITH_PACKAGE_ONLY=1` 时只产出 `artifacts/ZenithReborn-<ver>.tmod` + `package-result-<ver>.json`，
    不安装、不覆盖 `install-result.json`（`:82-91`）。
  - 正式安装：备份 `enabled.json` 与旧包到 `artifacts/backups/<YYYYmmdd-HHMMSS>/`，用 `.pending` + `os.replace` 原子替换。
- **`prepare_pure_code_package.py`**：从 `build-profile` 的包**重新拼一个 TMOD 容器**，物理删除
  `Assets/Vfx/*` 与 `SwordSpark.*`，保留同一份 DLL，写 `artifacts/pure-code-vfx-<ver>/{with-assets,without-vfx-assets}.tmod`。
- **`build_material_lab.ps1`**：先跑 `run_wood_checks.ps1`（可 `-SkipCoreChecks` 跳过），再把
  `tools/ZenithMaterialLab` 用原生编译器打进 `build-profile`；**不写正式 Mods**。
- **失败行为**：Python 侧用 `assert`（非零退出 + traceback）；PS 侧 `$ErrorActionPreference='Stop'` + 显式 `throw`。

### E. 着色器编译与清单（8 个 Python）

- **`compile_effects.py`（生产）**：`ctypes.WinDLL('d3dcompiler_43.dll')` 直接调 `D3DCompile`，profile `fx_2_0`。
  - 只展开**白名单 4 个** `.fxh`（`ZRSwordSpaceWarp/ZRClearGlass/ZRSwordHighlights/ZRLiquidGlass`），
    且**禁止间接 include**（`compile_effects.py:36-38`）。
  - `.fxc` 内容不变则不重写（`:52-55`，避免无意义的 mtime/哈希抖动）。
  - 恒写 `Assets/Effects/build-manifest.json`（`source/compiled + 双 sha256 + origin + projectSourceIncludes`），
    这份清单是 `check_vfx_ownership` 与 `package_install` 的比对基准。
- **`compile_liquid_glass{,_trail,_perf,_sentinel,_parameter_probe}.py`（5 个原型）**：同一套 ctypes 模板，只编译
  `checks/ZenithPrototypeCheck/Assets/LiquidGlass/*.fx`，**不碰生产清单**。被 `run_liquid_glass*.ps1` 在打包前调用。
- **`emit_vfx_mask_data.py`**：把已通过 GPU 校验的 H4 蒙版拷进 `ZenithReborn/Assets/Vfx/SwordBody` 并生成运行时几何/底色表。
- **`emit_glass_preset.py`**：把 `VfxLibrary/Materials/SwordTrail/Glass/preset.json` 编成
  `ZenithReborn/GlassMaterialPreset.cs` 常量；带 13 项参数上下界断言与 `scope=='developer-authoring-only'` 断言。

### F. 素材制作与登记（15 个 Python：prepare_* / pack_* / register_* / generate_* / import_* / png_stdlib）

- 统一约定：**只生成"制作候选"，不覆盖已认可素材**（`prepare_glass_reflection_lowpass.py` 注释明确）。
- `prepare_material_maps.py`（通道打包 + 语义 mip）→ `check_material_preparation.py` 做自测 →
  `import_material_export.py`（默认只列操作，`--apply` 才写源码）→ 生产。
- `pack_swordbody_mask.py` / `register_swordbody_vector.py` / `png_stdlib.py`：蒙版登记链。
  `png_stdlib.py` 是无 Pillow 环境的 PNG 读写替代（register 脚本在 `except ImportError` 时回退到它）。
- `generate_noise.py` **直接写** `ZenithReborn/Assets/Vfx/Materials/SwordFlow/NoiseSeamless.png`（唯一直接写生产素材的脚本）。
- `prepare_luminance.py` 见第三部分 E；`prepare_boss_portraits.py` 生成 `SwordBossPortraitCatalog.cs`（文件头注明"由脚本生成"）。

### G. 报告与记录（5 个 Python + 1 个编码 ps1）

- `collect_boss_unlock_evidence.py`：汇总原生构建、只读检查、GPU 样本、素材来源，做与 0.3.108 基线的差异审计。
- `pick_soul_flight_frames.py`：从抓帧里挑"拖尾最完整"的帧（只统计不生产）。
- `render_ribbon_preview.py`：用 CPU 复刻 C# 顶点网格做预览（明确"不是 GPU 截图"）。
- `simulate_viscous.py`：离线复刻 `ViscousSlash` 算法并交 ffmpeg 合成动图。
- `compare_clear_glass.py`：把真实 GPU 输出原样并列（不调色、不补光）。
- `encode_swordbody_preview.ps1`：用 `artifacts/preview-tools` 的 ffmpeg 把帧序列编码成 5 段 mp4。

---

## 第二部分：文件索引表

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| checks/run_wood_checks.ps1 | ps1 | A 全量回归 | 主线：资产检查→生成预设→编译shader→双包打包→隔离服务端断言→落盘 | check_mod_assets.py、emit_glass_preset.py、compile_effects.py、build-profile、wood-check-profile、artifacts/wood-check.txt、native-build-check.json | 已验证 |
| checks/package_install.py | py | D 打包安装 | 校验报告/包身份后原子安装、备份、写 install-result.json | wood-check.txt、check_vfx_ownership、prepare_luminance、tmod_archive、artifacts/backups | 已验证 |
| checks/check_vfx_ownership.py | py | C 静态检查 | 包内禁止第三方材质、自有 effect 清单哈希、生产源码禁用符号 | build-manifest.json、package_install:30 | 已验证 |
| checks/check_mod_assets.py | py | C 静态检查 | 被注册类型必须有同名 png 或 Texture override（纯标准库） | run_wood_checks:13、ZenithReborn/*.png | 已验证 |
| checks/prepare_luminance.py | py | D 依赖准备 | 定位 workshop 里的 Luminance.tmod，抽 DLL 到 artifacts/dependencies，可选注入 profile | ZenithReborn.csproj 的 PrepareLuminance Target、package_install:23 | 已验证 |
| checks/compile_effects.py | py | E 着色器 | 用 d3dcompiler_43 编译生产 .fx → .fxc + build-manifest.json | Assets/Effects/*.fx、4 个白名单 .fxh | 已验证 |
| checks/tmod_archive.py | py | D 库 | TMOD 容器读写（TMOD 头/变长串/sha1/逐条 deflate），可当 CLI 解包 | package_install、prepare_pure_code_package、verify_own_effect、18 个 check_* | 已验证 |
| checks/prepare_pure_code_package.py | py | D 打包 | 重新拼 TMOD 去掉 Assets/Vfx，保留同 DLL | run_pure_code_vfx.ps1 | 已验证 |
| checks/audit_material_assets.py | py | C 静态审计 | 材质贴图预算、shader 参数声明vs提交、源码与安装包身份 | artifacts/install-result.json、Assets/Vfx/Materials/SwordFlow | 已验证 |
| checks/check_material_preparation.py | py | C 静态检查 | 8 条离线语义自测（通道/sRGB/法线mip/图集） | prepare_material_maps.py | 已验证 |
| checks/check_visible_weapon_colors.py | py | C 交付核对 | 0.3.88 一次性证据：逐剑调色板、光学差异为 0、全模组世界日志 | 硬编码 version=='0.3.88' | 已验证 |
| checks/vfx_library.py | py | C 编目校验 | validate：id/类别/目录层级/引用环/清单齐全；search 检索 | VfxLibrary/library.json、run_swordbody_batch 收尾 | 已验证 |
| checks/emit_glass_preset.py | py | E 生成器 | preset.json → GlassMaterialPreset.cs（13 参数带上下界断言） | VfxLibrary/Materials/SwordTrail/Glass/preset.json | 已验证 |
| checks/build_material_lab.ps1 | ps1 | D 打包 | 先跑主回归，再打包 tools/ZenithMaterialLab 到 build-profile | run_wood_checks.ps1:6 | 已验证 |
| checks/encode_swordbody_preview.ps1 | ps1 | G 报告 | ffmpeg 把 5 个片段帧序列编 mp4 | artifacts/preview-tools/imageio_ffmpeg | 已验证 |
| checks/run_flying_blade_check.ps1 | ps1 | B 服务端夹具 | 独立 flying-blade-profile 构建两包，跑 22 项飞刃断言 | build-profile（仅读 Luminance.tmod）、artifacts/flying-blade-check.txt | 已验证 |
| checks/run_flying_blade_survey.ps1 | ps1 | B 服务端夹具 | 引擎级普查，输出弹幕/配方 JSON | 写 build-profile、artifacts/flying-blade-survey.json | 已验证 |
| checks/run_echo_phase_probe.ps1 | ps1 | B 服务端夹具 | 相位探针，产出 txt | 写 build-profile、不校验退出码 | 已验证 |
| checks/run_airflow_export.ps1 | ps1 | B 图形夹具 | 构建夹具 csproj→`-eac` 打包→一个客户端导出 21 把图集 | artifacts/airflow-H4-export/complete.txt | 已验证 |
| checks/run_airflow_material.ps1 | ps1 | B 图形夹具 | 生产材质 GPU 预览 + passed 断言 | artifacts/airflow-material-<ver>/gpu.json | 已验证 |
| checks/run_airflow_world.ps1 | ps1 | B 图形夹具 | 隔离世界（可 FullModpack/Dummy/PlacementOnly/before-placement） | artifacts/airflow-world-*/world.json | 已验证 |
| checks/run_airflow_batch.ps1 | ps1 | B 批量 | 21 把剑 H3 逐个 preview+register，跳过已有 clip.json | run_swordbody_preview.ps1、register_swordbody_vector.py | 已验证 |
| checks/run_archived_vfx_capture.ps1 | ps1 | B 图形夹具 | 0.3.79/0.3.80 归档包哈希校验后采集（world/gpu） | artifacts/vfx-comparison-<run>、capture-session.json | 已验证 |
| checks/run_clear_glass.ps1 | ps1 | B 图形夹具 | 透明玻璃专项（可 -Baseline/-PackageVersion/-HighlightProbe） | clear-glass.json / highlight-probe.json | 已验证 |
| checks/run_energy_blade_preview.ps1 | ps1 | B 图形夹具 | 能量薄刃素材预览（材质实验室专用档） | artwork.json | 已验证 |
| checks/run_hybrid_gpu.ps1 | ps1 | B 图形夹具 | 融合光学对照，异步不等待 | hybrid.json | 已验证 |
| checks/run_information_gpu.ps1 | ps1 | B 图形夹具 | 信息界面 GPU，启动前清空输出目录 | artifacts/boss-unlocks-<ver>/gpu | 已验证 |
| checks/run_information_ui_render.ps1 | ps1 | B 图形夹具 | 界面截屏+模拟输入，断言报告绑定包哈希 | gpu.json、client.pid | 已验证 |
| checks/run_ingame_preview.ps1 | ps1 | B 已撤下 | 文件即一句 throw（随 0.3.60 旧材质撤下） | — | 已验证 |
| checks/run_liquid_glass.ps1 | ps1 | B 图形夹具 | 编译 3 个原型 shader→打包夹具→断言 GPU 报告 | compile_liquid_glass*.py、shader-hashes.json | 已验证 |
| checks/run_liquid_glass_trail.ps1 | ps1 | B 图形夹具 | 刀光网格原型 | liquid-trail.json | 已验证 |
| checks/run_liquid_glass_perf.ps1 | ps1 | B 图形夹具 | GPU 计时基线（识别 Steam 登录失败） | liquid-performance.json | 已验证 |
| checks/run_martial_motion_render.ps1 | ps1 | B 图形夹具 | 武技动作菜单 GPU，异步 | artifacts/martial-motion-<ver> | 已验证 |
| checks/run_martial_world.ps1 | ps1 | B 图形夹具 | 武技世界自动跑（GUI+AUTORUN） | artifacts/martial-playtest-<ver> | 已验证 |
| checks/run_mask_preview.ps1 | ps1 | B 图形夹具 | 蒙版合成预览（AssetsOnly/Source） | artifacts/mask-composition-preview | 已验证 |
| checks/run_material_lab.ps1 | ps1 | B 图形夹具 | 材质实验室；-Check 时回写 schema-controls/material.json | artifacts/material-lab-<ver>/gpu.json | 已验证 |
| checks/run_pure_code_vfx.ps1 | ps1 | B 图形夹具 | 有/无 VFX 素材两包逐像素一致性 | prepare_pure_code_package.py、independence.json | 已验证 |
| checks/run_slash_render.ps1 | ps1 | B 图形夹具 | 菜单 GPU 斩击回放，异步 | artifacts/slash-gpu-<ver> | 已验证 |
| checks/run_smooth_glass_layers.ps1 | ps1 | B 图形夹具 | 平滑玻璃分层候选预览 | artwork.json | 已验证 |
| checks/run_soul_flight_world.ps1 | ps1 | B 图形夹具 | 主飞剑隔离世界（Surface/EchoDamage/FullModSet/FormalSettings/Original79/LightPreset/RunLabel） | world.json | 已验证 |
| checks/run_soul_input.ps1 | ps1 | B 图形夹具 | 真实鼠标输入（REQUIRE_INPUT_ARM），异步靠 exit/input-arm | artifacts/soul-input-<ver>/validation.json | 已验证 |
| checks/run_soul_trail_render.ps1 | ps1 | B 图形夹具 | 主飞剑拖尾 GPU（-WithoutThirdParty 重建隔离包、-TextureAblation） | artifacts/soul-trail-gpu-<ver> | 已验证 |
| checks/run_surge_preview.ps1 | ps1 | B 图形夹具 | 重建两包→等 preview.json（失败强杀） | 硬编码 taskRoot | 已验证 |
| checks/run_sword_head_join.ps1 | ps1 | B 图形夹具 | 指定包 + 夹具包做刀身接合检查 | head-join.json | 已验证 |
| checks/run_swordbody_batch.ps1 | ps1 | B 批量 | 20 把矿剑 R2 断点续跑，一次重试，收尾 vfx_library validate | 开头强杀所有 dotnet | 已验证 |
| checks/run_swordbody_preview.ps1 | ps1 | B 图形夹具 | 单剑单修订版蒙版 GPU 导出（被三个批量脚本调用） | VfxLibrary/library.json、ZENITH_SWORDBODY_* 8 个变量 | 已验证 |
| checks/run_textured_sword_material.ps1 | ps1 | B 图形夹具 | 完整贴图材质检查 | textured-material.json | 已验证 |
| checks/run_turbulence_batch.ps1 | ps1 | B 批量 | H2 湍流蒙版批量导出（无重试） | 同上 | 已验证 |
| checks/run_vfx_stripe_ablation.ps1 | ps1 | B 图形夹具 | 条纹消融对照 | ablation.json | 已验证 |
| checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj | csproj | 夹具项目 | net8.0、`BuildMod=false`、导入 tMLMod.targets、引用主模组 bin/Release DLL（Private=false） | 所有 `-eac` 打包路径 | 已验证 |
| checks/ZenithPrototypeCheck/build.txt | txt | 夹具项目 | displayName "Zenith Prototype Check (isolated)"、version 0.1、`modReferences=ZenithReborn`、`buildIgnore` | 打包时需先有 ZenithReborn.tmod | 已验证 |
| checks/check_color79.py | py | C 交付核对 | 0.3.87 逐剑配色与亮刃混色沿用 79 | 硬编码 0.3.87 | 部分待核实 |
| checks/check_color_glass_restoration.py | py | C 交付核对 | 0.3.83 保留选色同时恢复玻璃 | 硬编码 0.3.83 | 部分待核实 |
| checks/check_day_night_delivery.py | py | C 交付核对 | 0.3.89 昼夜纹理修订 + 原始 80 保留 | 硬编码 0.3.89 | 部分待核实 |
| checks/check_echo_damage_delivery.py | py | C 交付核对 | 剑影伤害包的实机证据 + 素材保持 | build-profile | 部分待核实 |
| checks/check_ending_preservation.py | py | C 交付核对 | 结束修订前后 GPU 图与随包美术比对 | build-profile、numpy/PIL | 部分待核实 |
| checks/check_glass_r3_delivery.py | py | C 交付核对 | 0.3.80 R3 交付证据 | 硬编码 0.3.80 | 部分待核实 |
| checks/check_glass_reference_restore.py | py | C 交付核对 | 0.3.84 用归档旧版 GPU 像素核对恢复 | 硬编码 0.3.84 | 部分待核实 |
| checks/check_glass_visibility_delivery.py | py | C 交付核对 | 0.3.81 增亮只改光能 | 硬编码 0.3.81 | 部分待核实 |
| checks/check_hybrid_preservation.py | py | C 交付核对 | 0.3.86 融合版只改材质光效 | 硬编码 0.3.86、79/80 哈希常量 | 部分待核实 |
| checks/check_sword_color_delivery.py | py | C 交付核对 | 0.3.82 剑身取色/亮度/滤镜保留 | 硬编码 0.3.82 | 部分待核实 |
| checks/check_vfx_rollback_0379.py | py | C 交付核对 | 与 0.3.79 归档逐字节核对 | `--version` 默认 0.3.85 | 部分待核实 |
| checks/check_airflow_assets.py | py | C 静态检查 | 21 把剑 H4 导出/运行时素材/逐矿颜色/拖尾长度 | VfxLibrary/Masks/SwordBody/*/H4/authoring.json | 部分待核实 |
| checks/check_liquid_glass_analytic.py | py | C 数值复核 | 解析梯度标量差分 | K=0.012 | 部分待核实 |
| checks/check_liquid_glass_geometry.py | py | C 数值复核 | 截面与合并函数边界性质 | numerical-check.json | 部分待核实 |
| checks/collect_boss_unlock_evidence.py | py | G 报告 | 汇总构建/检查/GPU/素材来源，对 0.3.108 基线做差异审计 | install-result.json | 部分待核实 |
| checks/compare_clear_glass.py | py | G 报告 | GPU 输出并排拼图 | numpy/PIL | 部分待核实 |
| checks/compile_liquid_glass.py | py | E 着色器 | 编译 LiquidGlass.fx 原型 | run_liquid_glass.ps1 | 部分待核实 |
| checks/compile_liquid_glass_trail.py | py | E 着色器 | 编译 LiquidGlassTrail.fx | run_liquid_glass_trail/perf | 部分待核实 |
| checks/compile_liquid_glass_perf.py | py | E 着色器 | 编译 LiquidGlassPerfBase.fx | run_liquid_glass_perf | 部分待核实 |
| checks/compile_liquid_glass_sentinel.py | py | E 着色器 | 编译固定色 Sentinel.fx | run_liquid_glass | 部分待核实 |
| checks/compile_liquid_glass_parameter_probe.py | py | E 着色器 | 编译 ParameterProbe.fx | run_liquid_glass | 部分待核实 |
| checks/emit_vfx_mask_data.py | py | E 生成器 | H4 蒙版 → ZenithReborn/Assets/Vfx/SwordBody + 几何/底色表 | 生产资产 | 部分待核实 |
| checks/evolve_swordbody_rhythm.py | py | F 素材制作 | 按剑性格演化 R2 蒙版节奏（美术决策脚本） | VfxLibrary/Masks/SwordBody | 部分待核实 |
| checks/generate_noise.py | py | F 素材制作 | 纯 Python 生成可平铺噪声 → NoiseSeamless.png | 直接写生产资产 | 部分待核实 |
| checks/import_material_export.py | py | F 素材制作 | 核对实验室导出并按剑名接入（默认 dry-run，`--apply` 才写） | tools/ZenithMaterialLab 导出物 | 部分待核实 |
| checks/make_autofit_recipe.py | py | F 素材制作 | 为一把剑生成 R2 autoFit 配方 | run_swordbody_batch | 部分待核实 |
| checks/mark_swordbody_entries.py | py | F 素材制作 | 标记 21 把矿石剑为 review-candidate R2 | VfxLibrary entry.json | 部分待核实 |
| checks/pack_swordbody_mask.py | py | F 素材制作 | 抽 alpha 单元并登记尖端，不重绘 | register_swordbody_vector | 部分待核实 |
| checks/pick_soul_flight_frames.py | py | G 报告 | 按非背景像素/亮度跨度挑帧 | artifacts 抓帧目录 | 部分待核实 |
| checks/png_stdlib.py | py | F 库 | 无依赖 PNG 读写（5 种扫描线滤波） | register_swordbody_vector 的 ImportError 回退 | 部分待核实 |
| checks/prepare_boss_portraits.py | py | F 素材制作 | Wiki GIF → 透明图集 + 生成 SwordBossPortraitCatalog.cs | 生产源码（生成物） | 部分待核实 |
| checks/prepare_energy_blade.py | py | F 素材制作 | 8 张原图 → 法线/表面/四区数据候选 | artifacts/energy-blade-a1 | 部分待核实 |
| checks/prepare_glass_r3.py | py | F 素材制作 | 原稿 → 环境/高度/法线/粗糙度 | ZenithReborn/Assets/Vfx/Materials/SwordFlow/GlassR3 | 部分待核实 |
| checks/prepare_glass_reflection_lowpass.py | py | F 素材制作 | 从 80 环境图生成暗处反射输入 | GlassR3 | 部分待核实 |
| checks/prepare_material_maps.py | py | F 素材制作 | 通道打包 + 语义 mip 烘焙（被 check_material_preparation 自测） | bake_mips/pack/data_payload | 部分待核实 |
| checks/prepare_smooth_glass_layers.py | py | F 素材制作 | 平滑玻璃分层候选 + 原贴图检查 | artifacts/smooth-glass-layers-r1 | 部分待核实 |
| checks/register_swordbody_vector.py | py | F 素材制作 | 登记原生 GPU 导出蒙版（不重绘 alpha），写 clip.json | 三个批量脚本 | 部分待核实 |
| checks/render_ribbon_preview.py | py | G 报告 | CPU 复刻顶点网格预览 | numpy/PIL | 部分待核实 |
| checks/repoint_colorfields.py | py | F 素材制作 | 把各剑 R2 的 colorField 指向新金属场 | VfxLibrary entry.json | 部分待核实 |
| checks/review_mod_vfx.py | py | C 静态检查 | 只读打开 .tmod 提取研究样本 | tmod_archive | 部分待核实 |
| checks/simulate_viscous.py | py | G 报告 | 离线复刻 ViscousSlash 并合成动图 | png_stdlib、ffmpeg | 部分待核实 |
| checks/verify_own_effect.py | py | C 静态检查 | 无第三方素材包与原包 GPU 图逐像素一致 | 产出 soul-trail-gpu-<ver>/independence.json（package_install:156 消费） | 部分待核实 |

---

## 第三部分：脚本依赖图

### 3.0 总图

```
                      ┌──────────────────────────────┐
                      │ A 资产闸门（离线·纯标准库）   │
                      │ check_mod_assets.py          │──FAIL(1)──▶ 主线终止
                      └──────────────┬───────────────┘
                                     │ PASS
                      ┌──────────────▼───────────────┐
                      │ emit_glass_preset.py         │──写──▶ ZenithReborn/GlassMaterialPreset.cs
                      └──────────────┬───────────────┘
                      ┌──────────────▼───────────────┐
                      │ compile_effects.py           │──写──▶ Assets/Effects/*.fxc
                      │ (d3dcompiler_43, fx_2_0)     │        Assets/Effects/build-manifest.json
                      └──────────────┬───────────────┘
                      ┌──────────────▼───────────────┐
                      │ tML -server -build ×2        │──▶ build-profile/Mods/{ZenithReborn,
                      │ (原生编译器, 不用 NuGet)     │        ZenithPrototypeCheck}.tmod
                      └──────────────┬───────────────┘
                       copy + sha256 │
                      ┌──────────────▼───────────────┐
                      │ wood-check-profile/Mods/     │
                      │ enabled.json=[两包]          │
                      └──────────────┬───────────────┘
                      ┌──────────────▼───────────────┐
                      │ tML -server -nosteam         │ ZENITH_WOOD=1
                      │ → 夹具 Assert ×N             │
                      └──────────────┬───────────────┘
                 ┌───────────────────┴───────────────────┐
       PASS      ▼                                        ▼  FAIL
  artifacts/wood-check.txt                        （txt 保持旧内容）
  "PASS: <n> focused checks; …"                   artifacts/wood-load.log
  artifacts/native-build-check.json                throw（含 passed:false 已落盘）
        │
        ▼
  package_install.py  ──▶ artifacts/ZenithReborn-<ver>.tmod / -source.zip
                     ──▶ artifacts/install-result.json（73 键）
                     ──▶ （非 PACKAGE_ONLY）正式 Mods/ZenithReborn.tmod + backups/<ts>/
        ▲
        └── 可选证据注入：artifacts/<专项>-<ver>/*.json（按 testedPackageSha256 与当前包哈希比对）
                    ▲
                    └── run_* 图形/世界夹具（消费 build-profile/Mods/*.tmod）
```

### 3.1 `run_wood_checks.ps1` 完整调用与断言顺序

| 序 | 行 | 动作 | 断言/失败作用 |
|---|---|---|---|
| 1 | `:3-11` | 定位 `taskRoot`、`loaderRoot`、`dotnet`、测试 profile、python（优先 codex runtime，回退 PATH） | 环境缺失即 `Get-Command` 抛错 |
| 2 | `:8` | 建 `wood-check-profile/Mods`、`artifacts` | — |
| 3 | `:13` | `python checks/check_mod_assets.py` | **闸门 1**：拦"新增 Mod 类型无贴图/无 override"。服务端夹具永远看不到这条（`ModContent.HasAsset` 在 `-server` 恒 false），所以必须离线拦 |
| 4 | `:14` | `$LASTEXITCODE -ne 0` → `throw` | 资产失败后**后面一条都不跑**（fail-fast） |
| 5 | `:15` | `emit_glass_preset.py`，日志进 `glass-preset-build.log` | 生成 `GlassMaterialPreset.cs`；断言 preset 的 13 个参数在上下界内、`scope=='developer-authoring-only'` |
| 6 | `:16` | 失败则先 `Get-Content` 日志再 `throw` | "先落盘再抛"的固定写法 |
| 7 | `:17` | `compile_effects.py`，日志进 `own-effect-build.log` | **闸门 2**：shader 编不过就不要打包；同时刷新 `build-manifest.json`（后面包审计的基准） |
| 8 | `:19-26` | 对两包各：`cd loaderRoot` → `dotnet tModLoader.dll -server -build <src> -tmlsavedirectory build-profile` | **闸门 3**：C# 编译失败/本地化 hjson 语法错误在此暴露 |
| 9 | `:27` | `Copy build-profile/Mods/<n>.tmod → wood-check-profile/Mods/` | 被测包与用于安装的包**物理分离**，后面 `package_install` 会比对两边哈希 |
| 10 | `:28` | 记 `sha256(tmod)` 到 `$nativePackages` | 供 `native-build-check.json` 与 `package_install:33` 比对 |
| 11 | `:30` | 写 `enabled.json = ["ZenithReborn","ZenithPrototypeCheck"]` | 夹具模组必须同时启用才能跑断言 |
| 12 | `:31-35` | 设 `ZENITH_WOOD=1`（固定）、`ZENITH_ECHO_ORE/INFORMATION_UI/GLASS`（按 switch）、`ZENITH_CHECK_REPORT=artifacts/wood-check.txt` | 开关决定夹具走哪条分支（见坑 6 第 1 点：加开关会切走主分支） |
| 13 | `:36-40` | `dotnet tModLoader.dll -server -nosteam -tmlsavedirectory wood-check-profile`，日志 `wood-load.log`，捕获 `$LASTEXITCODE` | 夹具断言的唯一执行点；夹具在通过时 `Environment.Exit(0)` |
| 14 | `:41` | `Get-Content wood-check.txt` | 把 `PASS: <n> …` 打到控制台，供人眼看项数 |
| 15 | `:43-44` | 写 `native-build-check.json`：`{compiler:'tModLoader native', packages:{名:sha256}, passed:(result==0)}` | **即使失败也写 `passed:false`**，防止旧文件冒充本次通过 |
| 16 | `:45` | `result -ne 0` → `throw` | — |

**不变式**：`wood-check.txt` 的 mtime 必须晚于 `build-profile/Mods/ZenithReborn.tmod` 的 mtime；
两者哈希必须与 `wood-check-profile/Mods/ZenithReborn.tmod`、`native-build-check.json.packages.ZenithReborn.sha256` 一致。
本机实读验证（2026-11-03 一轮）：包 22:58:22 → 报告 22:59:08 → install-result 22:59:35，三处 sha256 同为 `7b246fda…`。

### 3.2 `package_install.py` 完整断言顺序（20 步）

| 序 | 行 | 断言 | 作用 |
|---|---|---|---|
| 1 | `:18` | 读 `artifacts/wood-check.txt` | 回归必须已经跑过 |
| 2 | `:19` | `report_text.startswith('PASS: ')` | 报告必须是"通过"形态；`echo-phase-probe.txt`/`flying-blade-survey-report.txt` 不满足，**拿错报告会在这里炸** |
| 3 | `:20` | `checks = int(report_text.split()[1])` | 抓项数写进回执（`focused_checks`） |
| 4 | `:21-22` | 从 `ZenithReborn/build.txt` 读 `modReferences`，拆成 `required_mods` | 依赖集合**从声明推导**，不硬编码；当前 `build.txt` 已无该行 → 空集 |
| 5 | `:23` | 若声明了 Luminance → `prepare_luminance()` | 依赖包必须先就位 |
| 6 | `:24` | `assert set(required_mods) <= {'Luminance'}` | **白名单**：出现未准备的新依赖立即失败（不允许"悄悄多一个玩家依赖"） |
| 7 | `:25` | `assert report.mtime > built.mtime` | **时序断言**：报告必须比包新。包被后续任何脚本重建（刷新 mtime）都会让这条失败 —— 这是"先跑飞刃夹具再安装必然失败"的根因 |
| 8 | `:26-28` | `read_archive(built)`，`name=='ZenithReborn'` 且 `version == build.txt.version` | 包身份与版本一致（防止安装旧包） |
| 9 | `:30` | `verify_vfx_ownership(built)` | 包内不得有第三方材质；自有 effect 清单与哈希必须自洽 |
| 10 | `:31-36` | `native-build-check.passed` 且其 sha256 == 当前包 sha256 → **必须** `wood-check-profile` 里的包与 `built` 逐字节相同；否则退化为"包内 DLL == bin/Release DLL" | **被测包即安装包**的双保险：原生构建记录的哈希、隔离档里的副本、待安装的包三者一致 |
| 11 | `:37-48` | 逐条比对 `build-manifest.json` 里 source/compiled/项目内 include 的 sha256 与包内字节 | 防止"编了 shader 但打进旧 .fxc" |
| 12 | `:49-56` | `Assets/ThirdParty/*/provenance.json` 的 LICENSE、provenance、每个资源文件在包内逐字节一致，且磁盘哈希与清单 sha256 一致 | 第三方素材的可追溯性 |
| 13 | `:57-60` | 所有 `.cs`/`.hjson`/`description.txt` 在包内与磁盘逐字节一致 | 防止"源码改了但包是旧的"（`includeSource=true` 才有意义） |
| 14 | `:62-64` | 复制包到 `artifacts/ZenithReborn[-<ver>].tmod` | — |
| 15 | `:65-80` | 打包 `-source.zip`（源码 + README + AGENTS + docs/checks/VfxLibrary/tools），`testzip()` 通过，且**不得夹带** `OniCrimsonSlash.fx/.fxc`、`NoiseSoft01.rawimg`、`SlashBrush01.rawimg` | 源码包同样受"去第三方"约束 |
| 16 | `:82-91` | `ZENITH_PACKAGE_ONLY=1` → 写 `package-result-<ver>.json` 后 `SystemExit(0)` | 候选包不得覆盖正式安装记录与稳定别名 |
| 17 | `:96-116` | 备份 `enabled.json`、旧包到 `artifacts/backups/<ts>/`；`.pending` + `os.replace` 原子替换；按需把依赖与自身追加进 `enabled.json` | 可回滚、断电安全 |
| 18 | `:117-123` | `target == built`；`config.json` 未变；既有 addon 哈希未变；`enabled.json` 原有条目顺序保留；`ZenithPrototypeCheck` **不在**启用列表 | 不污染玩家配置、不把夹具装进正式档 |
| 19 | `:124-215` | 逐项探测可选证据报告：UI、战斗、slash/ore GPU、airflow material/world、soul trail、independence、soul flight world（含 echo-damage、surface）、material lab、manual、superdummy、soul input、unit trails、martial motion/world、visible colors | **每项都要求 `testedPackageSha256`（或 `tested_package_sha256`）等于当前包哈希**；缺失或哈希不符 → 该字段 `False`，**不报错** |
| 20 | `:288` | 写 `artifacts/install-result.json`（73 键；`installed/enabled` 为真，其余为逐项布尔） | 交付回执 |

### 3.3 `run_flying_blade_check.ps1`（飞刃夹具；`AGENTS.md:7` 记 22 项，项数同样是断言计数，会随夹具改动变化）

1. `:7` `testProfile = check-profile`（**不是** wood-check-profile）。
2. `:9` `fbBuildDir = flying-blade-profile`（**独立构建目录**，注释 `:2/:8` 明说不能污染 `build-profile`）。
3. `:12` `Copy build-profile/Mods/Luminance.tmod → fbBuildDir/Mods/`（**复制不移动**，防 `build.txt` 恢复 `modReferences` 后构建缺依赖）。
4. `:14-16` 用**系统 SDK** `dotnet build ZenithReborn.csproj -c Release` → 刷新 `bin/Release/net8.0/ZenithReborn.dll`，
   因为夹具 csproj 是按该 DLL 的 `<Reference>` 编译的，不刷新就会引用陈旧程序集。
5. `:17-25` 两包分别 `-build ... -tmlsavedirectory fbBuildDir`，拷到 `check-profile`。
6. `:26` `enabled.json`。
7. `:27-28` `ZENITH_CHECK_REPORT=artifacts/flying-blade-check.txt`、`ZENITH_FLYING_BLADE_CHECK=1`。
8. `:31` 起 `-server -nosteam`，日志 `flying-blade-check-load.log`。
9. `:34` 打印报告（`PASS: <n> flying blade checks; …`）。
10. `:35` 非零 → `throw`。
    **注意**：本脚本不写 `native-build-check.json`，也不更新 `wood-check.txt`，因此对 `package_install` 完全"无害"。

### 3.4 `check_vfx_ownership.py::verify()`（对**已打包的 .tmod** 做包审计）

| 序 | 行 | 断言 | 作用 |
|---|---|---|---|
| 1 | `:10-12` | `read_archive` → `packed` 字典 | 逐条解包 |
| 2 | `:12-13` | 任何路径含 `Assets/ThirdParty/`、`OniCrimsonSlash`、`SlashBrush01`、`NoiseSoft01` → 失败 | **资源层**去第三方 |
| 3 | `:14-21` | `build-manifest.json` 每条 `origin=='project-authored'`、`thirdPartySourceIncludes` 为空、source/compiled 哈希与包内一致 | **来源层**：不许把第三方 shader 改名冒充自有 |
| 4 | `:26-27` | 包内所有 `.fx/.fxc/.fxh` 集合 **恰好等于**清单声明集合 | **反向**也查：多一个未登记 shader 就失败 |
| 5 | `:28-31` | 生产 `.cs` 不得出现 `Assets/ThirdParty`、`MartialCrescentMaterial`、`OreVisualRenderer`；包内不得有 `MartialCrescentMaterial.cs` | **代码层**：防止运行期把旧材质"带回来" |
| 6 | `:32` | 包内不得有 `GlassMaterialConfig.cs` | 制作端材质参数不得变成玩家设置 |
| 7 | `:33-38` | 返回 `passed / testedPackageSha256 / ownSwordBodyMaskRestored` 等 | 被 `package_install` 直接消费 |

### 3.5 `prepare_luminance.py`

| 序 | 行 | 动作/断言 |
|---|---|---|
| 1 | `:14-17` | 取 `--archive` 或 `ZENITH_LUMINANCE_ARCHIVE` |
| 2 | `:19-30` | 否则扫 `E:/…/workshop/content/1281930/3222493606/*/Luminance.tmod`，只接受分支号 `<= (2026,8)`，取最大；一个都没有 → `FileNotFoundError` |
| 3 | `:31-33` | `read_archive` 后 `name=='Luminance'`，否则 `ValueError` |
| 4 | `:34-43` | 只抽 `Luminance.dll/.pdb/.xml` 到 `artifacts/dependencies/Luminance/`（内容相同则不重写）；缺 DLL → `ValueError` |
| 5 | `:44-48` | `profiles` 参数非空时把 `Luminance.tmod` 拷进各 profile（**默认空**） |
| 6 | `:51` | 写 `artifacts/dependencies/Luminance/dependency.json` |
| **调用方** | — | `ZenithReborn.csproj` 的 `<Target Name="PrepareLuminance" BeforeTargets="ResolveReferences">` 里执行 `python checks/prepare_luminance.py`（**用 PATH 上的 python**，不用脚本里那套 runtime 探测）；`package_install.py:23` 在声明了 Luminance 时再调一次 |

### 3.6 `check_mod_assets.py::main()`

| 序 | 行 | 动作 |
|---|---|---|
| 1 | `:606` | 收集 `ZenithReborn/**/*.cs`（跳过 bin/obj/.git/.vs/.idea/node_modules） |
| 2 | `:504-512` | 每文件生成 `clean`（去注释、保留字符串）与 `masked`（字符串内容遮成空格、偏移不变）双份 |
| 3 | `:308-347` | 递归解析类声明，按花括号配平取类体；嵌套类型体从外层"自身成员"里剔除 |
| 4 | `:613-624` | `partial` 同名分片合并；用简单名建基类索引 |
| 5 | `:626-632` | 只保留非 abstract 且 `inherits_textured()` 为真的类 |
| 6 | `:643-671` | 逐类：自身/祖先有 `Texture =>` → PASS（并解析三元里的字符串字面量）；否则要求 `<模组根>/<ns 路径>/<类名>.png` 存在 |
| 7 | `:673-688` | 只存在于 `Assets/` 的同名 png → **警告**（不参与默认查找，等于没有） |
| 8 | `:690-757` | 汇总行 + 缺口清单（`文件:行号  类名 → key.png`），有缺口 `return 1`，否则 `PASS` |

### 3.7 谁消费谁的产出（证据链闭环）

```
check_mod_assets.py ─┬─▶ 退出码 ─▶ run_wood_checks 闸门1
                     └─▶ 不落盘（只打印）

compile_effects.py ──┬─▶ *.fxc ─────────────▶ 进包
                     └─▶ build-manifest.json ─▶ check_vfx_ownership / package_install:37-48

run_wood_checks.ps1 ─┬─▶ wood-check.txt ─────────────▶ package_install:18-20,25
                     └─▶ native-build-check.json ────▶ package_install:31-36

run_* 图形/世界夹具 ──▶ artifacts/<专项>-<ver>/*.json ─▶ package_install:124-215（按包哈希）
verify_own_effect.py ─▶ soul-trail-gpu-<ver>/independence.json ─▶ package_install:156
check_visible_weapon_colors.py ─▶ visible-colors-<ver>.json ─▶ package_install:280-287

package_install.py ──▶ install-result.json ─▶ audit_material_assets.py（installedMatchesReceipt）
                                         └─▶ collect_boss_unlock_evidence.py
```

---

## 第四部分：易踩坑清单

### 1. 必须用 `pwsh` 7，不能用 Windows PowerShell 5.1

`CODEBUDDY.md:23` 已写明原因：这批 `.ps1` 全是 **无 BOM 的 UTF-8**，里面有中文注释与中文 `throw` 字符串。
5.1 会按 GBK 解码导致解析期就报错（不是运行时才怪），而且 5.1 的 `*> file`、`-Encoding utf8`（会写 BOM）行为与 7 不同。
本目录里还有**三层嵌套**调用：`run_swordbody_batch.ps1:18`、`run_turbulence_batch.ps1:19`、`run_airflow_batch.ps1:10`
都用 `pwsh -NoProfile -File ...` 起子进程——`pwsh` 必须在 `PATH` 上（不是绝对路径），否则批量脚本第一把剑就失败。
另外三个脚本的 **python 解释器路径互不相同**（`~/.cache/codex-runtimes/.../python.exe` vs
`C:/Users/15537/.workbuddy/binaries/python/versions/3.13.12/python.exe` vs `PATH` 上的 `python`），
批量脚本换机器必挂。

### 2. "先跑飞刃夹具再安装"曾经必然失败：`build-profile` 被覆盖

`package_install.py:25` 的断言是 `report.stat().st_mtime > built.stat().st_mtime`。
旧版飞刃夹具用 `-tmlsavedirectory build-profile` 打包，会把 `build-profile/Mods/ZenithReborn.tmod`
**重新写一遍**，mtime 变新 → 报告比包旧 → 断言失败。
现在的修法（`run_flying_blade_check.ps1:8-9` 注释写得很直白）：**给夹具一个专用构建目录 `flying-blade-profile`**，
`wood-check.txt` 的 mtime 不再被刷新，安装脚本照常通过。

**但这个修复只做了一半**——以下脚本仍写 `build-profile`，在任何一次主线回归之后跑它们，都会让下一次
`package_install` 的时序断言失败（或让"证据"对应到另一个包）：
`run_flying_blade_survey.ps1`、`run_echo_phase_probe.ps1`、`run_airflow_export.ps1`、
`run_mask_preview.ps1`、`run_swordbody_preview.ps1`、`run_surge_preview.ps1`、`build_material_lab.ps1`。
安全顺序是：**所有重建型脚本跑完 → 再跑 `run_wood_checks.ps1` → 最后 `package_install.py`**。

### 3. `wood-check.txt` 的时序断言到底在挡什么

不是"文件旧了就重跑"这么简单，它挡的是三种具体事故：
- **A 包旧报告新**：改了源码没重新打包，报告却是上一轮 PASS → 装上的是旧代码。
- **B 包新报告旧**：报告是前一个版本的通过记录，包已被别的脚本重建 → 用旧证据给新包背书。
- **C 两者都对但不是一个包**：所以 `package_install.py:31-36` 还要比哈希——
  `native-build-check.json.packages.ZenithReborn.sha256` == 当前 `built` 的 sha256，
  且 `wood-check-profile/Mods/ZenithReborn.tmod` 与 `built` **逐字节相同**。
  哈希不匹配时不直接失败，而是退化成"包内 DLL == `bin/Release/net8.0/ZenithReborn.dll`"这条弱断言——
  也就是说**漏掉 `run_wood_checks` 直接安装仍可能过**，只是失去"这包被夹具跑过"的证明。
  这是本体系最容易被误读的一处：`focused_checks` 字段在这种退化路径下依然会写上一个数字。

### 4. 资产检查能拦什么、拦不住什么

**能拦**（`check_mod_assets.py`，0.3.127 `MissingResourceException: FlyingBladeShot` 的直接产物）：
- 新增 `ModItem/ModProjectile/ModBuff/ModNPC/ModDust` 子类，既无 `Texture =>` 覆盖、又无 `<类名>.png`；
- `Texture => condition ? "ZenithReborn/A" : "ZenithReborn/B"` 里被引用的 `ZenithReborn/*` 路径文件缺失；
- 只有 `Assets/xxx.png` 同名图（打印警告：Assets 不参与默认查找）。

**拦不住**：
- **服务端夹具永远拦不住贴图问题**——`ModContent.HasAsset` 在 `-server` 下恒 false，所以"夹具全绿"不证明客户端能加载；
- `.xnb`/`rawimg` 之外的资产格式问题、图集尺寸/像素格式错误、`Texture` 用**非字面量**（方法调用/常量拼接/反射）算出来的路径；
- 继承链只在**同仓库源码**里解析，基类来自原版或其它模组时只按类名匹配（`index` 用 `setdefault`，重名取先出现者）；
- 解析器是自研正则+花括号配平，`#if` 条件编译、源生成器产物、`global using` 别名不在覆盖范围；
- 它只查"能不能找到文件"，不查"文件内容对不对"。

### 5. fail-fast：第一条失败，后面根本不跑

`run_wood_checks.ps1:2` 全局 `$ErrorActionPreference='Stop'`，每一步 `$LASTEXITCODE -ne 0` 就 `throw`；
夹具侧 `Assert` 一抛异常整个 `RunChecks()` 中断（`Check.cs:15-16`：`if(!ok) throw`，`checks++` 在 throw 之后）。
后果：
- **一次只暴露一个问题**。8511 项里第 900 项挂了，剩下 7611 项是"没跑"，不是"通过"。
- 报告 txt 只在**全部通过后**才写（`Check.cs:161`）。失败时磁盘上留的是**上一轮的 PASS**，
  这正是为什么必须有时序断言 + `native-build-check.json` 的 `passed:false`。
- 修一个、跑一次、再发现下一个，是这个目录的常规节奏；不要指望一次运行给出完整失败清单。
- 例外：`run_swordbody_batch.ps1` / `run_turbulence_batch.ps1` 用 `$ErrorActionPreference='Continue'`
  且逐剑记录 `OK/GPU-FAIL/REGISTER-FAIL`，是**唯一**两条"继续跑完再汇总"的批处理。

### 6. 报告项数为什么是动态的（8453 / 8498 / 8503 / 8507 / 8511 都出现过）

`checks` 是 `Assert` 的调用次数计数器（`Check.cs:13-16`），没有期望值、没有上限。它变的三个原因：

1. **分支不同套件不同**：`Check.cs` 按环境变量顺序判分支（`GLASS` :69 → `INFORMATION_UI` :75 →
   `ECHO_ORE` :83 → `WOOD` :125 → `STAGE_ONE` :164 → …），每个分支召唤完全不同的夹具组合，
   项数量级就不同。所以 `run_wood_checks.ps1 -EchoOre` 写的 `wood-check.txt` 是"三条伤害检查"的项数，
   **不是**主回归的项数，而 `package_install` 只校验前缀 `PASS: `、照样接受 —— 这是本目录最危险的静默收窄。
2. **数据驱动**：夹具里大量 `foreach` 遍历真实内容（`WoodCheck.cs:49` `foreach (int type in WoodTypes.Types)`、
   矿石族 14 材质、6 诅咒、前缀矩阵），内容加一把剑/一个材质，项数就涨。
3. **夹具本身在改**：0.3.130 记录写 8498（`docs/0.3.130-木质族V2重做与口径统一.md:30`），
   而当前 `artifacts/wood-check.txt` 与 `install-result.json` 都是 **8511** —— 同一个版本号下，
   夹具后来又加/改了断言。历史值 8453（`AGENTS.md:7`，0.3.128）、8498、8511 均可在仓库内定位；
   **8503 / 8507 在仓库文本里找不到出处**，只能当"口头历史值"看。

**因此项数不能当验收标准**：它只能证明"这轮比上轮多/少跑了多少条"，不能证明覆盖了哪些规则。
跨版本比较项数会得到错误结论。

### 7. 一批 `check_*_delivery.py` 是"冷冻证据"，对当前版本必然失败

`check_visible_weapon_colors.py:16` `assert version == '0.3.88'`、`check_color79.py:34` `'0.3.87'`、
`check_hybrid_preservation.py:31` `'0.3.86'`、`check_day_night_delivery.py:41` `'0.3.89'`、
`check_glass_r3_delivery.py:8` `'0.3.80'`、`check_glass_visibility_delivery.py:10` `'0.3.81'`、
`check_sword_color_delivery.py:10` `'0.3.82'`、`check_color_glass_restoration.py:10` `'0.3.83'`、
`check_glass_reference_restore.py:10` `'0.3.84'`、`check_vfx_rollback_0379.py:13` 默认 `'0.3.85'`。
它们还会断言"改动文件集合恰好等于白名单"（`check_visible_weapon_colors.py:29-30`），
在 0.3.130 上必然不符。**不要把它们塞进主线或 CI**；它们是"当时那一版改了什么、证据哈希是多少"的存档。
同理，`run_archived_vfx_capture.ps1` 把 0.3.79/0.3.80 两个包的 sha256 写死在脚本里（`c7b8842…` / `6473635…`），
归档包一旦被清理，脚本直接 `throw '原始包哈希不符'`。

### 8. 报告字段有**两种拼写**，写错不会报错，只会静默变 False

`package_install.py` 同时读：
- 驼峰 `testedPackageSha256`（`matching_report()` `:151`、UI/战斗/调色板/independence 等）；
- 下划线 `tested_package_sha256`（slash/ore GPU `:139/:144`、martial `:213-215`）。

夹具若把字段名写成另一种，`matching_report()` 返回 `False`，安装回执里对应能力就是 `False` ——
**看起来像"没测"，实际是"测了但字段名不匹配"**。同理 `run_pure_code_vfx.ps1` 写的
`artifacts/pure-code-vfx-<ver>/independence.json` 里没有 `testedPackageSha256`，
所以它**永远不会**被 `package_install:156` 认成 `own_shader_independence_gpu_tested`；
真正被消费的是 `verify_own_effect.py` 写的 `artifacts/soul-trail-gpu-<ver>/independence.json`。
两处同名 `independence.json` 分属不同生产者/消费者，读回执时必须看清路径。

### 9. 其他会咬人的细节

- **"只读"脚本其实会写生产源码/资产**：`run_wood_checks.ps1` 会经 `emit_glass_preset.py` 重写
  `ZenithReborn/GlassMaterialPreset.cs`、经 `compile_effects.py` 重写 `.fxc` 与 `build-manifest.json`；
  `generate_noise.py` 直接覆盖 `NoiseSeamless.png`；`prepare_boss_portraits.py` 生成 `SwordBossPortraitCatalog.cs`。
  所以"跑一遍回归"会产生 git 脏文件，**这是设计如此**，不是事故。
- **批量脚本会杀光所有 dotnet 进程**：`run_swordbody_batch.ps1:22`（失败重试前还会在 `:34` 再杀一次）与
  `run_turbulence_batch.ps1:16`（`:25` 失败后再杀）执行
  `Get-Process -Name dotnet | Where Id -ne $PID | Stop-Process -Force`，
  与 `AGENTS.md:56` "不得自行强杀游戏"直接冲突。跑它们之前必须确认没有正在运行的 tModLoader/主回归。
- **`run_surge_preview.ps1:5` 把 `taskRoot` 硬编码成绝对路径**，仓库换目录即失效；
  它也是少数会 `Stop-Process -Force` 的 runner。
- **`run_information_ui_render.ps1` 的 50 秒上限**：报告没出现就关窗口，2 秒不退再强杀。
  冷启动（首次解包两包）经常超过 50 秒 → 表现为"信息页绘制超时"，不是界面代码坏了。
- **`run_echo_phase_probe.ps1` 不校验退出码**：跑完只 `Get-Content` txt，即使客户端崩了也"看起来成功"；
  且它写 `build-profile`（见坑 2）。
- **`ZenithPrototypeCheck/build.txt` 声明 `modReferences = ZenithReborn`**，所以打包夹具时
  `-tmlsavedirectory` 里必须已有 `ZenithReborn.tmod`；`ZenithPrototypeCheck.csproj` 又按
  `<HintPath>../../ZenithReborn/bin/Release/net8.0/ZenithReborn.dll</HintPath>` 编译 ——
  **夹具的编译期依赖是 DLL，运行期依赖是 tmod**，两者不同步时会出现"能编译、跑起来类型不匹配"。
- **`Mods/` 里残留 `ZenithPrototypeCheck.dll`、`FamilyAudit.tmod`**（`build-profile/Mods/` 实读），
  它们是历史遗留，不影响 `enabled.json`（只列两包），但排查"为什么多加载了一个东西"时要想到。

---

## 未确认项

1. `run_echo_phase_probe.ps1` 的 `tModLoader -server` 退出码语义（夹具在此分支写的是自由文本，不写 `PASS:`）——
   无法仅凭脚本判断它是否**永远**返回 0。
2. `compile_effects.py` 用 `fx_2_0` profile 编译，而 `d3dcompiler_43.dll` 是 DX9 时代产物；
   当前 4 个 `.fx` 是否全部只用 SM2 指令集，脚本本身不做校验（编译不过就抛，但"能编过"不等于"在 DX11 后端表现一致"）。
3. `package_install.py:25` 的时序退化路径（坑 3 情形 C）在真实使用时是否被接受，仓库内没有文档明确表态。
4. 历史项数 8503 / 8507 的出处未在仓库文本中找到（仅 8453 / 8498 / 8511 可定位）。
5. `checks/archived-*` 三个子目录（共 39 个文件）按要求未展开分析，只统计了数量。
