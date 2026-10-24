param([switch]$WithoutThirdParty)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $taskRoot 'artifacts/soul-trail-render-profile'
$version = ((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$output = Join-Path $taskRoot "artifacts/soul-trail-gpu-$version"
if($WithoutThirdParty){$profile+='-no-thirdparty';$output+='-no-thirdparty'}
$loaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
New-Item -ItemType Directory -Force -Path "$profile/Mods", $output | Out-Null
foreach ($name in @('ZenithReborn', 'ZenithPrototypeCheck')) {
    Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$name.tmod" -Destination "$profile/Mods/$name.tmod"
}
if($WithoutThirdParty){
    # 只生成隔离包副本：没有第三方材质文件，但使用同一份生产DLL。
    $copyRoot=Join-Path $taskRoot ("artifacts/own-effect-isolation-"+[Guid]::NewGuid().ToString('N'))
    $copySource=Join-Path $copyRoot 'ZenithReborn'
    $original=Join-Path $taskRoot 'ZenithReborn'
    foreach($file in Get-ChildItem -LiteralPath $original -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($original,$file.FullName)
        if($relative -match '^(bin|obj)[\\/]' -or $relative -match '^Assets[\\/]ThirdParty[\\/]'){continue}
        $destination=Join-Path $copySource $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    Push-Location $loaderRoot
    try{
        & 'C:/Program Files/dotnet/dotnet.exe' ./tModLoader.dll -server -build $copySource `
            -eac "$original/bin/Release/net8.0/ZenithReborn.dll" -tmlsavedirectory "$copyRoot/build" *> "$output/isolated-pack.log"
        if($LASTEXITCODE -ne 0){Get-Content "$output/isolated-pack.log";throw '无第三方资源的隔离包构建失败'}
    }finally{Pop-Location}
    Copy-Item -LiteralPath "$copyRoot/build/Mods/ZenithReborn.tmod" -Destination "$profile/Mods/ZenithReborn.tmod"
}
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
# 菜单夹具使用英文，避开本机加载中中文计数格式偶发的原版异常；正式语言不变。
$config = @{Language='en-US';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true}
$config | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_SOUL_TRAIL_RENDER = $output
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN = '0'
$process = Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loaderRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/client-stdout.log" -RedirectStandardError "$output/client-stderr.log"
Write-Output "Menu-only GPU render client started: PID $($process.Id); output: $output"
