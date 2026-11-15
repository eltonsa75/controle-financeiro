using Financeiro.Api.Models.DTO;
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
    [Authorize] // Proteção estrita por token JWT do Firebase
    public class EstoqueController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EstoqueController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/estoque
        [HttpGet]
        public async Task<IActionResult> GetEstoque()
        {
            var usuarioId = ObtemUsuarioId();

            var estoque = await _context.Estoques
                .Include(e => e.Categoria) // Traz os dados da categoria associada
                .AsNoTracking()
                .Where(e => e.UsuarioId == usuarioId && e.Ativo)
                .OrderBy(e => e.Nome)
                .ToListAsync();

            return Ok(estoque);
        }

        // POST: api/estoque
        [HttpPost]
        public async Task<IActionResult> PostEstoque([FromBody] EstoqueDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var estoque = new Estoque
            {
                Nome = dto.Nome,
                QuantidadeAtual = dto.QuantidadeAtual,
                QuantidadeMinima = dto.QuantidadeMinima,
                UnidadeMedida = dto.UnidadeMedida,
                CategoriaId = dto.CategoriaId,
                UsuarioId = ObtemUsuarioId()
            };

            _context.Estoques.Add(estoque);
            await _context.SaveChangesAsync();

            // Retorna 201 Created com a localização do novo item
            return CreatedAtAction(nameof(GetEstoque), new { id = estoque.Id }, estoque);
        }

        // PUT: api/estoque/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutEstoque(int id, [FromBody] Estoque model)
        {
            var usuarioId = ObtemUsuarioId();

            if (id <= 0 || id != model.Id)
            {
                return BadRequest(new
                {
                    mensagem = "O código informado não corresponde ao produto."
                });
            }

            // Busca o produto existente e mantém o rastreamento pelo EF
            var item = await _context.Estoques
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.UsuarioId == usuarioId &&
                    e.Ativo);

            if (item == null)
            {
                return NotFound(new
                {
                    mensagem = "Produto não encontrado ou inativo."
                });
            }

            ModelState.Remove(nameof(model.UsuarioId));
            ModelState.Remove(nameof(model.Categoria));

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Atualiza somente os campos editáveis
            item.Nome = model.Nome;
            item.QuantidadeMinima = model.QuantidadeMinima;
            item.UnidadeMedida = model.UnidadeMedida;
            item.CategoriaId = model.CategoriaId;

            // Preserva os dados controlados pelo sistema:
            // item.Id
            // item.UsuarioId
            // item.Ativo
            // item.QuantidadeAtual

            await _context.SaveChangesAsync();

            return Ok(item);
        }

        // 🎯 DIFERENCIAL: Atualização rápida de quantidade (+1 ou -1) direto na linha da tabela
        // PATCH: api/estoque/5/ajustar?valor=-1
        [HttpPatch("{id}/ajustar")]
        public async Task<IActionResult> AjustarQuantidade(int id, [FromQuery] decimal valor)
        {
            var usuarioId = ObtemUsuarioId();
            var item = await _context.Estoques.FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == usuarioId);

            if (item == null) return NotFound("Item não encontrado.");

            item.QuantidadeAtual += valor;

            if (item.QuantidadeAtual < 0) item.QuantidadeAtual = 0; // Impede estoque negativo na despensa

            await _context.SaveChangesAsync();
            return Ok(item);
        }

        // DELETE: api/estoque/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteEstoque(int id)
        {
            var usuarioId = ObtemUsuarioId();

            if (id <= 0)
            {
                return BadRequest(new
                {
                    mensagem = "Código do produto inválido."
                });
            }

            // Localiza o produto do usuário autenticado
            var item = await _context.Estoques
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.UsuarioId == usuarioId);

            if (item == null)
            {
                return NotFound(new
                {
                    mensagem = "Produto não encontrado no seu estoque."
                });
            }

            if (!item.Ativo)
            {
                return Conflict(new
                {
                    mensagem = "Este produto já foi removido do estoque."
                });
            }

            // Exclusão lógica: mantém o produto no banco
            item.Ativo = false;

            // Registra a exclusão no histórico
            var movimentacao = new EstoqueMovimentacao
            {
                EstoqueId = item.Id,
                UsuarioId = usuarioId,

                Tipo = "Exclusao",
                Origem = "Manual",

                Quantidade = 0,
                QuantidadeAnterior = item.QuantidadeAtual,
                QuantidadePosterior = item.QuantidadeAtual,

                DataMovimentacao = DateTime.UtcNow,

                Observacao = $"Produto '{item.Nome}' removido da lista de estoque."
            };

            _context.EstoqueMovimentacoes.Add(movimentacao);

            try
            {
                // Salva a exclusão lógica e o histórico juntos
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new
                {
                    mensagem = "Não foi possível registrar a exclusão do produto."
                });
            }
        }

        // Método auxiliar privado para extrair a identidade do Firebase de forma limpa
        private string ObtemUsuarioId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("user_id")?.Value
                ?? string.Empty;
        }
    }
}