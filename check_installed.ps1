Add-Type -AssemblyName System.IO.Compression.FileSystem

$installedExe = 'C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe'
$tempDir = 'C:\Users\conno\AppData\Local\Temp\cb_decompile'
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

# .NET single-file apps embed everything in the exe - extract using the bundle format
# Read the file and search for key strings
$bytes = [System.IO.File]::ReadAllBytes($installedExe)
$str = [System.Text.Encoding]::UTF8.GetString($bytes)

# Search for our specific strings
$markers = @('StartupView', 'StartupViewIsCloudscape', 'IsCloudscapeCompact', 'SchedulePersist', '_startupView', 'StartupViewIsCloudscape')
foreach ($m in $markers) {
    $idx = $str.IndexOf($m)
    Write-Host "$m : index=$idx"
}

Write-Host "File size: $($bytes.Length) bytes"
