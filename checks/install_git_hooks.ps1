# 安装仓库 git 钩子：把 checks/hooks/pre-commit 复制到 .git/hooks/pre-commit。
# 钩子不入库（.git 不跟踪），新克隆的仓库或换了 git 目录后要重跑一次本脚本。
# 用法：pwsh -File checks/install_git_hooks.ps1
#       pwsh -File checks/install_git_hooks.ps1 -PythonPath C:\path\to\python.exe
# 安装时会探测可用解释器并写入 .git/hooks/python-path，钩子据此运行，无需每次提交都配 PATH。
param(
    [string]$PythonPath
)
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $PSScriptRoot 'hooks/pre-commit'
$hookDir = Join-Path $repoRoot '.git/hooks'
$target = Join-Path $hookDir 'pre-commit'

if (-not (Test-Path -LiteralPath $source)) { throw "钩子模板缺失：$source" }
if (-not (Test-Path -LiteralPath $hookDir)) { throw "找不到 .git/hooks，$repoRoot 可能不是 git 仓库根" }

Copy-Item -LiteralPath $source -Destination $target -Force
# Git for Windows 用自带的 sh 执行钩子，读 shebang 即可，NTFS 上的权限位不参与判断。
Write-Host "[hooks] 已安装 pre-commit → $target"
Write-Host "[hooks] 校验内容：python checks/map_freshness.py（索引新鲜度、文件:行号失效、版本号与计数一致性）"

# 安装时顺手探测一次解释器：换机器最常见的坑是 PATH 命中 Windows 商店别名，
# 钩子旧版会因此把「Python 跑不起来」误报成「地图过期」，这里提前把结论打出来。
# 探测口径与钩子一致，候选里不写死任何本机绝对路径。
$candidates = @(
    $PythonPath
    $env:ZR_PYTHON
    (Join-Path $HOME '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
)
foreach ($wbpy in Get-ChildItem -Path (Join-Path $HOME '.workbuddy/binaries/python/versions/*/python.exe') -ErrorAction SilentlyContinue) {
    $candidates += $wbpy.FullName
}
foreach ($name in @('python', 'python3')) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
}
$python = $null
foreach ($candidate in $candidates) {
    if (-not $candidate) { continue }
    try {
        & $candidate -c 'import sys; sys.exit(0 if sys.version_info >= (3, 8) else 1)' | Out-Null
        if ($LASTEXITCODE -eq 0) { $python = $candidate; break }
    } catch {
        # 候选不存在或无法启动：跳过，交给下一个候选。
    }
}
$pinPath = Join-Path $hookDir 'python-path'
if ($python) {
    # 钩子由 Git for Windows 的 sh 执行：写成正斜杠路径，避免反斜杠被当成转义符。
    $pinned = ($python -replace '\\', '/')
    [System.IO.File]::WriteAllText($pinPath, $pinned + "`n", (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "[hooks] 可用解释器：$python"
    Write-Host "[hooks] 已锁定到 .git/hooks/python-path（钩子优先读取，无需每次提交配 PATH）"
} else {
    if (Test-Path -LiteralPath $pinPath) { Remove-Item -LiteralPath $pinPath -Force }
    Write-Host "[hooks] 警告：本机未探测到可运行的 Python 3.8+，提交会被钩子阻止。" -ForegroundColor Yellow
    Write-Host "[hooks]        请用 -PythonPath 指定 python.exe，或设置 ZR_PYTHON，或把可用 Python 放到 PATH 前面。"
}
