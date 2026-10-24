$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet = 'C:/Program Files/dotnet/dotnet.exe'
$testProfile = Join-Path $taskRoot 'wood-check-profile'
New-Item -ItemType Directory -Force -Path "$testProfile/Mods", "$taskRoot/artifacts" | Out-Null
# 只编译项目自有着色器；微软编译器只在开发时使用，不新增玩家依赖。
$python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
if(-not(Test-Path $python)){$python=(Get-Command python -ErrorAction Stop).Source}
& $python "$taskRoot/checks/emit_glass_preset.py" *> "$taskRoot/artifacts/glass-preset-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/artifacts/glass-preset-build.log";throw '制作端玻璃预设生成失败'}
& $python "$taskRoot/checks/compile_effects.py" *> "$taskRoot/artifacts/own-effect-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/artifacts/own-effect-build.log";throw '自有着色器编译失败'}
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    $source = if ($name -eq 'ZenithReborn') { "$taskRoot/ZenithReborn" } else { "$taskRoot/checks/ZenithPrototypeCheck" }
    & $dotnet build "$source/$name.csproj" -c Release --nologo *> "$taskRoot/artifacts/$name-wood-build.log"
    if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-wood-build.log"; throw "Build failed: $name" }
    Push-Location $loaderRoot
    try {
        & $dotnet ./tModLoader.dll -server -build $source -eac "$source/bin/Release/net8.0/$name.dll" -tmlsavedirectory "$taskRoot/build-profile" *> "$taskRoot/artifacts/$name-wood-pack.log"
        if ($LASTEXITCODE -ne 0) { Get-Content "$taskRoot/artifacts/$name-wood-pack.log"; throw "Packaging failed: $name" }
    } finally { Pop-Location }
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$testProfile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$testProfile/Mods/enabled.json"
$env:ZENITH_WOOD = '1'
$env:ZENITH_CHECK_REPORT = "$taskRoot/artifacts/wood-check.txt"
Push-Location $loaderRoot
try {
    & $dotnet ./tModLoader.dll -server -nosteam -tmlsavedirectory $testProfile *> "$taskRoot/artifacts/wood-load.log"
    $result = $LASTEXITCODE
} finally { Pop-Location }
Get-Content "$taskRoot/artifacts/wood-check.txt"
if ($result -ne 0) { throw 'Isolated wood checks failed; see artifacts/wood-load.log' }
