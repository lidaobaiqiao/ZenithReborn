# 剑矩阵普查：隔离 profile 加载正式源码包，输出全部可寄宿原版近战的真实面板数值与族归属。
# 只读引擎数据，不触碰正式存档。数值曲线重建时用它做"每槽放什么最优"的推演基础。
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
# 解析顺序：环境变量 ZENITH_TML_ROOT > 仓库根 local.props 的 TmlRoot > 默认安装路径。
$loaderRoot = $env:ZENITH_TML_ROOT
if (-not $loaderRoot) {
    $props = Join-Path $taskRoot 'local.props'
    if (Test-Path -LiteralPath $props) {
        [xml]$xml = Get-Content -LiteralPath $props -Raw
        $loaderRoot = $xml.Project.PropertyGroup.TmlRoot
    }
}
if (-not $loaderRoot) { $loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader' }
$dotnet = Join-Path $loaderRoot 'dotnet/dotnet.exe'
$testProfile = Join-Path $taskRoot 'check-profile'
# 构建走独立目录：写 build-profile 会刷新正式包 mtime，破坏 package_install 的时序断言。
$surveyBuildDir = Join-Path $taskRoot 'sword-matrix-profile'
New-Item -ItemType Directory -Force -Path "$testProfile/Mods", "$surveyBuildDir/Mods", "$taskRoot/artifacts" | Out-Null
if (Test-Path -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod") {
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod" -Destination "$surveyBuildDir/Mods/Luminance.tmod" -Force
}
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    $source = if ($name -eq 'ZenithReborn') { "$taskRoot/ZenithReborn" } else { "$taskRoot/checks/ZenithPrototypeCheck" }
    Push-Location $loaderRoot
    try {
        & $dotnet ./tModLoader.dll -server -build $source -tmlsavedirectory "$surveyBuildDir" *> "$taskRoot/artifacts/$name-matrix-pack.log"
        if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-matrix-pack.log"; throw "Packaging failed: $name" }
    } finally { Pop-Location }
    Copy-Item -LiteralPath "$surveyBuildDir/Mods/$name.tmod" -Destination "$testProfile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$testProfile/Mods/enabled.json"
$env:ZENITH_CHECK_REPORT = "$taskRoot/artifacts/sword-matrix-report.txt"
$env:ZENITH_SWORD_MATRIX = '1'
$env:ZENITH_SWORD_MATRIX_OUTPUT = "$taskRoot/artifacts/sword-matrix.json"
Push-Location $loaderRoot
try {
    & $dotnet ./tModLoader.dll -server -nosteam -tmlsavedirectory $testProfile *> "$taskRoot/artifacts/sword-matrix-load.log"
    $result = $LASTEXITCODE
} finally { Pop-Location }
Get-Content "$taskRoot/artifacts/sword-matrix-report.txt"
if ($result -ne 0) { throw 'Sword matrix survey failed; see artifacts/sword-matrix-load.log' }
