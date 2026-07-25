
namespace RedisSample.Server.Infrastructure.Redis.Caching;

public interface IRedisService
{
    Task<bool> SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null);


    Task<T?> GetAsync<T>(
        string key);


    Task RemoveAsync(
        string key);


    Task<bool> ExistsAsync(
        string key);
}
