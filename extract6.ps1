$src = @"
using System;
using System.IO;
class X {
    static void Main(string[] args) {
        var b = File.ReadAllBytes(args[0]);
        // At 0x4B2E4D4 = 78832852 is "CloudBerry.dll"
        // PRE bytes ending just before name:
        // 00 E0 0F 01 00 00 00 00 00  E0 72 00 00 00 00 00 00  00 00 00 00 00 00 00  01  0E  [0E = nameLen 14]
        // So structure going backwards from name start (78832852):
        //   -1 = type byte = 0x01 (managed assembly)
        //   -2 = nameLen low byte = 0x0E = 14  (that's "CloudBerry.dll" = 14 chars)
        // Wait - 0x01 0x0E before name = 7-bit encoded nameLen = 0x010E? No...
        // Actually: byte before name = 0x0E = nameLen=14. byte before that = 0x01 = type.
        // Before type (8 bytes) = compressed size: 00 00 00 00 00 00 00 00
        // Before that (8 bytes) = size:            E0 72 00 00 00 00 00 00 = 29408
        // Before that (8 bytes) = offset:          00 E0 0F 01 00 00 00 00 = 17817600
        int nameStart = 78832852;
        int p = nameStart - 1; // type
        byte typ = b[p]; p--;
        byte nameLenByte = b[p]; p--; // this is actually AFTER type in real layout
        // Real layout: offset(8) size(8) compressedSize(8) type(1) nameLen(7bit) name
        // Going backwards: name starts at 78832852, nameLen=14 (0x0E)
        // byte at 78832852-1 = 0x0E = nameLen (stored as 7-bit encoded, single byte since <128)
        // byte at 78832852-2 = 0x01 = type
        // bytes at 78832852-10 = 8 bytes compressedSize
        // bytes at 78832852-18 = 8 bytes size  
        // bytes at 78832852-26 = 8 bytes offset
        int nameLen = b[nameStart - 1];
        int typeB   = b[nameStart - 2];
        long compSz = BitConverter.ToInt64(b, nameStart - 10);
        long size   = BitConverter.ToInt64(b, nameStart - 18);
        long offset = BitConverter.ToInt64(b, nameStart - 26);
        Console.WriteLine("nameLen=" + nameLen + " type=" + typeB);
        Console.WriteLine("offset=" + offset + " size=" + size + " compSz=" + compSz);
        if (size > 100000 && size < 50000000 && offset > 0 && offset+size <= b.Length) {
            var dll = new byte[size];
            Array.Copy(b, offset, dll, 0, size);
            File.WriteAllBytes(args[1], dll);
            Console.WriteLine("EXTRACTED " + size + " bytes from offset " + offset);
        } else {
            // Try offset with compSz meaning uncompressed
            Console.WriteLine("Trying alternate layout (no compSz field)...");
            size   = BitConverter.ToInt64(b, nameStart - 10);
            offset = BitConverter.ToInt64(b, nameStart - 18);
            Console.WriteLine("offset=" + offset + " size=" + size);
            if (size > 100000 && size < 50000000 && offset > 0 && offset+size <= b.Length) {
                var dll = new byte[size];
                Array.Copy(b, offset, dll, 0, size);
                File.WriteAllBytes(args[1], dll);
                Console.WriteLine("EXTRACTED " + size + " bytes from offset " + offset);
            } else { Console.WriteLine("FAILED"); }
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
    Write-Host "Decompiling..."
    & "C:\Temp\ilspy\ilspycmd.exe" "C:\Temp\cb_extracted\CloudBerry.dll" -t "CloudBerry.App.ViewModels.MainViewModel" -o "C:\Temp\cb_out"
    Write-Host "Result: $((Get-Item 'C:\Temp\cb_out\CloudBerry.App.ViewModels.MainViewModel.decompiled.cs' -EA SilentlyContinue).Length) bytes"
} else { Write-Host "DLL not extracted" }
