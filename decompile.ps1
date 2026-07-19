# Use dotnet-bundle-extract to get the embedded DLL, then decompile with ILSpy

$ilspyDir = "C:\Temp\ilspy"
$outDir   = "C:\Temp\cb_out"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$dotnet = "C:\Program Files\dotnet\dotnet.exe"

# Install the bundle extractor tool
Write-Host "Installing SuperDump/bundle extract tool..."
& $dotnet tool install dotnet-bundle-extract --tool-path $outDir 2>&1

$extractTool = "$outDir\dotnet-bundle-extract.exe"
if (-not (Test-Path $extractTool)) {
    Write-Host "Trying alternative: dotnet-bundleextract"
    & $dotnet tool install dotnet-bundleextract --tool-path $outDir 2>&1
    $extractTool = "$outDir\dotnet-bundleextract.exe"
}

if (Test-Path $extractTool) {
    Write-Host "Extracting bundle..."
    & $extractTool "C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe" --output "$outDir\extracted" 2>&1
} else {
    # Manual extraction using the known .NET bundle format
    # The bundle manifest footer is at a known offset from end of file
    # Signature bytes for .NET 6+ single-file: the last 8 bytes before the offset contain the bundle header offset
    Write-Host "Manual extraction..."
    $bytes = [System.IO.File]::ReadAllBytes("C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe")
    
    # Search for the bundle signature (netstandard.dll or CloudBerry.dll name in the manifest)
    $sig = [System.Text.Encoding]::UTF8.GetBytes("CloudBerry.dll")
    for ($i = $bytes.Length - 1; $i -ge 0; $i--) {
        $match = $true
        for ($j = 0; $j -lt $sig.Length; $j++) {
            if ($bytes[$i + $j] -ne $sig[$j]) { $match = $false; break }
        }
        if ($match) {
            Write-Host "Found 'CloudBerry.dll' string at offset: $i"
            # The offset and size are stored as Int64 before/after the name
            # Read 8 bytes before position i to get the file offset
            $offsetBytes = $bytes[($i-16)..($i-9)]
            $sizeBytes   = $bytes[($i-8)..($i-1)]
            $fileOffset  = [BitConverter]::ToInt64($offsetBytes, 0)
            $fileSize    = [BitConverter]::ToInt64($sizeBytes, 0)
            Write-Host "  Offset: $fileOffset, Size: $fileSize"
            
            if ($fileOffset -gt 0 -and $fileOffset -lt $bytes.Length -and $fileSize -gt 0 -and $fileSize -lt 50MB) {
                $dll = $bytes[$fileOffset..($fileOffset + $fileSize - 1)]
                $dllPath = "$outDir\CloudBerry_extracted.dll"
                [System.IO.File]::WriteAllBytes($dllPath, $dll)
                Write-Host "Extracted $fileSize bytes to $dllPath"
                
                # Now decompile
                Write-Host "Decompiling MainViewModel..."
                & "$ilspyDir\ilspycmd.exe" $dllPath -t "CloudBerry.App.ViewModels.MainViewModel" -o "$outDir\decompiled" 2>&1
                Get-ChildItem "$outDir\decompiled" -Recurse -Filter "*.cs" | Select-Object FullName, Length
            }
            break
        }
    }
}
