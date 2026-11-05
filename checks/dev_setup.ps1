<#
.SYNOPSIS
    一键本机配置：新开发者克隆仓库后跑这一条命令即可开始开发。

.DESCRIPTION
    tModLoader 安装目录解析优先级：
      -LoaderRoot 参数 > 环境变量 ZENITH_TML_ROOT > 仓库根 local.props 的 TmlRoot > 默认值。
    脚本可重复运行：local.props 内容一致时不重写，git 钩子为覆盖式安装。

.PARAMETER LoaderRoot
    tModLoader 安装根目录（该目录下应有 tMLMod.targets），优先级最高。

.PARAMETER SkipBuild
    只做配置，不编译模组。

.PARAMETER SkipLuminance
    跳过 Luminance 依赖准备（已有 artifacts/dependencies/Luminance 时可跳过）。

.EXAMPLE
    pwsh -File checks/dev_setup.ps1

.EXAMPLE
    pwsh -File checks/dev_setup.ps1 -LoaderRoot D:/Games/tModLoader -SkipBuild
#>
[CmdletBinding()]
param(
    [string]$LoaderRoot,
    [switch]$SkipBuild,
    [switch]$SkipLuminance
)

$ErrorActionPreference = 'Stop'

# 默认安装路径只是兜底值，其他机器一律用参数、环境变量或 local.props 覆盖。
$defaultLoaderRoot = 'E:/SteamLibrary/steamapps/common/tModLoader'
$taskRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $taskRoot 'local.props'

function Write-Setup([string]$Message) {
    Write-Host "[setup] $Message"
}

# 把用户给的路径规范化成绝对路径 + 正斜杠，避免 MSBuild 与脚本混用两种分隔符。
# 绝对路径直接用 .NET 展开：即使盘符不存在也能拿到完整路径，交给后面校验步骤给出准确提示。
function ConvertTo-SetupPath([string]$InputPath) {
    try {
        if ([System.IO.Path]::IsPathRooted($InputPath)) {
            $full = [System.IO.Path]::GetFullPath($InputPath)
        } else {
            $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InputPath)
        }
    } catch {
        throw "路径无法解析：$InputPath。请检查是否包含非法字符，或改用完整路径。"
    }
    $normalized = $full.TrimEnd('\', '/') -replace '\\', '/'
    # 盘符根目录（如 D:/）去掉尾部分隔符会变成 D:，补回来。
    if ($normalized -match '^[A-Za-z]:$') { $normalized = "$normalized/" }
    return $normalized
}

# 读取已存在的 local.props 里的 TmlRoot；文件损坏时只警告，不中断配置流程。
function Get-LocalPropsTmlRoot([string]$Path) {
    try {
        [xml]$doc = Get-Content -LiteralPath $Path -Raw
    } catch {
        Write-Warning "local.props 无法解析（$($_.Exception.Message)），本次按默认路径处理并重新生成该文件。"
        return $null
    }
    $groups = @($doc.Project.PropertyGroup)
    for ($i = $groups.Count - 1; $i -ge 0; $i--) {
        $value = $groups[$i].TmlRoot
        if ($value -and "$value".Trim()) { return "$value".Trim() }
    }
    return $null
}

# 生成 local.props 文本；内容固定，重复运行才能比对出“无需改写”。
function New-LocalPropsContent([string]$TmlRootValue) {
    $escaped = $TmlRootValue.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
    $lines = @(
        '<Project>'
        '  <PropertyGroup>'
        "    <TmlRoot>$escaped</TmlRoot>"
        '  </PropertyGroup>'
        '</Project>'
    )
    return ($lines -join "`n") + "`n"
}

# 步骤 1：仓库根 = checks/ 的上一级。
Write-Setup "1/7 仓库根：$taskRoot"

# 步骤 2：按优先级确定 tModLoader 安装目录。
$loaderSource = ''
if ($LoaderRoot) {
    $tmlRoot = ConvertTo-SetupPath $LoaderRoot
    $loaderSource = '-LoaderRoot 参数'
} elseif ($env:ZENITH_TML_ROOT) {
    $tmlRoot = ConvertTo-SetupPath $env:ZENITH_TML_ROOT
    $loaderSource = '环境变量 ZENITH_TML_ROOT'
} else {
    $savedRoot = $null
    if (Test-Path -LiteralPath $propsPath) { $savedRoot = Get-LocalPropsTmlRoot $propsPath }
    if ($savedRoot) {
        $tmlRoot = ConvertTo-SetupPath $savedRoot
        $loaderSource = 'local.props 的 TmlRoot'
    } else {
        $tmlRoot = $defaultLoaderRoot
        $loaderSource = '默认值'
    }
}
Write-Setup "2/7 tModLoader 目录：$tmlRoot（来源：$loaderSource）"

