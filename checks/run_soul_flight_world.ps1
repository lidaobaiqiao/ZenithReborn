param([int]$TimeoutSec=180,[switch]$Surface,[switch]$EchoDamage,[switch]$FullModSet,[switch]$FormalSettings,[switch]$Original79,
    [ValidateSet('day','dusk','night','torch')][string]$LightPreset='day')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
if($Original79){$version='0.3.79-original'}
$profile=Join-Path $taskRoot "artifacts/soul-flight-world-profile-$version"
$output=Join-Path $taskRoot "artifacts/soul-flight-world-$version"
if($Surface){$profile+='-surface';$output+='-surface'}
if($EchoDamage){$profile+='-echo-damage';$output+='-echo-damage'}
if($FullModSet){$profile+='-full';$output+='-full'}
if($LightPreset -ne 'day'){$profile+="-$LightPreset";$output+="-$LightPreset"}
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/Players","$profile/Worlds",$output | Out-Null
# 只读复制旧隔离档，不读取或改写正式人物／世界。
foreach($kind in @('Players','Worlds')){
    Get-ChildItem -LiteralPath "$taskRoot/stage-one-playtest/tModLoader/$kind" -File |
        Where-Object {$_.BaseName -eq '测试' -and $_.Extension -in @('.plr','.tplr','.wld','.twld')} |
        Copy-Item -Destination "$profile/$kind"
}
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    $package=if($Original79 -and $name -eq 'ZenithReborn'){
        # 当前夹具按恢复的79接口编译；被测生产包直接使用收藏原件。
        "$taskRoot/artifacts/backups/20261025-131530/ZenithReborn.tmod"
    }else{"$taskRoot/build-profile/Mods/$name.tmod"}
    if($Original79 -and $name -eq 'ZenithReborn' -and
        (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'c7b88428525a192e5764de000e180be73184933a41a86bf61a3832bd1fecdad1'){
        throw '原始79收藏包哈希不符'
    }
    Copy-Item -LiteralPath $package -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
if($FullModSet){
    # 与正式启用组合相同，但只在隔离档发射受控攻击，不读取正式人物与世界。
    $workshop='E:/SteamLibrary/steamapps/workshop/content/1281930'
    foreach($relative in @('2564645933/2026.7/HEROsMod.tmod','2797518634/2026.3/ImproveGame.tmod',
        '3403460014/2026.3/SilkyUIFramework.tmod','3339387093/2025.7/UpgradableZenith.tmod')){
        Copy-Item -LiteralPath "$workshop/$relative" -Destination "$profile/Mods/"
    }
    '["ZenithReborn","ZenithPrototypeCheck","HEROsMod","ImproveGame","SilkyUIFramework","UpgradableZenith"]' |
        Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
}
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;
  AutoSave=$false;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LightingMode=3;
  LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
if($FormalSettings){
    # 只读复制正式画面设置与模组配置，隔离档保持静音、窗口化与禁止自动保存。
    $formalProfile=Join-Path $env:USERPROFILE 'Documents/My Games/Terraria/tModLoader'
    $settings=Get-Content "$formalProfile/config.json" -Raw | ConvertFrom-Json
    $settings.Fullscreen=$false;$settings.WindowBorderless=$false;$settings.AutoSave=$false
    $settings.VolumeMusic=0;$settings.VolumeSound=0;$settings.VolumeAmbient=0
    $settings | ConvertTo-Json -Depth 30 | Set-Content -Encoding utf8 "$profile/config.json"
    New-Item -ItemType Directory -Force -Path "$profile/ModConfigs" | Out-Null
    Get-ChildItem "$formalProfile/ModConfigs" -Filter '*.json' -File | Copy-Item -Destination "$profile/ModConfigs"
}
$env:ZENITH_SOUL_FLIGHT_WORLD=$output
$env:ZENITH_WORLD_LIGHT_PRESET=$LightPreset
$env:ZENITH_SURFACE_WORLD=if($Surface){'1'}else{''}
$env:ZENITH_ECHO_DAMAGE_CHECK=if($EchoDamage){'1'}else{''}
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
