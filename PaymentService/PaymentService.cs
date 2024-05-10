using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NEventStore;
using NEventStore.Serialization.Json;
using NLog;
using Npgsql;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using vgt_saga_serialization;

namespace vgt_saga_payment.PaymentService;

/// <summary>
/// Saga Payment service;
/// handles all payments in the transaction.
/// </summary>
public class PaymentService : IDisposable
{
    private readonly PaymentQueueHandler _queues;
    private readonly Logger _logger;
    private readonly IConfiguration _config;
    private readonly Utils _jsonUtils;
    private readonly IStoreEvents _eventStore;
    
    private readonly Channel<Message> _payments;
    private readonly Channel<Message> _publish;
    private readonly PaymentHandler _paymentHandler;
    
    /// <summary>
    /// Allows tasks cancellation from the outside of the class
    /// </summary>
    public CancellationToken Token { get; } = new();

    /// <summary>
    /// Constructor of the PaymentService class.
    /// Initializes PaymentService object.
    /// Creates, initializes and opens connections to the database and rabbitmq
    /// based on configuration data present and handled by specified handling objects.
    /// Throws propagated exceptions if the configuration data is nowhere to be found.
    /// </summary>
    /// <param name="config"> Configuration with the connection params </param>
    /// <param name="lf"> Logger factory to use by the event store </param>
    /// <exception cref="ArgumentException"> Which variable is missing in the configuration </exception>
    /// <exception cref="BrokerUnreachableException"> Couldn't establish connection with RabbitMQ </exception>
    public PaymentService(IConfiguration config, ILoggerFactory lf)
    {
        _logger = LogManager.GetCurrentClassLogger();
        _config = config;

        _jsonUtils = new Utils(_logger);
        _payments = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions()
            { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true });
        
        var connStr = SecretUtils.GetConnectionString(_config, "DB_NAME_PAYM", _logger);
        
        _eventStore = Wireup.Init()
            .WithLoggerFactory(lf)
            .UsingInMemoryPersistence()
            .UsingSqlPersistence(NpgsqlFactory.Instance, connStr)
            .InitializeStorageEngine()
            .UsingJsonSerialization()
            .Compress()
            .Build();

        _publish = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions()
            { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true });
        
        _paymentHandler = new PaymentHandler(_payments, _publish, _eventStore, _logger);

        _queues = new PaymentQueueHandler(_config, _logger);
        
        _queues.AddRepliesConsumer(SagaOrdersEventHandler);
    }

    /// <summary>
    /// Publishes made messages to the right queues
    /// </summary>
    private async Task RabbitPublisher()
    {
        while (await _publish.Reader.WaitToReadAsync(Token))
        {
            var message = await _publish.Reader.ReadAsync(Token);

            _queues.PublishToOrchestrator( _jsonUtils.Serialize(message));
        }
    }

    /// <summary>
    /// Event Handler that hooks to the event of the queue consumer.
    /// Handles incoming replies from the RabbitMQ and routes them to the appropriate tasks.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="ea"></param>
    private void SagaOrdersEventHandler(object? sender, BasicDeliverEventArgs ea)
    {
        _logger.Debug("Received response | Tag: {tag}", ea.DeliveryTag);
        var body = ea.Body.ToArray();

        var reply = _jsonUtils.Deserialize(body);

        if (reply == null) return;

        var message = reply.Value;

        // send message reply to the appropriate task
        var result = _payments.Writer.TryWrite(message);
        
        if (result) _logger.Debug("Replied routed successfuly to {type} handler", message.MessageType.ToString());
        else _logger.Warn("Something went wrong in routing to {type} handler", message.MessageType.ToString());

        _queues.PublishTagResponse(ea, result);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _logger.Debug("Disposing");
        _queues.Dispose();
    }
}