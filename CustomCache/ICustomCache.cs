namespace CustomCache;

public interface ICustomCache
{
    ValueTask<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class;
}