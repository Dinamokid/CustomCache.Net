using System.Runtime.CompilerServices;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task WaitAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _uses);
        return _semaphore.WaitAsync(ct);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _uses);
        _semaphore.Wait(ct);
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
}