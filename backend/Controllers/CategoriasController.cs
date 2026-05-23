using Financeiro.Api.Models;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceiroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/categorias/status?mes=5&ano=2026
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(int mes, int ano)
        {
            try
            {
                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .ToListAsync();

                var resultado = categorias.Select(c => {
                    var gastos = _context.Lancamentos
                        .Where(l => l.CategoriaId == c.Id && l.Data.Month == mes && l.Data.Year == ano)
                        .Sum(l => (decimal?)l.Valor) ?? 0;

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
                // Isso vai te ajudar a ver o erro no console do VS
                Console.WriteLine($"Erro no Status: {ex.Message}");
                return StatusCode(500, ex.Message);
            }
        }

        // MANTIDO APENAS UM MÉTODO POST PARA EVITAR O ERRO DE DUPLICIDADE
        [HttpPost]
        public async Task<IActionResult> PostCategoria([FromBody] Categoria model)
        {
            // Se o ID vier nulo ou 0 no JSON, garantimos que o EF trate como novo registro
            if (model.Id == null) model.Id = 0;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Categorias.Add(model);
            await _context.SaveChangesAsync();

            return Ok(model);
        }

        // DELETE: api/categorias/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategoria(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null) return NotFound();

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}