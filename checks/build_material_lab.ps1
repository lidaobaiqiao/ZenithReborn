param([switch]$SkipCoreChecks)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
# 先准备正式渲染核心与基础回归，再构建独立制作模组；不写正式Mods目录。
if(-not $SkipCoreChecks){
    & "$PSScriptRoot/run_wood_checks.ps1"
    if($LASTEXITCODE -ne 0){throw '正式核心构建失败'}
}
$source=Join-Path $taskRoot 'tools/ZenithMaterialLab'
Push-Location 'E:/SteamLibrary/steamapps/common/tModLoader'
try{
    & './dotnet/dotnet.exe' ./tModLoader.dll -server -build $source -tmlsavedirectory "$taskRoot/build-profile" *> "$taskRoot/artifacts/material-lab-pack.log"
    if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/artifacts/material-lab-pack.log";throw '制作模组打包失败'}
}finally{Pop-Location}
Write-Output '材质核心与独立制作模组已构建；正式安装未动。'
