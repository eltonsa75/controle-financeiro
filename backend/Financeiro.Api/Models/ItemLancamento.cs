using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Financeiro.Api.Models
{
    public class ItemLancamento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Descricao { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public double Quantidade { get; set; }

        public string Categoria { get; set; } = "Geral";
        public string Unidade { get; set; } = "un";
        public bool Comprado { get; set; }

        [Column("CategoriaId")]
        public int CategoriaId { get; set; }

        [ForeignKey("Lancamento")]
        public int LancamentoId { get; set; }

        public string? UsuarioId { get; set; }

        // Mantemos a navegação apenas para leitura futura, mas sem forçar rastreamento na escrita
        [JsonIgnore]
        [ForeignKey("CategoriaId")]
        public virtual Categoria? CategoriaRef { get; set; }

        [JsonIgnore]
        public virtual Lancamento? Lancamento { get; set; }
    }
}