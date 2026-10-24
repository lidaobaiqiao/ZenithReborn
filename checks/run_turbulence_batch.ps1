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

$done=@()
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | Stop-Process -Force -ErrorAction SilentlyContinue
foreach($s in $swords){
    Write-Output "=== $s ==="
    & pwsh -NoProfile -File checks/run_swordbody_preview.ps1 -Revision H2 -Sword $s -Vector -Emissive -Local
    if($LASTEXITCODE -eq 0){
        & $py checks/register_swordbody_vector.py "artifacts/swordbody-$s-H2-emissive-local" "VfxLibrary/Masks/SwordBody/$s/H2"
        if($LASTEXITCODE -eq 0){Write-Output "$s OK";$done+="$s OK"}
        else{Write-Output "$s REGISTER-FAIL";$done+="$s REGISTER-FAIL"}
    } else {
        Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | Stop-Process -Force -ErrorAction SilentlyContinue
        Write-Output "$s EXPORT-FAIL";$done+="$s EXPORT-FAIL"
    }
}
Write-Output "BATCH DONE: $($done -join ', ')"
