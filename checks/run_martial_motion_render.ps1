$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot 'artifacts/martial-render-profile'
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$output=Join-Path $taskRoot "artifacts/martial-motion-$version"
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;DisplayWidth=1280;DisplayHeight=800;VolumeMusic=0;VolumeSound=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_MARTIAL_RENDER=$output
$env:ZENITH_CHECK_REPORT=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
# 只运行隔离菜单GPU夹具，完成后由客户端自行正常退出，不加载人物世界。
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/client.log" -RedirectStandardError "$output/client-error.log"
Write-Output "隔离动作GPU客户端PID: $($process.Id)，输出: $output"
