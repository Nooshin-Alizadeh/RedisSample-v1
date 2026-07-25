
using StackExchange.Redis;
using RedisSample.Server.Infrastructure.Redis.Caching;

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


        return services;

    }

}
