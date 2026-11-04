# 调查报告 I：tools/ 与 VfxLibrary

> 调查范围：`tools/`（222 文件，5 个子目录 + 6 个根文件）与 `VfxLibrary/`（11756 文件）。
> 方法：只读。文件数、分组统计、清单全部由 shell 命令生成，不手抄。
> 生成物：本文件 + `artifacts/codebase-map/vfx-library-manifest.txt`（11756 行）。

---

## 第一部分：tools/ 模块说明

### 总览

`tools/` 是**制作端工具区**，不参与运行时。`tools/README.md:3-9` 已经把四个条目定死：`ZenithMaterialLab`（材质调参工作台）、`xnbcli`（XNB 解包）、`decomp`/`ilspy`（反编译工具与产物）、`build_vanilla_vfx_catalog.py`（重建原版特效图鉴）。打包脚本 `checks/package_install.py:70` 显式把 `tools` 与 `docs`、`checks`、`VfxLibrary` 一起从成品模组包中排除。

| 子目录 | 文件数 | 来源 | 定位 |
|---|---:|---|---|
| `tools/`（根，散文件） | 6 | 自研 | 入口脚本 + 图鉴重建脚本 + 第三方下载包 |
| `tools/ZenithMaterialLab/` | 44 | **自研** | 独立 tModLoader 制作模组（材质实验室） |
| `tools/decomp/` | 8 | 第三方产物 | ILSpy 反编译出的 Terraria 源码片段 |
| `tools/ilspy/` | 68 | **第三方** | ILSpy 9.1.0.7988 命令行工具（ilspycmd） |
| `tools/xnbcli/` | 60 | **第三方** | xnbcli 1.0.7 源码（XNB 打包/解包，Node.js） |
| `tools/xnbcli-binary/` | 36 | **第三方** | xnbcli 1.0.7 的 Windows 二进制分发 |
| **合计** | **222** | | |

### 1.1 `tools/` 根文件（6 个，均为自研或第三方压缩包）

| 文件 | 类型 | 用途 | 关键位置 |
|---|---|---|---|
| `tools/README.md` | 文档 | 目录说明表，四条工具各自的用途与入库策略 | `tools/README.md:3` |
| `tools/build_vanilla_vfx_catalog.py` | Python | 重建 `VfxLibrary/Vanilla/` 可搜索图鉴：扫描 `Images/` 原尺寸 PNG、生成 128px 缩略图、按原版 ID 常量反查英文名与中文主题标签、从 `粒子图集/Dust.png` 切 324 个 Dust 小图、输出 `catalog.js` + `manifest.json` | `tools/build_vanilla_vfx_catalog.py:1-30` |
| `tools/desktop_control.ps1` | PowerShell | 桌面自动化：`user32.dll` 交互（`SetCursorPos`/`mouse_event`/`FindWindow`/`SetForegroundWindow`），按窗口标题匹配 `泰拉瑞亚|tModLoader|Terraria` 聚焦游戏窗口，用于无人值守截图/点击验证 | `tools/desktop_control.ps1:15-21` |
| `tools/打开材质实验室.cmd` | 批处理 | 转调 `checks/run_material_lab.ps1` | `tools/打开材质实验室.cmd:2` |
| `tools/打开能量薄刃A1.cmd` | 批处理 | 转调 `checks/run_energy_blade_preview.ps1 -Interactive` | `tools/打开能量薄刃A1.cmd:2` |
| `tools/xnbcli-windows-x64.zip` | 压缩包（12.2 MB） | xnbcli Windows x64 发行包下载原件 | 不入库 |

### 1.2 `tools/ZenithMaterialLab/` —— **自研**，是独立模组

**结论：这是一个独立的 tModLoader 模组（`Mod` 子类 + `build.txt` + `description.txt`），不是主模组的子模块，也不进正式游玩组合。**

- 模组身份：`tools/ZenithMaterialLab/ZenithMaterialLab.cs:5` `public sealed class ZenithMaterialLab : Mod {}`；`tools/ZenithMaterialLab/build.txt` 声明 `displayName = 天顶材质实验室（独立制作模组）`、`version = 0.1.3`、`modReferences = ZenithReborn@0.3.80`。
- 依赖方向：实验室**单向依赖**主模组 —— `tools/ZenithMaterialLab/ZenithMaterialLab.csproj:8` `<Reference Include="ZenithReborn"><HintPath>../../ZenithReborn/bin/Release/net8.0/ZenithReborn.dll</HintPath></Reference>`。主模组不引用实验室，所以 `checks/package_install.py` 排除 `tools/` 后主包仍可正常构建。
- 复用而非重画：`tools/ZenithMaterialLab/README.md:3` 明确 "与正式飞剑共用 `SurfaceMaterialDefinition`、`SurfaceMaterialRenderer` 及 `ZRMaterialSurface`，不会另外画一套只用于展示的材质"。
- 运行档案：`artifacts/material-lab-profile` 独立客户端档案，只启用天顶复苏 + 实验室，不复制正式人物/世界（`tools/ZenithMaterialLab/README.md:5`）。
- 界面结构：`MaterialLabSystem.cs`（184 行，工作台主界面与输入）、`LabPreview.cs`（64 行，预览渲染）、`LabResources.cs`（130 行，素材槽加载/刷新）、`LabChecks.cs`（127 行，45 组 GPU A/B 逐项核对）、`LabArtworkCheck.cs`（64 行，素材哈希核对）、`LabPipelineCheck.cs`（87 行，管线状态检查）、`LabRegionCheck.cs`（55 行，用真实打包载荷检查 RGBA 四区，特别是 Alpha=0 处的 RGB 是否被保留）。
- 数据出口：导出到 `MaterialLab/Exports/<时间戳>/`（material.json + 引用的 PNG/原始区域数据 + 哈希清单），再由 `checks/import_material_export.py <导出目录> --weapon <Name>`（先 dry-run，`--apply` 才写源码）落回主模组并登记到 VfxLibrary。**导出不会自动改正式包**（`tools/ZenithMaterialLab/README.md:41`）。

### 1.3 `tools/decomp/` —— 第三方工具产物（不是自研代码）

ILSpy 对 `tModLoader.dll` / `Terraria.dll` 的反编译输出。由 `docs/archive/需求-天顶飞剑拖尾与剑影形态.md:59-63` 记录：工具 `tools/ilspy`（ILSpy 9.1.0.7988），产物为下列 4 个文件。

| 文件 | 行数 | 说明 |
|---|---:|---|
| `Terraria.Main.decompiled.cs` | 87341 | 3.9 MB。原版主类，查光照/绘制顺序 |
| `Terraria.Projectile.decompiled.cs` | 94255 | 3.9 MB。原版弹幕（含飞剑、拖尾）实现 |
| `Terraria.ID.ProjectileID.decompiled.cs` | 2864 | 92 KB。弹幕 ID 常量表 —— 被 `tools/build_vanilla_vfx_catalog.py:11` **直接读取**用来给贴图反查英文名 |
| `Terraria.Graphics.FinalFractalHelper.decompiled.cs` | 204 | 天顶剑最终分形绘制（Zenith 多剑齐射的视觉核心） |
| `Terraria.Graphics.VertexStrip.decompiled.cs` | 264 | 顶点带（拖尾网格）构造器 |
| `Terraria.Graphics.Light.LightingMode.decompiled.cs` | **0** | **空文件**，反编译失败或中断的残留 |
| `Terraria.LightMode.decompiled.cs` | **0** | **同上，空文件** |
| `tModLoader.list.txt` | 160 | 枚举/类型名清单（`Enum Terraria.*` 逐行），用于查类型存在性 |

> 两个 0 字节文件是**未确认项**：无法判断是反编译失败、被清空，还是故意留占位。

### 1.4 `tools/ilspy/` —— **第三方** ILSpy 9.1.0.7988

- 版本证据：`tools/ilspy/pkg/ilspycmd.nuspec` → `<id>ilspycmd</id>`、`<version>9.1.0.7988</version>`；与 `docs/archive/需求-天顶飞剑拖尾与剑影形态.md:59` 记录一致。
- 结构：`ilspycmd.zip`（NuGet 包原件，4.2 MB）+ `index.json`（版本索引清单）+ `pkg/`（解开的 NuGet 包，66 个文件：`ilspycmd.dll`、`ICSharpCode.Decompiler.dll`、`ICSharpCode.ILSpyX.dll`、`Mono.Cecil*`、`NuGet.*`、27 个 `Microsoft.Extensions.*`、`K4os.Compression.LZ4`、`McMaster.Extensions.CommandLineUtils`，以及 4 个 `runtimes/win/**` 平台特化程序集）。
- **不深度分析**：全是官方发布二进制，项目未改动其源码。
- 用途：一次性/按需反编译原版与 tModLoader 程序集，产出 `tools/decomp/`。可由 `tModLoader.dll` 重新生成，故不入库（见 1.7）。