# 步骤 3：校验安装目录。缺 tMLMod.targets 说明指到了游戏本体或其他目录。
if (-not (Test-Path -LiteralPath "$tmlRoot/tMLMod.targets")) {
    throw "tModLoader 路径不对：$tmlRoot 下没有 tMLMod.targets。请用 -LoaderRoot 指定安装根目录（该目录下应有 tMLMod.targets），或设置环境变量 ZENITH_TML_ROOT。"
}
Write-Setup "3/7 已确认 tMLMod.targets 存在"

# 步骤 4：写 local.props（UTF-8 无 BOM），内容一致则跳过，保证幂等。
$content = New-LocalPropsContent $tmlRoot
$previous = $null
if (Test-Path -LiteralPath $propsPath) {
    $previous = [System.IO.File]::ReadAllText($propsPath) -replace "`r`n", "`n"
}
if ($null -ne $previous -and $previous.Trim() -eq $content.Trim()) {
    Write-Setup "4/7 local.props 已是最新，无需改写"
} else {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($propsPath, $content, $utf8NoBom)
    if ($null -ne $previous) {
        Write-Setup "4/7 已更新 local.props（TmlRoot 改为 $tmlRoot）"
    } else {
        Write-Setup "4/7 已生成 local.props（TmlRoot=$tmlRoot）"
    }
}

# 步骤 5：装 git 钩子。直接调用仓库内脚本，保持在当前会话里执行，输出可见。
$hookScript = Join-Path $taskRoot 'checks/install_git_hooks.ps1'
if (-not (Test-Path -LiteralPath $hookScript)) {
    throw "找不到 git 钩子安装脚本：$hookScript。仓库可能不完整，请重新克隆或检查 checks/ 目录。"
}
# 清掉上一个原生命令的退出码，避免误判为钩子安装失败。
$global:LASTEXITCODE = $null
try {
    & $hookScript
} catch {
    throw "安装 git 钩子失败：$($_.Exception.Message)。可手动执行 pwsh -File checks/install_git_hooks.ps1 排查。"
}
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "安装 git 钩子失败（退出码 $LASTEXITCODE）。可手动执行 pwsh -File checks/install_git_hooks.ps1 排查。"
}
Write-Setup "5/7 git 钩子已就绪"

# 步骤 6：准备 Luminance 依赖。python 探测方式与 run_wood_checks.ps1 保持一致。
if ($SkipLuminance) {
    Write-Setup "6/7 已跳过 Luminance 依赖准备（-SkipLuminance）。若编译报找不到 Luminance，请在 tModLoader 里订阅 Luminance 后重跑本脚本。"
} else {
    $python = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    if (-not (Test-Path -LiteralPath $python)) {
        $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
        if ($pythonCommand) { $python = $pythonCommand.Source }
    }
    if (-not $python -or -not (Test-Path -LiteralPath $python)) {
        throw "找不到 python。请安装 Python 3 并加入 PATH 后重跑；也可以先用 -SkipLuminance 跳过这一步。"
    }
    & $python (Join-Path $taskRoot 'checks/prepare_luminance.py')
    if ($LASTEXITCODE -ne 0) {
        throw "Luminance 依赖准备失败（退出码 $LASTEXITCODE）。请在 tModLoader 里订阅 Luminance，或用环境变量 ZENITH_LUMINANCE_ARCHIVE 指向它的 .tmod 文件后重跑。"
    }
    Write-Setup "6/7 Luminance 依赖已就绪"
}

# 步骤 7：编译模组。优先用 PATH 上的 dotnet，退回到 tModLoader 自带的那一份。
if ($SkipBuild) {
    Write-Setup "7/7 已跳过编译（-SkipBuild）"
} else {
    $project = Join-Path $taskRoot 'ZenithReborn/ZenithReborn.csproj'
    if (-not (Test-Path -LiteralPath $project)) {
        throw "找不到工程文件：$project。仓库可能不完整，请重新克隆。"
    }
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $dotnet = $dotnetCommand.Source
    } elseif (Test-Path -LiteralPath "$tmlRoot/dotnet/dotnet.exe") {
        $dotnet = "$tmlRoot/dotnet/dotnet.exe"
    } else {
        throw "找不到 dotnet。请安装 .NET 8 SDK 并加入 PATH 后重跑。"
    }
    & $dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "编译失败（退出码 $LASTEXITCODE）。请检查上面的 MSBuild 报错；若提示找不到 python，请把 Python 加入 PATH。"
    }
    Write-Setup "7/7 编译完成（Release）"
}

# 收尾提示：回归入口与协作文档。
Write-Setup '完成。下一步：'
Write-Host '  pwsh -File checks/run_wood_checks.ps1   # 跑全量回归，确认本机环境可用'
$guidePath = Join-Path $taskRoot 'docs/协作开发指南.md'
if (Test-Path -LiteralPath $guidePath) {
    Write-Host '  docs/协作开发指南.md                    # 了解分支同步与提交前检查流程'
} else {
    Write-Host '  AGENTS.md                               # docs/协作开发指南.md 尚未入库，先看仓库协作约定'
}
