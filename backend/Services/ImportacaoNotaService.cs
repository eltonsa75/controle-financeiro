using Financeiro.Api.Models;
using FinanceiroApi.Data;
using FinanceiroApi.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

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

        // ==================== MÉTODOS PÚBLICOS ====================

        public async Task<Lancamento> ProcessarPdfNota(Stream arquivoStream, string userId)
        {
            var lancamento = CriarLancamentoBase(userId);

            using (var pdf = PdfDocument.Open(arquivoStream))
            {
                var paginas = pdf.GetPages();

                // ========== Juntar páginas com separador ==========
                var textosPaginas = new List<string>();
                int pageCount = 0;
                foreach (var page in paginas)
                {
                    string textoPagina = page.Text;
                    textosPaginas.Add(textoPagina);
                    pageCount++;
                    Console.WriteLine($"[PDF] Página {pageCount}: {textoPagina.Length} caracteres");
                }

                // Junta com espaço para não concatenar palavras
                string textoBruto = string.Join(" ", textosPaginas);
                // ============================================

                // ========== EXTRAIR DATA DO PDF ==========
                ExtrairDataDoPdf(textoBruto, lancamento);
                // ============================================

                // Limpa o texto
                string textoLimpo = Regex.Replace(textoBruto, @"\s+", " ");

                // Processa estabelecimento (empresa + endereço)
                ProcessarEstabelecimentoPdf(textoBruto, lancamento);

                // Processa itens
                lancamento.Itens = ProcessarItensDoPdf(textoLimpo, lancamento.Descricao);

                // ========== CLASSIFICAÇÃO AUTOMÁTICA DOS PRODUTOS ==========
                foreach (var item in lancamento.Itens)
                {
                    item.Categoria = ClassificarAutomaticamente(item.Descricao);
                    Console.WriteLine($"[CLASSIFICAÇÃO] Produto: {item.Descricao} -> Categoria: {item.Categoria}");
                }

                // ========== CLASSIFICAÇÃO DA NOTA POR PRODUTOS ==========
                var listaItens = lancamento.Itens.ToList();
                string categoriaNota = ClassificarNotaPorItens(listaItens);

                // Busca a categoria no banco de dados
                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.Nome == categoriaNota && c.UsuarioId == userId);

                if (categoria != null)
                {
                    lancamento.CategoriaId = categoria.Id;
                    lancamento.Tipo = categoriaNota.ToLower();
                    Console.WriteLine($"[CLASSIFICAÇÃO NOTA] Categoria: {categoriaNota} (ID: {categoria.Id})");
                }
                else
                {
                    Console.WriteLine($"[CLASSIFICAÇÃO NOTA] Categoria '{categoriaNota}' não encontrada. Usando padrão (ID: 1)");
                }

                // Calcula soma dos itens
                decimal somaItens = lancamento.Itens.Any()
                    ? lancamento.Itens.Sum(it => (decimal)it.Quantidade * it.Preco)
                    : 0;

                // Extrai valor pago - busca em TODO o texto
                decimal valorPago = ExtrairValorPago(textoBruto, somaItens);

                // Define o valor final do lançamento
                lancamento.Valor = -Math.Abs(valorPago > 0 ? valorPago : somaItens);
                lancamento.DataImportacao = DateTime.Now;

                Console.WriteLine($"[FINAL] Data Emissão: {lancamento.DataEmissao:dd/MM/yyyy}");
                Console.WriteLine($"[FINAL] Descricao: {lancamento.Descricao}");
                Console.WriteLine($"[FINAL] Categoria Nota: {categoriaNota}");
                Console.WriteLine($"[FINAL] Soma Itens: {somaItens:C} | Valor Pago: {lancamento.Valor:C} | Itens: {lancamento.Itens.Count}");
            }

            // ========== ADICIONAR AO BANCO E RETORNAR ==========
            _context.Lancamentos.Add(lancamento);
            await _context.SaveChangesAsync();
            await _context.Entry(lancamento).Reference(l => l.Categoria).LoadAsync();

            return lancamento;  // <--- ESTA LINHA É ESSENCIAL
        }

        private void ExtrairDataDoPdf(string textoBruto, Lancamento lancamento)
        {
            DateTime dataEmissao = DateTime.Now;
            bool dataEncontrada = false;

            // PRIORIDADE 1: Buscar "Emissão: 28/02/2026"
            var matchEmissao = Regex.Match(textoBruto, @"Emissão:\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
            if (matchEmissao.Success)
            {
                if (DateTime.TryParseExact(matchEmissao.Groups[1].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out dataEmissao))
                {
                    dataEncontrada = true;
                    Console.WriteLine($"[DATA] Encontrada via 'Emissão': {dataEmissao:dd/MM/yyyy}");
                }
            }

            // PRIORIDADE 2: Buscar data no padrão "29/04/2026, 22:55"
            if (!dataEncontrada)
            {
                var matchDataCompra = Regex.Match(textoBruto, @"(\d{2}/\d{2}/\d{4}),\s*\d{2}:\d{2}", RegexOptions.IgnoreCase);
                if (matchDataCompra.Success)
                {
                    if (DateTime.TryParseExact(matchDataCompra.Groups[1].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out dataEmissao))
                    {
                        dataEncontrada = true;
                        Console.WriteLine($"[DATA] Encontrada via data da compra: {dataEmissao:dd/MM/yyyy}");
                    }
                }
            }

            // PRIORIDADE 3: Buscar "Data/Hora: 29/04/2026"
            if (!dataEncontrada)
            {
                var matchDataHora = Regex.Match(textoBruto, @"Data/Hora:\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
                if (matchDataHora.Success)
                {
                    if (DateTime.TryParseExact(matchDataHora.Groups[1].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out dataEmissao))
                    {
                        // Verifica se a data não é muito recente
                        if (dataEmissao < DateTime.Now.AddDays(-1))
                        {
                            dataEncontrada = true;
                            Console.WriteLine($"[DATA] Encontrada via 'Data/Hora': {dataEmissao:dd/MM/yyyy}");
                        }
                    }
                }
            }

            // PRIORIDADE 4: Buscar qualquer data
            if (!dataEncontrada)
            {
                var matchesData = Regex.Matches(textoBruto, @"\b(\d{2}/\d{2}/\d{4})\b");
                foreach (Match match in matchesData)
                {
                    if (DateTime.TryParseExact(match.Groups[1].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out dataEmissao))
                    {
                        if (dataEmissao < DateTime.Now && dataEmissao > new DateTime(2024, 1, 1))
                        {
                            dataEncontrada = true;
                            Console.WriteLine($"[DATA] Encontrada via data genérica: {dataEmissao:dd/MM/yyyy}");
                            break;
                        }
                    }
                }
            }

            if (dataEncontrada)
            {
                lancamento.DataEmissao = dataEmissao;
                lancamento.Data = dataEmissao;
                Console.WriteLine($"[DATA] Data da nota definida: {lancamento.DataEmissao:dd/MM/yyyy}");
            }
            else
            {
                Console.WriteLine($"[DATA] Nenhuma data válida encontrada, usando data atual: {DateTime.Now:dd/MM/yyyy}");
            }
        }

        private decimal ExtrairValorPago(string texto, decimal somaItens)
        {
            // Tenta encontrar "Valor pago R$: 698,92"
            var matchValorPago = Regex.Match(texto, @"Valor\s+pago\s+R?\$?:?\s*(\d+,\d{2})", RegexOptions.IgnoreCase);
            if (matchValorPago.Success)
            {
                decimal valor = ConverterParaDecimal(matchValorPago.Groups[1].Value, _culturaBr);
                Console.WriteLine($"[VALOR PAGO] Encontrado como 'Valor pago': R$ {valor:F2}");
                return valor;
            }

            // Tenta encontrar "Valor a pagar R$: 698,92"
            var matchValorAPagar = Regex.Match(texto, @"Valor\s+a\s+pagar\s+R?\$?:?\s*(\d+,\d{2})", RegexOptions.IgnoreCase);
            if (matchValorAPagar.Success)
            {
                decimal valor = ConverterParaDecimal(matchValorAPagar.Groups[1].Value, _culturaBr);
                Console.WriteLine($"[VALOR PAGO] Encontrado como 'Valor a pagar': R$ {valor:F2}");
                return valor;
            }

            // Tenta encontrar "Total: R$ 698,92"
            var matchTotal = Regex.Match(texto, @"(?:Total|TOTAL)\s*:?\s*R?\$?\s*(\d+,\d{2})", RegexOptions.IgnoreCase);
            if (matchTotal.Success)
            {
                decimal valor = ConverterParaDecimal(matchTotal.Groups[1].Value, _culturaBr);
                if (valor <= somaItens + 10 && valor > 0)
                {
                    Console.WriteLine($"[VALOR PAGO] Encontrado como 'Total': R$ {valor:F2}");
                    return valor;
                }
            }

            // Tenta encontrar "Valor total R$: 698,92"
            var matchValorTotal = Regex.Match(texto, @"Valor\s+total\s*:?\s*R?\$?\s*(\d+,\d{2})", RegexOptions.IgnoreCase);
            if (matchValorTotal.Success)
            {
                decimal valor = ConverterParaDecimal(matchValorTotal.Groups[1].Value, _culturaBr);
                if (valor <= somaItens + 10 && valor > 0)
                {
                    Console.WriteLine($"[VALOR PAGO] Encontrado como 'Valor total': R$ {valor:F2}");
                    return valor;
                }
            }

            // Tenta encontrar "LÍQUIDO R$: 698,92"
            var matchLiquido = Regex.Match(texto, @"L[ÍI]QUIDO\s*:?\s*R?\$?\s*(\d+,\d{2})", RegexOptions.IgnoreCase);
            if (matchLiquido.Success)
            {
                decimal valor = ConverterParaDecimal(matchLiquido.Groups[1].Value, _culturaBr);
                if (valor <= somaItens + 10 && valor > 0)
                {
                    Console.WriteLine($"[VALOR PAGO] Encontrado como 'Líquido': R$ {valor:F2}");
                    return valor;
                }
            }

            // Fallback: pega o último número da nota
            var matches = Regex.Matches(texto, @"\d+,\d{2}");
            int totalMatches = matches.Count;

            if (totalMatches >= 1)
            {
                // Pega o último número (geralmente o total)
                string ultimoNumero = matches[totalMatches - 1].Value;
                decimal valor = ConverterParaDecimal(ultimoNumero, _culturaBr);

                // Verifica se é um valor plausível
                if (valor > 0 && valor <= somaItens + 100)
                {
                    Console.WriteLine($"[VALOR PAGO] Fallback - último número: R$ {valor:F2}");
                    return valor;
                }

                // Se o último não funcionar, tenta o penúltimo
                if (totalMatches >= 2)
                {
                    string penultimoNumero = matches[totalMatches - 2].Value;
                    decimal valor2 = ConverterParaDecimal(penultimoNumero, _culturaBr);
                    if (valor2 > 0 && valor2 <= somaItens + 100)
                    {
                        Console.WriteLine($"[VALOR PAGO] Fallback - penúltimo número: R$ {valor2:F2}");
                        return valor2;
                    }
                }
            }

            Console.WriteLine($"[VALOR PAGO] Não encontrado, usando soma dos itens: R$ {somaItens:F2}");
            return somaItens;
        }

        // ==================== LÓGICA DE DATA INTELIGENTE ====================

        private DateTime ExtrairDataDaString(string texto)
        {
            // Tenta encontrar "Emissão: dd/mm/yyyy"
            var matchEmissao = Regex.Match(texto, @"(?:Emissão|Data|Apresentação):\s*(?<data>\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);

            if (matchEmissao.Success && DateTime.TryParseExact(matchEmissao.Groups["data"].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out var dataReal))
            {
                return dataReal;
            }

            // Busca todas as datas e ignora a de hoje (provável data de impressão)
            var todasDatas = Regex.Matches(texto, @"(\d{2}/\d{2}/\d{4})");
            for (int i = todasDatas.Count - 1; i >= 0; i--)
            {
                if (DateTime.TryParseExact(todasDatas[i].Value, "dd/MM/yyyy", _culturaBr, DateTimeStyles.None, out var data))
                {
                    if (data.Date == DateTime.Now.Date && todasDatas.Count > 1) continue;
                    return data;
                }
            }
            return DateTime.Now;
        }

        // ==================== LÓGICA DE VALOR COM PROVA REAL ====================

        private decimal ExtrairValorLiquidoComProvaReal(string texto, decimal somaItens)
        {
            // Captura todos os números monetários (0,00)
            var matches = Regex.Matches(texto, @"(?<val>\d+,\d{2})");
            var listaValores = matches.Cast<Match>().Select(m => m.Groups["val"].Value).ToList();

            decimal maiorValorConfirmado = 0;

            foreach (var valorTexto in listaValores)
            {
                decimal candidato = ConverterParaDecimal(valorTexto, _culturaBr);

                // Ignora se for a própria soma bruta
                if (Math.Abs(candidato - somaItens) < 0.01m) continue;

                // A PROVA REAL: (Soma Bruta - Valor Pago) deve ser igual ao Desconto que também está na nota
                decimal diferenca = Math.Abs(somaItens - candidato);
                string diferencaTexto = diferenca.ToString("N2", _culturaBr);

                if (diferenca > 0 && listaValores.Contains(diferencaTexto))
                {
                    // O valor pago é o maior do par (Ex: 92,39 vs 26,89)
                    decimal possivelTotal = Math.Max(candidato, diferenca);
                    if (possivelTotal > maiorValorConfirmado) maiorValorConfirmado = possivelTotal;
                }
            }

            return maiorValorConfirmado > 0 ? maiorValorConfirmado : somaItens;
        }

        // ==================== CLASSIFICAÇÃO AUTOMÁTICA ====================

        private string ClassificarAutomaticamente(string nomeProduto)
        {
            if (string.IsNullOrEmpty(nomeProduto)) return "Outros";

            string nome = nomeProduto.ToUpper();
            var mapeamento = new Dictionary<string, string[]>
            {
                { "Laticínios e Frios", new[] { "LEITE", "NINHO", "MUSSARELA", "QUEIJO", "DANONE", "IOG", "IOGURTE", "RICOTA", "APRESUNTADO", "AURORA", "BATAV", "FERM", "ELEGE" } },
                { "Mercearia", new[] { "FEIJAO", "ARROZ", "OLEO", "SOYA", "AZEITE", "ANDOR", "ACUCAR", "CAFE", "3COR", "CORACOES", "CAPS", "MAC.", "D.BENTA", "ATUM", "MOLHO", "EKMA", "KISABOR", "SAC ASSA", "SACO", "MAND", "RAV", "MEZZ", "QJOS", "MIX SA", "GRANO" } },
                { "Limpeza", new[] { "OMO", "VANISH", "LIMP.PERFUMADO", "DETERG", "AMACITEL", "SANOL", "ESPONJA", "SCOTCH", "BRITE", "PAPEL", "TOALHA", "BULNEZ", "GLADE", "DESODORIZADOR", "T.MANCHAS", "MANCHAS" } },
                { "Higiene e Saúde", new[] { "SAB.", "DOVE", "REXONA", "DESOD.", "COLGATE", "CREME DENT", "SHAMP", "KARITE", "AERO", "BWELL", "MAG", "500MG", "NASOAR", "FRASCO", "ENVELO", "SUPLEMENTO", "VITAMINA", "REMEDIO", "FARMACIA", "DROGASIL", "DROGARAIA" } },
                { "Hortifruti", new[] { "BATATA", "ALHO", "PEPINO", "ABOBORA", "CEBOLA", "TOMATE", "BANANA", "MACA", "LAVADA" } },
                { "Snacks e Doces", new[] { "BISCOITO", "CLUB SOCIAL", "BOMBOM", "LACTA", "SALG.", "ELMA", "AMEND", "DOCE", "GUIMARAES", "CRACKER", "RANCHEIRO", "VIVALE", "BISC", "POLV" } },
                { "Padaria", new[] { "PAO", "PULLMAN", "BISNAGUITO", "KIM", "TORRADA", "MASSA", "FORMA", "LEVE" } },
                { "Bebidas", new[] { "REF.", "SPRITE", "COCO", "AGUA", "CRYSTAL", "SUCO", "CERVEJA", "CHA", "LEAO" } },
                { "Pet Shop", new[] { "RACAO", "PEDIGREE", "DOG", "CAT", "BICHO" } },
                { "Acougue", new[] { "CARNE", "FRANGO", "LINGUICA", "BIFE", "PERD.", "PERDIGAO" } },
                { "Educacao", new[] { "FACULDADE", "FAAP", "CURSO", "LIVRO", "MENSALIDADE", "UDEMY" } }
            };

            foreach (var categoria in mapeamento)
            {
                if (categoria.Value.Any(keyword => nome.Contains(keyword)))
                    return categoria.Key;
            }

            return "Outros";
        }

        private string ClassificarNotaPorItens(List<ItemLancamento> itens)
        {
            if (itens == null || !itens.Any()) return "Outros";
            var categoriasContagem = itens.GroupBy(i => ClassificarAutomaticamente(i.Descricao))
                                          .OrderByDescending(g => g.Count())
                                          .First().Key;
            return categoriasContagem;
        }

        // ==================== PROCESSAMENTO DO ESTABELECIMENTO ====================

        private void ProcessarEstabelecimentoPdf(string textoBruto, Lancamento lancamento)
        {
            string texto = textoBruto.ToUpper();
            string empresa = ExtrairEmpresa(texto);
            string endereco = ExtrairEndereco(texto);

            if (!string.IsNullOrEmpty(empresa))
                lancamento.Descricao = string.IsNullOrEmpty(endereco) ? empresa : $"{empresa} - {endereco}";
            else
                lancamento.Descricao = $"NOTA FISCAL PDF - {lancamento.DataEmissao:dd/MM/yyyy}";

            lancamento.Descricao = Regex.Replace(lancamento.Descricao, @"\s+", " ").Trim();
        }

        private string ExtrairEmpresa(string texto)
        {
            if (texto.Contains("RAIADROGASIL")) return "RAIADROGASIL S.A.";
            if (texto.Contains("WMS SUPERMERCADOS")) return "WMS SUPERMERCADOS (SONDA/WALMART)";
            if (texto.Contains("SENDAS")) return "SENDAS DISTRIBUIDORA (ASSAI)";

            var matchCnpj = Regex.Match(texto, @"([A-Z\s\.]{5,80}?)\s*CNPJ:", RegexOptions.IgnoreCase);
            return matchCnpj.Success ? matchCnpj.Groups[1].Value.Trim() : "";
        }

        private string ExtrairEndereco(string texto)
        {
            var match = Regex.Match(texto, @"(?<tipo>RUA|AV|AVENIDA|ALAMEDA)\s+(?<nome>[^,]+),\s*(?<num>\d+)", RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups["tipo"].Value} {match.Groups["nome"].Value}, {match.Groups["num"].Value}" : "";
        }

        // ==================== PROCESSAMENTO DOS ITENS ====================

        private List<ItemLancamento> ProcessarItensDoPdf(string textoUmaLinha, string descricao)
        {
            if (descricao.Contains("WMS")) return ProcessarItensWms(textoUmaLinha);
            if (textoUmaLinha.Contains("BWELL") || textoUmaLinha.Contains("DROGA")) return ProcessarItensRaia(textoUmaLinha);
            return ProcessarItensGenerico(textoUmaLinha);
        }

        private List<ItemLancamento> ProcessarItensWms(string texto)
        {
            var itens = new List<ItemLancamento>();
            var pattern = new Regex(@"(?<nome>[A-Z][A-Z\s\.]+?)\s*\(Código:.*?\)\s*Qtde\.:\s*(?<qtd>[\d\.,]+).*?Unit\.:\s*(?<preco>\d+,\d{2})", RegexOptions.IgnoreCase);
            foreach (Match m in pattern.Matches(texto))
            {
                itens.Add(new ItemLancamento
                {
                    Descricao = m.Groups["nome"].Value.Trim(),
                    Quantidade = (double)ConverterParaDecimal(m.Groups["qtd"].Value, _culturaBr),
                    Preco = ConverterParaDecimal(m.Groups["preco"].Value, _culturaBr),
                    Comprado = true
                });
            }
            return itens;
        }

        private List<ItemLancamento> ProcessarItensRaia(string texto)
        {
            var itens = new List<ItemLancamento>();
            var pattern = new Regex(@"(?<nome>[A-Z\d\s\-]{3,40})\s*\(Código:\s*\d+\)\s*Vl\.\s*Unit\.:\s*(?<preco>\d+,\d{2})\s*Qtde\.:(?<qtd>\d+)", RegexOptions.IgnoreCase);
            foreach (Match m in pattern.Matches(texto))
            {
                itens.Add(new ItemLancamento
                {
                    Descricao = m.Groups["nome"].Value.Trim(),
                    Quantidade = (double)ConverterParaDecimal(m.Groups["qtd"].Value, _culturaBr),
                    Preco = ConverterParaDecimal(m.Groups["preco"].Value, _culturaBr),
                    Comprado = true
                });
            }
            return itens;
        }

        private List<ItemLancamento> ProcessarItensGenerico(string texto)
        {
            var itens = new List<ItemLancamento>();
            var pattern = new Regex(@"(?<nome>[A-Z\s]{3,}).*?Qtde\.:\s*(?<qtd>[\d\.,]+).*?Unit\.:\s*(?<preco>\d+,\d{2})", RegexOptions.IgnoreCase);
            foreach (Match m in pattern.Matches(texto))
            {
                itens.Add(new ItemLancamento
                {
                    Descricao = m.Groups["nome"].Value.Trim(),
                    Quantidade = (double)ConverterParaDecimal(m.Groups["qtd"].Value, _culturaBr),
                    Preco = ConverterParaDecimal(m.Groups["preco"].Value, _culturaBr),
                    Comprado = true
                });
            }
            return itens;
        }

        private decimal ConverterParaDecimal(string valor, CultureInfo cultura)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0;
            string limpo = Regex.Replace(valor, @"[^\d,]", "");
            return decimal.TryParse(limpo, NumberStyles.Any, cultura, out decimal res) ? res : 0;
        }

        private Lancamento CriarLancamentoBase(string userId)
        {
            return new Lancamento
            {
                UsuarioId = userId,
                Tipo = "despesa",
                CategoriaId = 1,
                Itens = new List<ItemLancamento>()
            };
        }
    }
}