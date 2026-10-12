[CmdletBinding()]
param(
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$releaseJsonFile = Join-Path $repoRoot 'release.json'

$version = "1.0.0"
if (Test-Path -LiteralPath $releaseJsonFile) {
    try {
        $meta = Get-Content -LiteralPath $releaseJsonFile -Encoding UTF8 -Raw | ConvertFrom-Json
        if ($meta.version) { $version = $meta.version }
    } catch { }
}

$commit = "untracked"
if (Get-Command git -ErrorAction SilentlyContinue) {
    try {
        $c = & git -C $repoRoot rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and $c) { $commit = "$c".Trim() }
    } catch { }
}

$distDir = Join-Path $repoRoot 'dist'
$stageDir = Join-Path $distDir "stage-$Runtime"
$releaseDir = Join-Path $distDir "releases/v$version"

New-Item -ItemType Directory -Path $stageDir, $releaseDir -Force | Out-Null

Write-Host "Building release v$version ($Runtime)..." -ForegroundColor Cyan
$projFile = Join-Path $repoRoot 'TaskbarAiMonitor.csproj'
& dotnet publish $projFile -c Release -r $Runtime --self-contained false -o $stageDir -p:Version=$version -p:SourceRevisionId=$commit | Out-Host

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$build = @{
    version = $version
    sourceCommit = $commit
    builtAt = [DateTimeOffset]::Now.ToString('O')
    runtime = $Runtime
}
[IO.File]::WriteAllText((Join-Path $stageDir 'build.json'), ($build | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $repoRoot 'apply.bat') -Destination $stageDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stageDir -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipFile = Join-Path $releaseDir "taskbar-ai-monitor-windows-$Runtime.zip"
if (Test-Path -LiteralPath $zipFile) { Remove-Item -LiteralPath $zipFile -Force }

[IO.Compression.ZipFile]::CreateFromDirectory($stageDir, $zipFile, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash -LiteralPath $zipFile -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zipFile + '.sha256'), ($hash + '  ' + [IO.Path]::GetFileName($zipFile) + "`n"), [Text.UTF8Encoding]::new($false))

Remove-Item -LiteralPath $stageDir -Recurse -Force
Write-Host "Release package ready: $zipFile" -ForegroundColor Green
Write-Host "SHA256: $hash" -ForegroundColor Gray
