param([int]$TimeoutSec=240,[switch]$FullModpack,[switch]$Dummy,[string]$OutputName='airflow-world-H4',
    [int]$Width=1280,[int]$Height=720,[switch]$Fullscreen,[switch]$PlacementOnly,[switch]$UseSaveCopies,
    [ValidateSet('none','attack','burst')][string]$BeforePlacement='none')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot $(if($UseSaveCopies){'artifacts/airflow-world-profile-save-copy'}else{'artifacts/airflow-world-profile'})
$output=Join-Path $taskRoot "artifacts/$OutputName"
$loader='E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet='C:/Program Files/dotnet/dotnet.exe'
New-Item -ItemType Directory -Force -Path "$profile/Mods","$profile/Players","$profile/Worlds",$output | Out-Null
# 默认复制既有测试档；专项排错可只读复制用户指定的「测试」档到新的隔离目录。
# 客户端只加载副本，自动保存关闭，不向正式目录写回任何内容。
foreach($kind in @('Players','Worlds')){
    $testSource=if($UseSaveCopies){"C:/Users/15537/Documents/My Games/Terraria/tModLoader/$kind"}else{Join-Path $taskRoot "stage-one-playtest/tModLoader/$kind"}
    Get-ChildItem -LiteralPath $testSource -File | Where-Object {$_.BaseName -eq '测试'} |
        Copy-Item -Destination "$profile/$kind"
}
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
if($FullModpack){
    $formalProfile='C:/Users/15537/Documents/My Games/Terraria/tModLoader'
    # 复制启用清单、模组包和配置；人物世界仍只取既有隔离测试档。
    Get-ChildItem -LiteralPath "$formalProfile/Mods" -Filter *.tmod | Where-Object {$_.Name -ne 'ZenithReborn.tmod'} | Copy-Item -Destination "$profile/Mods"
    $enabled=@(Get-Content "$formalProfile/Mods/enabled.json" -Raw | ConvertFrom-Json)+'ZenithPrototypeCheck'
    $enabled | ConvertTo-Json | Set-Content -Encoding utf8 "$profile/Mods/enabled.json"
    if(Test-Path "$formalProfile/ModConfigs"){Copy-Item "$formalProfile/ModConfigs" -Destination $profile -Recurse -Force}
}
@{Language='zh-Hans';Fullscreen=[bool]$Fullscreen;WindowBorderless=$false;DisplayWidth=$Width;DisplayHeight=$Height;
    VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;
    LightingMode=3;
    LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
    ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_AIRFLOW_WORLD=$output
$env:ZENITH_DUMMY_CHECK=if($Dummy){'1'}else{'0'}
$env:ZENITH_DUMMY_PLACEMENT_ONLY=if($PlacementOnly){'1'}else{'0'}
$env:ZENITH_DUMMY_BEFORE_PLACEMENT=$BeforePlacement
# 清除其他夹具变量，防止启动后运行旧的制作端或菜单回放。
foreach($name in @('ZENITH_CHECK_REPORT','ZENITH_WOOD','ZENITH_SWORDBODY_PREVIEW','ZENITH_SLASH_RENDER','ZENITH_INGAME_PREVIEW')){
    [Environment]::SetEnvironmentVariable($name,$null,'Process')
}
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$started=Get-Date
$process=Start-Process -FilePath $dotnet -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loader -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "隔离世界客户端 PID $($process.Id)；输出 $output"
$process.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $process.HasExited){throw "隔离世界超时；保留 PID $($process.Id)，等待正常退出，不强制终止。"}
$report=Join-Path $output 'world.json'
if(-not (Test-Path $report) -or (Get-Item $report).LastWriteTime -lt $started){throw '隔离世界未产生本轮验证报告。'}
$result=Get-Content $report -Raw | ConvertFrom-Json
if(-not $result.passed){throw '生产飞剑未完整经过三把剑的发射与命中验证。'}
Write-Output "世界夹具通过：真实AI、生产绘制、三把剑发射与命中；不代表多人测试。"
