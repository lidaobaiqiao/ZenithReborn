$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $taskRoot 'artifacts/slash-render-profile'
$version = ((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$output = Join-Path $taskRoot "artifacts/slash-gpu-$version"
$loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
New-Item -ItemType Directory -Force -Path "$profile/Mods", $output | Out-Null
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
$config = @{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true}
$config | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_SLASH_RENDER = $output
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN = '0'
$process = Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loaderRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/client-stdout.log" -RedirectStandardError "$output/client-stderr.log"
Write-Output "Menu-only GPU render client started: PID $($process.Id); output: $output"
