param([int]$TimeoutSec=180)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskProject=Join-Path $taskRoot 'artifacts/smooth-glass-layers-r1'
$taskProfile=Join-Path $taskRoot 'artifacts/material-lab-profile-smooth-glass-r1'
$taskOutput=Join-Path $taskProject 'gpu'
New-Item -ItemType Directory -Force -Path "$taskProfile/Mods","$taskProfile/MaterialLab/Input",$taskOutput | Out-Null
# 复用已构建包，在专用制作档验证新输入；不编译或覆盖其他窗口正在修改的生产源码。
foreach($taskName in @('ZenithReborn','ZenithMaterialLab')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$taskName.tmod" -Destination "$taskProfile/Mods/$taskName.tmod"
}
Copy-Item -LiteralPath "$taskProject/material.json" -Destination "$taskProfile/MaterialLab/active.material.json"
foreach($taskFile in Get-ChildItem -LiteralPath "$taskProject/Input" -File){
    Copy-Item -LiteralPath $taskFile.FullName -Destination (Join-Path "$taskProfile/MaterialLab/Input" $taskFile.Name)
}
'["ZenithReborn","ZenithMaterialLab"]' | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1440;DisplayHeight=900;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
$env:ZENITH_MATERIAL_LAB_CHECK=''
$env:ZENITH_MATERIAL_LAB_ARTCHECK=$taskOutput
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStarted=[DateTime]::UtcNow
$taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$taskOutput/stdout.log" -RedirectStandardError "$taskOutput/stderr.log"
Write-Output "平滑分层候选专用GPU客户端 PID $($taskProcess.Id)"
$taskProcess.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $taskProcess.HasExited){throw '候选预览超时，保留客户端等待正常退出，不强制结束。'}
if(-not(Test-Path "$taskOutput/artwork.json")){throw '未生成素材GPU记录，请查看独立客户端日志。'}
if((Get-Item "$taskOutput/artwork.json").LastWriteTimeUtc -lt $taskStarted){throw 'GPU记录属于旧运行。'}
$taskResult=Get-Content -LiteralPath "$taskOutput/artwork.json" -Raw | ConvertFrom-Json
$taskResult | Select-Object passed,changed,animated,sameCoverage,inputsEffective | ConvertTo-Json
if(-not $taskResult.passed){throw '素材专项检查存在失败，需逐项分析，不能称通过。'}
