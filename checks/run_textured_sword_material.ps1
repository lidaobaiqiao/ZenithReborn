param([string]$Label='current',[int]$TimeoutSec=180)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot "artifacts/textured-sword-material/$Label"
$taskProfile=Join-Path $taskRoot "artifacts/textured-sword-profile-$Label"
New-Item -ItemType Directory -Force -Path "$taskProfile/Mods",$taskOutput | Out-Null
foreach($taskName in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$taskName.tmod" -Destination "$taskProfile/Mods/$taskName.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
$env:ZENITH_TEXTURED_SWORD_RENDER=$taskOutput
$env:ZENITH_SOUL_TRAIL_RENDER='';$env:ZENITH_PURE_CODE_RENDER='';$env:ZENITH_HEAD_JOIN_RENDER='';$env:ZENITH_SOUL_FLIGHT_WORLD=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStart=[DateTime]::UtcNow
$taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$taskOutput/stdout.log" -RedirectStandardError "$taskOutput/stderr.log"
Write-Output "完整贴图材质GPU客户端 PID $($taskProcess.Id)"
$taskProcess.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $taskProcess.HasExited){throw '完整贴图检查超时，保留客户端等待正常退出'}
if(-not(Test-Path "$taskOutput/textured-material.json")){
    if(Test-Path "$taskOutput/error.txt"){Get-Content "$taskOutput/error.txt"};throw '未生成完整材质报告'
}
if((Get-Item "$taskOutput/textured-material.json").LastWriteTimeUtc -lt $taskStart){throw '材质报告属于旧回合'}
$taskResult=Get-Content "$taskOutput/textured-material.json" -Raw | ConvertFrom-Json
$taskResult | Select-Object passed,allEffectsEnabled,enabledOptionCount,uses,spaceWarp,lightingChangedPixels,refraction | ConvertTo-Json -Depth 6
if(-not $taskResult.passed){throw '完整贴图材质检查存在失败'}
