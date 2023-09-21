using NonBlocking;
namespace CustomCache;

public sealed class SingleEntrySemaphore : IDisposable
{
    private volatile int _uses;
    private readonly SemaphoreSlim _semaphore;
    private readonly string _key;
    private readonly ConcurrentDictionary<string, SingleEntrySemaphore> _registry;
    private bool _disposed;

    public SingleEntrySemaphore(string key, ConcurrentDictionary<string, SingleEntrySemaphore> registry)
    {
        _semaphore = new SemaphoreSlim(1, 1);
        _key = key;
        _registry = registry;
        _uses = 0;
    }

    ~SingleEntrySemaphore()
    {
        Dispose(false);
    }

    public async Task<IAsyncDisposable> WaitAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _uses);
        await _semaphore.WaitAsync(ct);
        return new Section(this);
    }

    public IDisposable Wait(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _uses);
        _semaphore.Wait(ct);
        return new Section(this);
    }

    public bool IsAvailable
        => _semaphore.CurrentCount == 1;

    public void Release()
    {
        _semaphore.Release();
        Interlocked.Decrement(ref _uses);
        if (_uses <= 0)
        {
            _registry.TryRemove(_key, out _);
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _semaphore.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private sealed class Section : IAsyncDisposable, IDisposable
    {
        private readonly SingleEntrySemaphore _semaphore;

        public Section(SingleEntrySemaphore semaphore) => _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }

        public void Dispose() => _semaphore.Release();
    }
}