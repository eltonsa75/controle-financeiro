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

        public DbSet<EstoqueMovimentacao> EstoqueMovimentacoes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configuração de Categoria
            // 1. Configuração de Categoria
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nome).IsRequired().HasMaxLength(100);
                entity.Property(c => c.UsuarioId).HasMaxLength(128).IsRequired();

                entity.HasMany(c => c.Lancamentos)
                      .WithOne(l => l.CategoriaRef)
                      .HasForeignKey(l => l.CategoriaId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 2. Configuração de Lancamento
            modelBuilder.Entity<Lancamento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(l => l.Valor).HasPrecision(18, 2);
                entity.Property(l => l.Subtotal).HasPrecision(18, 2);
                entity.Property(l => l.Desconto).HasPrecision(18, 2);
                entity.Property(l => l.DataEmissao).IsRequired();
                entity.Property(l => l.DataImportacao).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
                entity.Property(l => l.UsuarioId).HasMaxLength(128).IsRequired();

                entity.HasOne(l => l.CategoriaRef)
                      .WithMany(c => c.Lancamentos)
                      .HasForeignKey(l => l.CategoriaId)
                      .IsRequired(false) // <-- ESSENCIAL PARA EVITAR O ERRO DE SEVERED
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 3. Configuração de ItemLancamento
            modelBuilder.Entity<ItemLancamento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Preco).HasPrecision(18, 2);
                entity.Property(e => e.Categoria).IsRequired(false).HasMaxLength(100).HasDefaultValue("Geral");
                entity.Property(e => e.Unidade).IsRequired(false).HasMaxLength(50).HasDefaultValue("un");
                entity.Property(e => e.Descricao).IsRequired(false).HasMaxLength(255);
                entity.Property(e => e.UsuarioId).IsRequired().HasMaxLength(128);

                entity.HasOne(i => i.CategoriaRef)
                      .WithMany()
                      .HasForeignKey(i => i.CategoriaId)
                      .IsRequired(false) // <-- ESSENCIAL
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Lancamento)
                      .WithMany(l => l.Itens)
                      .HasForeignKey(i => i.LancamentoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            // 4. Configuração de EstoqueMovimentacao
            modelBuilder.Entity<EstoqueMovimentacao>(entity =>
            {
                entity.ToTable("EstoqueMovimentacoes");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.UsuarioId)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(e => e.Tipo)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(e => e.Origem)
                      .IsRequired()
                      .HasMaxLength(30);

                entity.Property(e => e.Quantidade)
                      .HasPrecision(10, 2);

                entity.Property(e => e.QuantidadeAnterior)
                      .HasPrecision(10, 2);

                entity.Property(e => e.QuantidadePosterior)
                      .HasPrecision(10, 2);

                entity.Property(e => e.DataMovimentacao)
                      .IsRequired();

                entity.Property(e => e.Observacao)
                      .HasMaxLength(500);

                // Cada movimentação pertence a um produto
                entity.HasOne(e => e.Estoque)
                      .WithMany()
                      .HasForeignKey(e => e.EstoqueId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Índices para melhorar as consultas do histórico
                entity.HasIndex(e => e.EstoqueId);

                entity.HasIndex(e => new
                {
                    e.UsuarioId,
                    e.DataMovimentacao
                });
            });

        }
    }
}