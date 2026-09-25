# 打包 .fep 插件包
# 用法：scripts\pack_fep.ps1 -ProjectDir examples\HelloBehaviorPlugin [-Configuration Release] [-OutputDir <dir>] [-NoBuild]
param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [string]$Configuration = "Release",
    [string]$OutputDir = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$projectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$csproj = Get-ChildItem -Path $projectDir -Filter *.csproj | Select-Object -First 1
if ($null -eq $csproj) { throw "未找到 .csproj：$projectDir" }

if (-not $NoBuild) {
    Write-Host "构建 $($csproj.Name)（$Configuration）..."
    dotnet build $csproj.FullName -c $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "构建失败" }
}

# 插件输出目录（含 plugin.json 与程序集）
$outDir = Join-Path (Join-Path $projectDir "bin") (Join-Path $Configuration "net8.0")
$manifestPath = Join-Path $outDir "plugin.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "输出目录缺少 plugin.json（请确保 csproj 将其 CopyToOutputDirectory）：$manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $manifest.id) { throw "plugin.json 缺少 id" }
if (-not $manifest.assembly) { throw "plugin.json 缺少 assembly" }

$dllPath = Join-Path $outDir $manifest.assembly
if (-not (Test-Path -LiteralPath $dllPath)) { throw "程序集不存在：$dllPath" }

# 暂存后压缩为 zip（扩展名 .fep）
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("fep_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    Copy-Item -LiteralPath $manifestPath (Join-Path $stage "plugin.json")
    Copy-Item -LiteralPath $dllPath (Join-Path $stage $manifest.assembly)

    if ($OutputDir) {
        $dest = (Resolve-Path -LiteralPath $OutputDir -ErrorAction SilentlyContinue)
        if ($null -eq $dest) { New-Item -ItemType Directory -Path $OutputDir | Out-Null; $dest = (Resolve-Path $OutputDir) }
        $outFile = Join-Path $dest.Path "$($manifest.id).fep"
    }
    else {
        $outFile = Join-Path $projectDir "$($manifest.id).fep"
    }

    if (Test-Path -LiteralPath $outFile) { Remove-Item -LiteralPath $outFile -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $outFile)
    Write-Host "已生成插件包：$outFile" -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}