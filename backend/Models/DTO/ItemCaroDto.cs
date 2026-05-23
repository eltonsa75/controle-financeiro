namespace Financeiro.Api.Models.DTO
{
    public class ItemCaroDto
    {
        public string Descricao { get; set; }
        public decimal ValorTotal { get; set; }

        // Adicione esta linha:
        public string Local { get; set; }
    }
}