### 1.5 `tools/xnbcli/` 与 `tools/xnbcli-binary/` —— **第三方** xnbcli 1.0.7

- 上游：`https://github.com/LeonBlade/xnbcli`（`tools/xnbcli/package.json:15`）；`package.json:2` `"version": "1.0.7"`；许可证 GNU LGPL v3.0（`tools/xnbcli/README.md:36`）+ `LICENSE.LESSER` / `LICENSE.md` 双许可文件。
- 用途：XNB 打包/解包 CLI。原本为《星露谷物语》编写，本项目用来解包泰拉瑞亚原版 `Content/Images/*.xnb`（见 `VfxLibrary/Vanilla/README.md:3`）。
- 命令：`xnbcli (pack|unpack) [input] [output]`，或把文件丢进 `packed/` 跑 `unpack.bat`（`tools/xnbcli/README.md:15-27`）。
- `tools/xnbcli/`（60 文件）是**完整源码**：`xnbcli.js` 入口；`app/Porter.js` 编解码主流程；`app/Presser/Lzx.js`（31 KB）LZX 解压；`app/Xnb/index.js` + `app/Xnb/Readers/*.js`（24 个 reader：`Texture2DReader`、`TBinReader`、`EffectReader`、`BmFontReader`、`SpriteFontReader` 等类型分发）；`app/Xact/*` 音频；`app/BufferReader.js`/`BufferWriter.js` 二进制读写；`package.json`/`package-lock.json` 依赖（`lz4`、`pngjs`、`dxt-js`、`chalk`、`commander`）。
- `tools/xnbcli-binary/`（36 文件）是**编译好的分发**：`xnbcli.exe`、`lz4.node`、`xxhash.node`、`pack.bat`/`unpack.bat`、`packed/PACKED.md`、`unpacked/UNPACKED.md`，外加 `xnbcli-upstream-git/`（29 个文件）—— 这是上游仓库的 **shallow clone 的 `.git` 目录**（`HEAD`、`config`、`shallow`、`objects/pack/pack-b6c07b1….pack` 等），用来固定上游 commit。
- **不深度分析**：第三方 Node.js/原生二进制，项目未改。

### 1.6 入库情况（`.gitignore` 实测）

实测用 `git check-ignore -v` 逐条验证规则命中：

| 路径 | 是否入库 | 命中规则 |
|---|---|---|
| `tools/README.md` | 入库 | — |
| `tools/build_vanilla_vfx_catalog.py` | 入库 | — |
| `tools/desktop_control.ps1` | 入库 | — |
| `tools/打开材质实验室.cmd` / `打开能量薄刃A1.cmd` | 入库 | — |
| `tools/ZenithMaterialLab/*.cs`、`.csproj`、`build.txt`、`description.txt`、`README.md`、`Localization/*.hjson`、`Assets/Diagnostics/RegionMask.zrdata` | **入库（15 个）** | — |
| `tools/ZenithMaterialLab/bin/**`（3 个） | 忽略 | `.gitignore:1` `**/bin/` |
| `tools/ZenithMaterialLab/obj/**`（26 个） | 忽略 | `.gitignore:2` `**/obj/` |
| `tools/decomp/**`（8 个） | 忽略 | `.gitignore:26` `tools/decomp/` |
| `tools/ilspy/**`（68 个） | 忽略 | `.gitignore:25` `tools/ilspy/` |
| `tools/xnbcli/**`（60 个） | **入库** | 脚本源码保留 |
| `tools/xnbcli-binary/**`（36 个） | 忽略 | `.gitignore:28` `tools/xnbcli-binary/` |
| `tools/xnbcli-windows-x64.zip` | 忽略 | `.gitignore:29` |

`.gitignore:24-29` 的注释给了理由："反编译工具与其输出：体积大（约 22MB），可由 tModLoader.dll 重新生成，不入库"；"XNB 解包工具二进制与下载压缩包：体积大（约 50MB），可从开源仓库重新获取，不入库；脚本源码 `tools/xnbcli/` 保留"。

**实测结果：`git ls-files tools` = 80 个已跟踪 / 222 个磁盘文件**（差额 142 = ilspy 68 + decomp 8 + xnbcli-binary 36 + bin 3 + obj 26 + zip 1）。这个划分是自洽的：能重新生成或能重新下载的一律不入库，只有**源码、脚本、配置**入库。

### 1.7 与主项目的关系（引用链条）

```
tools/ilspy (ILSpy 9.1)  ──反编译──>  tools/decomp/*.decompiled.cs  ──读取──>  tools/build_vanilla_vfx_catalog.py
                                                                              │
tools/xnbcli (1.0.7)  ──解包原版 XNB──>  VfxLibrary/Vanilla/Images/*.png  <──┘
                                              │
                                              ▼
                                    VfxLibrary/Vanilla/catalog.js + 浏览原版特效.html
                                    （只在浏览器里看，不进游戏）

tools/ZenithMaterialLab (独立模组)  ──导出──>  MaterialLab/Exports/<ts>/
                                              │  checks/import_material_export.py --apply
                                              ▼
                                    ZenithReborn/ 源码 + VfxLibrary 登记
```

- `tools/decomp/Terraria.ID.ProjectileID.decompiled.cs` 被 `tools/build_vanilla_vfx_catalog.py:11` **在构建图鉴时读取**（硬编码路径）。
- `tools/decomp/Terraria.Main.decompiled.cs`、`Terraria.Projectile.decompiled.cs`、`FinalFractalHelper`、`VertexStrip` 是**人工查阅用**，没有任何脚本消费它们；结论被写进 `docs/原版天顶飞剑源码.md` 与 `docs/archive/*`。
- `tools/ZenithMaterialLab` 的 `SurfaceMaterialDefinition` / `SurfaceMaterialRenderer` / `ZRMaterialSurface` 全部来自主模组 DLL。实验室是**主模组的消费者**，不是主模组的一部分。

---

## 第二部分：tools/ 文件索引（222/222 全覆盖）

状态标记：`已验证` = 自研，已读；`第三方-未深入` = 第三方，只记录版本与用途。

### 2.1 `tools/` 根（6）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `tools/README.md` | md | tools 根 | 目录总说明表 | 指向其余 4 项 | 已验证 |
| `tools/build_vanilla_vfx_catalog.py` | py | tools 根 | 重建 Vanilla 图鉴 | 读 `decomp/ProjectileID` + `artifacts/vanilla-*ID.reference.cs` | 已验证 |
| `tools/desktop_control.ps1` | ps1 | tools 根 | 窗口聚焦/鼠标键盘自动化 | `user32.dll` | 已验证 |
| `tools/打开材质实验室.cmd` | bat | tools 根 | 启动材质实验室 | `checks/run_material_lab.ps1` | 已验证 |
| `tools/打开能量薄刃A1.cmd` | bat | tools 根 | 启动能量薄刃 A1 预览 | `checks/run_energy_blade_preview.ps1` | 已验证 |
| `tools/xnbcli-windows-x64.zip` | zip | tools 根 | xnbcli 发行包下载件（12.2 MB） | 与 `xnbcli-binary/` 同源 | 第三方-未深入 |

### 2.2 `tools/decomp/`（8）

