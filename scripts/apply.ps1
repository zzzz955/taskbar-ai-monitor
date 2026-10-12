[CmdletBinding()]
param(
    [switch]$Local,
    [switch]$Force,
    [switch]$NoLaunch,
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64'
)

# Set UTF-8 encoding for console and pipe output
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$installRoot = Join-Path $env:LOCALAPPDATA 'TaskbarAiMonitor'
$stateFile = Join-Path $installRoot 'installed.json'
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
        if ($LASTEXITCODE -eq 0 -and $c) {
            $commit = "$c".Trim()
        }
    } catch { }
}

$installed = if (Test-Path -LiteralPath $stateFile) {
    try { Get-Content -LiteralPath $stateFile -Encoding UTF8 -Raw | ConvertFrom-Json } catch { $null }
} else { $null }

# 커밋 일치 여부 확인 (동일 커밋이면 재빌드 생략)
if ($installed -and $installed.sourceCommit -and ($installed.sourceCommit -eq $commit) -and -not $Force -and -not $Local) {
    $short = if ($commit.Length -gt 7) { $commit.Substring(0, 7) } else { $commit }
    Write-Host "[Taskbar AI Monitor] 이미 최신 커밋($short)으로 설치되어 있습니다. 변경 없음." -ForegroundColor Green
    Write-Host "강제 재설치가 필요하면 'apply.bat -Force' 또는 'apply.bat -Local'을 사용하세요." -ForegroundColor Gray
    return
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host " Taskbar AI Monitor - Git 기반 설치 및 업데이트" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host " 버전: v$version"
Write-Host " 커밋: $commit"
Write-Host " 대상 런타임: $Runtime"
Write-Host " 설치 경로: $installRoot"
Write-Host ""

# 기존 실행 중인 프로세스 확인 및 종료
$runningProcesses = Get-Process -Name "TaskbarAiMonitor" -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "실행 중인 TaskbarAiMonitor 프로세스를 종료합니다..." -ForegroundColor Yellow
    foreach ($p in $runningProcesses) {
        try {
            $p.CloseMainWindow() | Out-Null
            Start-Sleep -Milliseconds 300
            if (-not $p.HasExited) { $p.Kill() }
        } catch { }
    }
}

# 빌드 및 배포
$binDir = Join-Path $installRoot 'bin'
New-Item -ItemType Directory -Path $binDir -Force | Out-Null

Write-Host "프로젝트를 빌드 및 게시합니다 (dotnet publish)..." -ForegroundColor Yellow
$projFile = Join-Path $repoRoot 'TaskbarAiMonitor.csproj'
& dotnet publish $projFile -c Release -r $Runtime --self-contained false -o $binDir -p:Version=$version -p:SourceRevisionId=$commit | Out-Host

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 실패 (종료 코드 $LASTEXITCODE)."
}

# build.json 메타데이터 기록
$build = @{
    version = $version
    sourceCommit = $commit
    builtAt = [DateTimeOffset]::Now.ToString('O')
    runtime = $Runtime
}
$buildJsonPath = Join-Path $binDir 'build.json'
[IO.File]::WriteAllText($buildJsonPath, ($build | ConvertTo-Json), [Text.UTF8Encoding]::new($false))

# installed.json 상태 파일 기록
$state = @{
    version = $version
    runtime = $Runtime
    sourceCommit = $commit
    installedAt = [DateTimeOffset]::Now.ToString('O')
    installRoot = $installRoot
}
[IO.File]::WriteAllText($stateFile, ($state | ConvertTo-Json), [Text.UTF8Encoding]::new($false))

# 업데이트 스크립트 복사
$updaterDir = Join-Path $installRoot 'updater'
New-Item -ItemType Directory -Path $updaterDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'apply.bat') -Destination $updaterDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts') -Destination $updaterDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'release.json') -Destination $updaterDir -Force

$exePath = Join-Path $binDir 'TaskbarAiMonitor.exe'
Write-Host ""
Write-Host "======================================================" -ForegroundColor Green
Write-Host " [Taskbar AI Monitor] 설치/업데이트 성공!" -ForegroundColor Green
Write-Host " - 버전: v$version" -ForegroundColor Green
Write-Host " - 커밋: $commit" -ForegroundColor Green
Write-Host " - 실행 파일: $exePath" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""

if (-not $NoLaunch -and (Test-Path -LiteralPath $exePath)) {
    Write-Host "Taskbar AI Monitor를 시작합니다..." -ForegroundColor Cyan
    Start-Process -FilePath $exePath -WorkingDirectory $binDir
}
