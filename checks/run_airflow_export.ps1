param([int]$TimeoutSec=240)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot 'artifacts/airflow-author-profile'
$output=Join-Path $taskRoot 'artifacts/airflow-H4-export'
$loader='E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet='C:/Program Files/dotnet/dotnet.exe'
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
& $dotnet build "$taskRoot/checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj" -c Release --nologo *> "$output/build.log"
if($LASTEXITCODE -ne 0){Get-Content "$output/build.log" -Tail 15;throw '图集导出夹具编译失败。'}
Push-Location $loader
try{
    & $dotnet ./tModLoader.dll -server -build "$taskRoot/checks/ZenithPrototypeCheck" -eac "$taskRoot/checks/ZenithPrototypeCheck/bin/Release/net8.0/ZenithPrototypeCheck.dll" -tmlsavedirectory "$taskRoot/build-profile" *> "$output/pack.log"
    if($LASTEXITCODE -ne 0){throw '图集导出夹具打包失败。'}
}finally{Pop-Location}
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
Copy-Item -LiteralPath "$taskRoot/artifacts/swordbody-preview-profile/config.json" -Destination "$profile/config.json"
foreach($name in @('ZENITH_CHECK_REPORT','ZENITH_WOOD','ZENITH_SWORDBODY_PREVIEW','ZENITH_SLASH_RENDER','ZENITH_INGAME_PREVIEW','ZENITH_AIRFLOW_WORLD','ZENITH_AIRFLOW_MATERIAL')){
    [Environment]::SetEnvironmentVariable($name,$null,'Process')
}
$env:ZENITH_AIRFLOW_EXPORT=$output
$env:ZENITH_AIRFLOW_RECIPES=Join-Path $taskRoot 'VfxLibrary/Masks/SwordBody'
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$started=Get-Date
$process=Start-Process -FilePath $dotnet -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loader -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "整组图集客户端 PID $($process.Id)；输出 $output"
$process.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $process.HasExited){throw "整组导出超时；保留 PID $($process.Id)，等待正常退出。"}
if(-not (Test-Path "$output/complete.txt") -or (Get-Item "$output/complete.txt").LastWriteTime -lt $started){throw '本轮未完成图集导出，请检查 error.txt。'}
Write-Output '21把图集已在同一个隔离GPU客户端中导出。'