| 文件路径 | 类型 | 所属模块 | 文件职责 | 关键关联 | 分析状态 |
|---|---|---|---|---|---|
| `tools/decomp/Terraria.Main.decompiled.cs` | cs | decomp | 原版 `Terraria.Main` 反编译（87341 行 / 3.9 MB） | 光照与绘制顺序查证 | 第三方-未深入 |
| `tools/decomp/Terraria.Projectile.decompiled.cs` | cs | decomp | 原版 `Terraria.Projectile`（94255 行 / 3.9 MB） | 飞剑/拖尾行为对齐 | 第三方-未深入 |
| `tools/decomp/Terraria.ID.ProjectileID.decompiled.cs` | cs | decomp | 弹幕 ID 常量表（2864 行） | **被 `build_vanilla_vfx_catalog.py:11` 读取** | 第三方-未深入 |
| `tools/decomp/Terraria.Graphics.FinalFractalHelper.decompiled.cs` | cs | decomp | 天顶剑最终分形绘制（204 行） | 多剑齐射视觉 | 第三方-未深入 |
| `tools/decomp/Terraria.Graphics.VertexStrip.decompiled.cs` | cs | decomp | 顶点带网格构造器（264 行） | 拖尾几何 | 第三方-未深入 |
| `tools/decomp/Terraria.Graphics.Light.LightingMode.decompiled.cs` | cs | decomp | **0 字节，空文件** | 未确认原因 | 第三方-未深入 |
| `tools/decomp/Terraria.LightMode.decompiled.cs` | cs | decomp | **0 字节，空文件** | 未确认原因 | 第三方-未深入 |
| `tools/decomp/tModLoader.list.txt` | txt | decomp | 类型/枚举名清单（160 行） | 查类型存在性 | 第三方-未深入 |

### 2.3 `tools/ilspy/`（68）—— 全部 `第三方-未深入`

**根（2）**

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `tools/ilspy/ilspycmd.zip` | zip | NuGet 包原件（4.18 MB） |
| `tools/ilspy/index.json` | json | 上游版本索引清单 |

**`pkg/` 包元数据（7）**

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `tools/ilspy/pkg/.signature.p7s` | p7s | NuGet 包签名 |
| `tools/ilspy/pkg/ILSpyCmdNuGetPackageIcon.png` | png | 包图标 |
| `tools/ilspy/pkg/README.md` | md | 包说明 |
| `tools/ilspy/pkg/[Content_Types].xml` | xml | OPC 内容类型 |
| `tools/ilspy/pkg/_rels/.rels` | rels | OPC 根关系 |
| `tools/ilspy/pkg/ilspycmd.nuspec` | nuspec | **版本 9.1.0.7988 的唯一证据** |
| `tools/ilspy/pkg/package/services/metadata/core-properties/b41afabca47440d49a71e4d9b3a71796.psmdcp` | psmdcp | NuGet 核心属性 |

**`pkg/tools/net8.0/any/` 主程序与依赖（55）**

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `.../any/DotnetToolSettings.xml` | xml | dotnet tool 元数据 |
| `.../any/ilspycmd.dll` | dll | **ILSpy CLI 主程序** |
| `.../any/ilspycmd.pdb` | pdb | 符号 |
| `.../any/ilspycmd.deps.json` | json | 依赖图 |
| `.../any/ilspycmd.runtimeconfig.json` | json | 运行时配置 |
| `.../any/ICSharpCode.Decompiler.dll` | dll | **反编译核心（2.56 MB）** |
| `.../any/ICSharpCode.Decompiler.xml` | xml | IntelliSense 文档（962 KB） |
| `.../any/ICSharpCode.ILSpyX.dll` | dll | ILSpy 扩展层 |
| `.../any/K4os.Compression.LZ4.dll` | dll | LZ4 解压 |
| `.../any/McMaster.Extensions.CommandLineUtils.dll` | dll | CLI 参数解析 |
| `.../any/McMaster.Extensions.Hosting.CommandLine.dll` | dll | CLI 宿主集成 |
| `.../any/Mono.Cecil.dll` | dll | 程序集读取（361 KB） |
| `.../any/Mono.Cecil.Mdb.dll` | dll | MDB 符号读取 |
| `.../any/Mono.Cecil.Pdb.dll` | dll | PDB 符号读取 |
| `.../any/Mono.Cecil.Rocks.dll` | dll | Cecil 扩展 |
| `.../any/Newtonsoft.Json.dll` | dll | JSON |
| `.../any/NuGet.Common.dll` | dll | NuGet 基础设施 |
| `.../any/NuGet.Configuration.dll` | dll | NuGet 配置 |
| `.../any/NuGet.Frameworks.dll` | dll | NuGet 框架解析 |
| `.../any/NuGet.Packaging.dll` | dll | NuGet 打包 |
| `.../any/NuGet.Protocol.dll` | dll | NuGet 协议 |
| `.../any/NuGet.Versioning.dll` | dll | NuGet 版本 |
| `.../any/System.Collections.Immutable.dll` | dll | 不可变集合 |
| `.../any/System.Composition.AttributedModel.dll` | dll | MEF 组合 |
| `.../any/System.Diagnostics.EventLog.dll` | dll | 事件日志 |
| `.../any/System.Reflection.Metadata.dll` | dll | 元数据读取 |
| `.../any/System.Security.Cryptography.Pkcs.dll` | dll | PKCS 签名 |
| `.../any/System.Security.Cryptography.ProtectedData.dll` | dll | DPAPI |
| `.../any/Microsoft.Extensions.Configuration.dll` + `.Abstractions` + `.Binder` + `.CommandLine` + `.EnvironmentVariables` + `.FileExtensions` + `.Json` + `.UserSecrets`（8） | dll | 配置系统 |
| `.../any/Microsoft.Extensions.DependencyInjection.dll` + `.Abstractions`（2） | dll | DI 容器 |
| `.../any/Microsoft.Extensions.Diagnostics.dll` + `.Abstractions`（2） | dll | 诊断 |
| `.../any/Microsoft.Extensions.FileProviders.Abstractions.dll` + `.FileProviders.Physical.dll` + `.FileSystemGlobbing.dll`（3） | dll | 文件系统抽象 |
| `.../any/Microsoft.Extensions.Hosting.dll` + `.Abstractions`（2） | dll | 宿主 |
| `.../any/Microsoft.Extensions.Logging.dll` + `.Abstractions` + `.Configuration` + `.Console` + `.Debug` + `.EventLog` + `.EventSource`（7） | dll | 日志 |
| `.../any/Microsoft.Extensions.Options.dll` + `.ConfigurationExtensions`（2） | dll | 选项 |
| `.../any/Microsoft.Extensions.Primitives.dll` | dll | 基础原语 |

**`pkg/tools/net8.0/any/runtimes/` 平台特化（4）**

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `.../runtimes/win/lib/net6.0/System.Security.Cryptography.Pkcs.dll` | dll | Windows 特化 PKCS |
| `.../runtimes/win/lib/net8.0/System.Diagnostics.EventLog.dll` | dll | Windows 特化事件日志 |
| `.../runtimes/win/lib/net8.0/System.Diagnostics.EventLog.Messages.dll` | dll | 事件日志消息资源 |
| `.../runtimes/win/lib/netstandard2.0/System.Security.Cryptography.ProtectedData.dll` | dll | DPAPI 兜底实现 |

> 计数校验：根 2（`ilspycmd.zip`、`index.json`）+ 包元数据 7 + `net8.0/any` 直下 55 + `runtimes` 4 = **68 ✓**

