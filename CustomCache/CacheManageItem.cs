namespace CustomCache;

public class CacheManageItem : IDisposable
{
    public SemaphoreSlim Semaphore { get; }
    private DateTime ExpirationDate { get; set; }

    public CacheManageItem(TimeSpan expiration)
    {
        ExpirationDate = DateTime.UtcNow.Add(expiration);
        Semaphore = new SemaphoreSlim(1, 1);
    }

    public void UpdateExpirationTime(TimeSpan expiration) => ExpirationDate = DateTime.UtcNow.Add(expiration);
    public bool IsExpired() => DateTime.UtcNow > ExpirationDate;
    public void Dispose() => Semaphore.Dispose();
}