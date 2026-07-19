using System.Net;
using System.Net.Http;
using System.Text;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.Core.Tests;

public sealed class RawgMetadataProviderTests
{
    [Fact]
    public async Task SearchAsync_ParsesGameMatches()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "results": [
                {
                  "id": 3498,
                  "slug": "grand-theft-auto-v",
                  "name": "Grand Theft Auto V",
                  "released": "2013-09-17",
                  "background_image": "https://images.example/gta5.jpg",
                  "rating": 4.47,
                  "platforms": [
                    { "platform": { "name": "PC" } },
                    { "platform": { "name": "PlayStation 5" } }
                  ],
                  "genres": [
                    { "name": "Action" },
                    { "name": "Adventure" }
                  ],
                  "tags": [
                    { "name": "Open World" }
                  ]
                }
              ]
            }
            """);

        var provider = CreateProvider(handler);
        var results = await provider.SearchAsync(
            new GameMetadataSearchRequest { Query = "GTA V" },
            new CloudBerrySettings { Rawg = new RawgMetadataSettings { ApiKey = "test-key" } });

        var result = Assert.Single(results);
        Assert.Equal("Grand Theft Auto V", result.Title);
        Assert.Equal("3498", result.RemoteId);
        Assert.Contains("PC", result.Platforms);
        Assert.Contains("Action", result.Genres);
        Assert.Equal("https://images.example/gta5.jpg", result.CoverImageUrl);
    }

    [Fact]
    public async Task GetDetailsAsync_ParsesFullMetadata()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "id": 5679,
              "slug": "hades",
              "name": "Hades",
              "description_raw": "Battle out of hell in this rogue-like dungeon crawler.",
              "released": "2020-09-17",
              "background_image": "https://images.example/hades-cover.jpg",
              "background_image_additional": "https://images.example/hades-bg.jpg",
              "website": "https://www.supergiantgames.com/games/hades/",
              "rating": 4.66,
              "platforms": [
                { "platform": { "name": "PC" } },
                { "platform": { "name": "Nintendo Switch" } }
              ],
              "genres": [
                { "name": "Action" },
                { "name": "Indie" }
              ],
              "developers": [
                { "name": "Supergiant Games" }
              ],
              "publishers": [
                { "name": "Supergiant Games" }
              ],
              "tags": [
                { "name": "Rogue-like" }
              ]
            }
            """);

        var provider = CreateProvider(handler);
        var details = await provider.GetDetailsAsync(
            new GameMetadataRecord
            {
                ProviderId = "rawg",
                ProviderName = "RAWG",
                RemoteId = "5679",
                Title = "Hades"
            },
            new CloudBerrySettings { Rawg = new RawgMetadataSettings { ApiKey = "test-key" } });

        Assert.NotNull(details);
        Assert.Equal("Hades", details!.Title);
        Assert.Equal("Battle out of hell in this rogue-like dungeon crawler.", details.Summary);
        Assert.Equal("https://images.example/hades-cover.jpg", details.CoverImageUrl);
        Assert.Equal("https://images.example/hades-bg.jpg", details.BackgroundImageUrl);
        Assert.Contains("Supergiant Games", details.Developers);
        Assert.Contains("Rogue-like", details.Tags);
    }

    private static RawgMetadataProvider CreateProvider(HttpMessageHandler handler)
    {
        return new RawgMetadataProvider(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.rawg.io/api/")
        });
    }

    private sealed class StubHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}
