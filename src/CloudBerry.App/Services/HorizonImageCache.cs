using System.Windows.Media.Imaging;
using System.IO;
using System.Net.Http;

namespace CloudBerry.App.Services;

public sealed class HorizonImageCache
{
	private const int MaximumSourceBytes = 64 * 1024 * 1024;
	private static readonly HttpClient HttpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

    private sealed record CacheEntry(BitmapImage Image, long EstimatedBytes, long LastAccess);

    private readonly object _sync = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly long _byteBudget;
    private long _estimatedBytes;
    private long _accessSequence;

    public HorizonImageCache(long byteBudget = 192L * 1024 * 1024)
    {
        _byteBudget = Math.Max(32L * 1024 * 1024, byteBudget);
    }

    public async Task<BitmapImage?> LoadAsync(
        string? source,
        int decodePixelWidth,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var key = $"{source.Trim()}|{Math.Max(0, decodePixelWidth)}";
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var cached))
            {
                _entries[key] = cached with { LastAccess = ++_accessSequence };
                return cached.Image;
            }
        }

		byte[] sourceBytes;
        try
        {
			sourceBytes = await ReadSourceAsync(source.Trim(), cancellationToken).ConfigureAwait(false);
			if (sourceBytes.Length == 0)
			{
				return null;
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return null;
		}

		BitmapImage? image;
		try
		{
			image = await Task.Run(() => Decode(sourceBytes, decodePixelWidth, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
		catch (OperationCanceledException)
        {
			throw;
		}
		catch
		{
			return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (image is null)
        {
            return null;
        }

        var estimatedBytes = Math.Max(1L, image.PixelWidth) * Math.Max(1L, image.PixelHeight) * 4L;
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var raced))
            {
                return raced.Image;
            }

            _entries[key] = new CacheEntry(image, estimatedBytes, ++_accessSequence);
            _estimatedBytes += estimatedBytes;
            TrimUnsafe();
        }

        return image;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
            _estimatedBytes = 0;
        }
    }

	private static async Task<byte[]> ReadSourceAsync(string source, CancellationToken cancellationToken)
	{
		if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
			(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
		{
			using var response = await HttpClient.GetAsync(
				uri,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			if (response.Content.Headers.ContentLength is > MaximumSourceBytes)
			{
				return [];
			}

			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			return await ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
		}

		var localPath = Uri.TryCreate(source, UriKind.Absolute, out var fileUri) && fileUri.IsFile
			? fileUri.LocalPath
			: source;
		var file = new FileInfo(localPath);
		if (!file.Exists || file.Length > MaximumSourceBytes)
		{
			return [];
		}

		await using var local = new FileStream(
			localPath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete,
			81920,
			FileOptions.Asynchronous | FileOptions.SequentialScan);
		return await ReadBoundedAsync(local, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
	{
		using var buffer = new MemoryStream();
		var chunk = new byte[81920];
		while (true)
		{
			var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
			if (read == 0)
			{
				return buffer.ToArray();
			}

			if (buffer.Length + read > MaximumSourceBytes)
			{
				return [];
			}
			buffer.Write(chunk, 0, read);
		}
	}

    private static BitmapImage? Decode(byte[] sourceBytes, int decodePixelWidth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
		using var stream = new MemoryStream(sourceBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
		image.StreamSource = stream;
        if (decodePixelWidth > 0)
        {
            image.DecodePixelWidth = decodePixelWidth;
        }

        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        cancellationToken.ThrowIfCancellationRequested();
        image.Freeze();
        return image;
    }

    private void TrimUnsafe()
    {
        while (_estimatedBytes > _byteBudget && _entries.Count > 1)
        {
            var oldest = _entries.MinBy(pair => pair.Value.LastAccess);
            if (oldest.Key is null || !_entries.Remove(oldest.Key, out var removed))
            {
                return;
            }

            _estimatedBytes -= removed.EstimatedBytes;
        }
    }
}