### 2.4 `tools/xnbcli/`（60）—— 全部 `第三方-未深入`

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `tools/xnbcli/xnbcli.js` | js | CLI 入口（7.7 KB） |
| `tools/xnbcli/README.md` | md | 用法、许可 |
| `tools/xnbcli/LICENSE.LESSER` | 文本 | LGPL v3 正文（7.8 KB） |
| `tools/xnbcli/LICENSE.md` | md | 许可说明（35.8 KB） |
| `tools/xnbcli/package.json` | json | **版本 1.0.7**、依赖、脚本 |
| `tools/xnbcli/package-lock.json` | json | 依赖锁定（13.4 KB） |
| `tools/xnbcli/.gitignore` | 配置 | 上游自身忽略规则 |
| `tools/xnbcli/.travis.yml` / `.travis.build.sh` | 配置 | 上游 CI |
| `tools/xnbcli/appveyor.yml` | 配置 | 上游 CI |
| `tools/xnbcli/pack.bat` / `pack.command` / `pack.sh` | 脚本 | 打包入口（3 平台） |
| `tools/xnbcli/unpack.bat` / `unpack.command` / `unpack.sh` | 脚本 | 解包入口（3 平台） |
| `tools/xnbcli/packed/PACKED.md` | md | 待解包投放说明 |
| `tools/xnbcli/unpacked/UNPACKED.md` | md | 待打包投放说明 |
| `tools/xnbcli/app/BufferReader.js` | js | 二进制流读取（15.7 KB） |
| `tools/xnbcli/app/BufferWriter.js` | js | 二进制流写入 |
| `tools/xnbcli/app/Enum.js` | js | 枚举辅助 |
| `tools/xnbcli/app/Log.js` | js | 日志 |
| `tools/xnbcli/app/Porter.js` | js | **XNB 编解码主流程** |
| `tools/xnbcli/app/Struct.js` | js | 结构描述 |
| `tools/xnbcli/app/XnbError.js` | js | 错误类型 |
| `tools/xnbcli/app/Presser/index.js` | js | 压缩分发 |
| `tools/xnbcli/app/Presser/Lzx.js` | js | **LZX 解压（31.4 KB）** |
| `tools/xnbcli/app/Xnb/index.js` | js | XNB 容器读写（13.5 KB） |
| `tools/xnbcli/app/Xnb/TypeReader.js` | js | 类型读取基类 |
| `tools/xnbcli/app/Xnb/ReaderResolver.js` | js | reader 解析分发 |
| `tools/xnbcli/app/Xnb/Readers/BaseReader.js` | js | reader 基类 |
| `tools/xnbcli/app/Xnb/Readers/index.js` | js | reader 注册表 |
| `tools/xnbcli/app/Xnb/Readers/Texture2DReader.js` | js | **贴图解包（本项目主用途）** |
| `tools/xnbcli/app/Xnb/Readers/TBinReader.js` | js | TBin（泰拉瑞亚自定义二进制） |
| `tools/xnbcli/app/Xnb/Readers/EffectReader.js` | js | 着色器效果 |
| `tools/xnbcli/app/Xnb/Readers/BmFontReader.js` | js | BMFont |
| `tools/xnbcli/app/Xnb/Readers/SpriteFontReader.js` | js | SpriteFont |
| `tools/xnbcli/app/Xnb/Readers/SoundEffectReader.js` | js | 音效 |
| `tools/xnbcli/app/Xnb/Readers/ReflectiveReader.js` | js | 反射类型 |
| `tools/xnbcli/app/Xnb/Readers/ArrayReader.js` | js | 数组 |
| `tools/xnbcli/app/Xnb/Readers/ListReader.js` | js | 列表 |
| `tools/xnbcli/app/Xnb/Readers/DictionaryReader.js` | js | 字典 |
| `tools/xnbcli/app/Xnb/Readers/NullableReader.js` | js | 可空 |
| `tools/xnbcli/app/Xnb/Readers/StringReader.js` | js | 字符串 |
| `tools/xnbcli/app/Xnb/Readers/CharReader.js` | js | 字符 |
| `tools/xnbcli/app/Xnb/Readers/BooleanReader.js` | js | 布尔 |
| `tools/xnbcli/app/Xnb/Readers/Int32Reader.js` | js | Int32 |
| `tools/xnbcli/app/Xnb/Readers/UInt32Reader.js` | js | UInt32 |
| `tools/xnbcli/app/Xnb/Readers/SingleReader.js` | js | float |
| `tools/xnbcli/app/Xnb/Readers/DoubleReader.js` | js | double |
| `tools/xnbcli/app/Xnb/Readers/RectangleReader.js` | js | Rectangle |
| `tools/xnbcli/app/Xnb/Readers/Vector2Reader.js` | js | Vector2 |
| `tools/xnbcli/app/Xnb/Readers/Vector3Reader.js` | js | Vector3 |
| `tools/xnbcli/app/Xnb/Readers/Vector4Reader.js` | js | Vector4 |
| `tools/xnbcli/app/Xact/index.js` | js | XACT 音频分发 |
| `tools/xnbcli/app/Xact/AudioEngine.js` | js | 音频引擎 |
| `tools/xnbcli/app/Xact/SoundBank.js` | js | 音库 |
| `tools/xnbcli/app/Xact/WaveBank.js` | js | 波形库（空壳，55 B） |
| `tools/xnbcli/app/Xact/XactClip.js` | js | 音频片段 |
| `tools/xnbcli/app/Xact/XactSound.js` | js | 声音条目 |

### 2.5 `tools/xnbcli-binary/`（36）—— 全部 `第三方-未深入`

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `tools/xnbcli-binary/xnbcli.exe` | exe | **编译好的 xnbcli 可执行** |
| `tools/xnbcli-binary/lz4.node` | node | 原生 LZ4 绑定 |
| `tools/xnbcli-binary/xxhash.node` | node | 原生 xxHash 绑定 |
| `tools/xnbcli-binary/pack.bat` | bat | 打包入口 |
| `tools/xnbcli-binary/unpack.bat` | bat | 解包入口 |
| `tools/xnbcli-binary/packed/PACKED.md` | md | 投放说明 |
| `tools/xnbcli-binary/unpacked/UNPACKED.md` | md | 投放说明 |
| `tools/xnbcli-binary/xnbcli-upstream-git/HEAD` | git | 上游 clone 的 HEAD 指针 |
| `tools/xnbcli-binary/xnbcli-upstream-git/config` | git | 远端地址（固定上游 commit） |
| `tools/xnbcli-binary/xnbcli-upstream-git/description` | git | 仓库描述 |
| `tools/xnbcli-binary/xnbcli-upstream-git/index` | git | 暂存区索引 |
| `tools/xnbcli-binary/xnbcli-upstream-git/packed-refs` | git | 打包引用 |
| `tools/xnbcli-binary/xnbcli-upstream-git/shallow` | git | **shallow clone 标记** |
| `tools/xnbcli-binary/xnbcli-upstream-git/info/exclude` | git | 忽略规则 |
| `tools/xnbcli-binary/xnbcli-upstream-git/logs/HEAD` | git | reflog |
| `tools/xnbcli-binary/xnbcli-upstream-git/logs/refs/heads/master` | git | 分支 reflog |
| `tools/xnbcli-binary/xnbcli-upstream-git/logs/refs/remotes/origin/HEAD` | git | 远端 reflog |
| `tools/xnbcli-binary/xnbcli-upstream-git/refs/heads/master` | git | 分支引用 |
| `tools/xnbcli-binary/xnbcli-upstream-git/refs/remotes/origin/HEAD` | git | 远端引用 |
| `tools/xnbcli-binary/xnbcli-upstream-git/objects/pack/pack-b6c07b17749e803d99e73649f07a11aafb2ce51d.idx` | git | 对象包索引 |
| `tools/xnbcli-binary/xnbcli-upstream-git/objects/pack/pack-b6c07b17749e803d99e73649f07a11aafb2ce51d.pack` | git | 对象包 |
| `tools/xnbcli-binary/xnbcli-upstream-git/objects/pack/pack-b6c07b17749e803d99e73649f07a11aafb2ce51d.rev` | git | 反向索引 |
| `tools/xnbcli-binary/xnbcli-upstream-git/hooks/*.sample`（14 个） | git | 上游 hook 样例（`applypatch-msg`、`commit-msg`、`fsmonitor-watchman`、`post-update`、`pre-applypatch`、`pre-commit`、`pre-merge-commit`、`pre-push`、`pre-rebase`、`pre-receive`、`prepare-commit-msg`、`push-to-checkout`、`sendemail-validate`、`update`） |

