param([switch]$AssetsOnly,[string]$Source='Flames')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot 'artifacts/mask-preview-profile'
$output=Join-Path $taskRoot 'artifacts/mask-composition-preview'
# 打包走独立目录：写 build-profile 会刷新正式包 mtime，破坏 package_install 的时序断言。
$buildDir=Join-Path $taskRoot 'mask-preview-build-profile'
$loader='E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet='C:/Program Files/dotnet/dotnet.exe'
$production='C:/Users/15537/Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod'
New-Item -ItemType Directory -Force -Path "$profile/Mods","$buildDir/Mods",$output | Out-Null
# 夹具的 modReferences 需要主模组包；复制（不移动）同一份正式包，构建期与运行期保持一致。
Copy-Item -LiteralPath $production -Destination "$buildDir/Mods/ZenithReborn.tmod" -Force
& $dotnet build "$PSScriptRoot/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj" -c Release --nologo *> "$output/build.log"
if($LASTEXITCODE -ne 0){Get-Content "$output/build.log";throw 'Preview build failed'}
Push-Location $loader
try {
    & $dotnet ./tModLoader.dll -server -build "$PSScriptRoot/ZenithPrototypeCheck" -eac "$PSScriptRoot/ZenithPrototypeCheck/bin/Release/net8.0/ZenithPrototypeCheck.dll" -tmlsavedirectory "$buildDir" *> "$output/pack.log"
    if($LASTEXITCODE -ne 0){Get-Content "$output/pack.log";throw 'Preview package failed'}
} finally {Pop-Location}
Copy-Item -LiteralPath "$buildDir/Mods/ZenithPrototypeCheck.tmod" -Destination "$profile/Mods/ZenithPrototypeCheck.tmod"
Copy-Item -LiteralPath $production -Destination "$profile/Mods/ZenithReborn.tmod"
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_MASK_PREVIEW=$output
$env:ZENITH_MASK_SOURCE=$Source
$env:ZENITH_MASK_ASSETS_ONLY=if($AssetsOnly){'1'}else{'0'}
$env:ZENITH_SLASH_RENDER=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath $dotnet -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loader -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "Isolated mask preview PID $($process.Id); output $output"
