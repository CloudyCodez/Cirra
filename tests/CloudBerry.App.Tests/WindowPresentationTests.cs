using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CloudBerry.App.Services;

namespace CloudBerry.App.Tests;

public sealed class WindowPresentationTests
{
    [Fact]
    public void HandoffWaitsForFirstFrame() => OnSta(async () =>
    {
        var window = CreateWindow();
        bool rendered = false;
        window.ContentRendered += (_, _) => rendered = true;
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(window);
            Assert.True(rendered);
            Assert.True(window.IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ClosingDuringEntryCancelsHandoff() => OnSta(async () =>
    {
        var window = CreateWindow();
        window.Loaded += (_, _) => window.Close();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WindowPresentation.ShowFirstFrameAsync(window));
    });

    [Fact]
    public void LoadingOwnerTransfersOnlyAfterDestinationIsShown() => OnSta(async () =>
    {
        var desktop = CreateWindow();
        var loading = CreateWindow();
        var destination = CreateWindow();
        try
        {
            await WindowPresentation.ShowFirstFrameAsync(desktop);
            loading.Owner = desktop;
            await WindowPresentation.ShowFirstFrameAsync(loading);
            destination.Owner = desktop;
            await WindowPresentation.ShowFirstFrameAsync(destination, loading);
            Assert.Same(destination, loading.Owner);
            Assert.True(loading.IsVisible);
            Assert.True(destination.IsVisible);
        }
        finally { loading.Close(); destination.Close(); desktop.Close(); }
    });

    internal static Window CreateWindow() => new()
    {
        Left = -10000, Top = -10000, Width = 200, Height = 100, ShowInTaskbar = false,
        Content = new TextBlock { Text = "Ready" }
    };

    internal static void OnSta(Func<Task> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await action(); }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.InvokeShutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Window handoff timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
