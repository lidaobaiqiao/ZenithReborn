# 飞刃族候选普查：隔离 profile 加载正式源码包，输出引擎级弹幕/配方数据，不触碰正式存档。
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet = Join-Path $loaderRoot 'dotnet/dotnet.exe'
$testProfile = Join-Path $taskRoot 'check-profile'
# 构建走独立目录：写 build-profile 会刷新正式包 mtime，破坏 package_install 的时序断言。
$surveyBuildDir = Join-Path $taskRoot 'flying-blade-survey-profile'
New-Item -ItemType Directory -Force -Path "$testProfile/Mods", "$surveyBuildDir/Mods", "$taskRoot/artifacts" | Out-Null
# 构建期依赖兜底：复制（不移动）第三方包到普查构建目录，防止 build.txt 恢复 modReferences 后缺依赖。
if (Test-Path -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod") {
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod" -Destination "$surveyBuildDir/Mods/Luminance.tmod" -Force
}
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    $source = if ($name -eq 'ZenithReborn') { "$taskRoot/ZenithReborn" } else { "$taskRoot/checks/ZenithPrototypeCheck" }
    Push-Location $loaderRoot
    try {
        # 与其他检查一致：游戏自带编译器，产物只进本普查专用目录。
        & $dotnet ./tModLoader.dll -server -build $source -tmlsavedirectory "$surveyBuildDir" *> "$taskRoot/artifacts/$name-survey-pack.log"
        if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-survey-pack.log"; throw "Packaging failed: $name" }
    } finally { Pop-Location }
    Copy-Item -LiteralPath "$surveyBuildDir/Mods/$name.tmod" -Destination "$testProfile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$testProfile/Mods/enabled.json"
$env:ZENITH_CHECK_REPORT = "$taskRoot/artifacts/flying-blade-survey-report.txt"
$env:ZENITH_FLYING_BLADE = '1'
$env:ZENITH_FLYING_BLADE_OUTPUT = "$taskRoot/artifacts/flying-blade-survey.json"
Push-Location $loaderRoot
try {
    & $dotnet ./tModLoader.dll -server -nosteam -tmlsavedirectory $testProfile *> "$taskRoot/artifacts/flying-blade-survey-load.log"
    $result = $LASTEXITCODE
} finally { Pop-Location }
Get-Content "$taskRoot/artifacts/flying-blade-survey-report.txt"
if ($result -ne 0) { throw 'Flying blade survey failed; see artifacts/flying-blade-survey-load.log' }
