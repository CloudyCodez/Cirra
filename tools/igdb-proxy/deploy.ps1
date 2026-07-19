param(
    [string] $WorkerName = "cloudberry-igdb-proxy",
    [switch] $InstallNode
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

Write-Host ""
Write-Host "CloudBerry IGDB Proxy setup" -ForegroundColor Cyan
Write-Host "This deploys a Cloudflare Worker and stores your IGDB credentials as Worker secrets."
Write-Host ""

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

    return $null
}

function Ensure-Node {
    $npxPath = Find-Npx
    if ($npxPath) {
        return $npxPath
    }

    Write-Host "Node.js/npm is not installed yet. Cloudflare Wrangler needs npx." -ForegroundColor Yellow
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "Install Node.js LTS from https://nodejs.org, reopen PowerShell, then run deploy.cmd again."
    }

    if (-not $InstallNode) {
        $answer = Read-Host "Install Node.js LTS automatically with winget now? [Y/n]"
        if ($answer -match '^(n|no)$') {
            throw "Install Node.js LTS from https://nodejs.org, reopen PowerShell, then run deploy.cmd again."
        }
    }

    Write-Host "Installing Node.js LTS through winget..." -ForegroundColor Cyan
    winget install --id OpenJS.NodeJS.LTS --exact --source winget --accept-package-agreements --accept-source-agreements

    $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [Environment]::GetEnvironmentVariable("Path", "User")

    $npxPath = Find-Npx
    if (-not $npxPath) {
        throw "Node.js install finished, but npx is not visible yet. Close this PowerShell window, open a new one, then run deploy.cmd again."
    }

    return $npxPath
}

$Npx = Ensure-Node

$enteredName = Read-Host "Worker name [$WorkerName]"
if (-not [string]::IsNullOrWhiteSpace($enteredName)) {
    $WorkerName = $enteredName.Trim()
}

$ClientId = Read-Host "Twitch/IGDB Client ID"
$SecureSecret = Read-Host "Twitch/IGDB Client Secret" -AsSecureString
$SecretPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureSecret)
try {
    $ClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($SecretPtr)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($SecretPtr)
}

if ([string]::IsNullOrWhiteSpace($ClientId) -or [string]::IsNullOrWhiteSpace($ClientSecret)) {
    throw "Client ID and Client Secret are required."
}

Write-Host ""
Write-Host "Opening Cloudflare login if needed..." -ForegroundColor Cyan
& $Npx wrangler login

Write-Host ""
Write-Host "Saving Worker secrets..." -ForegroundColor Cyan
$ClientId.Trim() | & $Npx wrangler secret put IGDB_CLIENT_ID --name $WorkerName
$ClientSecret.Trim() | & $Npx wrangler secret put IGDB_CLIENT_SECRET --name $WorkerName

Write-Host ""
Write-Host "Deploying $WorkerName..." -ForegroundColor Cyan
& $Npx wrangler deploy worker.js --name $WorkerName --compatibility-date 2026-05-20

Write-Host ""
Write-Host "Done. Copy the workers.dev URL shown above into CloudBerry Settings > Metadata > IGDB Proxy Endpoint." -ForegroundColor Green
Write-Host "Health check path: /health"
Write-Host "Search path: /search?query=Halo&limit=5"
