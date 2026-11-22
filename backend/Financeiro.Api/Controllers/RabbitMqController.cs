
using FinanceiroApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financeiro.Api.Controllers;

[ApiController]
[Route("api/rabbitmq")]
[Authorize]
public class RabbitMqController : ControllerBase
{
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<RabbitMqController> _logger;

    public RabbitMqController(
        RabbitMqPublisher publisher,
        ILogger<RabbitMqController> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    [HttpPost("teste")]
    public async Task<IActionResult> Testar(
        CancellationToken cancellationToken)
    {
        var mensagem = new
        {
            Tipo = "TESTE_RABBITMQ",
            Origem = "Financeiro.Api",
            Descricao = "Teste de integração API RabbitMQ Worker",
            DataHora = DateTimeOffset.UtcNow
        };

        try
        {
            await _publisher.PublicarAsync(
                mensagem,
                cancellationToken);

            return Accepted(new
            {
                Sucesso = true,
                Mensagem = "Mensagem enviada ao RabbitMQ."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[RABBITMQ] Falha ao publicar mensagem.");

            return StatusCode(503, new
            {
                Sucesso = false,
                Mensagem = "RabbitMQ indisponível."
            });
        }
    }
}
