using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDatasControle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🎯 ALTERADO: Tipo mudou de "datetime(6)" para "timestamp"
            // Isso força o MySQL a respeitar o fuso local da aplicação em tempo de gravação
            migrationBuilder.AddColumn<DateTime>(
                name: "DataEmissao",
                table: "Lancamentos",
                type: "timestamp",
                nullable: false,
                defaultValue: DateTime.MinValue);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataImportacao",
                table: "Lancamentos",
                type: "timestamp",
                nullable: false,
                defaultValue: DateTime.MinValue);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataEmissao",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "DataImportacao",
                table: "Lancamentos");
        }
    }
}