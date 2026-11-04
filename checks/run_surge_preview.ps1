# Rebuild both mods from current sources, pack them into the isolated render profile
# and run the menu-only controlled GPU replay (OreRenderCheck via MartialSlashRenderCheck).
param([int]$TimeoutSec=300)
$ErrorActionPreference='Stop'
$taskRoot='C:\Users\15537\Documents\ChatGPT\泰拉瑞亚'
Set-Location $taskRoot
$loader='E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet='C:/Program Files/dotnet/dotnet.exe'
$profile=Join-Path $taskRoot 'artifacts/slash-render-profile'
# 打包走独立目录：写 build-profile 会刷新正式包 mtime，破坏 package_install 的时序断言。
$buildDir=Join-Path $taskRoot 'surge-preview-profile'
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$output=Join-Path $taskRoot "artifacts/surge-gpu-$version"
New-Item -ItemType Directory -Force -Path "$profile/Mods","$buildDir/Mods",$output | Out-Null

& $dotnet build "$taskRoot/ZenithReborn/ZenithReborn.csproj" -c Release --nologo *> "$output/build-production.log"
if($LASTEXITCODE -ne 0){Get-Content "$output/build-production.log" -Tail 20;throw 'Production build failed'}
& $dotnet build "$taskRoot/checks/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj" -c Release --nologo *> "$output/build-check.log"
if($LASTEXITCODE -ne 0){Get-Content "$output/build-check.log" -Tail 20;throw 'Check build failed'}

Push-Location $loader
try {
    & $dotnet ./tModLoader.dll -server -build "$taskRoot/ZenithReborn" -eac "$taskRoot/ZenithReborn/bin/Release/net8.0/ZenithReborn.dll" -tmlsavedirectory "$buildDir" *> "$output/pack-production.log"
    if($LASTEXITCODE -ne 0){Get-Content "$output/pack-production.log" -Tail 15;throw 'Production pack failed'}
    & $dotnet ./tModLoader.dll -server -build "$taskRoot/checks/ZenithPrototypeCheck" -eac "$taskRoot/checks/ZenithPrototypeCheck/bin/Release/net8.0/ZenithPrototypeCheck.dll" -tmlsavedirectory "$buildDir" *> "$output/pack-check.log"
    if($LASTEXITCODE -ne 0){Get-Content "$output/pack-check.log" -Tail 15;throw 'Check pack failed'}
} finally {Pop-Location}

foreach($name in @('ZenithReborn','ZenithPrototypeCheck')){
    Copy-Item -LiteralPath "$buildDir/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_SLASH_RENDER=$output
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$process=Start-Process -FilePath $dotnet -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loader -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/client-stdout.log" -RedirectStandardError "$output/client-stderr.log"
Write-Output "render client PID $($process.Id); output $output"
for($i=0;$i -lt $TimeoutSec;$i++){
    Start-Sleep -Seconds 1
    if(Test-Path "$output/preview.json"){break}
    if($process.HasExited -and -not (Test-Path "$output/preview.json")){break}
}
if(Test-Path "$output/preview.json"){
    Write-Output "GPU replay done: $output"
} else {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "GPU replay produced no preview.json (see $output/client-stdout.log)"
}
