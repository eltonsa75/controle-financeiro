using Financeiro.Api.Models;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace FinanceiroApi.Services
{
    public class ImportacaoNotaService
    {
        private readonly AppDbContext _context;
        private readonly CultureInfo _culturaBr = new CultureInfo("pt-BR");

        public ImportacaoNotaService(AppDbContext context)
        {
            _context = context;
        }

        // DTOs locais estruturados para mapear o retorno JSON do Python
        public class PythonItemDto
        {
            public string Descricao { get; set; }
            public double Quantidade { get; set; }
            public decimal Preco { get; set; }
            public string Categoria { get; set; } // Recebe o retorno estruturado do motor Python
        }

        public class PythonRespostaDto { public string Estabelecimento { get; set; } public string TextoBruto { get; set; } public List<PythonItemDto> Itens { get; set; } }

        // ==================== MÉTODO PRINCIPAL ====================

        public async Task<Lancamento> ProcessarPdfNota(Stream arquivoStream, string userId)
        {
            Console.WriteLine("\n==== [.NET] INTEGRANDO COM MICROSERVIÇO PYTHON ====");
            var lancamento = CriarLancamentoBase(userId);
            string textoBrutoDoPdf = "";
            PythonRespostaDto dadosMapeados = null;

            // 1. Envia o arquivo Stream recebido do Angular diretamente para o Python
            using (var client = new HttpClient())
            using (var content = new MultipartFormDataContent())
            {
                arquivoStream.Position = 0; // Reseta o ponteiro do arquivo
                var streamContent = new StreamContent(arquivoStream);
                content.Add(streamContent, "file", "nota_upload.pdf");

                try
                {
                    // Faz a chamada HTTP POST para a API Python FastAPI
                    var respostaPython = await client.PostAsync("http://localhost:8000/extrair-nota", content);

                    if (!respostaPython.IsSuccessStatusCode)
                    {
                        throw new Exception($"O microsserviço Python retornou erro: {respostaPython.StatusCode}");
                    }

                    // Lê o JSON estruturado que o Python gerou usando o 'pdfplumber' e a categorização local
                    dadosMapeados = await respostaPython.Content.ReadFromJsonAsync<PythonRespostaDto>();

                    if (dadosMapeados != null)
                    {
                        lancamento.Descricao = dadosMapeados.Estabelecimento;
                        textoBrutoDoPdf = dadosMapeados.TextoBruto;

                        // Alimenta a entidade de lançamento do C# com os itens já limpos pelo Python
                        lancamento.Itens = dadosMapeados.Itens.Select(i => new ItemLancamento
                        {
                            Descricao = i.Descricao,
                            Quantidade = i.Quantidade,
                            Preco = i.Preco,
                            Comprado = true
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[.NET ERRO CRÍTICO] Falha ao comunicar com o Python: {ex.Message}");
                    lancamento.Descricao = "ERRO INTERNO NO MOTOR PYTHON";
                }
            }

            // 2. Extrai os metadados usando o texto bruto unificado retornado do Python
            ExtrairDataDoPdf(textoBrutoDoPdf, lancamento);

            // 3. Busca categorias do banco para cruzamento inteligente por palavras-chave
            var categoriasDoUsuario = await _context.Categorias.Where(c => c.UsuarioId == userId).ToListAsync();
            lancamento.CategoriaId = DeterminarCategoriaInteligente(lancamento, categoriasDoUsuario, textoBrutoDoPdf);

            // 4. Vincula as subcategorias em cada item baseando-se no mapeamento do Python
            foreach (var item in lancamento.Itens)
            {
                // Puxa do retorno do Python o item correspondente para extrair a categoria definida
                var categoriaVindaDoPython = dadosMapeados?.Itens?.FirstOrDefault(i => i.Descricao == item.Descricao)?.Categoria;

                // 🎯 ATRIBUIÇÃO DIRETA NA STRING: Se o Python categorizou com sucesso, salvamos no campo. 
                // Se veio "Outros" ou vazio, roda o método estático local como segurança.
                item.Categoria = !string.IsNullOrEmpty(categoriaVindaDoPython) && categoriaVindaDoPython != "Outros"
                    ? categoriaVindaDoPython
                    : ClassificarAutomaticamente(item.Descricao);
            }

            lancamento.Tipo = "despesa";

            decimal somaItens = lancamento.Itens.Any()
                ? lancamento.Itens.Sum(it => (decimal)it.Quantidade * it.Preco)
                : 0;

            decimal valorPago = ExtrairValorPago(textoBrutoDoPdf, somaItens);

            lancamento.Valor = -Math.Abs(valorPago > 0 ? valorPago : somaItens);
            lancamento.DataImportacao = DateTime.Now;

            Console.WriteLine($"[.NET LOG] Total de Itens: {lancamento.Itens.Count} | Valor Calculado: R$ {Math.Abs(lancamento.Valor):F2}");

            // 5. Salva os registros de forma rígida no MySQL
            _context.Lancamentos.Add(lancamento);
            await _context.SaveChangesAsync();

            // 🎯 FORÇA CARREGAMENTO EM MEMÓRIA: Garante que os objetos internos fiquem totalmente sincronizados antes de voltar para o front
            await _context.Entry(lancamento).Reference(l => l.Categoria).LoadAsync();
            await _context.Entry(lancamento).Collection(l => l.Itens).LoadAsync();

            // 6. Alimenta de forma incremental as tabelas do Inventário (Estoque) utilizando as novas categorias
            await AtualizarEstoquePorNota(lancamento, userId, categoriasDoUsuario);

            Console.WriteLine("==== [.NET] FLUXO DE COMPILAÇÃO E SALVAMENTO CONCLUÍDO ====\n");
            return lancamento;
        }

        // ==================== INTELIGENCIAS E MÉTODOS AUXILIARES C# ====================

        private int DeterminarCategoriaInteligente(Lancamento lancamento, List<Categoria> categorias, string textoBruto)
        {
           
            string textoParaMapear = (lancamento.Descricao + " " + textoBruto).ToLower();

            foreach (var cat in categorias)
            {
                if (!string.IsNullOrEmpty(cat.PalavrasChave))
                {
                    var palavras = cat.PalavrasChave.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in palavras)
                    {
                        string palavraChaveLimpa = p.Trim().ToLower();
                        if (!string.IsNullOrEmpty(palavraChaveLimpa) && textoParaMapear.Contains(palavraChaveLimpa))
                        {
                            return cat.Id; 
                        }
                    }
                }
            }

           
            string categoriaNotaSub = ClassificarNotaPorItens(lancamento.Itens.ToList());

            string nomeAlvo = categoriaNotaSub.ToLower().Trim() switch
            {
                "mercearia" => "mercearia",
                "laticínios e frios" or "laticinios e frios" => "laticínios e frios",
                "snacks e doces" => "snacks e doces",
                "padaria" => "padaria",
                "acougue" or "açougue" => "açougue",
                "bebidas" => "bebidas",
                "limpeza" => "limpeza",
                "higiene e saúde" or "higiene e saude" or "higiene" => "higiene",
                "farmácia" or "farmacia" => "farmácia",
                "hortifruti" => "hortifruti",
                "pet shop" or "petshop" => "pet shop",
                "automóvel" or "automovel" => "automóvel",
                "estacionamento" => "estacionamento",
                "suplementos" => "suplementos",
                "lazer" => "lazer",
                "educação" or "educacao" => "educação",
                "roupas" => "roupas",
                _ => "geral"
            };

            // Busca no banco a categoria que bate com o nomeAlvo do switch
            var categoriaFinal = categorias.FirstOrDefault(c =>
                c.Nome.ToLower().Trim() == nomeAlvo);

            if (categoriaFinal != null && categoriaFinal.Id != 1) return categoriaFinal.Id;

            // Fallback de segurança: joga para Mercearia ou Geral
            var fallbackDespesa = categorias.FirstOrDefault(c => c.Nome.Contains("Mercearia") || c.Nome.Contains("Geral"))
                                  ?? categorias.FirstOrDefault(c => c.Id != 1);

            return fallbackDespesa?.Id ?? 2;
        }

        private string ClassificarAutomaticamente(string nomeProduto)
        {
            if (string.IsNullOrEmpty(nomeProduto)) return "Outros";
            string nome = nomeProduto.ToUpper();

            var mapeamento = new Dictionary<string, string[]>
            {
                { "Laticínios e Frios", new[] { "LEITE", "LEITE L.VIDA", "NINHO", "MUSSARELA", "QUEIJO", "DANONE", "IOG", "IOGURTE", "RICOTA", "APRESUNTADO", "AURORA", "BATAV", "FERM", "ELEGE" } },
                { "Mercearia", new[] { "FEIJAO", "FEIJÃO", "ARROZ", "ARROZ TIO JOAO", "TIO JOAO", "MAC.D.BENTA", "D.BENTA", "OLEO", "ÓLEO", "SOYA", "AZEITE", "ANDOR", "ACUCAR", "CAFE", "3COR", "CORACOES", "CAPS", "MAC.", "ATUM", "MOLHO", "EKMA", "KISABOR", "SAC ASSA", "SACO", "MAND", "RAV", "MEZZ", "QJOS", "MIX SA", "GRANO", "KICALDO", "CAMIL" } },
                { "Limpeza", new[] { "OMO", "VANISH", "LIMP.PERFUMADO", "DETERG", "AMAC ROUPA AMACITEL", "AMACITEL", "DESINF.SANOL", "SANOL", "SAC INST.BIO", "60X70", "INST.BIO", "ESPONJA", "SCOTCH", "BRITE", "PAPEL", "TOALHA", "BULNEZ", "GLADE", "DESODORIZADOR" } },
                {
    "Higiene e Saúde", new[] {
        "SAB.", "DOVE", "REXONA", "DESOD.", "COLGATE", "CREME DENT", "SHAMP",
        "KARITE", "AERO", "BWELL", "MAG", "500MG", "CR.D.", "REMEDIO", "REMEDIOS",
        "REM.", "MEDICAMENTO", "COMPRIMIDO", "COMP.", "DIPIRONA", "DORFLEX", "FARMACIA"
    }
},
                { "Hortifruti", new[] { "BATATA", "ALHO", "PEPINO", "ABOBORA", "CEBOLA", "TOMATE", "BANANA", "MACA" } },
                { "Snacks e Doces", new[] { "BISCOITO", "CLUB SOCIAL", "BOMBOM", "LACTA", "SALG.", "ELMA", "AMEND", "DOCE", "CRACKER", "RANCHEIRO" } },
                { "Padaria", new[] { "PAO", "PÃO", "PULLMAN", "BISNAGUITO", "KIM", "PAO FORMA KIM", "PAO QJO MASSA", "TORRADA", "MASSA" } },
                { "Bebidas", new[] { "REF.", "SPRITE", "COCO", "AGUA COCO PURO COCO", "PURO COCO", "AGUA", "A M.CRYSTAL", "CRYSTAL", "SUCO" } },
                { "Pet Shop", new[] { "RACAO", "RACÃO", "RAÇÃO PEDIGREE", "PEDIGREE", "DOG", "CAT" } },
                { "Acougue", new[] { "CARNE", "FRANGO", "LINGUICA", "BIFE", "PERD.", "PERDIGAO" } }
            };

            foreach (var categoria in mapeamento)
            {
                if (categoria.Value.Any(keyword => nome.Contains(keyword))) return categoria.Key;
            }
            return "Outros";
        }

        private string ClassificarNotaPorItens(List<ItemLancamento> itens)
        {
            if (itens == null || !itens.Any()) return "Outros";
            return itens.GroupBy(i => i.Categoria).OrderByDescending(g => g.Count()).First().Key;
        }

        private async Task AtualizarEstoquePorNota(Lancamento lancamento, string userId, List<Categoria> categoriasDoUsuario)
        {
            if (lancamento.Itens == null || !lancamento.Itens.Any()) return;

            var estoqueUsuario = await _context.Estoques.Where(e => e.UsuarioId == userId).ToListAsync();

            foreach (var itemNota in lancamento.Itens)
            {
                string descricaoNotaUpper = itemNota.Descricao.ToUpper();
                var itemEstoque = estoqueUsuario.FirstOrDefault(e => descricaoNotaUpper.Contains(e.Nome.ToUpper()) || e.Nome.ToUpper().Contains(descricaoNotaUpper));

                if (itemEstoque != null)
                {
                    itemEstoque.QuantidadeAtual += (decimal)itemNota.Quantidade;
                }
                else
                {
                    string categoriaTexto = itemNota.Categoria;
                    string nomeAlvo = categoriaTexto switch
                    {
                        "Mercearia" or "Laticínios e Frios" or "Snacks e Doces" or "Padaria" => "Mercado",
                        "Limpeza" => "Limpeza",
                        "Higiene e Saúde" => "Higiene",
                        "Hortifruti" => "Hortifruti",
                        "Acougue" => "Açougue",
                        _ => "Geral"
                    };

                    var catCorrespondente = categoriasDoUsuario.FirstOrDefault(c => c.Nome.ToLower().Contains(nomeAlvo.ToLower()) || nomeAlvo.ToLower().Contains(c.Nome.ToLower()));
                    int categoriaIdItem = catCorrespondente?.Id ?? lancamento.CategoriaId;

                    var novoItemDespensa = new Estoque
                    {
                        Nome = CapitalizarTexto(itemNota.Descricao),
                        QuantidadeAtual = (decimal)itemNota.Quantidade,
                        QuantidadeMinima = 1.00m,
                        UnidadeMedida = "un",
                        CategoriaId = categoriaIdItem,
                        UsuarioId = userId
                    };

                    _context.Estoques.Add(novoItemDespensa);
                    estoqueUsuario.Add(novoItemDespensa);
                }
            }
            await _context.SaveChangesAsync();
        }

        private string CapitalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            texto = texto.Trim().ToLower();
            if (texto.Length > 80) texto = texto.Substring(0, 80);
            if (texto.Length == 1) return texto.ToUpper();
            return char.ToUpper(texto[0]) + texto.Substring(1);
        }

        private void ExtrairDataDoPdf(string textoBruto, Lancamento lancamento)
        {
            DateTime dataEmissao = DateTime.Now;
            bool dataEncontrada = false;

            var matchEmissao = Regex.Match(textoBruto, @"Emissão:\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
            if (matchEmissao.Success && DateTime.TryParseExact(matchEmissao.Groups[1].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out dataEmissao))
                dataEncontrada = true;

            if (!dataEncontrada)
            {
                var matchDataCompra = Regex.Match(textoBruto, @"(\d{2}/\d{2}/\d{4}),\s*\d{2}:\d{2}", RegexOptions.IgnoreCase);
                if (matchDataCompra.Success && DateTime.TryParseExact(matchDataCompra.Groups[1].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out dataEmissao))
                    dataEncontrada = true;
            }

            lancamento.DataEmissao = dataEncontrada ? dataEmissao : DateTime.Now;
            lancamento.Data = lancamento.DataEmissao;
        }

        private decimal ExtrairValorPago(string texto, decimal somaItens)
        {
            var matchValorPago = Regex.Match(texto, @"Valor\s+pago\s+R?\$?:?\s*(\d+[\.,]\d{2})", RegexOptions.IgnoreCase);
            if (matchValorPago.Success) return ConverterParaDecimal(matchValorPago.Groups[1].Value, _culturaBr);

            var matchTotal = Regex.Match(texto, @"(?:Total|TOTAL)\s*:?\s*R?\$?\s*(\d+[\.,]\d{2})", RegexOptions.IgnoreCase);
            if (matchTotal.Success) return ConverterParaDecimal(matchTotal.Groups[1].Value, _culturaBr);

            return somaItens;
        }

        private decimal ConverterParaDecimal(string valor, CultureInfo cultura)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0;
            string limpo = Regex.Replace(valor, @"[^\d,]", "");
            return decimal.TryParse(limpo, NumberStyles.Any, cultura, out decimal res) ? res : 0;
        }

        private Lancamento CriarLancamentoBase(string userId)
        {
            return new Lancamento { UsuarioId = userId, Tipo = "despesa", CategoriaId = 2, Itens = new List<ItemLancamento>() };
        }
    }
}