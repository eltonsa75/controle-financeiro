namespace Financeiro.Api.Models.DTO
{
    public class EvolucaoMensalDto
    {
        public int Mes { get; set; }
        public int Ano { get; set; }
        public decimal TotalDespesas { get; set; }
        public decimal TotalReceitas { get; set; }
    }
}
