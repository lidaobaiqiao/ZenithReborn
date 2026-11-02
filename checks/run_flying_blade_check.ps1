# 飞刃族行为夹具：隔离 profile 编译两包并运行名册/蓄能/齐射/三阶段检查，不触碰正式存档。
# 构建走独立目录，不污染 build-profile（package_install 依赖其 mtime 时序）。
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet = Join-Path $loaderRoot 'dotnet/dotnet.exe'
$testProfile = Join-Path $taskRoot 'check-profile'
# 夹具专用构建目录：与 build-profile 隔离，避免刷新正式包 mtime 破坏安装脚本的时序断言。
$fbBuildDir = Join-Path $taskRoot 'flying-blade-profile'
New-Item -ItemType Directory -Force -Path "$testProfile/Mods", "$fbBuildDir/Mods", "$taskRoot/artifacts" | Out-Null
# 构建期依赖兜底：复制（不移动）Luminance 包到夹具构建目录，防止 build.txt 恢复 modReferences 声明后构建缺依赖。
Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/Luminance.tmod" -Destination "$fbBuildDir/Mods/Luminance.tmod" -Force
# 检查项目按 bin/Release 产物引用主模组；打包前先用系统 SDK 标准构建刷新该 DLL，避免引用陈旧程序集。
$dotnetSdk = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnetSdk build "$taskRoot/ZenithReborn/ZenithReborn.csproj" -c Release *> "$taskRoot/artifacts/ZenithReborn-fbcheck-build.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/ZenithReborn-fbcheck-build.log"; throw 'Standard build failed' }
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    $source = if ($name -eq 'ZenithReborn') { "$taskRoot/ZenithReborn" } else { "$taskRoot/checks/ZenithPrototypeCheck" }
    Push-Location $loaderRoot
    try {
        & $dotnet ./tModLoader.dll -server -build $source -tmlsavedirectory "$fbBuildDir" *> "$taskRoot/artifacts/$name-fbcheck-pack.log"
        if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-fbcheck-pack.log"; throw "Packaging failed: $name" }
    } finally { Pop-Location }
    Copy-Item -LiteralPath "$fbBuildDir/Mods/$name.tmod" -Destination "$testProfile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$testProfile/Mods/enabled.json"
$env:ZENITH_CHECK_REPORT = "$taskRoot/artifacts/flying-blade-check.txt"
$env:ZENITH_FLYING_BLADE_CHECK = '1'
Push-Location $loaderRoot
try {
    & $dotnet ./tModLoader.dll -server -nosteam -tmlsavedirectory $testProfile *> "$taskRoot/artifacts/flying-blade-check-load.log"
    $result = $LASTEXITCODE
} finally { Pop-Location }
Get-Content "$taskRoot/artifacts/flying-blade-check.txt"
if ($result -ne 0) { throw 'Flying blade checks failed; see artifacts/flying-blade-check-load.log' }
