using System.Globalization;
using System.Security.Claims;
using Financeiro.Api.Models.DTO;
using Financeiro.Api.Repositories;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinanceiroController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FinanceiroRepository _repository;

        public FinanceiroController(AppDbContext context, FinanceiroRepository repository)
        {
            _repository = repository;
            _context = context;
        }

        // Helper para centralizar a obtenção do UserId dinâmico com fallback seguro para testes
        private string ObterUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("id")?.Value;

            return string.IsNullOrEmpty(userId) ? "admin-001" : userId;
        }

        // --- ENDPOINTS PARA OS CARDS DE KPI (CORRIGIDO) ---

        [HttpGet("resumo-mensal")]
        public async Task<IActionResult> GetResumo([FromQuery] int? mes, [FromQuery] int? ano)
        {
            try
            {
                string userId = ObterUserId();
                int mesFiltro = mes ?? DateTime.Now.Month;
                int anoFiltro = ano ?? DateTime.Now.Year;

                // AJUSTADO: l.DataEmissao passa a governar a busca para os Cards
                var lancamentosMes = await _context.Lancamentos
                    .Where(l => l.UsuarioId == userId && l.DataEmissao.Month == mesFiltro && l.DataEmissao.Year == anoFiltro)
                    .ToListAsync();

                decimal totalReceitas = lancamentosMes
                    .Where(l => string.Equals(l.Tipo?.Trim(), "receita", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                decimal totalDespesas = lancamentosMes
                    .Where(l => string.Equals(l.Tipo?.Trim(), "despesa", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                // AJUSTADO: Filtro retroativo estruturado com base na DataEmissao
                var lancamentosAnteriores = await _context.Lancamentos
                    .Where(l => l.UsuarioId == userId && (l.DataEmissao.Year < anoFiltro || (l.DataEmissao.Year == anoFiltro && l.DataEmissao.Month < mesFiltro)))
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

        // --- ENDPOINTS PARA O GRÁFICO DE EVOLUÇÃO MENSAL (CORRIGIDO) ---

        [HttpGet("evolucao-mensal")]
        public async Task<IActionResult> GetEvolucaoMensal()
        {
            try
            {
                string userId = ObterUserId();

                // Busca todos os lançamentos do usuário
                var todosLancamentos = await _context.Lancamentos
                    .Where(l => l.UsuarioId == userId)
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

                // AJUSTADO: Escala de tempo gerada estritamente pela DataEmissao
                var dataMinima = todosLancamentos.Min(l => l.DataEmissao);
                var dataMaxima = todosLancamentos.Max(l => l.DataEmissao);

                // Força a data máxima a cobrir pelo menos o mês atual para evitar cortes de timeline
                if (dataMaxima < DateTime.Now) dataMaxima = DateTime.Now;

                var mesesTotais = (dataMaxima.Year - dataMinima.Year) * 12 + dataMaxima.Month - dataMinima.Month + 1;
                var mesesParaMostrar = Math.Max(12, Math.Min(mesesTotais, 24));

                var dataInicio = dataMaxima.AddMonths(-mesesParaMostrar + 1);
                dataInicio = new DateTime(dataInicio.Year, dataInicio.Month, 1);

                var todosMeses = new List<DateTime>();
                var dataCorrente = dataInicio;
                while (dataCorrente <= dataMaxima)
                {
                    todosMeses.Add(dataCorrente);
                    dataCorrente = dataCorrente.AddMonths(1);
                }

                // AJUSTADO: Agrupamento em memória usando chaves de DataEmissao
                var lancamentosPorMes = todosLancamentos
                    .GroupBy(l => new { l.DataEmissao.Year, l.DataEmissao.Month })
                    .ToDictionary(
                        g => new DateTime(g.Key.Year, g.Key.Month, 1),
                        g => g.ToList()
                    );

                var resultado = todosMeses.Select(mes => new
                {
                    ano = mes.Year,
                    mesNumero = mes.Month,
                    label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(mes.Month).ToUpper().Replace(".", ""),
                    anoLabel = mes.Year.ToString(),
                    totalReceitas = lancamentosPorMes.ContainsKey(mes)
                        ? lancamentosPorMes[mes]
                            .Where(l => string.Equals(l.Tipo?.Trim(), "receita", StringComparison.OrdinalIgnoreCase))
                            .Sum(l => Math.Abs(l.Valor))
                        : 0,
                    totalDespesas = lancamentosPorMes.ContainsKey(mes)
                        ? lancamentosPorMes[mes]
                            .Where(l => string.Equals(l.Tipo?.Trim(), "despesa", StringComparison.OrdinalIgnoreCase))
                            .Sum(l => Math.Abs(l.Valor))
                        : 0
                })
                .OrderBy(r => r.ano)
                .ThenBy(r => r.mesNumero)
                .ToList();

                decimal saldoCorrente = 0;
                var resultadoComSaldo = resultado.Select(r => new
                {
                    r.ano,
                    r.mesNumero,
                    r.label,
                    r.anoLabel,
                    r.totalReceitas,
                    r.totalDespesas,
                    saldo = r.totalReceitas - r.totalDespesas,
                    saldoAcumulado = (saldoCorrente += (r.totalReceitas - r.totalDespesas))
                }).ToList();

                var labels = resultadoComSaldo.Select(r => $"{r.label}/{r.anoLabel}").ToList();
                var receitas = resultadoComSaldo.Select(r => r.totalReceitas).ToList();
                var despesas = resultadoComSaldo.Select(r => r.totalDespesas).ToList();
                var saldos = resultadoComSaldo.Select(r => r.saldoAcumulado).ToList();

                return Ok(new
                {
                    meses = labels,
                    receitas = receitas,
                    despesas = despesas,
                    saldos = saldos,
                    detalhes = resultadoComSaldo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao carregar evolução: {ex.Message}");
            }
        }

        // --- ENDPOINTS PARA OS GRÁFICOS ANALÍTICOS ---
        // 🎯 Localize o endpoint na linha 206. Ele deve ser parecido com isso:
        [HttpGet("gastos-categoria")]
        public async Task<IActionResult> GetGastosCategoria()
        {
            try
            {
                // 1. Captura o ID do usuário logado de forma dinâmica (ou usa o seu ID fixo de testes se não estiver usando autenticação JWT ainda)
                var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("id")?.Value
                             ?? "PiGS3DH27AaUiLdNhnw9AiedMpm2"; // 🎯 Fallback de segurança com o seu ID do banco

                // 2. Passa o usuarioId que o repositório agora exige como argumento!
                var dados = await _repository.ObterGastosPorCategoria(usuarioId);

                return Ok(dados);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        [HttpGet("itens-mais-caros")]
        public async Task<ActionResult<IEnumerable<ItemCaroDto>>> GetItensMaisCaros([FromQuery] int? mes, [FromQuery] int? ano)
        {
            try
            {
                string userId = ObterUserId();
                int mesFiltro = mes ?? DateTime.Now.Month;
                int anoFiltro = ano ?? DateTime.Now.Year;

                // AJUSTADO: Top 10 itens amarrados dinamicamente à DataEmissao
                var itens = await _context.ItensLancamento
                    .Include(i => i.Lancamento)
                    .Where(i => i.Lancamento.UsuarioId == userId && i.Lancamento.DataEmissao.Month == mesFiltro && i.Lancamento.DataEmissao.Year == anoFiltro)
                    .GroupBy(i => new { i.Descricao, LocalNome = i.Lancamento.Descricao })
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
                return StatusCode(500, $"Erro ao carregar itens mais caros: {ex.Message}");
            }
        }
    }
}