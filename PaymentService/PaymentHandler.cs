using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NEventStore;
using NLog;
using vgt_saga_serialization;
using vgt_saga_serialization.MessageBodies;

namespace vgt_saga_payment.PaymentService;

/// <summary>
/// Handles saga orders beginning, end and failures
/// Creates the appropriate saga messages
/// Handles the data in messages
/// </summary>
public class PaymentHandler
{
    /// <summary>
    /// Requests from the orchestrator
    /// </summary>
    public Channel<Message> Requests { get; }
    
    /// <summary>
    /// Messages that need to be sent out to the queues
    /// </summary>
    public Channel<Message> Publish { get; }
    
    /// <summary>
    /// current request handled
    /// </summary>
    public Message CurrentRequest { get; set; }
    
    /// <summary>
    /// current reply handled
    /// </summary>
    public Message CurrentReply { get; set; }
    private Logger _logger;
    
    private IStoreEvents EventStore { get; }
    
    /// <summary>
    /// Task of the requests handler
    /// </summary>
    public Task RequestsTask { get; set; }

    /// <summary>
    /// Token allowing tasks cancellation from the outside of the class
    /// </summary>
    public CancellationToken Token { get; } = new();

    private SemaphoreSlim _concurencySemaphore = new SemaphoreSlim(6, 6);

    /// <summary>
    /// Default constructor of the order handler class
    /// that handles data and prepares messages concerning saga orders beginning, end and failure
    /// </summary>
    /// <param name="requests"> Queue with the requests from the orchestrator </param>
    /// <param name="publish"> Queue with messages that need to be published to RabbitMQ </param>
    /// <param name="eventStore"> EventStore for the event sourcing and CQRS </param>
    /// <param name="log"> logger to log to </param>
    public PaymentHandler(Channel<Message> requests, Channel<Message> publish, IStoreEvents eventStore, Logger log)
    {
        _logger = log;
        Requests = requests;
        Publish = publish;
        EventStore = eventStore;

        _logger.Debug("Starting tasks handling the messages");
        RequestsTask = Task.Run(HandlePayments);
        _logger.Debug("Tasks handling the messages started");
    }

    private async Task HandlePayments()
    {
        while (await Requests.Reader.WaitToReadAsync(Token))
        {
            var message = await Requests.Reader.ReadAsync(Token);

            await _concurencySemaphore.WaitAsync(Token);
            
            _ = Task.Run(() => Payment(message), Token);
        }
    }

    private async Task Payment(Message message)
    {
        var rnd = new Random();
        await Task.Delay(rnd.Next(0, 100), Token);
        var result = rnd.Next(0, 1) switch
        {
            1 => SagaState.PaymentAccept,
            _ => SagaState.PaymentFailed
        };
        
        message.MessageType = MessageType.PaymentReply;
        message.MessageId += 1;
        message.State = result;
        message.Body = new PaymentReply();
        message.CreationDate = DateTime.Now;
        
        await Publish.Writer.WriteAsync(CurrentRequest, Token);

        _concurencySemaphore.Release();
    }
}