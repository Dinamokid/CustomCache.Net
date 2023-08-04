namespace CustomCache;

public interface ICustomCache
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getValue, int? expirationInSecond = null) where T : class;
}