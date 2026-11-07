# 总门禁 / total gate：构建 + 格式 + 贴图 + 本地化 + 静态状态 + 禁用模式 + 编码
# 用法 / usage: pwsh -File tools/verify.ps1 [-ProjectPath <csproj>]
# 退出码 0 = 全部通过；非 0 = 有失败项（先修根因，不要绕过）。
[CmdletBinding()]
param(
    [string]$ProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'CommonTools.ps1')

# ---------------------------------------------------------------------------
# 内部检查函数（必须定义在执行流程之前）/ internal check functions
# ---------------------------------------------------------------------------

function Add-Failure
{
    param([string]$Message)
    $script:failures.Add($Message) | Out-Null
}

function Invoke-StaticStateCheck
{
    param([string]$Root)

    $registryPath = Join-Path $Root 'docs\state-registry.md'
    if (-not (Test-Path -LiteralPath $registryPath))
    {
        throw '缺少 docs\state-registry.md（静态状态登记表）/ registry file missing'
    }
    $registryText = Get-Content -LiteralPath $registryPath -Raw -Encoding utf8

    $fieldPattern = [regex]'^\s*(?:private|internal|protected|public)\s+static\s+(?!readonly\b|const\b|class\b|partial\b|event\b)(?<type>[\w<>\[\],\.\?\s]+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)'
    $problems = New-Object System.Collections.Generic.List[string]
    $baseline = Get-LegacyBaseline -Root $Root

    foreach ($file in (Get-SourceFiles -Root $Root -Extension 'cs'))
    {
        $lines = @(Get-Content -LiteralPath $file.FullName -Encoding utf8)
        for ($index = 0; $index -lt $lines.Count; $index++)
        {
            $line = $lines[$index]
            if ($line.TrimStart().StartsWith('//'))
            {
                continue
            }
            $match = $fieldPattern.Match($line)
            if (-not $match.Success)
            {
                continue
            }
            $name = $match.Groups['name'].Value

            # 旧代码基线豁免：docs\legacy-baseline.txt 里登记 state <字段名> 的可放行。
            # Legacy baseline: fields registered as 'state <name>' are tolerated.
            if ($baseline -contains "state $name")
            {
                continue
            }

            if ($registryText -notmatch [regex]::Escape($name))
            {
                $problems.Add("$($file.FullName):$($index + 1) 未登记的 static 字段 '$name'（CS-STATE-003）") | Out-Null
            }
        }
    }

    if ($problems.Count -gt 0)
    {
        throw ("发现未登记的静态可变状态 / unregistered static mutable state:`n  " + ($problems -join "`n  "))
    }
    Write-Host '[static-state] OK'
}

function Invoke-BannedPatternCheck
{
    param([string]$Root)

    $rules = @(
        @{ Pattern = '\bConsole\.(Write|WriteLine)\s*\('; Message = '禁用 Console 输出 / Console output banned（CS-ERRLOG-003）' },
        @{ Pattern = '\bnew\s+Random\s*\('; Message = '禁用 new System.Random / System.Random banned（CS-RANDOM-004）' },
        @{ Pattern = '\bSystem\.Threading\b'; Message = '禁用线程与并发 / threading banned（CS-LANG-005）' },
        @{ Pattern = '\bTask\.Run\s*\('; Message = '禁用 Task.Run / Task banned（CS-LANG-005）' },
        @{ Pattern = '#pragma\s+warning\s+disable'; Message = '禁用 #pragma 抑制 / pragma suppression banned（CS-LANG-008）' },
        @{ Pattern = '\bunsafe\b'; Message = '禁用 unsafe / unsafe banned（CS-LANG-004）' },
        @{ Pattern = '\bMonoMod\b'; Message = '禁用 MonoMod/IL / MonoMod banned（CS-LANG-011）' },
        @{ Pattern = '\bnew\s+ModPacket\s*\('; Message = '禁用 new ModPacket，用 Mod.GetPacket() / use Mod.GetPacket()（CS-NET-003）' }
    )

    $problems = New-Object System.Collections.Generic.List[string]
    foreach ($file in (Get-SourceFiles -Root $Root -Extension 'cs'))
    {
        $lines = @(Get-Content -LiteralPath $file.FullName -Encoding utf8)
        for ($index = 0; $index -lt $lines.Count; $index++)
        {
            $trimmed = $lines[$index].TrimStart()
            if ($trimmed.StartsWith('//'))
            {
                continue
            }
            foreach ($rule in $rules)
            {
                if ([regex]::IsMatch($lines[$index], $rule.Pattern))
                {
                    $problems.Add("$($file.FullName):$($index + 1) $($rule.Message)") | Out-Null
                }
            }
        }
    }

    if ($problems.Count -gt 0)
    {
        throw ("发现禁用模式 / banned pattern found:`n  " + ($problems -join "`n  "))
    }
    Write-Host '[banned-pattern] OK'
}

