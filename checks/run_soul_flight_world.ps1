param([int]$TimeoutSec=180,[switch]$Surface)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot 'artifacts/soul-flight-world-profile'
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$output=Join-Path $taskRoot "artifacts/soul-flight-world-$version"
if($Surface){$profile+='-surface';$output+='-surface'}
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/Players","$profile/Worlds",$output | Out-Null
# 只读复制旧隔离档，不读取或改写正式人物／世界。
foreach($kind in @('Players','Worlds')){
    Get-ChildItem -LiteralPath "$taskRoot/stage-one-playtest/tModLoader/$kind" -File |
        Where-Object {$_.BaseName -eq '测试' -and $_.Extension -in @('.plr','.tplr','.wld','.twld')} |
        Copy-Item -Destination "$profile/$kind"
}
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;
  AutoSave=$false;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LightingMode=3;
  LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
$env:ZENITH_SOUL_FLIGHT_WORLD=$output
$env:ZENITH_SURFACE_WORLD=if($Surface){'1'}else{''}
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$startedAt=[DateTime]::UtcNow
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "主飞剑隔离世界夹具 PID $($process.Id)；输出 $output"
$process.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $process.HasExited){throw "夹具超时；保留客户端等待正常退出，不强制终止。"}
$report=Join-Path $output 'world.json'
if(-not(Test-Path $report)){throw '未生成本轮世界验证结果，检查独立输出目录。'}
if((Get-Item $report).LastWriteTimeUtc -lt $startedAt){throw '结果属于旧回合，不能复用为本次通过。'}
$result=Get-Content $report -Raw | ConvertFrom-Json
if(-not $result.passed){throw '三剑实际命中或返回未全部通过。'}
Write-Output '三把主飞剑真实AI、命中与返回通过；这是世界夹具，不是鼠标攻击或多人证据。'
