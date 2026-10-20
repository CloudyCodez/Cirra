param(
    [string]$PackageName = "Cirra-Setup-v0.4.1",
    [string]$ProjectName = "cloudberry-updates"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectPath = Join-Path $root "src\CloudBerry.App\CloudBerry.App.csproj"
$installerPath = Join-Path $root ("publish\installer\" + $PackageName + ".exe")
$feedRoot = Join-Path $root "publish\update-feed"
$downloadsRoot = Join-Path $feedRoot "downloads"
$chunkSizeBytes = 20MB

if (-not (Test-Path $installerPath)) {
    throw "Installer not found at $installerPath"
}

[xml]$project = Get-Content $projectPath
$version = @($project.Project.PropertyGroup.Version)[0]
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "1.0.0"
}

New-Item -ItemType Directory -Force -Path $downloadsRoot | Out-Null

Get-ChildItem -LiteralPath $downloadsRoot -Filter "$PackageName.part*" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -LiteralPath $downloadsRoot -Filter "$PackageName.exe" -ErrorAction SilentlyContinue | Remove-Item -Force

$chunkUrls = @()
$stream = [System.IO.File]::OpenRead($installerPath)
try {
    $buffer = New-Object byte[] $chunkSizeBytes
    $chunkIndex = 0
    while (($bytesRead = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $chunkName = "{0}.part{1:D3}" -f $PackageName, $chunkIndex
        $chunkPath = Join-Path $downloadsRoot $chunkName
        $output = [System.IO.File]::Create($chunkPath)
        try {
            $output.Write($buffer, 0, $bytesRead)
        }
        finally {
            $output.Dispose()
        }

        $chunkUrls += "/downloads/$chunkName"
        $chunkIndex++
    }
}
finally {
    $stream.Dispose()
}

$manifest = [ordered]@{
    version = $version
    installerUrl = ""
    chunkUrls = $chunkUrls
    notes = "Cirra $version is ready."
    publishedAtUtc = [DateTime]::UtcNow.ToString("o")
    sha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    packageSizeBytes = (Get-Item -LiteralPath $installerPath).Length
}

$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $feedRoot "latest.json") -Encoding UTF8

Write-Host ""
Write-Host "Publishing Cirra update feed" -ForegroundColor Cyan
Write-Host "Version: $version"
Write-Host "Package: $installerPath"
Write-Host ""

$projectList = cmd /c "npx --yes wrangler pages project list" 2>&1 | Out-String
if ($projectList -notmatch [regex]::Escape($ProjectName)) {
    Write-Host "Creating Pages project $ProjectName..." -ForegroundColor Yellow
    cmd /c "npx --yes wrangler pages project create $ProjectName --production-branch main" | Out-Host
}

cmd /c "npx --yes wrangler pages deploy `"$feedRoot`" --project-name $ProjectName" | Out-Host

Write-Host ""
Write-Host "Done. Update feed URL:" -ForegroundColor Green
Write-Host "https://$ProjectName.pages.dev/latest.json" -ForegroundColor Green
