# 本地化检查 / localization check：
#   Localization\en-US_Mods.<ModName>.hjson 与 Localization\zh-Hans_Mods.<ModName>.hjson
#   1) 两个语种文件必须都存在；
#   2) 键集合必须双向齐全（互相没有缺键）；
#   3) 每个键必须以 Mods.<ModName>. 开头；
#   4) 键不得重复、值不得为空。
# 用法 / usage: pwsh -File tools/check-localization.ps1 [-ProjectPath <csproj>]
[CmdletBinding()]
param(
    [string]$ProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'CommonTools.ps1')

$root = Get-ModRoot -ScriptRoot $PSScriptRoot
$project = Get-ModProjectPath -Root $root -ProjectPath $ProjectPath
$modName = Get-ModAssemblyName -ProjectPath $project

$localizationDir = Join-Path $root 'Localization'
if (-not (Test-Path -LiteralPath $localizationDir))
{
    Write-Warning "未找到 Localization\ 目录，跳过本地化检查 / Localization dir missing, skipped"
    return
}

function Read-LocalizationFile
{
    param([string]$Path)

    # 支持嵌套 hjson（tML 实际加载的格式）：
    #   Mods: { sea: { Items: { DustSandItem: { DisplayName: Dust Sand } } } }
    # 用花括号深度维护键路径栈，拼出点分全键；同时兼容扁平点分键（无嵌套时路径栈为空）。
    # Supports nested hjson (the format tML actually loads) by tracking a key stack on brace depth,
    # and still accepts flat dotted keys (empty stack means the key is the full path).
    $entries = New-Object System.Collections.Generic.List[object]
    $lines = @(Get-Content -LiteralPath $Path -Encoding utf8)
    $stack = New-Object System.Collections.Generic.List[string]
    for ($index = 0; $index -lt $lines.Count; $index++)
    {
        $line = $lines[$index]
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('//') -or $trimmed.StartsWith('#'))
        {
            continue
        }
        if ($trimmed -match '^[}\]]*\}?\s*,?$')
        {
            $closing = 0
            foreach ($character in $trimmed.ToCharArray())
            {
                if ($character -eq '}')
                {
                    $closing++
                }
            }
            for ($pop = 0; $pop -lt $closing; $pop++)
            {
                if ($stack.Count -gt 0)
                {
                    $stack.RemoveAt($stack.Count - 1)
                }
            }
            continue
        }
        $match = [regex]::Match($line, '^\s*"?(?<key>[A-Za-z0-9_\.\-]+)"?\s*:\s*(?<rest>.*)$')
        if (-not $match.Success)
        {
            continue
        }
        $key = $match.Groups['key'].Value
        $rest = $match.Groups['rest'].Value.Trim()

        # hjson 的三引号多行字符串（tML 会把带换行的文本改写成这种形式）：
        # 整段跳过，只把它当成一条普通键值，避免把块内文字当成键压进路径栈。
        if ($rest.StartsWith("'''"))
        {
            $fullKey = if ($stack.Count -eq 0) { $key } else { (($stack + $key) -join '.') }
            $builder = New-Object System.Text.StringBuilder
            $tailText = $rest.Substring(3)

            if ($tailText.Length -gt 0)
            {
                [void]$builder.Append($tailText)
            }

            for ($cursor = $index + 1; $cursor -lt $lines.Count; $cursor++)
            {
                $inner = $lines[$cursor]

                if ($inner.Trim().StartsWith("'''"))
                {
                    $index = $cursor
                    break
                }

                if ($builder.Length -gt 0)
                {
                    [void]$builder.Append([char]10)
                }

                [void]$builder.Append($inner.Trim())
            }

            $entries.Add([pscustomobject]@{
                Line  = $index + 1
                Key   = $fullKey
                Value = $builder.ToString().Trim()
            }) | Out-Null
            continue
        }

        if ($rest -eq '' -or $rest -eq '{')
        {
            $stack.Add($key) | Out-Null
            continue
        }
        $fullKey = if ($stack.Count -eq 0) { $key } else { (($stack + $key) -join '.') }
        $entries.Add([pscustomobject]@{
            Line  = $index + 1
            Key   = $fullKey
            Value = $rest.TrimEnd(',').Trim()
        }) | Out-Null
    }
    return $entries
}

$problems = New-Object System.Collections.Generic.List[string]
$sets = @{}
$baseline = Get-LegacyBaseline -Root $root

foreach ($culture in @('en-US', 'zh-Hans'))
{
    $fileName = "${culture}_Mods.${modName}.hjson"

    # 旧代码基线豁免：整文件跳过（登记在 docs\legacy-baseline.txt）。
    # Legacy baseline: skip the whole file (registered in docs\legacy-baseline.txt).
    if ($baseline -contains "localization skip $fileName")
    {
        Write-Warning "旧代码基线豁免，跳过 $fileName / skipped by legacy baseline"
        continue
    }

    $path = Join-Path $localizationDir $fileName
    if (-not (Test-Path -LiteralPath $path))
    {
        $problems.Add("缺少本地化文件 / missing localization file: $path") | Out-Null
        continue
    }

    $entries = @(Read-LocalizationFile -Path $path)
    $prefix = "Mods.$modName."
    $seen = @{}

    foreach ($entry in $entries)
    {
        # tML 的 en-US_Mods.<Mod>.hjson 约定：文件内写的是「去掉前缀」的相对键
        # （根节点直接是 Items / NPCs / Projectiles / Tiles）。
        # 这里把相对键补上 Mods.<Mod>. 后再比对，同时仍兼容整键写法。
        # tML convention for en-US_Mods.<Mod>.hjson: entries are stored relative to
        # "Mods.<Mod>.", so prepend the prefix before comparing (full keys still accepted).
        $fullKey = if ($entry.Key.StartsWith('Mods.')) { $entry.Key } else { $prefix + $entry.Key }
        if ($fullKey.StartsWith('Mods.') -and -not $fullKey.StartsWith($prefix))
        {
            $problems.Add("${path}:$($entry.Line) 键前缀错误（应为 ${prefix}）：$fullKey") | Out-Null
        }
        if ($seen.ContainsKey($fullKey))
        {
            $problems.Add("${path}:$($entry.Line) 键重复 / duplicated key: $fullKey") | Out-Null
        }
        $seen[$fullKey] = $true
        if ([string]::IsNullOrWhiteSpace($entry.Value) -or $entry.Value -eq '""' -or $entry.Value -eq "''")
        {
            $problems.Add("${path}:$($entry.Line) 值为空 / empty value: $fullKey") | Out-Null
        }
    }
    $sets[$culture] = $seen
}

if ($sets.ContainsKey('en-US') -and $sets.ContainsKey('zh-Hans'))
{
    $enKeys = @($sets['en-US'].Keys)
    $zhKeys = @($sets['zh-Hans'].Keys)

    foreach ($key in $enKeys)
    {
        if (-not $sets['zh-Hans'].ContainsKey($key))
        {
            $problems.Add("zh-Hans 缺键 / missing in zh-Hans: $key") | Out-Null
        }
    }
    foreach ($key in $zhKeys)
    {
        if (-not $sets['en-US'].ContainsKey($key))
        {
            $problems.Add("en-US 缺键 / missing in en-US: $key") | Out-Null
        }
    }
}

if ($problems.Count -gt 0)
{
    throw ("本地化检查失败 / localization check failed:`n  " + ($problems -join "`n  "))
}

Write-Host "[localization] OK — en-US / zh-Hans 键集一致，前缀合法（ModName=$modName）"
