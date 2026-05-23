using Financeiro.Api.Models;

namespace FinanceiroApi.Models
{
    public class ItemCompra
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int Quantidade { get; set; }

        public string Unidade { get; set; } = "un";
        public decimal Preco { get; set; }
        public bool Comprado { get; set; }

        public string? Categoria { get; set; }

        // Relacionamento: cada item pertence a um Lançamento (Compra)
        public int LancamentoId { get; set; }

        public virtual ICollection<ItemLancamento> Itens { get; set; } = new List<ItemLancamento>();
    }
}
