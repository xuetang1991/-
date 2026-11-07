# 无头构建：只编译，不打包 .tmod（游戏运行时不会被占用）
# 用法 / usage: pwsh -File tools/build.ps1 [-ProjectPath <csproj>]
[CmdletBinding()]
param(
    [string]$ProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'CommonTools.ps1')

$root = Get-ModRoot -ScriptRoot $PSScriptRoot
$project = Get-ModProjectPath -Root $root -ProjectPath $ProjectPath

Invoke-ModBuild -ProjectPath $project

$dotnet = Get-DotnetPath
$sdkVersion = & $dotnet --version
if ($LASTEXITCODE -ne 0)
{
    Write-Warning '无法读取 dotnet SDK 版本 / cannot read SDK version'
}
else
{
    Write-Host "[build] SDK: $sdkVersion（基线 8.0.x / baseline 8.0.x）"
    if (-not $sdkVersion.StartsWith('8.'))
    {
        Write-Warning "当前 SDK 不是 8.x，与规范基线不一致 / SDK is not 8.x: $sdkVersion"
    }
}

Write-Host '[build] 完成 / done'
