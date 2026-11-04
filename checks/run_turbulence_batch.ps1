param([int]$Take=5)
# 批量导出 H2 湍流蒙版：断点续跑，每段处理 N 把，导出成功后立即注册。
# 用法：pwsh -NoProfile -File checks/run_turbulence_batch.ps1 -Take 5
$ErrorActionPreference='Continue'
$taskRoot='C:\Users\15537\Documents\ChatGPT\泰拉瑞亚'
Set-Location $taskRoot
$py='C:/Users/15537/.workbuddy/binaries/python/versions/3.13.12/python.exe'
$swords=@(
    'CopperShortsword','CopperBroadsword','TinShortsword','TinBroadsword','IronShortsword','IronBroadsword',
    'LeadShortsword','LeadBroadsword','SilverShortsword','SilverBroadsword','TungstenShortsword','TungstenBroadsword',
    'GoldShortsword','GoldBroadsword','PlatinumShortsword','PlatinumBroadsword',
    'CobaltSword','PalladiumSword','MythrilSword','OrichalcumSword','AdamantiteSword'
) | Where-Object { -not (Test-Path "VfxLibrary/Masks/SwordBody/$_/H2/clip.json") } | Select-Object -First $Take

# 只终止本轮预览脚本自己记录下来的客户端 PID（run_swordbody_preview.ps1 写出的 client.pid）。
# 项目硬规则禁止强杀游戏/进程：拿不到自己的进程对象或 PID 记录时，什么也不做，绝不按名字扫全表。
function Stop-PreviewClient([string]$out){
    $pidFile=Join-Path $out 'client.pid'
    if(-not (Test-Path -LiteralPath $pidFile)){return}
    $clientId=0
    if(-not [int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(),[ref]$clientId)){
        Remove-Item -LiteralPath $pidFile -ErrorAction SilentlyContinue
        return
    }
    $client=Get-Process -Id $clientId -ErrorAction SilentlyContinue
    if($client -and $client.ProcessName -eq 'dotnet'){
        Write-Output "清理本轮客户端 PID $clientId"
        Stop-Process -Id $clientId -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $pidFile -ErrorAction SilentlyContinue
}

$done=@()
foreach($s in $swords){
    Write-Output "=== $s ==="
    $out="artifacts/swordbody-$s-H2-emissive-local"
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $pwsh=(Get-Command pwsh -ErrorAction Stop).Source
    # 记录自己启动的子进程对象；退出码从对象读，收尾也只碰这个对象及其 PID 记录。
    $proc=Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile','-File','checks/run_swordbody_preview.ps1','-Revision','H2','-Sword',$s,'-Vector','-Emissive','-Local') -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out/batch.log" -RedirectStandardError "$out/batch.err.log"
    $proc.WaitForExit()
    if($proc.ExitCode -eq 0){
        & $py checks/register_swordbody_vector.py $out "VfxLibrary/Masks/SwordBody/$s/H2"
        if($LASTEXITCODE -eq 0){Write-Output "$s OK";$done+="$s OK"}
        else{Write-Output "$s REGISTER-FAIL";$done+="$s REGISTER-FAIL"}
    } else {
        Stop-PreviewClient $out
        Write-Output "$s EXPORT-FAIL";$done+="$s EXPORT-FAIL"
    }
}
Write-Output "BATCH DONE: $($done -join ', ')"
