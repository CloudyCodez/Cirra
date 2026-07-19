using System.Net;
using System.Net.Http;
using System.Text;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Achievements;

namespace CloudBerry.Core.Tests;

public sealed class SteamAchievementProviderTests
{
    [Fact]
    public async Task GetAchievementsAsync_ParsesCommunityAchievementHtml()
    {
        var handler = new StubHttpMessageHandler(
            """
            <div class="achieveRow ">
                <div class="achieveImgHolder">
                    <img src="https://cdn.example/icon-one.jpg" width="64" height="64" border="0" />
                </div>
                <div class="achieveTxtHolder">
                    <div class="achieveFill" style="width: 88%"></div>
                    <div class="achievePercent">88.2%</div>
                    <div class="achieveTxt">
                        <h3>Wake Up</h3>
                        <h5>Complete the tutorial.</h5>
                    </div>
                </div>
            </div>
            <div class="achieveRow ">
                <div class="achieveImgHolder">
                    <img src="https://cdn.example/icon-two.jpg" width="64" height="64" border="0" />
                </div>
                <div class="achieveTxtHolder">
                    <div class="achieveFill" style="width: 12%"></div>
                    <div class="achievePercent">12.4%</div>
                    <div class="achieveTxt">
                        <h3>True Ending</h3>
                        <h5>Reach the final ending.</h5>
                    </div>
                </div>
            </div>
            """
        );

        var provider = new SteamAchievementProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest
            {
                ProviderGameId = "999999",
                Title = "Example"
            },
            new CloudBerrySettings());

        Assert.Equal("steam-community", snapshot.ProviderId);
        Assert.Equal(2, snapshot.Achievements.Count);
        Assert.Equal("Wake Up", snapshot.Achievements[0].Title);
        Assert.Equal("Complete the tutorial.", snapshot.Achievements[0].Description);
        Assert.Equal(88.2, snapshot.Achievements[0].GlobalUnlockPercent);
        Assert.Equal(10, snapshot.Achievements[0].GamerScoreValue);
        Assert.Equal(15, snapshot.Achievements[1].GamerScoreValue);
    }

    [Fact]
    public async Task GetAchievementsAsync_FallsBackToGlobalPercentagesWhenCommunityHtmlHasNoRows()
    {
        var handler = new RoutingHttpMessageHandler(request =>
            request.RequestUri?.Host.Contains("api.steampowered.com", StringComparison.OrdinalIgnoreCase) == true
                ? """
                  {
                    "achievementpercentages": {
                      "achievements": [
                        { "name": "ACH_SECRET_ENDING", "percent": 4.2 }
                      ]
                    }
                  }
                  """
                : "<html>No public achievement rows here.</html>");

        var provider = new SteamAchievementProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest
            {
                ProviderGameId = "999999",
                Title = "Example"
            },
            new CloudBerrySettings());

        var achievement = Assert.Single(snapshot.Achievements);
        Assert.Equal("ACH_SECRET_ENDING", achievement.ApiName);
        Assert.Equal("Ach Secret Ending", achievement.Title);
        Assert.Equal(50, achievement.GamerScoreValue);
    }

    private sealed class StubHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/html")
            });
        }
    }

    private sealed class RoutingHttpMessageHandler(Func<HttpRequestMessage, string> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var contentType = request.RequestUri?.Host.Contains("api.steampowered.com", StringComparison.OrdinalIgnoreCase) == true
                ? "application/json"
                : "text/html";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responder(request), Encoding.UTF8, contentType)
            });
        }
    }
}
