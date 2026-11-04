param([switch]$SkipCoreChecks)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
# 先准备正式渲染核心与基础回归，再构建独立制作模组；不写正式Mods目录。
if(-not $SkipCoreChecks){
    & "$PSScriptRoot/run_wood_checks.ps1"
    if($LASTEXITCODE -ne 0){throw '正式核心构建失败'}
}
$source=Join-Path $taskRoot 'tools/ZenithMaterialLab'
# 打包走独立目录：写 build-profile 会刷新正式包 mtime，破坏 package_install 的时序断言。
# 下游（run_material_lab.ps1、package_install.py、check_glass_r3_delivery.py）统一读这个目录。
$labBuildDir=Join-Path $taskRoot 'material-lab-build-profile'
New-Item -ItemType Directory -Force -Path "$labBuildDir/Mods" | Out-Null
# 制作模组声明了 modReferences，打包前需要主模组包；复制（不移动）正式包，不刷新其 mtime。
foreach($name in @('ZenithReborn','Luminance')){
    $dependency="$taskRoot/build-profile/Mods/$name.tmod"
    if(Test-Path -LiteralPath $dependency){Copy-Item -LiteralPath $dependency -Destination "$labBuildDir/Mods/$name.tmod" -Force}
}
Push-Location 'E:/SteamLibrary/steamapps/common/tModLoader'
try{
    & './dotnet/dotnet.exe' ./tModLoader.dll -server -build $source -tmlsavedirectory "$labBuildDir" *> "$taskRoot/artifacts/material-lab-pack.log"
    if($LASTEXITCODE -ne 0){Get-Content "$taskRoot/artifacts/material-lab-pack.log";throw '制作模组打包失败'}
}finally{Pop-Location}
Write-Output '材质核心与独立制作模组已构建；正式安装未动。'
