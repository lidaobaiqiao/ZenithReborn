param([int]$Take=3)
# Batch: generate R2-family autoFit recipes, GPU-export and register ore swords.
# Resumable: swords whose R2/clip.json already exists are skipped.
# Recovering: one GPU retry after killing the isolated client on hang or failure.
# Segmented: processes up to $Take unfinished swords per invocation so each run
# stays inside the foreground shell time limit.
$ErrorActionPreference='Continue'
$taskRoot='C:\Users\15537\Documents\ChatGPT\泰拉瑞亚'
Set-Location $taskRoot
$py='C:/Users/15537/.workbuddy/binaries/python/versions/3.13.12/python.exe'
$swords=@(
    'CopperBroadsword','TinShortsword','TinBroadsword','IronShortsword','IronBroadsword',
    'LeadShortsword','LeadBroadsword','SilverShortsword','SilverBroadsword','TungstenShortsword',
    'TungstenBroadsword','GoldShortsword','GoldBroadsword','PlatinumShortsword','PlatinumBroadsword',
    'CobaltSword','PalladiumSword','MythrilSword','OrichalcumSword','AdamantiteSword'
) | Where-Object { -not (Test-Path "VfxLibrary/Masks/SwordBody/$_/R2/clip.json") } | Select-Object -First $Take
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
function Invoke-Preview($sword,$dir,$out){
    $pwsh=(Get-Command pwsh -ErrorAction Stop).Source
    # 记录自己启动的子进程对象；退出码从对象读，收尾也只碰这个对象及其 PID 记录。
    $proc=Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile','-File','checks/run_swordbody_preview.ps1','-Revision','R2','-Sword',$sword,'-Vector','-Emissive','-Local','-TimeoutMs','150000') -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out/batch.log" -RedirectStandardError "$out/batch.err.log"
    $proc.WaitForExit()
    if($proc.ExitCode -ne 0){Stop-PreviewClient $out}
    return $proc.ExitCode -eq 0
}
$done=@()
foreach($s in $swords){
    $dir="VfxLibrary/Masks/SwordBody/$s/R2"
    $out="artifacts/swordbody-$s-R2-emissive-local"
    if(Test-Path "$dir/clip.json"){Write-Output "$s SKIP (already registered)";$done+="$s SKIP";continue}
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    if(-not (Test-Path "$dir/authoring.json")){
        & $py checks/make_autofit_recipe.py $dir | Out-Null
        if($LASTEXITCODE -ne 0){Write-Output "$s RECIPE-FAIL";$done+="$s RECIPE-FAIL";continue}
    }
    $ok=Invoke-Preview $s $dir $out
    if(-not $ok){
        Stop-PreviewClient $out
        Start-Sleep -Seconds 3
        $ok=Invoke-Preview $s $dir $out
    }
    if(-not $ok){Write-Output "$s GPU-FAIL";$done+="$s GPU-FAIL";continue}
    & $py checks/register_swordbody_vector.py $out $dir *> "$out/register.log"
    if($LASTEXITCODE -ne 0){Write-Output "$s REGISTER-FAIL";$done+="$s REGISTER-FAIL";continue}
    Write-Output "$s OK"
    $done+="$s OK"
}
& $py checks/vfx_library.py validate *> artifacts/swordbody-batch-validate.txt
Write-Output "BATCH DONE: $($done -join ', ')"
