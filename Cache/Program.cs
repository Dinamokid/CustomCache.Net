using NBomber.CSharp;
using NonBlocking;

ConcurrentDictionary<string, SemaphoreSlim> semaphoreDictionary = new();
ConcurrentDictionary<string, string> cache = new();
ConcurrentDictionary<string, Lazy<Task<string>>> cacheLazy = new();

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

        return Response.Ok();
    }).WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));

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
        
        return Response.Ok();
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));

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
    .RegisterScenarios(
         getOrSetAsyncOld //как есть
         //getOrSetAsyncSemaphore //как будет
         //getOrSetNewLazy
    )
    .Run();

Console.WriteLine(fetchDataCounter);