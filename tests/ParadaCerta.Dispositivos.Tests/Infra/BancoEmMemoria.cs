using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ParadaCerta.Dispositivos.Api.Dados;

namespace ParadaCerta.Dispositivos.Tests.Infra;

/// <summary>
/// SQLite em memória no lugar do Oracle. Diferente do provider InMemory do EF,
/// o SQLite respeita índices UNIQUE, necessários para testar a idempotência.
/// </summary>
public sealed class BancoEmMemoria : IDisposable
{
    public SqliteConnection Conexao { get; } = new("DataSource=:memory:");

    public BancoEmMemoria()
    {
        Conexao.Open();
        using var db = CriarContexto();
        db.Database.EnsureCreated();
        DadosDeTeste.Popular(db);
    }

    public ParadaCertaDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<ParadaCertaDbContext>().UseSqlite(Conexao).Options);

    public void Dispose() => Conexao.Dispose();
}
