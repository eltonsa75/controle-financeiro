using Financeiro.Api.Models;
using FinanceiroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceiroApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Lancamento> Lancamentos { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<ItemLancamento> ItensLancamento { get; set; }
        public DbSet<Estoque> Estoques { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configuração de Categoria
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nome).IsRequired().HasMaxLength(100);

                entity.Property(c => c.UsuarioId)
                    .HasMaxLength(128)
                    .IsRequired();
            });

            // 2. Configuração de Lancamento
            modelBuilder.Entity<Lancamento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(l => l.Valor).HasPrecision(18, 2);

                // --- NOVAS COLUNAS DE DATA ---
                // Data que consta na nota fiscal (essencial para o Dashboard)
                entity.Property(l => l.DataEmissao)
                    .IsRequired();

                // Data de quando o registro foi criado no sistema
                entity.Property(l => l.DataImportacao)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
                // -----------------------------

                entity.Property(l => l.UsuarioId)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.HasOne(d => d.Categoria)
                    .WithMany(p => p.Lancamentos)
                    .HasForeignKey(d => d.CategoriaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 3. Configuração de ItemLancamento
            modelBuilder.Entity<ItemLancamento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Preco).HasPrecision(18, 2);

                entity.Property(e => e.Categoria)
                    .IsRequired(false)
                    .HasMaxLength(100)
                    .HasDefaultValue("Geral");

                entity.Property(e => e.Unidade)
                    .IsRequired(false)
                    .HasMaxLength(50)
                    .HasDefaultValue("un");

                entity.Property(e => e.Descricao)
                    .IsRequired(false)
                    .HasMaxLength(255);

                entity.HasOne(i => i.Lancamento)
                    .WithMany(l => l.Itens)
                    .HasForeignKey(i => i.LancamentoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}