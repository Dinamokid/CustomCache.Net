using System.Runtime.Caching;
namespace CustomCache;

public class CustomLazyCache : ICustomCache
{
    private static readonly MemoryCache Cache = new("CustomLazyCache");
    private static readonly CacheItemPolicy CacheOptions = new();
    public static readonly NonBlocking.ConcurrentDictionary<string, CacheManageItem> CacheManageDictionary = new();

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem) || Cache.Get(key) == null)
        {
            return await GetValueFromSource(key, getValue, expirationInSecond);
        }
        
        if (cacheManageItem.IsNotExpired())
        {
            return Cache.Get(key) as T;
        }
        
        if (cacheManageItem.IsExpired() && cacheManageItem.Semaphore.CurrentCount == 1)
        {
            _ = Task.Run(() => GetValueFromSource(key, getValue, expirationInSecond));
        }
        
        return Cache.Get(key) as T;
    }
    
    private async Task<T> GetValueFromSource<T>(string key, Func<Task<T>> getValue, int? expirationInSecond) where T : class
    {
        CacheManageDictionary.TryAdd(key, new CacheManageItem(TimeSpan.FromSeconds(0)));
        var cacheManageItem = CacheManageDictionary[key];

        await cacheManageItem.Semaphore.WaitAsync();
        try
        {
            if (cacheManageItem.IsNotExpired()) return Cache.Get(key) as T;
            
            var value = await getValue();
            Cache.Set(key, value, CacheOptions);
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