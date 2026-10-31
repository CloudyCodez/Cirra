namespace CloudBerry.App.Services;

/// <summary>Rejects overlapping operations while allowing independent work to proceed.</summary>
internal sealed class InFlightOperations
{
    private readonly object _sync = new();
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);

    public IDisposable? TryBegin(string key)
    {
        lock (_sync)
        {
            return _active.Add(key) ? new Lease(this, key) : null;
        }
    }

    private sealed class Lease(InFlightOperations owner, string key) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (owner._sync) owner._active.Remove(key);
        }
    }
}
