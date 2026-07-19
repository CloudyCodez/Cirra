param(
    [string] $WorkerName = "cloudberry-igdb-proxy"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

function Find-Npx {
    $command = Get-Command npx -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $knownPaths = @(
        "$env:ProgramFiles\nodejs\npx.cmd",
        "${env:ProgramFiles(x86)}\nodejs\npx.cmd",
        "$env:LOCALAPPDATA\Programs\nodejs\npx.cmd"
    )

    foreach ($path in $knownPaths) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }

    throw "npx was not found. Close this PowerShell window, open a new one, then run redeploy.cmd again."
}

$Npx = Find-Npx

Write-Host ""
Write-Host "Redeploying CloudBerry IGDB Proxy..." -ForegroundColor Cyan
& $Npx wrangler deploy worker.js --name $WorkerName --compatibility-date 2026-05-20

Write-Host ""
Write-Host "Done. Test the printed workers.dev URL with /health." -ForegroundColor Green
