param(
    [string]$Configuration = "Release",
    [switch]$RunTests = $true,
    [switch]$Publish = $false
)

$ErrorActionPreference = "Stop"
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " CutCraft - Build and Test Automation" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
Set-Location $scriptRoot

$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

Write-Host "Using .NET: $dotnet" -ForegroundColor Gray

# 1. Restore & Build
Write-Host "`n[1/3] Building CutCraft Solution ($Configuration)..." -ForegroundColor Yellow
& $dotnet build CutCraft.sln -c $Configuration /m:1 /p:BuildInParallel=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}

# 2. Test
if ($RunTests) {
    Write-Host "`n[2/3] Running Unit Test Suites..." -ForegroundColor Yellow
    $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
    $testDll = Join-Path $scriptRoot "tests\CutCraft.Core.Tests\bin\$Configuration\net10.0-windows\CutCraft.Core.Tests.dll"
    & $dotnet $testDll
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit tests failed!"
    }
}

# 3. Publish
if ($Publish) {
    Write-Host "`n[3/3] Publishing Standalone Windows App..." -ForegroundColor Yellow
    $publishDir = Join-Path $scriptRoot "publish\win-x64"
    & $dotnet publish src/CutCraft.Wpf/CutCraft.Wpf.csproj -c $Configuration --no-build -o $publishDir
    Write-Host "`nPublished executable ready at: $publishDir\CutCraft.exe" -ForegroundColor Green
}

Write-Host "`n[OK] CutCraft build process completed successfully!" -ForegroundColor Green
