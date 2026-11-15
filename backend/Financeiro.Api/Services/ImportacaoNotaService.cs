
using Financeiro.Api.Models;
using Financeiro.Api.Models.DTO;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceiroApi.Services
{
    public class ImportacaoNotaService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly CultureInfo _culturaBr = new CultureInfo("pt-BR");
        private readonly IConfiguration _configuration;

        public ImportacaoNotaService(
            IDbContextFactory<AppDbContext> contextFactory,
            IConfiguration configuration)
        {
            _contextFactory = contextFactory;
            _configuration = configuration;
        }

        // ============================================================
        // MÉTODO PRINCIPAL
        // ============================================================

        public async Task<Lancamento> ProcessarPdfNota(
            Stream arquivoStream,
            string userId)
        {
            Console.WriteLine($"USER ID RECEBIDO: {userId}");
            Console.WriteLine("\n==== [.NET] INTEGRANDO COM MICROSERVIÇO PYTHON ====");
            Console.WriteLine("[IMPORTACAO-NOTA] versão reconstruída 2026-09-28-fix1");

            var lancamento = CriarLancamentoBase(userId);

            string textoBrutoDoPdf = string.Empty;
            PythonRespostaDto dadosMapeados = null;
            bool dataCarregadaPeloPython = false;

            // ========================================================
            // 1. ENVIA O PDF PARA O PYTHON
            // ========================================================

            using (var client = new HttpClient())
            using (var content = new MultipartFormDataContent())
            {
                arquivoStream.Position = 0;

                var streamContent = new StreamContent(arquivoStream);

                content.Add(
                    streamContent,
                    "file",
                    "nota_upload.pdf"
                );

                try
                {
                    var pythonUrl = _configuration["PythonServiceUrl"];

                    if (string.IsNullOrWhiteSpace(pythonUrl))
                    {
                        throw new Exception(
                            "PythonServiceUrl não foi configurada."
                        );
                    }

                    var respostaPython = await client.PostAsync(
                        $"{pythonUrl}/extrair-nota",
                        content
                    );

                    if (!respostaPython.IsSuccessStatusCode)
                    {
                        var erroPython =
                            await respostaPython.Content.ReadAsStringAsync();

                        throw new Exception(
                            $"O microsserviço Python retornou erro: " +
                            $"{respostaPython.StatusCode}. " +
                            $"Detalhes: {erroPython}"
                        );
                    }

                    // ====================================================
                    // 2. LÊ O JSON RETORNADO PELO PYTHON
                    // ====================================================

                    var jsonPython =
                        await respostaPython.Content.ReadAsStringAsync();

                    Console.WriteLine(
                        "\n========== JSON RETORNADO PELO PYTHON =========="
                    );

                    Console.WriteLine(jsonPython);

                    Console.WriteLine(
                        "================================================\n"
                    );

                    // ====================================================
                    // Desserialização
                    // ====================================================

                    var jsonOptions =
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                    dadosMapeados =
                        System.Text.Json.JsonSerializer.Deserialize<PythonRespostaDto>(
                            jsonPython,
                            jsonOptions
                        );

                    if (dadosMapeados == null)
                    {
                        throw new Exception(
                            "O Python retornou um JSON vazio ou inválido."
                        );
                    }

                    // ====================================================
                    // LOG DOS DADOS RECEBIDOS
                    // ====================================================

                    Console.WriteLine(
                        $"[PYTHON] Estabelecimento: " +
                        $"{dadosMapeados.Estabelecimento}"
                    );

                    Console.WriteLine(
                        $"[PYTHON] CategoriaPrincipal: " +
                        $"{dadosMapeados.CategoriaPrincipal}"
                    );

                    Console.WriteLine(
                        $"[PYTHON] DataEmissao: " +
                        $"{dadosMapeados.DataEmissao}"
                    );

                    Console.WriteLine(
                        $"[PYTHON] Quantidade de itens: " +
                        $"{dadosMapeados.Itens?.Count ?? 0}"
                    );

                    // ====================================================
                    // 3. DADOS DO LANÇAMENTO
                    // ====================================================

                    lancamento.Descricao =
                        !string.IsNullOrWhiteSpace(
                            dadosMapeados.Estabelecimento)
                            ? dadosMapeados.Estabelecimento.Trim()
                            : "Lançamento importado";

                    textoBrutoDoPdf =
                        dadosMapeados.TextoBruto ?? string.Empty;

                    // ====================================================
                    // 4. DATA
                    // ====================================================

                    if (!string.IsNullOrWhiteSpace(
                            dadosMapeados.DataEmissao) &&
                        DateTime.TryParse(
                            dadosMapeados.DataEmissao,
                            out DateTime dataPython))
                    {
                        lancamento.DataEmissao = dataPython.Date;
                        lancamento.Data = dataPython.Date;

                        dataCarregadaPeloPython = true;
                    }

                    // ====================================================
                    // 5. ITENS
                    // ====================================================

                    lancamento.Itens =
                        (dadosMapeados.Itens ?? new List<ItemPythonDto>())
                        .Where(i => i != null)
                        .Select(i => new ItemLancamento
                        {
                            Descricao =
                                i.Descricao?.Trim() ?? string.Empty,

                            Quantidade = i.Quantidade,

                            Preco = i.Preco,

                            Comprado = true
                        })
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[.NET ERRO CRÍTICO] " +
                        $"Falha ao comunicar com o Python: {ex.Message}"
                    );

                    throw new Exception(
                        "Falha ao processar PDF pelo serviço Python.",
                        ex
                    );
                }
            }

            // ============================================================
            // 6. FALLBACK DA DATA
            // ============================================================

            if (!dataCarregadaPeloPython)
            {
                ExtrairDataDoPdf(
                    textoBrutoDoPdf,
                    lancamento
                );
            }

            // Executa somente as operações de banco em transação.
            // O serviço Python é chamado uma única vez, fora da transação.
            await using var strategyContext = await _contextFactory.CreateDbContextAsync();
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                // Cada tentativa utiliza um contexto e entidades novos.
                await using var context = await _contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();

                var tentativa = CriarLancamentoBase(userId);
                tentativa.Descricao = lancamento.Descricao;
                tentativa.DataEmissao = lancamento.DataEmissao;
                tentativa.Data = lancamento.Data;
                tentativa.Itens = lancamento.Itens.Select(item => new ItemLancamento
                {
                    Descricao = item.Descricao,
                    Quantidade = item.Quantidade,
                    Preco = item.Preco,
                    Comprado = item.Comprado
                }).ToList();

                var resultado = await PersistirNotaAsync(
                    context, tentativa, userId, dadosMapeados, textoBrutoDoPdf);

                await transaction.CommitAsync();
                return resultado;
            });
        }

        private async Task<Lancamento> PersistirNotaAsync(
            AppDbContext context,
            Lancamento lancamento,
            string userId,
            PythonRespostaDto dadosMapeados,
            string textoBrutoDoPdf)
        {
            // ============================================================
            // 7. GARANTE CATEGORIA GERAL
            // ============================================================

            var categoriaGeral =
                await context.Categorias
                    .FirstOrDefaultAsync(c =>
                        c.UsuarioId == userId &&
                        c.Nome.ToLower() == "geral");

            if (categoriaGeral == null)
            {
                categoriaGeral = new Categoria
                {
                    Nome = "Geral",
                    UsuarioId = userId,
                    MetaMensal = 0,
                    CorHex = "#607D8B"
                };

                context.Categorias.Add(categoriaGeral);

                await context.SaveChangesAsync();
            }

            // 8. CLASSIFICAÇÃO PRINCIPAL DO LANÇAMENTO
            //
            // A categoria do LANÇAMENTO representa o tipo de estabelecimento.
            // A categoria do Python continua sendo usada individualmente nos itens.
            // ============================================================

            string categoriaEstabelecimento =
                ClassificarEstabelecimentoFallback(
                    lancamento.Descricao
                );

            string nomeCategoriaPrincipal;

            if (!string.Equals(
                    categoriaEstabelecimento,
                    "Geral",
                    StringComparison.OrdinalIgnoreCase))
            {
                nomeCategoriaPrincipal = categoriaEstabelecimento;
            }
            else
            {
                nomeCategoriaPrincipal =
                    NormalizarCategoriaPrincipal(
                        dadosMapeados?.CategoriaPrincipal
                    );
            }

            if (string.IsNullOrWhiteSpace(nomeCategoriaPrincipal))
            {
                nomeCategoriaPrincipal = "Geral";
            }

            Console.WriteLine(
                $"[CATEGORIA LANÇAMENTO] " +
                $"Estabelecimento: {lancamento.Descricao} | " +
                $"Python: {dadosMapeados?.CategoriaPrincipal} | " +
                $"Final: {nomeCategoriaPrincipal}"
            );

            // 10. BUSCA CATEGORIA NO BANCO TRATANDO ACENTOS
            var todasCategoriasUsuario = await context.Categorias
                .Where(c => c.UsuarioId == userId)
                .ToListAsync();

            string nomeBuscaNormalizado = RemoverAcentos(nomeCategoriaPrincipal).ToLowerInvariant();

            var categoriaNotaEncontrada = todasCategoriasUsuario
                .FirstOrDefault(c => RemoverAcentos(c.Nome).ToLowerInvariant() == nomeBuscaNormalizado);

            // 11. CRIA SE NÃO EXISTIR
            if (categoriaNotaEncontrada == null)
            {
                categoriaNotaEncontrada = new Categoria
                {
                    Nome = nomeCategoriaPrincipal,
                    UsuarioId = userId,
                    MetaMensal = 0,
                    CorHex = "#" + new Random().Next(0x1000000).ToString("X6")
                };

                context.Categorias.Add(categoriaNotaEncontrada);
                await context.SaveChangesAsync();

                todasCategoriasUsuario.Add(categoriaNotaEncontrada);
            }

            // 12. ATRIBUI CORRETAMENTE AO LANÇAMENTO
            lancamento.CategoriaId = categoriaNotaEncontrada.Id;
            lancamento.CategoriaRef = categoriaNotaEncontrada;

            // ============================================================
            // 13. CARREGA CATEGORIAS DO USUÁRIO
            // ============================================================

            var todasCategorias =
                await context.Categorias
                    .Where(c => c.UsuarioId == userId)
                    .ToListAsync();

            // ============================================================
            // 14. CLASSIFICA OS ITENS
            //
            // Aqui NÃO usamos a categoria principal.
            //
            // Cada item recebe:
            //
            // Python Itens[].Categoria
            //
            // ============================================================

            foreach (var item in lancamento.Itens)
            {
                if (item == null)
                    continue;

                // ========================================================
                // Procura o item correspondente no retorno do Python
                // ========================================================

                var itemPython =
                    dadosMapeados?.Itens?
                        .FirstOrDefault(i =>
                            i != null &&
                            !string.IsNullOrWhiteSpace(i.Descricao) &&
                            !string.IsNullOrWhiteSpace(item.Descricao) &&
                            NormalizarTexto(i.Descricao) ==
                            NormalizarTexto(item.Descricao));

                string nomeCategoriaItem =
                    itemPython?.Categoria?.Trim();

                // ========================================================
                // Se o Python não classificou o item,
                // utiliza Geral.
                // ========================================================

                if (string.IsNullOrWhiteSpace(
                        nomeCategoriaItem))
                {
                    nomeCategoriaItem = "Geral";
                }

                nomeCategoriaItem =
                    NormalizarNomeCategoria(
                        nomeCategoriaItem
                    );

                Console.WriteLine(
                    $"[ITEM] {item.Descricao} -> " +
                    $"Categoria: {nomeCategoriaItem}"
                );

                // ========================================================
                // PROCURA CATEGORIA DO ITEM
                // ========================================================

                var categoriaItemEncontrada =
                    todasCategorias.FirstOrDefault(c =>
                        c.Nome.Equals(
                            nomeCategoriaItem,
                            StringComparison.OrdinalIgnoreCase
                        ));

                // ========================================================
                // CRIA CATEGORIA DO ITEM SE NÃO EXISTIR
                // ========================================================

                if (categoriaItemEncontrada == null)
                {
                    categoriaItemEncontrada = new Categoria
                    {
                        Nome = nomeCategoriaItem,
                        UsuarioId = userId,
                        MetaMensal = 0,
                        CorHex = "#" +
                            new Random()
                                .Next(0x1000000)
                                .ToString("X6")
                    };

                    context.Categorias.Add(
                        categoriaItemEncontrada
                    );

                    await context.SaveChangesAsync();

                    todasCategorias.Add(
                        categoriaItemEncontrada
                    );
                }

                // ========================================================
                // ATRIBUI CATEGORIA AO ITEM
                // ========================================================

                item.UsuarioId = userId;

                item.CategoriaId =
                    categoriaItemEncontrada.Id;

                item.Categoria =
                    categoriaItemEncontrada.Nome;

                item.CategoriaRef =
                    categoriaItemEncontrada;
            }

            // ============================================================
            // 15. TIPO
            // ============================================================

            lancamento.Tipo = "despesa";

            // ============================================================
            // 16. SOMA DOS ITENS
            // ============================================================

            decimal somaItens =
                lancamento.Itens.Any()
                    ? lancamento.Itens.Sum(
                        it =>
                            (decimal)it.Quantidade *
                            it.Preco)
                    : 0;

            // ============================================================
            // 17. VALOR PAGO
            // ============================================================

            decimal valorPago =
                ExtrairValorPago(
                    textoBrutoDoPdf,
                    somaItens
                );

            lancamento.Valor =
                -Math.Abs(
                    valorPago > 0
                        ? valorPago
                        : somaItens
                );

            // Valores financeiros extraídos da nota fiscal.
            lancamento.Subtotal =
                dadosMapeados?.Subtotal ??
                (somaItens > 0 ? somaItens : (decimal?)null);

            lancamento.Desconto =
                dadosMapeados?.Desconto;

            // ============================================================
            // 18. VERIFICA SE A NOTA JÁ FOI IMPORTADA
            // ============================================================

            string descricaoDuplicidade =
                lancamento.Descricao?.Trim() ?? string.Empty;

            DateTime dataDuplicidade =
                lancamento.DataEmissao.Date;

            decimal valorDuplicidade =
                lancamento.Valor;

            bool notaJaImportada =
                await context.Lancamentos
                    .AsNoTracking()
                    .AnyAsync(l =>
                        l.UsuarioId == userId &&
                        l.Descricao == descricaoDuplicidade &&
                        l.DataEmissao.Date == dataDuplicidade &&
                        l.Valor == valorDuplicidade
                    );

            if (notaJaImportada)
            {
                Console.WriteLine(
                    $"[NOTA DUPLICADA] " +
                    $"Estabelecimento: {descricaoDuplicidade} | " +
                    $"Data: {dataDuplicidade:dd/MM/yyyy} | " +
                    $"Valor: {valorDuplicidade:C2}"
                );

                throw new InvalidOperationException(
                    $"Esta nota já foi importada anteriormente. " +
                    $"Estabelecimento: {descricaoDuplicidade}, " +
                    $"Data: {dataDuplicidade:dd/MM/yyyy}, " +
                    $"Valor: {Math.Abs(valorDuplicidade):C2}."
                );
            }

            // ============================================================
            // 19. DATA DE IMPORTAÇÃO
            // ============================================================

            TimeZoneInfo fusoBrasilia;

            try
            {
                fusoBrasilia =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "E. South America Standard Time"
                    );
            }
            catch (TimeZoneNotFoundException)
            {
                fusoBrasilia =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "America/Sao_Paulo"
                    );
            }

            lancamento.DataImportacao =
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    fusoBrasilia
                );

            // ============================================================
            // 20. SALVA LANÇAMENTO
            // ============================================================

            context.Lancamentos.Add(lancamento);

            await context.SaveChangesAsync();

            // ============================================================
            // 21. ATUALIZA ESTOQUE
            // ============================================================

            await AtualizarEstoquePorNota(
                context,
                lancamento,
                userId,
                todasCategorias
            );

            // ============================================================
            // 22. LOG FINAL
            // ============================================================

            Console.WriteLine(
                "\n========== RESULTADO FINAL =========="
            );

            Console.WriteLine(
                $"Estabelecimento: {lancamento.Descricao}"
            );

            Console.WriteLine(
                $"Categoria lançamento: " +
                $"{categoriaNotaEncontrada.Nome}"
            );

            Console.WriteLine(
                $"Quantidade de itens: " +
                $"{lancamento.Itens.Count}"
            );

            foreach (var item in lancamento.Itens)
            {
                Console.WriteLine(
                    $"  - {item.Descricao} -> " +
                    $"{item.Categoria}"
                );
            }

            Console.WriteLine(
                $"Valor: {lancamento.Valor}"
            );

            Console.WriteLine(
                "==== [.NET] FLUXO DE COMPILAÇÃO E SALVAMENTO CONCLUÍDO ====\n"
            );

            return lancamento;
        }

        // ============================================================
        // NORMALIZA CATEGORIA PRINCIPAL
        // ============================================================

        private string NormalizarCategoriaPrincipal(
            string categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return "Geral";

            string texto =
                RemoverAcentos(categoria)
                    .Trim()
                    .ToUpperInvariant();

            if (texto.Contains("SUPERMERC"))
                return "Supermercado";

            if (texto.Contains("FARMAC"))
                return "Farmácia";

            if (texto.Contains("ESTACION"))
                return "Estacionamento";

            if (texto.Contains("POSTO"))
                return "Combustível";

            if (texto.Contains("RESTAUR"))
                return "Restaurante";

            if (texto.Contains("LANCH"))
                return "Alimentação";

            if (texto.Contains("TRANSPORTE"))
                return "Transporte";

            if (texto.Contains("SAUDE"))
                return "Saúde";

            if (texto.Contains("EDUCAC"))
                return "Educação";

            if (texto.Contains("VESTUARIO") ||
                texto.Contains("ROUPA"))
                return "Vestuário";

            if (texto.Contains("ELETRON"))
                return "Eletrônicos";

            if (texto.Contains("LAZER"))
                return "Lazer";

            if (texto == "GERAL")
                return "Geral";

            return categoria.Trim();
        }

        // ============================================================
        // FALLBACK PELO ESTABELECIMENTO
        // ============================================================

        private string ClassificarEstabelecimentoFallback(
            string estabelecimento)
        {
            if (string.IsNullOrWhiteSpace(estabelecimento))
                return "Geral";

            string texto =
                RemoverAcentos(estabelecimento)
                    .ToUpperInvariant();

            // Supermercados
            if (texto.Contains("SONDA") ||
                texto.Contains("WMS") ||
                texto.Contains("WALMART") ||
                texto.Contains("ASSAI") ||
                texto.Contains("SENDAS") ||
                texto.Contains("ATACADAO") ||
                texto.Contains("CARREFOUR") ||
                texto.Contains("PAO DE ACUCAR") ||
                texto.Contains("SUPERMERC") ||
                texto.Contains("MERCADO"))
            {
                return "Supermercado";
            }

            // Farmácias
            if (texto.Contains("DROGASIL") ||
                texto.Contains("DROGA RAIA") ||
                texto.Contains("RAIADROGASIL") ||
                texto.Contains("DROGARIA") ||
                texto.Contains("FARMAC"))
            {
                return "Farmácia";
            }

            // Estacionamentos
            if (texto.Contains("ESTAPAR") ||
                texto.Contains("ESTACIONAMENTO") ||
                texto.Contains("PARKING") ||
                texto.Contains("PARK"))
            {
                return "Estacionamento";
            }

            // Combustível
            if (texto.Contains("POSTO") ||
                texto.Contains("IPIRANGA") ||
                texto.Contains("SHELL") ||
                texto.Contains("PETROBRAS") ||
                texto.Contains("BR MANIA"))
            {
                return "Combustível";
            }

            return "Geral";
        }

        // ============================================================
        // NORMALIZA CATEGORIA DOS ITENS
        // ============================================================

        private string NormalizarNomeCategoria(
            string categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return "Geral";

            string texto =
                RemoverAcentos(categoria)
                    .Trim()
                    .ToUpperInvariant();

            if (texto == "GERAL")
                return "Geral";

            if (texto.Contains("LATIC"))
                return "Laticínios e Frios";

            if (texto.Contains("FRIOS"))
                return "Laticínios e Frios";

            if (texto.Contains("MERCEARIA"))
                return "Mercearia";

            if (texto.Contains("LIMPEZA"))
                return "Limpeza";

            if (texto.Contains("PADARIA"))
                return "Padaria";

            if (texto.Contains("HORTIFRUT"))
                return "Hortifruti";

            if (texto.Contains("BEBIDA"))
                return "Bebidas";

            if (texto.Contains("PET"))
                return "Pet Shop";

            if (texto.Contains("HIGIENE"))
                return "Higiene e Saúde";

            if (texto.Contains("SAUDE"))
                return "Higiene e Saúde";

            if (texto.Contains("SNACK") ||
                texto.Contains("DOCE"))
                return "Snacks e Doces";

            if (texto.Contains("ACOUQUE") ||
                texto.Contains("ACOUgue"))
                return "Açougue";

            if (texto.Contains("FARMAC"))
                return "Farmácia";

            return categoria.Trim();
        }

        // ============================================================
        // NORMALIZA TEXTO PARA COMPARAR ITEM
        // ============================================================

        private string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            return RemoverAcentos(texto)
                .Trim()
                .ToUpperInvariant()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace(".", "")
                .Replace(",", "");
        }

        // ============================================================
        // REMOVE ACENTOS
        // ============================================================

        private string RemoverAcentos(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            var normalized =
                texto.Normalize(
                    System.Text.NormalizationForm.FormD
                );

            var chars =
                normalized
                    .Where(c =>
                        System.Globalization.CharUnicodeInfo
                            .GetUnicodeCategory(c) !=
                        System.Globalization.UnicodeCategory.NonSpacingMark)
                    .ToArray();

            return new string(chars)
                .Normalize(
                    System.Text.NormalizationForm.FormC
                );
        }

        // ============================================================
        // INTELIGÊNCIA LEGADA
        // ============================================================

        private int DeterminarCategoriaInteligente(
            Lancamento lancamento,
            List<Categoria> categorias,
            string textoBruto)
        {
            string textoParaMapear =
                (lancamento.Descricao + " " + textoBruto)
                .ToLower();

            foreach (var cat in categorias)
            {
                if (!string.IsNullOrEmpty(cat.PalavrasChave))
                {
                    var palavras =
                        cat.PalavrasChave.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries
                        );

                    foreach (var p in palavras)
                    {
                        string palavraChaveLimpa =
                            p.Trim().ToLower();

                        if (!string.IsNullOrEmpty(
                                palavraChaveLimpa) &&
                            textoParaMapear.Contains(
                                palavraChaveLimpa))
                        {
                            return cat.Id;
                        }
                    }
                }
            }

            string categoriaNotaSub =
                ClassificarNotaPorItens(
                    lancamento.Itens.ToList()
                );

            string nomeAlvo =
                categoriaNotaSub
                    .ToLower()
                    .Trim() switch
                {
                    "mercearia" => "mercearia",

                    "laticínios e frios" or
                    "laticinios e frios"
                        => "laticínios e frios",

                    "snacks e doces"
                        => "snacks e doces",

                    "padaria"
                        => "padaria",

                    "acougue" or
                    "açougue"
                        => "açougue",

                    "bebidas"
                        => "bebidas",

                    "limpeza"
                        => "limpeza",

                    "higiene e saúde" or
                    "higiene e saude" or
                    "higiene"
                        => "higiene",

                    "farmácia" or
                    "farmacia"
                        => "farmácia",

                    "hortifruti"
                        => "hortifruti",

                    "pet shop" or
                    "petshop"
                        => "pet shop",

                    "automóvel" or
                    "automovel"
                        => "automóvel",

                    "estacionamento"
                        => "estacionamento",

                    "suplementos"
                        => "suplementos",

                    "lazer"
                        => "lazer",

                    "educação" or
                    "educacao"
                        => "educação",

                    "roupas"
                        => "roupas",

                    _ => "geral"
                };

            var categoriaFinal =
                categorias.FirstOrDefault(
                    c =>
                        c.Nome.ToLower().Trim() ==
                        nomeAlvo);

            if (categoriaFinal != null &&
                categoriaFinal.Id != 1)
            {
                return categoriaFinal.Id;
            }

            var fallbackDespesa =
                categorias.FirstOrDefault(
                    c =>
                        c.Nome.Contains("Mercearia") ||
                        c.Nome.Contains("Geral"))
                ?? categorias.FirstOrDefault(
                    c => c.Id != 1);

            return fallbackDespesa?.Id ?? 2;
        }

        // ============================================================
        // CLASSIFICAÇÃO AUTOMÁTICA DE PRODUTO
        // ============================================================

        private string ClassificarAutomaticamente(
            string nomeProduto)
        {
            if (string.IsNullOrEmpty(nomeProduto))
                return "Outros";

            string nome =
                RemoverAcentos(nomeProduto)
                    .ToUpperInvariant();

            var mapeamento =
                new Dictionary<string, string[]>
                {
                    {
                        "Laticínios e Frios",
                        new[]
                        {
                            "LEITE",
                            "NINHO",
                            "MUSSARELA",
                            "QUEIJO",
                            "DANONE",
                            "IOG",
                            "IOGURTE",
                            "RICOTA",
                            "APRESUNTADO",
                            "AURORA",
                            "BATAV",
                            "FERM",
                            "ELEGE"
                        }
                    },

                    {
                        "Mercearia",
                        new[]
                        {
                            "FEIJAO",
                            "ARROZ",
                            "TIO JOAO",
                            "MAC.D.BENTA",
                            "D.BENTA",
                            "OLEO",
                            "SOYA",
                            "AZEITE",
                            "ANDOR",
                            "ACUCAR",
                            "CAFE",
                            "3COR",
                            "CORACOES",
                            "CAPS",
                            "ATUM",
                            "MOLHO",
                            "EKMA",
                            "KISABOR",
                            "SAC ASSA",
                            "SACO",
                            "MAND",
                            "RAV",
                            "MEZZ",
                            "QJOS",
                            "MIX SA",
                            "GRANO",
                            "KICALDO",
                            "CAMIL"
                        }
                    },

                    {
                        "Limpeza",
                        new[]
                        {
                            "OMO",
                            "VANISH",
                            "LIMP.PERFUMADO",
                            "DETERG",
                            "AMAC",
                            "AMACITEL",
                            "DESINF",
                            "SANOL",
                            "ESPONJA",
                            "SCOTCH",
                            "BRITE",
                            "PAPEL",
                            "TOALHA",
                            "GLADE",
                            "DESODORIZADOR"
                        }
                    },

                    {
                        "Higiene e Saúde",
                        new[]
                        {
                            "SAB",
                            "DOVE",
                            "REXONA",
                            "DESOD",
                            "COLGATE",
                            "CREME DENT",
                            "SHAMP",
                            "KARITE",
                            "AERO",
                            "BWELL",
                            "MAG",
                            "500MG",
                            "REMEDIO",
                            "REMEDIOS",
                            "MEDICAMENTO",
                            "COMPRIMIDO",
                            "DIPIRONA",
                            "DORFLEX",
                            "FARMACIA"
                        }
                    },

                    {
                        "Hortifruti",
                        new[]
                        {
                            "BATATA",
                            "ALHO",
                            "PEPINO",
                            "ABOBORA",
                            "CEBOLA",
                            "TOMATE",
                            "BANANA",
                            "MACA"
                        }
                    },

                    {
                        "Snacks e Doces",
                        new[]
                        {
                            "BISCOITO",
                            "CLUB SOCIAL",
                            "BOMBOM",
                            "LACTA",
                            "SALG",
                            "ELMA",
                            "AMEND",
                            "DOCE",
                            "CRACKER",
                            "RANCHEIRO"
                        }
                    },

                    {
                        "Padaria",
                        new[]
                        {
                            "PAO",
                            "PULLMAN",
                            "BISNAGUITO",
                            "KIM",
                            "PAO FORMA KIM",
                            "PAO QJO MASSA",
                            "TORRADA",
                            "MASSA"
                        }
                    },

                    {
                        "Bebidas",
                        new[]
                        {
                            "REF",
                            "SPRITE",
                            "COCO",
                            "AGUA COCO",
                            "PURO COCO",
                            "AGUA",
                            "CRYSTAL",
                            "SUCO"
                        }
                    },

                    {
                        "Pet Shop",
                        new[]
                        {
                            "RACAO",
                            "PEDIGREE",
                            "DOG",
                            "CAT"
                        }
                    },

                    {
                        "Açougue",
                        new[]
                        {
                            "CARNE",
                            "FRANGO",
                            "LINGUICA",
                            "BIFE",
                            "PERD",
                            "PERDIGAO"
                        }
                    }
                };

            foreach (var categoria in mapeamento)
            {
                if (categoria.Value.Any(
                        keyword =>
                            nome.Contains(keyword)))
                {
                    return categoria.Key;
                }
            }

            return "Outros";
        }

        // ============================================================
        // CLASSIFICA NOTA PELOS ITENS
        // ============================================================

        private string ClassificarNotaPorItens(
            List<ItemLancamento> itens)
        {
            if (itens == null || !itens.Any())
                return "Outros";

            return itens
                .GroupBy(i => i.Categoria)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
        }

        // ============================================================
        // ATUALIZA ESTOQUE
        // ============================================================

        private async Task AtualizarEstoquePorNota(
            AppDbContext context,
            Lancamento lancamento,
            string userId,
            List<Categoria> categoriasDoUsuario)
        {
            if (lancamento.Itens == null ||
                !lancamento.Itens.Any())
            {
                return;
            }

            var estoqueUsuario =
                await context.Estoques
                    .Where(e => e.UsuarioId == userId)
                    .ToListAsync();

            foreach (var itemNota in lancamento.Itens)
            {
                string descricaoNotaUpper =
                    itemNota.Descricao.ToUpper();

                var itemEstoque =
                    estoqueUsuario.FirstOrDefault(
                        e =>
                            descricaoNotaUpper.Contains(
                                e.Nome.ToUpper()) ||
                            e.Nome.ToUpper().Contains(
                                descricaoNotaUpper));

                if (itemEstoque != null)
                {
                    itemEstoque.QuantidadeAtual +=
                        (decimal)itemNota.Quantidade;
                }
                else
                {
                    string categoriaTexto =
                        itemNota.Categoria;

                    string nomeAlvo =
                        categoriaTexto switch
                        {
                            "Mercearia" or
                            "Laticínios e Frios" or
                            "Snacks e Doces" or
                            "Padaria"
                                => "Mercado",

                            "Limpeza"
                                => "Limpeza",

                            "Higiene e Saúde"
                                => "Higiene",

                            "Hortifruti"
                                => "Hortifruti",

                            "Acougue" or
                            "Açougue"
                                => "Açougue",

                            _
                                => "Geral"
                        };

                    var catCorrespondente =
                        categoriasDoUsuario.FirstOrDefault(
                            c =>
                                c.Nome.ToLower()
                                    .Contains(
                                        nomeAlvo.ToLower()) ||
                                nomeAlvo.ToLower()
                                    .Contains(
                                        c.Nome.ToLower()));

                    int categoriaIdItem =
                        catCorrespondente?.Id ??
                        lancamento.CategoriaId;

                    var novoItemDespensa =
                        new Estoque
                        {
                            Nome =
                                CapitalizarTexto(
                                    itemNota.Descricao),

                            QuantidadeAtual =
                                (decimal)itemNota.Quantidade,

                            QuantidadeMinima = 1.00m,

                            UnidadeMedida = "un",

                            CategoriaId =
                                categoriaIdItem,

                            UsuarioId = userId
                        };

                    context.Estoques.Add(
                        novoItemDespensa);

                    estoqueUsuario.Add(
                        novoItemDespensa);
                }
            }

            await context.SaveChangesAsync();
        }

        // ============================================================
        // CAPITALIZA TEXTO
        // ============================================================

        private string CapitalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            texto = texto.Trim().ToLower();

            if (texto.Length > 80)
                texto = texto.Substring(0, 80);

            if (texto.Length == 1)
                return texto.ToUpper();

            return char.ToUpper(texto[0]) +
                   texto.Substring(1);
        }

        // ============================================================
        // EXTRAI DATA DO PDF
        // ============================================================

        private void ExtrairDataDoPdf(
            string textoBruto,
            Lancamento lancamento)
        {
            DateTime dataEmissao = DateTime.Now;
            bool dataEncontrada = false;

            var matchEmissao =
                Regex.Match(
                    textoBruto,
                    @"Emissão:\s*(\d{2}/\d{2}/\d{4})",
                    RegexOptions.IgnoreCase);

            if (matchEmissao.Success &&
                DateTime.TryParseExact(
                    matchEmissao.Groups[1].Value,
                    "dd/MM/yyyy",
                    _culturaBr,
                    DateTimeStyles.None,
                    out dataEmissao))
            {
                dataEncontrada = true;
            }

            if (!dataEncontrada)
            {
                var matchDataCompra =
                    Regex.Match(
                        textoBruto,
                        @"(\d{2}/\d{2}/\d{4}),\s*\d{2}:\d{2}",
                        RegexOptions.IgnoreCase);

                if (matchDataCompra.Success &&
                    DateTime.TryParseExact(
                        matchDataCompra.Groups[1].Value,
                        "dd/MM/yyyy",
                        _culturaBr,
                        DateTimeStyles.None,
                        out dataEmissao))
                {
                    dataEncontrada = true;
                }
            }

            lancamento.DataEmissao =
                dataEncontrada
                    ? dataEmissao.Date
                    : DateTime.Today;

            lancamento.Data =
                lancamento.DataEmissao;
        }

        // ============================================================
        // EXTRAI VALOR PAGO
        // ============================================================

        private decimal ExtrairValorPago(
            string texto,
            decimal somaItens)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                Console.WriteLine(
                    $"[VALOR] Texto vazio. Usando soma dos itens: {somaItens:C2}"
                );
                return somaItens;
            }

            // Prioridade 1: valor efetivamente devido após descontos.
            var matchValorAPagar =
                Regex.Match(
                    texto,
                    @"Valor\s+a\s+pagar\s+R?\$?\s*:?\s*(\d{1,3}(?:\.\d{3})*,\d{2}|\d+[\.,]\d{2})",
                    RegexOptions.IgnoreCase);

            if (matchValorAPagar.Success)
            {
                decimal valor =
                    ConverterParaDecimal(
                        matchValorAPagar.Groups[1].Value,
                        _culturaBr);

                if (valor > 0)
                {
                    Console.WriteLine(
                        $"[VALOR] Valor a pagar encontrado: {valor:C2}"
                    );
                    return valor;
                }
            }

            // Prioridade 2: valor pago quando o número aparece na mesma sequência.
            var matchValorPago =
                Regex.Match(
                    texto,
                    @"Valor\s+pago\s+R?\$?\s*:?\s*(\d{1,3}(?:\.\d{3})*,\d{2}|\d+[\.,]\d{2})",
                    RegexOptions.IgnoreCase);

            if (matchValorPago.Success)
            {
                decimal valor =
                    ConverterParaDecimal(
                        matchValorPago.Groups[1].Value,
                        _culturaBr);

                if (valor > 0)
                {
                    Console.WriteLine(
                        $"[VALOR] Valor pago encontrado: {valor:C2}"
                    );
                    return valor;
                }
            }

            // Prioridade 3: total bruto.
            var matchValorTotal =
                Regex.Match(
                    texto,
                    @"Valor\s+total\s+R?\$?\s*:?\s*(\d{1,3}(?:\.\d{3})*,\d{2}|\d+[\.,]\d{2})",
                    RegexOptions.IgnoreCase);

            if (matchValorTotal.Success)
            {
                decimal valor =
                    ConverterParaDecimal(
                        matchValorTotal.Groups[1].Value,
                        _culturaBr);

                if (valor > 0)
                {
                    Console.WriteLine(
                        $"[VALOR] Valor total encontrado: {valor:C2}"
                    );
                    return valor;
                }
            }

            Console.WriteLine(
                $"[VALOR] Usando soma dos itens: {somaItens:C2}"
            );

            return somaItens;
        }

        // ============================================================
        // CONVERTE DECIMAL
        // ============================================================

        private decimal ConverterParaDecimal(
            string valor,
            CultureInfo cultura)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return 0;

            string limpo =
                Regex.Replace(
                    valor,
                    @"[^\d,]",
                    "");

            return decimal.TryParse(
                limpo,
                NumberStyles.Any,
                cultura,
                out decimal res)
                    ? res
                    : 0;
        }

        // ============================================================
        // CRIA LANÇAMENTO BASE
        // ============================================================

        private Lancamento CriarLancamentoBase(
            string userId)
        {
            return new Lancamento
            {
                UsuarioId = userId,
                Tipo = "despesa",
                Itens = new List<ItemLancamento>()
            };
        }
    }
}
