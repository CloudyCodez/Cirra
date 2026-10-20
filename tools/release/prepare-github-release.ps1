[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$project = Join-Path $root "src\CloudBerry.App\CloudBerry.App.csproj"
$innoCompiler = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"

[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string](@($projectXml.Project.PropertyGroup.Version)[0])
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "CloudBerry.App.csproj does not declare a Version."
}
if (-not (Test-Path -LiteralPath $innoCompiler)) {
    throw "Inno Setup 6 is required at $innoCompiler."
}

& (Join-Path $PSScriptRoot "build-closed-beta.ps1") -SkipTests:$SkipTests
if ($LASTEXITCODE -ne 0) {
    throw "The protected release build failed."
}

$appDirectory = Join-Path $root "artifacts\cirra-v$version-closed-beta\tester-package\app"
$installerName = "Cirra-Setup-v$version.exe"
$installerPath = Join-Path $root "publish\installer\$installerName"
$installerScript = Join-Path $root "tools\installer\CloudBerry.iss"

& $innoCompiler "/DMyAppVersion=$version" "/DMyAppOutputBase=Cirra-Setup-v$version" "/DMyAppSourceDir=$appDirectory" $installerScript
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath)) {
    throw "The Cirra installer build failed."
}

$installer = Get-Item -LiteralPath $installerPath
$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = "$installerPath.sha256"
[IO.File]::WriteAllText($hashPath, "$hash  $installerName`n", [Text.UTF8Encoding]::new($false))

$manifest = [ordered]@{
    version = $version
    installerUrl = "https://github.com/CloudyCodez/Cirra/releases/download/v$version/$installerName"
    notes = "Cirra $version is ready."
    publishedAtUtc = [DateTime]::UtcNow.ToString("o")
    sha256 = $hash
    packageSizeBytes = $installer.Length
}
$manifestPath = Join-Path $root "update-feed\latest.json"
$manifestJson = $manifest | ConvertTo-Json
[IO.File]::WriteAllText($manifestPath, "$manifestJson`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Version = $version
    Installer = $installerPath
    InstallerSha256 = $hash
    InstallerBytes = $installer.Length
    PortableArchive = Join-Path $root "artifacts\Cirra-v$version-closed-beta-win-x64.zip"
    UpdateManifest = $manifestPath
    NextStep = "Review and commit update-feed/latest.json, then create the GitHub release from that commit."
} | Format-List
