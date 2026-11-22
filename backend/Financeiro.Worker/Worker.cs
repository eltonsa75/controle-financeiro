
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Financeiro.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                await using var channel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var mensagem = Encoding.UTF8.GetString(
                            ea.Body.ToArray());

                        _logger.LogInformation(
                            "[RABBITMQ] Mensagem recebida: {Mensagem}",
                            mensagem);

                        // Futuramente, processaremos aqui
                        // as tarefas do Compras Inteligentes.

                        await channel.BasicAckAsync(
                            deliveryTag: ea.DeliveryTag,
                            multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "[RABBITMQ] Erro ao processar mensagem.");

                        await channel.BasicNackAsync(
                            deliveryTag: ea.DeliveryTag,
                            multiple: false,
                            requeue: false);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: queue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "[RABBITMQ] Worker conectado. Fila: {Fila}",
                    queue);

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[RABBITMQ] Falha na conexão. Nova tentativa em 5 segundos.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }
}
