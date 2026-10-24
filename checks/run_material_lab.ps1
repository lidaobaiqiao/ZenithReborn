param([switch]$Check,[int]$TimeoutSec=240)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
# 自动检查使用自己的隔离档，不能覆盖用户正在制作的输入图和材质草稿。
$profile=Join-Path $taskRoot $(if($Check){"artifacts/material-lab-profile-check-$version"}else{'artifacts/material-lab-profile'})
$output=Join-Path $taskRoot "artifacts/material-lab-$version"
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
foreach($name in @('ZenithReborn','ZenithMaterialLab')){Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"}
'["ZenithReborn","ZenithMaterialLab"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1440;DisplayHeight=900;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
$env:ZENITH_MATERIAL_LAB_CHECK=if($Check){$output}else{''}
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$startedAt=[DateTime]::UtcNow
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "独立材质实验室 PID $($process.Id)；制作目录 $profile/MaterialLab"
if($Check){
    $process.WaitForExit($TimeoutSec*1000) | Out-Null
    if(-not $process.HasExited){throw '制作客户端超时，等待正常退出，不强制结束。'}
    if(-not(Test-Path "$output/gpu.json")){throw "未生成结果：$output"}
    if((Get-Item "$output/gpu.json").LastWriteTimeUtc -lt $startedAt){throw '结果属于旧回合，不能复用为本次通过。'}
    $result=Get-Content "$output/gpu.json" -Raw | ConvertFrom-Json
    if(-not $result.passed){throw '材质逐项GPU核对存在失败，请查看gpu.json及A/B图片。'}
    # 描述表来自与正式渲染共用的C#合同，导入工具不另写一份参数范围。
    New-Item -ItemType Directory -Force -Path "$taskRoot/VfxLibrary/Materials/Surface/Glass" | Out-Null
    Copy-Item "$output/controls.json" "$taskRoot/VfxLibrary/Materials/Surface/schema-controls.json"
    Copy-Item "$output/sample.material.json" "$taskRoot/VfxLibrary/Materials/Surface/Glass/material.json"
    Write-Output "实际GPU逐项核对通过：$($result.cases.Count)组。"
}
