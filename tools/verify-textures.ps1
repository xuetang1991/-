# 贴图检查 / texture check（CS-FS-004）：
#   Content\ 下直接继承 ModItem / ModProjectile / ModNPC / ModBuff / ModTile / ModWall /
#   ModDust / ModGore / ModPrefix / ModMount / ModRarity 的类，必须与同名 .png 同目录；
#   Content\ 下出现 override string Texture 必须已在 docs\style-exceptions.md 登记（四类白名单）；
#   孤儿贴图（有 .png 无同名 .cs）只警告，不阻断。
# 用法 / usage: pwsh -File tools/verify-textures.ps1 [-ProjectPath <csproj>]
[CmdletBinding()]
param(
    [string]$ProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'CommonTools.ps1')

$root = Get-ModRoot -ScriptRoot $PSScriptRoot
Get-ModProjectPath -Root $root -ProjectPath $ProjectPath | Out-Null

$contentDir = Join-Path $root 'Content'
if (-not (Test-Path -LiteralPath $contentDir))
{
    Write-Warning "未找到 Content\ 目录，跳过贴图检查 / Content dir missing, skipped"
    return
}

$exceptionsPath = Join-Path $root 'docs\style-exceptions.md'
$exceptionsText = ''
if (Test-Path -LiteralPath $exceptionsPath)
{
    $exceptionsText = Get-Content -LiteralPath $exceptionsPath -Raw -Encoding utf8
}
else
{
    Write-Warning "未找到 docs\style-exceptions.md，Texture override 将一律视为未登记 / exceptions file missing"
}

$modTypePattern = [regex]':\s*(?<base>ModItem|ModProjectile|ModNPC|ModBuff|ModTile|ModWall|ModDust|ModGore|ModPrefix|ModMount|ModRarity)\b'
$overridePattern = [regex]'\boverride\s+string\s+Texture\b'

$problems = New-Object System.Collections.Generic.List[string]
$typeCount = 0

foreach ($file in (Get-SourceFiles -Root $contentDir -Extension 'cs'))
{
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
    $className = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)

    if ($overridePattern.IsMatch($text))
    {
        if ($exceptionsText -notmatch [regex]::Escape($className))
        {
            $problems.Add("$($file.FullName): 存在 override string Texture，但未在 docs\style-exceptions.md 登记（CS-FS-004）") | Out-Null
        }
        continue
    }

    if ($modTypePattern.IsMatch($text))
    {
        $typeCount++
        $pngPath = [System.IO.Path]::ChangeExtension($file.FullName, '.png')
        if (-not (Test-Path -LiteralPath $pngPath))
        {
            $problems.Add("$($file.FullName): 缺少同名贴图 $className.png（CS-FS-004，自动推断贴图只能靠同名 png）") | Out-Null
        }
    }
}

# 孤儿贴图只警告：可能是素材暂存，不阻断门禁
foreach ($png in (Get-ChildItem -LiteralPath $contentDir -Recurse -File -Filter '*.png' -ErrorAction SilentlyContinue))
{
    $csPath = [System.IO.Path]::ChangeExtension($png.FullName, '.cs')
    if (-not (Test-Path -LiteralPath $csPath))
    {
        Write-Warning "孤儿贴图 / orphan texture（无同名 .cs）: $($png.FullName)"
    }
}

if ($problems.Count -gt 0)
{
    throw ("贴图检查失败 / texture check failed:`n  " + ($problems -join "`n  "))
}

Write-Host "[textures] OK — 检查 $typeCount 个 ModType 类"
