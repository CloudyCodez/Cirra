using System.Buffers;
using System.Security.Cryptography;

namespace CloudBerry.Core.Services;

public static class LibraryArtifactFingerprint
{
    private const int ChunkSize = 128 * 1024;

    public static async Task<string> ComputeQuickHashAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            ChunkSize,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        var length = stream.Length;
        var firstLength = (int)Math.Min(length, ChunkSize);
        var lastLength = length <= ChunkSize ? 0 : (int)Math.Min(length - firstLength, ChunkSize);
        var buffer = ArrayPool<byte>.Shared.Rent(firstLength + lastLength + sizeof(long));
        try
        {
            BitConverter.TryWriteBytes(buffer.AsSpan(0, sizeof(long)), length);
            await stream.ReadExactlyAsync(
                buffer.AsMemory(sizeof(long), firstLength),
                cancellationToken);
            if (lastLength > 0)
            {
                stream.Seek(-lastLength, SeekOrigin.End);
                await stream.ReadExactlyAsync(
                    buffer.AsMemory(sizeof(long) + firstLength, lastLength),
                    cancellationToken);
            }

            var hash = SHA256.HashData(buffer.AsSpan(0, sizeof(long) + firstLength + lastLength));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
