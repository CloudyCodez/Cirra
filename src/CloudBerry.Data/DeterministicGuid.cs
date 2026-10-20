using System.Security.Cryptography;
using System.Text;

namespace CloudBerry.Data;

internal static class DeterministicGuid
{
    public static Guid Create(string scope, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"cirra:{scope}:{value}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
