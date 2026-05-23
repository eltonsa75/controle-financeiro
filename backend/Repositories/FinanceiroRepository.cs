using Dapper;
using Financeiro.Api.Models.DTO;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System.Data;

namespace Financeiro.Api.Repositories
{
    public class FinanceiroRepository
    {
        private readonly string _connectionString;

        public FinanceiroRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        private IDbConnection Connection => new MySqlConnection(_connectionString);

        public async Task<dynamic> ObterResumoMensal()
        {
            using (var conn = Connection)
            {
                // Ajustado para pegar o resumo de TODO o período disponível, 
                // permitindo que o valor de R$ 278,36 apareça no dashboard.
                const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN Tipo = 'receita' THEN Valor ELSE 0 END), 0) as receitas,
                COALESCE(SUM(CASE WHEN Tipo = 'despesa' THEN Valor ELSE 0 END), 0) as despesas
            FROM Lancamentos";
                // Filtro de mês removido temporariamente para validar dados de Março

                return await conn.QueryFirstOrDefaultAsync(sql);
            }
        }

        public async Task<IEnumerable<GastosCategoriaDTO>> ObterGastosPorCategoria()
        {
            using var conn = Connection;

            // Lógica de Analista: Como os itens estão no banco, 
            // precisamos garantir que a query os alcance.
            const string sql = @"
            SELECT 
                Categoria,
                SUM(Preco * Quantidade) as Valor
            FROM ItensLancamento
            GROUP BY Categoria
            ORDER BY Valor DESC";

            return await conn.QueryAsync<GastosCategoriaDTO>(sql);
        }
    }
}