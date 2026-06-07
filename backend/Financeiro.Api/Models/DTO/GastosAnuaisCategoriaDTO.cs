namespace Financeiro.Api.Models.DTO
{
    public class GastosAnuaisCategoriaDTO
    {
        public string Categoria { get; set; }
        public decimal ValorGastoAno { get; set; }
        public decimal MetaMensalBase { get; set; }

        // 🎯 A Opção A ganha vida aqui: Cálculo dinâmico direto na propriedade do objeto
        public decimal MetaAnualCalculada => MetaMensalBase * 12;

        // Porcentagem de estouro ou progresso anual para a barra do Angular
        public int PorcentagemAno => MetaAnualCalculada > 0
            ? (int)Math.Round((ValorGastoAno / MetaAnualCalculada) * 100)
            : 0;
    }
}
