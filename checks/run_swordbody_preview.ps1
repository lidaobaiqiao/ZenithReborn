param([string]$Revision='R2',[string]$Sword='TitaniumSword',[switch]$Emissive=$true,[switch]$Vector,[switch]$Local,[switch]$NoWait,[int]$TimeoutMs=300000,[switch]$Candidate)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$profile=Join-Path $taskRoot $(if($Candidate){'artifacts/swordbody-h3-profile'}else{'artifacts/swordbody-preview-profile'})
$output=Join-Path $taskRoot "artifacts/swordbody-$Sword-$Revision"
if($Emissive){$output+='-emissive'}
if($Local){$output+='-local'}
$library=Get-Content -LiteralPath (Join-Path $taskRoot 'VfxLibrary/library.json') -Raw | ConvertFrom-Json
$entry=$library.swords | Where-Object { $_.id -eq $Sword }
if(-not $entry){throw "Unknown sword $Sword in VfxLibrary/library.json"}
$itemName=($entry.source.key -split '/')[-1]
if($itemName -notlike 'ItemID.*'){throw "Sword $Sword has no vanilla ItemID source ($($entry.source.key)); autoFit needs the original sprite."}
$itemName=$itemName -replace '^ItemID\.',''
$recipeDir=Join-Path $taskRoot "VfxLibrary/Masks/SwordBody/$Sword/$Revision"
$loader='E:/SteamLibrary/steamapps/common/tModLoader'
$dotnet='C:/Program Files/dotnet/dotnet.exe'
New-Item -ItemType Directory -Force -Path "$profile/Mods",$output | Out-Null
& $dotnet build "$PSScriptRoot/ZenithPrototypeCheck/ZenithPrototypeCheck.csproj" -c Release --nologo *> "$output/build.log"
if($LASTEXITCODE -ne 0){Get-Content "$output/build.log";throw 'Preview build failed'}
Push-Location $loader
try {
    & $dotnet ./tModLoader.dll -server -build "$PSScriptRoot/ZenithPrototypeCheck" -eac "$PSScriptRoot/ZenithPrototypeCheck/bin/Release/net8.0/ZenithPrototypeCheck.dll" -tmlsavedirectory "$taskRoot/build-profile" *> "$output/pack.log"
    if($LASTEXITCODE -ne 0){Get-Content "$output/pack.log";throw 'Preview package failed'}
} finally {Pop-Location}
Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/ZenithPrototypeCheck.tmod" -Destination "$profile/Mods/ZenithPrototypeCheck.tmod"
$taskProduction=if($Candidate){Join-Path $taskRoot 'build-profile/Mods/ZenithReborn.tmod'}else{'C:/Users/15537/Documents/My Games/Terraria/tModLoader/Mods/ZenithReborn.tmod'}
Copy-Item -LiteralPath $taskProduction -Destination "$profile/Mods/ZenithReborn.tmod"
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$profile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1280;DisplayHeight=720;VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$profile/config.json"
$env:ZENITH_MASK_PREVIEW=''
$env:ZENITH_SWORDBODY_PREVIEW=$output
$env:ZENITH_SWORDBODY_ATLAS=Join-Path $recipeDir 'active.png'
$env:ZENITH_SWORDBODY_AUTHORING=if($Vector){Join-Path $recipeDir 'authoring.json'}else{''}
$env:ZENITH_SWORDBODY_REVISION=$Revision
$env:ZENITH_SWORDBODY_ITEM=$itemName
$env:ZENITH_SWORDBODY_LOCAL=if($Local){'1'}else{'0'}
$env:ZENITH_SWORDBODY_COLORFIELD=''
if(Test-Path -LiteralPath (Join-Path $recipeDir 'authoring.json')){
    $taskRecipe=Get-Content -LiteralPath (Join-Path $recipeDir 'authoring.json') -Raw | ConvertFrom-Json
    $taskFieldId=$taskRecipe.colorField.Split('.')
    $env:ZENITH_SWORDBODY_COLORFIELD=Join-Path $taskRoot "VfxLibrary/ColorFields/$($taskFieldId[1])/$($taskFieldId[2])/entry.json"
}
$env:ZENITH_SWORDBODY_EMISSIVE=if($Emissive){'1'}else{'0'}
$env:ZENITH_SLASH_RENDER=''
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$taskStarted=Get-Date
$process=Start-Process -FilePath $dotnet -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $profile)) -WorkingDirectory $loader -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/stdout.log" -RedirectStandardError "$output/stderr.log"
Write-Output "Isolated mask preview PID $($process.Id); sword $Sword ($itemName); output $output"
if($NoWait){return}
$process.WaitForExit($TimeoutMs) | Out-Null
if(-not $process.HasExited){throw "隔离预览超时，客户端 PID $($process.Id) 保留运行；请正常退出后重试。"}
$gpu=Join-Path $output 'gpu.json'
if((Test-Path -LiteralPath $gpu) -and (Get-Item -LiteralPath $gpu).LastWriteTime -ge $taskStarted){
    $report=Get-Content -LiteralPath $gpu -Raw | ConvertFrom-Json
    if(-not $report.passed){throw "GPU preview did not pass: $gpu"}
    Write-Output "GPU preview passed: $Sword/$Revision maskSha256=$($report.maskSha256)"
} else {throw 'Isolated GPU preview produced no gpu.json'}
