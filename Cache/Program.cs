// using System.Collections.Concurrent;
using NBomber.CSharp;
using NonBlocking;

ConcurrentDictionary<string, SemaphoreSlim> semaphoreDictionary = new();
ConcurrentDictionary<string, string> cache = new();
ConcurrentDictionary<string, Lazy<Task<string>>> cacheLazy = new();
ConcurrentDictionary<string, bool> isExpiredDictionary = new();

bool IsExpired(string key) => !isExpiredDictionary.TryGetValue(key, out _);

var counter = 0;
var fetchDataCounter = 0;

async Task<string?> GetAsync(string key)
{
    return cache.TryGetValue(key, out var value) ? value : null;
}

async Task<string> SetAsync(string key)
{
    var value = await GetValue();

    cache[key] = value;
    isExpiredDictionary.TryAdd(key, true);

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
    var value = await GetAsync(key);
    if (value == null)
    {
        value = await SetAsync(key);
    }

    return value;
}

async Task<string> GetOrSetAsyncSemaphoreDic(string key)
{
    var isExpired = IsExpired(key);
    if (!isExpired) await GetAsync(key);

    var semaphore = GetOrCreateSemaphore(key);
    await semaphore.WaitAsync();
    try
    {
        isExpired = IsExpired(key);

        if (isExpired)
        {
            return await SetAsync(key);
        }
    }
    finally
    {
        semaphore.Release();
    }

    return (await GetAsync(key))!;
}

async Task<string> GetOrSetNewLazy(string key)
{
    var value = cacheLazy
        .GetOrAdd(key, _ => new Lazy<Task<string>>(GetValue, LazyThreadSafetyMode.ExecutionAndPublication))
        .Value;

    return await value;
}

async Task<string> GetValue()
{
    await Task.Delay(1000);
    fetchDataCounter++;
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

        if (counter == 250)
        {
            isExpiredDictionary = new ConcurrentDictionary<string, bool>();
            cache = new ConcurrentDictionary<string, string>();
            counter = 0;
        }

        counter++;

        return Response.Ok();
    }).WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));

var getOrSetAsyncSemaphoreDic = Scenario.Create("GetOrSetAsyncSemaphoreDic", async _ =>
    {
        Task.WaitAll(
            GetOrSetAsyncSemaphoreDic("1"),
            GetOrSetAsyncSemaphoreDic("2"),
            GetOrSetAsyncSemaphoreDic("3"),
            GetOrSetAsyncSemaphoreDic("4"),
            GetOrSetAsyncSemaphoreDic("5")
        );
        
        if (counter == 250)
        {
            isExpiredDictionary = new ConcurrentDictionary<string, bool>();
            cache = new ConcurrentDictionary<string, string>();
            counter = 0;
        }

        counter++;
        return Response.Ok();
    }).WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));

var getOrSetNewLazy = Scenario.Create("getOrSetAsyncLazy", async _ =>
    {
        Task.WaitAll(
            GetOrSetNewLazy("1"),
            GetOrSetNewLazy("2"),
            GetOrSetNewLazy("3"),
            GetOrSetNewLazy("4"),
            GetOrSetNewLazy("5")
        );
        
        if (counter == 250)
        {
            cacheLazy = new ConcurrentDictionary<string, Lazy<Task<string>>>();
            counter = 0;
        }

        counter++;
        return Response.Ok();
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));


NBomberRunner
    .RegisterScenarios(
         //getOrSetAsyncOld //как есть
         getOrSetAsyncSemaphoreDic //как будет
         //getOrSetNewLazy
    )
    .Run();

Console.WriteLine(fetchDataCounter);