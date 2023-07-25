using System.Collections.Concurrent;
using NBomber.CSharp;

ConcurrentDictionary<string, SemaphoreSlim> semaphoreDictionary = new();
ConcurrentDictionary<string, string> cache = new();
ConcurrentDictionary<string, object> cacheLazy = new();
ConcurrentDictionary<string, bool> isExpiredDictionary = new();

bool IsExpired(string key) => !isExpiredDictionary.TryGetValue(key, out _);

var counter = 0;
var fetchDataCounter = 0;

async Task<string?> GetAsync(string key)
{
    return cache.TryGetValue(key, out var value) ? value : null;
}

async Task<string?>? GetLazyAsync(string key)
{
    var newLazyValue = new Lazy<Task<string>>(GetValue, LazyThreadSafetyMode.ExecutionAndPublication);
    object lazyValue = null;
    try
    {
        // Получаем лок на вызов valueFactory
        lazyValue = cacheLazy.GetOrAdd(key, newLazyValue);

        // Ждем, пока выполнится valueFactory.
        // Так как это Lazy, то valueFactory вызовется только один раз,
        // независимо от того, сколько потоков одновременно пытаются получить результат
        var value = await ((Lazy<Task<string>>) lazyValue).Value;
        if (cacheLazy.TryGetValue(key, out var current) && ReferenceEquals(current, newLazyValue))
        {
            SetLazy(key, value);
            return value;
        }
        else
        {
            // Если из _factoryCalls мы достали другое значние, а не newLazyValue
            // это значит, в другом потоке тоже был вызван метод SetAsync для этого же ключа.
            // Поэтому мы просто возвращаем значение, а в кэш значение положит другой поток.
            return value;
        }
    }
    finally
    {
        if (ReferenceEquals(newLazyValue, lazyValue))
        {
            cacheLazy.TryRemove(key, out lazyValue);
        }
    }
}

async Task<string> SetAsync(string key)
{
    var value = await GetValue();

    cache[key] = value;
    isExpiredDictionary.TryAdd(key, true);

    return value;
}

async Task<string> SetLazyAsync(string key)
{
    var value = await GetValue();
    
    cacheLazy[key] = value;
    cache[key] = value;
    isExpiredDictionary.TryAdd(key, true);

    return value;
}

string SetLazy(string key, string value)
{
    cacheLazy[key] = value;
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

async Task<string> GetOrSetAsyncLazy(string key)
{
    if (!cache.TryGetValue(key, out var value))
    {
        value = await GetLazyAsync(key) ?? await SetLazyAsync(key);
    }

    return value;
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
            isExpiredDictionary.Clear();
            cache.Clear();
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
            isExpiredDictionary.Clear();
            cache.Clear();
            counter = 0;
        }

        counter++;
        return Response.Ok();
    }).WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));

var getOrSetAsyncLazy = Scenario.Create("getOrSetAsyncLazy", async _ =>
    {
        Task.WaitAll(
            GetOrSetAsyncLazy("1"),
            GetOrSetAsyncLazy("2"),
            GetOrSetAsyncLazy("3"),
            GetOrSetAsyncLazy("4"),
            GetOrSetAsyncLazy("5")
        );
        
        if (counter == 250)
        {
            cache.Clear();
            cacheLazy.Clear();
            counter = 0;
        }

        counter++;
        return Response.Ok();
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(50, TimeSpan.FromSeconds(10)));


NBomberRunner
    .RegisterScenarios(
         getOrSetAsyncOld //как есть
        //getOrSetAsyncSemaphoreDic //как будет
         // getOrSetAsyncLazy
    )
    .Run();

Console.WriteLine(fetchDataCounter);