> 计数校验：非 git 部分 7（`xnbcli.exe`、`lz4.node`、`xxhash.node`、`pack.bat`、`unpack.bat`、`packed/PACKED.md`、`unpacked/UNPACKED.md`）+ `xnbcli-upstream-git/` 29（1 config + HEAD + description + index + packed-refs + shallow + info/exclude + 3 logs + 2 refs + 3 objects/pack + 14 hooks/*.sample）= **36 ✓**

### 2.6 `tools/ZenithMaterialLab/`（44）

**自研源码与配置（15，全部入库，`已验证`）**

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `tools/ZenithMaterialLab/ZenithMaterialLab.cs` | cs | **Mod 入口**（5 行，`class ZenithMaterialLab : Mod`） |
| `tools/ZenithMaterialLab/MaterialLabSystem.cs` | cs | 工作台系统：UI、滑块、加载/保存/导出、撤销重做、输入（184 行） |
| `tools/ZenithMaterialLab/LabPreview.cs` | cs | 预览渲染：球体/平面/剑迹网格、棋盘、校准树干（64 行） |
| `tools/ZenithMaterialLab/LabResources.cs` | cs | 素材槽加载与刷新（color/mask/normal/surface/height/emission/environment/regions/background）（130 行） |
| `tools/ZenithMaterialLab/LabChecks.cs` | cs | 45 组 GPU A/B 逐项核对（127 行） |
| `tools/ZenithMaterialLab/LabPipelineCheck.cs` | cs | 管线状态检查（87 行） |
| `tools/ZenithMaterialLab/LabArtworkCheck.cs` | cs | 素材哈希与来源核对（64 行） |
| `tools/ZenithMaterialLab/LabRegionCheck.cs` | cs | RGBA 四区载荷校验，重点验证 Alpha=0 处 RGB 不被清空（55 行） |
| `tools/ZenithMaterialLab/GlobalUsings.cs` | cs | 显式 global using（tModLoader 原生编译不读隐式命名空间）（8 行） |
| `tools/ZenithMaterialLab/ZenithMaterialLab.csproj` | csproj | net8.0，`Reference ZenithReborn.dll`，`Import tMLMod.targets` |
| `tools/ZenithMaterialLab/build.txt` | txt | 模组元数据：v0.1.3，`modReferences = ZenithReborn@0.3.80`，`includeSource = true` |
| `tools/ZenithMaterialLab/description.txt` | txt | 模组描述（开发者独立工作台，不装进正式组合） |
| `tools/ZenithMaterialLab/README.md` | md | 完整使用说明：打开方式、操作、素材约定表、导出、区域图工具 |
| `tools/ZenithMaterialLab/Localization/en-US_Mods.ZenithMaterialLab.hjson` | hjson | 本地化 |
| `tools/ZenithMaterialLab/Assets/Diagnostics/RegionMask.zrdata` | zrdata | 四区诊断原始 RGBA8 载荷 |

**构建产物（29，忽略，`第三方-未深入`）**

| 文件路径 | 类型 | 职责 |
|---|---|---|
| `tools/ZenithMaterialLab/bin/Release/net8.0/ZenithMaterialLab.dll` | dll | 编译产物 |
| `tools/ZenithMaterialLab/bin/Release/net8.0/ZenithMaterialLab.pdb` | pdb | 符号 |
| `tools/ZenithMaterialLab/bin/Release/net8.0/ZenithMaterialLab.deps.json` | json | 依赖图 |
| `tools/ZenithMaterialLab/obj/Debug/net8.0/`（7 个：`AssemblyAttributes.cs`、`AssemblyInfo.cs`、`AssemblyInfoInputs.cache`、`GeneratedMSBuildEditorConfig.editorconfig`、`GlobalUsings.g.cs`、`assets.cache`、`csproj.AssemblyReference.cache`） | 混合 | Debug 中间产物 |
| `tools/ZenithMaterialLab/obj/Release/net8.0/`（9 个：`AssemblyAttributes.cs`、`AssemblyInfo.cs`、`AssemblyInfoInputs.cache`、`GeneratedMSBuildEditorConfig.editorconfig`、`GlobalUsings.g.cs`、`assets.cache`、`csproj.AssemblyReference.cache`、`csproj.CoreCompileInputs.cache`、`csproj.FileListAbsolute.txt`、`ZenithMaterialLab.dll`、`ZenithMaterialLab.pdb`、`sourcelink.json`、`ref/ZenithMaterialLab.dll`、`refint/ZenithMaterialLab.dll`） | 混合 | Release 中间产物 |
| `tools/ZenithMaterialLab/obj/`（3 个：`ZenithMaterialLab.csproj.nuget.dgspec.json`、`csproj.nuget.g.props`、`csproj.nuget.g.targets`） | json/xml | NuGet 还原 |
| `tools/ZenithMaterialLab/obj/project.assets.json`、`project.nuget.cache` | json | 依赖资产 |

> 计数校验：15 + 3（bin）+ 26（obj）= 44 ✓
> 总计：6 + 8 + 68 + 60 + 36 + 44 = **222 ✓（100% 覆盖）**

---

## 第三部分：VfxLibrary 结构与索引方式

### 3.1 定位

`VfxLibrary/` = **制作端素材库 + 可搜索图鉴**，`library.json:2-3` 自报 `"scope": "authoring-only"`、`"runtimeIntegrated": false`。`VfxLibrary/README.md:3` 说"它是素材参考库，不直接进入游戏生产包"，`checks/package_install.py:70` 在打包时排除整个目录。

**重要区分**：库里有两条独立血脉 ——
1. **旧设计体系**（`Masks/` + `ColorFields/` + `Examples/` + `library.json` + `INDEX.md`）：按"特效种类 → 剑名 → 动态蒙版"组织，`entry.json` 是最小单位。
2. **原版素材库**（`Vanilla/`）：从本机泰拉瑞亚安装目录的 XNB 解出的原版美术，11185 个文件占了全部文件的 95.1%，**纯参考，零运行时引用**。

### 3.2 目录结构与文件数（清单实测）

一级分类 16 个（含 3 个根文件）：

| 路径 | 文件数 | 占比 | 说明 |
|---|---:|---:|---|
| `VfxLibrary/Vanilla/` | **11185** | 95.1% | 原版素材 + 图鉴 |
| `VfxLibrary/Masks/` | 413 | 3.5% | 动态蒙版（Alpha 形状） |
| `VfxLibrary/ColorFields/` | 74 | 0.6% | 底色配方（慢流动色场） |
| `VfxLibrary/Materials/` | 58 | 0.5% | 表面材质预设 + 贴图 + R3 快照 |
| `VfxLibrary/References/` | 8 | — | 外部拆解与来源记录 |
| `VfxLibrary/LayerMasks/` | 6 | — | RGBA 四区图层掩码 |
| `VfxLibrary/SurfaceMaps/` | 2 | — | 金属度/粗糙度/AO |
| `VfxLibrary/Shaders/` | 2 | — | 剑流着色器登记 |
| `VfxLibrary/Brushes/` | 1 | — | 笔刷贴图 |
| `VfxLibrary/HeightMaps/` | 1 | — | 高度图 |
| `VfxLibrary/NoiseMaps/` | 1 | — | 噪声图 |
| `VfxLibrary/NormalMaps/` | 1 | — | 法线图 |
| `VfxLibrary/Examples/` | 1 | — | M1 技术演示绑定 |
| `VfxLibrary/README.md` / `INDEX.md` / `library.json` | 3 | — | 根文件 |
| **合计** | **11756** | 100% | |

二级分类（关键子目录）：

| 路径 | 文件数 | 内容 |
|---|---:|---|
| `Vanilla/Images/` | 6922 | 原尺寸透明 PNG（3461 张）+ 同名 XNB 描述 JSON（3461 个） |
| `Vanilla/Thumbnails/` | 3935 | 128px 预览图（`Images/` 的去重覆盖 + `DustByID/` 的缩略） |
| `Vanilla/DustByID/` | 324 | 按 Dust ID 切出的三帧 8×8 并排小图 |
| `Masks/SwordBody/` | 363 | 22 把剑 × 多轮修订（R1–R5 / H1–H4） |
| `Materials/SwordTrail/` | 48 | 玻璃预设 + R3 归档 + 0.3.80 快照 |
| `Masks/Particles/` | 25 | 22 剑 + `_Shared` |
| `Masks/Rings/` | 23 | 22 剑 + `_Shared` |
| `ColorFields/{SwordBody,Rings,Particles}/` | 各 23 | 22 剑 + `_Shared` |
| `ColorFields/_Shared/` | 4 | 共享底色（SlowFlowM1、SlowFlowRadiantR2/R3 等） |
| `Materials/Surface/` | 5 | 高级表面材质合同 |
| `References/Retired/` | 3 | 已撤回的 CalamityOverhaul 素材（LICENSE + provenance + README） |
| `LayerMasks/SwordTrail/` | 6 | TerraBlade/EnergyBladeA1 + RegionContract |

`Vanilla/Images/` 的 8 个内容分类（`manifest.json` + 实测）：

| 分类 | PNG 数 | 分类 | PNG 数 |
|---|---:|---|---:|
| 碎片 | 1404 | 辉光 | 384 |
| 弹幕 | 1136 | 额外特效 | 316 |
| 其他 | 169 | 粒子 | 324 |
| 物品火焰 | 32 | 火焰 | 19 |
| 粒子图集 | 1（`Dust.png`，324 个 Dust 的母图集） | | |

扩展名分布（11756）：`png` 7929、`json` 3792、`md` 18、`py` 3、`fxc` 3、`fx` 3、`cs` 3、`zrdata` 2、`txt` 1、`js` 1、`html` 1。

### 3.3 图鉴 JSON 的作用与格式

库里有 **4 套不同层级的 JSON**，各有分工：

**(a) `VfxLibrary/library.json`（5.1 KB）—— 顶层分类与剑名登记**

只登记"有什么"，不含素材数据。字段：`schemaVersion`、`scope: "authoring-only"`、`runtimeIntegrated: false`、`categories`（3 类：`SwordBody`/`Rings`/`Particles`，每类带 `displayName`、`components`、`oneColorField`）、`swords`（23 条：22 把矿石剑 + 天铸巨剑，每条 `id` + `displayName` + `source{kind,key}`）。

样本（`library.json`）：
```json
"categories": { "SwordBody": { "displayName": "剑身一体组",
  "components": ["trail","bladeFlow","edgeMotion","tipAirbreak"], "oneColorField": true } }
"swords": [{ "id": "TitaniumSword", "displayName": "钛金剑",
  "source": { "kind": "terraria-item", "key": "Terraria/ItemID.TitaniumSword" } }]
```

**(b) `entry.json`（156 个）—— 每个素材条目的最小登记单位**

样本一：`VfxLibrary/Masks/SwordBody/TitaniumSword/entry.json`（蒙版类）
```json
{ "id": "mask.SwordBody.TitaniumSword", "kind": "mask", "category": "SwordBody",
  "sword": "TitaniumSword", "status": "user-preferred-baseline",
  "components": ["trail","bladeFlow","edgeMotion","tipAirbreak"], "channel": "alpha",
  "colorField": "color.SwordBody.TitaniumSword",
  "canvas": [448, 256], "pivot": [305, 128],
  "clips": { "appear": {"metadata":"R2/clip.json","atlas":"R2/active.png","phase":"appear"},
             "active": {...}, "finish": {...} },
  "resources": [{ "repoPath": "VfxLibrary/Masks/SwordBody/TitaniumSword/R2/active.png",
                  "provenance": "VfxLibrary/Masks/SwordBody/TitaniumSword/R2/authoring.json" }],
  "revision": "R2", "runtimeIntegrated": false, "finalArtApproved": false,
  "previousRevisions": [{"revision":"R1","status":"art-rejected"}, ...],
  "userPreferred": true,
  "currentRuntime": { "version": "0.3.60", "enabled": false,
    "reason": "旧材质外观已撤下，等待自有材质接入；历史配方和导出保留。" } }
```

样本二：`VfxLibrary/ColorFields/_Shared/SlowFlowM1/entry.json`（底色类）
```json
{ "id": "color._Shared.SlowFlowM1", "kind": "color", "category": "_Shared",
  "status": "example",
  "recipe": { "clear": [15,18,47],
    "layers": [ {"tint":[65,205,235],"opacity":0.75,"scrollPixelsPerSecond":[8,0]},
                {"tint":[132,60,205],"opacity":0.28,"scrollPixelsPerSecond":[-5,0],
                 "initialYOffset":"textureHeight/3"} ] },
  "historicalResources": [{ "repoPath": "ZenithReborn/Assets/ThirdParty/CalamityOverhaul/NoiseSoft01.rawimg", ... }] }
```

关键字段语义（来自 `VfxLibrary/README.md:36-40` 与 `checks/vfx_library.py:22-40`）：
- `kind` ∈ {`mask`,`color`}，`vfx_library.py:26` 强校验。
- `category` 必须在 `library.json.categories` 里，或为 `_Shared`。
- **路径必须自洽**：`vfx_library.py:29-30` 断言 `Masks/<category>/<sword>` 或 `ColorFields/<category>/<sword>` 与 `entry.json` 内容一致。
- `status` 枚举：`awaiting-art`（只建位置无蒙版）、`unassigned`（底色未定）、`example`（技术示例）、`source-reference`（现有素材来源）、`review-candidate`（已有可审素材未获美术认可）、`user-preferred-baseline`（用户已明确偏好）、`withdrawn`/`withdrawn-study`（已撤回）。
- **`runtimeIntegrated: false` 是硬事实**，钛金剑条目里还有 `currentRuntime.enabled: false`。

**(c) `clip.json`（67 个）/ `authoring.json`（90 个）—— 蒙版图集与制作溯源**

`clip.json` 是图集元数据（帧数/列数/`tip`/`pivot`/`atlasSha256`），被 `checks/emit_vfx_mask_data.py:20` 读取并**校验 SHA256** 后才允许复制进模组。`authoring.json` 记录生成该图集的配方与实测参数。

**(d) `Vanilla/catalog.js` + `Vanilla/manifest.json` —— 原版素材浏览器数据**

`catalog.js` 是 617 KB 的单行 `window.VANILLA_VFX=[...]` JSONP 数组，供 `浏览原版特效.html` 离线打开。每条记录的字段（`tools/build_vanilla_vfx_catalog.py:20-21`）：

```
c = 分类（弹幕/碎片/辉光/...）        n = 标识（贴图相对路径去后缀）
i = 原版数字 ID（Dust/Gore/Projectile/Extra）   a = 英文别名（由 ID 常量反查）
t = 中文主题标签（星辰/神圣/剑与斩击/光束/火焰/冰雪/爆炸/魔法）
w,h = 原始尺寸                       p = 原图相对路径
s = 缩略图相对路径                    o = 原始来源（如 "Dust.xnb"）
```

样本：`{"c":"其他","n":"Bone_Laser","i":null,"a":"","t":"光束","w":40,"h":62,"p":"Images/其他/Bone_Laser.png","s":"Thumbnails/其他/Bone_Laser.png","o":""}`

`manifest.json` 只有统计与免责声明：`raw_png_count: 3461`、`dust_slices: 324`、`counts`（按分类）、`notes`（4 条："贴图为原始素材，不等于实际动画或完整战斗效果"、"粒子按原版8×8帧从Dust图集裁剪"、"英文标识来自本机 tModLoader ID 定义"、"按照文件类型归档，分类并不保证每张图都可作为特效"）。

**`catalog.js` 实测统计（用 Python 解析）：** 总 3785 条 = `Images/` 的 3461 张原图 + `DustByID/` 的 324 个切片；其中 **3738 条有数字 ID（98.8%）、2344 条有英文别名、585 条有中文主题标签**。分类计数与 `manifest.json` 完全一致。

**已知脏数据（已核实）：`Thumbnails/其他/` 有 150 张孤儿缩略图。** `Thumbnails/其他/` 319 张 vs `Images/其他/` 169 张，差额 150。用 `comm` 比对后确认这 150 个缩略图（`Background`、`Beam`、`Bolt`、`Explosion`、`Flash`、`GolfBallOutline`、`Head`、`Meteor`、`Perlin`、`Planet`、`PointBlurry`、`PointSharp`、`Ripples`、`Rock_0..2`、`Shoulder`、`Sky_Slime_1..3` 等）在 `Images/其他/` 里**没有对应原图**，也不在 `catalog.js` 的 3785 条里 —— 是上一轮提取后原图被清掉、缩略图残留的死文件。其余目录的缩略图数量与原图**逐一相等**（弹幕 1136、火焰 19、物品火焰 32、碎片 1404、辉光 384、额外特效 316、粒子图集 1），且 `Thumbnails/粒子/` 324 张对应 `DustByID/` 324 个切片。计数校验：3461 + 324 + 150 = **3935 ✓**

### 3.4 检索方式（3 条路径）

1. **结构化检索（推荐）**：`checks/vfx_library.py`，纯读 JSON 不加载模组。
   ```powershell
   python checks/vfx_library.py search 钛金
   python checks/vfx_library.py search CopperBroadsword --kind mask --category SwordBody
   python checks/vfx_library.py search SlowFlowM1 --kind color
   python checks/vfx_library.py validate      # 校验 id 唯一 / 路径自洽 / resources 存在
   ```
   （用法见 `VfxLibrary/README.md:46-51`；实现见 `checks/vfx_library.py:14-40`）

2. **纯文本/grep**：`entry.json` 是扁平 JSON，`grep -rn '"status": "user-preferred-baseline"' VfxLibrary/` 之类可以直接按状态、按剑名、按 `kind` 筛。

3. **图谱 md**：`VfxLibrary/INDEX.md` 用一张 23 行 × 5 列的表格把"剑名 → 剑身一体蒙版 / 圆环蒙版 / 粒子蒙版 / 底色入口"全量列成超链接；下方"共享来源"区列 `_Shared` 条目。`Masks/.../R2/README.md` 是单把剑的视觉基准说明，`References/OniCrimsonSlash-拆解.md` 是外部 shader 的逐层拆解。`Vanilla/浏览原版特效.html` 是浏览器可视化检索（按分类/关键词/按 ID）。

### 3.5 清单文件与"不逐个语义分析"的取舍

**生成方式（shell，不手抄）：**
```bash
find VfxLibrary -type f | sort > artifacts/codebase-map/vfx-library-manifest.txt
```

**结果：`artifacts/codebase-map/vfx-library-manifest.txt`，11756 行，与磁盘文件数一一对应（差 0 行）。** 每行一个仓库相对路径，字典序，可直接 `grep`/`awk`/`comm` 差分。

分类统计（对清单本身做 awk 聚合，与 3.2 表一致）：
```bash
awk -F/ '{print $1"/"$2}' artifacts/codebase-map/vfx-library-manifest.txt | sort | uniq -c | sort -rn
# 11185 VfxLibrary/Vanilla      413 VfxLibrary/Masks      74 VfxLibrary/ColorFields
#    58 VfxLibrary/Materials      8 VfxLibrary/References    6 VfxLibrary/LayerMasks
#     2 VfxLibrary/SurfaceMaps    2 VfxLibrary/Shaders       1 VfxLibrary/Brushes
#     1 VfxLibrary/HeightMaps     1 VfxLibrary/NoiseMaps     1 VfxLibrary/NormalMaps
#     1 VfxLibrary/Examples       1 VfxLibrary/library.json  1 VfxLibrary/README.md  1 VfxLibrary/INDEX.md
```

**取舍说明（为什么不做 11756 条逐文件语义分析）：**

1. **95.1% 是原版素材的机械复制**。`Vanilla/Images/` 6922 个文件里 3461 是 PNG、3461 是**同名的 XNB 描述 JSON**（`Texture2DReader` 导出的元数据），后者内容高度同构，逐条读没有信息增量。`Thumbnails/` 3935 个文件是 128px 降采样，**零独立语义**。
2. **原版素材的语义已经在两处被结构化描述**：`catalog.js` 给每张图记了分类/ID/别名/主题标签，`manifest.json` 给了分类计数与边界声明。再逐图写一遍就是重复劳动。
3. **真正需要语义分析的是 `Masks/` + `ColorFields/` 的 156 个 `entry.json`**（已抽样验证两种 `kind` 的完整字段），以及 `Materials/` 的材质合同 —— 这部分规模可控且已有 `checks/vfx_library.py validate` 机检。
4. **可追踪性由清单保证**：任何"这个文件到底存不存在/在哪"的问题，`grep <名> vfx-library-manifest.txt` 立即回答；任何"这个分类有多少文件"的问题，上面的 awk 一行回答。**清单 + 分组统计 + 抽样字段说明** 三件套，比逐文件一句话描述更不容易出错，也不会因为 11756 条噪声把真正重要的 156 条淹没。

**代价（明确记录）**：本报告**不能**回答"某张具体原版贴图长什么样/适合什么效果"。需要时按清单定位后直接看图，或打开 `Vanilla/浏览原版特效.html`。

---

## 第四部分：素材使用关系

### 4.1 核心结论：主模组代码**几乎不直接引用 VfxLibrary**

```
$ grep -rn "VfxLibrary" ZenithReborn --include=*.cs --include=*.json --include=*.csproj | wc -l
2
```

命中的 2 行**都在注释里**，且都在同一个文件：

- `ZenithReborn/VfxMaskData.cs:6` —— `// 本文件由 checks/emit_vfx_mask_data.py 依据 VfxLibrary 登记自动生成。`
- `ZenithReborn/VfxMaskData.cs:8` —— `// 不要手工改这个文件，改配方请改 VfxLibrary 后重新生成。`

**没有任何运行时 C# 代码读取 `VfxLibrary/` 下的路径。** 素材进入游戏的路径是**构建期复制**，不是运行时加载。

### 4.2 实际生效的 4 条链路（构建期）

**链路 1：矿石剑剑身蒙版 → 主模组贴图（最主要的素材消费路径）**

- 消费者：`checks/emit_vfx_mask_data.py:14` —— `glob.glob('VfxLibrary/Masks/SwordBody/*/H4/clip.json')`，`assert` 必须恰好 21 把。
- 动作：`checks/emit_vfx_mask_data.py:20` 校验 `atlasSha256`，`:23` `shutil.copyfile(src, 'ZenithReborn/Assets/Vfx/SwordBody/<Sword>.png')`；`:28` 从 `VfxLibrary/ColorFields/SwordBody/<name>/entry.json` 的 `variants.H4.recipe` 提取 `clear`/两层 `tint`/`opacity`/`scroll`。
- 产物：`ZenithReborn/VfxMaskData.cs`（39 行，21 条 `Row` 记录，含 `ItemId`/`TipPx`/`PivotX`/`Clear`/`Primary`/`Secondary`/两层 opacity 与 scroll/`LoopSeconds`）+ `ZenithReborn/Assets/Vfx/SwordBody/*.png`（21 张，实测目录内 21 个 PNG，钛金剑被 `:17` 显式跳过走用户基准）。
- 运行时消费：`ZenithReborn/VfxSwordMask.cs:32` 拼 `"ZenithReborn/Assets/Vfx/SwordBody/"+ItemID.Search.GetName(row.ItemId)`，`:33` `ModContent.HasAsset` 判存，`:94` `ModContent.Request<Texture2D>(atlasPaths[itemType], AssetRequestMode.ImmediateLoad)`，`:78`/`:129` 绑到 `device.Textures[0..2]`（色场 + 蒙版 + 噪声三槽）。
- **用于什么效果**：22 把矿石剑的"剑身一体组"拖尾 —— 拖尾/剑身流动/边缘动效/剑头破空共用同一套 Alpha 蒙版和图集坐标（`VfxLibrary/README.md:22`）。

