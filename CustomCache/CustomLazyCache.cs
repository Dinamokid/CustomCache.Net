#nullable enable
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using NonBlocking;

namespace CustomCache;

public class CustomLazyCache
{
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private readonly ConcurrentDictionary<string, SingleEntrySemaphore> _semaphores = new();
    private readonly ConcurrentDictionary<Type, object> _factories = new(); 

    public T? Get<T>(string key) where T : class
    {
        return Cache.Get<ICacheItem<T>>(key)?.Value;
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        return Task.FromResult(Cache.Get<ICacheItem<T>>(key)?.Value);
    }

    public T Set<T>(string key, Func<T> getValue, int? expirationInSecond = null) where T : class
    {
        return GetValueFromSource(key, getValue, expirationInSecond, true);
    }

    public async Task<T> SetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        return await GetValueFromSourceAsync(key, getValue, expirationInSecond, true);
    }

    public T GetOrSet<T>(string key, Func<T> getValue, int? expirationInSecond = null) where T : class
    {
        var cacheItem = Cache.Get<ICacheItem<T>>(key);
        if (cacheItem == null)
        {
            return GetValueFromSource(key, getValue, expirationInSecond);
        }

        var semaphore = _semaphores.GetValueOrDefault(key);
        if (cacheItem.IsExpired() && (semaphore == null || semaphore.IsAvailable))
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    var __ = GetValueFromSource(key, getValue, expirationInSecond);
                }
            );
        }

        return cacheItem.Value;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class
    {
        var cacheItem = Cache.Get<ICacheItem<T>>(key);
        if (cacheItem == null)
        {
            return await GetValueFromSourceAsync(key, getValue, expirationInSecond);
        }

        var semaphore = _semaphores.GetValueOrDefault(key);
        if (cacheItem.IsExpired() && (semaphore == null || semaphore.IsAvailable))
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    var __ = GetValueFromSourceAsync(key, getValue, expirationInSecond);
                }
            );
        }

        return cacheItem.Value;
    }

    private async ValueTask<T> GetValueFromSourceAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond, bool forceSet = false) where T : class
    {
        var semaphore = GetOrCreateSemaphoreEntry(key);
        await using var _ = await semaphore.WaitAsync();
        if (TryGetCached(key, forceSet, out T cacheItemValue))
        {
            return cacheItemValue;
        }

        var value = await getValue();
        return SetValue(key, value, expirationInSecond);
    }

    private T GetValueFromSource<T>(string key, Func<T> getValue, int? expirationInSecond, bool forceSet = false) where T : class
    {
        var semaphore = GetOrCreateSemaphoreEntry(key);
        using var _ = semaphore.Wait();
        if (TryGetCached(key, forceSet, out T cacheItemValue))
        {
            return cacheItemValue;
        }

        var value = getValue();
        return SetValue(key, value, expirationInSecond);
    }

    private static bool TryGetCached<T>(string key, bool forceSet, out T cacheItemValue) where T : class
    {
        // we don't return expired values because we don't want to return stale data here
        if (!forceSet && Cache.TryGetValue<ICacheItem<T>>(key, out var cacheItem) && cacheItem.IsNotExpired())
        {
            cacheItemValue = cacheItem.Value;
            return true;
        }

        cacheItemValue = default!;
        return false;
    }

    private T SetValue<T>(string key, T value, int? expirationInSecond) where T : class
    {
        var expiration = GetExpirationTime(expirationInSecond!.Value);
        var valueType = value.GetType();

        ICacheItem<T> newCacheItem;
        if (valueType == typeof(T) || valueType.IsValueType)
        {
            newCacheItem = new CacheItem<T>(value, expiration);
        }
        else
        {
            //для того чтобы создавать инстанс CacheItem<> от типа Value, а не от типа T 
            //это нужно для того, чтобы иметь возможность добавлять в кэш и доставать из него значение используя в качестве параметра типа
            //как производный, так и базовый тип.

            //пример: CityDepartment: Department 
            //cache.Set<Department>(method, parameters, () => new CityDepartment());
            //cache.Get<CityDepartment>(method, parameters);

            var factory = (Func<object, TimeSpan, ICacheItem<T>>)_factories.GetOrAdd(
                key: valueType,
                valueFactory: CacheItemHelper.CreateFactoryMethod<T>
            );
            newCacheItem = factory(value, expiration);
        }

        Cache.Set(key, newCacheItem, newCacheItem.GetAbsoluteExpiration());
        return value;
    }


    private SingleEntrySemaphore GetOrCreateSemaphoreEntry(string key)
    {
        SingleEntrySemaphore? semaphore;
        while (!_semaphores.TryGetValue(key, out semaphore))
        {
            var newSemaphore = new SingleEntrySemaphore(key, _semaphores);
            if (!_semaphores.TryAdd(key, newSemaphore))
            {
                newSemaphore.Dispose();
            }
        }

        return semaphore;
    }

    private TimeSpan GetExpirationTime(int expirationInSecond)
    {
        return TimeSpan.FromSeconds(expirationInSecond);
    }


    private static class CacheItemHelper
    {
        private static readonly IReadOnlyList<Type> Args = new[] { typeof(object), typeof(TimeSpan) };
        private static readonly IReadOnlyList<ParameterExpression> Parameters = Args.Select(Expression.Parameter).ToArray();

        public static Func<object, TimeSpan, ICacheItem<T>> CreateFactoryMethod<T>(Type valueType)
            where T : class
        {
            var ctorInfo = GetTargetTypeConstructor(valueType);

            // given: T Value;
            // created: new CacheItem<Value_type>((Value_type)Value, TimeSpan);
            // We can do this because the compiler checks that T is a base type of Value
            var castExpression = Expression.Convert(Parameters[0], valueType);
            var newExpression = Expression.New(ctorInfo, castExpression, Parameters[1]);

            var lambda = Expression.Lambda<Func<object, TimeSpan, ICacheItem<T>>>(newExpression, Parameters);

            return lambda.Compile();
        }

        private static ConstructorInfo GetTargetTypeConstructor(Type valueType) =>
            typeof(CacheItem<>).MakeGenericType(valueType)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .First();
    }
}