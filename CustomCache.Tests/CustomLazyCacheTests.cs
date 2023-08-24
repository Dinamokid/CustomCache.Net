namespace CustomCache.Tests;

public class CustomLazyCacheTests
{
    private int _fetchCount;

    [TearDown]
    public void Cleanup()
    {
        _fetchCount = 0;
    }

    [Test]
    public async Task CacheWorksLikeCache()
    {
        var cache = new CustomLazyCache();

        var key = Guid.NewGuid().ToString();
        
        var result1 = await cache.GetOrSetAsync(key, async () => await GetValue(), 2);
        var result2 = await cache.GetOrSetAsync(key, async () => await GetValue(), 2);

        Assert.That(result2, Is.EqualTo(result1));
    }

    [Test]
    public void CacheMustBeLazy()
    {
        var cache = new CustomLazyCache();
        var x = 0;
        var y = 0;
        var rnd = Random.Shared;
            
        var key = Guid.NewGuid().ToString();
            
        var threads = Enumerable.Range(0, 10000).Select(_ => new Thread(() =>
        {
            Interlocked.Increment(ref y);
            Thread.Sleep(rnd.Next(15, 20));
            cache.GetOrSetAsync(key, () =>
            {
                Interlocked.Increment(ref x);
                Thread.Sleep(rnd.Next(10,20));
                return Task.FromResult(new object());
            }, 60);
        })).ToList();
            
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.That(Volatile.Read(ref x), Is.EqualTo(1));
        Assert.That(Volatile.Read(ref y), Is.EqualTo(10000));
    }
    
    [Test]
    public async Task CacheForManyKeysMustBeWorkRight()
    {
        var cache = new CustomLazyCache();

        var task1 = cache.GetOrSetAsync(Guid.NewGuid().ToString(), GetValue, 10);
        var task2 = cache.GetOrSetAsync(Guid.NewGuid().ToString(), GetValue, 10);

        await Task.WhenAll(task1, task2);

        var result1 = await task1;
        var result2 = await task2;
        
        Assert.That(_fetchCount, Is.EqualTo(2));
        Assert.That(result1, Is.Not.EqualTo(result2));
    }
    
    [Test]
    public async Task CacheMustExpiring()
    {
        var cache = new CustomLazyCache();
        var key = Guid.NewGuid().ToString();

        var result1 = await cache.GetOrSetAsync(key, GetValue, 1);
        await Task.Delay(TimeSpan.FromSeconds(3));
        
        var result2 = await cache.GetOrSetAsync(key, GetValue, 1);
        await Task.Delay(TimeSpan.FromSeconds(3));
        
        Assert.That(_fetchCount, Is.EqualTo(2));
        Assert.That(result2, Is.EqualTo(result1));
    }
    
    async Task<string> GetValue()
    {
        _fetchCount++;
        await Task.Delay(1000);
        return Guid.NewGuid().ToString();
    }
}