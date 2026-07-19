# Extract the CloudBerry DLL from the update exe
$exe = 'C:\Users\conno\Documents\CloudBerry\update1.1.6.exe'
$out = 'C:\Users\conno\AppData\Local\Temp\cb_extract'

# Try NSIS extraction with 7zip
& 'C:\Program Files\7-Zip\7z.exe' x $exe -o"$out" -y 2>&1
if (-not $?) {
    # Fallback: try innounp for Inno Setup installers  
    Write-Host "7zip failed, exe may be InnoSetup or other format"
}

# List what was extracted
if (Test-Path $out) {
    Get-ChildItem $out -Recurse | Select-Object FullName, Length | Sort-Object Length -Descending | Select-Object -First 20
}
