#!/usr/bin/env pwsh
# Aimon build + test entry point. Restores, builds (Release), and runs all tests.
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'Aimon.slnx'

Write-Host "==> Restoring $solution" -ForegroundColor Cyan
dotnet restore $solution

Write-Host "==> Building (Release)" -ForegroundColor Cyan
dotnet build $solution -c Release --no-restore

Write-Host "==> Testing" -ForegroundColor Cyan
dotnet test $solution -c Release --no-build

Write-Host "==> Done" -ForegroundColor Green
