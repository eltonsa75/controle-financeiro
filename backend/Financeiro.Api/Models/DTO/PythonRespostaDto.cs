using System.Text.Json.Serialization;

namespace Financeiro.Api.Models.DTO
{
    public class ItemPythonDto
    {
        [JsonPropertyName("descricao")]
        public string Descricao { get; set; }

        [JsonPropertyName("quantidade")]
        public double Quantidade { get; set; }

        [JsonPropertyName("preco")]
        public decimal Preco { get; set; }

        [JsonPropertyName("categoria")]
        public string Categoria { get; set; }
    }

    public class PythonRespostaDto
    {
        [JsonPropertyName("estabelecimento")]
        public string Estabelecimento { get; set; }

        [JsonPropertyName("categoria_principal")]
        public string CategoriaPrincipal { get; set; }

        [JsonPropertyName("dataEmissao")]
        public string DataEmissao { get; set; }

        [JsonPropertyName("subtotal")]
        public decimal? Subtotal { get; set; }

        [JsonPropertyName("desconto")]
        public decimal? Desconto { get; set; }

        [JsonPropertyName("valorPago")]
        public decimal? ValorPago { get; set; }

        [JsonPropertyName("textoBruto")]
        public string TextoBruto { get; set; }

        [JsonPropertyName("itens")]
        public List<ItemPythonDto> Itens { get; set; }
    }
}