using Microsoft.Extensions.Caching.Memory;
using NonBlocking;
namespace CustomCache;

public class CustomLazyCache : ICustomCache
{
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private static readonly ConcurrentDictionary<string, CacheManageItem> CacheManageDictionary = new();

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        var value = Cache.Get(key);

        if (!CacheManageDictionary.TryGetValue(key, out var cacheManageItem) || value == null)
        {
            return await GetValueFromSource(key, getValue, expirationInSecond);
        }

        if (cacheManageItem.IsExpired() && cacheManageItem.Semaphore.CurrentCount == 1)
        {
            _ = Task.Run(() => GetValueFromSource(key, getValue, expirationInSecond));
        }

        return value as T;
    }

    private async ValueTask<T> GetValueFromSource<T>(string key, Func<Task<T>> getValue, int? expirationInSecond) where T : class
    {
        var cacheManageItem = GetOrCreateCacheItem(key);
        
        await cacheManageItem.Semaphore.WaitAsync();
        try
        {
            T value;
            if (cacheManageItem.IsExpired())
            {
                value = await getValue();
                Cache.Set(key, value);
                cacheManageItem.UpdateExpirationTime(GetExpirationTime(expirationInSecond));
            }
            else
            {
                value = Cache.Get(key) as T;
            }

            return value;
        }
        finally
        {
            cacheManageItem.Semaphore.Release();
        }
    }
    
    private static CacheManageItem GetOrCreateCacheItem(string key)
    {
        CacheManageItem cacheManageItem;

        if (CacheManageDictionary.TryGetValue(key, out cacheManageItem))
        {
            return cacheManageItem;
        }
        
        var tempItem = new CacheManageItem(TimeSpan.Zero);
        var added = CacheManageDictionary.TryAdd(key, tempItem);
        if (added)
        {
            cacheManageItem = tempItem;
        }
        else
        {
            tempItem.Dispose();
            cacheManageItem = CacheManageDictionary[key];
        }

        return cacheManageItem;
    }

    private TimeSpan GetExpirationTime(int? expirationInSecond = null) =>
        expirationInSecond.HasValue ? TimeSpan.FromSeconds(expirationInSecond.Value) : TimeSpan.FromMinutes(1);
}