$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$profile=Join-Path $taskRoot "artifacts/hybrid-render-profile-$version"
$output=Join-Path $taskRoot "artifacts/hybrid-optics-$version"
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;
  AutoSave=$false;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;
  LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
foreach($key in @('ZENITH_SOUL_FLIGHT_WORLD','ZENITH_SOUL_TRAIL_RENDER','ZENITH_WOOD','ZENITH_SOUL_INPUT_CHECK')){
    [Environment]::SetEnvironmentVariable($key,$null,'Process')
}
$env:ZENITH_HYBRID_GPU=$output
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "融合光学GPU对照已启动，PID $($process.Id)；输出 $output"
