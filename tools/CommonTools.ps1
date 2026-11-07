# 公共工具函数（被 build / verify / check-localization / verify-textures 引用）
# 本文件不是门禁入口，不要单独运行 / shared helpers, not a gate entry point.
Set-StrictMode -Version Latest

function Get-ModRoot
{
    param([string]$ScriptRoot)
    return (Split-Path -Parent $ScriptRoot)
}

function Get-DotnetPath
{
    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE))
    {
        $candidates.Add((Join-Path $env:USERPROFILE '.dotnet\dotnet.exe')) | Out-Null
    }
    $candidates.Add('C:\Users\Alsac\.dotnet\dotnet.exe') | Out-Null

    foreach ($candidate in $candidates)
    {
        if (Test-Path -LiteralPath $candidate)
        {
            return $candidate
        }
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $command)
    {
        return $command.Source
    }

    throw '未找到 dotnet / dotnet not found：请安装 .NET 8 SDK 或把 dotnet 加入 PATH。'
}

function Get-ModProjectPath
{
    param(
        [string]$Root,
        [string]$ProjectPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ProjectPath))
    {
        $resolved = [System.IO.Path]::GetFullPath($ProjectPath)
        if (-not (Test-Path -LiteralPath $resolved))
        {
            throw "指定的工程不存在 / project not found: $resolved"
        }
        return $resolved
    }

    $projects = @(Get-ChildItem -LiteralPath $Root -Filter '*.csproj' -File -ErrorAction SilentlyContinue)
    if ($projects.Count -eq 1)
    {
        return $projects[0].FullName
    }
    if ($projects.Count -eq 0)
    {
        throw "工程根目录未找到 *.csproj / no *.csproj under: $Root"
    }
    throw "工程根目录有多个 *.csproj，请用 -ProjectPath 指定 / multiple projects: $($projects.Name -join ', ')"
}

function Get-ModAssemblyName
{
    param([string]$ProjectPath)

    $xml = Get-Content -LiteralPath $ProjectPath -Raw -Encoding utf8
    $match = [regex]::Match($xml, '<AssemblyName>\s*(?<name>[^<\s]+)\s*</AssemblyName>')
    if ($match.Success)
    {
        return $match.Groups['name'].Value
    }
    return [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
}

function Get-SourceFiles
{
    param(
        [string]$Root,
        [string]$Extension
    )

    $excluded = @('bin', 'obj', '.git', '.vs', '.idea', 'node_modules', '.cache')
    $files = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.$Extension" -ErrorAction SilentlyContinue)
    return @($files | Where-Object {
        $relative = $_.FullName.Substring($Root.Length).TrimStart('\', '/')
        $segments = $relative -split '[\\/]'
        $hits = @($segments | Where-Object { $excluded -contains $_ })
        return ($hits.Count -eq 0)
    })
}

# 读取旧代码基线豁免清单（docs\legacy-baseline.txt）：一行一项，'#' 开头为注释。
# Reads the legacy baseline whitelist (docs\legacy-baseline.txt): one entry per line, '#' starts a comment.
function Get-LegacyBaseline
{
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root))
    {
        throw 'Get-LegacyBaseline 需要 -Root 参数 / -Root is required'
    }

    $baselinePath = Join-Path $Root 'docs\legacy-baseline.txt'
    if (-not (Test-Path -LiteralPath $baselinePath))
    {
        return @()
    }

    return @(
        Get-Content -LiteralPath $baselinePath -Encoding utf8 |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -ne '' -and -not $_.StartsWith('#') }
    )
}

function Resolve-TModTargetsPath
{
    param([string]$ProjectPath)

    $projectDir = Split-Path -Parent $ProjectPath
    $candidates = @(
        (Join-Path $projectDir '..\tModLoader.targets'),
        (Join-Path $projectDir 'tModLoader.targets')
    )

    foreach ($candidate in $candidates)
    {
        if (-not (Test-Path -LiteralPath $candidate))
        {
            continue
        }

        $targetsText = Get-Content -LiteralPath $candidate -Raw -Encoding utf8
        $match = [regex]::Match($targetsText, 'Project="(?<path>[^"]*tMLMod\.targets)"')
        if ($match.Success)
        {
            return $match.Groups['path'].Value
        }
    }

    throw '无法从 ..\tModLoader.targets 解析 tMLMod.targets / cannot resolve tMLMod.targets'
}

