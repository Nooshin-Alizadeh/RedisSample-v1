using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace RedisSample.Server.Infrastructure.Redis;


public class RedisPublisher
{

    private readonly ISubscriber _subscriber;


    public RedisPublisher(
        IConnectionMultiplexer connection)
    {
        _subscriber =
            connection.GetSubscriber();
    }



    public async Task PublishAsync(
        string channel,
        string message)
    {

        await _subscriber.PublishAsync(
            channel,
            message);

    }

}
/*
 
 [ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{

    private readonly RedisPublisher _publisher;


    public OrdersController(
        RedisPublisher publisher)
    {
        _publisher = publisher;
    }



    [HttpPost]
    public async Task<IActionResult> Create()
    {

        // Save order in database


        await _publisher.PublishAsync(
            "notifications",
            "new order created");


        return Ok();
    }

}
 */
