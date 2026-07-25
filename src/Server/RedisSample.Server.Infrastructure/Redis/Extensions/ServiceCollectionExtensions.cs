
using StackExchange.Redis;
using RedisSample.Server.Infrastructure.Redis.Caching;
using Microsoft.Extensions.DependencyInjection;
namespace RedisSample.Server.Infrastructure.Redis.Extensions;


public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddRedis(
        this IServiceCollection services,
        string connectionString)
    {


        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(
                connectionString));


        services.AddScoped<IRedisService, RedisService>();

        services.AddSingleton<RedisPublisher>();
        services.AddSingleton<RedisSubscriber>();

        return services;

    }
    public static IServiceCollection IDistributedCacheRedis(
        this IServiceCollection services,
        string connectionString)
    {

        //use of : private readonly IDistributedCache _cache;
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
               // "localhost:6379";
        });


        return services;

    }
    //public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddStackExchangeRedisCache(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, Action<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions> setupAction);

}
/*
 Namespace:
Microsoft.Extensions.DependencyInjection
Assembly:
Microsoft.Extensions.Caching.StackExchangeRedis.dll
Package:
Microsoft.Extensions.Caching.StackExchangeRedis v11.0.0-preview.5.26302.115
Source:
StackExchangeRedisCacheServiceCollectionExtensions.cs
 */
