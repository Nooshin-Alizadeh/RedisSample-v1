using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace RedisSample.Server.Infrastructure.Redis;


public class RedisSubscriber
{

    private readonly ISubscriber _subscriber;



    public RedisSubscriber(
        IConnectionMultiplexer connection)
    {

        _subscriber =
            connection.GetSubscriber();

    }



    public async Task SubscribeAsync()
    {

        await _subscriber.SubscribeAsync(
            "notifications",
            (channel, message) =>
            {

                Console.WriteLine(
                    $"Channel: {channel}");

                Console.WriteLine(
                    $"Message: {message}");

            });

    }

}
/*
 public class RedisListenerService 
    : BackgroundService
{

    private readonly RedisSubscriber _subscriber;


    public RedisListenerService(
        RedisSubscriber subscriber)
    {
        _subscriber = subscriber;
    }



    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {

        await _subscriber.SubscribeAsync();


        while(!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(
                1000,
                stoppingToken);
        }

    }
}
 */
