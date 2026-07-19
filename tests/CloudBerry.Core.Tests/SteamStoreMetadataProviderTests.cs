using System.Net;
using System.Text;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.Core.Tests;

public sealed class SteamStoreMetadataProviderTests
{
    [Fact]
    public async Task GetDetailsAsync_ParsesRichStoreMediaAndDescriptions()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            Assert.Contains("appdetails", request.RequestUri?.ToString() ?? string.Empty);
            return """
            {
              "413150": {
                "success": true,
                "data": {
                  "name": "Stardew Valley",
                  "short_description": "",
                  "about_the_game": "<p>You inherited your grandfather's old farm plot.</p>",
                  "header_image": "https://shared.example/header.jpg",
                  "clienticon": "abc123",
                  "background": "https://shared.example/background.jpg",
                  "background_raw": "https://shared.example/background_raw.jpg",
                  "website": "https://www.stardewvalley.net/",
                  "release_date": { "date": "Feb 26, 2016" },
                  "metacritic": { "score": 89 },
                  "genres": [
                    { "description": "RPG" },
                    { "description": "Simulation" }
                  ],
                  "developers": [ "ConcernedApe" ],
                  "publishers": [ "ConcernedApe" ],
                  "screenshots": [
                    { "path_full": "https://shared.example/shot1.jpg" }
                  ],
                  "movies": [
                    { "mp4": { "480": "https://shared.example/trailer_480.mp4", "max": "https://shared.example/trailer_max.mp4" } }
                  ]
                }
              }
            }
            """;
        });

        var provider = new SteamStoreMetadataProvider(new HttpClient(handler));
        var details = await provider.GetDetailsAsync(
            new GameMetadataRecord
            {
                ProviderId = "steam-store",
                ProviderName = "Steam Store",
                RemoteId = "413150",
                Title = "Stardew Valley"
            },
            new CloudBerrySettings());

        Assert.NotNull(details);
        Assert.Equal("Stardew Valley", details!.Title);
        Assert.Equal("You inherited your grandfather's old farm plot.", details.Summary);
        Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/413150/library_600x900.jpg", details.CoverImageUrl);
        Assert.Equal("https://shared.example/background_raw.jpg", details.BackgroundImageUrl);
        Assert.Equal("https://shared.example/header.jpg", details.BannerImageUrl);
        Assert.Equal("https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/413150/abc123.jpg", details.IconImageUrl);
        Assert.Equal("https://shared.example/shot1.jpg", Assert.Single(details.Screenshots));
        Assert.Equal("https://shared.example/trailer_max.mp4", details.TrailerUrl);
        Assert.Equal(4.45, details.CommunityRating);
        Assert.Contains("ConcernedApe", details.Developers);
        Assert.Contains("Simulation", details.Genres);
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
