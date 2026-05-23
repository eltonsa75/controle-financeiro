namespace Financeiro.Api.Models
{
    public class Lancamento
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string Tipo { get; set; } = "Despesa"; // Receita ou Despesa

        // Coluna antiga (podes manter para compatibilidade ou remover após migration)
        public DateTime Data { get; set; }

        // NOVAS COLUNAS CRÍTICAS
        public DateTime DataEmissao { get; set; } // O gráfico vai ler daqui
        public DateTime DataImportacao { get; set; } = DateTime.Now; // Log de quando entrou no banco

        public string? UsuarioId { get; set; }
        public int CategoriaId { get; set; }
        public virtual Categoria? Categoria { get; set; }

        public virtual ICollection<ItemLancamento> Itens { get; set; } = new List<ItemLancamento>();
    }
}