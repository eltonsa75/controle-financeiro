using Dapper;
using Financeiro.Api.Models.DTO;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Data;

namespace Financeiro.Api.Repositories
{
    public class FinanceiroRepository
    {
        private readonly string _connectionString;

        public FinanceiroRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                                ?? "Server=mysql-db;Database=comprasintelig;Uid=root;Pwd=sua_senha;";
        }

        private IDbConnection Connection => new MySqlConnection(_connectionString);

        public async Task<dynamic> ObterResumoMensal()
        {
            using (var conn = Connection)
            {
                const string sql = @"
                SELECT
                    COALESCE(SUM(CASE WHEN LOWER(TRIM(Tipo)) = 'receita' THEN Valor ELSE 0 END), 0) as receitas,
                    COALESCE(SUM(CASE WHEN LOWER(TRIM(Tipo)) = 'despesa' THEN Valor ELSE 0 END), 0) as despesas
                FROM Lancamentos
                WHERE MONTH(DataEmissao) = MONTH(CURDATE())
                  AND YEAR(DataEmissao) = YEAR(CURDATE())";

                return await conn.QueryFirstOrDefaultAsync(sql);
            }
        }

        // Distribuição por categoria: cada lançamento contribui uma única vez.
        // Para notas com itens, rateia o valor pago (incluindo descontos)
        // proporcionalmente ao valor bruto de cada item.
        public async Task<IEnumerable<GastosCategoriaDTO>> ObterGastosPorCategoria(
            string usuarioId,
            int mes,
            int ano)
        {
            using var conn = Connection;

            const string sql = @"
                SELECT
                    Dados.Categoria,
                    ROUND(SUM(Dados.Valor), 2) AS Valor
                FROM (
                    -- Lançamentos com itens: rateio proporcional do valor pago.
                    SELECT
                        COALESCE(
                            NULLIF(TRIM(cItem.Nome), ''),
                            NULLIF(TRIM(i.Categoria), ''),
                            'Geral'
                        ) AS Categoria,
                        CASE
                            WHEN totais.TotalBruto > 0 THEN
                                ABS(l.Valor) * ABS(i.Preco * i.Quantidade) / totais.TotalBruto
                            ELSE 0
                        END AS Valor
                    FROM ItensLancamento i
                    INNER JOIN Lancamentos l ON l.Id = i.LancamentoId
                    INNER JOIN (
                        SELECT LancamentoId,
                               SUM(ABS(Preco * Quantidade)) AS TotalBruto
                        FROM ItensLancamento
                        GROUP BY LancamentoId
                    ) totais ON totais.LancamentoId = l.Id
                    LEFT JOIN Categorias cItem
                        ON cItem.Id = i.CategoriaId
                       AND cItem.UsuarioId = l.UsuarioId
                    WHERE l.DataEmissao >= @Inicio
                      AND l.DataEmissao < @Fim
                      AND LOWER(TRIM(l.Tipo)) = 'despesa'
                      AND l.UsuarioId = @UsuarioId

                    UNION ALL

                    -- Lançamentos sem itens: valor total na categoria principal.
                    SELECT
                        COALESCE(NULLIF(TRIM(c.Nome), ''), 'Geral') AS Categoria,
                        ABS(l.Valor) AS Valor
                    FROM Lancamentos l
                    LEFT JOIN Categorias c
                        ON c.Id = l.CategoriaId
                       AND c.UsuarioId = l.UsuarioId
                    WHERE l.DataEmissao >= @Inicio
                      AND l.DataEmissao < @Fim
                      AND LOWER(TRIM(l.Tipo)) = 'despesa'
                      AND l.UsuarioId = @UsuarioId
                      AND NOT EXISTS (
                          SELECT 1
                          FROM ItensLancamento i2
                          WHERE i2.LancamentoId = l.Id
                      )
                ) Dados
                GROUP BY Dados.Categoria
                HAVING SUM(Dados.Valor) > 0
                ORDER BY Valor DESC;";

            var inicio = new DateTime(ano, mes, 1);
            var fim = inicio.AddMonths(1);

            return await conn.QueryAsync<GastosCategoriaDTO>(sql, new
            {
                UsuarioId = usuarioId,
                Inicio = inicio,
                Fim = fim
            });
        }

