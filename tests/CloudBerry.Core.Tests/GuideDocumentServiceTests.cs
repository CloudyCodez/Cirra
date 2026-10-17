using System.Net;
using System.Text;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class GuideDocumentServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CirraGuideTests", Guid.NewGuid().ToString("N"));
    private const string PdfEnvelope = "%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\n%%EOF\n";

    [Fact]
    public async Task DownloadsAttachmentAsPdfAndReusesLocalCopyOffline()
    {
        int calls = 0;
        using var client = Client(_ =>
        {
            calls++;
            var response = Response(PdfEnvelope, "application/octet-stream");
            response.Content.Headers.ContentDisposition = new("attachment");
            return response;
        });
        var service = new GuideDocumentService(client, _root);
        var path = await service.PrepareAsync("https://example.org/guide.pdf");
        Assert.Equal(PdfEnvelope, await File.ReadAllTextAsync(path));
        Assert.Equal(path, await service.PrepareAsync("https://example.org/guide.pdf"));
        Assert.Equal(1, calls);
        Assert.Empty(Directory.GetFiles(_root, "*.part"));
    }

    [Theory]
    [InlineData("<html>Sign in to borrow this book</html>")]
    [InlineData("%PDF-1.7\ntruncated")]
    [InlineData("")]
    public async Task RejectsHtmlAndIncompleteDownloadsWithoutCaching(string body)
    {
        using var client = Client(_ => Response(body));
        var service = new GuideDocumentService(client, _root);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.PrepareAsync("https://example.org/guide.pdf"));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ReportsUnavailableSourcesWithoutCaching(HttpStatusCode status)
    {
        using var client = Client(_ => new HttpResponseMessage(status));
        await Assert.ThrowsAsync<InvalidDataException>(() => new GuideDocumentService(client, _root).PrepareAsync("https://example.org/guide.pdf"));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task LocalFilesNeverNeedNetworkAndMissingFilesAreExplained()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "manual with spaces.pdf");
        await File.WriteAllTextAsync(path, PdfEnvelope);
        using var client = Client(_ => throw new InvalidOperationException("No network expected"));
        var service = new GuideDocumentService(client, _root);
        Assert.Equal(path, await service.PrepareAsync(path));
        Assert.Equal(path, await service.PrepareAsync(new Uri(path).AbsoluteUri));
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.PrepareAsync(Path.Combine(_root, "missing.pdf")));
    }

    [Fact]
    public async Task CorruptCachedCopyIsReplacedAndCancellationLeavesNoPartialFile()
    {
        using var client = Client(_ => Response(PdfEnvelope));
        var service = new GuideDocumentService(client, _root);
        string path = await service.PrepareAsync("https://example.org/manual.pdf");
        await File.WriteAllTextAsync(path, "broken");
        Assert.Equal(path, await service.PrepareAsync("https://example.org/manual.pdf"));
        Assert.Equal(PdfEnvelope, await File.ReadAllTextAsync(path));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync("https://example.org/other.pdf", cancellationToken: cancellation.Token));
        Assert.Empty(Directory.GetFiles(_root, "*.part"));
    }

    private static HttpResponseMessage Response(string text, string type = "application/pdf") => new(HttpStatusCode.OK)
    { Content = new StringContent(text, Encoding.ASCII, type) };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response));
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
