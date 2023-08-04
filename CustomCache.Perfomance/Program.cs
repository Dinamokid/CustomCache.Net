using CustomCache;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using NBomber.CSharp;
using NonBlocking;

ConcurrentDictionary<string, SemaphoreSlim> semaphoreDictionary = new();
ConcurrentDictionary<string, string> cache = new();
ConcurrentDictionary<string, Lazy<Task<string>>> cacheLazy = new();
CachingService libCache = new();
CustomLazyCache customLazyCache = new();

var lazyCacheOptions = new LazyCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

var fetchDataCounter = 0;

string? Get(string key)
{
    return cache.TryGetValue(key, out var value) ? value : null;
}

async Task<string> SetAsync(string key)
{
    var value = await GetValue();

    cache[key] = value;

    return value;
}

SemaphoreSlim GetOrCreateSemaphore(string key)
{
    if (semaphoreDictionary.TryGetValue(key, out var semaphore))
    {
        return semaphore;
    }

    semaphoreDictionary.TryAdd(key, new SemaphoreSlim(1));

    return semaphoreDictionary[key];
}

async Task<string> GetOrSetAsyncOld(string key)
{
    var value = Get(key) ?? await SetAsync(key);

    return value;
}

async Task<string> GetOrSetAsyncSemaphore(string key)
{
    var value = Get(key);
    if (value is not null) return value;

    var semaphore = GetOrCreateSemaphore(key);
    await semaphore.WaitAsync();
    try
    {
        value = Get(key);

        if (value is null)
        {
            return await SetAsync(key);
        }
    }
    finally
    {
        semaphore.Release();
    }

    return Get(key)!;
}

async Task<string> GetOrSetNewLazy(string key)
{
    var value = cacheLazy
        .GetOrAdd(key, _ => new Lazy<Task<string>>(GetValue))
        .Value;

    return await value;
}

async Task<string> GetOrSetLazyCacheLib(string key)
{
    return await libCache.GetOrAddAsync(key, GetValue, lazyCacheOptions);
}

async Task<string> GetOrSetCustomLazyCache(string key)
{
    return await customLazyCache.GetOrSetAsync(key, GetValue, 2);
}

async Task<string> GetValue()
{
    fetchDataCounter++;
    await Task.Delay(1000);
    return Guid.NewGuid().ToString();
}

var getOrSetAsyncOld = Scenario.Create("GetOrSetAsyncOld", async _ =>
    {
        Task.WaitAll(
            GetOrSetAsyncOld("6"),
            GetOrSetAsyncOld("7"),
            GetOrSetAsyncOld("8"),
            GetOrSetAsyncOld("9"),
            GetOrSetAsyncOld("10")
        );

        return Response.Ok();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(60)))
    .WithClean(context =>
    {
        context.Logger.Information($"Fetches: {fetchDataCounter}");
        fetchDataCounter = 0;
        cache = new ConcurrentDictionary<string, string>();
        cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        return Task.CompletedTask;
    });

var getOrSetAsyncSemaphore = Scenario.Create("GetOrSetAsyncSemaphoreDic", async _ =>
    {
        Task.WaitAll(
            GetOrSetAsyncSemaphore("1"),
            GetOrSetAsyncSemaphore("2"),
            GetOrSetAsyncSemaphore("3"),
            GetOrSetAsyncSemaphore("4"),
            GetOrSetAsyncSemaphore("5")
        );

        return Response.Ok();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(60)))
    .WithClean(context =>
    {
        context.Logger.Information($"Fetches: {fetchDataCounter}");
        fetchDataCounter = 0;
        cache = new ConcurrentDictionary<string, string>();
        cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        return Task.CompletedTask;
    });

var getOrSetNewLazy = Scenario.Create("getOrSetAsyncLazy", async _ =>
    {
        Task.WaitAll(
            GetOrSetNewLazy("1"),
            GetOrSetNewLazy("2"),
            GetOrSetNewLazy("3"),
            GetOrSetNewLazy("4"),
            GetOrSetNewLazy("5")
        );

        return Response.Ok();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(60)))
    .WithClean(context =>
    {
        context.Logger.Information($"Fetches: {fetchDataCounter}");
        fetchDataCounter = 0;
        cache = new ConcurrentDictionary<string, string>();
        cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        return Task.CompletedTask;
    });

var getOrSetLazyCacheLib = Scenario.Create("getOrSetLazyCacheLib", async _ =>
    {
        Task.WaitAll(
            GetOrSetLazyCacheLib("1"),
            GetOrSetLazyCacheLib("2"),
            GetOrSetLazyCacheLib("3"),
            GetOrSetLazyCacheLib("4"),
            GetOrSetLazyCacheLib("5")
        );

        return Response.Ok();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(60)))
    .WithClean(context =>
    {
        context.Logger.Information($"Fetches: {fetchDataCounter}");
        fetchDataCounter = 0;
        cache = new ConcurrentDictionary<string, string>();
        cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        return Task.CompletedTask;
    });

var getOrSetCustomLazyCache = Scenario.Create("getOrSetCustomLazyCache", async _ =>
    {
        Task.WaitAll(
            GetOrSetCustomLazyCache("1"),
            GetOrSetCustomLazyCache("2"),
            GetOrSetCustomLazyCache("3"),
            GetOrSetCustomLazyCache("4"),
            GetOrSetCustomLazyCache("5")
        );

        return Response.Ok();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(60)))
    .WithClean(context =>
    {
        context.Logger.Information($"Fetches: {fetchDataCounter}");
        fetchDataCounter = 0;
        cache = new ConcurrentDictionary<string, string>();
        cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
        return Task.CompletedTask;
    });

await Task.Factory.StartNew(async () =>
{
    while (true)
    {
        await Task.Delay(2000);

        cache = new ConcurrentDictionary<string, string>();
        cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
    }
});

NBomberRunner
    .RegisterScenarios(getOrSetAsyncOld)
    .WithReportFolder(@"../../../../Reports/without_lazy_cache")
    .WithReportFileName("without_lazy_cache") 
    .Run();

NBomberRunner
    .RegisterScenarios(getOrSetAsyncSemaphore)
    .WithReportFolder(@"../../../../Reports/semaphore_lazy_cache")
    .WithReportFileName("semaphore_lazy_cache")
    .Run();

NBomberRunner
    .RegisterScenarios(getOrSetNewLazy)
    .WithReportFolder(@"../../../../Reports/lazy_cache")
    .WithReportFileName("lazy_cache")
    .Run();

NBomberRunner
    .RegisterScenarios(getOrSetLazyCacheLib)
    .WithReportFolder(@"../../../../Reports/lazy_cache_lib")
    .WithReportFileName("lazy_cache_lib")
    .Run();

NBomberRunner
    .RegisterScenarios(getOrSetCustomLazyCache)
    .WithReportFolder(@"../../../../Reports/custom_lazy_cache")
    .WithReportFileName("custom_lazy_cache")
    .Run();

Console.ReadLine();