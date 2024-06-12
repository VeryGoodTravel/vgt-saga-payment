using System.Threading.Channels;
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

    private Logger _logger;

    /// <summary>
    /// Task of the requests handler
    /// </summary>
    public Task RequestsTask { get; set; }

    /// <summary>
    /// Token allowing tasks cancellation from the outside of the class
    /// </summary>
    public CancellationToken Token { get; } = new();

    private SemaphoreSlim _concurencySemaphore = new SemaphoreSlim(10, 10);

    private readonly int _minDelay;
    private readonly int _maxDelay;

    /// <summary>
    /// Default constructor of the order handler class
    /// that handles data and prepares messages concerning saga orders beginning, end and failure
    /// </summary>
    /// <param name="requests"> Queue with the requests from the orchestrator </param>
    /// <param name="publish"> Queue with messages that need to be published to RabbitMQ </param>
    /// <param name="max"> maximum delay time of the payment in seconds </param>
    /// <param name="log"> logger to log to </param>
    /// <param name="min"> minimum delay time of the payment in seconds</param>
    public PaymentHandler(Channel<Message> requests, Channel<Message> publish, int min, int max, Logger log)
    {
        _logger = log;
        Requests = requests;
        Publish = publish;
        _minDelay = min;
        _maxDelay = max;

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

        var tests = new[] {0, 0, 0, 0};
        for (var i = 0; i < 1000; i++)
        {
            tests[rnd.Next(0, 3)]++;
        }
        _logger.Debug($"0:{tests[0]}, 1:{tests[1]}, 2:{tests[2]}, 3:{tests[3]}");
        
        var delayRoll = rnd.Next(0, 3);
        _logger.Debug($"Randomizing delay: {delayRoll}");
        if (delayRoll == 0) await Task.Delay(rnd.Next(_minDelay + 1, _maxDelay * 1000), Token);

        var resultRoll = rnd.Next(0, 3);
        _logger.Debug($"Randomizing result: {resultRoll}");
        SagaState result = resultRoll switch
        {
            0 => SagaState.PaymentFailed,
            1 => SagaState.PaymentAccept,
            2 => SagaState.PaymentAccept,
            _ => SagaState.PaymentAccept
            
        };
        _logger.Debug($"State result: {result}");

        message.MessageType = MessageType.PaymentReply;
        message.MessageId += 1;
        message.State = result;
        message.Body = new PaymentReply();
        _logger.Debug($"Payment finished message: {message}");

        await Publish.Writer.WriteAsync(message, Token);

        _concurencySemaphore.Release();
    }
}