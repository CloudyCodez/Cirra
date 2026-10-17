using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows.Threading;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Tests;

public sealed class HorizonSpotlightTests
{
    [Fact]
    public void RepeatedSpotlightCyclesPreserveWorkingSelectionAndGuideResults() => OnSta(() =>
    {
        var vm = CreateViewModel();
        var selected = vm.SelectedGame;
        var preflight = vm.SelectedLaunchPreflight;
        var guide = new GameGuidebook { Title = "Manual", PdfLocation = "test.pdf" };
        vm.GuidebookSearchResults.Add(guide);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        for (int i = 0; i < 20; i++) Assert.True(vm.AdvanceBigPictureSpotlight());

        Assert.Same(selected, vm.SelectedGame);
        Assert.Same(preflight, vm.SelectedLaunchPreflight);
        Assert.Same(guide, Assert.Single(vm.GuidebookSearchResults));
        Assert.DoesNotContain(nameof(MainViewModel.SelectedGame), changed);
        Assert.DoesNotContain(nameof(MainViewModel.SelectedLaunchPreflight), changed);
        Assert.Contains(nameof(MainViewModel.BigPictureHeroGame), changed);
    });

    [Fact]
    public void ReselectingTheSameCardRestoresItsHeroWithoutClearingGuides() => OnSta(() =>
    {
        var vm = CreateViewModel();
        var selected = vm.SelectedGame;
        vm.GuidebookSearchResults.Add(new GameGuidebook { Title = "Manual" });
        vm.AdvanceBigPictureSpotlight();
        Assert.NotSame(selected, vm.BigPictureHeroGame);

        vm.SelectedGame = selected;

        Assert.Same(selected, vm.BigPictureHeroGame);
        Assert.Single(vm.GuidebookSearchResults);
    });

    [Fact]
    public void ExplicitSelectionAndPresentationResetRemainIndependent() => OnSta(() =>
    {
        var vm = CreateViewModel();
        vm.AdvanceBigPictureSpotlight();
        vm.SelectedGame = vm.Games[2];
        Assert.Same(vm.Games[2], vm.BigPictureHeroGame);
        vm.AdvanceBigPictureSpotlight();
        vm.AttractModeGame = vm.Games[1];
        vm.ResetBigPictureSpotlight();

        Assert.Same(vm.Games[2], vm.SelectedGame);
        Assert.Same(vm.Games[2], vm.BigPictureHeroGame);
        Assert.Same(vm.Games[1], vm.AttractModeGame);
    });

    [Fact]
    public void RemovingSpotlightGameFallsBackToWorkingSelection() => OnSta(() =>
    {
        var vm = CreateViewModel();
        vm.AdvanceBigPictureSpotlight();
        vm.Games.Remove(vm.BigPictureHeroGame!);
        Assert.Same(vm.SelectedGame, vm.BigPictureHeroGame);
        Assert.True(vm.AdvanceBigPictureSpotlight());
        vm.Games.Remove(vm.BigPictureHeroGame!);
        Assert.False(vm.AdvanceBigPictureSpotlight());
    });

    [Fact]
    public void PauseFreezesSpotlightAndResumeLeavesWorkingSelectionAlone() => OnSta(() =>
    {
        var vm = CreateViewModel();
        vm.AdvanceBigPictureSpotlight();
        var hero = vm.BigPictureHeroGame;
        vm.HorizonSpotlightPaused = true;
        Assert.False(vm.AdvanceBigPictureSpotlight());
        Assert.Same(hero, vm.BigPictureHeroGame);
        Assert.Equal("Resume spotlight rotation", vm.HorizonSpotlightActionLabel);
        vm.HorizonSpotlightPaused = false;
        Assert.True(vm.AdvanceBigPictureSpotlight());
        Assert.Same(vm.Games[0], vm.SelectedGame);
    });

    [Fact]
    public void PausePreferenceSurvivesSettingsSerialization()
    {
        var settings = new AppearanceSettings { HorizonSpotlightPaused = true };
        var restored = JsonSerializer.Deserialize<AppearanceSettings>(JsonSerializer.Serialize(settings));
        Assert.True(restored!.HorizonSpotlightPaused);
        Assert.False(JsonSerializer.Deserialize<AppearanceSettings>("{}")!.HorizonSpotlightPaused);
    }

    private static MainViewModel CreateViewModel()
    {
        // No InitializeAsync or dispatcher pump: never read or persist the player's library.
        var vm = new MainViewModel();
        foreach (string title in new[] { "Selected game", "Spotlight game", "Third game" })
            vm.Games.Add(new GameEntryViewModel(new GameRecord { Title = title }));
        vm.SelectedGame = vm.Games[0];
        return vm;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Spotlight test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
