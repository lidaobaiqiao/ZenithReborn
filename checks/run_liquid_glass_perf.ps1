param([int]$TimeoutSec=180)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$loaderRoot='E:/SteamLibrary/steamapps/common/tModLoader'
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$taskProfile=Join-Path $taskRoot "artifacts/liquid-perf-profile-$stamp"
$taskOutput=Join-Path $taskRoot "artifacts/liquid-glass-performance/$stamp"
$taskCheck=Join-Path $taskRoot 'checks/ZenithPrototypeCheck'
New-Item -ItemType Directory -Force -Path "$taskProfile/Mods",$taskOutput | Out-Null
$taskPython=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
if(-not(Test-Path $taskPython)){$taskPython=(Get-Command python -ErrorAction Stop).Source}
foreach($taskCompiler in @('compile_liquid_glass_trail.py','compile_liquid_glass_perf.py')){
    & $taskPython "$taskRoot/checks/$taskCompiler" *> "$taskOutput/$taskCompiler.log"
    if($LASTEXITCODE -ne 0){Get-Content "$taskOutput/$taskCompiler.log";throw '性能对照着色器编译失败'}
}
$taskShaderHashes=@{}
foreach($taskName in @('LiquidGlassTrail.fx','LiquidGlassTrail.fxc','LiquidGlassPerfBase.fx','LiquidGlassPerfBase.fxc')){
    $taskShaderHashes[$taskName]=(Get-FileHash -LiteralPath "$taskCheck/Assets/LiquidGlass/$taskName" -Algorithm SHA256).Hash.ToLowerInvariant()
}
$taskShaderHashes | ConvertTo-Json | Set-Content -Encoding utf8 "$taskOutput/shader-hashes.json"
# 依赖包只提供刀光几何，整个测试不构建或安装生产模组。
Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/ZenithReborn.tmod" -Destination "$taskProfile/Mods/ZenithReborn.tmod"
Push-Location $loaderRoot
try{
    & './dotnet/dotnet.exe' ./tModLoader.dll -server -build $taskCheck -tmlsavedirectory $taskProfile *> "$taskOutput/check-build.log"
    if($LASTEXITCODE -ne 0){Get-Content "$taskOutput/check-build.log";throw '性能检查模组构建失败'}
}finally{Pop-Location}
'[''ZenithReborn'',''ZenithPrototypeCheck'']'.Replace("'",'"') | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
$env:ZENITH_LIQUID_PERF_RENDER=$taskOutput
$env:ZENITH_LIQUID_TRAIL_RENDER='';$env:ZENITH_LIQUID_GLASS_RENDER='';$env:ZENITH_CLEAR_GLASS_RENDER='';$env:ZENITH_WOOD='';$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStart=[DateTime]::UtcNow
$taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
    -WorkingDirectory $loaderRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$taskOutput/stdout.log" -RedirectStandardError "$taskOutput/stderr.log"
Write-Output "独立刀光 GPU 计时客户端 PID $($taskProcess.Id)，输出 $taskOutput"
while(-not $taskProcess.HasExited -and ([DateTime]::UtcNow-$taskStart).TotalSeconds -lt $TimeoutSec){$taskProcess.WaitForExit(1000) | Out-Null}
if(-not $taskProcess.HasExited){throw 'GPU 计时超时，保留客户端等待正常退出'}
$taskReport="$taskOutput/liquid-performance.json"
if(-not(Test-Path $taskReport)){
    if(Test-Path "$taskOutput/error.txt"){Get-Content "$taskOutput/error.txt"}
    if((Test-Path "$taskOutput/stdout.log") -and (Select-String -LiteralPath "$taskOutput/stdout.log" -SimpleMatch 'Please ensure Steam is logged in and running.' -Quiet)){
        throw "tModLoader 未通过 Steam 登录校验，未进入 GPU 计时；日志：$taskOutput/stdout.log"
    }
    throw "未生成 GPU 计时报告；请查看 $taskOutput 中的日志"
}
if((Get-Item $taskReport).LastWriteTimeUtc -lt $taskStart){throw 'GPU 计时报告属于旧回合'}
$taskResult=Get-Content $taskReport -Raw | ConvertFrom-Json
$taskResult | ConvertTo-Json -Depth 6
if(-not $taskResult.passed){throw 'GPU 计时未通过，请查看报告和日志'}
