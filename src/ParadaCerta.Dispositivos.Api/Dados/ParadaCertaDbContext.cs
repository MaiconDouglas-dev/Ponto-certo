using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ParadaCerta.Dispositivos.Api.Dominio;

namespace ParadaCerta.Dispositivos.Api.Dados;

/// <summary>
/// Mapeia só as tabelas que a API Dispositivos usa. O banco é criado pelo script DDL
/// (db/scripts/01-ddl.sql), por isso não usamos migrations.
/// Os nomes estão em MAIÚSCULAS porque o Oracle guarda assim identificadores sem aspas.
/// </summary>
public class ParadaCertaDbContext(DbContextOptions<ParadaCertaDbContext> options) : DbContext(options)
{
    public DbSet<Parada> Paradas => Set<Parada>();
    public DbSet<Totem> Totens => Set<Totem>();
    public DbSet<SinalTotem> Sinais => Set<SinalTotem>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <summary>
    /// As colunas TIMESTAMP não guardam fuso: o Oracle devolve DateTime com Kind=Unspecified, que sai no JSON
    /// sem o "Z" e é lido como horário local pelo cliente. Toda data é gravada em UTC, então marcamos como UTC na leitura.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<DataUtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<DataUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Parada>(e =>
        {
            e.ToTable("PARADA");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(p => p.CodigoSptrans).HasColumnName("CODIGO_SPTRANS");
            e.Property(p => p.Nome).HasColumnName("NOME").HasMaxLength(150);
            e.Property(p => p.Endereco).HasColumnName("ENDERECO").HasMaxLength(250);
            e.Property(p => p.Latitude).HasColumnName("LATITUDE").HasPrecision(9, 6);
            e.Property(p => p.Longitude).HasColumnName("LONGITUDE").HasPrecision(9, 6);
        });

        modelBuilder.Entity<Totem>(e =>
        {
            e.ToTable("TOTEM");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(t => t.ParadaId).HasColumnName("PARADA_ID");
            e.Property(t => t.Identificador).HasColumnName("IDENTIFICADOR").HasMaxLength(30);
            e.Property(t => t.TokenHash).HasColumnName("TOKEN_HASH").HasMaxLength(64).IsFixedLength();
            e.Property(t => t.Status).HasColumnName("STATUS").HasMaxLength(10).HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<StatusTotem>(v, true));
            e.Property(t => t.UltimoSinalEm).HasColumnName("ULTIMO_SINAL_EM");
            e.Property(t => t.CriadoEm).HasColumnName("CRIADO_EM");
            e.HasIndex(t => t.Identificador).IsUnique();
            e.HasOne(t => t.Parada).WithMany().HasForeignKey(t => t.ParadaId);
        });

        modelBuilder.Entity<SinalTotem>(e =>
        {
            e.ToTable("SINAL_TOTEM");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(s => s.TotemId).HasColumnName("TOTEM_ID");
            e.Property(s => s.ChaveIdempotencia).HasColumnName("CHAVE_IDEMPOTENCIA").HasMaxLength(80);
            e.Property(s => s.Tipo).HasColumnName("TIPO").HasMaxLength(10).HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<TipoSinal>(v, true));
            e.Property(s => s.UidHash).HasColumnName("UID_HASH").HasMaxLength(64).IsFixedLength();
            e.Property(s => s.SolicitacaoId).HasColumnName("SOLICITACAO_ID");
            e.Property(s => s.RecebidoEm).HasColumnName("RECEBIDO_EM");
            e.HasIndex(s => s.ChaveIdempotencia).IsUnique();
            e.HasOne(s => s.Totem).WithMany().HasForeignKey(s => s.TotemId);
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("USUARIO");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(u => u.Nome).HasColumnName("NOME").HasMaxLength(120);
            e.Property(u => u.Email).HasColumnName("EMAIL").HasMaxLength(160);
            e.Property(u => u.SenhaHash).HasColumnName("SENHA_HASH").HasMaxLength(100);
            e.Property(u => u.Perfil).HasColumnName("PERFIL").HasMaxLength(10);
            e.HasIndex(u => u.Email).IsUnique();
        });
    }
}

/// <summary>Grava em UTC e lê marcando Kind=Utc.</summary>
public sealed class DataUtcConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
