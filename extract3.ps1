$src = @"
using System;
using System.IO;
using System.Text;
class X {
    static void Main(string[] args) {
        var bytes = File.ReadAllBytes(args[0]);
        var magic = new byte[]{0x8B,0x5D,0x4F,0x46,0x5F,0x63,0xB1,0xAB};
        int pos = -1;
        for (int i = bytes.Length - 8; i >= 0; i--) {
            bool ok = true;
            for (int j = 0; j < 8 && ok; j++) if (bytes[i+j] != magic[j]) ok = false;
            if (ok) { pos = i; break; }
        }
        if (pos < 0) { Console.WriteLine("MAGIC_NOT_FOUND"); return; }
        Console.WriteLine("MAGIC:" + pos);
        long hdrOff = BitConverter.ToInt64(bytes, pos - 8);
        Console.WriteLine("HDR:" + hdrOff);
        if (hdrOff < 0 || hdrOff >= bytes.Length) { Console.WriteLine("BAD_HDR"); return; }
        int p = (int)hdrOff;
        uint maj = BitConverter.ToUInt32(bytes, p); p+=4;
        uint min = BitConverter.ToUInt32(bytes, p); p+=4;
        int fc  = BitConverter.ToInt32(bytes, p);  p+=4;
        Console.WriteLine("VER:" + maj + "." + min + " FILES:" + fc);
        if (fc < 1 || fc > 500) { Console.WriteLine("BAD_FC"); return; }
        int idLen = BitConverter.ToInt32(bytes, p); p += 4 + idLen;
        if (maj >= 2) p += 8;
        for (int f = 0; f < fc; f++) {
            long off  = BitConverter.ToInt64(bytes, p); p+=8;
            long size = BitConverter.ToInt64(bytes, p); p+=8;
            if (maj >= 6) p+=8;
            p+=1;
            int nl = BitConverter.ToInt32(bytes, p); p+=4;
            string name = Encoding.UTF8.GetString(bytes, p, nl); p+=nl;
            Console.WriteLine("FILE:" + name + "|" + off + "|" + size);
            if (name == "CloudBerry.dll" && size > 100000 && size < 50000000) {
                var dll = new byte[size];
                Array.Copy(bytes, off, dll, 0, size);
                File.WriteAllBytes(args[1], dll);
                Console.WriteLine("EXTRACTED:" + size);
            }
        }
    }
}
"@
$tmp = "C:\Temp\extractor"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$src | Out-File "$tmp\x.cs" -Encoding UTF8
$csc = Get-ChildItem "C:\Windows\Microsoft.NET\Framework64" -Filter "csc.exe" -Recurse | Sort-Object FullName | Select-Object -Last 1
if ($csc) {
    Write-Host "Using csc: $($csc.FullName)"
    & $csc.FullName /out:"$tmp\x.exe" "$tmp\x.cs" /nologo
    New-Item -ItemType Directory -Force -Path "C:\Temp\cb_extracted" | Out-Null
    & "$tmp\x.exe" "C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe" "C:\Temp\cb_extracted\CloudBerry.dll"
} else { Write-Host "csc not found" }
if (Test-Path "C:\Temp\cb_extracted\CloudBerry.dll") {
    Write-Host "Decompiling..."
    & "C:\Temp\ilspy\ilspycmd.exe" "C:\Temp\cb_extracted\CloudBerry.dll" -t "CloudBerry.App.ViewModels.MainViewModel" -o "C:\Temp\cb_out"
    Write-Host "Result: $((Get-Item 'C:\Temp\cb_out\CloudBerry.App.ViewModels.MainViewModel.decompiled.cs' -EA SilentlyContinue).Length) bytes"
} else { Write-Host "DLL not extracted" }
