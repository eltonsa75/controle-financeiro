using Financeiro.Api.Models;
using Financeiro.Api.Models.DTO;
using Financeiro.Api.Repositories;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using FinanceiroApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceiroApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LancamentosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FinanceiroRepository _repository;
        private readonly ImportacaoNotaService _importacaoService;
        private readonly IDistributedCache _cache;

        public LancamentosController(
       AppDbContext context,
       FinanceiroRepository repository,
       ImportacaoNotaService importacaoService,
       IDistributedCache cache)
        {
            _context = context;
            _repository = repository;
            _importacaoService = importacaoService;
            _cache = cache;
        }

        // Helper para centralizar a obtenção do UserId dinâmico com fallback seguro para testes
        private string ObterUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("user_id")?.Value
                       ?? User.FindFirst("id")?.Value;

            return !string.IsNullOrWhiteSpace(userId) ? userId : throw new UnauthorizedAccessException("UID do Firebase não encontrado no token autenticado.");
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
                    .Include(l => l.CategoriaRef)
                    .Include(l => l.Itens)
                    .Where(x => x.UsuarioId == userId)
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
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == ObterUserId());

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
                    .Where(i => (i.Lancamento.UsuarioId == userId) && i.Lancamento.DataEmissao.Month == mesFiltro && i.Lancamento.DataEmissao.Year == anoFiltro)
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
                .Where(i => i.LancamentoId == id && i.Lancamento.UsuarioId == ObterUserId())
                .AsNoTracking()
                .ToListAsync();

            return Ok(itens);
        }



        // POST: api/Lancamentos
        [HttpPost]
        public async Task<ActionResult<Lancamento>> PostLancamento(
            [FromBody] Lancamento lancamento)
        {
            try
            {
                string userId = ObterUserId();
                lancamento.UsuarioId = userId;

                // 1. Valida a categoria principal
                bool categoriaExiste = await _context.Categorias
                    .AnyAsync(c =>
                        c.Id == lancamento.CategoriaId &&
                        c.UsuarioId == userId);

                if (!categoriaExiste)
                {
                    return BadRequest(new
                    {
                        mensagem = "A categoria informada não existe.",
                        categoriaId = lancamento.CategoriaId
                    });
                }

                // 2. Prepara e valida os itens do lançamento
                if (lancamento.Itens != null)
                {
                    foreach (var item in lancamento.Itens)
                    {
                        item.UsuarioId = userId;

                        if (item.CategoriaId <= 0)
                            item.CategoriaId = lancamento.CategoriaId;

                        bool categoriaItemExiste = await _context.Categorias
                            .AnyAsync(c =>
                                c.Id == item.CategoriaId &&
                                c.UsuarioId == userId);

                        if (!categoriaItemExiste)
                        {
                            return BadRequest(new
                            {
                                mensagem = $"Categoria inválida para o item {item.Descricao}.",
                                categoriaId = item.CategoriaId
                            });
                        }
                    }
                }

                var strategy = _context.Database.CreateExecutionStrategy();



                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync();

                    try
                    {
                        // 3. Salva o lançamento e seus itens
                        _context.Lancamentos.Add(lancamento);

                        await _context.SaveChangesAsync();

                        Console.WriteLine(
                            $"[LANÇAMENTO] Lançamento #{lancamento.Id} salvo.");

                        // 4. Atualiza o estoque somente para despesas com itens
                        bool atualizarEstoque =
                            string.Equals(
                                lancamento.Tipo?.Trim(),
                                "Despesa",
                                StringComparison.OrdinalIgnoreCase)
                            && lancamento.Itens?.Any() == true;

                        if (atualizarEstoque)
                        {
                            await AtualizarEstoqueCompraAsync(lancamento);

                            // 5. Persiste produtos, reativações e movimentações
                            await _context.SaveChangesAsync();

                            Console.WriteLine(
                                $"[ESTOQUE] Atualizado pelo lançamento #{lancamento.Id}.");
                        }

                        // 6. Confirma todas as operações
                        await transaction.CommitAsync();

                        Console.WriteLine(
                            $"[SUCESSO] Lançamento #{lancamento.Id} concluído.");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();

                        Console.WriteLine(
                            $"[ERRO TRANSAÇÃO] {ex}");

                        throw;
                    }
                });

                // CI-030 - Invalida o cache após a transação confirmada.
                await InvalidarCacheFinanceiroAsync(
                    userId,
                    lancamento.DataEmissao
                );

                Console.WriteLine(
                    $"[REDIS] Cache invalidado após lançamento #{lancamento.Id}."
                );

                return CreatedAtAction(
                    nameof(GetLancamento),
                    new { id = lancamento.Id },
                    lancamento
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[ERRO] POST Lancamentos: {ex}");

                return StatusCode(500, new
                {
                    mensagem = "Erro ao salvar lançamento e atualizar estoque."
                });
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
                    .Where(l => (l.UsuarioId == userId) && l.DataEmissao.Month == mesFiltro && l.DataEmissao.Year == anoFiltro)
                    .ToListAsync();

                decimal totalReceitas = lancamentosMes
                    .Where(l => string.Equals(l.Tipo?.Trim(), "receita", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                decimal totalDespesas = lancamentosMes
                    .Where(l => string.Equals(l.Tipo?.Trim(), "despesa", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Abs(l.Valor));

                var lancamentosAnteriores = await _context.Lancamentos
                    .Where(l => (l.UsuarioId == userId) && (l.DataEmissao.Year < anoFiltro || (l.DataEmissao.Year == anoFiltro && l.DataEmissao.Month < mesFiltro)))
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

        // GET: api/Lancamentos/gastos-categoria?mes=5&ano=2026
        [HttpGet("gastos-categoria")]
        public async Task<IActionResult> ObterGastosPorCategoria(
            [FromQuery] int? mes,
            [FromQuery] int? ano)
        {
            try
            {
                string userId = ObterUserId();

                int mesFiltro = mes ?? DateTime.Now.Month;
                int anoFiltro = ano ?? DateTime.Now.Year;

                if (mesFiltro < 1 || mesFiltro > 12)
                {
                    return BadRequest(new
                    {
                        mensagem = "Mês inválido."
                    });
                }

                if (anoFiltro < 2000 || anoFiltro > 2100)
                {
                    return BadRequest(new
                    {
                        mensagem = "Ano inválido."
                    });
                }

                Console.WriteLine(
                    $"[GASTOS CATEGORIA] Usuario: {userId} | Mês: {mesFiltro} | Ano: {anoFiltro}"
                );

                var resultado = await _repository.ObterGastosPorCategoria(
                    userId,
                    mesFiltro,
                    anoFiltro
                );

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[ERRO GASTOS-CATEGORIA] {ex}"
                );

                return StatusCode(500, new
                {
                    erro = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("gastos-anuais")]
        public async Task<IActionResult> GetGastosAnuais()
        {
            try
            {
                string userId = ObterUserId();

                Console.WriteLine($"[DEBUG BI ANUAL] Buscando gastos acumulados do ano para o usuário: {userId}");

                var dados = await _repository.ObterGastosAnuaisComMetas(userId);

                return Ok(dados);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO] GET Gastos Anuais: {ex.Message}");
                return StatusCode(500, $"Erro interno ao calcular BI Anual: {ex.Message}");
            }
        }


        private async Task AtualizarEstoqueCompraAsync(Lancamento lancamento)
        {
            if (lancamento.Itens == null || !lancamento.Itens.Any())
                return;

            if (lancamento.Id <= 0)
                throw new InvalidOperationException(
                    "O lançamento precisa estar salvo antes de atualizar o estoque.");

            string usuarioId = lancamento.UsuarioId;

            var itensAgrupados = lancamento.Itens
                .Where(i =>
                    !string.IsNullOrWhiteSpace(i.Descricao) &&
                    i.Quantidade > 0)
                .GroupBy(i => new
                {
                    Nome = i.Descricao.Trim().ToUpperInvariant(),
                    Unidade = string.IsNullOrWhiteSpace(i.Unidade)
                        ? "un"
                        : i.Unidade.Trim().ToLowerInvariant()
                });

            foreach (var grupo in itensAgrupados)
            {
                string nome = grupo.First().Descricao.Trim();
                string unidade = grupo.Key.Unidade;

                decimal quantidade = grupo.Sum(i =>
                    Convert.ToDecimal(i.Quantidade));

                int categoriaId = grupo.First().CategoriaId > 0
                    ? grupo.First().CategoriaId
                    : lancamento.CategoriaId;

                // Procura o produto independentemente de estar ativo
                var produto = await _context.Estoques
                    .FirstOrDefaultAsync(e =>
                        e.UsuarioId == usuarioId &&
                        e.Nome.ToUpper() == grupo.Key.Nome &&
                        e.UnidadeMedida.ToLower() == unidade);

                decimal quantidadeAnterior;
                bool produtoReativado = false;

                if (produto != null)
                {
                    if (!produto.Ativo)
                    {
                        // Produto anteriormente removido do estoque.
                        // Reativa sem recuperar o saldo antigo.
                        produto.Ativo = true;
                        produtoReativado = true;

                        quantidadeAnterior = 0;
                        produto.QuantidadeAtual = quantidade;
                    }
                    else
                    {
                        // Produto ativo: soma a quantidade comprada
                        quantidadeAnterior = produto.QuantidadeAtual;
                        produto.QuantidadeAtual += quantidade;
                    }
                }
                else
                {
                    // Produto ainda não cadastrado
                    quantidadeAnterior = 0;

                    produto = new Estoque
                    {
                        Nome = nome,
                        QuantidadeAtual = quantidade,
                        QuantidadeMinima = 1,
                        UnidadeMedida = unidade,
                        CategoriaId = categoriaId,
                        UsuarioId = usuarioId,
                        Ativo = true
                    };

                    _context.Estoques.Add(produto);
                }

                // Registra a entrada no histórico
                var movimentacao = new EstoqueMovimentacao
                {
                    Estoque = produto,
                    LancamentoId = lancamento.Id,
                    UsuarioId = usuarioId,

                    Tipo = "Entrada",
                    Origem = "Compra",

                    Quantidade = quantidade,
                    QuantidadeAnterior = quantidadeAnterior,
                    QuantidadePosterior = produto.QuantidadeAtual,

                    DataMovimentacao = DateTime.UtcNow,

                    Observacao = produtoReativado
                        ? $"Produto reativado automaticamente pela compra #{lancamento.Id}"
                        : $"Entrada automática pela compra #{lancamento.Id}"
                };

                _context.EstoqueMovimentacoes.Add(movimentacao);
            }
        }


        [HttpPost("importar-pdf")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportarPdf([FromForm] ImportarPdfRequest request)
        {
            var pdf = request.Pdf;

            if (pdf == null || pdf.Length == 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Arquivo PDF não recebido."
                });
            }

            try
            {
                string userId = ObterUserId();

                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Usuário não identificado."
                    });
                }

                using var stream = pdf.OpenReadStream();

                var lancamento =
                    await _importacaoService.ProcessarPdfNota(
                        stream,
                        userId
                    );

                if (lancamento == null)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Não foi possível extrair dados."
                    });
                }

                // CI-030 - Invalida o Redis após a importação do PDF.
                await InvalidarCacheFinanceiroAsync(
                    userId,
                    lancamento.DataEmissao
                );

                Console.WriteLine(
                    $"[REDIS] Cache invalidado após importação do PDF #{lancamento.Id}."
                );

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "PDF importado e salvo com sucesso!",
                    id = lancamento.Id,
                    descricao = lancamento.Descricao,
                    valor = lancamento.Valor,
                    totalItens = lancamento.Itens?.Count ?? 0
                });
            }

            // ============================================================
            // NOTA DUPLICADA
            // ============================================================
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(
                    $"[NOTA DUPLICADA - CONTROLLER] {ex.Message}"
                );

                return Conflict(new
                {
                    sucesso = false,
                    duplicada = true,
                    mensagem = ex.Message
                });
            }

            // ============================================================
            // QUALQUER OUTRO ERRO
            // ============================================================
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[ERRO CONTROLLER] Falha na importação do PDF: {ex}"
                );

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        sucesso = false,
                        duplicada = false,
                        mensagem = "Não conseguimos processar ou salvar este PDF."
                    }
                );
            }
        }

        [HttpPost("lancamento-manual")]
        public async Task<IActionResult> SalvarLancamentoManual([FromBody] Lancamento novoLancamento)
        {
            try
            {
                string userId = ObterUserId();
                novoLancamento.UsuarioId = userId;

                var fusoBrasilia = ObterFusoBrasilia();
                DateTime agoraBrasilia = TimeZoneInfo.ConvertTime(DateTime.UtcNow, fusoBrasilia);

                var dataParaGravar = novoLancamento.Data == DateTime.MinValue
                                   ? agoraBrasilia.Date
                                   : novoLancamento.Data.Date;

                novoLancamento.Data = dataParaGravar;
                novoLancamento.DataEmissao = dataParaGravar;
                novoLancamento.DataImportacao = agoraBrasilia;
                novoLancamento.Tipo = novoLancamento.Tipo?.Trim().ToLowerInvariant() ?? "";

                if (novoLancamento.Tipo != "receita" &&
                    novoLancamento.Tipo != "despesa")
                {
                    return BadRequest(new
                    {
                        mensagem = "O tipo do lançamento deve ser receita ou despesa."
                    });
                }

                if (novoLancamento.CategoriaId <= 0)
                {
                    novoLancamento.CategoriaId =
                        await ObterIdDaCategoriaPorNome(
                            ClassClassificacaoManual(novoLancamento.Descricao));
                }

                bool categoriaValida = await _context.Categorias
                    .AnyAsync(c =>
                        c.Id == novoLancamento.CategoriaId &&
                        c.UsuarioId == userId);

                if (!categoriaValida)
                {
                    return BadRequest(new
                    {
                        mensagem = "Categoria inválida ou não pertence ao usuário."
                    });
                }

                // Não aceitar entidades relacionadas enviadas pelo cliente.
                novoLancamento.CategoriaRef = null;
                novoLancamento.Itens = new List<ItemLancamento>();
                novoLancamento.Id = 0;

                _context.Lancamentos.Add(novoLancamento);

                await _context.SaveChangesAsync();

                // CI-030 - Invalida o cache após salvar no MySQL.
                await InvalidarCacheFinanceiroAsync(
                    userId,
                    novoLancamento.DataEmissao
                );

                Console.WriteLine(
                    $"[REDIS] Cache invalidado após lançamento manual #{novoLancamento.Id}."
                );

                return Ok(novoLancamento);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"[LANÇAMENTO MANUAL] {ex}");
                return BadRequest(new
                {
                    mensagem = "Não foi encontrada uma categoria válida. Cadastre uma categoria antes de salvar o lançamento."
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERRO LANÇAMENTO MANUAL] {ex}");
                return StatusCode(500, new
                {
                    mensagem = "Não foi possível salvar o lançamento. Tente novamente."
                });
            }
        }

        private TimeZoneInfo ObterFusoBrasilia()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "Brasilia Time",
                        TimeSpan.FromHours(-3),
                        "Brasília Time",
                        "Brasília Time"
                    );
                }
            }
        }

        // DELETE: api/Lancamentos/5

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteLancamento(int id)
        {
            var userId = ObterUserId();

            var lancamento = await _context.Lancamentos
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == userId);

            if (lancamento == null)
                return NotFound();

            // Guarda a data antes de excluir o lançamento.
            var dataLancamento = lancamento.DataEmissao;

            _context.Lancamentos.Remove(lancamento);

            // Confirma a exclusão no MySQL.
            await _context.SaveChangesAsync();

            // CI-030 - Invalida o Redis após a exclusão.
            await InvalidarCacheFinanceiroAsync(
                userId,
                dataLancamento
            );

            Console.WriteLine(
                $"[REDIS] Cache invalidado após exclusão do lançamento #{id}."
            );

            return NoContent();
        }


        private async Task<int> ObterIdDaCategoriaPorNome(string nome)
        {
            string userId = ObterUserId();

            var categoria = await _context.Categorias
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.UsuarioId == userId && c.Nome == nome);

            if (categoria == null)
            {
                categoria = await _context.Categorias
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.UsuarioId == userId && c.Nome == "Outros");
            }

            if (categoria == null)
            {
                throw new InvalidOperationException(
                    "Nenhuma categoria válida encontrada para o usuário.");
            }

            return categoria.Id;
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
                { "Automóvel", new[] { "GASOLINA", "ALCOOL", "DIESEL", "SHELL", "IPIRANGA", "ESTACIONAMENTO", "UBER", "MECANICO", "AUTO", "CONSERTO" } },
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

        private async Task InvalidarCacheFinanceiroAsync(
    string usuarioId,
    DateTime dataLancamento)
        {
            var chaveCategoria =
                $"gastos-categoria:{usuarioId}:{dataLancamento:yyyy:MM}";

            var chaveAnual =
                $"gastos-anuais:{usuarioId}:{DateTime.Now.Year}";

            try
            {
                await _cache.RemoveAsync(chaveCategoria);

                // O cache anual existente utiliza o ano corrente.
                if (dataLancamento.Year == DateTime.Now.Year)
                {
                    await _cache.RemoveAsync(chaveAnual);
                }

                Console.WriteLine(
                    $"[REDIS] Cache financeiro invalidado para {dataLancamento:MM/yyyy}.");
            }
            catch (Exception ex)
            {
                // Uma falha no Redis não deve desfazer uma operação
                // financeira já confirmada no MySQL.
                Console.WriteLine(
                    $"[REDIS] Falha ao invalidar cache: {ex.Message}");
            }
        }
    }
}