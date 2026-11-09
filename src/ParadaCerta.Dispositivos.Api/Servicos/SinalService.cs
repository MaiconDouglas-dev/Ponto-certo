using Microsoft.EntityFrameworkCore;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Mensageria;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Api.Servicos;

public enum ResultadoSinalTipo
{
    /// <summary>Sinal novo: gravado e publicado (201).</summary>
    Criado,
    /// <summary>Mesma chave e mesmo conteúdo: devolve o sinal já gravado, sem duplicar (200).</summary>
    Repetido,
    /// <summary>Chave já usada com outro totem ou outro conteúdo (409).</summary>
    ChaveEmConflito,
    TotemNaoEncontrado,
    TotemInativo,
    /// <summary>Não foi possível publicar no RabbitMQ; nada fica gravado e o totem pode reenviar (503).</summary>
    MensageriaIndisponivel
}

public sealed record ResultadoSinal(ResultadoSinalTipo Tipo, SinalResponse? Sinal = null);

public sealed class SinalService(
    ParadaCertaDbContext db,
    IHashCartao hashCartao,
    IPublicadorSinais publicador,
    IRegistroEventos eventos,
    TimeProvider relogio,
    ILogger<SinalService> logger)
{
    /// <summary>
    /// Fluxo do sinal: idempotência → totem ativo → grava SINAL_TOTEM → publica na fila → registra evento.
    /// A entrada já deve estar validada pelo <see cref="SinalValidator"/>.
    /// </summary>
    public async Task<ResultadoSinal> ReceberAsync(SinalRequest request, string chaveIdempotencia, CancellationToken ct)
    {
        var tipo = SinalValidator.ConverterTipo(request.Tipo)!.Value;
        var uidHash = tipo == TipoSinal.Cartao ? hashCartao.Calcular(request.UidCartao!) : null;

        // 1) Idempotência: a chave já foi processada?
        var existente = await BuscarPorChaveAsync(chaveIdempotencia, ct);
        if (existente is not null)
            return await ResponderRepeticaoAsync(existente, request.TotemId, tipo, uidHash, ct);

        // 2) O totem existe e está ativo? (o gestor pode ter desativado depois de emitir o token)
        var totem = await db.Totens.FirstOrDefaultAsync(t => t.Identificador == request.TotemId, ct);
        if (totem is null) return new ResultadoSinal(ResultadoSinalTipo.TotemNaoEncontrado);
        if (totem.Status != StatusTotem.Ativo) return new ResultadoSinal(ResultadoSinalTipo.TotemInativo);

        // 3) Grava o sinal. O UNIQUE de CHAVE_IDEMPOTENCIA protege contra dois envios simultâneos.
        var agora = relogio.AgoraParaOBanco();
        var sinal = new SinalTotem
        {
            TotemId = totem.Id,
            ChaveIdempotencia = chaveIdempotencia,
            Tipo = tipo,
            UidHash = uidHash,
            RecebidoEm = agora
        };
        totem.UltimoSinalEm = agora;
        db.Sinais.Add(sinal);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Outra requisição com a mesma chave pode ter gravado primeiro (ORA-00001): trata como repetição.
            db.ChangeTracker.Clear();
            var vencedor = await BuscarPorChaveAsync(chaveIdempotencia, ct);
            if (vencedor is null) throw;
            return await ResponderRepeticaoAsync(vencedor, request.TotemId, tipo, uidHash, ct);
        }

        // 4) Publica para a API Java. Se falhar, desfaz o registro para o totem poder reenviar com a mesma chave.
        try
        {
            await publicador.PublicarAsync(new MensagemSinalTotem(
                sinal.Id, sinal.ChaveIdempotencia, totem.Id, totem.Identificador, totem.ParadaId,
                ParaTexto(tipo), uidHash, request.DataHora, sinal.RecebidoEm), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao publicar o sinal {Chave} na fila. O registro será desfeito.", chaveIdempotencia);
            db.Sinais.Remove(sinal);
            await db.SaveChangesAsync(CancellationToken.None);
            return new ResultadoSinal(ResultadoSinalTipo.MensageriaIndisponivel);
        }

        await RegistrarEventoAsync(TiposEvento.SinalRecebido, totem.Identificador, sinal, ct);
        return new ResultadoSinal(ResultadoSinalTipo.Criado, ParaResposta(sinal, totem.Identificador));
    }

    public async Task<SinalResponse?> ObterAsync(long id, CancellationToken ct)
    {
        var sinal = await db.Sinais.AsNoTracking().Include(s => s.Totem).FirstOrDefaultAsync(s => s.Id == id, ct);
        return sinal is null ? null : ParaResposta(sinal, sinal.Totem!.Identificador);
    }

    public async Task<IReadOnlyList<SinalResponse>> ListarPorTotemAsync(long totemId, int limite, CancellationToken ct)
    {
        var sinais = await db.Sinais.AsNoTracking()
            .Include(s => s.Totem)
            .Where(s => s.TotemId == totemId)
            .OrderByDescending(s => s.RecebidoEm)
            .Take(limite)
            .ToListAsync(ct);

        return sinais.Select(s => ParaResposta(s, s.Totem!.Identificador)).ToList();
    }

    private async Task<ResultadoSinal> ResponderRepeticaoAsync(
        SinalTotem existente, string totemId, TipoSinal tipo, string? uidHash, CancellationToken ct)
    {
        var mesmoConteudo = existente.Totem!.Identificador == totemId
                            && existente.Tipo == tipo
                            && existente.UidHash?.Trim() == uidHash;
        if (!mesmoConteudo) return new ResultadoSinal(ResultadoSinalTipo.ChaveEmConflito);

        await RegistrarEventoAsync(TiposEvento.SinalRepetido, totemId, existente, ct);
        return new ResultadoSinal(ResultadoSinalTipo.Repetido, ParaResposta(existente, totemId));
    }

    private Task<SinalTotem?> BuscarPorChaveAsync(string chave, CancellationToken ct) =>
        db.Sinais.AsNoTracking().Include(s => s.Totem).FirstOrDefaultAsync(s => s.ChaveIdempotencia == chave, ct);

    /// <summary>O MongoDB é só telemetria: se estiver fora do ar, o sinal continua valendo.</summary>
    private async Task RegistrarEventoAsync(string tipoEvento, string totemIdentificador, SinalTotem sinal, CancellationToken ct)
    {
        try
        {
            await eventos.RegistrarAsync(new EventoTotem
            {
                Tipo = tipoEvento,
                TotemIdentificador = totemIdentificador,
                OcorridoEm = relogio.GetUtcNow().UtcDateTime,
                SinalId = sinal.Id,
                ChaveIdempotencia = sinal.ChaveIdempotencia,
                TipoSinal = ParaTexto(sinal.Tipo)
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível registrar o evento {Tipo} no MongoDB.", tipoEvento);
        }
    }

    private static SinalResponse ParaResposta(SinalTotem s, string totemIdentificador) =>
        new(s.Id, s.ChaveIdempotencia, totemIdentificador, ParaTexto(s.Tipo), s.RecebidoEm, s.SolicitacaoId);

    private static string ParaTexto(TipoSinal tipo) => tipo.ToString().ToUpperInvariant();
}
