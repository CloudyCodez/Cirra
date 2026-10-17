using System.Windows;
using CloudBerry.App.Services;
using static CloudBerry.App.Tests.WindowPresentationTests;

namespace CloudBerry.App.Tests;

public sealed class HorizonEntryTests
{
    [Fact]
    public void SuccessClosesSplashAndRecordsOrderedMilestones() => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var loading = CreateWindow();
        var destination = CreateWindow();
        var service = new HorizonEntryService();
        var states = new List<HorizonEntryState>();
        bool loadingClosed = false;
        loading.Closed += (_, _) => loadingClosed = true;
        service.StateChanged += states.Add;
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            var result = await service.EnterAsync(desktop, () => loading, () => destination, (splash, _) =>
            {
                Assert.Same(destination, splash.Owner);
                Assert.True(desktop.IsVisible);
                Assert.True(destination.IsVisible);
                return Task.CompletedTask;
            });
            Assert.Equal(HorizonEntryState.Ready, result.State);
            Assert.Equal(new[] { HorizonEntryState.Preparing, HorizonEntryState.Presenting, HorizonEntryState.Ready }, states);
            Assert.Same(destination, result.Destination);
            Assert.Null(result.Error);
            Assert.True(loadingClosed);
            Assert.False(desktop.IsVisible);
            Assert.True(destination.IsVisible);
            Assert.True(result.LoadingFirstFrame <= result.DashboardFirstFrame);
            Assert.True(result.DashboardFirstFrame <= result.Ready);
            Assert.True(result.Ready <= result.Total);
            Assert.False(service.IsEntering);
        }
        finally { loading.Close(); destination.Close(); desktop.Close(); }
    });

    [Fact]
    public void RepeatAndReentrantRequestsShareOneEntry() => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var service = new HorizonEntryService();
        int factories = 0;
        Window Destination() { factories++; return CreateWindow(); }
        Task<HorizonEntryResult>? reentrant = null;
        service.StateChanged += state =>
        {
            if (state == HorizonEntryState.Preparing)
                reentrant = service.EnterAsync(desktop, CreateWindow, Destination, Dismiss);
        };
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            var first = service.EnterAsync(desktop, CreateWindow, Destination, Dismiss);
            Assert.Same(first, service.EnterAsync(desktop, CreateWindow, Destination, Dismiss));
            var result = await first;
            Assert.Same(first, reentrant);
            Assert.Equal(1, factories);
            Assert.Equal(HorizonEntryState.Ready, result.State);
            result.Destination!.Close();
        }
        finally { desktop.Close(); }
    });

    [Fact]
    public void ImmediateCancellationDoesNotCreateWindows() => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var service = new HorizonEntryService();
        Window Unexpected() => throw new InvalidOperationException("Factory must not run.");
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            var pending = service.EnterAsync(desktop, Unexpected, Unexpected, Dismiss);
            service.Cancel();
            service.Cancel();
            var result = await pending;
            Assert.Equal(HorizonEntryState.Cancelled, result.State);
            Assert.Null(result.LoadingFirstFrame);
            Assert.Null(result.Error);
            Assert.True(desktop.IsVisible);
            service.Cancel();
        }
        finally { desktop.Close(); }
    });

    [Theory]
    [InlineData("loading-loaded")]
    [InlineData("presenting")]
    [InlineData("destination-loaded")]
    [InlineData("dismiss")]
    public void CancelAtEachStageClosesOwnedWindows(string stage) => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var service = new HorizonEntryService();
        var windows = new List<Window>();
        int closed = 0;
        Window New(string kind)
        {
            var window = CreateWindow();
            windows.Add(window);
            window.Closed += (_, _) => closed++;
            if (stage == kind + "-loaded") window.Loaded += (_, _) => window.Close();
            return window;
        }
        service.StateChanged += state =>
        {
            if (stage == "presenting" && state == HorizonEntryState.Presenting) service.Cancel();
        };
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            var result = await service.EnterAsync(desktop, () => New("loading"), () => New("destination"), (loading, token) =>
            {
                if (stage == "dismiss") loading.Close();
                return Task.Delay(1, token);
            });
            Assert.Equal(HorizonEntryState.Cancelled, result.State);
            Assert.Null(result.Error);
            Assert.Null(result.Destination);
            Assert.Equal(windows.Count, closed);
            Assert.True(desktop.IsVisible);
        }
        finally { foreach (var window in windows) window.Close(); desktop.Close(); }
    });

    [Theory]
    [InlineData("preparing")]
    [InlineData("presenting")]
    [InlineData("dismiss")]
    public void DesktopClosingCancelsWithoutReopeningIt(string stage) => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var service = new HorizonEntryService();
        int opened = 0, closed = 0;
        Window New()
        {
            var window = CreateWindow();
            opened++;
            window.Closed += (_, _) => closed++;
            return window;
        }
        service.StateChanged += state =>
        {
            if ((stage == "preparing" && state == HorizonEntryState.Preparing) ||
                (stage == "presenting" && state == HorizonEntryState.Presenting)) desktop.Close();
        };
        await WindowPresentation.ShowFirstFrameAsync(desktop);
        var result = await service.EnterAsync(desktop, New, New, (_, token) =>
        {
            desktop.Close();
            return Task.Delay(1, token);
        });
        Assert.Equal(HorizonEntryState.Cancelled, result.State);
        Assert.Null(result.Error);
        Assert.False(desktop.IsVisible);
        Assert.Equal(opened, closed);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureCleansUpAndAllowsRetry(bool failDuringDismiss) => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var service = new HorizonEntryService();
        var failure = new InvalidOperationException("Injected entry failure.");
        int opened = 0, closed = 0;
        Window New()
        {
            var window = CreateWindow();
            opened++;
            window.Closed += (_, _) => closed++;
            return window;
        }
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            var result = await service.EnterAsync(desktop, New,
                () => failDuringDismiss ? New() : throw failure,
                (_, _) => Task.FromException(failure));
            Assert.Equal(HorizonEntryState.Failed, result.State);
            Assert.Same(failure, result.Error);
            Assert.Equal(opened, closed);
            Assert.True(desktop.IsVisible);
            var retry = await service.EnterAsync(desktop, New, New, Dismiss);
            Assert.Equal(HorizonEntryState.Ready, retry.State);
            retry.Destination!.Close();
            Assert.Equal(opened, closed);
        }
        finally { desktop.Close(); }
    });

    [Fact]
    public void TwentyHandoffsLeaveNoOwnedWindows() => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var service = new HorizonEntryService();
        int closed = 0;
        Window New()
        {
            var window = CreateWindow();
            window.Closed += (_, _) => closed++;
            return window;
        }
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            for (int i = 0; i < 20; i++)
            {
                var result = await service.EnterAsync(desktop, New, New, Dismiss);
                Assert.Equal(HorizonEntryState.Ready, result.State);
                result.Destination!.Close();
                desktop.Show();
                Assert.Empty(desktop.OwnedWindows.Cast<Window>());
                Assert.Equal((i + 1) * 2, closed);
            }
        }
        finally { desktop.Close(); }
    });

    [Fact]
    public void AlreadyCancelledPresentationDoesNotShowWindow() => OnSta(async () =>
    {
        var window = CreateWindow();
        using var source = new CancellationTokenSource();
        source.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                WindowPresentation.ShowFirstFrameAsync(window, cancellationToken: source.Token));
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    });

    private static Task Dismiss(Window _, CancellationToken token) => Task.Delay(1, token);
}
