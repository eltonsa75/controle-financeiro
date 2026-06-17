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
                .Where(e => e.UsuarioId == usuarioId)
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
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEstoque(int id, [FromBody] Estoque model)
        {
            var usuarioId = ObtemUsuarioId();
            if (id != model.Id) return BadRequest("O ID informado não corresponde ao objeto enviado.");

            var itemExiste = await _context.Estoques.AnyAsync(e => e.Id == id && e.UsuarioId == usuarioId);
            if (!itemExiste) return NotFound("Item não encontrado no seu estoque.");

            model.UsuarioId = usuarioId;
            ModelState.Remove(nameof(model.UsuarioId));
            ModelState.Remove(nameof(model.Categoria));

            if (!ModelState.IsValid) return BadRequest(ModelState);

            _context.Entry(model).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(model);
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEstoque(int id)
        {
            var usuarioId = ObtemUsuarioId();
            var item = await _context.Estoques.FirstOrDefaultAsync(e => e.Id == id && e.UsuarioId == usuarioId);

            if (item == null) return NotFound("Item não encontrado ou sem permissão para exclusão.");

            _context.Estoques.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
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