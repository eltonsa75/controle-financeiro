using FinanceiroApi.Models;

namespace Financeiro.Api.Models
{
    public class Categoria
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal MetaMensal { get; set; }
        public string? CorHex { get; set; }
        public string? PalavrasChave { get; set; } // Usado para a inteligência de importação
        public string? UsuarioId { get; set; }

        // Relacionamento: Uma categoria pode ter vários lançamentos
        [System.Text.Json.Serialization.JsonIgnore]
        public virtual ICollection<Lancamento>? Lancamentos { get; set; }
    }
}
