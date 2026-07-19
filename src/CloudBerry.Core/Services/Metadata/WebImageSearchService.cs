using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Keyless web image search for manual media selection.
///
/// Source priority:
///   1. DuckDuckGo Images  - structured JSON via two-step vqd token.
///   2. Google Images      - AF_initDataCallback "ou" JSON field (most reliable 2025 pattern).
///   3. Bing Images        - HTML metadata blob fallback.
///
/// All sources use a realistic Chrome User-Agent, Accept headers, and gzip/br decompression.
/// The HttpClient is created with a cookie container so DDG session cookies persist across
/// the two-step token + image fetch.
/// </summary>
public sealed class WebImageSearchService
{
    private readonly HttpClient _httpClient;

    private const string ChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.6367.207 Safari/537.36";

    public WebImageSearchService(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.Brotli,
                AllowAutoRedirect = true,
                UseCookies        = true,
                CookieContainer   = new CookieContainer()
            };
            _httpClient = new HttpClient(handler);
        }

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",      ChromeUserAgent);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest",  "document");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode",  "navigate");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site",  "none");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<IReadOnlyList<MediaImageCandidate>> SearchAsync(
        string gameTitle,
        string mediaKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameTitle))
            return [];

        var queries    = BuildQueryVariants(gameTitle, mediaKind);
        var candidates = new List<MediaImageCandidate>();

        foreach (var query in queries)
        {
            var ddg = await SearchDuckDuckGoAsync(query, mediaKind, cancellationToken);
            candidates.AddRange(ddg);

            if (candidates.Count < 18)
            {
                var google = await SearchGoogleImagesAsync(query, mediaKind, cancellationToken);
                candidates.AddRange(google);
            }

            if (candidates.Count < 12)
            {
                var bing = await SearchBingImagesAsync(query, mediaKind, cancellationToken);
                candidates.AddRange(bing);
            }

            if (candidates.Count >= 40)
            {
                break;
            }
        }

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.ImageUrl))
            .Where(IsLikelyRenderableImage)
            .DistinctBy(c => c.ImageUrl, StringComparer.OrdinalIgnoreCase)
            .Select(c => { c.Score = ScoreCandidate(c, gameTitle, mediaKind); return c; })
            .Where(c => IsViableCandidate(c, mediaKind))
            .OrderByDescending(c => c.Score)
            .Take(40)
            .ToArray();
    }

    // -- DuckDuckGo --------------------------------------------------
    // DDG changes its vqd token format periodically. We try several extraction patterns
    // and also let the CookieContainer persist the session cookie from step 1 into step 2.

    private static readonly string[] VqdPatterns =
    [
        @"vqd=""([^""]+)""",              // vqd="TOKEN"
        @"vqd='([^']+)'",               // vqd='TOKEN'
        @"""vqd""\s*:\s*""([^""]+)""",  // JSON: "vqd":"TOKEN"
        @"vqd=([-\w]+)",                 // unquoted vqd=TOKEN
        @"data-vqd=[""']([^""']+)[""']", // data-vqd="" attr
    ];

    private async Task<IReadOnlyList<MediaImageCandidate>> SearchDuckDuckGoAsync(
        string query,
        string mediaKind,
        CancellationToken ct)
    {
        try
        {
            // Step 1 - obtain vqd session token from the image search landing page
            var initUrl = "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query) + "&ia=images&iax=images";
            using var initReq = new HttpRequestMessage(HttpMethod.Get, initUrl);
            initReq.Headers.TryAddWithoutValidation("Referer",        "https://duckduckgo.com/");
            initReq.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            var initResp = await _httpClient.SendAsync(initReq, ct);
            var initHtml = await initResp.Content.ReadAsStringAsync(ct);

            string? vqd = null;
            foreach (var pattern in VqdPatterns)
            {
                var m = Regex.Match(initHtml, pattern);
                if (m.Success)
                {
                    vqd = m.Groups[1].Value;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(vqd))
                return [];

            // Step 2 - fetch the JSON image results using the vqd token
            var apiUrl = "https://duckduckgo.com/i.js"
                + "?l=us-en&o=json"
                + "&q=" + Uri.EscapeDataString(query)
                + "&vqd=" + Uri.EscapeDataString(vqd)
                + "&f=,,,,,&p=1&s=0";

            using var apiReq = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            apiReq.Headers.TryAddWithoutValidation("Referer",          initUrl);
            apiReq.Headers.TryAddWithoutValidation("Accept",           "application/json, text/javascript, */*; q=0.01");
            apiReq.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            apiReq.Headers.TryAddWithoutValidation("Sec-Fetch-Site",   "same-origin");
            apiReq.Headers.TryAddWithoutValidation("Sec-Fetch-Mode",   "cors");
            apiReq.Headers.TryAddWithoutValidation("Sec-Fetch-Dest",   "empty");

            var apiResp = await _httpClient.SendAsync(apiReq, ct);
            if (!apiResp.IsSuccessStatusCode)
                return [];

            var json = await apiResp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json) || json.TrimStart().StartsWith('<'))
                return [];

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results))
                return [];

            var candidates = new List<MediaImageCandidate>();
            foreach (var r in results.EnumerateArray())
            {
                var imageUrl  = r.TryGetProperty("image",     out var img) ? img.GetString()  : null;
                var thumbUrl  = r.TryGetProperty("thumbnail", out var th)  ? th.GetString()   : null;
                var sourceUrl = r.TryGetProperty("url",       out var src) ? src.GetString()  : null;
                var title     = r.TryGetProperty("title",     out var t)   ? t.GetString()    : null;
                var width     = r.TryGetProperty("width",     out var w)   && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 0;
                var height    = r.TryGetProperty("height",    out var h)   && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0;

                if (string.IsNullOrWhiteSpace(imageUrl)) continue;

                candidates.Add(new MediaImageCandidate
                {
                    MediaKind    = mediaKind,
                    SourceName   = "DuckDuckGo Images",
                    Title        = title ?? query,
                    ImageUrl     = imageUrl,
                    ThumbnailUrl = thumbUrl ?? imageUrl,
                    SourceUrl    = sourceUrl ?? string.Empty,
                    Width        = width,
                    Height       = height
                });
            }

            return candidates;
        }
        catch
        {
            return [];
        }
    }

    // -- Google Images -----------------------------------------------
    // Google Images embeds full-res image data in AF_initDataCallback JS blobs.
    //
    // Pattern A  "ou":"URL","ow":W,"oh":H  - the most reliable field in 2024-2025 pages.
    // Pattern B  "ou":"URL"               - URL only; look for nearby [W,H] tuple.
    // Pattern C  ["URL",W,H]             - classic JS array form, still present on some pages.

    private async Task<IReadOnlyList<MediaImageCandidate>> SearchGoogleImagesAsync(
        string query,
        string mediaKind,
        CancellationToken ct)
    {
        try
        {
            var url = "https://www.google.com/search?tbm=isch&hl=en&safe=active&num=40&q="
                + Uri.EscapeDataString(query);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Referer",        "https://www.google.com/");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");

            var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return [];

            var html       = await resp.Content.ReadAsStringAsync(ct);
            var candidates = new List<MediaImageCandidate>();

            // Pattern A - "ou":"URL","ow":W,"oh":H  (most common in 2024-2025 Google pages)
            var matchesA = Regex.Matches(
                html,
                @"""ou""\s*:\s*""(https?://(?!encrypted-tbn)[^""\\]+\.(?:jpg|jpeg|png|webp)[^""\\]*)""[\s\S]{0,300}?""ow""\s*:\s*(\d+)[\s\S]{0,100}?""oh""\s*:\s*(\d+)",
                RegexOptions.IgnoreCase);

            foreach (Match m in matchesA)
            {
                var imgUrl = DecodeJsonUrl(m.Groups[1].Value);
                if (IsBlockedGoogleDomain(imgUrl)) continue;

                candidates.Add(new MediaImageCandidate
                {
                    MediaKind    = mediaKind,
                    SourceName   = "Google Images",
                    Title        = query,
                    ImageUrl     = imgUrl,
                    ThumbnailUrl = imgUrl,
                    Width        = ParseInt(m.Groups[2].Value),
                    Height       = ParseInt(m.Groups[3].Value)
                });
            }

            // Pattern B - "ou":"URL" only; try to find dimensions in nearby context
            if (candidates.Count < 8)
            {
                var matchesB = Regex.Matches(
                    html,
                    @"""ou""\s*:\s*""(https?://(?!encrypted-tbn)[^""\\]+\.(?:jpg|jpeg|png|webp)[^""\\]*)""",
                    RegexOptions.IgnoreCase);

                foreach (Match m in matchesB)
                {
                    var imgUrl = DecodeJsonUrl(m.Groups[1].Value);
                    if (IsBlockedGoogleDomain(imgUrl)) continue;
                    if (candidates.Any(c => string.Equals(c.ImageUrl, imgUrl, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Search for [W,H] within a small window around this match
                    var contextStart = Math.Max(0, m.Index - 64);
                    var contextLen   = Math.Min(512, html.Length - contextStart);
                    var context      = html.Substring(contextStart, contextLen);
                    var dimMatch     = Regex.Match(context, @"\[(\d{2,5}),(\d{2,5})\]");

                    candidates.Add(new MediaImageCandidate
                    {
                        MediaKind    = mediaKind,
                        SourceName   = "Google Images",
                        Title        = query,
                        ImageUrl     = imgUrl,
                        ThumbnailUrl = imgUrl,
                        Width        = dimMatch.Success ? ParseInt(dimMatch.Groups[1].Value) : 0,
                        Height       = dimMatch.Success ? ParseInt(dimMatch.Groups[2].Value) : 0
                    });
                }
            }

            // Pattern C - classic ["URL",W,H] JS array triples
            if (candidates.Count < 5)
            {
                var matchesC = Regex.Matches(
                    html,
                    @"\[""(https?://(?!encrypted-tbn)[^""\\]+\.(?:jpg|jpeg|png|webp)[^""\\]*)"",\s*(\d+),\s*(\d+)\]",
                    RegexOptions.IgnoreCase);

                foreach (Match m in matchesC)
                {
                    var imgUrl = DecodeJsonUrl(m.Groups[1].Value);
                    if (IsBlockedGoogleDomain(imgUrl)) continue;
                    if (candidates.Any(c => string.Equals(c.ImageUrl, imgUrl, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    candidates.Add(new MediaImageCandidate
                    {
                        MediaKind    = mediaKind,
                        SourceName   = "Google Images",
                        Title        = query,
                        ImageUrl     = imgUrl,
                        ThumbnailUrl = imgUrl,
                        Width        = ParseInt(m.Groups[2].Value),
                        Height       = ParseInt(m.Groups[3].Value)
                    });
                }
            }

            return candidates;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsBlockedGoogleDomain(string url) =>
        url.Contains("gstatic.com",           StringComparison.OrdinalIgnoreCase) ||
        url.Contains("googlelogo",            StringComparison.OrdinalIgnoreCase) ||
        url.Contains("google.com/images",     StringComparison.OrdinalIgnoreCase) ||
        url.Contains("googleapis.com/photos", StringComparison.OrdinalIgnoreCase);

    // -- Bing Images -------------------------------------------------

    private async Task<IReadOnlyList<MediaImageCandidate>> SearchBingImagesAsync(
        string query,
        string mediaKind,
        CancellationToken ct)
    {
        try
        {
            var url = "https://www.bing.com/images/search?q="
                + Uri.EscapeDataString(query)
                + "&form=HDRSC2&first=1&tsc=ImageHoverTitle"
                + ResolutionFilter(mediaKind);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Referer", "https://www.bing.com/");
            var resp = await _httpClient.SendAsync(req, ct);
            var html = await resp.Content.ReadAsStringAsync(ct);

            // Bing embeds metadata in  m="{...}" attributes on <a class="iusc"> elements.
            var matches = Regex.Matches(
                html,
                @"m=""{([^""]*?)}""",
                RegexOptions.IgnoreCase);

            var candidates = new List<MediaImageCandidate>();
            foreach (Match m in matches)
            {
                var blob = WebUtility.HtmlDecode("{" + m.Groups[1].Value + "}");

                var imageUrl = ExtractJsonString(blob, "murl");
                var thumbUrl = ExtractJsonString(blob, "turl");
                var pageUrl  = ExtractJsonString(blob, "purl");
                var wStr     = ExtractJsonString(blob, "\"w\"");
                var hStr     = ExtractJsonString(blob, "\"h\"");

                if (string.IsNullOrWhiteSpace(imageUrl)) continue;

                candidates.Add(new MediaImageCandidate
                {
                    MediaKind    = mediaKind,
                    SourceName   = "Bing Images",
                    Title        = query,
                    ImageUrl     = DecodeJsonUrl(imageUrl),
                    ThumbnailUrl = DecodeJsonUrl(thumbUrl),
                    SourceUrl    = DecodeJsonUrl(pageUrl),
                    Width        = ParseInt(wStr),
                    Height       = ParseInt(hStr)
                });
            }

            // Bing JSON array fallback
            if (candidates.Count == 0)
            {
                var fallback = Regex.Matches(
                    html,
                    @"""murl"":""(?<u>https?:[^""]+)"".+?""w"":(?<w>\d+).+?""h"":(?<h>\d+)",
                    RegexOptions.Singleline);

                foreach (Match m in fallback)
                {
                    candidates.Add(new MediaImageCandidate
                    {
                        MediaKind  = mediaKind,
                        SourceName = "Bing Images",
                        Title      = query,
                        ImageUrl   = DecodeJsonUrl(m.Groups["u"].Value),
                        Width      = ParseInt(m.Groups["w"].Value),
                        Height     = ParseInt(m.Groups["h"].Value)
                    });
                }
            }

            return candidates;
        }
        catch
        {
            return [];
        }
    }

    // -- Query building ----------------------------------------------

    private static string BuildQuery(string gameTitle, string mediaKind)
    {
        var title = gameTitle.Trim();
        return mediaKind.ToLowerInvariant() switch
        {
            "cover"      => $"{title} game cover art box art",
            "background" => $"{title} game wallpaper background art 1920x1080",
            "banner"     => $"{title} game banner header art",
            "icon"       => $"{title} game icon logo png transparent",
            _            => $"{title} game artwork"
        };
    }

    private static IReadOnlyList<string> BuildQueryVariants(string gameTitle, string mediaKind)
    {
        var title = gameTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        var primary = BuildQuery(title, mediaKind);
        var titleOnly = mediaKind.ToLowerInvariant() switch
        {
            "cover" => $"{title} box art",
            "background" => $"{title} key art wallpaper",
            "banner" => $"{title} header art",
            "icon" => $"{title} icon logo",
            _ => $"{title} artwork"
        };

        if (string.Equals(primary, titleOnly, StringComparison.OrdinalIgnoreCase))
        {
            return [primary];
        }

        return [primary, titleOnly];
    }

    private static string ResolutionFilter(string mediaKind) =>
        mediaKind.ToLowerInvariant() switch
        {
            "cover"      => "&qft=+filterui:imagesize-large",
            "background" => "&qft=+filterui:imagesize-wallpaper",
            "banner"     => "&qft=+filterui:imagesize-large",
            "icon"       => "&qft=+filterui:imagesize-medium",
            _            => string.Empty
        };

    // -- Filtering and scoring ---------------------------------------

    private static bool IsViableCandidate(MediaImageCandidate candidate, string mediaKind)
    {
        if (candidate.Width <= 0 || candidate.Height <= 0)
            return true;

        var ratio = candidate.Width / (double)candidate.Height;
        return mediaKind.ToLowerInvariant() switch
        {
            "cover"      => candidate.Width  >= 200 && candidate.Height >= 260 && ratio is >= 0.45 and <= 0.95,
            "background" => candidate.Width  >= 900 && candidate.Height >= 500 && ratio is >= 1.3  and <= 2.6,
            "banner"     => candidate.Width  >= 600 && candidate.Height >= 150 && ratio is >= 1.8  and <= 6.0,
            "icon"       => candidate.Width  >= 64  && candidate.Height >= 64  && ratio is >= 0.75 and <= 1.33,
            _            => candidate.Width  >= 150 && candidate.Height >= 150
        };
    }

    private static double ScoreCandidate(MediaImageCandidate candidate, string gameTitle, string mediaKind)
    {
        double ratioScore = 50, sizeScore = 50;

        if (candidate.Width > 0 && candidate.Height > 0)
        {
            var ratio = candidate.Width / (double)candidate.Height;
            var targetRatio = mediaKind.ToLowerInvariant() switch
            {
                "cover"      => 0.75,
                "background" => 16 / 9d,
                "banner"     => 3.1,
                "icon"       => 1.0,
                _            => ratio
            };
            ratioScore = Math.Max(0, 1 - Math.Abs(ratio - targetRatio) / targetRatio) * 100;
            sizeScore  = Math.Min(candidate.Width * candidate.Height / 25000d, 100);
        }

        var sourceBonus = candidate.SourceName == "DuckDuckGo Images" ? 8 : 0;
        var keywordScore = ScoreKeywordFit(candidate, gameTitle, mediaKind);
        return ratioScore * 0.45 + sizeScore * 0.2 + keywordScore * 0.3 + sourceBonus;
    }

    private static double ScoreKeywordFit(MediaImageCandidate candidate, string gameTitle, string mediaKind)
    {
        var haystack = string.Join(
            ' ',
            candidate.Title,
            candidate.ImageUrl,
            candidate.SourceUrl).ToLowerInvariant();

        var tokens = Tokenize(gameTitle)
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0) return 50;

        var matched = tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
        var score   = matched / (double)tokens.Length * 100;

        if (mediaKind == "icon"       && ContainsAny(haystack, "logo", "icon", "transparent", "png"))  score += 18;
        if (mediaKind == "background" && ContainsAny(haystack, "wallpaper", "background", "key art"))  score += 15;
        if (mediaKind == "cover"      && ContainsAny(haystack, "cover", "box", "boxart", "poster"))    score += 15;
        if (mediaKind == "banner"     && ContainsAny(haystack, "banner", "header", "hero"))            score += 15;

        if (mediaKind != "icon"  && ContainsAny(haystack, "favicon", "sprite"))                       score -= 25;
        if (mediaKind == "cover" && ContainsAny(haystack, "wallpaper", "1920", "background", "1080")) score -= 20;
        if (ContainsAny(haystack, "mockup", "template", "placeholder", "stock", "ad", "sponsored"))    score -= 20;
        if (ContainsAny(haystack, "wiki", "fandom", "guide", "walkthrough"))                           score -= 14;
        if (ContainsAny(haystack, "steamgriddb", "igdb", "mobygames", "launchbox-app", "rawg"))        score += 10;

        return Math.Clamp(score, 0, 100);
    }

    // -- Helpers -----------------------------------------------------

    private static IEnumerable<string> Tokenize(string value) =>
        Regex.Split(value ?? string.Empty, @"[^a-z0-9]+", RegexOptions.IgnoreCase)
             .Where(t => !string.IsNullOrWhiteSpace(t));

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static bool IsLikelyRenderableImage(MediaImageCandidate candidate)
    {
        return IsLikelyRenderableUrl(candidate.ThumbnailUrl) && IsLikelyRenderableUrl(candidate.ImageUrl);
    }

    private static bool IsLikelyRenderableUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var normalized = url.ToLowerInvariant();
        if (normalized.Contains("format=webp", StringComparison.Ordinal) ||
            normalized.Contains("fm=webp", StringComparison.Ordinal) ||
            normalized.Contains("image/webp", StringComparison.Ordinal) ||
            normalized.Contains("format=avif", StringComparison.Ordinal) ||
            normalized.Contains("fm=avif", StringComparison.Ordinal) ||
            normalized.Contains("image/avif", StringComparison.Ordinal))
        {
            return false;
        }

        var path = normalized;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath.ToLowerInvariant();
        }

        var extension = Path.GetExtension(path);
        return extension switch
        {
            ".webp" => false,
            ".avif" => false,
            ".svg" => false,
            ".gifv" => false,
            _ => true
        };
    }

    private static string DecodeJsonUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return WebUtility.HtmlDecode(
            value.Replace("\\/", "/",    StringComparison.Ordinal)
                 .Replace("\\u0026", "&", StringComparison.Ordinal));
    }

    private static string ExtractJsonString(string json, string key)
    {
        var pattern = Regex.Escape(key) + @"""?\s*:\s*""((?:[^""\\]|\\.)*)""";
        var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, out var n) ? n : 0;
}
