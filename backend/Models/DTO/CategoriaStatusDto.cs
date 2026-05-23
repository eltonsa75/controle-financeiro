namespace Financeiro.Api.Models.DTO
{
    public class CategoriaStatusDto
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public decimal Meta { get; set; }
        public decimal GastoAtual { get; set; }
        public decimal Percentual => Meta > 0 ? (GastoAtual / Meta) * 100 : 0;
    }
}