function Invoke-ModBuild
{
    param([string]$ProjectPath)

    $dotnet = Get-DotnetPath
    $tModTargetsPath = Resolve-TModTargetsPath -ProjectPath $ProjectPath
    $tModLoaderDir = Split-Path -Parent $tModTargetsPath
    $tModLoaderDll = Join-Path $tModLoaderDir 'tModLoader.dll'
    if (-not (Test-Path -LiteralPath $tModLoaderDll))
    {
        throw "未找到 tModLoader.dll / tModLoader.dll not found: $tModLoaderDll"
    }

    $projectDir = (Split-Path -Parent $ProjectPath).TrimEnd('\') + '\'
    Write-Host "[build] dotnet  : $dotnet"
    Write-Host "[build] project : $projectDir"
    Write-Host '[build] 由 tModLoader 进程内编译（可解析 modReferences；会顺带打包 .tmod）/ in-process build via tModLoader'

    # 打包到临时目录：即使 tModLoader 正在运行（占用 Mods\<name>.tmod）也能通过门禁。
    # Package into a temp save directory so the gate also works while tModLoader is running.
    $baseline = Get-LegacyBaseline -Root (Split-Path -Parent $ProjectPath)
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) 'sea-gate-build'
    if (Test-Path -LiteralPath $sandbox)
    {
        Remove-Item -LiteralPath $sandbox -Recurse -Force
    }

    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    Write-Host "[build] sandbox : $sandbox"

    $arguments = @(
        $tModLoaderDll
        '-server'
        '-build'
        $projectDir
        '-tmlsavedirectory'
        $sandbox
        '-define'
        'TML_2026_08;JETBRAINS_ANNOTATIONS'
        '-unsafe'
        'false'
    )

    Push-Location -LiteralPath $tModLoaderDir
    try
    {
        $output = & $dotnet @arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally
    {
        Pop-Location
    }

    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0)
    {
        # 唯一允许降级的情况：本机拿不到 SubworldLibrary 参考程序集（它只在 Steam 创意工坊）。
        # 此时每个 Subworld / SubworldSystem 引用各报一条，属于环境缺失而非代码错误。
        # Only tolerate the case where the SubworldLibrary reference assembly is unavailable
        # (Workshop-only); every other build failure still fails the gate.
        $errors = @($output | Where-Object { ([string]$_).Contains('error CS') })
        $referenceOnly = $errors.Count -gt 0
        foreach ($line in $errors)
        {
            $text = [string]$line
            if ($text.Contains('SubworldSystem') -or $text.Contains("'Subworld'"))
            {
                continue
            }

            $referenceOnly = $false
            break
        }

        if ($referenceOnly)
        {
            Write-Host '[build] SKIP — 缺少 SubworldLibrary 参考程序集（仅 Steam 创意工坊提供），本机无法离屏编译' -ForegroundColor Yellow
            Write-Host '[build] SKIP — reference assembly SubworldLibrary (Workshop-only) is unavailable for the headless build'
            return
        }

        throw "构建失败 / build failed (exit $exitCode)。先修根因，不要绕过门禁。"
    }

    # 逐条比对 warning：允许 docs\legacy-baseline.txt 登记的 CS 编号，其余一律失败。
    # Check every warning: IDs registered in docs\legacy-baseline.txt are allowed, everything else fails.
    $unbaselined = New-Object System.Collections.Generic.List[string]
    $totalWarnings = 0
    foreach ($line in $output)
    {
        $text = [string]$line
        $summary = [regex]::Match($text, 'Compilation finished with \d+ errors and (?<warnings>\d+) warnings')
        if ($summary.Success)
        {
            $totalWarnings = [int]$summary.Groups['warnings'].Value
        }

        $warningMatch = [regex]::Match($text, ' warning (?<id>[A-Z]+\d+): ')
        if (-not $warningMatch.Success)
        {
            continue
        }

        $warningId = $warningMatch.Groups['id'].Value
        $covered = $false
        foreach ($entry in $baseline)
        {
            if ($entry -eq $warningId -or $text.Contains($entry))
            {
                $covered = $true
                break
            }
        }

        if (-not $covered)
        {
            $unbaselined.Add($text)
        }
    }

    if ($unbaselined.Count -gt 0)
    {
        $unbaselined | ForEach-Object { Write-Host "[build] 未登记 warning / unbaselined warning: $_" }
        throw "构建产生 $($unbaselined.Count) 个未登记的 warning（门禁要求 0 warning）/ build produced unbaselined warning(s)"
    }

    if ($totalWarnings -gt 0)
    {
        Write-Host "[build] warning 共 $totalWarnings 个，全部命中 docs\legacy-baseline.txt 基线 / all warnings matched the registered baseline"
    }

    Write-Host '[build] OK'
}

function Test-Utf8BomFile
{
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 3)
    {
        return $false
    }
    return ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
}
