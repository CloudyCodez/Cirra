using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CloudBerry.Core.Services;

/// <summary>Stages complete PDFs locally before handing them to the native PDF viewer.</summary>
public sealed class GuideDocumentService
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly HttpClient _http;
    private readonly string _root;
    private const long MaximumBytes = 512L * 1024 * 1024;

    public GuideDocumentService(HttpClient? httpClient = null, string? cacheRoot = null)
    {
        _http = httpClient ?? SharedClient;
        _root = cacheRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloudBerry", "guides");
    }

    public async Task<string> PrepareAsync(string location, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        cancellationToken = deadline.Token;
        if (string.IsNullOrWhiteSpace(location)) throw new InvalidDataException("This guide has no document location.");
        Uri.TryCreate(location, UriKind.Absolute, out var uri);
        if (uri is null || uri.IsFile)
        {
            string local = uri?.LocalPath ?? location;
            if (!File.Exists(local)) throw new FileNotFoundException("The attached PDF was moved or removed. Attach it again.");
            await ValidateAsync(local, cancellationToken);
            return Path.GetFullPath(local);
        }
        if (uri.Scheme is not ("http" or "https")) throw new InvalidDataException("Only local PDFs and HTTP(S) document links are supported.");

        Directory.CreateDirectory(_root);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        string destination = Path.Combine(_root, key + ".pdf");
        if (File.Exists(destination))
        {
            try { await ValidateAsync(destination, cancellationToken); return destination; }
            catch (InvalidDataException) { /* Replace an incomplete cache entry only after a successful download. */ }
        }

        string temporary = Path.Combine(_root, key + "." + Guid.NewGuid().ToString("N") + ".part");
        try
        {
            progress?.Report("Connecting to the document source...");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Cirra/1.0");
            request.Headers.Accept.ParseAdd("application/pdf, application/octet-stream;q=0.9");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new InvalidDataException("This source requires permission or a loan. Open Source to check availability; Cirra cannot download restricted books.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidDataException("The source removed or moved this document. Discover guides again or attach a local PDF.");
            response.EnsureSuccessStatusCode();
            long? expected = response.Content.Headers.ContentLength;
            if (expected > MaximumBytes) throw new InvalidDataException("This document exceeds the 512 MB download limit. Download it yourself and attach the local PDF.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                long lastReport = -1;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += count;
                    if (total > MaximumBytes) throw new InvalidDataException("This document exceeds the 512 MB download limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    if (total / (1024 * 1024) != lastReport)
                    {
                        lastReport = total / (1024 * 1024);
                        progress?.Report(expected > 0 ? $"Downloading PDF: {total * 100 / expected}%" : $"Downloading PDF: {lastReport} MB");
                    }
                }
                if (expected.HasValue && expected != total) throw new InvalidDataException("The download ended early. Retry to download the complete PDF.");
            }
            progress?.Report("Checking the downloaded PDF...");
            await ValidateAsync(temporary, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task ValidateAsync(string path, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var header = new byte[(int)Math.Min(1024, file.Length)];
        await file.ReadExactlyAsync(header, token);
        if (!Encoding.ASCII.GetString(header).TrimStart('\uFEFF', ' ', '\r', '\n', '\t').StartsWith("%PDF-", StringComparison.Ordinal))
            throw new InvalidDataException("The source returned a web page or another file, not a PDF. Open Source to check access.");
        file.Seek(Math.Max(0, file.Length - 4096), SeekOrigin.Begin);
        var tail = new byte[(int)(file.Length - file.Position)];
        await file.ReadExactlyAsync(tail, token);
        if (!Encoding.ASCII.GetString(tail).Contains("%%EOF", StringComparison.Ordinal))
            throw new InvalidDataException("This PDF appears incomplete. Retry the download or attach a complete local copy.");
    }
}
