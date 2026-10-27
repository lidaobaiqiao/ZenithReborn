param([int]$TimeoutSec=180)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskVersion=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$taskOutput=Join-Path $taskRoot "artifacts/pure-code-vfx-$taskVersion"
$taskPython='C:/Users/15537/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $taskPython "$PSScriptRoot/prepare_pure_code_package.py"
if($LASTEXITCODE -ne 0){throw '隔离素材包准备失败'}
$env:ZENITH_SOUL_TRAIL_RENDER=''
$env:ZENITH_MATERIAL_LAB_CHECK=''
$env:ZENITH_MATERIAL_LAB_ARTCHECK=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
foreach($taskCase in @('with-assets','without-vfx-assets')){
    $taskProfile=Join-Path $taskRoot "artifacts/pure-code-vfx-profile-$taskVersion-$taskCase"
    $taskCaseOutput=Join-Path $taskOutput $taskCase
    New-Item -ItemType Directory -Force -Path "$taskProfile/Mods",$taskCaseOutput | Out-Null
    Copy-Item -LiteralPath "$taskOutput/ZenithReborn-$taskCase.tmod" -Destination "$taskProfile/Mods/ZenithReborn.tmod"
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/ZenithPrototypeCheck.tmod" -Destination "$taskProfile/Mods/ZenithPrototypeCheck.tmod"
    '["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 "$taskProfile/Mods/enabled.json"
    @{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;AutoSave=$false;
      VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
      ConvertTo-Json | Set-Content -Encoding utf8 "$taskProfile/config.json"
    $env:ZENITH_PURE_CODE_RENDER=$taskCaseOutput
    $taskStarted=[DateTime]::UtcNow
    $taskProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $taskProfile)) `
        -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput "$taskCaseOutput/stdout.log" -RedirectStandardError "$taskCaseOutput/stderr.log"
    Write-Output "$taskCase GPU PID $($taskProcess.Id)"
    $taskProcess.WaitForExit($TimeoutSec*1000) | Out-Null
    if(-not $taskProcess.HasExited){throw 'GPU检查超时，保留进程等待正常退出，不强杀'}
    $taskReport=Join-Path $taskCaseOutput 'pure-code.json'
    if(-not(Test-Path $taskReport)){if(Test-Path "$taskCaseOutput/error.txt"){Get-Content "$taskCaseOutput/error.txt"};throw '没有生成本轮GPU记录'}
    if((Get-Item $taskReport).LastWriteTimeUtc -lt $taskStarted){throw 'GPU记录属于旧运行'}
    $taskResult=Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
    if(-not $taskResult.passed){$taskResult.checks | ConvertTo-Json;throw '纯代码GPU专项存在失败'}
    Write-Output "$taskCase 资源及GPU检查通过"
}
$taskFull=Get-Content "$taskOutput/with-assets/pure-code.json" -Raw | ConvertFrom-Json
$taskStripped=Get-Content "$taskOutput/without-vfx-assets/pure-code.json" -Raw | ConvertFrom-Json
$taskSame=$true
foreach($taskProperty in $taskFull.hashes.PSObject.Properties){
    if($taskStripped.hashes.($taskProperty.Name) -ne $taskProperty.Value){$taskSame=$false}
}
@{passed=$taskSame;actualGpu=$true;sameDll=$true;comparedImages=@($taskFull.hashes.PSObject.Properties).Count;
  fullPackageSha256=$taskFull.testedPackageSha256;strippedPackageSha256=$taskStripped.testedPackageSha256;
  worldCombatTested=$false;artApproved=$false} | ConvertTo-Json | Set-Content -Encoding utf8 "$taskOutput/independence.json"
if(-not $taskSame){throw '移除素材后实际GPU像素发生变化'}
Write-Output '完整包与移除素材包：所有专项图像逐像素一致。'