**链路 2：玻璃材质预设 → 编译期常量**

- 消费者：`checks/emit_glass_preset.py:7` —— `source = root/'VfxLibrary/Materials/SwordTrail/Glass/preset.json'`。
- 动作：`:9-10` 读 `parameters`，`:11` 用 `bounds` 逐项卡范围，`:12` `assert data['scope']=='developer-authoring-only'`，`:14` 生成常量类 `GlassMaterialPreset`。
- 产物注释自陈："由 checks/emit_glass_preset.py 从制作端预设生成，不提供玩家调参入口"（`checks/emit_glass_preset.py:14`）。
- **用于什么效果**：剑迹玻璃外观（IOR 1.5、Roughness 0.08、Transmission 0.55、Thickness 72、Dispersion 0.35、SpaceWarp…共 12 个参数），驱动 `ZRGlassScreen.fx`。
- 注意：`VfxLibrary/Shaders/SwordFlow/entry.json` 的 `"preset"` 字段直接指回同一个 `preset.json`，`"lifecycleEnding"` 还绑了 `SoulTrailEnding.cs` 的帧数表。

**链路 3：着色器绑定表 → 主模组内副本**

- 运行时读取：`ZenithReborn/SwordFlowMaterial.cs:58` `Mod.GetFileBytes("Assets/Vfx/Materials/SwordFlow/bindings.json")`，`:91` `TryLoadRaw("Assets/Vfx/Materials/SwordFlow/ZoneMask.zrdata")`。
- 这两个文件是 `VfxLibrary/Materials/` 同类内容的**模组内副本**（`ZenithReborn/Assets/Vfx/Materials/SwordFlow/` 下有 `bindings.json`、`ColorField.png`、`NormalMap.png`、`SurfaceMap.png`、`HeightMap.png`、`EmissionMap.png`、`EnvironmentMap.png`、`NoiseSeamless.png`、`BrushDefault.png`、`ZenithField.png`、`ZenithWear.png`、`ZoneMask.png`、`ZoneMask.zrdata`）。`VfxLibrary/Shaders/SwordFlow/entry.json` 的 `"bindings"` 字段指向的就是这个**模组内路径**，不是 VfxLibrary 路径 —— 说明登记表把"制作源"和"运行时副本"分开记录。
- **用于什么效果**：逐剑统一表面材质（`ZRSwordFlow.fx`），默认无第三方贴图输入，程序色场 + 格点噪声。

