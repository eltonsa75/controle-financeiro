using System.Text.Json.Serialization;

namespace Financeiro.Api.Models
{
    public class ItemLancamento
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public double Quantidade { get; set; }
        public string Categoria { get; set; } = "Geral"; // Valor garantido no C#
        public string Unidade { get; set; } = "un";      // Valor garantido no C#
        public bool Comprado { get; set; }
        public int LancamentoId { get; set; }

        public string? UsuarioId { get; set; }
        [JsonIgnore]
        public virtual Lancamento? Lancamento { get; set; }
    }
}