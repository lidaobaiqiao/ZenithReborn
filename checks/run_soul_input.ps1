param([switch]$FullModSet,[switch]$FormalSettings)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$profile=Join-Path $taskRoot "artifacts/soul-flight-world-profile-input-$version"
$output=Join-Path $taskRoot "artifacts/soul-input-$version"
if($FullModSet){$profile+='-full';$output+='-full'}
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/Players","$profile/Worlds",$output | Out-Null
# 上轮正常退出标记不能让新客户端刚启动就退出。
if(Test-Path -LiteralPath "$output/exit"){Remove-Item -LiteralPath "$output/exit"}
# 只复制旧隔离档，实际攻击必须来自鼠标，不由夹具发射。
foreach($kind in @('Players','Worlds')){
    Get-ChildItem -LiteralPath "$taskRoot/stage-one-playtest/tModLoader/$kind" -File |
        Where-Object {$_.BaseName -eq '测试' -and $_.Extension -in @('.plr','.tplr','.wld','.twld')} |
        Copy-Item -Destination "$profile/$kind"
}
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
$workshop='E:/SteamLibrary/steamapps/workshop/content/1281930'
foreach($relative in @('2564645933/2026.7/HEROsMod.tmod','2797518634/2026.3/ImproveGame.tmod','3403460014/2026.3/SilkyUIFramework.tmod')){
    Copy-Item -LiteralPath "$workshop/$relative" -Destination "$profile/Mods/"
}
if($FullModSet){
    Copy-Item -LiteralPath "$workshop/3339387093/2025.7/UpgradableZenith.tmod" -Destination "$profile/Mods/"
    '["ZenithReborn","ZenithPrototypeCheck","HEROsMod","ImproveGame","SilkyUIFramework","UpgradableZenith"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
}else{
    '["ZenithReborn","ZenithPrototypeCheck","HEROsMod","ImproveGame","SilkyUIFramework"]' | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
}
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LightingMode=3;FrameSkipMode=0;
  LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 "$profile/config.json"
if($FormalSettings){
    # 只复制设置，不复制正式人物/世界；禁用隔离自动保存并保持窗口化，便于正常退出。
    $formalProfile=Join-Path $env:USERPROFILE 'Documents/My Games/Terraria/tModLoader'
    $settings=Get-Content "$formalProfile/config.json" -Raw | ConvertFrom-Json
    $settings.Fullscreen=$false;$settings.WindowBorderless=$false;$settings.AutoSave=$false
    $settings.VolumeMusic=0;$settings.VolumeSound=0;$settings.VolumeAmbient=0
    $settings | ConvertTo-Json -Depth 30 | Set-Content -Encoding utf8 "$profile/config.json"
    New-Item -ItemType Directory -Force -Path "$profile/ModConfigs" | Out-Null
    Get-ChildItem "$formalProfile/ModConfigs" -Filter '*.json' -File | Copy-Item -Destination "$profile/ModConfigs"
}
$env:ZENITH_SOUL_FLIGHT_WORLD=$output
$env:ZENITH_SOUL_INPUT_CHECK='1'
$env:ZENITH_SURFACE_WORLD=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) `
    -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "隔离真实输入客户端 PID $($process.Id)，输出 $output"
