param([string]$Name='final')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$version=((Get-Content "$taskRoot/ZenithReborn/build.txt" | Where-Object {$_ -match '^version\s*='}) -split '=',2)[1].Trim()
$uiProfile=Join-Path $taskRoot "artifacts/information-ui-profile-$Name"
$output=Join-Path $taskRoot "artifacts/information-ui-$version/$Name"
New-Item -ItemType Directory -Force -Path "$uiProfile/Mods",$output | Out-Null
# 同名复跑必须清除旧完成标记，避免启动后立即误读旧报告并结束新客户端。
foreach($marker in @('gpu.json','failure.txt'))
{
    $markerPath=Join-Path $output $marker
    if(Test-Path -LiteralPath $markerPath){Remove-Item -LiteralPath $markerPath -Force}
}
foreach($mod in @('ZenithReborn','ZenithPrototypeCheck'))
{Copy-Item -LiteralPath "$taskRoot/build-profile/Mods/$mod.tmod" -Destination "$uiProfile/Mods/$mod.tmod"}
$expectedPackageHash=(Get-FileHash -LiteralPath "$uiProfile/Mods/ZenithReborn.tmod" -Algorithm SHA256).Hash.ToLowerInvariant()
'["ZenithReborn","ZenithPrototypeCheck"]' | Set-Content -Encoding utf8 -LiteralPath "$uiProfile/Mods/enabled.json"
@{Language='zh-Hans';Fullscreen=$false;WindowBorderless=$false;DisplayWidth=1440;DisplayHeight=900;UIScale=1;
VolumeMusic=0;VolumeSound=0;VolumeAmbient=0;AutoSave=$false;LastLaunchedVersion=279;LastLaunchedTModLoaderVersion='2026.8.3.0';SeenFirstLaunchModderWelcomeMessage=$true} |
ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath "$uiProfile/config.json"
$env:ZENITH_INFORMATION_GPU=$output
$env:SDL_WINDOW_ACTIVATE_WHEN_SHOWN='0'
$uiProcess=Start-Process -FilePath 'C:/Program Files/dotnet/dotnet.exe' -ArgumentList @('tModLoader.dll','-nosteam','-tmlsavedirectory',('"{0}"' -f $uiProfile)) -WorkingDirectory 'E:/SteamLibrary/steamapps/common/tModLoader' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output/client-stdout.log" -RedirectStandardError "$output/client-stderr.log"
$uiProcess.Id | Set-Content -LiteralPath "$output/client.pid"
Write-Output "Production information UI capture started: PID $($uiProcess.Id); output: $output"
# 仅清理本脚本启动的独立图形客户端；原生退出处理偶尔会在写完报告后继续持有资源。
$captureDeadline=[DateTime]::UtcNow.AddSeconds(50)
while(-not(Test-Path -LiteralPath "$output/gpu.json") -and -not(Test-Path -LiteralPath "$output/failure.txt") -and -not $uiProcess.HasExited -and [DateTime]::UtcNow -lt $captureDeadline)
{Start-Sleep -Milliseconds 250}
if(-not $uiProcess.HasExited)
{
    $null=$uiProcess.CloseMainWindow()
    if(-not $uiProcess.WaitForExit(2000)){Stop-Process -Id $uiProcess.Id -Force}
}
if(Test-Path -LiteralPath "$output/failure.txt"){Get-Content -LiteralPath "$output/failure.txt";throw '信息页绘制失败'}
if(-not(Test-Path -LiteralPath "$output/gpu.json")){throw '信息页绘制超时或未生成报告'}
$captureReport=Get-Content -LiteralPath "$output/gpu.json" -Raw -Encoding UTF8 | ConvertFrom-Json
if(-not $captureReport.passed){throw '信息页绘制或模拟输入检查未通过'}
if($captureReport.testedPackageSha256 -ne $expectedPackageHash){throw '图形报告没有绑定本次复制的模组包'}
Write-Output 'Production UI capture passed; isolated client cleaned up.'