function Invoke-EncodingCheck
{
    param([string]$Root)

    $problems = New-Object System.Collections.Generic.List[string]
    $files = @()
    $files += Get-SourceFiles -Root $Root -Extension 'cs'
    $files += Get-SourceFiles -Root $Root -Extension 'hjson'

    foreach ($file in $files)
    {
        if ($file.Extension -eq '.hjson')
        {
            # tML 每次加载模组都会重写本地化文件（去掉 BOM、改用 Tab），这里先按 CS-FMT-002 归一
            # tML rewrites .hjson files on every mod load (drops the BOM, switches to tabs);
            # normalize them back before checking so the gate stays meaningful.
            $hasBom = Test-Utf8BomFile -Path $file.FullName
            $rawText = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
            $hasLf = [regex]::IsMatch($rawText, '(?<!\r)\n')
            if (-not $hasBom -or $hasLf)
            {
                $normalizedText = ($rawText -replace "`r`n", "`n") -replace "`n", "`r`n"
                if (-not $normalizedText.EndsWith("`r`n"))
                {
                    $normalizedText += "`r`n"
                }
                [System.IO.File]::WriteAllText($file.FullName, $normalizedText, (New-Object System.Text.UTF8Encoding($true)))
                Write-Warning "$($file.FullName): 被 tML 重写过，已自动归一回 UTF-8 BOM + CRLF / normalized back to UTF-8 BOM + CRLF"
            }
        }

        if (-not (Test-Utf8BomFile -Path $file.FullName))
        {
            $problems.Add("$($file.FullName): 缺少 UTF-8 BOM（CS-FMT-002）") | Out-Null
        }
        $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($file.FullName))
        if ([regex]::IsMatch($text, '(?<!\r)\n'))
        {
            $problems.Add("$($file.FullName): 存在 LF 行尾，应全部为 CRLF（CS-FMT-002）") | Out-Null
        }
    }

    if ($problems.Count -gt 0)
    {
        throw ("编码检查失败 / encoding check failed:`n  " + ($problems -join "`n  "))
    }
    Write-Host '[encoding] OK'
}

# ---------------------------------------------------------------------------
# 执行流程 / main flow
# ---------------------------------------------------------------------------

$root = Get-ModRoot -ScriptRoot $PSScriptRoot
$script:failures = New-Object System.Collections.Generic.List[string]

$project = $null
try
{
    $project = Get-ModProjectPath -Root $root -ProjectPath $ProjectPath
}
catch
{
    Add-Failure "工程定位失败 / cannot locate project: $($_.Exception.Message)"
}

if ($null -ne $project)
{
    Write-Host ''
    Write-Host '== 1/6 无头构建 / headless build（0 warning）==' -ForegroundColor Cyan
    try
    {
        Invoke-ModBuild -ProjectPath $project
    }
    catch
    {
        Add-Failure "构建失败 / build failed: $($_.Exception.Message)"
    }

    Write-Host ''
    Write-Host '== 2/6 dotnet format 格式与风格 / formatting and style ==' -ForegroundColor Cyan
    $dotnet = Get-DotnetPath

    & $dotnet format whitespace $project --verify-no-changes
    if ($LASTEXITCODE -ne 0)
    {
        Add-Failure 'format whitespace 未通过：缩进/换行/空行/末尾换行不符合 .editorconfig'
    }

    & $dotnet format style $project --verify-no-changes --severity warn
    if ($LASTEXITCODE -ne 0)
    {
        Add-Failure 'format style 未通过：命名/using/风格诊断未清理'
    }

    Write-Host ''
    Write-Host '== 3/6 贴图命名 / texture naming ==' -ForegroundColor Cyan
    try
    {
        & (Join-Path $PSScriptRoot 'verify-textures.ps1') -ProjectPath $project
    }
    catch
    {
        Add-Failure "贴图检查失败 / texture check failed: $($_.Exception.Message)"
    }

    Write-Host ''
    Write-Host '== 4/6 本地化键 / localization keys ==' -ForegroundColor Cyan
    try
    {
        & (Join-Path $PSScriptRoot 'check-localization.ps1') -ProjectPath $project
    }
    catch
    {
        Add-Failure "本地化检查失败 / localization check failed: $($_.Exception.Message)"
    }
}

Write-Host ''
Write-Host '== 5/6 静态可变状态登记 / static state registry ==' -ForegroundColor Cyan
try
{
    Invoke-StaticStateCheck -Root $root
}
catch
{
    Add-Failure "静态状态检查失败 / static state check failed: $($_.Exception.Message)"
}

Write-Host ''
Write-Host '== 6/6 禁用模式与编码 / banned patterns and encoding ==' -ForegroundColor Cyan
try
{
    Invoke-BannedPatternCheck -Root $root
}
catch
{
    Add-Failure "禁用模式检查失败 / banned pattern check failed: $($_.Exception.Message)"
}

try
{
    Invoke-EncodingCheck -Root $root
}
catch
{
    Add-Failure "编码检查失败 / encoding check failed: $($_.Exception.Message)"
}

Write-Host ''
if ($script:failures.Count -eq 0)
{
    Write-Host 'VERIFY OK — 全部门禁通过 / all gates passed' -ForegroundColor Green
    exit 0
}

Write-Host 'VERIFY FAILED — 先修根因，不要绕过门禁 / fix root cause, do not bypass:' -ForegroundColor Red
foreach ($failure in $script:failures)
{
    Write-Host "  - $failure" -ForegroundColor Red
}
exit 1
