param([int]$TimeoutSec=180,[string]$OutputName='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot 'artifacts/airflow-render-profile'
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
if(-not $OutputName){$OutputName="airflow-material-$version"}
$output=Join-Path $taskRoot "artifacts/$OutputName"
$loader='E:/SteamLibrary/steamapps/common/tModLoader'
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
Copy-Item -LiteralPath "$taskRoot/artifacts/swordbody-preview-profile/config.json" -Destination "$profile/config.json"
foreach($name in @('ZENITH_CHECK_REPORT','ZENITH_WOOD','ZENITH_SWORDBODY_PREVIEW','ZENITH_SLASH_RENDER','ZENITH_INGAME_PREVIEW','ZENITH_AIRFLOW_WORLD')){
    [Environment]::SetEnvironmentVariable($name,$null,'Process')
}
$env:ZENITH_AIRFLOW_MATERIAL=$output
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$started=Get-Date
$process=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loader -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "生产材质 GPU 客户端 PID $($process.Id)；输出 $output"
$process.WaitForExit($TimeoutSec*1000) | Out-Null
if(-not $process.HasExited){throw "GPU预览超时；保留 PID $($process.Id)，请正常退出。"}
$report=Join-Path $output 'gpu.json'
if(-not (Test-Path $report) -or (Get-Item $report).LastWriteTime -lt $started){throw '本轮没有生成 GPU 报告。'}
$result=Get-Content $report -Raw | ConvertFrom-Json
if(-not $result.passed){throw '生产材质 GPU 检查失败。'}
Write-Output '生产材质 GPU 检查通过。'
