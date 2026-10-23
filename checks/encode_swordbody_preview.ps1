param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskEncoder=Get-ChildItem "$taskRoot/artifacts/preview-tools/imageio_ffmpeg/binaries" -Filter '*.exe' | Select-Object -First 1 -ExpandProperty FullName
if(!$taskEncoder){throw 'Preview encoder not found'}
foreach($taskClip in @('breakdown','loop-context','lifecycle','motion','mask-only')){
    $taskFrames=Join-Path $Directory "$taskClip/frame-%03d.png"
    if(!(Test-Path (Join-Path $Directory "$taskClip/frame-000.png"))){continue}
    & $taskEncoder -y -framerate 60 -start_number 0 -i $taskFrames -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart (Join-Path $Directory "$taskClip.mp4") 2> (Join-Path $Directory "$taskClip-encode.log")
    if($LASTEXITCODE -ne 0){throw "Encode failed: $taskClip"}
}
if(!(Test-Path (Join-Path $Directory 'mask-only.mp4'))){
    & $taskEncoder -y -framerate 60 -start_number 0 -i (Join-Path $Directory 'breakdown/frame-%03d.png') -vf 'crop=448:256:496:92' -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart (Join-Path $Directory 'mask-only.mp4') 2> (Join-Path $Directory 'mask-only-encode.log')
    if($LASTEXITCODE -ne 0){throw 'Mask encode failed'}
}
Get-ChildItem $Directory -Filter '*.mp4' | Select-Object Name,Length
