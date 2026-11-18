param([switch]$EchoOre,[switch]$InformationUi,[switch]$Glass)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
# tModLoader 安装路径可按机器覆盖，优先级：环境变量 ZENITH_TML_ROOT > 仓库根 local.props 的 TmlRoot > 默认值。
# local.props 是本机配置文件（不入库），这样第二个开发者换机器时不用改被 git 跟踪的脚本。
$loaderRoot = $env:ZENITH_TML_ROOT
if (-not $loaderRoot) {
    $localProps = Join-Path $taskRoot 'local.props'
    if (Test-Path -LiteralPath $localProps) {
        try {
            [xml]$localPropsXml = Get-Content -LiteralPath $localProps -Raw
        } catch {
            throw "读取 local.props 失败：$localProps。请确认它是格式正确的 XML（需要 <Project><PropertyGroup><TmlRoot>...</TmlRoot></PropertyGroup></Project>）。$($_.Exception.Message)"
        }
        # PropertyGroup 与 TmlRoot 都可能是数组：跳过空项后取**最后**一个，与 MSBuild「同名属性后者覆盖前者」的语义保持一致。
        # 取值必须 Trim：XML 里换行缩进会带进空白，直接当路径用会报"名为 '\n  D' 的驱动器不存在"。
        $tmlRootValue = @($localPropsXml.Project.PropertyGroup) | ForEach-Object { $_.TmlRoot } | Where-Object { $_ } | Select-Object -Last 1
        if ($tmlRootValue) {
            $loaderRoot = ([string]$tmlRootValue).Trim()
        } else {
            Write-Warning "local.props 未设置 TmlRoot，回退默认路径：$localProps"
        }
    }
}
if (-not $loaderRoot) { $loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader' }
$dotnet = Join-Path $loaderRoot 'dotnet/dotnet.exe'
$testProfile = Join-Path $taskRoot 'wood-check-profile'
$nativePackages=@{}
New-Item -ItemType Directory -Force -Path "$testProfile/Mods", "$taskRoot/artifacts" | Out-Null
# 只编译项目自有着色器；微软编译器只在开发时使用，不新增玩家依赖。
$python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
if(-not(Test-Path $python)){$python=(Get-Command python -ErrorAction Stop).Source}
# 地图新鲜度：只提示不阻断（提交时由 pre-commit 钩子强制），避免改代码期间反复卡回归。
& $python "$taskRoot/checks/map_freshness.py" *> "$taskRoot/artifacts/map-freshness.log"
if($LASTEXITCODE -ne 0){Write-Warning '代码地图落后于源码：细节见 artifacts/map-freshness.log，提交前必须修（pre-commit 会拦）'}
# 生成物不漂移的基线：必须在下面所有生成器运行之前拍快照。
# 用文件哈希而不是 git status：本机 git 不一定在 PATH，且这条断言判的是
# 「回归自己有没有改写源码树」，与用户手上尚未提交的改动无关。
function Get-ZenithSourceSnapshot {
    $snapshot = @{}
    foreach ($f in Get-ChildItem -LiteralPath "$taskRoot/ZenithReborn" -Recurse -File) {
        $rel = $f.FullName.Substring($taskRoot.Length + 1)
        if ($rel -match '(^|[\\/])(bin|obj)([\\/]|$)') { continue }
        $snapshot[$rel] = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
    }
    return $snapshot
}
$sourceSnapshotBefore = Get-ZenithSourceSnapshot
# 资产完整性先跑：服务端夹具不加载图形资产，新增 Mod 类型缺同名 png 或缺 Texture override 只会在客户端崩溃。
& $python "$taskRoot/checks/check_mod_assets.py"
if($LASTEXITCODE -ne 0){throw '资产完整性检查失败：新增 Mod 类型必须有同名 png 或显式 Texture override'}
# 本地化键集合：中文侧漏键或残留英文占位不会让编译失败，只有进游戏才看得见。
& $python "$taskRoot/checks/check_localization_keys.py"
if($LASTEXITCODE -ne 0){throw '本地化键集合不一致或中文侧仍有英文占位：见上方明细'}
& $python "$taskRoot/checks/emit_glass_preset.py" *> "$taskRoot/artifacts/glass-preset-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/artifacts/glass-preset-build.log";throw '制作端玻璃预设生成失败'}
& $python "$taskRoot/checks/compile_effects.py" *> "$taskRoot/artifacts/own-effect-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/artifacts/own-effect-build.log";throw '自有着色器编译失败'}
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    $source = if ($name -eq 'ZenithReborn') { "$taskRoot/ZenithReborn" } else { "$taskRoot/checks/ZenithPrototypeCheck" }
    Push-Location $loaderRoot
    try {
        # 直接使用游戏自带编译器，避开本机已损坏的独立NuGet还原；不复用旧DLL。
        & $dotnet ./tModLoader.dll -server -build $source -tmlsavedirectory "$taskRoot/build-profile" *> "$taskRoot/artifacts/$name-wood-pack.log"
        if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-wood-pack.log"; throw "Packaging failed: $name" }
    } finally { Pop-Location }
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$testProfile/Mods/$name.tmod"
    $nativePackages[$name]=@{sha256=(Get-FileHash "$testProfile/Mods/$name.tmod" -Algorithm SHA256).Hash.ToLowerInvariant()}
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$testProfile/Mods/enabled.json"
$env:ZENITH_WOOD = '1'
$env:ZENITH_ECHO_ORE = if($EchoOre){'1'}else{'0'}
$env:ZENITH_INFORMATION_UI = if($InformationUi){'1'}else{'0'}
$env:ZENITH_GLASS = if($Glass){'1'}else{'0'}
$env:ZENITH_CHECK_REPORT = "$taskRoot/artifacts/wood-check.txt"
Push-Location $loaderRoot
try {
    & $dotnet ./tModLoader.dll -server -nosteam -tmlsavedirectory $testProfile *> "$taskRoot/artifacts/wood-load.log"
    $result = $LASTEXITCODE
} finally { Pop-Location }
# 全量标记：-Glass/-InformationUi/-EchoOre 小套件走的是 Check.cs 里排在前面的分支，
# 输出同样是 "PASS: N focused checks"，package_install.py 只看前缀分不出来。
# 所以只在真正没带小套件开关时，往报告里补人读标注和机器标记；两处都必须配套修改。
$reportPath = "$taskRoot/artifacts/wood-check.txt"
$fullSuite = -not ($EchoOre -or $InformationUi -or $Glass)
if ($result -eq 0 -and $fullSuite) {
    if (-not (Test-Path -LiteralPath $reportPath)) { throw '全量回归已成功但未产出报告 artifacts/wood-check.txt' }
    $reportText = (Get-Content -LiteralPath $reportPath -Raw).TrimEnd()
    if ($reportText -notmatch '^PASS: \d+ focused checks') {
        throw "全量回归报告首行格式异常，拒绝写全量标记：$($reportText.Substring(0, [Math]::Min(80, $reportText.Length)))"
    }
    $reportText = $reportText -replace '^(PASS: \d+ focused checks)', '$1 [suite=full ZENITH_WOOD]'
    Set-Content -LiteralPath $reportPath -Value ($reportText + "`nSUITE=ZENITH_WOOD`n") -Encoding utf8 -NoNewline
}
# 打印最终报告：全量跑法这里能看到 [suite=full ZENITH_WOOD] 与 SUITE=ZENITH_WOOD 两处标记。
if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath } else { Write-Output "本轮未产出报告：$reportPath" }
# 生成物不漂移断言：把回归前后的源码树哈希逐文件对照。
# 生成器写出与仓库不一致的字节（历史上是 CRLF / 着色器哈希口径漂移）会让工作树变脏，
# 过去只能靠人跑 git status 发现，这里改成硬失败。
$sourceSnapshotAfter = Get-ZenithSourceSnapshot
$drifted = @()
foreach ($key in (@($sourceSnapshotBefore.Keys) + @($sourceSnapshotAfter.Keys) | Sort-Object -Unique)) {
    if ($sourceSnapshotBefore[$key] -ne $sourceSnapshotAfter[$key]) { $drifted += $key }
}
if ($drifted.Count -gt 0) {
    Write-Warning '生成物漂移：本轮回归改写了源码树，明细：'
    $drifted | ForEach-Object { Write-Warning "  $_" }
}
# 回归结果绑定实际加载的包；失败时也要落盘 passed=false，不能让上一轮的成功记录冒充本次通过。
# passed 同时要求夹具通过与源码树稳定，避免漂移被上一轮结果或安装脚本当成绿灯。
@{compiler='tModLoader native';packages=$nativePackages;generatedArtifactsStable=($drifted.Count -eq 0);
  passed=($result -eq 0 -and $drifted.Count -eq 0)} | ConvertTo-Json -Depth 5 |
    Set-Content -Encoding utf8 "$taskRoot/artifacts/native-build-check.json"
if ($result -ne 0) { throw 'Isolated wood checks failed; see artifacts/wood-load.log' }
if ($drifted.Count -gt 0) { throw '生成物漂移：回归改写了源码树，请检查生成器写盘口径（见上方明细）' }
