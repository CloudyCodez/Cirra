using System.Net;
using System.Text;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.Core.Tests;

public sealed class GuidebookDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_ReturnsOriginalPdfAndSkipsDerivedTextCopy()
    {
        var handler = new StubArchiveHandler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("advancedsearch.php", StringComparison.Ordinal))
            {
                return Json("""
                    {"response":{"docs":[
                      {"identifier":"zelda-manual","title":"The Legend of Zelda Player's Manual"},
                      {"identifier":"zelda-notes","title":"Zelda Notes"}
                    ]}}
                    """);
            }

            if (url.EndsWith("/metadata/zelda-manual", StringComparison.Ordinal))
            {
                return Json("""
                    {"files":[
                      {"name":"zelda_text.pdf","source":"derivative","format":"Text PDF"},
                      {"name":"Zelda Manual.pdf","source":"original","format":"PDF"}
                    ]}
                    """);
            }

            return Json("{" + "\"files\":[]}" );
        });
        var service = new GuidebookDiscoveryService(new HttpClient(handler));

        var results = await service.DiscoverAsync("The Legend of Zelda", "NES");

        var guide = Assert.Single(results);
        Assert.Equal("The Legend of Zelda Player's Manual", guide.Title);
        Assert.Equal("Internet Archive", guide.SourceName);
        Assert.Equal("https://archive.org/download/zelda-manual/Zelda%20Manual.pdf", guide.PdfLocation);
        Assert.Equal("https://archive.org/details/zelda-manual", guide.SourcePageUrl);
        Assert.DoesNotContain("_text.pdf", guide.PdfLocation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverAsync_WithBlankTitle_DoesNotCallArchive()
    {
        var handler = new StubArchiveHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var service = new GuidebookDiscoveryService(new HttpClient(handler));

        var results = await service.DiscoverAsync("   ");

        Assert.Empty(results);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task DiscoverAsync_PrioritizesExactPhysicalCaseManualFromGamesDatabase()
    {
        var handler = new StubArchiveHandler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith("gamesdatabase.org/game/nintendo-n64/donkey-kong-64", StringComparison.Ordinal))
            {
                return Html("""
                    <h1>Donkey Kong 64 - <A HREF="/system-nintendo_n64">Nintendo N64</A></h1>
                    <B>Game Manual:</B></TD><TD><A HREF="/Media/SYSTEM/Nintendo_N64/Manual/formated/Donkey_Kong_64.pdf">Download</A>
                    """);
            }

            return Json("{\"response\":{\"docs\":[]}}");
        });
        var service = new GuidebookDiscoveryService(new HttpClient(handler));

        var results = await service.DiscoverAsync("Donkey Kong 64.", "Nintendo 64");

        var manual = Assert.Single(results);
        Assert.Equal("Original Case Manual", manual.DocumentKind);
        Assert.Equal("Games Database", manual.SourceName);
        Assert.Equal("Nintendo 64", manual.Platform);
        Assert.Equal("https://www.gamesdatabase.org/Media/SYSTEM/Nintendo_N64/Manual/formated/Donkey_Kong_64.pdf", manual.PdfLocation);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsUnrelatedSingleWordAndPlatformMatch()
    {
        var handler = new StubArchiveHandler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("advancedsearch.php", StringComparison.Ordinal))
            {
                return Json("""
                    {"response":{"docs":[{
                      "identifier":"skate-rock-c64",
                      "title":"Commodore C64 Manual: Skate Rock (1986)(Bubble Bus Software)",
                      "collection":["gamemanuals"]
                    }]}}
                    """);
            }

            throw new InvalidOperationException("Rejected candidates must not trigger metadata resolution.");
        });
        var service = new GuidebookDiscoveryService(new HttpClient(handler));

        var results = await service.DiscoverAsync("Skate.", "Windows PC");

        Assert.Empty(results);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsExactTitleForWrongConsoleEdition()
    {
        var handler = new StubArchiveHandler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("gamesdatabase.org", StringComparison.Ordinal))
            {
                return Html("<html></html>");
            }

            if (url.Contains("advancedsearch.php", StringComparison.Ordinal))
            {
                return Json("""
                    {"response":{"docs":[{
                      "identifier":"goldeneye-ds-manual",
                      "title":"GoldenEye 007 instructions booklet (Nintendo DS)",
                      "collection":["consolemanuals"]
                    }]}}
                    """);
            }

            throw new InvalidOperationException("Wrong-platform candidates must not trigger metadata resolution.");
        });
        var service = new GuidebookDiscoveryService(new HttpClient(handler));

        var results = await service.DiscoverAsync("GoldenEye 007", "Nintendo 64");

        Assert.Empty(results);
    }

    [Fact]
    public async Task DiscoveryContinuesAfterOneCandidateFailsAndRejectsRestrictedFiles()
    {
        using var client = new HttpClient(new StubArchiveHandler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("advancedsearch")) return Json("""
                {"response":{"docs":[
                {"identifier":"broken","title":"Portal 2 Manual"},
                {"identifier":"good","title":"Portal 2 Manual"},
                {"identifier":"private","title":"Portal 2 Guide"},
                {"identifier":"restricted","title":"Portal 2 Guide"},
                {"identifier":"sequel","title":"Portal 3 Manual"}]}}
                """);
            if (url.EndsWith("/broken")) return new(HttpStatusCode.ServiceUnavailable);
            if (url.EndsWith("/private")) return Json("""{"files":[{"name":"guide.pdf","private":true}]}""");
            if (url.EndsWith("/restricted")) return Json("""{"nodownload":true,"files":[{"name":"guide.pdf"}]}""");
            if (url.EndsWith("/sequel")) throw new InvalidOperationException("Wrong sequel reached resolution");
            return Json("""{"files":[{"name":"Portal 2 Manual.pdf","source":"original"}]}""");
        }));
        var result = await new GuidebookDiscoveryService(client).DiscoverDetailedAsync("Portal 2", "Windows PC");
        Assert.Single(result.Guides);
        Assert.Contains("/good/", result.Guides[0].PdfLocation);
        Assert.Single(result.SourceFailures);
    }

    [Fact]
    public async Task SelectsMatchingFileWithinMultiPdfItem()
    {
        using var client = new HttpClient(new StubArchiveHandler(request => request.RequestUri!.AbsoluteUri.Contains("advancedsearch")
            ? Json("""{"response":{"docs":[{"identifier":"bundle","title":"Portal 2 Manuals","collection":"gamemanuals"}]}}""")
            : Json("""{"files":[{"name":"a.pdf"},{"name":"Portal 3 Manual.pdf"},{"name":"Portal 2 Manual.pdf"}]}""")));
        var results = await new GuidebookDiscoveryService(client).DiscoverAsync("Portal 2");
        Assert.EndsWith("Portal%202%20Manual.pdf", Assert.Single(results).PdfLocation);
    }

    [Theory]
    [InlineData("Acoustic Solutions Portal 2 User Manual")]
    [InlineData("supplier portal version - 2 user manual")]
    [InlineData("Sepsis Data Portal User Manual (v.2)")]
    public async Task RejectsRealWorldNonGamingPortalManuals(string title)
    {
        using var client = new HttpClient(new StubArchiveHandler(request =>
            request.RequestUri!.AbsoluteUri.Contains("advancedsearch")
                ? Json(System.Text.Json.JsonSerializer.Serialize(new { response = new { docs = new[] { new { identifier = "not-a-game", title } } } }))
                : throw new InvalidOperationException("Non-game PDF must not be resolved")));
        var result = await new GuidebookDiscoveryService(client).DiscoverDetailedAsync("Portal 2", "Windows PC");
        Assert.Empty(result.Guides);
        Assert.Empty(result.SourceFailures);
    }

    [Fact]
    public async Task OfficialNintendoIndexMatchesExactTitleOnly()
    {
        using var client = new HttpClient(new StubArchiveHandler(request =>
        {
            if (request.RequestUri!.Host == "www.nintendo.com") return Html("""
                <a class="pdf" href="/manuals/Mario.pdf"><span>Super Mario 64 - Manual (PDF, 1247 kB)</span></a>
                <a href="/manuals/Kart.pdf">Mario Kart 64 - Manual (PDF)</a>
                """);
            return Json("""{"response":{"docs":[]}}""");
        }));
        var results = await new GuidebookDiscoveryService(client).DiscoverAsync("Super Mario 64", "Nintendo 64");
        Assert.Equal("Nintendo Support (UK)", Assert.Single(results).SourceName);
    }

    [Fact]
    public async Task FindsExactGameInsideConsoleManualPackWithoutSubstitutingSequel()
    {
        using var client = new HttpClient(new StubArchiveHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.EndsWith("/metadata/SonyPSPManuals"))
                return Json("""{"files":[{"name":"Yu-Gi-Oh! GX - Tag Force (USA).pdf"},{"name":"Yu-Gi-Oh! GX - Tag Force 2 (USA).pdf"}]}""");
            return Json("""{"response":{"docs":[]},"files":[]}""");
        }));
        var service = new GuidebookDiscoveryService(client);
        var found = await service.DiscoverAsync("Yu-Gi-Oh! GX Tag Force 2", "PSP");
        Assert.Contains("Tag%20Force%202", Assert.Single(found).PdfLocation);
        Assert.Empty(await service.DiscoverAsync("Yu-Gi-Oh! GX Tag Force 3", "PSP"));
    }

    [Theory]
    [InlineData("Nintendo DS", "Original Case Manual")]
    [InlineData("Nintendo 3DS", "Digital Manual")]
    public async Task OfficialNintendoLegacyIndexesSupportCdnAndAccents(string platform, string kind)
    {
        using var client = new HttpClient(new StubArchiveHandler(request => request.RequestUri!.Host == "www.nintendo.com"
            ? Html("""<a href="https://cdn02.nintendo-europe.com/manual.pdf">Pokémon Alpha Sapphire - Manual (PDF)</a><a href="https://notnintendo.com/manual.pdf">Pokémon Alpha Sapphire - Manual (PDF)</a>""")
            : Json("""{"response":{"docs":[]}}""")));
        var guide = Assert.Single(await new GuidebookDiscoveryService(client).DiscoverAsync("Pokemon Alpha Sapphire", platform));
        Assert.Equal(kind, guide.DocumentKind);
        Assert.Equal("cdn02.nintendo-europe.com", new Uri(guide.PdfLocation).Host);
    }

    [Fact]
    public async Task ReturnsManualAndStrategyGuideWithinOneItemButNotOversizedPdf()
    {
        using var client = new HttpClient(new StubArchiveHandler(request => request.RequestUri!.AbsoluteUri.Contains("advancedsearch")
            ? Json("""{"response":{"docs":[{"identifier":"books","title":"Portal 2 Manuals","collection":"gamemanuals"}]}}""")
            : Json("""{"files":[{"name":"Portal 2 Manual.pdf","source":"original"},{"name":"Portal 2 Strategy Guide.pdf","source":"original"},{"name":"Portal 2 Huge Guide.pdf","size":"600000000"}]}""")));
        var guides = await new GuidebookDiscoveryService(client).DiscoverAsync("Portal 2");
        Assert.Equal(2, guides.Count);
        Assert.Contains(guides, guide => guide.DocumentKind == "Strategy Guide");
        Assert.Contains(guides, guide => guide.DocumentKind == "Original Case Manual");
    }

    [Theory]
    [InlineData("Final Fantasy 12", "Final Fantasy 13 Official Strategy Guide")]
    [InlineData("Tomodachi Life", "Tomodachi Life ROM - Complete Guide for Nintendo 3DS")]
    public async Task RejectsWrongMultiDigitEditionAndDownloadAdvertisements(string game, string title)
    {
        using var client = new HttpClient(new StubArchiveHandler(request => request.RequestUri!.AbsoluteUri.Contains("advancedsearch")
            ? Json(System.Text.Json.JsonSerializer.Serialize(new { response = new { docs = new[] { new { identifier = "wrong", title, collection = "gamemanuals" } } } }))
            : throw new InvalidOperationException("Rejected document was resolved")));
        var result = await new GuidebookDiscoveryService(client).DiscoverDetailedAsync(game);
        Assert.Empty(result.Guides);
        Assert.Empty(result.SourceFailures);
    }

    [Theory]
    [InlineData("Sid Meier's Civilization V", true)]
    [InlineData("Sid Meier's Civilization VI", false)]
    public async Task SteamManualRequiresCorrectAppAndTitle(string title, bool expected)
    {
        using var client = new HttpClient(new StubArchiveHandler(request => request.RequestUri!.Host == "store.steampowered.com"
            ? Html("""<div class="apphub_AppName">Sid Meier's Civilization V</div><a href="https://store.steampowered.com/manual/8930">View manual</a>""")
            : Json("""{"response":{"docs":[]}}""")));
        var result = await new GuidebookDiscoveryService(client).DiscoverDetailedAsync(title, "Windows PC", steamAppId: "8930");
        Assert.Equal(expected ? 1 : 0, result.Guides.Count);
        if (expected) Assert.Equal("https://store.steampowered.com/manual/8930", result.Guides[0].PdfLocation);
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Html(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "text/html")
    };

    private sealed class StubArchiveHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
