$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$profile=Join-Path $taskRoot "artifacts/information-ui-profile-$version"
$output=Join-Path $taskRoot "artifacts/boss-unlocks-$version/gpu"
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
# 清空旧的输出，避免把上一次结果误当成本次结果；个别日志句柄可能短暂未释放，跳过即可
Get-ChildItem $output -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod" -Force
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;
  AutoSave=$false;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;
  LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
# 清掉其他夹具的环境变量，避免它们误触发自动进世界
foreach($key in @('ZENITH_SOUL_FLIGHT_WORLD','ZENITH_SOUL_TRAIL_RENDER','ZENITH_WOOD','ZENITH_SOUL_INPUT_CHECK',
    'ZENITH_AIRFLOW_WORLD','ZENITH_SURFACE_WORLD','ZENITH_HYBRID_GPU','ZENITH_WORLD_LIGHT_PRESET')){
    [Environment]::SetEnvironmentVariable($key,$null,'Process')
}
$env:ZENITH_INFORMATION_GPU=$output
# 窗口不抢前台，避免被壁纸层干扰，也不打断用户
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "信息界面GPU已启动，PID $($process.Id)；输出 $output"
