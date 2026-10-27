param([string]$PackagePath='', [string]$Label='before', [int]$TimeoutSec=150)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not $PackagePath){$PackagePath='C:/Users/15537/Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod'}
$taskOutput=Join-Path $taskRoot "artifacts/sword-head-join/$Label"
$taskProfile=Join-Path $taskRoot "artifacts/head-join-profile-$Label"
New-Item -ItemType Directory -Force -Path "$taskProfile/Mods",$taskOutput | Out-Null
Copy-Item -LiteralPath $PackagePath -Destination "$taskProfile/Mods/ZenithReborn.tmod"
Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/ZenithPrototypeCheck.tmod" -Destination "$taskProfile/Mods/ZenithPrototypeCheck.tmod"
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
$env:ZENITH_HEAD_JOIN_RENDER=$taskOutput;$env:ZENITH_PURE_CODE_RENDER='';$env:ZENITH_STRIPE_ABLATION='';$env:ZENITH_SOUL_TRAIL_RENDER=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStarted=[DateTime]::UtcNow
$taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$taskOutput/stdout.log" -RedirectStandardError "$taskOutput/stderr.log"
Write-Output "刀身接合独立GPU客户端 PID $($taskProcess.Id)"
$taskProcess.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $taskProcess.HasExited){throw '接合检查超时，保留客户端等待正常退出'}
if(-not(Test-Path "$taskOutput/head-join.json")){
    if(Test-Path "$taskOutput/error.txt"){Get-Content "$taskOutput/error.txt"};throw '未生成接合检查报告'
}
if((Get-Item "$taskOutput/head-join.json").LastWriteTimeUtc -lt $taskStarted){throw '检查报告属于旧运行'}
(Get-Content "$taskOutput/head-join.json" -Raw | ConvertFrom-Json).records | Select-Object item,halfLength,connected | ConvertTo-Json
