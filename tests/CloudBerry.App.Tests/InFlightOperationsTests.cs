using CloudBerry.App.Services;

namespace CloudBerry.App.Tests;

public sealed class InFlightOperationsTests
{
    [Fact]
    public void RejectsSameOperationWhileAllowingIndependentGames()
    {
        var operations = new InFlightOperations();
        using var first = operations.TryBegin("launch:one");
        Assert.NotNull(first);
        Assert.Null(operations.TryBegin("launch:one"));
        using var second = operations.TryBegin("launch:two");
        Assert.NotNull(second);
    }

    [Fact]
    public void OldLeaseCannotReleaseANewerOperation()
    {
        var operations = new InFlightOperations();
        var old = operations.TryBegin("import")!;
        old.Dispose();
        using var current = operations.TryBegin("import");
        old.Dispose();
        Assert.Null(operations.TryBegin("import"));
    }

    [Fact]
    public async Task FailureReleasesTheOperationForRetry()
    {
        var operations = new InFlightOperations();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var operation = operations.TryBegin("import");
            await Task.Yield();
            throw new InvalidOperationException("Failed to save");
        });
        using var retry = operations.TryBegin("import");
        Assert.NotNull(retry);
    }
}
