using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
namespace RedisSample.Server.Infrastructure.Redis.Caching;




public class RedisService : IRedisService
{

    private readonly IDatabase _database;


    public RedisService(
        IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }



    #region Main Redis Object
    public async Task<bool> SetAsync<T>(
   string key,
   T value,
   TimeSpan? expiry = null)
    {

        var json =
            JsonSerializer.Serialize(value);

        //    await _database.StringSetAsync(
        //key,
        //json,
        //expiry,
        //When.Always);
        return await _database.StringSetAsync(
            key,
            json,
            expiry.Value);
    }

    public async Task<T?> GetAsync<T>(
        string key)
    {

        var value =
            await _database.StringGetAsync(key);


        if (!value.HasValue)
            return default;


        return JsonSerializer.Deserialize<T>(
            value.ToString());
    }

    public async Task RemoveAsync(
        string key)
    {
        await _database.KeyDeleteAsync(key);
    }
    public async Task<bool> ExistsAsync(
        string key)
    {
        return await _database.KeyExistsAsync(key);
    }
    public async Task<TimeSpan?> Remainn(string key)
    {
        var ttl = await _database.KeyTimeToLiveAsync(key);

        return ttl == TimeSpan.Zero ? TimeSpan.Zero : ttl;
    }
    #endregion

    #region Hash
    public async Task HashSetAsync(string key, HashEntry[] hashFields)
    {
        await _database.HashSetAsync(key, hashFields);
    }
    public async Task<HashEntry[]?> HashGetAllAsync(string key)
    {
        HashEntry[]? hashFields = await _database.HashGetAllAsync(key);
        return hashFields;
    }
    #endregion


    #region RedisList(for Email ,sms) //Producer  ---->  Redis List  ---->  Consumer  
    public async Task Producer_ListRightPushAsync(RedisKey key, RedisValue value)
    {
        await _database.ListRightPushAsync(key, value);
    }
    public async Task Producer_ListRightPushAsync(RedisKey key, RedisValue[] values)
    {
        await _database.ListRightPushAsync(key, values);
    }
    public async Task<RedisValue[]> Consumer_ListLeftPopAsync(RedisKey key, long count)
    {
        var value = await _database.ListLeftPopAsync(key, count);
        return value;
    }
    #endregion

    #region Counter//when you need the count of sth
    /*
     * write :await _database.StringIncrementAsync(
    "article:100:views");
    read :var views = await _database.StringGetAsync(
    "article:100:views");

     */
    #endregion

    #region Cache Aside Pattern
    // set in get , remove in update
    #endregion

    #region Lock
    private async Task RedisLock(string key)
    {
        var acquired = await _database.StringSetAsync(
    "lock:order:100",
    Guid.NewGuid().ToString(),
    TimeSpan.FromSeconds(30),
    When.NotExists);

        if (!acquired)
        {
            throw new Exception("Order is being processed");
        }

        try
        {
            // process order
        }
        finally
        {
            await _database.KeyDeleteAsync("lock:order:100");
        }
    }
    #endregion

    #region Pub/Sub -- Realtime  - Microservice

    //public async Task PublishAsync(RedisChannel channel, RedisValue message)
    public async Task<long> RedisPublishAsync(string key, string value)
    {
        return await subscriber.PublishAsync(key, value);
    }
    public async Task<long> RedisSubscribeAsync(string key, string value)
    {
        await subscriber.SubscribeAsync(
    key,
    (channel, message) =>
    {
        Console.WriteLine(message);
    });
    }


    #endregion

    #region Sliding Expiration // add extra 30 minute
    DistributedCacheEntryOptions SlidingExpiration= new DistributedCacheEntryOptions
{
        SlidingExpiration =
        TimeSpan.FromMinutes(30)
};
    #endregion
}

