param(
    [string] $WorkerName = "cloudberry-social"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

function Find-Npx {
    $command = Get-Command npx -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

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

    throw "Node.js/npm is required so Wrangler can run. Install Node.js LTS, reopen PowerShell, then run deploy.cmd again."
}

function Ensure-KvNamespace {
    param([string] $NpxPath)

    $wranglerPath = Join-Path $Root "wrangler.toml"
    $config = Get-Content -LiteralPath $wranglerPath -Raw
    if ($config -notmatch "__SOCIAL_KV_ID__") {
        return
    }

    Write-Host "Creating Cloudflare KV namespace..." -ForegroundColor Cyan
    $output = & $NpxPath wrangler kv namespace create cloudberry-social-store 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Cloudflare KV namespace creation failed.`n$output"
    }

    if ($output -notmatch 'id\s*=\s*"(?<id>[^"]+)"') {
        throw "Cloudflare KV namespace id could not be parsed automatically.`n$output"
    }

    $id = $matches["id"]
    $config = $config.Replace("__SOCIAL_KV_ID__", $id)
    Set-Content -LiteralPath $wranglerPath -Value $config -NoNewline
    Write-Host "KV namespace ready: $id" -ForegroundColor Green
}

$Npx = Find-Npx

$enteredName = Read-Host "Worker name [$WorkerName]"
if (-not [string]::IsNullOrWhiteSpace($enteredName)) {
    $WorkerName = $enteredName.Trim()
}

Write-Host ""
Write-Host "CloudBerry Social setup" -ForegroundColor Cyan
Write-Host "This deploys the online Cloud Code and presence service." -ForegroundColor Cyan
Write-Host ""

Write-Host "Opening Cloudflare login if needed..." -ForegroundColor Cyan
& $Npx wrangler login

Ensure-KvNamespace -NpxPath $Npx

Write-Host ""
Write-Host "Deploying $WorkerName..." -ForegroundColor Cyan
& $Npx wrangler deploy worker.js --name $WorkerName --compatibility-date 2026-05-24

Write-Host ""
Write-Host "Done. Test these paths on the printed workers.dev URL:" -ForegroundColor Green
Write-Host "/health"
Write-Host "/friend/ABCDEFGH"
