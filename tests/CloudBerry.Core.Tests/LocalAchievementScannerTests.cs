using CloudBerry.Core.Services.Achievements;

namespace CloudBerry.Core.Tests;

public sealed class LocalAchievementScannerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "CloudBerryAchievementTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadFromInstallPath_ParsesAchievementJson()
    {
        var gameDirectory = Directory.CreateDirectory(Path.Combine(_rootPath, "Game"));
        var executablePath = Path.Combine(gameDirectory.FullName, "Game.exe");
        File.WriteAllText(executablePath, "test");
        File.WriteAllText(
            Path.Combine(gameDirectory.FullName, "achievements.json"),
            """
            {
              "achievements": [
                {
                  "apiName": "first_blood",
                  "displayName": "First Blood",
                  "description": "Win your first battle.",
                  "unlocked": true,
                  "unlocktime": 1714000000
                }
              ]
            }
            """);

        var scanner = new LocalAchievementScanner();
        var result = scanner.LoadFromInstallPath(executablePath);

        var achievement = Assert.Single(result.Snapshot.Achievements);
        Assert.Equal("first_blood", achievement.ApiName);
        Assert.Equal("First Blood", achievement.Title);
        Assert.True(achievement.IsUnlocked);
        Assert.NotNull(achievement.UnlockedAtUtc);
        Assert.True(result.SupportsLiveTracking);
    }

    [Fact]
    public void LoadFromInstallPath_MergesSchemaAndIniState()
    {
        var gameDirectory = Directory.CreateDirectory(Path.Combine(_rootPath, "HybridGame"));
        var executablePath = Path.Combine(gameDirectory.FullName, "HybridGame.exe");
        File.WriteAllText(executablePath, "test");
        File.WriteAllText(
            Path.Combine(gameDirectory.FullName, "achievements.json"),
            """
            [
              {
                "name": "master_collector",
                "description": "Find every relic."
              }
            ]
            """);
        File.WriteAllText(
            Path.Combine(gameDirectory.FullName, "achievements_state.ini"),
            """
            master_collector=1
            """
        );

        var scanner = new LocalAchievementScanner();
        var result = scanner.LoadFromInstallPath(executablePath);

        var achievement = Assert.Single(result.Snapshot.Achievements);
        Assert.Equal("master_collector", achievement.ApiName);
        Assert.Equal("Master Collector", achievement.Title);
        Assert.Equal("Find every relic.", achievement.Description);
        Assert.True(achievement.IsUnlocked);
    }

    [Fact]
    public void LoadFromInstallPath_ParsesNestedSteamStyleSchemaAndAssignsScore()
    {
        var gameDirectory = Directory.CreateDirectory(Path.Combine(_rootPath, "SteamLikeGame"));
        var executablePath = Path.Combine(gameDirectory.FullName, "SteamLikeGame.exe");
        File.WriteAllText(executablePath, "test");
        File.WriteAllText(
            Path.Combine(gameDirectory.FullName, "user_stats.json"),
            """
            {
              "game": {
                "availableGameStats": {
                  "achievements": [
                    {
                      "apiname": "ACH_WIN_ONE_GAME",
                      "displayName": "Win One Game",
                      "description": "Win your first match.",
                      "hidden": 0,
                      "achieved": 1,
                      "unlocktime": 1714000000,
                      "percent": 8.5
                    }
                  ]
                }
              }
            }
            """);

        var scanner = new LocalAchievementScanner();
        var result = scanner.LoadFromInstallPath(executablePath);

        var achievement = Assert.Single(result.Snapshot.Achievements);
        Assert.Equal("ACH_WIN_ONE_GAME", achievement.ApiName);
        Assert.Equal("Win One Game", achievement.Title);
        Assert.True(achievement.IsUnlocked);
        Assert.Equal(25, achievement.GamerScoreValue);
    }

    [Fact]
    public void LoadFromInstallPath_ParsesTrophyFilesAndPreservesExplicitPoints()
    {
        var gameDirectory = Directory.CreateDirectory(Path.Combine(_rootPath, "TrophyGame"));
        var executablePath = Path.Combine(gameDirectory.FullName, "TrophyGame.exe");
        File.WriteAllText(executablePath, "test");
        File.WriteAllText(
            Path.Combine(gameDirectory.FullName, "trophies.json"),
            """
            {
              "trophies": {
                "finish_campaign": {
                  "title": "Finish Campaign",
                  "desc": "Roll credits.",
                  "completed": "yes",
                  "points": 75
                }
              }
            }
            """);

        var scanner = new LocalAchievementScanner();
        var result = scanner.LoadFromInstallPath(executablePath);

        var achievement = Assert.Single(result.Snapshot.Achievements);
        Assert.Equal("finish_campaign", achievement.ApiName);
        Assert.True(achievement.IsUnlocked);
        Assert.Equal(75, achievement.GamerScoreValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
