using System.Reflection;
using System.Runtime.Caching;
using NonBlocking;

namespace Cache;

public class CustomLazyCache
{
    private static readonly MemoryCache Cache = new("MemoryCache");
    private static readonly ConcurrentDictionary<string, CustomCacheItem> CacheManageDictionary = new();

    public T Get<T>(string key) where T : class
    {
        if (Cache.Get(key) is not T)
        {
            return null!;
        }

        return (Cache.Get(key) as T)!;
    }

    public Task<T> GetAsync<T>(string key) where T : class
    {
        if (Cache.Get(key) is not T)
        {
            return null!;
        }
        
        return Task.FromResult(Cache.Get(key) as T)!;
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
        var value = await getValue();

        Cache.Set(key, value, DateTimeOffset.UtcNow.AddYears(1));
        CacheManageDictionary.TryRemove(key, out _);
        CacheManageDictionary.TryAdd(key, new CustomCacheItem
        {
            ExpirationDate = expirationInSecond.HasValue ? DateTime.UtcNow.AddSeconds(expirationInSecond.Value) : DateTime.UtcNow.AddMinutes(3)
        });

        return value;
    }

    public T GetOrSet<T>(string key, Func<T> getValue, int? expirationInSecond = null) where T : class
    {
        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem))
        {
            return Set(key, getValue, expirationInSecond);
        }

        var isExpired = cacheManageItem!.ExpirationDate > DateTime.UtcNow;

        if (!isExpired) return Get<T>(key);

        return cacheManageItem.Semaphore.CurrentCount == 1 ? Get<T>(key) : Set(key, getValue, expirationInSecond);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem))
        {
            return await SetAsync(key, getValue, expirationInSecond);
        }

        var isExpired = cacheManageItem!.ExpirationDate > DateTime.UtcNow;

        if (!isExpired || cacheManageItem.Semaphore.CurrentCount == 1) return await GetAsync<T>(key);

        await cacheManageItem.Semaphore.WaitAsync();
        try
        {
            return await SetAsync(key, getValue, expirationInSecond);
        } 
        finally
        {
            cacheManageItem.Semaphore.Release();
        }
    }
}

internal class CustomCacheItem
{
    public readonly SemaphoreSlim Semaphore = new(1);
    public DateTime ExpirationDate { get; set; }
}