using System.Net;
using System.Net.Http;
using System.Text;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Achievements;

namespace CloudBerry.Core.Tests;

public sealed class RetroAchievementsProviderTests
{
    [Fact]
    public async Task GetAchievementsAsync_RawUnlockCountIsNeverTreatedAsARarityPercent()
    {
        // Regression test: RA's "NumAwarded" field is a raw unlock COUNT (can run into
        // the thousands for popular games), not a percentage. Earlier code used it
        // directly as GlobalUnlockPercent, which badly skewed scoring. A count this
        // large must be rejected and the achievement should fall back to the
        // no-rarity-data default score of 10, not be treated as near-0% (i.e. "ultra rare").
        var handler = new StubHttpMessageHandler(
            """
            {
              "Achievements": {
                "5501": {
                  "ID": "5501",
                  "Title": "First Steps",
                  "Description": "Complete level 1.",
                  "BadgeURL": "/Badge/12345.png",
                  "NumAwarded": 15234,
                  "DateEarned": 1700000000
                }
              }
            }
            """);

        var provider = new RetroAchievementsProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest { ProviderGameId = "778", Title = "Example RA Game" },
            new CloudBerrySettings
            {
                RetroAchievementsApiKey = "test-key",
                RetroAchievementsUsername = "test-user"
            });

        var achievement = Assert.Single(snapshot.Achievements);
        Assert.Null(achievement.GlobalUnlockPercent);
        Assert.Equal(10, achievement.GamerScoreValue);
    }

    [Fact]
    public async Task GetAchievementsAsync_RarityPercentIsComputedFromAwardedCountOverTotalPlayers()
    {
        // The real rarity percent is NumAwarded / total distinct players * 100 — RA doesn't
        // hand back a ready-made percent field. TrueRatio ("Retro Ratio") is a difficulty-
        // weighted point value, not a percent, and must be ignored for this purpose even
        // though it's present here alongside the real data.
        var handler = new StubHttpMessageHandler(
            """
            {
              "NumDistinctPlayersCasual": 1000,
              "Achievements": {
                "5502": {
                  "ID": "5502",
                  "Title": "Rare Feat",
                  "Description": "Do something rare.",
                  "BadgeURL": "/Badge/67890.png",
                  "NumAwarded": 30,
                  "TrueRatio": 42.7
                }
              }
            }
            """);

        var provider = new RetroAchievementsProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest { ProviderGameId = "778", Title = "Example RA Game" },
            new CloudBerrySettings
            {
                RetroAchievementsApiKey = "test-key",
                RetroAchievementsUsername = "test-user"
            });

        var achievement = Assert.Single(snapshot.Achievements);
        Assert.Equal(3.0, achievement.GlobalUnlockPercent);
        Assert.Equal(50, achievement.GamerScoreValue); // <=5% tier
    }

    [Fact]
    public async Task GetAchievementsAsync_WithoutTotalPlayerCount_LeavesRarityNullRatherThanGuessing()
    {
        // If the response doesn't include a distinct-player total (e.g. the field is
        // missing or malformed), Cirra must not fall back to TrueRatio or any other
        // stand-in — it should simply have no rarity data for that achievement.
        var handler = new StubHttpMessageHandler(
            """
            {
              "Achievements": {
                "5502": {
                  "ID": "5502",
                  "Title": "Rare Feat",
                  "Description": "Do something rare.",
                  "BadgeURL": "/Badge/67890.png",
                  "NumAwarded": 30,
                  "TrueRatio": 4.2
                }
              }
            }
            """);

        var provider = new RetroAchievementsProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest { ProviderGameId = "778", Title = "Example RA Game" },
            new CloudBerrySettings
            {
                RetroAchievementsApiKey = "test-key",
                RetroAchievementsUsername = "test-user"
            });

        var achievement = Assert.Single(snapshot.Achievements);
        Assert.Null(achievement.GlobalUnlockPercent);
        Assert.Equal(10, achievement.GamerScoreValue);
    }

    [Fact]
    public async Task GetAchievementsAsync_WithUserProgress_MarksUnlockedAchievementsFromDateEarned()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "Achievements": {
                "5501": {
                  "ID": "5501",
                  "Title": "First Steps",
                  "Description": "Complete level 1.",
                  "BadgeURL": "/Badge/12345.png",
                  "DateEarned": 1700000000
                },
                "5502": {
                  "ID": "5502",
                  "Title": "Not Yet Earned",
                  "Description": "Still locked.",
                  "BadgeURL": "/Badge/67890.png"
                }
              }
            }
            """);

        var provider = new RetroAchievementsProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest { ProviderGameId = "778", Title = "Example RA Game" },
            new CloudBerrySettings
            {
                RetroAchievementsApiKey = "test-key",
                RetroAchievementsUsername = "test-user"
            });

        Assert.Equal(2, snapshot.Achievements.Count);
        var earned = Assert.Single(snapshot.Achievements, a => a.Title == "First Steps");
        var locked = Assert.Single(snapshot.Achievements, a => a.Title == "Not Yet Earned");

        Assert.True(earned.IsUnlocked);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, earned.UnlockedAtUtc);
        Assert.False(locked.IsUnlocked);
        Assert.Null(locked.UnlockedAtUtc);
    }

    [Fact]
    public async Task GetAchievementsAsync_WithoutApiKeyOrUsername_ReturnsMetadataOnlyWithNoUnlockStatus()
    {
        // No key/username means GetAchievementsAsync falls back to the public,
        // no-auth endpoint — which carries no per-user unlock status at all.
        var handler = new StubHttpMessageHandler(
            """
            {
              "achievements": {
                "5501": {
                  "ID": "5501",
                  "Title": "First Steps",
                  "Description": "Complete level 1.",
                  "BadgeURL": "/Badge/12345.png"
                }
              }
            }
            """);

        var provider = new RetroAchievementsProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest { ProviderGameId = "778", Title = "Example RA Game" },
            new CloudBerrySettings());

        var achievement = Assert.Single(snapshot.Achievements);
        Assert.False(achievement.IsUnlocked);
        Assert.Null(achievement.UnlockedAtUtc);
    }

    [Fact]
    public async Task GetAchievementsAsync_WithNoResolvableGameId_ReturnsEmptySnapshot()
    {
        var handler = new StubHttpMessageHandler("{}");

        var provider = new RetroAchievementsProvider(new HttpClient(handler));
        var snapshot = await provider.GetAchievementsAsync(
            new GameAchievementRequest { ProviderGameId = string.Empty, Title = string.Empty },
            new CloudBerrySettings());

        Assert.Equal("retroachievements", snapshot.ProviderId);
        Assert.Empty(snapshot.Achievements);
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
