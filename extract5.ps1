$src = @"
using System;
using System.IO;
using System.Text;
class X {
    static void Main(string[] args) {
        var bytes = File.ReadAllBytes(args[0]);
        // We know CloudBerry.dll is at offset 0x4B2E4D4 (78832852)
        // From hex after: 00 C0 82 01 = next entry offset data
        // The entry before shows: 00 E0 0F 01 00 00 00 00 00 = size, 00 E0 72 00 00 00 00 00 = compressed
        // Let's scan ALL occurrences of CloudBerry.dll and try all entry layouts
        var target = Encoding.UTF8.GetBytes("CloudBerry.dll");
        for (int i = 0; i <= bytes.Length - target.Length; i++) {
            bool ok = true;
            for (int j = 0; j < target.Length && ok; j++) if (bytes[i+j] != target[j]) ok = false;
            if (!ok) continue;
            Console.WriteLine("Hit at " + i + " (0x" + i.ToString("X") + ")");
            // Print 80 bytes before
            Console.Write("PRE: ");
            for (int k = Math.Max(0,i-80); k < i; k++) Console.Write(bytes[k].ToString("X2")+" ");
            Console.WriteLine();
            Console.Write("POST: ");
            for (int k = i+target.Length; k < Math.Min(bytes.Length, i+target.Length+40); k++) Console.Write(bytes[k].ToString("X2")+" ");
            Console.WriteLine();
        }
    }
}
"@
$tmp = "C:\Temp\extractor"
$src | Out-File "$tmp\x.cs" -Encoding UTF8
$csc = Get-ChildItem "C:\Windows\Microsoft.NET\Framework64" -Filter "csc.exe" -Recurse | Sort-Object FullName | Select-Object -Last 1
& $csc.FullName /out:"$tmp\x.exe" "$tmp\x.cs" /nologo
& "$tmp\x.exe" "C:\Users\conno\AppData\Local\Programs\CloudBerry\CloudBerry.exe"
