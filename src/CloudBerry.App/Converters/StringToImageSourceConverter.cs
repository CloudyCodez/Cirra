using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CloudBerry.App.Converters;

public sealed class StringToImageSourceConverter : IValueConverter
{
    // Two-level cache: hot (strong ref) + cold (weak ref) for GC-friendly eviction.
    // Strong cache is capped so it never balloons — oldest entries are evicted when full.
    private static readonly Dictionary<string, ImageSource> HotCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> HotOrder = new();
    private static readonly Dictionary<string, LinkedListNode<string>> HotIndex = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, WeakReference<ImageSource>> ColdCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();
    private const int HotCacheCapacity = 80; // Keep the 80 most-recently-used images in RAM

    // Context-aware decode widths — only decode as many pixels as the UI actually needs.
    // This is the single biggest RAM saving: 1800px default was 8× larger than card size.
    private const int DecodeWidthCard       = 260;  // shelf cards (220px display + 2× buffer)
    private const int DecodeWidthHero       = 440;  // hero cover panel
    private const int DecodeWidthBackground = 820;  // full-bleed backgrounds
    private const int DecodeWidthDefault    = 260;  // safe fallback

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string source || string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            var decodeWidth = ResolveDecodePixelWidth(parameter);
            var cacheKey = $"{source}|{decodeWidth}";

            // 1. Check hot cache first (no alloc on hit)
            lock (CacheLock)
            {
                if (HotCache.TryGetValue(cacheKey, out var hot))
                {
                    // Promote to front (LRU)
                    if (HotIndex.TryGetValue(cacheKey, out var node))
                    {
                        HotOrder.Remove(node);
                        HotOrder.AddFirst(node);
                    }
                    return hot;
                }
            }

            // 2. Check cold cache (weak refs — GC may have collected)
            if (ColdCache.TryGetValue(cacheKey, out var weakRef) &&
                weakRef.TryGetTarget(out var cold))
            {
                Promote(cacheKey, cold);
                return cold;
            }

            // 3. Load from disk/network
            ImageSource loaded = File.Exists(source)
                ? LoadFromFile(Path.GetFullPath(source), decodeWidth)
                : LoadFromRemoteUri(new Uri(source, UriKind.Absolute), decodeWidth);

            Promote(cacheKey, loaded);
            ColdCache[cacheKey] = new WeakReference<ImageSource>(loaded);
            return loaded;
        }
        catch
        {
            return null;
        }
    }

    private static void Promote(string key, ImageSource source)
    {
        lock (CacheLock)
        {
            if (HotCache.ContainsKey(key))
                return;

            // Evict oldest if at capacity
            if (HotCache.Count >= HotCacheCapacity && HotOrder.Last is { } oldest)
            {
                HotCache.Remove(oldest.Value);
                HotIndex.Remove(oldest.Value);
                HotOrder.RemoveLast();
            }

            HotCache[key] = source;
            var node = HotOrder.AddFirst(key);
            HotIndex[key] = node;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static int ResolveDecodePixelWidth(object? parameter)
    {
        if (parameter is int w && w > 0) return w;
        if (parameter is string s)
        {
            if (s.Equals("hero",       StringComparison.OrdinalIgnoreCase)) return DecodeWidthHero;
            if (s.Equals("background", StringComparison.OrdinalIgnoreCase)) return DecodeWidthBackground;
            if (s.Equals("card",       StringComparison.OrdinalIgnoreCase)) return DecodeWidthCard;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0) return p;
        }
        return DecodeWidthDefault;
    }

    private static BitmapImage LoadFromFile(string path, int decodePixelWidth)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        // OnDemand + DelayCreation: EndInit() returns immediately without touching disk.
        // WPF decodes the image on the render thread when the control is first painted,
        // keeping this binding dispatch call off the UI thread critical path.
        bitmap.CacheOption = BitmapCacheOption.OnDemand;
        bitmap.CreateOptions = BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile;
        bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        // Cannot Freeze() an OnDemand bitmap before it is loaded — decoding happens lazily.
        return bitmap;
    }

    private static ImageSource LoadFromRemoteUri(Uri uri, int decodePixelWidth)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = uri;
        bitmap.CacheOption = BitmapCacheOption.OnDemand;
        bitmap.CreateOptions = BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile;
        bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        return bitmap;
    }
}
