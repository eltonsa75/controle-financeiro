using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financeiro.Api.Models
{
    public class Lancamento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }

        public decimal? Subtotal { get; set; }

        public decimal? Desconto { get; set; }
        public string Tipo { get; set; } = "Despesa";

        public DateTime Data { get; set; }
        public DateTime DataEmissao { get; set; }
        public DateTime DataImportacao { get; set; } = DateTime.Now;

        public string? UsuarioId { get; set; }

        [Column("CategoriaID")] // Força o EF a procurar pelo nome exato do banco
        public int CategoriaId { get; set; }

        [ForeignKey("CategoriaId")] // <--- Adicione este atributo
        public virtual Categoria? CategoriaRef { get; set; }

        public virtual ICollection<ItemLancamento> Itens { get; set; } = new List<ItemLancamento>();
    }
}