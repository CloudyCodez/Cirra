$exePath = "C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe"
$extractDir = "C:\Temp\cb_extracted"
$ilspyDir = "C:\Temp\ilspy"
$outDir = "C:\Temp\cb_out"
New-Item -ItemType Directory -Force -Path $extractDir,$ilspyDir,$outDir | Out-Null
$bytes = [System.IO.File]::ReadAllBytes($exePath)
Write-Host "File size: $($bytes.Length) bytes"
$magic = [byte[]](0x8B,0x5D,0x4F,0x46,0x5F,0x63,0xB1,0xAB)
$magicPos = -1
for ($i = $bytes.Length - 8; $i -ge $bytes.Length - 65536; $i--) {
    $ok = $true
    for ($j = 0; $j -lt 8 -and $ok; $j++) { if ($bytes[$i+$j] -ne $magic[$j]) { $ok = $false } }
    if ($ok) { $magicPos = $i; break }
}
if ($magicPos -lt 0) {
    Write-Host "Not in last 64KB, using Array::IndexOf scan..."
    $span = New-Object System.ReadOnlySpan[byte] (,$bytes)
    $magicSpan = New-Object System.ReadOnlySpan[byte] (,$magic)
    $magicPos = [System.MemoryExtensions]::LastIndexOf($span, $magicSpan)
}
if ($magicPos -lt 0) { Write-Host "ERROR: magic not found"; exit 1 }
Write-Host "Magic at: 0x$($magicPos.ToString('X'))"
$headerOffset = [BitConverter]::ToInt64($bytes, $magicPos - 8)
Write-Host "Header offset: $headerOffset"
if ($headerOffset -lt 0 -or $headerOffset -gt $bytes.Length) { Write-Host "Bad header offset, trying -16..."; $headerOffset = [BitConverter]::ToInt64($bytes, $magicPos - 16) }
Write-Host "Using header offset: $headerOffset"
$p = [int]$headerOffset
$majorVer = [BitConverter]::ToUInt32($bytes, $p); $p += 4
$minorVer = [BitConverter]::ToUInt32($bytes, $p); $p += 4
$fileCount = [BitConverter]::ToInt32($bytes, $p); $p += 4
Write-Host "Bundle v$majorVer.$minorVer, Files: $fileCount"
if ($fileCount -lt 1 -or $fileCount -gt 500) { Write-Host "Bad file count, aborting"; exit 1 }
$idLen = [BitConverter]::ToInt32($bytes, $p); $p += 4 + $idLen
if ($majorVer -ge 2) { $p += 8 }
for ($f = 0; $f -lt $fileCount; $f++) {
    $offset = [BitConverter]::ToInt64($bytes, $p); $p += 8
    $size   = [BitConverter]::ToInt64($bytes, $p); $p += 8
    if ($majorVer -ge 6) { $p += 8 }
    $type = $bytes[$p]; $p += 1
    $nameLen = [BitConverter]::ToInt32($bytes, $p); $p += 4
    $name = [Text.Encoding]::UTF8.GetString($bytes, $p, $nameLen); $p += $nameLen
    Write-Host "  [$f] $name  off=$offset  sz=$size"
    if ($name -match "CloudBerry\.dll" -and $size -gt 100000 -and $size -lt 50000000) {
        $dllBytes = New-Object byte[] $size
        [Array]::Copy($bytes, $offset, $dllBytes, 0, $size)
        [IO.File]::WriteAllBytes("$extractDir\CloudBerry.dll", $dllBytes)
        Write-Host "  --> Extracted $size bytes"
    }
}
if (Test-Path "$extractDir\CloudBerry.dll") {
    Write-Host "Decompiling MainViewModel..."
    & "$ilspyDir\ilspycmd.exe" "$extractDir\CloudBerry.dll" -t "CloudBerry.App.ViewModels.MainViewModel" -o $outDir
    $f = "$outDir\CloudBerry.App.ViewModels.MainViewModel.decompiled.cs"
    Write-Host "Result: $((Get-Item $f -EA SilentlyContinue).Length) bytes"
} else { Write-Host "DLL not extracted" }
