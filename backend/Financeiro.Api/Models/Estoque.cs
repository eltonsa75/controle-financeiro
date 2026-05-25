using Financeiro.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceiroApi.Models
{
    [Table("Estoque")]
    public class Estoque
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantidadeAtual { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantidadeMinima { get; set; }

        [Required]
        [StringLength(10)]
        public string UnidadeMedida { get; set; } = "un"; // un, kg, lt, pct

        [Required]
        public int CategoriaId { get; set; }

        // Propriedade de Navegação do EF Core
        [ForeignKey("CategoriaId")]
        public Categoria? Categoria { get; set; }

        [Required]
        [StringLength(255)]
        public string UsuarioId { get; set; } = string.Empty; // Isolamento por conta do Firebase
    }
}