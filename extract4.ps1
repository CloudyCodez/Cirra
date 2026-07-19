$src = @"
using System;
using System.IO;
using System.Text;
class X {
    static void Main(string[] args) {
        var bytes = File.ReadAllBytes(args[0]);
        // .NET 10 changed the bundle magic - search for "CloudBerry.dll" string instead
        // and work backwards to find the manifest
        var target = Encoding.UTF8.GetBytes("CloudBerry.dll");
        Console.WriteLine("Searching for CloudBerry.dll string...");
        for (int i = bytes.Length - target.Length; i >= 0; i--) {
            bool ok = true;
            for (int j = 0; j < target.Length && ok; j++) if (bytes[i+j] != target[j]) ok = false;
            if (ok) {
                Console.WriteLine("Found at offset: " + i + " (0x" + i.ToString("X") + ")");
                // Print 64 bytes before and after for inspection
                int start = Math.Max(0, i - 64);
                int end = Math.Min(bytes.Length, i + target.Length + 64);
                Console.Write("HEX_BEFORE:");
                for (int k = start; k < i; k++) Console.Write(bytes[k].ToString("X2") + " ");
                Console.WriteLine();
                Console.Write("HEX_AFTER:");
                for (int k = i + target.Length; k < end; k++) Console.Write(bytes[k].ToString("X2") + " ");
                Console.WriteLine();
                // Try to read as a bundle entry: 8 bytes offset + 8 bytes size before name
                if (i >= 21) {
                    int nameLen = BitConverter.ToInt32(bytes, i - 4);
                    Console.WriteLine("nameLen_before=" + nameLen);
                    long sz = BitConverter.ToInt64(bytes, i - 20);
                    long off = BitConverter.ToInt64(bytes, i - 28);
                    Console.WriteLine("possible_off=" + off + " possible_sz=" + sz);
                    if (sz > 100000 && sz < 50000000 && off > 0 && off < bytes.Length) {
                        Console.WriteLine("EXTRACTING from off=" + off + " sz=" + sz);
                        var dll = new byte[sz];
                        Array.Copy(bytes, off, dll, 0, sz);
                        File.WriteAllBytes(args[1], dll);
                        Console.WriteLine("EXTRACTED:" + sz);
                    }
                }
                break;
            }
        }
    }
}
"@
$tmp = "C:\Temp\extractor"
$src | Out-File "$tmp\x.cs" -Encoding UTF8
$csc = Get-ChildItem "C:\Windows\Microsoft.NET\Framework64" -Filter "csc.exe" -Recurse | Sort-Object FullName | Select-Object -Last 1
& $csc.FullName /out:"$tmp\x.exe" "$tmp\x.cs" /nologo
New-Item -ItemType Directory -Force -Path "C:\Temp\cb_extracted" | Out-Null
& "$tmp\x.exe" "C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe" "C:\Temp\cb_extracted\CloudBerry.dll"
if (Test-Path "C:\Temp\cb_extracted\CloudBerry.dll") {
    Write-Host "DLL extracted! Decompiling..."
    & "C:\Temp\ilspy\ilspycmd.exe" "C:\Temp\cb_extracted\CloudBerry.dll" -t "CloudBerry.App.ViewModels.MainViewModel" -o "C:\Temp\cb_out"
    Write-Host "Result: $((Get-Item 'C:\Temp\cb_out\CloudBerry.App.ViewModels.MainViewModel.decompiled.cs' -EA SilentlyContinue).Length) bytes"
} else { Write-Host "DLL not extracted" }
