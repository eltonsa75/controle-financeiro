using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceiroApi.Models
{
    [Table("EstoqueMovimentacoes")]
    public class EstoqueMovimentacao
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int EstoqueId { get; set; }

        [ForeignKey(nameof(EstoqueId))]
        public Estoque? Estoque { get; set; }

        public int? LancamentoId { get; set; }

        [Required]
        [StringLength(255)]
        public string UsuarioId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Tipo { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Origem { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantidade { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantidadeAnterior { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantidadePosterior { get; set; }

        public DateTime DataMovimentacao { get; set; }
            = DateTime.UtcNow;

        [StringLength(500)]
        public string? Observacao { get; set; }
    }
}
