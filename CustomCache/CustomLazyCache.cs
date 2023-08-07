using Microsoft.Extensions.Caching.Memory;
using NonBlocking;
namespace CustomCache;

public class CustomLazyCache : ICustomCache
{
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    public static readonly ConcurrentDictionary<string, CacheManageItem> CacheManageDictionary = new();

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        var value = Cache.Get(key);

        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem) || value == null)
        {
            return await GetValueFromSource(key, getValue, expirationInSecond);
        }
        
        if (cacheManageItem.IsExpired() && cacheManageItem.Semaphore.CurrentCount == 1)
        {
            GetValueFromSource(key, getValue, expirationInSecond);
        }
        
        return value as T;
    }
    
    private async ValueTask<T> GetValueFromSource<T>(string key, Func<Task<T>> getValue, int? expirationInSecond) where T : class
    {
        var cacheManageItem = CacheManageDictionary.GetOrAdd(key, new CacheManageItem(TimeSpan.FromMilliseconds(0)));
        
        await cacheManageItem.Semaphore.WaitAsync();
        try
        {
            if (cacheManageItem.IsNotExpired()) return Cache.Get(key) as T;
            
            var value = await getValue();
            Cache.Set(key, value);
            cacheManageItem.ChangeExpirationTime(GetExpirationTime(expirationInSecond));
        
            return value;
        }
        finally
        {
            cacheManageItem.Semaphore.Release();
        }
    }

    private TimeSpan GetExpirationTime(int? expirationInSecond = null) =>
        expirationInSecond.HasValue ? TimeSpan.FromSeconds(expirationInSecond.Value) : TimeSpan.FromMinutes(3);
}

public class CacheManageItem
{
    public SemaphoreSlim Semaphore { get; }
    private DateTime ExpirationDate { get; set; }

    public CacheManageItem(TimeSpan expiration)
    {
        ExpirationDate = DateTime.UtcNow.Add(expiration);
        Semaphore = new SemaphoreSlim(1);
    }

    public void ChangeExpirationTime(TimeSpan expiration)
    {
        ExpirationDate = DateTime.UtcNow.Add(expiration);
    }

    public bool IsExpired() => DateTime.UtcNow > ExpirationDate;
    public bool IsNotExpired() => DateTime.UtcNow < ExpirationDate;
}