using System.Runtime.Caching;
namespace CustomCache;

public class CustomLazyCache
{
    private static readonly MemoryCache Cache = new("CustomLazyCache");
    private static readonly NonBlocking.ConcurrentDictionary<string, CustomCacheItem> CacheManageDictionary = new();

    public T Get<T>(string key) where T : class
    {
        return Cache.Get(key) is T ? Cache.Get(key) as T : null;
    }

    public Task<T> GetAsync<T>(string key) where T : class
    {
        return Task.FromResult(Cache.Get(key) is T ? Cache.Get(key) as T : null);
    }

    public T Set<T>(string key, Func<T> getValue, int? expirationInSecond = null) where T : class
    {
        var value = getValue();

        Cache.Set(key, value, DateTimeOffset.UtcNow.AddYears(1));
        CacheManageDictionary.TryRemove(key, out _);
        CacheManageDictionary.TryAdd(key, new CustomCacheItem
        {
            ExpirationDate = expirationInSecond.HasValue ? DateTime.UtcNow.AddSeconds(expirationInSecond.Value) : DateTime.UtcNow.AddMinutes(3)
        });

        return value;
    }

    public async Task<T> SetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        CacheManageDictionary.TryRemove(key, out _);
        CacheManageDictionary.TryAdd(key, new CustomCacheItem
        {
            ExpirationDate = expirationInSecond.HasValue ? DateTime.UtcNow.AddSeconds(expirationInSecond.Value) : DateTime.UtcNow.AddMinutes(3)
        });

        var value = await getValue();
        
        Cache.Set(key, value, DateTimeOffset.UtcNow.AddYears(1));
        
        return value;
    }

    public T? GetOrSet<T>(string key, Func<T?> getValue, int? expirationInSecond = null) where T : class
    {
        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem))
        {
            return Set(key, getValue, expirationInSecond);
        }

        var isExpired = cacheManageItem!.ExpirationDate <= DateTime.UtcNow;

        var result = Get<T>(key);
        if (!isExpired && result != null)
        {
            return result;
        }

        return Set(key, getValue, expirationInSecond);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem))
        {
            CacheManageDictionary.TryRemove(key, out _);
            CacheManageDictionary.TryAdd(key, new CustomCacheItem
            {
                ExpirationDate = expirationInSecond.HasValue ? DateTime.UtcNow.AddSeconds(expirationInSecond.Value) : DateTime.UtcNow.AddMinutes(3)
            });
            
            cacheManageItem = CacheManageDictionary[key];
        }
        
        var isExpired = cacheManageItem!.ExpirationDate <= DateTime.UtcNow;
        
        var result = await GetAsync<T>(key);
        if (result != null && (!isExpired || cacheManageItem.Semaphore.CurrentCount == 1))
        {
            return result;
        }
        
        await cacheManageItem.Semaphore.WaitAsync();
        try
        {
            isExpired = cacheManageItem!.ExpirationDate <= DateTime.UtcNow;
            result = await GetAsync<T>(key);
        
            if (isExpired || result == null)
            {
                return await SetAsync(key, getValue, expirationInSecond);
            }
        }
        finally
        {
            cacheManageItem.Semaphore.Release();
        }
        
        return await GetAsync<T>(key);
    }
    
    private CustomCacheItem GetOrCreateSemaphore(string key, int? expirationInSecond = null)
    {
        if (CacheManageDictionary.TryGetValue(key, out var customCacheItem))
        {
            return customCacheItem;
        }

        CacheManageDictionary.TryAdd(key, new CustomCacheItem()
        {
            ExpirationDate = expirationInSecond.HasValue ? DateTime.UtcNow.AddSeconds(expirationInSecond.Value) : DateTime.UtcNow.AddMinutes(3)
        });

        return CacheManageDictionary[key];
    }
}

internal class CustomCacheItem
{
    public SemaphoreSlim Semaphore { get; set; } = new(1);
    public DateTime ExpirationDate { get; set; }
}