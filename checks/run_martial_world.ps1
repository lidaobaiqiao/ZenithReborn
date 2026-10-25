$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$profile=Join-Path $taskRoot "artifacts/martial-playtest-$version"
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/Players","$profile/Worlds" | Out-Null
# 只复制旧隔离档的本地测试人物世界，禁止引用正式存档。
foreach($kind in @('Players','Worlds')){
    Get-ChildItem -LiteralPath "$taskRoot/stage-one-playtest/tModLoader/$kind" -File |
        Where-Object {$_.BaseName -eq '测试' -and $_.Extension -in @('.plr','.tplr','.wld','.twld')} |
        Copy-Item -Destination "$profile/$kind"
}
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;DisplayWidth=1280;DisplayHeight=800;VolumeMusic=0;VolumeSound=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_MARTIAL_GUI='1';$env:ZENITH_MARTIAL_AUTORUN='1';$env:ZENITH_CHECK_REPORT='';$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$profile/client.log" -RedirectStandardError "$profile/client-error.log"
Write-Output "武技隔离世界PID: $($process.Id)，输出: $profile"
