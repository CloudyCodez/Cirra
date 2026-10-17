using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Discovers original case manuals and published game guides from preservation-focused sources.
/// Results are scored against both game title and platform before being shown to the player.
/// </summary>
public sealed partial class GuidebookDiscoveryService
{
    private const string ArchiveBaseUrl = "https://archive.org";
    private const string GamesDatabaseBaseUrl = "https://www.gamesdatabase.org";
    private readonly HttpClient _httpClient;

    public GuidebookDiscoveryService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Cirra", "0.2"));
        }
    }

    public async Task<IReadOnlyList<GameGuidebook>> DiscoverAsync(
        string gameTitle,
        string? platform = null,
        int maxResults = 8,
        CancellationToken cancellationToken = default)
        => (await DiscoverDetailedAsync(gameTitle, platform, maxResults, cancellationToken)).Guides;

    public async Task<GuideDiscoveryResult> DiscoverDetailedAsync(
        string gameTitle, string? platform = null, int maxResults = 8, CancellationToken cancellationToken = default, string? steamAppId = null)
    {
        if (string.IsNullOrWhiteSpace(gameTitle))
        {
            return new GuideDiscoveryResult([], []);
        }

        string title = CleanGameTitle(gameTitle);
        string normalizedPlatform = platform?.Trim() ?? string.Empty;
        int resultLimit = Math.Clamp(maxResults, 1, 12);
        var scoredResults = new List<ScoredGuidebook>();
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        if (normalizedPlatform.Equals("Windows PC", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(steamAppId, out uint appId) && appId > 0)
        {
            try
            {
                var manual = await FindSteamManualAsync(title, appId, cancellationToken);
                if (manual is not null) scoredResults.Add(new ScoredGuidebook(manual, 200));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { failures.Add("Steam manual: " + ex.Message); }
        }

        if (GetNintendoManualIndex(normalizedPlatform) is not null)
        {
            try
            {
                foreach (var manual in await FindNintendoManualsAsync(title, normalizedPlatform, cancellationToken))
                    scoredResults.Add(new ScoredGuidebook(manual, 200));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { failures.Add("Nintendo Support: " + ex.Message); }
        }

        try
        {
            GameGuidebook? caseManual = await FindGamesDatabaseManualAsync(title, normalizedPlatform, cancellationToken);
            if (caseManual is not null)
            {
                scoredResults.Add(new ScoredGuidebook(caseManual, 160));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            failures.Add("Games Database: " + ex.Message);
        }

        var archiveCandidates = new Dictionary<string, ArchiveCandidate>(StringComparer.OrdinalIgnoreCase);
        // Many archives index a whole console's manuals as one item, not one item per game.
        if (normalizedPlatform.Equals("PSP", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string identifier in new[] { "SonyPSPManuals", "PSP-instruction-manuals-and-document-dat" })
                archiveCandidates[identifier] = new ArchiveCandidate(identifier, title + " Manual", "", "PSP", "consolemanuals", "", "", 145, true);
        }
        if (!string.IsNullOrWhiteSpace(normalizedPlatform) && !normalizedPlatform.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string systems = string.Join(" OR ", GetPlatformAliases(normalizedPlatform).Select(EscapeQueryValue));
                string query = $"mediatype:(texts OR software OR data) AND title:({systems}) AND title:(manuals OR \"manual collection\")";
                foreach (var candidate in (await SearchArchiveAsync(query, cancellationToken))
                    .Where(candidate => GetPlatformAliases(normalizedPlatform).Any(alias => ContainsPhrase(Normalize(candidate.Title), Normalize(alias))))
                    .Take(3))
                    archiveCandidates.TryAdd(candidate.Identifier, candidate with { Score = 135, IsBundle = true });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { failures.Add("Internet Archive manual collections: " + ex.Message); }
        }
        using var searchGate = new SemaphoreSlim(3, 3);
        var searches = BuildArchiveQueries(title).Select(async query =>
        {
            await searchGate.WaitAsync(cancellationToken);
            try
            {
                return await SearchArchiveAsync(query, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                failures.Add("Internet Archive search: " + ex.Message);
                return Array.Empty<ArchiveCandidate>();
            }
            finally { searchGate.Release(); }
        }).ToArray();
        foreach (var candidates in await Task.WhenAll(searches))
        {
                foreach (ArchiveCandidate candidate in candidates)
                {
                    double score = ScoreCandidate(title, normalizedPlatform, candidate);
                    if (score < 62)
                    {
                        continue;
                    }

                    if (!archiveCandidates.TryGetValue(candidate.Identifier, out ArchiveCandidate? existing) || score > existing.Score)
                    {
                        archiveCandidates[candidate.Identifier] = candidate with { Score = score };
                    }
                }
        }

        ArchiveCandidate[] finalists = archiveCandidates.Values
            .OrderByDescending(candidate => candidate.Score)
            .Take(Math.Min(16, resultLimit * 3))
            .ToArray();
        using var gate = new SemaphoreSlim(4, 4);
        Task<ScoredGuidebook[]>[] resolutionTasks = finalists.Select(async candidate =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var guidebooks = await ResolveArchivePdfAsync(candidate, title, normalizedPlatform, cancellationToken);
                return guidebooks.Select(guidebook => new ScoredGuidebook(guidebook, candidate.Score)).ToArray();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                failures.Add("Internet Archive document: " + ex.Message);
                return Array.Empty<ScoredGuidebook>();
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        foreach (var results in await Task.WhenAll(resolutionTasks))
        {
            scoredResults.AddRange(results);
        }

        var guides = scoredResults
            .GroupBy(result => result.Guidebook.PdfLocation, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(result => result.Score).First())
            .OrderBy(result => DocumentKindRank(result.Guidebook.DocumentKind))
            .ThenByDescending(result => result.Score)
            .Take(resultLimit)
            .Select(result => result.Guidebook)
            .ToList();
        return new GuideDiscoveryResult(guides, failures.Distinct().ToArray());
    }

    private async Task<GameGuidebook?> FindSteamManualAsync(string title, uint appId, CancellationToken token)
    {
        string page = $"https://store.steampowered.com/app/{appId}/?l=english";
        string html = await _httpClient.GetStringAsync(page, token);
        var name = SteamTitleRegex().Match(html);
        if (!name.Success || Normalize(WebUtility.HtmlDecode(StripTags(name.Groups["title"].Value))) != Normalize(title)) return null;
        string manual = $"https://store.steampowered.com/manual/{appId}";
        if (!SteamManualRegex().Matches(html).Any(link => WebUtility.HtmlDecode(link.Groups["url"].Value).TrimEnd('/') == manual)) return null;
        return new GameGuidebook
        {
            Title = title + " - Steam Manual", PdfLocation = manual,
            SourceName = "Steam", SourcePageUrl = page, DocumentKind = "Digital Manual",
            Platform = "Windows PC", Language = "English"
        };
    }

    private async Task<GameGuidebook?> FindGamesDatabaseManualAsync(
        string gameTitle,
        string platform,
        CancellationToken cancellationToken)
    {
        string? systemSlug = GetGamesDatabaseSystemSlug(platform);
        if (systemSlug is null)
        {
            return null;
        }

        string titleSlug = Slugify(gameTitle);
        if (string.IsNullOrWhiteSpace(titleSlug))
        {
            return null;
        }

        string sourcePageUrl = $"{GamesDatabaseBaseUrl}/game/{systemSlug}/{titleSlug}";
        using HttpResponseMessage response = await _httpClient.GetAsync(sourcePageUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
            return null;
        }

        string html = await response.Content.ReadAsStringAsync(cancellationToken);
        Match manualMatch = ManualPdfRegex().Match(html);
        Match titleMatch = GamePageTitleRegex().Match(html);
        if (!manualMatch.Success || !titleMatch.Success)
        {
            return null;
        }

        string pageTitle = WebUtility.HtmlDecode(StripTags(titleMatch.Groups["title"].Value)).Trim();
        if (ScoreTitle(gameTitle, pageTitle) < 62)
        {
            return null;
        }

        string pdfPath = WebUtility.HtmlDecode(manualMatch.Groups["url"].Value);
        string pdfUrl = new Uri(new Uri(GamesDatabaseBaseUrl), pdfPath).AbsoluteUri;
        return new GameGuidebook
        {
            Title = pageTitle + " - Original Manual",
            PdfLocation = pdfUrl,
            SourceName = "Games Database",
            SourcePageUrl = sourcePageUrl,
            DocumentKind = "Original Case Manual",
            Platform = platform
        };
    }

    private static string? GetNintendoManualIndex(string platform) => platform.ToLowerInvariant() switch
    {
        "nintendo 64" => "https://www.nintendo.com/en-gb/Support/Nintendo-64/Nintendo-64-manual-and-additional-documents/Nintendo-64-games-manuals/Nintendo-64-games-manuals-990819.html",
        "nintendo ds" => "https://www.nintendo.com/en-gb/Support/Legacy-system/Nintendo-DS-games-manuals-1006833.html",
        "nintendo 3ds" => "https://www.nintendo.com/en-gb/Support/Legacy-system/Nintendo-3DS-games-electronic-manuals-850636.html",
        _ => null
    };

    private async Task<IReadOnlyList<GameGuidebook>> FindNintendoManualsAsync(string title, string platform, CancellationToken token)
    {
        string index = GetNintendoManualIndex(platform)!;
        string html = await _httpClient.GetStringAsync(index, token);
        var manuals = new List<GameGuidebook>();
        foreach (Match link in PdfAnchorRegex().Matches(html))
        {
            string label = WebUtility.HtmlDecode(StripTags(link.Groups["title"].Value)).Trim();
            int suffix = label.IndexOf(" - Manual", StringComparison.OrdinalIgnoreCase);
            if (suffix < 0 || Normalize(label[..suffix]) != Normalize(title)) continue;
            var uri = new Uri(new Uri(index), WebUtility.HtmlDecode(link.Groups["url"].Value));
            if (uri.Scheme != "https" || !new[] { "nintendo.com", "nintendo-europe.com", "nintendo.eu" }.Any(domain => uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))) continue;
            manuals.Add(new GameGuidebook
            {
                Title = label[..suffix] + " - Nintendo Manual (UK)", PdfLocation = uri.AbsoluteUri,
                SourceName = "Nintendo Support (UK)", SourcePageUrl = index,
                DocumentKind = platform == "Nintendo 3DS" ? "Digital Manual" : "Original Case Manual", Platform = platform, Language = "English"
            });
        }
        return manuals;
    }

    private async Task<IReadOnlyList<ArchiveCandidate>> SearchArchiveAsync(string query, CancellationToken cancellationToken)
    {
        string searchUrl = ArchiveBaseUrl + "/advancedsearch.php?q=" + Uri.EscapeDataString(query) +
                           "&fl%5B%5D=identifier&fl%5B%5D=title&fl%5B%5D=description" +
                           "&fl%5B%5D=subject&fl%5B%5D=collection&fl%5B%5D=language&fl%5B%5D=year" +
                           "&sort%5B%5D=downloads+desc&rows=24&page=1&output=json";

        using HttpResponseMessage response = await _httpClient.GetAsync(searchUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("response", out JsonElement searchResponse) ||
            !searchResponse.TryGetProperty("docs", out JsonElement docs) ||
            docs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return docs.EnumerateArray()
            .Select(doc => new ArchiveCandidate(
                ReadString(doc, "identifier"),
                ReadString(doc, "title"),
                string.Join(" ", ReadStrings(doc, "description")),
                string.Join(" ", ReadStrings(doc, "subject")),
                string.Join(" ", ReadStrings(doc, "collection")),
                string.Join(", ", ReadStrings(doc, "language")),
                ReadString(doc, "year"),
                0))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Identifier) && !string.IsNullOrWhiteSpace(candidate.Title))
            .ToList();
    }

    private async Task<IReadOnlyList<GameGuidebook>> ResolveArchivePdfAsync(
        ArchiveCandidate candidate,
        string gameTitle,
        string platform,
        CancellationToken cancellationToken)
    {
        string metadataUrl = ArchiveBaseUrl + "/metadata/" + Uri.EscapeDataString(candidate.Identifier);
        using HttpResponseMessage response = await _httpClient.GetAsync(metadataUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            response.EnsureSuccessStatusCode();
            return [];
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument metadata = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (metadata.RootElement.TryGetProperty("is_dark", out JsonElement isDark) && IsTrue(isDark))
        {
            return [];
        }

        if (metadata.RootElement.TryGetProperty("nodownload", out var noDownload) && IsTrue(noDownload)) return [];

        if (metadata.RootElement.TryGetProperty("metadata", out JsonElement itemMetadata) &&
            itemMetadata.TryGetProperty("access-restricted-item", out JsonElement restricted) && IsTrue(restricted))
        {
            return [];
        }

        if (!metadata.RootElement.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var pdfs = files.EnumerateArray()
            .Select(file => new ArchiveFile(
                ReadString(file, "name"),
                ReadString(file, "source"),
                ReadString(file, "format"),
                ReadString(file, "private"),
                long.TryParse(ReadString(file, "size"), out long size) ? size : 0))
            .Where(file => file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(file => !IsDerivedTextPdf(file.Name) && !IsTruthy(file.Private))
            .Where(file => file.Size <= 512L * 1024 * 1024)
            .ToArray();
        var matches = pdfs
            .Where(file => candidate.IsBundle
                ? IsExactCatalogTitle(gameTitle, Path.GetFileNameWithoutExtension(file.Name))
                : pdfs.Length == 1 || ScoreTitle(gameTitle, Path.GetFileNameWithoutExtension(file.Name)) >= 62)
            .OrderByDescending(file => file.Source.Equals("original", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(file => file.Format.Contains("PDF", StringComparison.OrdinalIgnoreCase))
            .ThenBy(file => file.Name.Length)
            .Take(4).ToArray();

        return matches.Select(pdf => new GameGuidebook
        {
            Title = candidate.IsBundle || matches.Length > 1 ? Path.GetFileNameWithoutExtension(pdf.Name) : candidate.Title.Trim(),
            PdfLocation = $"{ArchiveBaseUrl}/download/{Uri.EscapeDataString(candidate.Identifier)}/{EscapePath(pdf.Name)}",
            SourceName = "Internet Archive",
            SourcePageUrl = ArchiveBaseUrl + "/details/" + Uri.EscapeDataString(candidate.Identifier),
            DocumentKind = InferDocumentKind(pdf.Name + " " + candidate.Title),
            Platform = platform,
            Language = candidate.Language
        }).ToArray();
    }

    private static IEnumerable<string> BuildArchiveQueries(string title)
    {
        string quotedTitle = EscapeQueryValue(title);
        yield return $"mediatype:texts AND collection:(consolemanuals OR gamemanuals OR manuals_various OR manuals_contributions) AND (title:{quotedTitle} OR subject:{quotedTitle} OR description:{quotedTitle})";
        yield return $"mediatype:texts AND (title:{quotedTitle} OR subject:{quotedTitle}) AND (manual OR \"instruction booklet\" OR handbook OR \"quick reference\")";
        yield return $"mediatype:texts AND (title:{quotedTitle} OR subject:{quotedTitle} OR description:{quotedTitle}) AND (\"strategy guide\" OR guidebook OR \"official guide\" OR \"player's guide\")";
        yield return $"mediatype:texts AND {quotedTitle} AND (manual OR \"strategy guide\" OR guidebook OR handbook)";
        // Punctuation and subtitle separators vary across cataloguers; require every title token.
        string tokens = string.Join(" AND ", Tokenize(title).Select(token => "title:" + EscapeQueryValue(token)));
        if (tokens.Length > 0)
            yield return $"mediatype:texts AND ({tokens}) AND (manual OR guide OR guidebook OR handbook OR collection:consolemanuals)";
    }

    private static double ScoreCandidate(string gameTitle, string platform, ArchiveCandidate candidate)
    {
        // Download advertisements can masquerade as short "complete guide" PDFs.
        if (Regex.IsMatch(candidate.Title, @"\b(rom|apk|torrent)\b", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(gameTitle, @"\b(rom|apk|torrent)\b", RegexOptions.IgnoreCase)) return 0;
        string searchable = string.Join(" ", candidate.Title, candidate.Description, candidate.Subject, candidate.Collection);
        string candidateTitle = candidate.Title;
        foreach (string alias in GetPlatformAliases(platform).OrderByDescending(value => value.Length))
            candidateTitle = Regex.Replace(candidateTitle, @"\b" + Regex.Escape(alias) + @"\b", " ", RegexOptions.IgnoreCase);
        double score = ScoreTitle(gameTitle, candidateTitle);
        string normalized = Normalize(searchable);
        if (ContainsDocumentTerm(normalized))
        {
            score += 12;
        }

        string[] platformAliases = GetPlatformAliases(platform);
        bool hasExpectedPlatform = platformAliases.Any(alias => ContainsPhrase(normalized, Normalize(alias)));
        if (hasExpectedPlatform)
        {
            score += 22;
        }
        else if (HasConflictingPlatform(normalized, platformAliases))
        {
            return 0;
        }

        string[] gameTokens = Tokenize(gameTitle);
        string[] coreCandidateTokens = Tokenize(RemoveDocumentTerms(candidateTitle));
        int extraTokens = coreCandidateTokens.Except(gameTokens, StringComparer.Ordinal).Count();
        bool gamingContext = new[] { "consolemanuals", "gamemanuals", "video game", "videogame", "strategy guide", "prima", "bradygames", "nintendo power" }
            .Any(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (!hasExpectedPlatform && !gamingContext && extraTokens > 0) return 0;
        if (gameTokens.Length == 1 && extraTokens > 0)
        {
            score -= Math.Min(50, extraTokens * 25);
        }
        return score;
    }

    private static double ScoreTitle(string gameTitle, string candidateTitle)
    {
        string game = Normalize(gameTitle);
        string candidate = Normalize(RemoveDocumentTerms(candidateTitle));
        // A matching franchise is not a matching edition (e.g. volumes 2 and 3).
        string[] requestedNumbers = Tokenize(game).Where(token => token.All(char.IsDigit)).ToArray();
        string[] candidateNumbers = Tokenize(candidate).Where(token => token.All(char.IsDigit))
            .Where(token => requestedNumbers.Contains(token) || !(token.Length == 4 && (token.StartsWith("19") || token.StartsWith("20"))))
            .ToArray();
        if (requestedNumbers.Any(number => !candidateNumbers.Contains(number)) ||
            candidateNumbers.Any(number => !requestedNumbers.Contains(number) && number is not "64")) return 0;
        if (game == candidate)
        {
            return 120;
        }

        string[] gameTokens = Tokenize(game);
        string[] candidateTokens = Tokenize(candidate);
        if (gameTokens.Length == 0 || candidateTokens.Length == 0)
        {
            return 0;
        }

        int shared = gameTokens.Intersect(candidateTokens, StringComparer.Ordinal).Count();
        double coverage = shared / (double)gameTokens.Length;
        double score = coverage * 58;
        if (ContainsPhrase(candidate, game))
        {
            score += 34;
        }
        if (coverage < 0.6)
        {
            score -= 35;
        }
        return score;
    }

    private static bool HasConflictingPlatform(string text, IReadOnlyCollection<string> expectedAliases)
    {
        if (expectedAliases.Count == 0)
        {
            return false;
        }

        string[] knownPlatforms =
        [
            "nintendo 64", "nes", "super nintendo", "snes", "game boy", "gamecube", "nintendo ds", "nintendo 3ds",
            "playstation 2", "playstation 3", "playstation 4", "playstation", "psp", "ps vita", "xbox 360", "xbox",
            "dreamcast", "saturn", "genesis", "mega drive", "commodore 64", "amiga", "windows", "dos"
        ];
        return knownPlatforms.Any(known => ContainsPhrase(text, known) &&
            !expectedAliases.Any(expected => Normalize(expected).Contains(known, StringComparison.Ordinal) || known.Contains(Normalize(expected), StringComparison.Ordinal)));
    }

    private static bool ContainsPhrase(string normalizedText, string normalizedPhrase) =>
        (" " + normalizedText + " ").Contains(" " + normalizedPhrase + " ", StringComparison.Ordinal);

    private static string[] GetPlatformAliases(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform) || platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return platform.ToLowerInvariant() switch
        {
            "nintendo 64" => ["Nintendo 64", "N64"],
            "nes" => ["NES", "Nintendo Entertainment System"],
            "snes" => ["SNES", "Super Nintendo"],
            "game boy advance" => ["Game Boy Advance", "GBA"],
            "game boy color" => ["Game Boy Color", "GBC"],
            "nintendo ds" => ["Nintendo DS", "NDS"],
            "nintendo 3ds" => ["Nintendo 3DS", "3DS"],
            "playstation" => ["PlayStation", "PS1", "PSX"],
            "playstation 2" => ["PlayStation 2", "PS2"],
            "playstation 3" => ["PlayStation 3", "PS3"],
            "playstation 4" => ["PlayStation 4", "PS4"],
            "mega drive" => ["Mega Drive", "Genesis"],
            "windows pc" => ["Windows", "PC"],
            _ => [platform]
        };
    }

    private static string? GetGamesDatabaseSystemSlug(string platform) => platform.ToLowerInvariant() switch
    {
        "nes" => "nintendo-nes", "snes" => "nintendo-snes", "nintendo 64" => "nintendo-n64",
        "game boy" => "nintendo-game-boy", "game boy color" => "nintendo-game-boy-color",
        "game boy advance" => "nintendo-game-boy-advance", "nintendo ds" => "nintendo-ds",
        "nintendo 3ds" => "nintendo-3ds", "gamecube" => "nintendo-gamecube",
        "wii" => "nintendo-wii", "wii u" => "nintendo-wii-u", "playstation" => "sony-playstation",
        "playstation 2" => "sony-playstation-2", "playstation 3" => "sony-playstation-3",
        "playstation 4" => "sony-playstation-4", "psp" => "sony-psp", "ps vita" => "sony-playstation-vita",
        "xbox" => "microsoft-xbox", "xbox 360" => "microsoft-xbox-360", "dreamcast" => "sega-dreamcast",
        "sega saturn" => "sega-saturn", "saturn" => "sega-saturn", "mega drive" => "sega-genesis",
        "genesis" => "sega-genesis", _ => null
    };

    private static string InferDocumentKind(string value)
    {
        string normalized = Normalize(value);
        if (normalized.Contains("quick reference", StringComparison.Ordinal)) return "Quick Reference";
        if (normalized.Contains("digital manual", StringComparison.Ordinal) || normalized.Contains("electronic manual", StringComparison.Ordinal) || normalized.Contains("web manual", StringComparison.Ordinal)) return "Digital Manual";
        if (normalized.Contains("strategy", StringComparison.Ordinal) || normalized.Contains("players guide", StringComparison.Ordinal) || value.Contains("攻略", StringComparison.Ordinal)) return "Strategy Guide";
        if (normalized.Contains("manual", StringComparison.Ordinal) || normalized.Contains("instruction booklet", StringComparison.Ordinal)) return "Original Case Manual";
        if (normalized.Contains("handbook", StringComparison.Ordinal) || normalized.Contains("guidebook", StringComparison.Ordinal)) return "Guidebook";
        return "Game Guide";
    }

    private static int DocumentKindRank(string kind) => kind switch
    {
        "Original Case Manual" => 0, "Digital Manual" => 1, "Quick Reference" => 2,
        "Strategy Guide" => 3, "Guidebook" => 4, _ => 5
    };

    private static string CleanGameTitle(string value) => value.Trim().TrimEnd('.', ',', ';', ':');
    private static bool IsExactCatalogTitle(string gameTitle, string fileTitle)
    {
        string clean = CatalogAnnotationRegex().Replace(fileTitle, " ");
        return Tokenize(RemoveDocumentTerms(clean)).Order().SequenceEqual(Tokenize(RemoveDocumentTerms(gameTitle)).Order());
    }
    private static string RemoveDocumentTerms(string value) => DocumentTermsRegex().Replace(value, " ");
    private static bool ContainsDocumentTerm(string value) => DocumentTermsRegex().IsMatch(value);

    private static string[] Tokenize(string value) => Normalize(value)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(token => token is not "the" and not "a" and not "an" and not "of" and not "and")
        .Distinct(StringComparer.Ordinal).ToArray();

    private static string Normalize(string value)
    {
        string decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        bool previousSpace = true;
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousSpace = false;
            }
            else if (!previousSpace)
            {
                builder.Append(' ');
                previousSpace = true;
            }
        }
        return builder.ToString().Trim();
    }

    private static string Slugify(string value) => Normalize(value).Replace(' ', '-');
    private static string StripTags(string value) => HtmlTagRegex().Replace(value, string.Empty);
    private static string EscapeQueryValue(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    private static string EscapePath(string value) => string.Join("/", value.Split('/').Select(Uri.EscapeDataString));
    private static bool IsDerivedTextPdf(string name) => name.EndsWith("_text.pdf", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_bw.pdf", StringComparison.OrdinalIgnoreCase) || name.Contains("scandata", StringComparison.OrdinalIgnoreCase);
    private static bool IsTrue(JsonElement value) => value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && IsTruthy(value.GetString()));
    private static bool IsTruthy(string? value) => value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
    private static string ReadString(JsonElement element, string propertyName) => ReadStrings(element, propertyName).FirstOrDefault() ?? string.Empty;

    private static IReadOnlyList<string> ReadStrings(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)) return [];
        return value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString() ?? string.Empty],
            JsonValueKind.Number => [value.ToString()],
            JsonValueKind.True => ["true"],
            JsonValueKind.False => ["false"],
            JsonValueKind.Array => value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToList(),
            _ => []
        };
    }

    [GeneratedRegex("<div\\b[^>]*class=[\"']apphub_AppName[\"'][^>]*>(?<title>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SteamTitleRegex();
    [GeneratedRegex("<a\\b[^>]*href=[\"'](?<url>https://store\\.steampowered\\.com/manual/\\d+/?)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex SteamManualRegex();
    [GeneratedRegex("Game Manual:</B>.*?<A\\s+HREF=[\"'](?<url>[^\"']+\\.pdf)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ManualPdfRegex();
    [GeneratedRegex("<h1>(?<title>.*?)\\s+-\\s+<A\\s+HREF=", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GamePageTitleRegex();
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
    [GeneratedRegex("<a\\b[^>]*href=[\"'](?<url>[^\"']+\\.pdf)[\"'][^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PdfAnchorRegex();
    [GeneratedRegex(@"\((?:USA|US|Europe|EU|Japan|JP|UK|English|En|Fr|De|Es|It|Nl|Pt)(?:[, /-][\w -]+)*\)|\b(?:[A-Z]{4}\d{5}|19\d{2}|20\d{2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex CatalogAnnotationRegex();
    [GeneratedRegex("\\b(official|nintendo power|prima|bradygames|player's|players?|strategy|game|video|instructions?|booklets?|manuals?|guidebooks?|guides?|handbooks?|quick reference|walkthrough|complete|compressed|scan|pdf)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex DocumentTermsRegex();

    private sealed record ArchiveCandidate(string Identifier, string Title, string Description, string Subject, string Collection, string Language, string Year, double Score, bool IsBundle = false);
    private sealed record ArchiveFile(string Name, string Source, string Format, string Private, long Size);
    private sealed record ScoredGuidebook(GameGuidebook Guidebook, double Score);
}

public sealed record GuideDiscoveryResult(IReadOnlyList<GameGuidebook> Guides, IReadOnlyList<string> SourceFailures);
