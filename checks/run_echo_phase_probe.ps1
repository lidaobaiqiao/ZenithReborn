$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet = Join-Path $loaderRoot 'dotnet/dotnet.exe'
$testProfile = Join-Path $taskRoot 'wood-check-profile'
# 构建走独立目录：写 build-profile 会刷新正式包 mtime，破坏 package_install 的时序断言。
$probeBuildDir = Join-Path $taskRoot 'echo-phase-profile'
New-Item -ItemType Directory -Force -Path "$testProfile/Mods", "$probeBuildDir/Mods", "$taskRoot/artifacts" | Out-Null
# 构建期依赖兜底：复制（不移动）第三方包到探针构建目录，防止 build.txt 恢复 modReferences 后缺依赖。
if (Test-Path -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod") {
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod" -Destination "$probeBuildDir/Mods/Luminance.tmod" -Force
}
# 与正式回归同一套隔离档与原生编译器，不触碰正式人物和世界。
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    $source = if ($name -eq 'ZenithReborn') { "$taskRoot/ZenithReborn" } else { "$taskRoot/checks/ZenithPrototypeCheck" }
    Push-Location $loaderRoot
    try {
        & $dotnet ./tModLoader.dll -server -build $source -tmlsavedirectory "$probeBuildDir" *> "$taskRoot/artifacts/$name-echo-probe-pack.log"
        if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-echo-probe-pack.log"; throw "Packaging failed: $name" }
    } finally { Pop-Location }
    Copy-Item -LiteralPath "$probeBuildDir/Mods/$name.tmod" -Destination "$testProfile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$testProfile/Mods/enabled.json"
$env:ZENITH_WOOD = $null
$env:ZENITH_ECHO_PHASE = '1'
$env:ZENITH_CHECK_REPORT = "$taskRoot/artifacts/echo-phase-probe.txt"
Push-Location $loaderRoot
try {
    & $dotnet ./tModLoader.dll -server -nosteam -tmlsavedirectory $testProfile *> "$taskRoot/artifacts/echo-phase-probe-load.log"
} finally { Pop-Location }
Get-Content "$taskRoot/artifacts/echo-phase-probe.txt"
