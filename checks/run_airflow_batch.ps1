param([int]$Take=21)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
$python='C:/Users/15537/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
# 每次只启动并等待一个自有隔离客户端，不枚举或终止其他 dotnet／游戏进程。
$recipes=Get-ChildItem -Path 'VfxLibrary/Masks/SwordBody/*/H3/authoring.json' | Where-Object {-not (Test-Path (Join-Path $_.Directory.FullName 'clip.json'))} | Select-Object -First $Take
foreach($recipe in $recipes){
    $sword=$recipe.Directory.Parent.Name
    & pwsh -NoProfile -File checks/run_swordbody_preview.ps1 -Sword $sword -Revision H3 -Vector -Local -Candidate
    if($LASTEXITCODE -ne 0){throw "导出失败：$sword"}
    & $python checks/register_swordbody_vector.py "artifacts/swordbody-$sword-H3-emissive-local" "VfxLibrary/Masks/SwordBody/$sword/H3"
    if($LASTEXITCODE -ne 0){throw "登记失败：$sword"}
    Write-Output "H3 完成：$sword"
}
