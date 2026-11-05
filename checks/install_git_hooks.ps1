# 安装仓库 git 钩子：把 checks/hooks/pre-commit 复制到 .git/hooks/pre-commit。
# 钩子不入库（.git 不跟踪），新克隆的仓库或换了 git 目录后要重跑一次本脚本。
# 用法：pwsh -File checks/install_git_hooks.ps1
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
