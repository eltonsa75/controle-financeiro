using FinanceiroApi.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financeiro.Api.Models
{
    public class Categoria
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal MetaMensal { get; set; }
        public string? CorHex { get; set; }
        public string? PalavrasChave { get; set; } // Usado para a inteligência de importação
        public string? UsuarioId { get; set; }

        public int Ordem { get; set; } = 0;

        // Relacionamento: Uma categoria pode ter vários lançamentos
        [System.Text.Json.Serialization.JsonIgnore]
        public virtual ICollection<Lancamento>? Lancamentos { get; set; }
    }
}
