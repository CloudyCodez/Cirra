using System.IO;
using System.Windows.Media.Imaging;
using PDFiumSharp;

namespace CloudBerry.App.Services;

/// <summary>Renders original PDF pages as bitmaps for Cirra's page-turn reader.</summary>
internal sealed class PdfSpreadRenderer : IDisposable
{
    private readonly PdfDocument _document;
    private readonly Dictionary<uint, BitmapSource> _cache = [];
    private static readonly object NativeLock = new();
    private bool _disposed;

    private PdfSpreadRenderer(PdfDocument document) => _document = document;

    public uint PageCount { get; private init; }
    public bool HasWidePages { get; private init; }

    public static async Task<PdfSpreadRenderer> OpenAsync(string localPdfPath)
    {
        return await Task.Run(() =>
        {
            lock (NativeLock)
            {
                var document = new PdfDocument(Path.GetFullPath(localPdfPath));
                if (document.Pages.Count == 0)
                {
                    document.Dispose();
                    throw new InvalidDataException("The PDF has no pages.");
                }
                return new PdfSpreadRenderer(document)
                {
                    PageCount = (uint)document.Pages.Count,
                    HasWidePages = document.Pages[0].Width > document.Pages[0].Height * 1.15
                };
            }
        });
    }

    public async Task<BitmapSource?> RenderPageAsync(uint pageNumber, uint destinationHeight, CancellationToken cancellationToken)
    {
        if (pageNumber is 0 || pageNumber > PageCount) return null;
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            // PDFium is process-global; serialize rendering and document disposal.
            lock (NativeLock)
            {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_cache.TryGetValue(pageNumber, out BitmapSource? cached) && cached.PixelHeight == (int)Math.Clamp(destinationHeight, 600u, 2400u)) return cached;
            PdfPage page = _document.Pages[(int)pageNumber - 1];
            int height = (int)Math.Clamp(destinationHeight, 600u, 2400u);
            int width = Math.Max(300, (int)Math.Round(height * page.Width / page.Height));
            var image = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            page.Render(image, PageOrientations.Normal, RenderingFlags.LcdText);
            image.Freeze();
            cancellationToken.ThrowIfCancellationRequested();
            if (_cache.Count >= 6) _cache.Remove(_cache.Keys.First());
            _cache[pageNumber] = image;
            return (BitmapSource)image;
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        lock (NativeLock)
        {
            if (_disposed) return;
            _disposed = true;
            _cache.Clear();
            _document.Dispose();
        }
    }
}