**链路 4：`Terraria/Images/*` —— 真正在代码里硬编码的贴图**

```
$ grep -rn "Terraria/Images" ZenithReborn --include=*.cs | wc -l
24
```

这 24 处才是"运行时直接引用的贴图"，全部指向**原版游戏资源**，与 VfxLibrary 无关：

| 位置 | 引用 | 用途 |
|---|---|---|
| `ZenithReborn/OreFlyingSword.cs:61` | `Terraria/Images/Item_` + `ItemID.Zenith` | 飞剑本体用天顶剑贴图 |
| `ZenithReborn/SlashZenithTest.cs:13` | `Terraria/Images/Item_` + `ItemID.Zenith` | 测试斩击【已删除/历史快照：已随 2026-11-04 清理删除；本表为 2026-11-03 调查快照，原文保留】 |
| `ZenithReborn/SoulBlade.cs:121`、`SoulItem.cs:90` | `Terraria/Images/Item_` + `ItemID.Zenith` | 灵魂剑/物品图标 |
| `ZenithReborn/FlyingBladeShot.cs:16` | `Terraria/Images/Item_` + `ItemID.FlyingKnife` | 飞刀弹幕 |
| `ZenithReborn/OreSkyfall.cs:39` | `Terraria/Images/Item_` + `ItemID.TitaniumSword` | 天铸巨剑代表武器贴图 |
| `ZenithReborn/EnvironmentEffects.cs:16`、`:128` | `Terraria/Images/Extra_98` | 环境特效 |
| `ZenithReborn/EvapCircle.cs:19` | `Terraria/Images/Dust`（图集） | 蒸发圆环 |
| `ZenithReborn/HardOreShot.cs:20` | `Terraria/Images/Projectile_` + `CrystalShard` | 矿石弹 |
| `ZenithReborn/HolyHeavenStar.cs:11`、`:39` | `Terraria/Images/Projectile_` + `FallingStar` | 圣天星 |
| `ZenithReborn/SoulBlessings.cs:186` | `Terraria/Images/Projectile_` + `SwordBeam` | 剑气 |
| `ZenithReborn/SoulFlowParticles.cs:77`、`SwordSpark.cs:10` | `VfxIsolation.PureCodeOnly ? "Terraria/Images/MagicPixel" : "ZenithReborn/SwordSpark"` | **隔离模式降级**：纯代码模式下退回原版 1×1 白点当粒子 |
| 其余（`AttachmentStory.cs:108/182`、`CrossSummon.cs:45/51`、`LivingArmament.cs:14`、`Progression.cs:165`、`WoodCombat.cs:290`、`WoodPlayer.cs:126`） | `Item_`/`NPC_`/`Buff_` + ID 常量 | 用原版物品/NPC/增益图标当占位贴图 |

