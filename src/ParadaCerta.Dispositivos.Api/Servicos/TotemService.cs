using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Api.Servicos;

public enum ResultadoTotemTipo { Ok, NaoEncontrado, Invalido, Duplicado }

public sealed record ResultadoTotem<T>(ResultadoTotemTipo Tipo, T? Valor = default, Dictionary<string, string[]>? Erros = null);

/// <summary>Gestão de totens pelo GESTOR (RF16): criar, ativar/desativar, trocar segredo e consultar estado.</summary>
public sealed partial class TotemService(
    ParadaCertaDbContext db,
    IRegistroEventos eventos,
    TimeProvider relogio,
    ILogger<TotemService> logger)
{
    /// <summary>Sem heartbeat há mais tempo que isso, o totem aparece como offline.</summary>
    public static readonly TimeSpan LimiteOnline = TimeSpan.FromMinutes(3);

    public async Task<IReadOnlyList<TotemResponse>> ListarAsync(StatusTotem? status, CancellationToken ct)
    {
        var consulta = db.Totens.AsNoTracking().Include(t => t.Parada).AsQueryable();
        if (status is not null) consulta = consulta.Where(t => t.Status == status);

        var totens = await consulta.OrderBy(t => t.Identificador).ToListAsync(ct);
        return totens.Select(ParaResposta).ToList();
    }

    public async Task<TotemDetalheResponse?> ObterAsync(long id, CancellationToken ct)
    {
        var totem = await db.Totens.AsNoTracking().Include(t => t.Parada).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (totem is null) return null;

        DateTime? ultimoHeartbeat = null;
        try
        {
            ultimoHeartbeat = await eventos.UltimoHeartbeatAsync(totem.Identificador, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível consultar o último heartbeat de {Totem}.", totem.Identificador);
        }

        var online = ultimoHeartbeat is not null
                     && relogio.GetUtcNow().UtcDateTime - ultimoHeartbeat.Value <= LimiteOnline;

        return new TotemDetalheResponse(
            totem.Id, totem.Identificador, totem.ParadaId, totem.Parada?.Nome, ParaTexto(totem.Status),
            totem.UltimoSinalEm, ultimoHeartbeat, online, totem.CriadoEm);
    }

    public async Task<ResultadoTotem<TotemComSegredoResponse>> CriarAsync(CriarTotemRequest request, CancellationToken ct)
    {
        var identificador = request.Identificador?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!IdentificadorValido().IsMatch(identificador))
            return Invalido<TotemComSegredoResponse>(nameof(request.Identificador),
                "Use de 3 a 30 caracteres: letras, números e '-' (ex.: TOTEM-0001).");

        var parada = await db.Paradas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ParadaId, ct);
        if (parada is null)
            return Invalido<TotemComSegredoResponse>(nameof(request.ParadaId), "Parada não encontrada.");

        if (await db.Totens.AnyAsync(t => t.Identificador == identificador, ct))
            return new ResultadoTotem<TotemComSegredoResponse>(ResultadoTotemTipo.Duplicado);

        var segredo = HashSegredo.GerarSegredo();
        var totem = new Totem
        {
            Identificador = identificador,
            ParadaId = parada.Id,
            TokenHash = HashSegredo.Calcular(segredo),
            Status = StatusTotem.Ativo,
            CriadoEm = relogio.AgoraParaOBanco()
        };
        db.Totens.Add(totem);
        await db.SaveChangesAsync(ct);

        totem.Parada = parada;
        return new ResultadoTotem<TotemComSegredoResponse>(ResultadoTotemTipo.Ok, new(ParaResposta(totem), segredo));
    }

    public async Task<ResultadoTotem<TotemResponse>> AlterarStatusAsync(long id, AlterarStatusTotemRequest request, CancellationToken ct)
    {
        var novoStatus = ConverterStatus(request.Status);
        if (novoStatus is null)
            return Invalido<TotemResponse>(nameof(request.Status), "status deve ser ATIVO ou INATIVO.");

        var totem = await db.Totens.Include(t => t.Parada).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (totem is null) return new ResultadoTotem<TotemResponse>(ResultadoTotemTipo.NaoEncontrado);

        totem.Status = novoStatus.Value;
        await db.SaveChangesAsync(ct);
        return new ResultadoTotem<TotemResponse>(ResultadoTotemTipo.Ok, ParaResposta(totem));
    }

    /// <summary>Gera um novo segredo; o anterior para de funcionar no próximo login do totem.</summary>
    public async Task<ResultadoTotem<TotemComSegredoResponse>> RenovarSegredoAsync(long id, CancellationToken ct)
    {
        var totem = await db.Totens.Include(t => t.Parada).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (totem is null) return new ResultadoTotem<TotemComSegredoResponse>(ResultadoTotemTipo.NaoEncontrado);

        var segredo = HashSegredo.GerarSegredo();
        totem.TokenHash = HashSegredo.Calcular(segredo);
        await db.SaveChangesAsync(ct);
        return new ResultadoTotem<TotemComSegredoResponse>(ResultadoTotemTipo.Ok, new(ParaResposta(totem), segredo));
    }

    public Task<bool> ExisteAsync(long id, CancellationToken ct) => db.Totens.AnyAsync(t => t.Id == id, ct);

    public static StatusTotem? ConverterStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "ATIVO" => StatusTotem.Ativo,
        "INATIVO" => StatusTotem.Inativo,
        _ => null
    };

    private static TotemResponse ParaResposta(Totem t) =>
        new(t.Id, t.Identificador, t.ParadaId, t.Parada?.Nome, ParaTexto(t.Status), t.UltimoSinalEm, t.CriadoEm);

    private static string ParaTexto(StatusTotem status) => status.ToString().ToUpperInvariant();

    private static ResultadoTotem<T> Invalido<T>(string campo, string mensagem) =>
        new(ResultadoTotemTipo.Invalido, Erros: new() { [campo] = [mensagem] });

    [GeneratedRegex("^[A-Z0-9-]{3,30}$")]
    private static partial Regex IdentificadorValido();
}
