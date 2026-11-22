
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace FinanceiroApi.Services;

public class RabbitMqPublisher
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IConfiguration configuration,
        ILogger<RabbitMqPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublicarAsync<T>(
        T mensagem,
        CancellationToken cancellationToken = default)
    {
        var host = _configuration["RabbitMQ:HostName"]
            ?? "rabbitmq";

        var port = _configuration.GetValue<int>(
            "RabbitMQ:Port", 5672);

        var queue = _configuration["RabbitMQ:QueueName"]
            ?? "comprasintelig.processamento";

        var user = _configuration["RabbitMQ:UserName"];
        var password = _configuration["RabbitMQ:Password"];

        if (string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Credenciais RabbitMQ não configuradas.");
        }

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = password,
            AutomaticRecoveryEnabled = true
        };

        await using var connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var json = JsonSerializer.Serialize(mensagem);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: queue,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "[RABBITMQ] Publicação enviada. Fila: {Fila} | MessageId: {MessageId}",
            queue,
            properties.MessageId);
    }
}
