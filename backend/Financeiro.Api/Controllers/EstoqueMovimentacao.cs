
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinanceiroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EstoqueMovimentacoesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EstoqueMovimentacoesController(AppDbContext context)
        {
            _context = context;
        }

        private string ObterUsuarioId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("user_id")
                ?? string.Empty;
        }

        // GET: api/EstoqueMovimentacoes
        [HttpGet]
        public async Task<IActionResult> GetMovimentacoes()
        {
            string usuarioId = ObterUsuarioId();

            if (string.IsNullOrWhiteSpace(usuarioId))
                return Unauthorized();

            var movimentacoes = await _context.EstoqueMovimentacoes
                .AsNoTracking()
                .Where(m => m.UsuarioId == usuarioId)
                .OrderByDescending(m => m.DataMovimentacao)
                .ThenByDescending(m => m.Id)
                .Select(m => new
                {
                    m.Id,
                    m.EstoqueId,
                    Produto = m.Estoque != null
                        ? m.Estoque.Nome
                        : null,
                    m.LancamentoId,
                    m.Tipo,
                    m.Origem,
                    m.Quantidade,
                    m.QuantidadeAnterior,
                    m.QuantidadePosterior,
                    m.DataMovimentacao,
                    m.Observacao
                })
                .ToListAsync();

            return Ok(movimentacoes);
        }

        // GET: api/EstoqueMovimentacoes/produto/15
        [HttpGet("produto/{estoqueId:int}")]
        public async Task<IActionResult> GetPorProduto(int estoqueId)
        {
            string usuarioId = ObterUsuarioId();

            if (string.IsNullOrWhiteSpace(usuarioId))
                return Unauthorized();

            bool produtoExiste = await _context.Estoques
                .AnyAsync(e =>
                    e.Id == estoqueId &&
                    e.UsuarioId == usuarioId);

            if (!produtoExiste)
                return NotFound(new
                {
                    mensagem = "Produto não encontrado."
                });

            var movimentacoes = await _context.EstoqueMovimentacoes
                .AsNoTracking()
                .Where(m =>
                    m.EstoqueId == estoqueId &&
                    m.UsuarioId == usuarioId)
                .OrderByDescending(m => m.DataMovimentacao)
                .ThenByDescending(m => m.Id)
                .Select(m => new
                {
                    m.Id,
                    m.EstoqueId,
                    Produto = m.Estoque != null
                        ? m.Estoque.Nome
                        : null,
                    m.LancamentoId,
                    m.Tipo,
                    m.Origem,
                    m.Quantidade,
                    m.QuantidadeAnterior,
                    m.QuantidadePosterior,
                    m.DataMovimentacao,
                    m.Observacao
                })
                .ToListAsync();

            return Ok(movimentacoes);
        }
    }
}
