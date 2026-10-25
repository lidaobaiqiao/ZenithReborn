param(
    [Parameter(Mandatory=$true)][ValidateSet('0.3.79','0.3.80')][string]$Version,
    [ValidateSet('world','gpu')][string]$Mode='world',
    [string]$Run='20261025-originals'
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$captureRoot=Join-Path $taskRoot "artifacts/vfx-comparison-$Run"
$output=Join-Path $captureRoot "$Version-$Mode"
$marker=if($Mode -eq 'world'){'soul-flight-world-profile'}else{'soul-trail-render-profile'}
$profile=Join-Path $captureRoot "$marker-$Version"
$package=if($Version -eq '0.3.79'){
    Join-Path $taskRoot 'artifacts/backups/20261025-131530/ZenithReborn.tmod'
}else{Join-Path $taskRoot 'artifacts/ZenithReborn-0.3.80.tmod'}
$expected=if($Version -eq '0.3.79'){'c7b88428525a192e5764de000e180be73184933a41a86bf61a3832bd1fecdad1'}else{'6473635ed76bffaeb06bb404375138bdb250b3b0529cb11755419e5d7aef20dd'}
if((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw '原始包哈希不符'}
if(Test-Path $output){throw '输出目录已存在；使用新Run值保留历史记录'}
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/Players","$profile/Worlds",$output | Out-Null
Copy-Item -LiteralPath $package -Destination "$profile/Mods/ZenithReborn.tmod"
$fixture=if($Version -eq '0.3.80'){
    Join-Path $taskRoot 'artifacts/vfx-comparison-20261025-originals/fixture-0.3.80/build/Mods/ZenithPrototypeCheck.tmod'
}else{
    # 正式开发已进入新材质接口；继续使用首次79采集中保留的兼容夹具，不能取最新构建包。
    Join-Path $taskRoot 'artifacts/vfx-comparison-20261025-originals/soul-flight-world-profile-0.3.79/Mods/ZenithPrototypeCheck.tmod'
}
# 测试入口源码相同，但必须分别针对两版不同的材质方法签名编译。
Copy-Item -LiteralPath $fixture -Destination "$profile/Mods/ZenithPrototypeCheck.tmod"
if($Mode -eq 'world'){
    # 两版都从同一份隔离档复制，不读取正式人物或世界。
    foreach($kind in @('Players','Worlds')){
        Get-ChildItem -LiteralPath "$taskRoot/stage-one-playtest/tModLoader/$kind" -File |
            Where-Object {$_.BaseName -eq '测试' -and $_.Extension -in @('.plr','.tplr','.wld','.twld')} |
            Copy-Item -Destination "$profile/$kind"
    }
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;
  AutoSave=$false;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LightingMode=3;
  LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
# 显式清除继承的其他夹具开关，避免两种采集模式互相影响。
foreach($key in @('ZENITH_SOUL_FLIGHT_WORLD','ZENITH_SOUL_TRAIL_RENDER','ZENITH_SURFACE_WORLD','ZENITH_ECHO_DAMAGE_CHECK','ZENITH_SOUL_INPUT_CHECK','ZENITH_WOOD')){
    [Environment]::SetEnvironmentVariable($key,$null,'Process')
}
if($Mode -eq 'world'){$env:ZENITH_SOUL_FLIGHT_WORLD=$output}else{$env:ZENITH_SOUL_TRAIL_RENDER=$output}
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
@{version=$Version;mode=$Mode;pid=$process.Id;startedUtc=[DateTime]::UtcNow.ToString('o');packageSha256=$expected;
  fixtureSha256=(Get-FileHash "$profile/Mods/ZenithPrototypeCheck.tmod" -Algorithm SHA256).Hash.ToLowerInvariant();
  output=$output;profile=$profile;formalPackageModified=$false} | ConvertTo-Json |
    Set-Content -Encoding utf8 "$output/capture-session.json"
Write-Output "已启动原始 $Version 的 $Mode 隔离采集，PID $($process.Id)；输出 $output"
