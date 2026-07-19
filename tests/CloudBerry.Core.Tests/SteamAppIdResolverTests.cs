using System.Net;
using System.Text;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class SteamAppIdResolverTests
{
    [Fact]
    public async Task ResolveByTitleAsync_ReturnsBestExactSteamStoreMatch()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "items": [
                {
                  "id": 814380,
                  "name": "Sekiro: Shadows Die Twice - GOTY Edition",
                  "platforms": { "windows": true }
                },
                {
                  "id": 123,
                  "name": "Shadow Something Else",
                  "platforms": { "windows": true }
                }
              ]
            }
            """);

        var resolver = new SteamAppIdResolver(new HttpClient(handler));
        var appId = await resolver.ResolveByTitleAsync("Sekiro Shadows Die Twice", "Windows PC");

        Assert.Equal("814380", appId);
    }

    [Fact]
    public void TryExtractAppIdFromUrl_ReturnsSteamAppId()
    {
        var appId = SteamAppIdResolver.TryExtractAppIdFromUrl("https://store.steampowered.com/app/413150/Stardew_Valley/");

        Assert.Equal("413150", appId);
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