### 4.3 三条明确结论

1. **VfxLibrary 不是运行时素材源**。`grep -rn VfxLibrary ZenithReborn` 只命中 2 行注释；`library.json` 自报 `runtimeIntegrated: false`；`checks/package_install.py:70` 把整个目录从成品包排除。素材通过**构建期 copy + 代码生成**（`emit_vfx_mask_data.py` / `emit_glass_preset.py`）进入 `ZenithReborn/Assets/`，运行时只认识 `ModContent.Request<Texture2D>("ZenithReborn/Assets/...")`。
2. **运行时代码里的贴图引用全部是 `Terraria/Images/*`（24 处）或模组自有路径（`ZenithReborn/Assets/Vfx/**`）**。其中 `SwordSpark.cs:10` 与 `SoulFlowParticles.cs:77` 还有 `VfxIsolation.PureCodeOnly` 分支，隔离验证时降级到原版 `MagicPixel` —— 说明"零外部贴图"是被当成可切换能力设计的，不只是口号。
3. **`VfxLibrary/Vanilla/` 11185 个文件与主模组零代码关联**。它是纯浏览器参考库（`浏览原版特效.html` + `catalog.js`），唯一被代码消费的原版数据是 `tools/decomp/Terraria.ID.ProjectileID.decompiled.cs`（被 `build_vanilla_vfx_catalog.py:11` 读取以反查英文名），而不是素材本身。

### 4.4 反向流向

`checks/import_material_export.py` 是**唯一反向写入**通道：材质实验室导出 → `python checks/import_material_export.py <导出目录> --weapon TerraBlade`（dry-run）→ `--apply` 才写，写之前把目标文件备份到 `artifacts/material-import-backups`，并按底色/蒙版/法线/表面/高度/自发光/环境图**各大类登记到 VfxLibrary**，同时给该把剑设置 `SurfaceMaterial` 引用，"不改其他剑绑定"（`tools/ZenithMaterialLab/README.md:41`）。

---

## 交付摘要

1. **tools 覆盖数：222/222（100%）**。根 6 + `ZenithMaterialLab` 44 + `decomp` 8 + `ilspy` 68 + `xnbcli` 60 + `xnbcli-binary` 36。
2. **VfxLibrary：11756 个文件，一级目录 13 个 + 根文件 3 个（`README.md`/`INDEX.md`/`library.json`）**，二级关键分组见 3.2 节两张表。清单 `artifacts/codebase-map/vfx-library-manifest.txt` = **11756 行**，与磁盘一一对应（差 0）。95.1%（11185）集中在 `Vanilla/`，其中 `Images/` 6922（3461 PNG + 3461 同名 JSON）、`Thumbnails/` 3935、`DustByID/` 324。索引单位：`entry.json` **156 个**、`clip.json` 67 个、`authoring.json` 90 个、`library.json` 1 个、`Vanilla/catalog.js` 1 个（3785 条记录）。
3. **自研 vs 第三方**：
   - **自研**：`tools/README.md`、`build_vanilla_vfx_catalog.py`、`desktop_control.ps1`、2 个 `.cmd`、`tools/ZenithMaterialLab/` 全部 15 个源码/配置（这是一个**独立 tModLoader 模组**，`modReferences = ZenithReborn@0.3.80`，单向依赖主模组）。
   - **第三方**：`tools/ilspy`（ILSpy **9.1.0.7988**，ilspycmd，`ilspycmd.nuspec` 实证）、`tools/decomp`（ILSpy 产物）、`tools/xnbcli`（xnbcli **1.0.7**，LGPL v3.0，LeonBlade 上游，Node.js 源码 60 文件）、`tools/xnbcli-binary`（1.0.7 Windows 分发 + 上游 shallow `.git` 29 文件）、`xnbcli-windows-x64.zip`。
   - **入库策略**：`git ls-files tools` = **80 / 222**。忽略 ilspy(68) + decomp(8) + xnbcli-binary(36) + bin(3) + obj(26) + zip(1) = 142，理由均为"体积大 + 可重新生成/可重新下载"（`.gitignore:24-29`）；`tools/xnbcli/` 源码保留入库。
4. **素材使用关系 3 条结论**：
   - 主模组对 VfxLibrary 的引用**只有 2 行注释**（`VfxMaskData.cs:6,8`），实际流通靠构建期 `checks/emit_vfx_mask_data.py:14-23`（蒙版 SHA256 校验后 copy 到 `ZenithReborn/Assets/Vfx/SwordBody/`）与 `checks/emit_glass_preset.py:7-14`（预设编成常量类）。
   - 运行时的贴图引用是 `Terraria/Images/*`（**24 处**，`OreFlyingSword.cs:61`、`OreSkyfall.cs:39`、`EnvironmentEffects.cs:16/128`、`EvapCircle.cs:19` 等）与模组自有 `ZenithReborn/Assets/Vfx/**`；`SwordSpark.cs:10`、`SoulFlowParticles.cs:77` 还有 `VfxIsolation.PureCodeOnly → MagicPixel` 降级分支。
   - `VfxLibrary/Vanilla/`（11185 文件）与代码**零关联**，纯浏览器参考；唯一被脚本消费的原版数据是 `decomp/Terraria.ID.ProjectileID.decompiled.cs`（喂给 `build_vanilla_vfx_catalog.py:11` 反查英文名）。
5. **未确认项**：
   - `tools/decomp/Terraria.Graphics.Light.LightingMode.decompiled.cs` 与 `tools/decomp/Terraria.LightMode.decompiled.cs` **均为 0 字节**，无法判断是反编译失败、被清空还是占位。
   - `tools/xnbcli-binary/xnbcli-upstream-git/` 的 `.git` 目录未进一步核对上游 commit 哈希（`packed-refs` 未展开），只能确认是 shallow clone。
   - **已核对消除的疑点**：`build_vanilla_vfx_catalog.py:11` 依赖的 `artifacts/vanilla-{Dust,Gore,Extras}ID.reference.cs` **三个文件确实存在**（虽被 `.gitignore:8` 忽略，但磁盘上有）。`catalog.js` 实测 3785 条，其中 3738 条有数字 `i`（98.8%）、2344 条有英文别名、585 条有中文主题标签 —— ID 映射覆盖率很高，剩余 47 条无 ID 属正常（部分资源无常量映射）。
   - 见下方"孤儿缩略图"问题（已核实，属已知脏数据）。
