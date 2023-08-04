namespace CustomCache.Tests;

public class CustomLazyCacheTests
{
    private int _fetchCount;

    [Test]
    public async Task CacheWorksLikeCache()
    {
        var cache = new CustomLazyCache();

        var result1 = await cache.GetOrSetAsync("test", async () => await GetValue(), 2);
        var result2 = await cache.GetOrSetAsync("test", async () => await GetValue(), 2);

        Assert.That(result2, Is.EqualTo(result1));
    }

    [Test]
    public async Task CacheMustBeLazy()
    {
        var cache = new CustomLazyCache();

        var task1 = cache.GetOrSetAsync("test", GetValue, 10);
        var task2 = cache.GetOrSetAsync("test", GetValue, 10);

        await Task.WhenAll(task1, task2);

        var result1 = await task1;
        var result2 = await task2;
        
        Assert.That(_fetchCount, Is.EqualTo(1));
        Assert.That(result2, Is.EqualTo(result1));
    }

    async Task<string> GetValue()
    {
        _fetchCount++;
        await Task.Delay(1000);
        return Guid.NewGuid().ToString();
    }
}