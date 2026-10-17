using System.Windows;

namespace CloudBerry.App.Services;

internal static class WindowPresentation
{
    public static async Task ShowFirstFrameAsync(Window window, Window? loadingWindow = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Rendered(object? sender, EventArgs e) => ready.TrySetResult();
        void Closed(object? sender, EventArgs e) => ready.TrySetCanceled();
        window.ContentRendered += Rendered;
        window.Closed += Closed;
        try
        {
            window.Show();
            if (ready.Task.IsCanceled) await ready.Task;
            cancellationToken.ThrowIfCancellationRequested();
            // WPF only accepts an owner after its native window has been shown.
            if (loadingWindow is not null) loadingWindow.Owner = window;
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        finally
        {
            window.ContentRendered -= Rendered;
            window.Closed -= Closed;
        }
    }
}
