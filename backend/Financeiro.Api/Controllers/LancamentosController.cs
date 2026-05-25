using Financeiro.Api.Models;
using Financeiro.Api.Models.DTO;
using Financeiro.Api.Repositories;
using FinanceiroApi.Data;
using FinanceiroApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace FinanceiroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LancamentosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FinanceiroRepository _repository;
        private readonly ImportacaoNotaService _importacaoService;

        public LancamentosController(AppDbContext context, FinanceiroRepository repository, ImportacaoNotaService importacaoService)
        {
            _context = context;
            _repository = repository;
            _importacaoService = importacaoService;
        }

        // Helper para centralizar a obtenção do UserId dinâmico com fallback seguro para testes
        private string ObterUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("id")?.Value;

            return string.IsNullOrEmpty(userId) ? "admin-001" : userId;
        }

        // GET: api/Lancamentos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Lancamento>>> GetLancamentos()
        {
            try
            {
                string userId = ObterUserId();
                Console.WriteLine($"[DEBUG] Buscando lançamentos para o usuário: {userId}");

                var lancamentos = await _context.Lancamentos
                    .Include(l => l.Categoria)
                    .Include(l => l.Itens)
                    .Where(x => x.UsuarioId == userId || x.UsuarioId == "admin-001")
                    .OrderByDescending(x => x.DataEmissao)
                    .ToListAsync();

                return Ok(lancamentos);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO] GET Lancamentos: {ex.Message}");
                return StatusCode(500, "Erro interno ao buscar lançamentos.");
            }
        }

        // GET: api/Lancamentos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Lancamento>> GetLancamento(int id)
        {
            var lancamento = await _context.Lancamentos
                .Include(l => l.Itens)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lancamento == null) return NotFound();

            return lancamento;
        }

        // GET: api/Lancamentos/itens-mais-caros
        [HttpGet("itens-mais-caros")]
        public async Task<ActionResult<IEnumerable<ItemCaroDto>>> GetItensMaisCaros([FromQuery] int? mes, [FromQuery] int? ano)
        {
            try
            {
                string userId = ObterUserId();
                int mesFiltro = mes ?? DateTime.Now.Month;
                int anoFiltro = ano ?? DateTime.Now.Year;

                var itens = await _context.ItensLancamento
                    .Include(i => i.Lancamento)
                    .Where(i => (i.Lancamento.UsuarioId == userId || i.Lancamento.UsuarioId == "admin-001") && i.Lancamento.DataEmissao.Month == mesFiltro && i.Lancamento.DataEmissao.Year == anoFiltro)
                    .GroupBy(i => new {
                        i.Descricao,
                        LocalNome = i.Lancamento != null ? i.Lancamento.Descricao : "Local Indefinido"
                    })
                    .Select(g => new ItemCaroDto
                    {
                        Descricao = g.Key.Descricao,
                        Local = g.Key.LocalNome,
                        ValorTotal = g.Sum(x => x.Preco * (decimal)x.Quantidade)
                    })
                    .OrderByDescending(x => x.ValorTotal)
                    .Take(10)
                    .ToListAsync();

                return Ok(itens);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro: {ex.Message}");
            }
        }

        // GET: api/Lancamentos/5/itens
        [HttpGet("{id:int}/itens")]
        public async Task<IActionResult> GetItens(int id)
        {
            var itens = await _context.ItensLancamento
                .Where(i => i.LancamentoId == id)
                .AsNoTracking()
                .ToListAsync();

            return Ok(itens);
        }

        // POST: api/Lancamentos
        [HttpPost]
        public async Task<ActionResult<Lancamento>> PostLancamento(Lancamento lancamento)
        {
            try
            {
                string userId = ObterUserId();
                lancamento.UsuarioId = userId;

                if (lancamento.Itens != null)
                {
                    foreach (var item in lancamento.Itens)
                    {
                        item.UsuarioId = userId;
                    }
                }

                _context.Lancamentos.Add(lancamento);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetLancamento", new { id = lancamento.Id }, lancamento);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar lançamento: {ex}");
                return StatusCode(500, "Erro interno ao salvar lançamento.");
            }
        }

        // GET: api/Lancamentos/resumo-mensal
        [HttpGet("resumo-mensal")]
        public async Task<IActionResult> GetResumoMensal([FromQuery] int? mes, [FromQuery] int? ano)
        {
            try
            {
                string userId = ObterUserId();
                int mesFiltro = mes ?? DateTime.Now.Month;
                int anoFiltro = ano ?? DateTime.Now.Year;

                var lancamentosMes = await _context.Lancamentos
                    .Where(l => (l.UsuarioId == userId || l.UsuarioId == "admin-001") && l.DataEmissao.Month == mesFiltro && l.DataEmissao.Year == anoFiltro)
                    .ToListAsync();

                decimal totalReceitas = lancamentosMes
                    .Where(l => string.Equals(l.Tipo?.Trim(), "receita", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                decimal totalDespesas = lancamentosMes
                    .Where(l => string.Equals(l.Tipo?.Trim(), "despesa", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                var lancamentosAnteriores = await _context.Lancamentos
                    .Where(l => (l.UsuarioId == userId || l.UsuarioId == "admin-001") && (l.DataEmissao.Year < anoFiltro || (l.DataEmissao.Year == anoFiltro && l.DataEmissao.Month < mesFiltro)))
                    .ToListAsync();

                decimal receitasAnteriores = lancamentosAnteriores
                    .Where(l => string.Equals(l.Tipo?.Trim(), "receita", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                decimal despesasAnteriores = lancamentosAnteriores
                    .Where(l => string.Equals(l.Tipo?.Trim(), "despesa", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                decimal saldoAcumulado = receitasAnteriores - despesasAnteriores;

                return Ok(new
                {
                    receitas = totalReceitas,
                    despesas = totalDespesas,
                    saldo = totalReceitas - totalDespesas,
                    saldoAcumulado = saldoAcumulado,
                    mesAtual = new DateTime(anoFiltro, mesFiltro, 1).ToString("MMMM/yyyy"),
                    receitasTotal = receitasAnteriores + totalReceitas,
                    despesasTotal = despesasAnteriores + totalDespesas,
                    saldoTotal = (receitasAnteriores + totalReceitas) - (despesasAnteriores + totalDespesas)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao carregar resumo mensal: {ex.Message}");
            }
        }

        // GET: api/Lancamentos/evolucao-mensal
        [HttpGet("evolucao-mensal")]
        public async Task<IActionResult> GetEvolucaoMensal()
        {
            try
            {
                string userId = ObterUserId();

                var todosLancamentos = await _context.Lancamentos
                    .Where(l => l.UsuarioId == userId || l.UsuarioId == "admin-001")
                    .ToListAsync();

                if (!todosLancamentos.Any())
                {
                    return Ok(new
                    {
                        meses = new List<string>(),
                        receitas = new List<decimal>(),
                        despesas = new List<decimal>(),
                        saldos = new List<decimal>()
                    });
                }

                var dataMinima = todosLancamentos.Min(l => l.DataEmissao);
                var dataMaxima = DateTime.Now;

                var resultado = new List<object>();
                var dataCorrente = new DateTime(dataMinima.Year, dataMinima.Month, 1);

                decimal saldoCorrente = 0;

                while (dataCorrente <= dataMaxima)
                {
                    var lancamentosMes = todosLancamentos
                        .Where(l => l.DataEmissao.Year == dataCorrente.Year && l.DataEmissao.Month == dataCorrente.Month)
                        .ToList();

                    decimal totalReceitas = lancamentosMes
                        .Where(l => string.Equals(l.Tipo?.Trim(), "receita", StringComparison.OrdinalIgnoreCase))
                        .Sum(l => Math.Abs(l.Valor));

                    decimal totalDespesas = lancamentosMes
                        .Where(l => string.Equals(l.Tipo?.Trim(), "despesa", StringComparison.OrdinalIgnoreCase))
                        .Sum(l => Math.Abs(l.Valor));

                    string nomeMes = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(dataCorrente.Month).ToUpper().Replace(".", "");
                    saldoCorrente += (totalReceitas - totalDespesas);

                    resultado.Add(new
                    {
                        label = $"{nomeMes}/{dataCorrente.Year}",
                        totalReceitas = totalReceitas,
                        totalDespesas = totalDespesas,
                        saldoAcumulado = saldoCorrente
                    });

                    dataCorrente = dataCorrente.AddMonths(1);
                }

                return Ok(new
                {
                    meses = resultado.Select(r => ((dynamic)r).label).ToList(),
                    receitas = resultado.Select(r => ((dynamic)r).totalReceitas).Cast<decimal>().ToList(),
                    despesas = resultado.Select(r => ((dynamic)r).totalDespesas).Cast<decimal>().ToList(),
                    saldos = resultado.Select(r => ((dynamic)r).saldoAcumulado).Cast<decimal>().ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar gráfico histórico: {ex.Message}");
            }
        }

        // GET: api/Lancamentos/gastos-categoria
        [HttpGet("gastos-categoria")]
        public async Task<IActionResult> GetGastosCategoria()
        {
            var dados = await _repository.ObterGastosPorCategoria();
            return Ok(dados);
        }

        // POST: api/Lancamentos/importar-pdf
        [HttpPost("importar-pdf")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportarPdf(IFormFile pdf)
        {
            if (pdf == null || pdf.Length == 0)
                return BadRequest("Arquivo PDF não recebido.");

            try
            {
                string userId = ObterUserId();

                using (var stream = pdf.OpenReadStream())
                {
                    var resultado = await _importacaoService.ProcessarPdfNota(stream, userId);

                    if (resultado == null)
                        return BadRequest("Não foi possível extrair dados deste PDF.");

                    return Ok(resultado);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no processamento: {ex.Message}");
                return StatusCode(500, $"Erro no processamento: {ex.Message}");
            }
        }

        // POST: api/Lancamentos/lancamento-manual
        [HttpPost("lancamento-manual")]
        public async Task<IActionResult> SalvarLancamentoManual([FromBody] Lancamento novoLancamento)
        {
            try
            {
                string userId = ObterUserId();
                novoLancamento.UsuarioId = userId;

                novoLancamento.Data = novoLancamento.DataEmissao;
                novoLancamento.Tipo = novoLancamento.Tipo.Trim().ToLower();

                if (novoLancamento.CategoriaId <= 0)
                {
                    novoLancamento.CategoriaId = await ObterIdDaCategoriaPorNome(ClassClassificacaoManual(novoLancamento.Descricao));
                }

                _context.Lancamentos.Add(novoLancamento);
                await _context.SaveChangesAsync();

                return Ok(novoLancamento);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao salvar lançamento manual: {ex.Message}");
            }
        }

        // DELETE: api/Lancamentos/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteLancamento(int id)
        {
            var lancamento = await _context.Lancamentos.FindAsync(id);
            if (lancamento == null) return NotFound();

            _context.Lancamentos.Remove(lancamento);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<int> ObterIdDaCategoriaPorNome(string nome)
        {
            var categoria = await _context.Categorias
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Nome == nome);

            if (categoria == null)
            {
                categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Nome == "Outros");
            }

            return categoria?.Id ?? 1;
        }

        private string ClassClassificacaoManual(string nomeProduto)
        {
            if (string.IsNullOrEmpty(nomeProduto)) return "Outros";
            string nome = nomeProduto.ToUpper();

            var mapeamento = new Dictionary<string, string[]>
            {
                { "Laticínios e Frios", new[] { "LEITE", "NINHO", "MUSSARELA", "QUEIJO", "DANONE", "IOG", "RICOTA" } },
                { "Mercearia", new[] { "FEIJAO", "ARROZ", "OLEO", "SOYA", "AZEITE", "ACUCAR", "CAFE", "MACARRÃO", "ATUM" } },
                { "Limpeza", new[] { "OMO", "VANISH", "DETERGENTE", "AMACIANTE", "DESINFETANTE", "PAPEL HIGIENICO" } },
                { "Higiene e Saúde", new[] { "SABONETE", "SHAMPOO", "DENTIFRICIO", "DROGASIL", "DROGARAIA", "REMEDIO", "FARMACIA" } },
                { "Hortifruti", new[] { "BATATA", "ALHO", "CEBOLA", "TOMATE", "BANANA", "MACA" } },
                { "Snacks e Doces", new[] { "BISCOITO", "CHOCOLATE", "BOMBOM", "SALGADINHO" } },
                { "Padaria", new[] { "PAO", "PULLMAN", "TORRADA" } },
                { "Bebidas", new[] { "REFRIGERANTE", "COCA", "AGUA", "SUCO", "CERVEJA" } },
                { "Pet Shop", new[] { "RACAO", "PEDIGREE", "CACHORRO", "GATO" } },
                { "Acougue", new[] { "CARNE", "FRANGO", "LINGUICA", "BIFE" } },
                { "Transporte", new[] { "GASOLINA", "ALCOOL", "DIESEL", "SHELL", "IPIRANGA", "ESTACIONAMENTO", "UBER" } },
                { "Academia", new[] { "SMART FIT", "WHEY", "CREATINA", "GYMPASS" } },
                { "Educacao", new[] { "FACULDADE", "FAAP", "CURSO", "UDEMY" } },
                { "Restaurante", new[] { "IFOOD", "BURGER", "MC DONALDS", "PIZZA", "ALMOCO" } }
            };

            foreach (var categoria in mapeamento)
            {
                if (categoria.Value.Any(keyword => nome.Contains(keyword)))
                    return categoria.Key;
            }

            return "Outros";
        }
    }
}