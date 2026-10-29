param([int]$TimeoutSec=180)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$loaderRoot='E:/SteamLibrary/steamapps/common/tModLoader'
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$taskProfile=Join-Path $taskRoot "artifacts/liquid-glass-profile-$stamp"
$taskOutput=Join-Path $taskRoot "artifacts/liquid-glass-prototype/$stamp"
$taskCheck=Join-Path $taskRoot 'checks/ZenithPrototypeCheck'
New-Item -ItemType Directory -Force -Path "$taskProfile/Mods",$taskOutput | Out-Null
# 每次检查前只编译独立原型，确保打包时使用当前着色器源码对应的字节码。
$taskPython=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
if(-not(Test-Path $taskPython)){$taskPython=(Get-Command python -ErrorAction Stop).Source}
& $taskPython "$taskRoot/checks/compile_liquid_glass.py" *> "$taskOutput/shader-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskOutput/shader-build.log";throw '独立液态玻璃着色器编译失败'}
& $taskPython "$taskRoot/checks/compile_liquid_glass_sentinel.py" *> "$taskOutput/sentinel-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskOutput/sentinel-build.log";throw '独立固定色着色器编译失败'}
& $taskPython "$taskRoot/checks/compile_liquid_glass_parameter_probe.py" *> "$taskOutput/parameter-probe-build.log"
if($LASTEXITCODE -ne 0){Get-Content "$taskOutput/parameter-probe-build.log";throw '独立参数探针着色器编译失败'}
$taskShader=Join-Path $taskCheck 'Assets/LiquidGlass/LiquidGlass.fx'
$taskShaderBinary=Join-Path $taskCheck 'Assets/LiquidGlass/LiquidGlass.fxc'
@{sourceSha256=(Get-FileHash -LiteralPath $taskShader -Algorithm SHA256).Hash.ToLowerInvariant();
  compiledSha256=(Get-FileHash -LiteralPath $taskShaderBinary -Algorithm SHA256).Hash.ToLowerInvariant()} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskOutput/shader-hashes.json"
# 检查模组有核心依赖，只复制现有候选包供加载；不编译或安装生产模组。
Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/ZenithReborn.tmod" -Destination "$taskProfile/Mods/ZenithReborn.tmod"
Push-Location $loaderRoot
try{
    & './dotnet/dotnet.exe' ./tModLoader.dll -server -build $taskCheck -tmlsavedirectory $taskProfile *> "$taskOutput/check-build.log"
    if($LASTEXITCODE -ne 0){Get-Content "$taskOutput/check-build.log";throw '实验检查模组构建失败'}
}finally{Pop-Location}
Copy-Item -LiteralPath "$taskRoot/artifacts/soul-flight-world-0.3.104-full-spatial-final/before-refraction.png" -Destination "$taskProfile/day.png"
'[''ZenithReborn'',''ZenithPrototypeCheck'']'.Replace("'",'"') | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
@{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
  VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
  ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
$env:ZENITH_LIQUID_GLASS_RENDER=$taskOutput
$env:ZENITH_CLEAR_GLASS_RENDER='';$env:ZENITH_WOOD='';$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStart=[DateTime]::UtcNow
$taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
    -WorkingDirectory $loaderRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$taskOutput/stdout.log" -RedirectStandardError "$taskOutput/stderr.log"
Write-Output "液态玻璃独立GPU客户端 PID $($taskProcess.Id)，输出 $taskOutput"
while(-not $taskProcess.HasExited -and ([DateTime]::UtcNow-$taskStart).TotalSeconds -lt $TimeoutSec){$taskProcess.WaitForExit(1000) | Out-Null}
if(-not $taskProcess.HasExited){throw 'GPU检查超时，保留客户端等待正常退出'}
$taskReport="$taskOutput/liquid-glass.json"
if(-not(Test-Path $taskReport)){
    if(Test-Path "$taskOutput/error.txt"){Get-Content "$taskOutput/error.txt"};throw '未生成GPU检查报告'
}
if((Get-Item $taskReport).LastWriteTimeUtc -lt $taskStart){throw 'GPU报告属于旧回合'}
$taskResult=Get-Content $taskReport -Raw | ConvertFrom-Json
$taskResult | ConvertTo-Json
if(-not $taskResult.passed){throw 'GPU检查存在失败，请查看报告和图片'}
