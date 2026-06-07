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

        public async Task<IEnumerable<GastosCategoriaDTO>> ObterGastosPorCategoria(string usuarioId)
        {
            using var conn = Connection;

            // 🎯 ARQUITETURA UNIFICADA MENSAL: Itens de Notas + Lançamentos Manuais Diretos
            const string sql = @"
            SELECT 
                Dados.Categoria,
                SUM(Dados.Valor) as Valor
            FROM (
                -- 1. BUSCA ITENS GRANULARES (De Notas Fiscais processadas no mês atual)
                SELECT 
                    CASE 
                        WHEN LOWER(i.Categoria) IN ('mercearia') THEN 'Mercearia'
                        WHEN LOWER(i.Categoria) IN ('laticínios e frios', 'laticinios e frios') THEN 'Laticínios e Frios'
                        WHEN LOWER(i.Categoria) IN ('snacks e doces') THEN 'Snacks e Doces'
                        WHEN LOWER(i.Categoria) IN ('padaria') THEN 'Padaria'
                        WHEN LOWER(i.Categoria) IN ('acougue', 'açougue') THEN 'Açougue'
                        WHEN LOWER(i.Categoria) IN ('bebidas') THEN 'Bebidas'
                        WHEN LOWER(i.Categoria) IN ('limpeza') THEN 'Limpeza'
                        WHEN LOWER(i.Categoria) IN ('higiene e saúde', 'higiene e saude', 'higiene') THEN 'Higiene'
                        WHEN LOWER(i.Categoria) IN ('farmácia', 'farmacia') THEN 'Farmácia'
                        WHEN LOWER(i.Categoria) IN ('hortifruti') THEN 'Hortifruti'
                        WHEN LOWER(i.Categoria) IN ('pet shop', 'petshop') THEN 'Pet Shop'
                        WHEN LOWER(i.Categoria) IN ('automóvel', 'automovel', 'estacionamento', 'transporte') THEN 'Automóvel'
                        WHEN LOWER(i.Categoria) IN ('suplementos') THEN 'Suplementos'
                        WHEN LOWER(i.Categoria) IN ('lazer') THEN 'Lazer'
                        WHEN LOWER(i.Categoria) IN ('educação', 'educacao') THEN 'Educação'
                        WHEN LOWER(i.Categoria) IN ('roupas') THEN 'Roupas'
                        ELSE 'Geral'
                    END as Categoria,
                    (i.Preco * i.Quantidade) as Valor,
                    l_filho.UsuarioId
                FROM ItensLancamento i
                INNER JOIN lancamentos l_filho ON i.LancamentoId = l_filho.Id
                WHERE MONTH(l_filho.DataEmissao) = MONTH(CURDATE())
                  AND YEAR(l_filho.DataEmissao) = YEAR(CURDATE())
                  AND LOWER(TRIM(l_filho.Tipo)) = 'despesa'
                  AND LOWER(i.Categoria) NOT IN ('supermercado')

                UNION ALL

                -- 2. BUSCA LANÇAMENTOS DIRETOS (Manuais como Açougue, Educação, Estacionamento ou Notas Fechadas)
                SELECT 
                    CASE 
                        WHEN LOWER(TRIM(c_pai.Nome)) IN ('automóvel', 'automovel', 'estacionamento', 'transporte') THEN 'Automóvel'
        
        -- 🎯 Força o mapeamento correto caso haja divergência de escrita no banco
        WHEN LOWER(TRIM(c_pai.Nome)) IN ('açougue', 'acougue') THEN 'Açougue'
        WHEN LOWER(TRIM(c_pai.Nome)) IN ('educação', 'educacao') THEN 'Educação'
        WHEN LOWER(TRIM(c_pai.Nome)) IN ('farmácia', 'farmacia') THEN 'Farmácia'
                   ELSE c_pai.Nome
    END as Categoria,
    l.Valor as Valor,
    l.UsuarioId
FROM lancamentos l
INNER JOIN categorias c_pai ON l.CategoriaId = c_pai.Id
WHERE MONTH(l.DataEmissao) = MONTH(CURDATE())
  AND YEAR(l.DataEmissao) = YEAR(CURDATE())
  AND LOWER(TRIM(l.Tipo)) = 'despesa'
  AND (l.Id NOT IN (SELECT DISTINCT LancamentoId FROM ItensLancamento) OR LOWER(c_pai.Nome) = 'supermercado')
            ) AS Dados
            INNER JOIN categorias c ON LOWER(TRIM(c.Nome)) = LOWER(TRIM(Dados.Categoria)) AND c.UsuarioId = Dados.UsuarioId
            WHERE Dados.UsuarioId = @UsuarioId
            GROUP BY Dados.Categoria
            ORDER BY Valor DESC";

            return await conn.QueryAsync<GastosCategoriaDTO>(sql, new { UsuarioId = usuarioId });
        }

        public async Task<IEnumerable<GastosAnuaisCategoriaDTO>> ObterGastosAnuaisComMetas(string usuarioId)
        {
            using var conn = Connection;

            // 🎯 ARQUITETURA UNIFICADA ANUAL: Higienizada e padronizada com strings 'despesa'
            const string sql = @"
            SELECT 
                Dados.Categoria,
                SUM(Dados.Valor) as ValorGastoAno,
                MAX(c.MetaMensal) as MetaMensalBase
            FROM (
                -- 1. BUSCA ITENS GRANULARES (Notas Fiscais do ano atual)
                SELECT 
                    CASE 
                        WHEN LOWER(i.Categoria) IN ('mercearia') THEN 'Mercearia'
                        WHEN LOWER(i.Categoria) IN ('laticínios e frios', 'laticinios e frios') THEN 'Laticínios e Frios'
                        WHEN LOWER(i.Categoria) IN ('snacks e doces') THEN 'Snacks e Doces'
                        WHEN LOWER(i.Categoria) IN ('padaria') THEN 'Padaria'
                        WHEN LOWER(i.Categoria) IN ('acougue', 'açougue') THEN 'Açougue'
                        WHEN LOWER(i.Categoria) IN ('bebidas') THEN 'Bebidas'
                        WHEN LOWER(i.Categoria) IN ('limpeza') THEN 'Limpeza'
                        WHEN LOWER(i.Categoria) IN ('higiene e saúde', 'higiene e saude', 'higiene') THEN 'Higiene'
                        WHEN LOWER(i.Categoria) IN ('farmácia', 'farmacia') THEN 'Farmácia'
                        WHEN LOWER(i.Categoria) IN ('hortifruti') THEN 'Hortifruti'
                        WHEN LOWER(i.Categoria) IN ('pet shop', 'petshop') THEN 'Pet Shop'
                        WHEN LOWER(i.Categoria) IN ('automóvel', 'automovel', 'estacionamento', 'transporte') THEN 'Automóvel'
                        WHEN LOWER(i.Categoria) IN ('suplementos') THEN 'Suplementos'
                        WHEN LOWER(i.Categoria) IN ('lazer') THEN 'Lazer'
                        WHEN LOWER(i.Categoria) IN ('educação', 'educacao') THEN 'Educação'
                        WHEN LOWER(i.Categoria) IN ('roupas') THEN 'Roupas'
                        ELSE 'Geral'
                    END as Categoria,
                    (i.Preco * i.Quantidade) as Valor,
                    l_filho.UsuarioId
                FROM ItensLancamento i
                INNER JOIN lancamentos l_filho ON i.LancamentoId = l_filho.Id
                WHERE YEAR(l_filho.DataEmissao) = YEAR(CURDATE())
                  AND LOWER(TRIM(l_filho.Tipo)) = 'despesa'
                  AND LOWER(i.Categoria) NOT IN ('supermercado')

                UNION ALL

                -- 2. BUSCA LANÇAMENTOS DIRETOS DA TABELA PAI (Manuais ou Notas de Supermercado fechadas)
                SELECT 
                    CASE 
                        WHEN LOWER(c_pai.Nome) IN ('automóvel', 'automovel', 'estacionamento', 'transporte') THEN 'Automóvel'
                        ELSE c_pai.Nome
                    END as Categoria,
                    l.Valor as Valor,
                    l.UsuarioId
                FROM lancamentos l
                INNER JOIN categorias c_pai ON l.CategoriaId = c_pai.Id
                WHERE YEAR(l.DataEmissao) = YEAR(CURDATE())
                  AND LOWER(TRIM(l.Tipo)) = 'despesa'
                  AND (l.Id NOT IN (SELECT DISTINCT LancamentoId FROM ItensLancamento) OR LOWER(c_pai.Nome) = 'supermercado')
            ) AS Dados
            INNER JOIN categorias c ON LOWER(TRIM(c.Nome)) = LOWER(TRIM(Dados.Categoria)) AND c.UsuarioId = Dados.UsuarioId
            WHERE Dados.UsuarioId = @UsuarioId
            GROUP BY Dados.Categoria
            ORDER BY ValorGastoAno DESC";

            return await conn.QueryAsync<GastosAnuaisCategoriaDTO>(sql, new { UsuarioId = usuarioId });
        }
    }
}