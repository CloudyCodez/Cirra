using System.Net;
using System.Text;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.Core.Tests;

public sealed class AggregatedMetadataProviderTests
{
    [Fact]
    public void CloudBerrySettings_DefaultsToBuiltInIgdbProxy()
    {
        var settings = new CloudBerrySettings();

        Assert.Equal("aggregated", settings.SelectedMetadataProviderId);
        Assert.True(settings.Igdb.ProxyEnabled);
        Assert.Equal(CloudBerrySettings.BuiltInIgdbProxyEndpoint, settings.Igdb.ProxyEndpoint);
    }

    [Fact]
    public async Task SearchAsync_NormalizesYugiohAliasAndReturnsMatches()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;

            if (url.Contains("storesearch", StringComparison.OrdinalIgnoreCase))
            {
                return """{"items":[]}""";
            }

            if (url.Contains("pcgamingwiki.com/w/api.php", StringComparison.OrdinalIgnoreCase))
            {
                return """{"query":{"search":[]}}""";
            }

            if (url.Contains("opencritic.com/api/game/search", StringComparison.OrdinalIgnoreCase))
            {
                return "[]";
            }

            if (url.Contains("wikipedia.org/w/api.php", StringComparison.OrdinalIgnoreCase))
            {
                if (url.Contains("yu%20gi%20oh", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("yu gi oh", StringComparison.OrdinalIgnoreCase))
                {
                    return """
                    {
                      "query": {
                        "search": [
                          { "pageid": 101, "title": "Yu-Gi-Oh! GX: Spirit Caller", "snippet": "Nintendo DS game." }
                        ]
                      }
                    }
                    """;
                }

                return """{"query":{"search":[]}}""";
            }

            return "{}";
        });

        var provider = new AggregatedMetadataProvider(new HttpClient(handler));
        var results = await provider.SearchAsync(
            new GameMetadataSearchRequest
            {
                Query = "yugioh gx spirit caller",
                Platform = "Nintendo DS",
                MaxResults = 10
            },
            new CloudBerrySettings());

        var result = Assert.Single(results);
        Assert.Equal("Yu-Gi-Oh! GX: Spirit Caller", result.Title);
    }

    [Fact]
    public async Task SearchAsync_DemotesFranchiseAndListPagesBelowSpecificGame()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;

            if (url.Contains("storesearch", StringComparison.OrdinalIgnoreCase))
            {
                return """{"items":[]}""";
            }

            if (url.Contains("pcgamingwiki.com/w/api.php", StringComparison.OrdinalIgnoreCase))
            {
                return """{"query":{"search":[]}}""";
            }

            if (url.Contains("opencritic.com/api/game/search", StringComparison.OrdinalIgnoreCase))
            {
                return "[]";
            }

            if (url.Contains("wikipedia.org/w/api.php", StringComparison.OrdinalIgnoreCase))
            {
                return """
                {
                  "query": {
                    "search": [
                      { "pageid": 201, "title": "Yu-Gi-Oh! GX", "snippet": "Anime franchise entry." },
                      { "pageid": 202, "title": "List of Yu-Gi-Oh! video games", "snippet": "List page." },
                      { "pageid": 203, "title": "Yu-Gi-Oh! GX: Spirit Caller", "snippet": "Nintendo DS game." }
                    ]
                  }
                }
                """;
            }

            return "{}";
        });

        var provider = new AggregatedMetadataProvider(new HttpClient(handler));
        var results = await provider.SearchAsync(
            new GameMetadataSearchRequest
            {
                Query = "yugioh gx spirit caller",
                Platform = "Nintendo DS",
                MaxResults = 10
            },
            new CloudBerrySettings());

        Assert.True(results.Count >= 3);
        Assert.Equal("Yu-Gi-Oh! GX: Spirit Caller", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_RespectsRequestedCollectionAndHdTerms()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;

            if (url.Contains("storesearch", StringComparison.OrdinalIgnoreCase))
            {
                return """{"items":[]}""";
            }

            if (url.Contains("pcgamingwiki.com/w/api.php", StringComparison.OrdinalIgnoreCase))
            {
                return """{"query":{"search":[]}}""";
            }

            if (url.Contains("opencritic.com/api/game/search", StringComparison.OrdinalIgnoreCase))
            {
                return "[]";
            }

            if (url.Contains("wikipedia.org/w/api.php", StringComparison.OrdinalIgnoreCase))
            {
                return """
                {
                  "query": {
                    "search": [
                      { "pageid": 301, "title": "Dragon Ball Z: Budokai", "snippet": "Fighting video game." },
                      { "pageid": 302, "title": "Dragon Ball Z: Budokai HD Collection", "snippet": "HD collection of fighting games." }
                    ]
                  }
                }
                """;
            }

            return "{}";
        });

        var provider = new AggregatedMetadataProvider(new HttpClient(handler));
        var results = await provider.SearchAsync(
            new GameMetadataSearchRequest
            {
                Query = "Dragon Ball Z Budokai HD Collection",
                Platform = "PlayStation 3",
                MaxResults = 10
            },
            new CloudBerrySettings());

        Assert.True(results.Count >= 2);
        Assert.Equal("Dragon Ball Z: Budokai HD Collection", results[0].Title);
    }

    private sealed class RouteHttpMessageHandler(Func<HttpRequestMessage, string> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responder(request), Encoding.UTF8, "application/json")
            });
        }
    }
}
