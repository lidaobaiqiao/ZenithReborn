param([int]$TimeoutSec=180,[switch]$Interactive)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$project=Join-Path $taskRoot 'artifacts/energy-blade-a1'
$profile=Join-Path $taskRoot 'artifacts/material-lab-profile-energy-blade-a1'
$output=Join-Path $project 'gpu'
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/MaterialLab/Input",$output | Out-Null
# 只写本次素材的专用隔离制作档，不覆盖正式档或现有工作台草稿。
foreach($name in @('ZenithReborn','ZenithMaterialLab')){Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"}
if(-not $Interactive -or -not(Test-Path "$profile/MaterialLab/active.material.json")){
    Copy-Item -LiteralPath "$project/material.json" -Destination "$profile/MaterialLab/active.material.json"
}
foreach($file in Get-ChildItem -LiteralPath "$project/Input" -File){
    $target=Join-Path "$profile/MaterialLab/Input" $file.Name
    if(-not $Interactive -or -not(Test-Path $target)){Copy-Item -LiteralPath $file.FullName -Destination $target}
}
'["ZenithReborn","ZenithMaterialLab"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1440;DisplayHeight=900;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
$env:ZENITH_MATERIAL_LAB_CHECK=''
$env:ZENITH_MATERIAL_LAB_ARTCHECK=if($Interactive){''}else{$output}
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "能量薄刃素材预览 PID $($process.Id)；专用隔离制作档已启动。"
if($Interactive){return}
$process.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $process.HasExited){throw '预览超时，保留客户端等待正常退出。'}
if(-not(Test-Path "$output/artwork.json")){throw '没有生成素材GPU记录，请检查隔离日志。'}
if((Get-Item "$output/artwork.json").LastWriteTimeUtc -lt $started){throw '记录属于旧运行，不能复用。'}
$result=Get-Content "$output/artwork.json" -Raw | ConvertFrom-Json
if(-not $result.passed){throw '素材GPU对照未通过，请查看具体记录。'}
Write-Output '素材实际GPU动画、轮廓一致性及配方导出通过；这不是世界战斗或美术认可。'