        public async Task<IEnumerable<GastosAnuaisCategoriaDTO>> ObterGastosAnuaisComMetas(string usuarioId)
        {
            using var conn = Connection;

            const string sql = @"
        SELECT
            Dados.Categoria,
            SUM(Dados.Valor) AS ValorGastoAno,
            MAX(Dados.MetaMensalBase) AS MetaMensalBase
        FROM
        (
            -- =====================================================
            -- 1. ITENS DAS NOTAS IMPORTADAS
            -- =====================================================
            SELECT
                CASE
                    WHEN LOWER(TRIM(i.Categoria)) = 'mercearia'
                        THEN 'Mercearia'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('laticínios e frios', 'laticinios e frios', 'laticínios', 'laticinios')
                        THEN 'Laticínios e Frios'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('snacks e doces', 'snacks', 'doces')
                        THEN 'Snacks e Doces'

                    WHEN LOWER(TRIM(i.Categoria)) = 'padaria'
                        THEN 'Padaria'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('açougue', 'acougue')
                        THEN 'Açougue'

                    WHEN LOWER(TRIM(i.Categoria)) = 'bebidas'
                        THEN 'Bebidas'

                    WHEN LOWER(TRIM(i.Categoria)) = 'limpeza'
                        THEN 'Limpeza'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('higiene e saúde', 'higiene e saude', 'higiene')
                        THEN 'Higiene e Saúde'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('farmácia', 'farmacia')
                        THEN 'Farmácia'

                    WHEN LOWER(TRIM(i.Categoria)) = 'hortifruti'
                        THEN 'Hortifruti'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('pet shop', 'petshop')
                        THEN 'Pet Shop'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('automóvel', 'automovel', 'estacionamento', 'transporte')
                        THEN 'Automóvel'

                    WHEN LOWER(TRIM(i.Categoria)) = 'suplementos'
                        THEN 'Suplementos'

                    WHEN LOWER(TRIM(i.Categoria)) = 'lazer'
                        THEN 'Lazer'

                    WHEN LOWER(TRIM(i.Categoria)) IN
                        ('educação', 'educacao')
                        THEN 'Educação'

                    WHEN LOWER(TRIM(i.Categoria)) = 'roupas'
                        THEN 'Roupas'

                    ELSE 'Geral'
                END AS Categoria,

                ABS(i.Preco * i.Quantidade) AS Valor,

                l.UsuarioId,

                COALESCE(cItem.MetaMensal, 0) AS MetaMensalBase

            FROM ItensLancamento i

            INNER JOIN Lancamentos l
                ON i.LancamentoId = l.Id

            LEFT JOIN Categorias cItem
                ON LOWER(TRIM(cItem.Nome)) =
                   LOWER(TRIM(i.Categoria))
                AND cItem.UsuarioId = l.UsuarioId

            WHERE
                YEAR(l.DataEmissao) = YEAR(CURDATE())
                AND LOWER(TRIM(l.Tipo)) = 'despesa'
                AND l.UsuarioId = @UsuarioId

            UNION ALL

            -- =====================================================
            -- 2. LANÇAMENTOS MANUAIS SEM ITENS
            -- =====================================================
            SELECT
                CASE
                    WHEN LOWER(TRIM(c.Nome)) IN
                        ('automóvel', 'automovel', 'transporte')
                        THEN 'Automóvel'

                    WHEN LOWER(TRIM(c.Nome)) = 'estacionamento'
                        THEN 'Estacionamento'

                    WHEN LOWER(TRIM(c.Nome)) IN
                        ('açougue', 'acougue')
                        THEN 'Açougue'

                    WHEN LOWER(TRIM(c.Nome)) IN
                        ('educação', 'educacao')
                        THEN 'Educação'

                    WHEN LOWER(TRIM(c.Nome)) IN
                        ('farmácia', 'farmacia')
                        THEN 'Farmácia'

                    WHEN LOWER(TRIM(c.Nome)) IN
                        ('higiene e saúde', 'higiene e saude', 'higiene')
                        THEN 'Higiene e Saúde'

                    ELSE c.Nome
                END AS Categoria,

                ABS(l.Valor) AS Valor,

                l.UsuarioId,

                COALESCE(c.MetaMensal, 0) AS MetaMensalBase

            FROM Lancamentos l

            INNER JOIN Categorias c
                ON l.CategoriaId = c.Id

            WHERE
                YEAR(l.DataEmissao) = YEAR(CURDATE())
                AND LOWER(TRIM(l.Tipo)) = 'despesa'
                AND l.UsuarioId = @UsuarioId

                AND NOT EXISTS
                (
                    SELECT 1
                    FROM ItensLancamento i2
                    WHERE i2.LancamentoId = l.Id
                )

        ) AS Dados

        GROUP BY Dados.Categoria

        ORDER BY ValorGastoAno DESC;
    ";

            return await conn.QueryAsync<GastosAnuaisCategoriaDTO>(
                sql,
                new
                {
                    UsuarioId = usuarioId
                }
            );
        }
    }
}