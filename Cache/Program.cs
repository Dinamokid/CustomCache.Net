using System.Collections.Concurrent;
using System.Diagnostics;
using NBomber.CSharp;

ConcurrentDictionary<string, SemaphoreSlim> semaphoreDictionary = new();
SemaphoreSlim _semaphore = new(1);
ConcurrentDictionary<string, string> cache = new();
ConcurrentDictionary<string, bool> isExpiredDictionary = new();

bool IsExpired(string key) => !isExpiredDictionary.TryGetValue(key, out _);

var counter = 0;
var fetchDataCounter = 0;

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

async Task<string> GetOrSetAsyncSemaphore(string key)
{
    var isExpired = IsExpired(key);
    if (!isExpired) await GetAsync(key);

    await _semaphore.WaitAsync();
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
        _semaphore.Release();
    }

    return (await GetAsync(key))!;
}

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

async Task<string> GetValue()
{
    await Task.Delay(1000);
    fetchDataCounter++;
    return Guid.NewGuid().ToString();
}

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

var getOrSetAsyncSemaphore = Scenario.Create("GetOrSetAsyncSemaphore", async _ =>
    {
        Task.WaitAll(
            GetOrSetAsyncSemaphore("11"),
            GetOrSetAsyncSemaphore("12"),
            GetOrSetAsyncSemaphore("13"),
            GetOrSetAsyncSemaphore("14"),
            GetOrSetAsyncSemaphore("15")
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

var sw = new Stopwatch();
//
// sw.Start();
// await Task.WhenAll(GetOrSetAsyncOld("111"), GetOrSetAsyncOld("111"), GetOrSetAsyncOld("111"));
// sw.Stop();
// Console.WriteLine(sw.ElapsedMilliseconds);
// isExpiredDictionary.Clear();
// sw.Reset();
//
// sw.Start();
// await Task.WhenAll(GetOrSetAsyncSemaphore("111"), GetOrSetAsyncSemaphore("111"), GetOrSetAsyncSemaphore("111"));
// sw.Stop();
// Console.WriteLine(sw.ElapsedMilliseconds);
// isExpiredDictionary.Clear();
// sw.Reset();
//
// sw.Start();
// await Task.WhenAll(GetOrSetAsyncSemaphoreDic("111"), GetOrSetAsyncSemaphoreDic("111"), GetOrSetAsyncSemaphoreDic("111"));
// sw.Stop();
// Console.WriteLine(sw.ElapsedMilliseconds);
// isExpiredDictionary.Clear();
// sw.Reset();

// sw.Start();
// for (var i = 0; i < 5; i++)
// {
//     await GetOrSetAsyncOld(i.ToString());
// }
// for (var i = 0; i < 5; i++)
// {
//     await GetOrSetAsyncOld(i.ToString());
// }
// sw.Stop();
// Console.WriteLine(sw.ElapsedMilliseconds);
// isExpiredDictionary.Clear();
// cache.Clear();
// sw.Reset();
//
// sw.Start();
// for (var i = 0; i < 5; i++)
// {
//     await GetOrSetAsyncSemaphoreDic(i.ToString());
// }
// for (var i = 0; i < 5; i++)
// {
//     await GetOrSetAsyncSemaphoreDic(i.ToString());
// }
// sw.Stop();
// Console.WriteLine(sw.ElapsedMilliseconds);
// isExpiredDictionary.Clear();
// cache.Clear();
// sw.Reset();

NBomberRunner
    .RegisterScenarios(
        //getOrSetAsyncOld //как есть
        // getOrSetAsyncSemaphore
        getOrSetAsyncSemaphoreDic //как будет
    )
    .Run();

Console.WriteLine(fetchDataCounter);