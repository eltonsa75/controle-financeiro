using Financeiro.Api.Models;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // 🎯 OBRIGATÓRIO: Para o [Authorize] funcionar
using System.Security.Claims;

namespace FinanceiroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 🎯 AQUI: Protege a classe inteira. Qualquer chamada à API sem Token será rejeitada com 401 Unauthorized
    public class CategoriasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/categorias
        [HttpGet]
        public async Task<IActionResult> GetCategorias()
        {
            try
            {
                var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("user_id")?.Value;

                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .Where(c => c.UsuarioId == usuarioId)
                    .OrderBy(c => c.Nome)
                    .ToListAsync();

                return Ok(categorias);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao listar categorias: {ex.Message}");
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(int mes, int ano, string tipo = "mensal")
        {
            try
            {
                var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("user_id")?.Value;

                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .Where(c => c.UsuarioId == usuarioId)
                    .ToListAsync();

                var resultado = categorias.Select(c => {
                    // 🎯 AQUI ESTÁ A CORREÇÃO: Adicionamos o filtro de data na query
                    var query = _context.Lancamentos
                        .Where(l => l.CategoriaId == c.Id
                                 && l.Data.Month == mes
                                 && l.Data.Year == ano); // FILTRO DE DATA APLICADO

                    var gastos = query.Sum(l => (decimal?)Math.Abs(l.Valor)) ?? 0;

                    return new
                    {
                        c.Id,
                        c.Nome,
                        c.MetaMensal,
                        c.CorHex,
                        c.PalavrasChave,
                        GastoAtual = gastos,
                        Percentual = c.MetaMensal > 0 ? (gastos / c.MetaMensal) * 100 : 0
                    };
                }).ToList();

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // POST: api/categorias
        [HttpPost]
        public async Task<IActionResult> PostCategoria([FromBody] Categoria model)
        {
            try
            {
                var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("user_id")?.Value;

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Unauthorized("Usuário não identificado no token de autenticação.");
                }

                model.UsuarioId = usuarioId;

                if (model.Id == null) model.Id = 0;

                ModelState.Remove(nameof(model.UsuarioId));

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _context.Categorias.Add(model);
                await _context.SaveChangesAsync();

                return Ok(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar categoria: {ex.Message}");
                return StatusCode(500, $"Erro interno ao salvar: {ex.Message}");
            }
        }

        // PUT: api/categorias/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCategoria(int id, [FromBody] Categoria model)
        {
            try
            {
                var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("user_id")?.Value;

                if (id != model.Id)
                {
                    return BadRequest("O ID da URL não corresponde ao ID do objeto enviado.");
                }

                var categoriaExiste = await _context.Categorias
                    .AnyAsync(c => c.Id == id && c.UsuarioId == usuarioId);

                if (!categoriaExiste)
                {
                    return NotFound("Categoria não encontrada ou sem permissão de edição.");
                }

                model.UsuarioId = usuarioId;
                ModelState.Remove(nameof(model.UsuarioId));

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _context.Entry(model).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar categoria: {ex.Message}");
                return StatusCode(500, $"Erro interno ao atualizar: {ex.Message}");
            }
        }

        // DELETE: api/categorias/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategoria(int id)
        {
            var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("user_id")?.Value;

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);

            if (categoria == null) return NotFound();

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}