[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$project = Join-Path $root "src\CloudBerry.App\CloudBerry.App.csproj"
$docs = Join-Path $root "tools\release\closed-beta"
$template = Join-Path $root "tools\obfuscation\closed-beta.xml.template"
$obfuscar = Join-Path $root "tools\obfuscation\obfuscar.console.exe"

[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string](@($projectXml.Project.PropertyGroup.Version)[0])
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "CloudBerry.App.csproj does not declare a Version."
}

$artifactDirectory = Join-Path $root "artifacts\cirra-v$version-closed-beta"
$stageDirectory = Join-Path $artifactDirectory "stage"
$protectedDirectory = Join-Path $artifactDirectory "protected"
$packageDirectory = Join-Path $artifactDirectory "tester-package"
$appDirectory = Join-Path $packageDirectory "app"
$archivePath = Join-Path $root "artifacts\Cirra-v$version-closed-beta-win-x64.zip"
$hashPath = "$archivePath.sha256"

function Assert-WorkspacePath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $workspacePrefix = $root.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the workspace: $resolved"
    }
}

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

Assert-WorkspacePath $artifactDirectory
Assert-WorkspacePath $archivePath
Assert-WorkspacePath $hashPath

if (-not $SkipTests) {
    Invoke-Checked "dotnet" @(
        "test", (Join-Path $root "CloudBerry.slnx"),
        "--no-restore", "-m:1", "-nr:false", "-p:BuildInParallel=false"
    )
}

if (Test-Path -LiteralPath $artifactDirectory) {
    Remove-Item -LiteralPath $artifactDirectory -Recurse -Force
}
foreach ($output in @($archivePath, $hashPath)) {
    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Force
    }
}

New-Item -ItemType Directory -Path $stageDirectory, $protectedDirectory, $appDirectory -Force | Out-Null

Invoke-Checked "dotnet" @(
    "publish", $project,
    "--configuration", "Release",
    "--runtime", "win-x64",
    "--self-contained", "true",
    "--output", $stageDirectory,
    "-p:PublishSingleFile=false",
    "-p:PublishReadyToRun=false",
    "-p:DebugSymbols=false",
    "-p:DebugType=None",
    "-p:IncludeCompanionTools=false"
)

foreach ($required in @("Cirra.exe", "Cirra.dll", "CloudBerry.Core.dll", "CloudBerry.Data.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $stageDirectory $required))) {
        throw "Publish output is missing $required."
    }
}

if (-not (Test-Path -LiteralPath $obfuscar)) {
    throw "The repository Obfuscar tool is missing: $obfuscar"
}

$obfuscationConfig = Join-Path $artifactDirectory "obfuscar.xml"
$config = (Get-Content -LiteralPath $template -Raw).
    Replace("__INPUT_DIRECTORY__", [Security.SecurityElement]::Escape($stageDirectory)).
    Replace("__OUTPUT_DIRECTORY__", [Security.SecurityElement]::Escape($protectedDirectory))
[IO.File]::WriteAllText($obfuscationConfig, $config, [Text.UTF8Encoding]::new($false))
Invoke-Checked $obfuscar @($obfuscationConfig)

Copy-Item -Path (Join-Path $stageDirectory "*") -Destination $appDirectory -Recurse -Force
foreach ($assembly in @("Cirra.dll", "CloudBerry.Core.dll", "CloudBerry.Data.dll")) {
    $protectedAssembly = Join-Path $protectedDirectory $assembly
    if (-not (Test-Path -LiteralPath $protectedAssembly)) {
        throw "Protected output is missing $assembly."
    }
    Copy-Item -LiteralPath $protectedAssembly -Destination (Join-Path $appDirectory $assembly) -Force
}

Get-ChildItem -LiteralPath $appDirectory -Recurse -File -Filter "*.xml" |
    Remove-Item -Force

foreach ($document in @("START_HERE.md", "TESTING_GUIDE.md", "RELEASE_NOTES.md")) {
    Copy-Item -LiteralPath (Join-Path $docs $document) -Destination (Join-Path $packageDirectory $document)
}

$forbidden = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Where-Object {
    $_.Extension -in @(".cs", ".csproj", ".sln", ".slnx", ".pdb", ".py", ".mjs", ".xml") -or
    $_.Name -in @("Mapping.txt", "obfuscar.xml")
}
if ($forbidden) {
    throw "Forbidden files reached the tester package: $($forbidden.FullName -join ', ')"
}

$companionFiles = @("title_cleaner.py", "library_insights.mjs")
foreach ($companion in $companionFiles) {
    if (Get-ChildItem -LiteralPath $packageDirectory -Recurse -File -Filter $companion) {
        throw "Readable companion source reached the tester package: $companion"
    }
}

$versionInfo = (Get-Item -LiteralPath (Join-Path $appDirectory "Cirra.exe")).VersionInfo
if ($versionInfo.ProductVersion -notlike "$version*") {
    throw "Packaged Cirra.exe reports product version '$($versionInfo.ProductVersion)', expected '$version'."
}

Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(
    $hashPath,
    "$hash  $([IO.Path]::GetFileName($archivePath))`n",
    [Text.UTF8Encoding]::new($false))

$packageFiles = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File
[pscustomobject]@{
    Version = $version
    Archive = $archivePath
    Sha256 = $hash
    ArchiveBytes = (Get-Item -LiteralPath $archivePath).Length
    PackagedFiles = $packageFiles.Count
    ExecutableVersion = $versionInfo.ProductVersion
} | Format-List
