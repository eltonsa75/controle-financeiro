using Financeiro.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceiroApi.Models
{
    public class ItemCompra
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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
