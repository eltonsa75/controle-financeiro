using System.ComponentModel.DataAnnotations;

namespace Financeiro.Api.Models.DTO
{
    public class EstoqueDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        public string Nome { get; set; } = string.Empty;

        public decimal QuantidadeAtual { get; set; }

        public decimal QuantidadeMinima { get; set; }

        public string UnidadeMedida { get; set; } = "un";

        [Required(ErrorMessage = "A categoria é obrigatória.")]
        public int CategoriaId { get; set; }
    }
}
