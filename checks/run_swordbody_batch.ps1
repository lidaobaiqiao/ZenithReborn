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
function Invoke-Preview($sword,$dir,$out){
    pwsh -NoProfile -File checks/run_swordbody_preview.ps1 -Revision R2 -Sword $sword -Vector -Emissive -Local -TimeoutMs 150000 *> "$out/batch.log"
    return $LASTEXITCODE -eq 0
}
$done=@()
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | Stop-Process -Force -ErrorAction SilentlyContinue
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
        Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | Stop-Process -Force -ErrorAction SilentlyContinue
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
