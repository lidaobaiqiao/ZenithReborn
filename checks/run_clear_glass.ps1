param([string]$Label='candidate',[switch]$Baseline,[string]$Version='0.3.111',[switch]$HighlightProbe,[string]$PackageVersion='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot "artifacts/clear-glass-$Version/$Label"
$taskProfile=Join-Path $taskRoot "artifacts/clear-glass-profile-$Label"
New-Item -ItemType Directory -Force -Path "$taskProfile/Mods",$taskOutput | Out-Null
foreach($taskName in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$taskName.tmod" -Destination "$taskProfile/Mods/$taskName.tmod"
}
if($Baseline){Copy-Item -LiteralPath "$taskRoot/artifacts/ZenithReborn-0.3.110.tmod" -Destination "$taskProfile/Mods/ZenithReborn.tmod"}
if($PackageVersion){Copy-Item -LiteralPath "$taskRoot/artifacts/ZenithReborn-$PackageVersion.tmod" -Destination "$taskProfile/Mods/ZenithReborn.tmod"}
Copy-Item -LiteralPath "$taskRoot/artifacts/soul-flight-world-0.3.104-full-spatial-final/before-refraction.png" -Destination "$taskProfile/day.png"
Copy-Item -LiteralPath "$taskRoot/artifacts/soul-flight-world-0.3.104-full-night-spatial-final/before-refraction.png" -Destination "$taskProfile/night.png"
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
$env:ZENITH_CLEAR_GLASS_RENDER=$taskOutput
$env:ZENITH_CLEAR_GLASS_BASELINE=if($Baseline){'1'}else{'0'}
$env:ZENITH_GLASS_HIGHLIGHT_PROBE=if($HighlightProbe){'1'}else{'0'}
$env:ZENITH_TEXTURED_SWORD_RENDER='';$env:ZENITH_SOUL_FLIGHT_WORLD='';$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStart=[DateTime]::UtcNow
$taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$taskOutput/stdout.log" -RedirectStandardError "$taskOutput/stderr.log"
Write-Output "玻璃专项GPU客户端 PID $($taskProcess.Id)"
while(-not $taskProcess.HasExited -and ([DateTime]::UtcNow-$taskStart).TotalSeconds -lt 180){$taskProcess.WaitForExit(1000) | Out-Null}
if(-not $taskProcess.HasExited){throw '玻璃专项超时，保留客户端等待正常退出'}
$taskReport=if($HighlightProbe){"$taskOutput/highlight-probe.json"}else{"$taskOutput/clear-glass.json"}
if(-not(Test-Path $taskReport)){
    if(Test-Path "$taskOutput/error.txt"){Get-Content "$taskOutput/error.txt"};throw '未生成玻璃报告'
}
if((Get-Item $taskReport).LastWriteTimeUtc -lt $taskStart){throw '玻璃报告属于旧回合'}
$taskResult=Get-Content $taskReport -Raw | ConvertFrom-Json
$taskResult | Select-Object passed,baseline,coverageIndependent,clearSurfacePixels,alphaMismatch,overlapGain,refractedPixels,emptyChanged,testedPackageSha256 | ConvertTo-Json
if(-not $taskResult.passed){throw '玻璃专项存在失败'}
